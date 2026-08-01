using System;
using System.IO;

namespace OctoTask.Core.Registry
{
    internal static class TaskmgrHook
    {
        private const string IfgoKeyPath =
            @"SOFTWARE\Microsoft\Windows NT\CurrentVersion\Image File Execution Options\taskmgr.exe";
        private const string DebuggerValueName = "Debugger";
        private const string BackupFileName = "taskmgr_backup.reg";

        public static string BackupFilePath => Path.Combine(AppContext.BaseDirectory, BackupFileName);

        /// <summary>
        /// Installs the IFEO hook so that launching taskmgr.exe launches OctoTask instead.
        /// Automatically backs up the existing Debugger value.
        /// Requires administrator privileges.
        /// </summary>
        public static bool Install(string octoTaskExePath)
        {
            try
            {
                // Read current value BEFORE installing
                string? existingDebugger = GetCurrentDebuggerValue();

                // Backup if there's an existing value and no backup exists yet
                if (!string.IsNullOrEmpty(existingDebugger) && !File.Exists(BackupFilePath))
                {
                    Backup(existingDebugger);
                }

                using var key = Microsoft.Win32.Registry.LocalMachine.CreateSubKey(IfgoKeyPath, writable: true);
                if (key == null)
                    return false;

                key.SetValue(DebuggerValueName, octoTaskExePath, Microsoft.Win32.RegistryValueKind.String);
                return true;
            }
            catch (UnauthorizedAccessException)
            {
                return false;
            }
            catch (Exception)
            {
                return false;
            }
        }

        /// <summary>
        /// Removes the IFEO hook for taskmgr.exe and deletes the backup.
        /// Requires administrator privileges.
        /// </summary>
        public static bool Uninstall()
        {
            try
            {
                using var key = Microsoft.Win32.Registry.LocalMachine.OpenSubKey(IfgoKeyPath, writable: true);
                if (key == null)
                    return true; // Already uninstalled

                key.DeleteValue(DebuggerValueName, throwOnMissingValue: false);

                // Check if the key has any remaining subkeys or values
                if (key.SubKeyCount == 0 && key.ValueCount == 0)
                {
                    key.Close();
                    Microsoft.Win32.Registry.LocalMachine.DeleteSubKey(IfgoKeyPath);
                }

                // Delete backup file
                if (File.Exists(BackupFilePath))
                    File.Delete(BackupFilePath);

                return true;
            }
            catch (UnauthorizedAccessException)
            {
                return false;
            }
            catch (Exception)
            {
                return false;
            }
        }

        /// <summary>
        /// Restores the original Task Manager by applying the backup Debugger value.
        /// If no backup exists, simply removes the Debugger value.
        /// </summary>
        public static bool Restore()
        {
            try
            {
                if (!File.Exists(BackupFilePath))
                {
                    // No backup — just remove the hook
                    return Uninstall();
                }

                string backupContent = File.ReadAllText(BackupFilePath);
                string? debuggerValue = ParseRegFile(backupContent);
                if (string.IsNullOrEmpty(debuggerValue))
                {
                    Uninstall();
                    return true;
                }

                // Restore by setting the original Debugger value
                using var key = Microsoft.Win32.Registry.LocalMachine.CreateSubKey(IfgoKeyPath, writable: true);
                if (key == null)
                    return false;

                key.SetValue(DebuggerValueName, debuggerValue, Microsoft.Win32.RegistryValueKind.String);

                // Delete backup file
                File.Delete(BackupFilePath);

                return true;
            }
            catch
            {
                return false;
            }
        }

        public static bool HasBackup() => File.Exists(BackupFilePath);

        public static bool IsInstalled(string? octoTaskExePath = null)
        {
            try
            {
                using var key = Microsoft.Win32.Registry.LocalMachine.OpenSubKey(IfgoKeyPath, writable: false);
                if (key == null)
                    return false;

                var debuggerValue = key.GetValue(DebuggerValueName) as string;
                if (string.IsNullOrEmpty(debuggerValue))
                    return false;

                return string.IsNullOrEmpty(octoTaskExePath) ||
                       string.Equals(debuggerValue, octoTaskExePath, StringComparison.OrdinalIgnoreCase);
            }
            catch
            {
                return false;
            }
        }

        public static string? GetCurrentDebuggerValue()
        {
            try
            {
                using var key = Microsoft.Win32.Registry.LocalMachine.OpenSubKey(IfgoKeyPath, writable: false);
                return key?.GetValue(DebuggerValueName) as string;
            }
            catch
            {
                return null;
            }
        }

        private static void Backup(string existingDebuggerValue)
        {
            string regContent = $@"Windows Registry Editor Version 5.00

[HKEY_LOCAL_MACHINE\SOFTWARE\Microsoft\Windows NT\CurrentControlSet\Control\Image File Execution Options\taskmgr.exe]
""Debugger""=""{existingDebuggerValue.Replace(@"\", @"\\")}""
";
            File.WriteAllText(BackupFilePath, regContent);
        }

        private static string? ParseRegFile(string content)
        {
            foreach (var line in content.Split(Environment.NewLine, StringSplitOptions.RemoveEmptyEntries))
            {
                if (line.StartsWith("\"Debugger\"=", StringComparison.Ordinal))
                {
                    string value = line.Substring("\"Debugger\"=".Length).Trim('"');
                    // Unescape .reg format: \\ -> \
                    return value.Replace("\\", "");
                }
            }
            return null;
        }
    }
}

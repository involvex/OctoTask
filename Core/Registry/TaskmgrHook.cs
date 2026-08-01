using System;

namespace OctoTask.Core.Registry
{
    internal static class TaskmgrHook
    {
        private const string IfeoKeyPath =
            @"SOFTWARE\Microsoft\Windows NT\CurrentVersion\Image File Execution Options\taskmgr.exe";

        private const string DebuggerValueName = "Debugger";

        /// <summary>
        /// Installs the IFEO hook so that launching taskmgr.exe launches OctoTask instead.
        /// Requires administrator privileges.
        /// </summary>
        public static bool Install(string octoTaskExePath)
        {
            try
            {
                using var key = Microsoft.Win32.Registry.LocalMachine.CreateSubKey(IfeoKeyPath, writable: true);
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
        /// Removes the IFEO hook for taskmgr.exe.
        /// Requires administrator privileges.
        /// </summary>
        public static bool Uninstall()
        {
            try
            {
                using var key = Microsoft.Win32.Registry.LocalMachine.OpenSubKey(IfeoKeyPath, writable: true);
                if (key == null)
                    return true; // Already uninstalled

                key.DeleteValue(DebuggerValueName, throwOnMissingValue: false);

                // Check if the key has any remaining subkeys or values
                if (key.SubKeyCount == 0 && key.ValueCount == 0)
                {
                    // Key is empty, we can clean it up
                    key.Close();
                    Microsoft.Win32.Registry.LocalMachine.DeleteSubKey(IfeoKeyPath);
                }

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
        /// Checks if the IFEO hook is currently installed and points to OctoTask.
        /// </summary>
        public static bool IsInstalled(string? octoTaskExePath = null)
        {
            try
            {
                using var key = Microsoft.Win32.Registry.LocalMachine.OpenSubKey(IfeoKeyPath, writable: false);
                if (key == null)
                    return false;

                var debuggerValue = key.GetValue(DebuggerValueName) as string;
                if (string.IsNullOrEmpty(debuggerValue))
                    return false;

                // If a path is provided, check if it matches
                return string.IsNullOrEmpty(octoTaskExePath) ||
                       string.Equals(debuggerValue, octoTaskExePath, StringComparison.OrdinalIgnoreCase);
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// Gets the current debugger value (if any) for the taskmgr IFEO key.
        /// </summary>
        public static string? GetCurrentDebuggerValue()
        {
            try
            {
                using var key = Microsoft.Win32.Registry.LocalMachine.OpenSubKey(IfeoKeyPath, writable: false);
                return key?.GetValue(DebuggerValueName) as string;
            }
            catch
            {
                return null;
            }
        }
    }
}

using System;
using System.IO;
using System.Reflection;
using System.Windows;
using OctoTask.Core.Registry;

namespace OctoTask;

internal static class CrashLog
{
    private static readonly string LogPath = Path.Combine(AppContext.BaseDirectory, "crash.log");
    private const long MaxSizeBytes = 1 * 1024 * 1024;
    private const int MaxBackups = 3;

    public static void Write(string message)
    {
        try
        {
            RotateIfNeeded();
            File.AppendAllText(LogPath, $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] {message}{Environment.NewLine}");
        }
        catch
        {
        }
    }

    private static void RotateIfNeeded()
    {
        if (!File.Exists(LogPath))
            return;

        try
        {
            var info = new FileInfo(LogPath);
            if (info.Length < MaxSizeBytes)
                return;
        }
        catch
        {
            return;
        }

        var oldest = $"{LogPath}.{MaxBackups}";
        if (File.Exists(oldest))
            File.Delete(oldest);

        for (int i = MaxBackups - 1; i >= 1; i--)
        {
            var src = $"{LogPath}.{i}";
            var dst = $"{LogPath}.{i + 1}";
            if (File.Exists(src))
                File.Move(src, dst);
        }

        if (File.Exists(LogPath))
            File.Move(LogPath, $"{LogPath}.1");
    }
}

/// <summary>
/// Interaction logic for App.xaml
/// </summary>
public partial class App : Application
{
    public static bool SilentMode { get; private set; }
    public static bool StartMinimized { get; private set; }

    protected override void OnStartup(StartupEventArgs e)
    {
        DispatcherUnhandledException += (_, args) =>
        {
            CrashLog.Write($"Unhandled exception:\n{args.Exception}");
            MessageBox.Show(args.Exception.ToString(), "OctoTask Crash", MessageBoxButton.OK, MessageBoxImage.Error);
            args.Handled = true;
        };
        AppDomain.CurrentDomain.UnhandledException += (_, args) =>
        {
            CrashLog.Write($"AppDomain exception:\n{args.ExceptionObject}");
        };
        TaskScheduler.UnobservedTaskException += (_, args) =>
        {
            CrashLog.Write($"Task exception:\n{args.Exception}");
        };

        base.OnStartup(e);

        SilentMode = e.Args.Contains("--no-ui", StringComparer.OrdinalIgnoreCase) ||
                     e.Args.Contains("--silent", StringComparer.OrdinalIgnoreCase);

        StartMinimized = e.Args.Contains("--minimized", StringComparer.OrdinalIgnoreCase);

        // CLI mode
        if (e.Args.Length > 0)
        {
            HandleCliArgument(e.Args[0]);
        }
    }

    private void HandleCliArgument(string arg)
    {
        var exePath = Assembly.GetEntryAssembly()?.Location ?? "";
        var backupPath = Path.Combine(AppContext.BaseDirectory, "taskmgr_backup.reg");

        switch (arg.ToLowerInvariant())
        {
            case "--install":
                InstallWithBackup(exePath, backupPath);
                Shutdown();
                break;
            case "--uninstall":
                TaskmgrHook.Uninstall();
                Shutdown();
                break;
            case "--restore":
                RestoreFromBackup(backupPath);
                Shutdown();
                break;
            case "--no-ui":
            case "--silent":
                // Continue to normal startup
                ShutdownMode = ShutdownMode.OnExplicitShutdown;
                break;
            default:
                // Normal startup — show UI
                break;
        }
    }

    private static void InstallWithBackup(string exePath, string backupPath)
    {
        try
        {
            // Backup existing value before modifying
            if (TaskmgrHook.GetCurrentDebuggerValue() is string existing && !string.IsNullOrEmpty(existing))
            {
                File.WriteAllText(backupPath, $@"Windows Registry Editor Version 5.00

[HKEY_LOCAL_MACHINE\SOFTWARE\Microsoft\Windows NT\CurrentVersion\Image File Execution Options\taskmgr.exe]
""Debugger""=""{existing.Replace(@"\", @"\\")}""
");
            }

            TaskmgrHook.Install(exePath);
            Console.WriteLine("Install completed.");
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Install failed: {ex.Message}");
            Environment.Exit(1);
        }
    }

    private static void RestoreFromBackup(string backupPath)
    {
        try
        {
            if (!File.Exists(backupPath))
            {
                Console.WriteLine("No backup found. Removing Debugger value.");
                TaskmgrHook.Uninstall();
                return;
            }

            var lines = File.ReadAllLines(backupPath);
            string? debuggerValue = null;
            foreach (var line in lines)
            {
                if (line.StartsWith("\"Debugger\"=", StringComparison.Ordinal))
                {
                    debuggerValue = line.Substring("\"Debugger\"=".Length).Trim('"');
                    debuggerValue = debuggerValue.Replace("\\\\", "\\"); // Undo .reg escaping
                    break;
                }
            }

            if (!string.IsNullOrEmpty(debuggerValue))
                TaskmgrHook.Install(debuggerValue);
            else
                TaskmgrHook.Uninstall();

            File.Delete(backupPath);
            Console.WriteLine("Restore completed.");
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Restore failed: {ex.Message}");
            Environment.Exit(1);
        }
    }
}

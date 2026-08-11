using System;
using System.IO;
using System.Reflection;
using System.Windows;
using OctoTask.Core.Registry;

namespace OctoTask;

/// <summary>
/// Interaction logic for App.xaml
/// </summary>
public partial class App : Application
{
    public static bool SilentMode { get; private set; }

    protected override void OnStartup(StartupEventArgs e)
    {
        DispatcherUnhandledException += (_, args) =>
        {
            File.WriteAllText(Path.Combine(AppContext.BaseDirectory, "crash.log"),
                $"[{DateTime.Now}] Unhandled exception:\n{args.Exception}");
            MessageBox.Show(args.Exception.ToString(), "OctoTask Crash", MessageBoxButton.OK, MessageBoxImage.Error);
            args.Handled = true;
        };
        AppDomain.CurrentDomain.UnhandledException += (_, args) =>
        {
            File.WriteAllText(Path.Combine(AppContext.BaseDirectory, "crash.log"),
                $"[{DateTime.Now}] AppDomain exception:\n{args.ExceptionObject}");
        };
        TaskScheduler.UnobservedTaskException += (_, args) =>
        {
            File.WriteAllText(Path.Combine(AppContext.BaseDirectory, "crash.log"),
                $"[{DateTime.Now}] Task exception:\n{args.Exception}");
        };

        base.OnStartup(e);

        SilentMode = e.Args.Contains("--no-ui", StringComparer.OrdinalIgnoreCase) ||
                     e.Args.Contains("--silent", StringComparer.OrdinalIgnoreCase);

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
                    debuggerValue = debuggerValue.Replace("\\", ""); // Undo .reg escaping
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

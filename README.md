# OctoTask

A modern, dark-themed Task Manager replacement for Windows built with WPF and .NET 10.

## Features

- **Process Management** — View, search, sort, and kill running processes with detailed information including CPU usage, RAM consumption, command line, and executable path
- **System Telemetry Dashboard** — Real-time CPU and RAM usage gauges in the header
- **System Tray Icon** — Dynamic tray icon showing live CPU or RAM usage with a colored progress arc (configurable display mode, minimize-to-tray support)
- **Task Manager Hook** — Replace Windows Task Manager with OctoTask via IFEO registry hook
- **Dark Theme** — Full dark UI with dark title bar via DWM
- **Process Details** — Detailed pane with start time, running time, threads, handles, priority, owner, parent process, file info, loaded modules, and environment variables

## Requirements

- Windows 10/11
- .NET 10.0 SDK
- Administrator privileges (for Task Manager hook installation)

## Build & Run

```powershell
dotnet build -c Release
dotnet run
```

Or build and run the release executable:

```powershell
dotnet publish -c Release --self-contained false
./bin/Release/net10.0-windows/OctoTask.exe
```

## CLI Options

| Option | Description |
|--------|-------------|
| `--install` | Install Task Manager hook (requires admin) |
| `--uninstall` | Remove Task Manager hook (requires admin) |
| `--restore` | Restore original Task Manager from backup |
| `--no-ui` / `--silent` | Start without UI (silent mode) |

## Architecture

```
OctoTask/
├── Core/
│   ├── Models/          # ProcessInfo, ProcessDetails data models
│   ├── Native/          # Win32 P/Invoke interop
│   │   ├── DwmInterop.cs         # Dark title bar
│   │   ├── ProcessInterop.cs     # Process enumeration via PEB reading
│   │   ├── SystemInfo.cs         # RAM info via GlobalMemoryStatusEx
│   │   ├── TrayIconService.cs    # System tray icon management
│   │   └── TrayIconRenderer.cs   # Dynamic icon bitmap generation
│   ├── Registry/        # Task Manager IFEO hook
│   └── Settings/        # App settings persistence
├── UI/
│   ├── ViewModels/      # MVVM ViewModels
│   ├── Views/           # WPF Windows (settings dialogs)
│   └── Converters/      # Value converters
├── MainWindow.xaml      # Main application window
└── App.xaml             # Application entry point
```

## Settings

Tray icon settings are stored in `%APPDATA%\OctoTask\settings.json`:

```json
{
  "trayDisplayMode": "cpu",
  "minimizeToTray": true,
  "updateIntervalMs": 2000
}
```

| Setting | Values | Description |
|---------|--------|-------------|
| `trayDisplayMode` | `cpu`, `ram` | What the tray icon displays |
| `minimizeToTray` | `true`, `false` | Hide to system tray on minimize |
| `updateIntervalMs` | `1000`-`10000` | How often the tray icon refreshes |

## License

MIT

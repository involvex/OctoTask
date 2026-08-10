# OctoTask — Feature Suggestions

## Current State Overview

OctoTask is a WPF-based dark-mode process manager targeting Windows, built on .NET 10. It replaces the default Task Manager via the Image File Execution Options (IFEO) registry hook, reads process information directly from the PEB via P/Invoke, includes a full MVVM architecture with auto-refresh, column sorting, process termination, a system tray icon with dynamic usage display, and a settings dialog. Installer scripts and a restore mechanism are included.

### Architecture Summary
- **UI Layer**: WPF with custom dark theme (`#0f172a` terminal aesthetic)
- **Native Layer**: `ProcessInterop.cs` (P/Invoke for `NtQueryInformationProcess`, `ReadProcessMemory`), `DwmInterop.cs` (dark title bar), `TrayIconService.cs` (system tray), `TrayIconRenderer.cs` (dynamic icon generation)
- **Core Layer**: `ProcessInfo`/`ProcessDetails` models, `TaskmgrHook` registry management, `AppSettings` persistence
- **Deployment**: PowerShell scripts for install/uninstall/restore with `.reg` backup

---

## Completed

| # | Feature | Notes |
|---|---------|-------|
| 2 | Search / Filter Bar | Live filtering by name, PID, executable path, command line |
| 3 | CPU Usage Column | Per-process CPU % via TotalProcessorTime sampling |
| 4 | Process Details Pane | Side panel with basic info, owner, parent, file info, modules, environment variables |
| 5 | System Resource Gauges | CPU and RAM progress bars in header dashboard |
| — | System Tray Icon | Dynamic icon with progress arc, configurable CPU/RAM display, minimize-to-tray, settings dialog |

---

## Priority: High

### 1. Process Tree View
**Status**: Not started
**Description**: Add a hierarchical view showing parent-child process relationships.
**Why**: The current DataGrid is flat — users often need to understand which processes spawned others.
**How**: Extend `ProcessInfo` with `ParentPid`, build a tree structure in `MainViewModel`, switch between flat DataGrid and TreeView via a toggle.

### 7. Startup / Auto-Launch Management
**Status**: Not started
**Description**: Add a "Startup" view listing processes with autostart entries (registry, task scheduler, startup folders).
**Why**: Many users want to manage what runs at boot.
**How**: Query `HKCU\Software\Microsoft\Windows\CurrentVersion\Run`, `HKLM\...Run`, Task Scheduler API (`Microsoft.Win32.TaskScheduler` NuGet), and `Startup` folder.

### 8. Process Suspension / Resumption
**Status**: Not started
**Description**: Allow users to suspend and resume individual processes (like Process Explorer).
**Why**: Temporarily halting a process without killing it can be useful for troubleshooting.
**How**: P/Invoke `NtSuspendProcess` / `NtResumeProcess` from `ntdll.dll`.

---

## Priority: Medium

### 5b. System Resource History Graphs
**Status**: Gauges done, history graphs pending
**Description**: Add live line charts showing CPU, Memory, Disk, and Network usage over time.
**Why**: Gauges show current state — graphs show trends and spikes.
**How**: Use WPF `Polyline` in a `Canvas` or lightweight charting. Poll `PerformanceCounter` every 500ms-1s.

### 6. Network Monitoring per Process
**Status**: Not started
**Description**: Show network activity (send/receive bytes per second) per process.
**Why**: Network-based malware or runaway downloads are common suspects.
**How**: P/Invoke `GetExtendedTcpTable` and `GetExtendedUdpTable` from `iphlpapi.dll`, or use `System.Net.NetworkInformation` for global stats. Map connections to PIDs.

### 9. Export to CSV / JSON
**Status**: Not started
**Description**: Add a button to export the current process list to CSV or JSON.
**Why**: Useful for documentation, sharing, or offline analysis.
**How**: Serialize `Processes` collection using `System.Text.Json` or CSV writer.

### 10. Dark/Light Theme Toggle
**Status**: Not started
**Description**: Allow switching between dark terminal theme and a light theme.
**Why**: Some users prefer light themes in well-lit environments.
**How**: Move color resources to a theme dictionary, add toggle in settings. Re-apply DWM title bar color based on choice.

### 12. Column Customization
**Status**: Not started
**Description**: Let users show/hide columns, reorder them, and resize.
**Why**: Different workflows prefer different columns.
**How**: Use `DataGridColumn Visibility` bindings, allow drag-drop reordering, persist column layout in settings.

### 13. Keyboard Shortcuts
**Status**: Not started
**Description**: Add global hotkeys for common actions (Ctrl+R for refresh, Ctrl+K for kill, Ctrl+F for search).
**Why**: Terminal-oriented users expect keyboard-first workflows.
**How**: Register global hotkeys via `RegisterHotKey` from `user32.dll`, or use WPF `InputBinding`s within the window.

---

## Priority: Low / Future

### 11. Portable Mode
**Status**: Not started
**Description**: Allow running OctoTask without installation — all state stored locally in the app directory.
**Why**: Some users (especially power users, sysadmins) prefer not to install software.
**How**: Detect a `portable.flag` file or `--portable` CLI flag. Store backup `.reg` file and settings next to the executable instead of using `%APPDATA%` or `Program Files`.

### 14. Metrics Overlay (Like MSI Afterburner for processes)
**Status**: Not started
**Description**: A minimal always-on-top overlay showing selected process's real-time CPU/RAM usage.
**Why**: Useful during gaming or performance testing to monitor a background process.
**How**: A borderless, transparent, click-through `WPF` window bound to a single selected process.

### 15. Service Management
**Status**: Not started
**Description**: Show Windows services and allow starting/stopping/recycling them.
**Why**: Many admins use Task Manager to manage services quickly.
**How**: Query `ServiceController.GetServices()`, create a separate or toggleable view for services.

### 16. Multi-Language / Localization
**Status**: Not started
**Description**: Localize the UI into multiple languages.
**Why**: Wider adoption in non-English environments.
**How**: Use `.resx` resource files, add a language selector in settings.

---

## Technical Debt / Code Quality

### 17. Unit Tests
**Description**: Add unit tests for `ProcessInfo.FormatBytes`, `TaskmgrHook` registry logic (mockable), `MainViewModel` command bindings, and `RelayCommand`.
**Why**: Ensures reliability as the codebase grows.
**How**: Add `xUnit` test project, restructure code to allow mocking.

### 18. Logging
**Description**: Add structured logging (e.g., file-based or event log) for diagnostics.
**Why**: When things go wrong with P/Invoke calls or registry access, logs help debug.
**How**: Add `Serilog` or `NLog` NuGet, log at key points (process enumeration, hook install/restore).

### 19. Migrate to ReactiveUI
**Description**: Replace the hand-rolled MVVM pattern with [ReactiveUI](https://reactiveui.net/).
**Why**: Cleaner async data flows, less manual `INotifyPropertyChanged` boilerplate, better testability.
**How**: Replace `INotifyPropertyChanged` with `ReactiveObject`, use `ReactiveCommand`, leverage `WhenAnyValue` for property changes.

### 20. Modern Windows App SDK / WinUI 3
**Description**: Migrate from WPF to [Windows App SDK](https://learn.microsoft.com/windows/apps/windows-app-sdk/) (WinUI 3) for better future-proofing and native Windows 11 integration.
**Why**: WPF is in maintenance mode — WinUI 3 is Microsoft's recommended desktop UI stack going forward.
**How**: Rewrite UI in WinUI 3, reuse `Core` and `Native` logic as-is. This is a significant effort but aligns with the platform's roadmap.

---

## Security Considerations

### 21. Code Signing
**Description**: Sign the executable with a code-signing certificate.
**Why**: Running as admin + hooking Task Manager requires users to trust the app. Unsigned executables trigger SmartScreen warnings.
**How**: Obtain an EV code-signing cert, integrate `SignTool` into the build pipeline.

### 22. Integrity Check / Self-Protection
**Description**: Add a feature to verify the app's own integrity or guard against tampering.
**Why**: As a Task Manager replacement running as admin, it could be a target for attack.
**How**: Hash the executable on startup, optionally compare against a known-good hash, or use Windows Defender Application Control (WDAC) policies.

### 23. ETW (Event Tracing Export)
**Description**: Instead of (or in addition to) polling processes, use Event Tracing for Windows (ETW) to receive real-time process start/stop events.
**Why**: Much more efficient than polling every 5 seconds; catches transient processes.
**How**: Use `System.Diagnostics.Tracing.EventListener` or the `Microsoft.Diagnostics.Tracing` (TraceEvent) NuGet package.

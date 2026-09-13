# OctoTask — Feature Suggestions

## Current State Overview

OctoTask is a WPF-based dark-mode process manager targeting Windows, built on .NET 10. It replaces the default Task Manager via the Image File Execution Options (IFEO) registry hook, reads process information directly from the PEB via P/Invoke, includes a full MVVM architecture with auto-refresh, column sorting, process termination, suspend/resume, a system tray icon with dynamic usage display, a process tree view, network monitoring with bandwidth tracking, and a settings dialog.

### Architecture Summary
- **UI Layer**: WPF with custom dark theme (`#0f172a` terminal aesthetic)
- **Native Layer**: `ProcessInterop.cs` (P/Invoke), `DwmInterop.cs` (dark title bar), `NetworkInterop.cs` (TCP/UDP table), `TrayIconService.cs` (system tray), `TrayIconRenderer.cs` (dynamic icon generation), `GlobalHotKey.cs` (system-wide hotkeys)
- **Core Layer**: `ProcessInfo`/`ProcessDetails`/`ConnectionInfo` models, `TaskmgrHook` registry management, `AppSettings` persistence
- **Deployment**: PowerShell scripts for install/uninstall/restore with `.reg` backup

---

## Completed

| # | Feature | Notes |
|---|---------|-------|
| 2 | Search / Filter Bar | Live filtering by name, PID, executable path, command line with debounce |
| 3 | CPU Usage Column | Per-process CPU % via TotalProcessorTime sampling |
| 4 | Process Details Pane | Side panel with basic info, owner, parent, file info, modules, environment variables |
| 5 | System Resource Gauges | CPU and RAM progress bars in header dashboard |
| — | System Tray Icon | Dynamic icon with progress arc, configurable CPU/RAM display, minimize-to-tray, settings dialog |
| — | Process Tree View | Hierarchical parent-child view with toggle button (BuildProcessTree now called in refresh) |
| — | Process Suspend / Resume | NtSuspendProcess / NtResumeProcess via P/Invoke |
| — | Export CSV / JSON | SaveFileDialog with formatted export |
| — | App Icon & Branding | Multi-size .ico embedded in EXE and window title bar |
| — | Search Highlight | Matched text highlighted brown in search columns via SearchHighlightConverter |
| — | Refresh on Focus | Auto-refresh when window activated (5s cooldown to avoid spam) |
| — | Port Search Filter | Filter by port, protocol, process name, address with debounce |
| — | Context Menu: Go to Ports | Right-click process → switch to Ports tab filtered by PID |
| — | Context Menu: Copy Port/Address | Copy connection details from Ports tab context menu |
| — | Tray Icon Tooltip | Shows CPU/RAM + process count |
| — | Global Keyboard Shortcuts | Ctrl+Shift+R (Refresh), Ctrl+Shift+K (Kill), Ctrl+Shift+S (Suspend) |

---

## Priority: High

### 1. Startup / Auto-Launch Management
**Status**: Not started
**Description**: Add a "Startup" view listing processes with autostart entries (registry, task scheduler, startup folders).
**Why**: Many users want to manage what runs at boot.
**How**: Query `HKCU\Software\Microsoft\Windows\CurrentVersion\Run`, `HKLM\...Run`, Task Scheduler API (`Microsoft.Win32.TaskScheduler` NuGet), and `Startup` folder.

### 2. System Resource History Graphs
**Status**: Gauges done, history graphs pending
**Description**: Add live line charts showing CPU, Memory, Disk, and Network usage over time.
**Why**: Gauges show current state — graphs show trends and spikes.
**How**: Use WPF `Polyline` in a `Canvas` or lightweight charting. Poll `PerformanceCounter` every 500ms-1s.

### 3. Network Bandwidth per Process
**Status**: Done
**Description**: Show network activity (connection rate per second) per process in the Ports tab.
**Why**: Network-based malware or runaway downloads are common suspects.
**How**: Snapshot TCP connections at each refresh, compute per-PID connection rate (connections/sec) from deltas, display in Ports tab DataGrid.

---

## Priority: Medium

### 4. Column Customization
**Status**: Partially done (widths + sort persisted)
**Description**: Let users show/hide columns, reorder them, and resize.
**Why**: Different workflows prefer different columns.
**How**: Use `DataGridColumn Visibility` bindings, allow drag-drop reordering, persist column layout in settings.

### 5. Keyboard Shortcuts
**Status**: Done (Ctrl+R, Ctrl+K, Ctrl+F, Ctrl+S in-window + Ctrl+Shift+R/K/S global)
**Description**: Global hotkeys for common actions implemented.
**Why**: Terminal-oriented users expect keyboard-first workflows.
**How**: `GlobalHotKey.cs` in Core/Native using `RegisterHotKey` P/Invoke.

### 6. Dark/Light Theme Toggle
**Status**: Not started
**Description**: Allow switching between dark terminal theme and a light theme.
**Why**: Some users prefer light themes in well-lit environments.
**How**: Move color resources to a theme dictionary, add toggle in settings. Re-apply DWM title bar color based on choice.

### 7. Portable Mode
**Status**: Not started
**Description**: Allow running OctoTask without installation — all state stored locally in the app directory.
**Why**: Some users (especially power users, sysadmins) prefer not to install software.
**How**: Detect a `portable.flag` file or `--portable` CLI flag. Store backup `.reg` file and settings next to the executable instead of using `%APPDATA%` or `Program Files`.

---

## Quick Wins

Low-effort, high-value improvements that can be done in a single session.

All quick wins (Q1–Q6) are completed.

---

## Priority: Low / Future

### 8. Metrics Overlay (Like MSI Afterburner for processes)
**Status**: Not started
**Description**: A minimal always-on-top overlay showing selected process's real-time CPU/RAM usage.
**Why**: Useful during gaming or performance testing to monitor a background process.
**How**: A borderless, transparent, click-through `WPF` window bound to a single selected process.

### 9. Service Management
**Status**: Not started
**Description**: Show Windows services and allow starting/stopping/recycling them.
**Why**: Many admins use Task Manager to manage services quickly.
**How**: Query `ServiceController.GetServices()`, create a separate or toggleable view for services.

### 10. Multi-Language / Localization
**Status**: Not started
**Description**: Localize the UI into multiple languages.
**Why**: Wider adoption in non-English environments.
**How**: Use `.resx` resource files, add a language selector in settings.

---

## Technical Debt / Code Quality

### 11. Unit Tests
**Description**: Add unit tests for `ProcessInfo.FormatBytes`, `TaskmgrHook` registry logic (mockable), `MainViewModel` command bindings, and `RelayCommand`.
**Why**: Ensures reliability as the codebase grows.
**How**: Add `xUnit` test project, restructure code to allow mocking.

### 12. Logging
**Description**: Add structured logging (e.g., file-based or event log) for diagnostics.
**Why**: When things go wrong with P/Invoke calls or registry access, logs help debug.
**How**: Add `Serilog` or `NLog` NuGet, log at key points (process enumeration, hook install/restore).

### 13. Migrate to ReactiveUI
**Description**: Replace the hand-rolled MVVM pattern with [ReactiveUI](https://reactiveui.net/).
**Why**: Cleaner async data flows, less manual `INotifyPropertyChanged` boilerplate, better testability.
**How**: Replace `INotifyPropertyChanged` with `ReactiveObject`, use `ReactiveCommand`, leverage `WhenAnyValue` for property changes.

### 14. Modern Windows App SDK / WinUI 3
**Description**: Migrate from WPF to [Windows App SDK](https://learn.microsoft.com/windows/apps/windows-app-sdk/) (WinUI 3) for better future-proofing and native Windows 11 integration.
**Why**: WPF is in maintenance mode — WinUI 3 is Microsoft's recommended desktop UI stack going forward.
**How**: Rewrite UI in WinUI 3, reuse `Core` and `Native` logic as-is. This is a significant effort but aligns with the platform's roadmap.

---

## Security Considerations

### 15. Code Signing
**Description**: Sign the executable with a code-signing certificate.
**Why**: Running as admin + hooking Task Manager requires users to trust the app. Unsigned executables trigger SmartScreen warnings.
**How**: Obtain an EV code-signing cert, integrate `SignTool` into the build pipeline.

### 16. Integrity Check / Self-Protection
**Description**: Add a feature to verify the app's own integrity or guard against tampering.
**Why**: As a Task Manager replacement running as admin, it could be a target for attack.
**How**: Hash the executable on startup, optionally compare against a known-good hash, or use Windows Defender Application Control (WDAC) policies.

### 17. ETW (Event Tracing Export)
**Description**: Instead of (or in addition to) polling processes, use Event Tracing for Windows (ETW) to receive real-time process start/stop events.
**Why**: Much more efficient than polling every 5 seconds; catches transient processes.
**How**: Use `System.Diagnostics.Tracing.EventListener` or the `Microsoft.Diagnostics.Tracing` (TraceEvent) NuGet package.

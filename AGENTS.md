# AGENTS.md — OctoTask

This file provides guidance to AI coding agents (Claude, Copilot, Gemini, etc.) working on the **OctoTask** codebase. It is the single source of truth for project commands, architecture, and conventions. When in doubt, follow these rules.

---

## Table of Contents

1. [Project Overview](#project-overview)
2. [Useful Commands](#useful-commands)
3. [Technologies](#technologies)
4. [Architecture & Directory Structure](#architecture--directory-structure)
5. [Best Practices & Guidelines](#best-practices--guidelines)
6. [Security Considerations](#security-considerations)
7. [Testing & Quality](#testing--quality)
8. [Git Workflow](#git-workflow)

---

## Project Overview

**OctoTask** is a modern, dark-themed Task Manager replacement for Windows, built with **WPF** and **.NET 10**. It provides real-time process monitoring, system telemetry, and a system tray icon with dynamic CPU/RAM usage display. It can replace Windows Task Manager via an IFEO registry hook.

### Key Features
- Process enumeration with detailed command-line and executable path extraction via PEB reading
- Dark-themed UI with dark title bar (DWM)
- System tray icon with live usage arc
- Task Manager hook (IFEO)
- MVVM architecture with pure `INotifyPropertyChanged`

---

## Useful Commands

### Build & Run

```powershell
# Build the project (Release)
dotnet build -c Release

# Build with warnings as errors
dotnet build -c Release /warnaserror

# Run directly
dotnet run

# Build and run Release executable
dotnet publish -c Release --self-contained false
./bin/Release/net10.0-windows/OctoTask.exe
```

### CLI Options

```powershell
# Install Task Manager hook (requires Administrator)
.\bin\Release\net10.0-windows\OctoTask.exe --install

# Uninstall Task Manager hook (requires Administrator)
.\bin\Release\net10.0-windows\OctoTask.exe --uninstall

# Restore original Task Manager from backup
.\bin\Release\net10.0-windows\OctoTask.exe --restore

# Start silently without UI
.\bin\Release\net10.0-windows\OctoTask.exe --no-ui
```

### Git

```powershell
# Check status
git status

# Stage and commit
git add .
git commit -m "feat: description"

# Create feature branch
git checkout -b feature/my-feature

# Push to remote
git push origin feature/my-feature

# Sync with remote
git pull --no-edit
```

### PowerShell (Development)

```powershell
# Clean build artifacts
Remove-Item -Recurse -Force .\bin, .\obj

# Restore NuGet packages
dotnet restore
```

---

## Technologies

| Technology | Details |
|------------|---------|
| **Language** | C# 13 (.NET 10) |
| **UI Framework** | WPF (Windows Presentation Foundation) |
| **Target Framework** | `net10.0-windows` |
| **Runtime** | Windows 10 / 11 |
| **Architecture** | MVVM (no framework — pure `INotifyPropertyChanged`) |
| **Native Interop** | P/Invoke (`ntdll.dll`, `kernel32.dll`, `dwmapi.dll`, `user32.dll`) |
| **Packages** | `System.Management` (v9.0.0+) |
| **Build System** | MSBuild via `dotnet` CLI |
| **Code Style** | Nullable enabled, Implicit Usings enabled |

---

## Architecture & Directory Structure

```
OctoTask/
├── Core/
│   ├── Models/              # Data models (ProcessInfo, ProcessDetails, AppSettings)
│   ├── Native/              # P/Invoke interop layer
│   │   ├── DwmInterop.cs            # Dark title bar via DWM
│   │   ├── ProcessInterop.cs        # Process enumeration via PEB/NtQueryInformationProcess
│   │   ├── SystemInfo.cs            # System memory info via GlobalMemoryStatusEx
│   │   ├── TrayIconService.cs       # System tray management
│   │   └── TrayIconRenderer.cs      # Dynamic icon bitmap generation
│   ├── Registry/            # IFEO registry hook management
│   │   └── TaskmgrHook.cs
│   └── Settings/            # Settings persistence (JSON)
├── UI/
│   ├── ViewModels/          # MVVM ViewModels (MainViewModel, etc.)
│   │   └── RelayCommand.cs   # Simple ICommand implementation
│   ├── Views/               # WPF Windows / UserControls / Dialogs
│   └── Converters/          # Value converters for XAML bindings
├── MainWindow.xaml          # Main application window
├── MainWindow.xaml.cs       # Code-behind (minimal — ViewModel wiring only)
├── App.xaml                 # Application entry point, global resources
├── App.xaml.cs
├── OctoTask.csproj          # Project file
└── app.manifest             # Windows application manifest (requireAdministrator)
```

### Architectural Rules

- **Separation of Concerns**: `Core/` must remain UI-agnostic. No WPF types in `Core/` or `Native/`.
- **Native Layer**: All P/Invoke goes in `Core/Native/`. Never put `DllImport` in ViewModels or Views.
- **MVVM**: No business logic in code-behind. ViewModels handle all logic. Views are dumb XAML.
- **Data Flow**: `Native/` → `Models/` → `ViewModels/` → `Views/` (one-way dependency only).

---

## Best Practices & Guidelines

### C# Coding Standards

1. **Nullable Reference Types** — Always enabled (`<Nullable>enable</Nullable>`). Do not use `null!` suppression unless absolutely necessary.
2. **Implicit Usings** — Enabled. Do not add redundant `using` statements.
3. **Naming Conventions**:
   - **PascalCase** for public members, classes, methods, properties.
   - **_camelCase** for private fields.
   - **camelCase** for local variables and parameters.
   - **Interfaces**: `IService` prefix.
4. **Properties** — Prefer expression-bodied members where appropriate:
   ```csharp
   public string DisplayName => $"{Name} ({Pid})";
   ```
5. **Exception Handling** — Catch specific exceptions. Never catch `Exception` unless re-throwing or at the top boundary. Log with context.
6. **Dispose Pattern** — Use `using` statements for `SafeHandle`, `RegistryKey`, and any disposable native resources.
7. **Async/Await** — Use `async Task` for async operations. Avoid `async void` except for event handlers.
8. **Comments** — Write code that explains itself. Use XML doc comments (`///`) for public APIs. Avoid inline comments unless explaining non-obvious P/Invoke or interop details.

### WPF / XAML Guidelines

1. **No Code-Behind Logic** — `MainWindow.xaml.cs` should only contain:
   - `InitializeComponent()`
   - `DataContext` assignment
   - Event wiring for window-level events (e.g., `OnSourceInitialized` for DWM)
2. **Dark Theme Consistency** — Use the established palette:
   - Background: `#0d1117`
   - Surface: `#161b22`
   - Border: `#30363d`
   - Text: `#c9d1d9`
   - Muted: `#8b949e`
3. **Fonts** — Monospace preferred: `Cascadia Mono`, `Consolas`, `Courier New`.
4. **Resources** — Put reusable styles, brushes, and templates in `Window.Resources` or `App.xaml` resource dictionaries.
5. **Data Binding** — Use `INotifyPropertyChanged` correctly. Always raise `PropertyChanged` on the UI thread.

### P/Invoke & Native Interop

1. **LibraryImport** — Prefer `[LibraryImport]` over `[DllImport]` for new code (source generator support).
2. **SafeHandle** — Wrap native handles in `SafeHandle` subclasses when possible.
3. **32/64-bit Compatibility** — Use `IntPtr.Size` for pointer arithmetic. Handle cross-architecture process reading gracefully (try/catch).
4. **Error Handling** — Native calls can fail silently. Always validate return values and handle `Win32Exception` / `AccessDenied` gracefully.
5. **Unsafe Code** — `<AllowUnsafeBlocks>true</AllowUnsafeBlocks>` is enabled. Use `unsafe` blocks only when necessary for pointer operations, and keep them minimal and well-audited.

### MVVM

1. **No Framework** — Do not introduce MVVM frameworks (ReactiveUI, CommunityToolkit.Mvvm, etc.) without explicit team approval. Pure `INotifyPropertyChanged` + `RelayCommand` is the standard.
2. **ViewModels** — Must implement `INotifyPropertyChanged`. Use `[CallerMemberName]` for the property name parameter.
3. **Commands** — All user actions must use `ICommand`. Never attach click handlers directly in XAML code-behind.
4. **ObservableCollection** — Use for collections bound to ItemsControls. Do not use `List<T>` directly.

---

## Security Considerations

1. **Administrator Privileges** — The app requires admin for registry operations (IFEO hook). The manifest enforces this. Do not weaken the manifest.
2. **Registry Operations** — All HKLM writes require elevation. Always catch `SecurityException` and `UnauthorizedAccessException` when accessing registry.
3. **IFEO Hook** — The `Debugger` registry value points to `OctoTask.exe`. Validate the path before writing. Do not allow arbitrary executable paths from user input.
4. **P/Invoke Surface** — Minimize the native API surface. Only import functions that are strictly needed. Do not use `unsafe` for string handling unless absolutely necessary.
5. **Process Memory Reading** — Reading another process's memory is sensitive. Handle `AccessDenied` gracefully. Never attempt to read system/protected processes.
6. **Input Validation** — Validate all user input (CLI args, settings values) before passing to native APIs or registry.

---

## Testing & Quality

### Current State
- No automated tests exist yet (see `suggestions.md` #17).

### Recommended Approach
- Use **xUnit** for unit tests (no existing test framework).
- Test candidates: `ProcessInfo.FormatBytes()`, `TaskmgrHook` logic (with mocked registry), `RelayCommand`, `MainViewModel` command execution.
- Add a test project: `OctoTask.Tests.csproj`.

### Manual Testing Checklist
- [ ] App launches without white flash (dark theme applied at startup)
- [ ] Dark title bar enabled
- [ ] Process list populates correctly
- [ ] Refresh works
- [ ] Process kill works
- [ ] System tray icon shows and updates
- [ ] Minimize to tray works
- [ ] Settings persist to `%APPDATA%\OctoTask\settings.json`
- [ ] Task Manager hook installs/uninstalls correctly (admin)
- [ ] Restore backup works after uninstall

---

## Git Workflow

1. **Branch Naming**: `feature/`, `fix/`, `refactor/`, `docs/`
2. **Commit Messages** — Follow conventional commits:
   ```
   feat: add process tree view
   fix: handle 32-bit process PEB reading
   refactor: extract ProcessInterop error handling
   docs: update AGENTS.md with build commands
   ```
3. **No Force Push** — Never force push to shared branches.
4. **Pull Requests** — Keep PRs focused. One feature/fix per PR. Update `AGENTS.md` if conventions change.
5. **.gitignore** — Do not commit `bin/`, `obj/`, `*.log`, `settings.json` (user-specific), or `.env`.

---

## File Operations

- **Never delete user files without confirmation** — especially `settings.json` and registry backups.
- **Registry backups** — Before modifying IFEO registry, create a `.reg` backup file.
- **Paths** — Use `%APPDATA%` for settings. Do not hardcode user-specific paths.

---

## Common Pitfalls

1. **Cross-Architecture Process Reading** — A 32-bit process cannot read the PEB of a 64-bit process (and vice versa). Always catch `Win32Exception` and fall back to `Process.MainModule.FileName`.
2. **WPF Threading** — All UI updates must happen on the dispatcher thread. Use `Dispatcher.Invoke` / `Dispatcher.BeginInvoke` when updating collections from background threads.
3. **DWM Dark Title Bar** — Must be called after `OnSourceInitialized`, not in the constructor. The window handle is not valid until then.
4. **System Tray Disposal** — Always dispose the tray icon on application exit. Leaking the tray icon causes ghost icons in the taskbar.
5. **Memory Pressure** — Process enumeration allocates. Reuse collections where possible. Avoid creating new `Process` objects in tight loops.

---

## Skill References

When working on specific domains, load the relevant skill:

| Task | Skill |
|------|-------|
| WPF / MVVM UI work | No specific skill — follow this AGENTS.md |
| Native P/Invoke debugging | No specific skill — use systematic-debugging |
| Receiving code review feedback | `receiving-code-review` |
| Requesting code review | `requesting-code-review` |
| Strategic planning | `plan-mode-strategic-planning-and-architecture` |
| Feature implementation | `test-driven-development` (recommended) |

---

*Last updated: 2026-09-09*

# OctoTask — Quick Wins & High Priority Plan

## Overview

This plan covers **Quick Wins** (low effort, single-session) and **High Priority** features for OctoTask. Each item is ordered by effort within its category.

**Scope**: Source code changes only. No installer/script changes unless noted.

---

## Quick Wins

### Q1: Refresh on Focus Restore
**Effort**: ~15 min | **Value**: Low | **Files**: `MainWindow.xaml.cs`

**Current Gap**: When the user Alt+Tabs back to OctoTask, the process list may be stale (last refreshed 5+ seconds ago).

**Implementation**:
1. In `MainWindow.xaml.cs`, handle the `Activated` event:
   ```csharp
   Activated += (_, _) => { if (_viewModel != null) _viewModel.RefreshProcesses(); };
   ```
2. Add a guard in `MainViewModel.RefreshProcessesAsync()` to skip refresh if it's already running (already guarded by `IsBusy`).

**Validation**: Build and run. Alt+Tab away, wait 10+ seconds, return. Process list should refresh immediately.

---

### Q2: Port Search Filter Fix
**Effort**: ~30 min | **Value**: Medium | **Files**: `UI/ViewModels/PortViewModel.cs`

**Current Gap**: `PortViewModel` has `PortFilter` and `ProtocolFilter` properties with a `ApplyFilters()` method that is an empty stub. The UI has filter controls but they don't actually filter.

**Implementation**:
1. In `PortViewModel`, implement `ApplyFilters()` using a `CollectionViewSource` or rebuild `Connections` from a full list:
   - Keep a `_allConnections` backing list.
   - Filter by `PortFilter` (match `LocalPort`, `RemotePort`, or `ProcessName`).
   - Filter by `ProtocolFilter` (All/TCP/UDP).
   - Rebuild the `Connections` ObservableCollection from the filtered list.
2. Call `ApplyFilters()` in `Refresh()` after populating connections.

**Validation**: Build and run. Open Ports tab, type "chrome" in Port filter — only Chrome connections should show. Select "TCP" — UDP connections should disappear.

---

### Q3: Context Menu — "Go to Port(s)" for Selected Process
**Effort**: ~45 min | **Value**: Medium | **Files**: `MainWindow.xaml.cs`, `MainWindow.xaml`, `MainViewModel.cs`

**Current Gap**: Right-clicking a process offers End Task, Suspend, Resume, Properties, Copy — but no way to jump to its network connections.

**Implementation**:
1. Add command `GoToPortsCommand` to `MainViewModel`:
   ```csharp
   public ICommand GoToPortsCommand { get; }
   ```
2. Handler: set `IsTreeView = false` (ensure DataGrid tab), then `MainTabControl.SelectedIndex = 1` (Ports tab), and filter PortViewModel to show only connections for the selected PID.
3. Add `PortViewModel.FilterByPid(int pid)` method that sets a PID filter and refreshes.
4. Add menu item to `ProcessContextMenu` in `MainWindow.xaml`:
   ```xml
   <MenuItem Header="Go to Ports" Command="{Binding GoToPortsCommand}"/>
   ```

**Validation**: Build and run. Right-click a process → "Go to Ports" → Ports tab should show and filter to that PID's connections.

---

### Q4: Context Menu — "Copy Port/Address"
**Effort**: ~30 min | **Value**: Low | **Files**: `MainWindow.xaml.cs`, `MainWindow.xaml`

**Current Gap**: Process context menu has copy options but no equivalent for Ports tab.

**Implementation**:
1. When Ports tab is active and a connection is selected, add context menu items: Copy Address, Copy Port, Copy Protocol.
2. Clipboard text from `SelectedConnection` properties.

**Validation**: Build and run. Right-click a connection row → verify clipboard contents.

---

### Q5: Tray Icon Tooltip — Process Count
**Effort**: ~15 min | **Value**: Low | **Files**: `MainWindow.xaml.cs`

**Current Gap**: Tray tooltip shows CPU or RAM percentage but not process count.

**Implementation**:
1. In `UpdateTrayIcon()` in `MainWindow.xaml.cs`, append process count to tooltip:
   ```csharp
   tooltip = $"{tooltip} | {_viewModel.Processes.Count} processes";
   ```

**Validation**: Build and run. Hover over tray icon — should show "OctoTask — CPU: 12.3% | 147 processes".

---

### Q6: Search Box — Highlight Matched Text
**Effort**: ~1 hr | **Value**: Medium | **Files**: `MainWindow.xaml`, `MainViewModel.cs`

**Current Gap**: Search filters rows but doesn't visually highlight matches within cells.

**Implementation**:
1. Create a `HighlightTextConverter` that takes the filter text and a cell value, returns a `Inline` collection with highlighted runs.
2. Switch the DataGrid cell template for text columns to use a `TextBlock` with `Inlines` bound via the converter.
3. This requires replacing `DataGridTextColumn` with `DataGridTemplateColumn` for searchable columns.

**Validation**: Build and run. Type "svch" in search — matching text in visible rows should be highlighted blue.

---

## High Priority

### HP1: Global Keyboard Shortcuts
**Effort**: ~2 hr | **Value**: High | **Files**: `MainWindow.xaml.cs`, `Core/Native/GlobalHotKey.cs`

**Status**: ✅ Done

**Implementation**:
1. Created `GlobalHotKey.cs` in `Core/Native/` — uses `RegisterHotKey`/`UnregisterHotKey` P/Invoke with `HwndSource` hook for WM_HOTKEY handling
2. Registered 3 global hotkeys in `MainWindow.OnSourceInitialized()`:
   - **Ctrl+Shift+R** → Refresh processes
   - **Ctrl+Shift+K** → Kill selected process
   - **Ctrl+Shift+S** → Suspend selected process
3. Unregistered in `MainWindow.OnClosed()`
4. Uses `ICommand.CanExecute` guards — won't kill when no process selected

---

### HP2: Network Bandwidth per Process
**Effort**: ~4 hr | **Value**: High | **Files**: `Core/Models/ConnectionInfo.cs`, `UI/ViewModels/PortViewModel.cs`, `UI/Views/PortViewerControl.xaml`

**Status**: ✅ Done (simplified scope)

**Implementation**:
1. Added `NetworkRate` property to `ConnectionInfo` — shows connection rate per second for owning process
2. In `PortViewModel.Refresh()`, added `ComputeBandwidth()`:
   - Takes snapshot of connection counts per PID
   - Computes rate from delta with previous snapshot (1s minimum interval)
   - Displays as "+5/s", "0/s", or "—" (warmup)
3. Added "Network" column to Ports tab DataGrid
4. Bandwidth computed on background thread alongside connection enumeration

---

### HP3: System Resource History Graphs
**Effort**: ~4-6 hr | **Value**: High | **Files**: `UI/Views/ResourceGraphView.xaml`, `UI/ViewModels/ResourceGraphViewModel.cs`, `MainWindow.xaml`, `UI/Views/ResourceGraphView.xaml.cs`

**Status**: ✅ Done

**Implementation**:
1. Created `ResourceGraphViewModel` in `UI/ViewModels/`:
   - Circular buffer (60 points) for CPU% and RAM%
   - 1-second `DispatcherTimer` for sampling
   - Accepts `Func<double>` callbacks for CPU and RAM values from MainViewModel
   - Exposes `CpuPoints` and `RamPoints` as `List<Point>` for binding
2. Created `ResourceGraphView.xaml` + `.xaml.cs`:
   - WPF `Canvas` with `Path` geometry for polyline rendering
   - Grid lines, axis labels (0-100%)
   - Color-coded: CPU=blue (#3b82f6), RAM=green (#10b981)
   - Collapsible header with toggle button
3. Added to `MainWindow.xaml` between dashboard and toolbar (collapsible row)
4. Wired in `MainViewModel`:
   - `ResourceGraph` property exposed for binding
   - Started on each `RefreshProcessesAsync()`
   - Samples CPU/RAM every second independently of 5s refresh
5. Updated tray context menu:
   - Added "Toggle Window" (show/hide)
   - Added "Network View" (switch to Ports tab)
   - "Settings" (renamed from "Tray Settings...")

**Validation**: Build successful — 0 warnings, 0 errors.

---

### HP4: Startup / Auto-Launch Management
**Effort**: ~6 hr | **Value**: High | **Files**: `Core/` (new), `UI/` (new)

**Current Gap**: No startup management — users cannot see or control what launches at boot.

**Implementation**:
1. Create `StartupManager.cs` in `Core/`:
   - Read `HKCU\Software\Microsoft\Windows\CurrentVersion\Run` and `HKLM\...Run`.
   - Enumerate startup folders (`%AppData%\Microsoft\Windows\Start Menu\Programs\Startup`, `Common Startup`).
   - Parse Task Scheduler via `Microsoft.Win32.TaskScheduler` NuGet (or `taskschd.msc` interop).
2. Create `StartupEntry` model: `Name`, `Path`, `Location` (Registry/Task/Folder), `Enabled`, `Type`.
3. Create `StartupViewModel` with `ObservableCollection<StartupEntry>`, toggle enable/disable, add/remove.
4. Create `StartupView.xaml` (UserControl or new tab in MainWindow).
5. Add to MainWindow TabControl as "Startup" tab.

**Validation**: Build and run. Startup tab should list all autostart entries. Toggling a checkbox should enable/disable the entry. Adding a new entry should create the appropriate registry/folder entry.

---

## Dependencies & Order

```
Q1 (15 min) ───┐
Q2 (30 min) ───┤── All can be done independently
Q3 (45 min) ───┘
Q4 (30 min) ───┐
Q5 (15 min) ───┤── All can be done independently
Q6 (1 hr) ─────┘

HP1 (2 hr) ────┐
HP2 (4 hr) ────┤── HP1 has no dependencies on HP2-HP4
HP3 (5 hr) ────┤── HP3 can use HP2's bandwidth data
HP4 (6 hr) ────┘── HP4 is standalone
```

**Recommended execution order**: Q5 → Q1 → Q2 → Q3 → Q4 → Q6 → HP1 → HP2 → HP3 → HP4

---

## Risk Assessment

| Item | Risk | Mitigation |
|------|------|------------|
| Q1 | Low | Single event handler addition |
| Q2 | Medium | Filtering logic is straightforward but edge cases in string matching |
| Q3 | Medium | Cross-tab coordination needs careful DataContext wiring |
| Q4 | Low | Simple Clipboard API calls |
| Q5 | Low | One-line tooltip change |
| Q6 | High | Highlighting requires template columns; test with various input types |
| HP1 | Medium | Global hotkeys can conflict with other apps; use Ctrl+Shift modifier |
| HP2 | High | Bandwidth calculation is complex; start with TCP table delta approach |
| HP3 | Medium | WPF canvas charting is manual; keep it simple with Polyline |
| HP4 | Medium | Task Scheduler API is large; scope to registry + folders first, add scheduler later |

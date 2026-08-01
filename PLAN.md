# OctoTask — Implementation Plan

## Current State Summary

The project scaffold is fully in place:
- `OctoTask.csproj` targeting `net10.0-windows` with `UseWPF`, `Nullable`, `ImplicitUsings`, and `System.Management` v10.0.10
- Solution file `OctoTask.slnx`
- Empty directory structure: `Core/Models`, `Core/Native`, `Core/Registry`, `UI/ViewModels`, `UI/Views`
- Default/empty `MainWindow.xaml`, `MainWindow.xaml.cs`, `App.xaml`, `App.xaml.cs`
- Empty/broken `app.manifest` (no XML content)
- `.gitignore` is good

**No implementation code exists. All directories are empty.**

---

## Step 1: Fix `app.manifest` and Link It in `.csproj`

### Files
- `app.manifest` (rewrite)
- `OctoTask.csproj` (add `<ApplicationManifest>`)

### What To Do
1. **Rewrite `app.manifest`** with a proper Windows application manifest XML that sets:
   - `requestedExecutionLevel level="requireAdministrator" uiAccess="false"`
   - `supportedOS` entries for Windows 10/11
   - `longPathAware` and `dpiAware` for modern Windows
2. **Add `<ApplicationManifest>app.manifest</ApplicationManifest>`** inside the `<PropertyGroup>` of `OctoTask.csproj`

### Why First
Without this, the registry hook (Step 5) will fail at runtime because HKLM writes require elevation.

---

## Step 2: Create `Core/Models/ProcessInfo.cs`

### Files
- `Core/Models/ProcessInfo.cs` (new)

### What To Do
Create a simple POCO/model class with these properties:

```csharp
public class ProcessInfo
{
    public int Pid { get; set; }
    public string ProcessName { get; set; } = string.Empty;
    public string ExecutablePath { get; set; } = string.Empty;
    public string CommandLine { get; set; } = string.Empty;
    public long WorkingSetBytes { get; set; }
    public string WorkingSetDisplay => FormatBytes(WorkingSetBytes);

    private static string FormatBytes(long bytes) { ... }
}
```

### Why
Central data model used by the process engine and bound to the DataGrid.

---

## Step 3: Create `Core/Native/DwmInterop.cs`

### Files
- `Core/Native/DwmInterop.cs` (new)

### What To Do
Implement P/Invoke for the Desktop Window Manager to enable dark title bar:

```csharp
internal static partial class DwmInterop
{
    private const int DWMWA_USE_IMMERSIVE_DARK_MODE = 20;

    [LibraryImport("dwmapi.dll")]
    private static partial int DwmSetWindowAttribute(
        IntPtr hwnd, int dwAttribute, ref int pvAttribute, int cbAttribute);

    public static void EnableDarkTitleBar(IntPtr hwnd) { ... }
}
```

### Why
WPF windows have light title bars by default. This forces the Windows 10/11 dark title bar to match the app theme. Called once from `MainWindow.xaml.cs` after `OnSourceInitialized`.

---

## Step 4: Create `Core/Native/ProcessInterop.cs`

### Files
- `Core/Native/ProcessInterop.cs` (new)

### What To Do
This is the most complex file. Implement:

1. **P/Invoke declarations** for:
   - `NtQueryInformationProcess` from `ntdll.dll`
   - `ReadProcessMemory` from `kernel32.dll`
   - `OpenProcess` from `kernel32.dll`
   - `CloseHandle` from `kernel32.dll`

2. **Constants and structs**:
   - `ProcessBasicInformation` struct
   - `PROCESS_ACCESS_RIGHTS` (PROCESS_QUERY_INFORMATION | PROCESS_VM_READ)
   - `OBJECT_NAME_INFORMATION`, `UNICODE_STRING` for path resolution
   - PEB structure offsets (BeingDebugged, Ldr, ProcessParameters)
   - `RTL_USER_PROCESS_PARAMETERS` offsets (CommandLine, ImagePathName)

3. **Main method** `GetAllProcesses()` returning `List<ProcessInfo>`:
   - Enumerate processes via `Process.GetProcesses()`
   - For each process, open handle with `OpenProcess`
   - Call `NtQueryInformationProcess` to get PEB address
   - Read PEB → `ProcessParameters` → `CommandLine` and `ImagePathName` via `ReadProcessMemory`
   - Read executable path from `ProcessParameters.ImagePathName`
   - Fall back to `Process.MainModule.FileName` if PEB read fails (permissions, 32/64 bit mismatch)
   - Catch and silently skip processes that can't be read (system, protected)

4. **Helper methods**:
   - `ReadUnicodeStringFromProcessMemory(IntPtr processHandle, long unicodeStringPtr)` — reads a `UNICODE_STRING` from remote process memory
   - `ReadNtPathToDosPath(string ntPath)` — converts `\Device\HarddiskVolume3\...` to `C:\...`

### Design Notes
- Must handle 32-bit reading 64-bit processes and vice versa gracefully (try/catch)
- Use `IntPtr.Size` for pointer arithmetic portability
- The whole method should be wrapped in try/catch to avoid crashing on any single unreadable process

---

## Step 5: Create `Core/Registry/TaskmgrHook.cs`

### Files
- `Core/Registry/TaskmgrHook.cs` (new)

### What To Do
Implement the Image File Execution Options (IFEO) hook:

```csharp
internal static class TaskmgrHook
{
    private const string IfeoKeyPath =
        @"SOFTWARE\Microsoft\Windows NT\CurrentVersion\Image File Execution Options\taskmgr.exe";

    public static bool Install(string octoTaskExePath) { ... }
    public static bool Uninstall() { ... }
    public static bool IsInstalled() { ... }
}
```

### Logic
- `Install`: Create/open `HKLM\...\Image File Execution Options\taskmgr.exe`, set string value `Debugger` = path to `OctoTask.exe`
- `Uninstall`: Delete the `Debugger` value (or the entire key if it was created by us)
- `IsInstalled`: Check if `Debugger` value exists and points to OctoTask
- All operations require admin privileges (enforced by manifest)
- Use `Microsoft.Win32.Registry` (available in .NET without extra packages for HKLM writes)

---

## Step 6: Create `UI/ViewModels/MainViewModel.cs`

### Files
- `UI/ViewModels/MainViewModel.cs` (new)

### What To Do
Create an MVVM ViewModel (no framework — pure INotifyPropertyChanged):

```csharp
public class MainViewModel : INotifyPropertyChanged
{
    public ObservableCollection<ProcessInfo> Processes { get; } = new();

    public ICommand RefreshCommand { get; }
    public ICommand InstallHookCommand { get; }
    public ICommand UninstallHookCommand { get; }

    private bool _hookInstalled;
    public bool HookInstalled { get => _hookInstalled; set { _hookInstalled = value; OnPropertyChanged(); } }

    public MainViewModel()
    {
        RefreshCommand = new RelayCommand(_ => RefreshProcesses());
        InstallHookCommand = new RelayCommand(_ => InstallHook());
        UninstallHookCommand = new RelayCommand(_ => UninstallHook());
    }

    public void RefreshProcesses() { ... }
    private void InstallHook() { ... }
    private void UninstallHook() { ... }

    // INotifyPropertyChanged boilerplate
    public event PropertyChangedEventHandler? PropertyChanged;
    protected void OnPropertyChanged([CallerMemberName] string? name = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}
```

### Also create
- `UI/ViewModels/RelayCommand.cs` — simple `ICommand` implementation using `Action<object?>` and `Predicate<object?>`

### Why MVVM
Clean separation. The code-behind stays minimal. Commands bind directly to buttons.

---

## Step 7: Rewrite `MainWindow.xaml` — Full Dark-Themed DataGrid UI

### Files
- `MainWindow.xaml` (rewrite completely)

### What To Do
Build the complete dark terminal UI in XAML:

```xml
<Window ... Title="OctoTask" Background="#0d1117"
        Foreground="#c9d1d9" WindowStartupLocation="CenterScreen"
        Height="700" Width="1200" MinHeight="400" MinWidth="800">

    <Window.Resources>
        <!-- Global dark theme styles -->
        <!-- DataGrid styles: dark background, no gridlines, terminal font -->
        <!-- Button styles: flat dark buttons with hover effects -->
        <!-- ScrollBar styles: thin dark scrollbars -->
    </Window.Resources>

    <DockPanel>
        <!-- Top toolbar: Refresh button, Hook Install/Uninstall buttons, status indicator -->
        <ToolBar DockPanel.Dock="Top" Background="#161b22">
            <Button Content="⟳ Refresh" Command="{Binding RefreshCommand}" />
            <Separator />
            <Button Content="⚡ Install Taskmgr Hook" Command="{Binding InstallHookCommand}" />
            <Button Content="✕ Remove Hook" Command="{Binding UninstallHookCommand}" />
            <TextBlock Text="{Binding HookStatusText}" Foreground="#8b949e" />
        </ToolBar>

        <!-- Status bar -->
        <TextBlock DockPanel.Dock="Bottom" ... />

        <!-- Main DataGrid fills remaining space -->
        <DataGrid ItemsSource="{Binding Processes}"
                  AutoGenerateColumns="False"
                  Background="#0d1117"
                  Foreground="#c9d1d9"
                  BorderBrush="#30363d"
                  RowBackground="#0d1117"
                  AlternatingRowBackground="#161b22"
                  GridLinesVisibility="None"
                  HeadersVisibility="Column"
                  CanUserAddRows="False"
                  IsReadOnly="True"
                  SelectionMode="Single"
                  FontFamily="Cascadia Mono, Consolas, Courier New">

            <DataGrid.Columns>
                <DataGridTextColumn Header="PID" Binding="{Binding Pid}" Width="70" />
                <DataGridTextColumn Header="Process" Binding="{Binding ProcessName}" Width="180" />
                <DataGridTextColumn Header="RAM" Binding="{Binding WorkingSetDisplay}" Width="100" />
                <DataGridTextColumn Header="Executable Path" Binding="{Binding ExecutablePath}" Width="*" />
                <DataGridTextColumn Header="Command Line" Binding="{Binding CommandLine}" Width="*" />
            </DataGrid.Columns>
        </DataGrid>
    </DockPanel>
</Window>
```

### Design Principles
- **Colors**: Background `#0d1117`, Surface `#161b22`, Border `#30363d`, Text `#c9d1d9`, Muted `#8b949e` (GitHub Dark / Terminal palette)
- **Font**: Monospace (Cascadia Mono preferred, Consolas fallback)
- **No external packages** — pure WPF styles and templates in `Window.Resources`
- Custom DataGrid row style, column header style, scrollbar template

---

## Step 8: Rewrite `MainWindow.xaml.cs` — Code-Behind

### Files
- `MainWindow.xaml.cs` (rewrite)

### What To Do
Minimal code-behind — just DWM setup and ViewModel wiring:

```csharp
public partial class MainWindow : Window
{
    private readonly MainViewModel _viewModel;

    public MainWindow()
    {
        InitializeComponent();
        _viewModel = new MainViewModel();
        DataContext = _viewModel;

        // Auto-refresh on load
        Loaded += (_, _) => _viewModel.RefreshProcesses();
    }

    protected override void OnSourceInitialized(EventArgs e)
    {
        base.OnSourceInitialized(e);
        // Enable dark title bar via DWM
        var helper = new WindowInteropHelper(this);
        DwmInterop.EnableDarkTitleBar(helper.Handle);
    }
}
```

---

## Step 9: Update `App.xaml` — Global Dark Theme Base

### Files
- `App.xaml` (rewrite)

### What To Do
Set global application-level dark theme defaults so no white flash on startup:

```xml
<Application ...>
    <Application.Resources>
        <ResourceDictionary>
            <SolidColorBrush x:Key="WindowBackground" Color="#0d1117"/>
            <SolidColorBrush x:Key="ForegroundBrush" Color="#c9d1d9"/>
            <Style TargetType="Window">
                <Setter Property="Background" Value="{StaticResource WindowBackground}"/>
                <Setter Property="Foreground" Value="{StaticResource ForegroundBrush}"/>
            </Style>
            <Style TargetType="TextBlock">
                <Setter Property="Foreground" Value="{StaticResource ForegroundBrush}"/>
            </Style>
            <!-- Base styles for TextBox, Button, etc. -->
        </ResourceDictionary>
    </Application.Resources>
</Application>
```

---

## Execution Order Summary

| Step | Files Changed | Depends On |
|------|--------------|------------|
| 1. Fix app.manifest + link in csproj | `app.manifest`, `OctoTask.csproj` | Nothing |
| 2. ProcessInfo model | `Core/Models/ProcessInfo.cs` | Nothing |
| 3. DWM dark title bar P/Invoke | `Core/Native/DwmInterop.cs` | Nothing |
| 4. Process engine P/Invoke | `Core/Native/ProcessInterop.cs` | Step 2 (uses ProcessInfo) |
| 5. Registry hook | `Core/Registry/TaskmgrHook.cs` | Step 1 (needs admin) |
| 6. ViewModel + RelayCommand | `UI/ViewModels/MainViewModel.cs`, `UI/ViewModels/RelayCommand.cs` | Steps 2, 4, 5 |
| 7. Full dark UI XAML | `MainWindow.xaml` | Step 6 (binds to ViewModel) |
| 8. Code-behind | `MainWindow.xaml.cs` | Steps 3, 6 |
| 9. App-level dark theme | `App.xaml` | Nothing |

**Independent steps that can be parallelized:** Steps 1, 2, 3
**Sequential chain:** 2 → 4 → 6 → 7
**Sequential chain:** 1 → 5 → 6
**Can be done anytime:** 3, 9

---

## Estimated Line Counts

| File | ~Lines |
|------|--------|
| `app.manifest` | 40 |
| `Core/Models/ProcessInfo.cs` | 35 |
| `Core/Native/DwmInterop.cs` | 25 |
| `Core/Native/ProcessInterop.cs` | 200 |
| `Core/Registry/TaskmgrHook.cs` | 60 |
| `UI/ViewModels/RelayCommand.cs` | 30 |
| `UI/ViewModels/MainViewModel.cs` | 80 |
| `MainWindow.xaml` | 200 |
| `MainWindow.xaml.cs` | 30 |
| `App.xaml` | 50 |
| `OctoTask.csproj` (edit) | +1 line |
| **Total** | **~750** |

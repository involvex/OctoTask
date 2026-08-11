# Task 5: Modify `MainViewModel` — Expose PortViewModel + Cross-linking

**Files:**
- Modify: `UI/ViewModels/MainViewModel.cs`

**Interfaces:**
- Consumes: `PortViewModel` from Task 3
- Produces: `MainViewModel.PortVM` property, `MainViewModel.SelectProcessByPid(int pid)` method

## What To Do

Modify the existing `MainViewModel.cs` to:
1. Add a `PortViewModel` instance as a public property
2. Wire up the `GoToProcessRequested` event to a new `SelectProcessByPid` method
3. The `SelectProcessByPid` method finds a process by PID in the `Processes` collection and selects it

### Changes to make in `UI/ViewModels/MainViewModel.cs`:

**Change 1:** Add a new field and property after the existing fields (around line 38, after the `_cpuStopwatch` field):

```csharp
private readonly PortViewModel _portVM;
public PortViewModel PortVM => _portVM;
```

**Change 2:** In the constructor, after the line `_sortClickCount = 1;` (around line 216), add:

```csharp
_portVM = new PortViewModel();
_portVM.GoToProcessRequested += SelectProcessByPid;
```

**Change 3:** Add the `SelectProcessByPid` method after the `BuildProcessTree` method (before the `#region Sorting` section, around line 574):

```csharp
public void SelectProcessByPid(int pid)
{
    var match = Processes.FirstOrDefault(p => p.Pid == pid);
    if (match != null)
    {
        SelectedProcess = match;
        IsTreeView = false; // Switch to DataGrid view to show selection
    }
    else
    {
        StatusText = $"PID {pid} not found in process list — try refreshing first";
    }
}
```

## Verification

Run: `dotnet build OctoTask.csproj`
Expected: BUILD SUCCEEDED

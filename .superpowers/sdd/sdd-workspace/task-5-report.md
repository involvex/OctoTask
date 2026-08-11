# Task 5 Report: MainViewModel — PortViewModel + Cross-linking

## Status: DONE

## Changes Made

Modified `UI/ViewModels/MainViewModel.cs` with three changes:

1. **Field + Property (line 42-43):** Added `private readonly PortViewModel _portVM;` field and `public PortViewModel PortVM => _portVM;` property after `_cpuStopwatch`.

2. **Constructor initialization (line 218-220):** After `_sortClickCount = 1;`, added:
   ```csharp
   _portVM = new PortViewModel();
   _portVM.GoToProcessRequested += SelectProcessByPid;
   ```

3. **SelectProcessByPid method (line 578-589):** Added before `#region Sorting` — finds a process by PID in `Processes`, selects it, and switches to DataGrid view. Shows status message if PID not found.

## Build Result

```
BUILD SUCCEEDED — 0 errors, 0 warnings
```

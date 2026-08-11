# Task 3 Report: Create PortViewModel

## Status: COMPLETE

## Files Created
- `UI/ViewModels/PortViewModel.cs`

## Build Result
- `dotnet build OctoTask.csproj` — BUILD SUCCEEDED (0 warnings, 0 errors)

## Summary
Created `PortViewModel.cs` as specified in the task brief. The ViewModel:
- Implements `INotifyPropertyChanged` following the existing `MainViewModel` pattern
- Exposes `ObservableCollection<ConnectionInfo> Connections`
- Provides `RefreshCommand` and `ClearFilterCommand` via `RelayCommand`
- Supports `PortFilter`, `ProtocolFilter`, and `SelectedConnection` properties with change notification
- Includes a 5-second `DispatcherTimer` for auto-refresh (togglable via `IsAutoRefreshEnabled`)
- Caches process names during refresh to avoid duplicate `Process.GetProcessById` calls
- Fires `GoToProcessRequested` event when `GoToProcess()` is called
- Shows connection counts (TCP + UDP) in `StatusText`

## Verification
No test files exist in this project yet. Build verification passed.

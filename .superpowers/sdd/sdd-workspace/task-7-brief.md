# Task 7: Modify `MainWindow.xaml.cs` — Wire TabControl Selection

**Files:**
- Modify: `MainWindow.xaml.cs`

**Interfaces:**
- Consumes: `PortViewModel.Refresh()` from Task 3 (via `_viewModel.PortVM`)
- Consumes: `MainTabControl` named element from Task 6

## What To Do

Modify `MainWindow.xaml.cs` to handle tab switching — when the user switches to the Ports tab, trigger a refresh of the port connections.

### Changes to make:

**Change 1:** In the `OnLoaded` method (around line 35-48), add a subscription to the TabControl's `SelectionChanged` event. Add this line after the existing `ProcessTreeView.SelectedItemChanged` subscription:

```csharp
MainTabControl.SelectionChanged += OnTabChanged;
```

**Change 2:** Add the `OnTabChanged` handler method. Add it after the `OnTreeViewSelectedItemChanged` method (around line 53):

```csharp
private void OnTabChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
{
    // When switching to Ports tab, ensure it refreshes
    if (MainTabControl.SelectedIndex == 1)
    {
        _viewModel.PortVM.Refresh();
    }
}
```

## Verification

Run: `dotnet build OctoTask.csproj`
Expected: BUILD SUCCEEDED

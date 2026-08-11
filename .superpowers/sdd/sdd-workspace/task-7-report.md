# Task 7 Report: Wire TabControl Selection

**Status:** COMPLETE

## Changes Made

**File:** `MainWindow.xaml.cs`

1. **Added event subscription** in `OnLoaded` (line 49):
   ```csharp
   MainTabControl.SelectionChanged += OnTabChanged;
   ```

2. **Added handler method** `OnTabChanged` after `OnTreeViewSelectedItemChanged` (lines 55-62):
   ```csharp
   private void OnTabChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
   {
       if (MainTabControl.SelectedIndex == 1)
       {
           _viewModel.PortVM.Refresh();
       }
   }
   ```

## Verification

- **Build:** `dotnet build OctoTask.csproj` — BUILD SUCCEEDED, 0 warnings, 0 errors

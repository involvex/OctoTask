# Task 6: Modify MainWindow.xaml — Add TabControl with Ports Tab

**Status:** ✅ COMPLETE

## Changes Made

### Change 1: Added `xmlns:views` namespace
- Added `xmlns:views="clr-namespace:OctoTask.UI.Views"` to the `<Window>` tag after `xmlns:local` (line 9).

### Change 2: Added TabControl and TabItem styles
- Added two new implicit styles inside `<Window.Resources>` after the `DangerButton` style:
  - `TabControl` style: Transparent background, no border thickness/padding.
  - `TabItem` style: Custom ControlTemplate with dark theme support, hover/selected triggers matching the existing design system (AccentBrush for selected, SurfaceAltBrush for hover).

### Change 3: Wrapped Row 2 content in TabControl
- Replaced the bare `<DockPanel Grid.Row="2">` with a `<TabControl x:Name="MainTabControl" Grid.Row="2">`.
- Wrapped the existing DockPanel (DataGrid + DetailsPane) inside a `<TabItem>` with header "Processes".
- Added a second `<TabItem>` with header "Ports" containing `<views:PortViewerControl DataContext="{Binding PortVM}"/>`.
- Closing tag structure: `</DockPanel>` → `</TabItem>` → Ports TabItem → `</TabControl>`.

## Build Result
```
dotnet build OctoTask.csproj
→ BUILD SUCCEEDED
→ 0 Warning(s)  0 Error(s)
```

## Verification
- XAML structure verified: TabControl in Grid.Row="2" with two TabItems (Processes, Ports).
- Namespace `views` correctly declared for `PortViewerControl` reference.
- No compilation errors or warnings.

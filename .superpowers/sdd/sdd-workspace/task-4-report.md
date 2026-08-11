# Task 4 Report: Create PortViewerControl (XAML + Code-behind)

**Status:** ✅ COMPLETE

## Files Created

- `UI/Views/PortViewerControl.xaml` — WPF UserControl with dark-themed search/filter toolbar and DataGrid
- `UI/Views/PortViewerControl.xaml.cs` — Code-behind with double-click and button event handlers

## What Was Built

**XAML (`PortViewerControl.xaml`):**
- DockPanel layout: toolbar Border docked Top, DataGrid fills remaining space
- Toolbar grid with 9 columns: Port search TextBox, Protocol ComboBox filter, Refresh button (ToolbarButton style), Auto-refresh CheckBox, Go to Process button
- All brushes reference StaticResource (BgBrush, SurfaceBrush, BorderBrush, TextPrimary, TextSecondary) — available at runtime from MainWindow.xaml
- DataGrid with 8 columns: Protocol, Local Address, Local Port (right-aligned), Remote Address, Remote Port (right-aligned), State, PID (right-aligned), Process
- Font set to Cascadia Mono, Consolas, Courier New throughout
- BoolToVisibilityConverter declared in UserControl.Resources

**Code-behind (`PortViewerControl.xaml.cs`):**
- `OnDataGridDoubleClick` — casts DataContext to PortViewModel, calls GoToProcess() if CanGoToProcess
- `OnGoToProcessClick` — same logic for the toolbar button
- Both handlers match the brief exactly

## Build Result

```
Build succeeded.
    0 Warning(s)
    0 Error(s)
```

## Notes

- The `ProtocolFilter` ComboBox uses string items ("All", "TCP", "UDP") that bind to the ViewModel's string property. The ViewModel's ApplyFilters() will need to handle these values when the filtering pipeline is wired up.
- Pre-existing LSP errors in MainWindow.xaml.cs (InitializeComponent, ProcessDataGrid, ProcessTreeView) are unrelated to this task.

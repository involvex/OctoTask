using System;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using OctoTask.Core.Native;
using OctoTask.UI.ViewModels;

namespace OctoTask;

/// <summary>
/// Interaction logic for MainWindow.xaml
/// </summary>
public partial class MainWindow : Window
{
    private readonly MainViewModel _viewModel;

    public MainWindow()
    {
        InitializeComponent();

        _viewModel = new MainViewModel();
        DataContext = _viewModel;

        Loaded += OnLoaded;
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        _viewModel.RefreshProcesses();

        // Enable column sorting on header click
        if (ProcessDataGrid != null)
        {
            ProcessDataGrid.Sorting += OnDataGridSorting;
        }
    }

    protected override void OnSourceInitialized(EventArgs e)
    {
        base.OnSourceInitialized(e);

        // Enable dark title bar via DWM
        var helper = new WindowInteropHelper(this);
        if (helper.Handle != IntPtr.Zero)
        {
            DwmInterop.EnableDarkTitleBar(helper.Handle);
        }
    }

    private void OnDataGridSorting(object sender, DataGridSortingEventArgs e)
    {
        e.Handled = true;

        if (e.Column == null)
            return;

        // Map column header to property name
        string propertyName = e.Column.SortMemberPath ?? GetPropertyName(e.Column) ?? string.Empty;

        _viewModel.SetSort(propertyName);
    }

    private string? GetPropertyName(System.Windows.Controls.DataGridColumn column)
    {
        var header = column.Header?.ToString() ?? "";
        return header switch
        {
            "PID" => "Pid",
            "Process" => "ProcessName",
            "RAM" => "WorkingSetBytes",
            "Executable Path" => "ExecutablePath",
            "Command Line" => "CommandLine",
            _ => null
        };
    }
}

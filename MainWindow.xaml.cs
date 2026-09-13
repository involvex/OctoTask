using System;
using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using OctoTask.Core.Models;
using OctoTask.Core.Native;
using OctoTask.Core.Settings;
using OctoTask.UI.ViewModels;
using OctoTask.UI.Views;

namespace OctoTask;

public partial class MainWindow : Window
{
    private readonly MainViewModel _viewModel;
    private readonly TrayIconService _trayIcon;
    private readonly AppSettings _settings;
    private IntPtr _currentIcon = IntPtr.Zero;
    private DateTime _lastIconUpdate = DateTime.MinValue;

    public MainWindow()
    {
        InitializeComponent();

        _settings = AppSettings.Load();
        _trayIcon = new TrayIconService();
        _viewModel = new MainViewModel();
        DataContext = _viewModel;

        Loaded += OnLoaded;
        StateChanged += OnStateChanged;
        Activated += OnActivated;
        Closed += OnClosed;
    }

    private DateTime _lastActivatedRefresh = DateTime.MinValue;

    private void OnActivated(object? sender, EventArgs e)
    {
        if ((DateTime.UtcNow - _lastActivatedRefresh).TotalSeconds >= 5)
        {
            _lastActivatedRefresh = DateTime.UtcNow;
            _viewModel?.RefreshProcesses();
        }
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        _viewModel.RefreshProcesses();
        RestoreColumnLayout();

        if (ProcessDataGrid != null)
        {
            ProcessDataGrid.Sorting += OnDataGridSorting;
            ProcessDataGrid.ContextMenuOpening += OnProcessContextMenuOpening;
            ProcessDataGrid.Loaded += (_, _) => RestoreColumnWidths();
        }

        if (ProcessTreeView != null)
        {
            ProcessTreeView.SelectedItemChanged += OnTreeViewSelectedItemChanged;
            ProcessTreeView.ContextMenuOpening += OnProcessContextMenuOpening;
        }

        MainTabControl.SelectionChanged += OnTabChanged;

        if (App.StartMinimized)
            WindowState = WindowState.Minimized;
    }

        private void OnTreeViewSelectedItemChanged(object sender, RoutedPropertyChangedEventArgs<object> e)
        {
            _viewModel.SelectedProcess = e.NewValue as Core.Models.ProcessInfo;
        }

        private void OnTabChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
        {
            if (MainTabControl.SelectedIndex == 1)
            {
                _ = _viewModel.PortVM.Refresh();
            }
        }

    protected override void OnSourceInitialized(EventArgs e)
    {
        base.OnSourceInitialized(e);

        var helper = new WindowInteropHelper(this);
        if (helper.Handle != IntPtr.Zero)
        {
            DwmInterop.EnableDarkTitleBar(helper.Handle);

            _currentIcon = TrayIconRenderer.RenderIcon(0, 0, _settings.TrayDisplayMode);
            _trayIcon.AddIcon(helper.Handle, _currentIcon, "OctoTask");
            _trayIcon.DoubleClick += (_, _) => RestoreFromTray();
            _trayIcon.RightClick += (_, _) => ShowTrayContextMenu();

            GlobalHotKey.Register(this,
                _viewModel.RefreshCommand,
                _viewModel.KillProcessCommand,
                _viewModel.SuspendProcessCommand);
        }

        _viewModel.PropertyChanged += OnViewModelPropertyChanged;
    }

    private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(MainViewModel.SystemCpuUsage)
            or nameof(MainViewModel.SystemRamUsage))
        {
            var now = DateTime.UtcNow;
            if ((now - _lastIconUpdate).TotalMilliseconds >= _settings.UpdateIntervalMs)
            {
                _lastIconUpdate = now;
                UpdateTrayIcon();
            }
        }
    }

    private void UpdateTrayIcon()
    {
        double percentage = _settings.TrayDisplayMode == TrayDisplayMode.Cpu
            ? _viewModel.SystemCpuUsage
            : _viewModel.SystemRamUsage;
        int value = (int)Math.Round(percentage);

        var newIcon = TrayIconRenderer.RenderIcon(value, percentage, _settings.TrayDisplayMode);
        if (newIcon == IntPtr.Zero)
            return;

        string tooltip = _settings.TrayDisplayMode == TrayDisplayMode.Cpu
            ? $"OctoTask — CPU: {percentage:F1}%"
            : $"OctoTask — RAM: {_viewModel.SystemRamDisplay}";

        tooltip += $" | {_viewModel.Processes.Count} processes";

        _trayIcon.UpdateIcon(newIcon, tooltip);

        if (_currentIcon != IntPtr.Zero)
            DestroyIcon(_currentIcon);
        _currentIcon = newIcon;
    }

    private void OnStateChanged(object? sender, EventArgs e)
    {
        if (WindowState == WindowState.Minimized && _settings.MinimizeToTray)
        {
            Dispatcher.BeginInvoke(new Action(() => Hide()));
        }
    }

    private void RestoreFromTray()
    {
        Dispatcher.BeginInvoke(new Action(() =>
        {
            Show();
            WindowState = WindowState.Normal;
            Activate();
            Focus();
        }));
    }

        private void ShowTrayContextMenu()
        {
            Dispatcher.BeginInvoke(new Action(() =>
            {
                var menu = new ContextMenu();

                var toggleItem = new MenuItem { Header = "Toggle Window" };
                toggleItem.Click += (_, _) => ToggleWindow();
                menu.Items.Add(toggleItem);

                menu.Items.Add(new Separator());

                var networkItem = new MenuItem { Header = "Network View" };
                networkItem.Click += (_, _) => ShowNetworkView();
                menu.Items.Add(networkItem);

                var settingsItem = new MenuItem { Header = "Settings" };
                settingsItem.Click += (_, _) => OpenTraySettings();
                menu.Items.Add(settingsItem);

                menu.Items.Add(new Separator());

                var exitItem = new MenuItem { Header = "Exit" };
                exitItem.Click += (_, _) => Close();
                menu.Items.Add(exitItem);

                menu.Placement = System.Windows.Controls.Primitives.PlacementMode.MousePoint;
                menu.IsOpen = true;
            }));
        }

        private void ToggleWindow()
        {
            Dispatcher.BeginInvoke(new Action(() =>
            {
                if (Visibility == Visibility.Visible && WindowState != WindowState.Minimized)
                {
                    Hide();
                }
                else
                {
                    Show();
                    WindowState = WindowState.Normal;
                    Activate();
                    Focus();
                }
            }));
        }

        private void ShowNetworkView()
        {
            Dispatcher.BeginInvoke(new Action(() =>
            {
                Show();
                WindowState = WindowState.Normal;
                Activate();
                _viewModel.SelectedTabIndex = 1;
                _ = _viewModel.PortVM.Refresh();
            }));
        }

    private void OpenTraySettings()
    {
        var win = new TrayIconSettingsWindow(AppSettings.Load())
        {
            Owner = this
        };
        if (win.ShowDialog() == true)
        {
            var loaded = AppSettings.Load();
            _settings.TrayDisplayMode = loaded.TrayDisplayMode;
            _settings.MinimizeToTray = loaded.MinimizeToTray;
            _settings.UpdateIntervalMs = loaded.UpdateIntervalMs;
            UpdateTrayIcon();
        }
    }

    private void OnClosed(object? sender, EventArgs e)
    {
        SaveColumnLayout();
        GlobalHotKey.UnregisterAll(this);
        _trayIcon.RemoveIcon();
        if (_currentIcon != IntPtr.Zero)
        {
            DestroyIcon(_currentIcon);
            _currentIcon = IntPtr.Zero;
        }
    }

    private void OnDataGridSorting(object sender, DataGridSortingEventArgs e)
    {
        e.Handled = true;

        if (e.Column == null)
            return;

        string propertyName = e.Column.SortMemberPath ?? GetPropertyName(e.Column) ?? string.Empty;
        _viewModel.SetSort(propertyName);
    }

    private void OnProcessContextMenuOpening(object sender, ContextMenuEventArgs e)
    {
        var menu = (e.Source as FrameworkElement)?.ContextMenu;
        if (menu == null)
            return;

        ProcessInfo? targetProcess = null;
        var mousePos = Mouse.GetPosition((IInputElement)sender);

        if (sender is DataGrid dataGrid)
        {
            var hit = dataGrid.InputHitTest(mousePos);
            if (hit is DependencyObject depObj)
            {
                var row = FindParent<DataGridRow>(depObj);
                if (row != null && row.Item is ProcessInfo pi)
                {
                    targetProcess = pi;
                    dataGrid.SelectedItem = pi;
                    _viewModel.SelectedProcess = pi;
                }
            }
        }
        else if (sender is TreeView treeView)
        {
            var hit = treeView.InputHitTest(mousePos);
            if (hit is DependencyObject depObj)
            {
                var item = FindParent<TreeViewItem>(depObj);
                if (item != null && item.DataContext is ProcessInfo pi)
                {
                    targetProcess = pi;
                    _viewModel.SelectedProcess = pi;
                }
            }
        }

        menu.DataContext = _viewModel;
        menu.IsOpen = targetProcess is not null;
        e.Handled = targetProcess is not null;
    }

    private static T? FindParent<T>(DependencyObject child) where T : DependencyObject
    {
        while (child != null)
        {
            if (child is T parent)
                return parent;
            child = System.Windows.Media.VisualTreeHelper.GetParent(child);
        }
        return null;
    }

    private void OnContextMenuProperties(object sender, RoutedEventArgs e)
    {
        var process = _viewModel.SelectedProcess;
        if (process == null)
            return;

        _viewModel.SelectedProcess = process;
    }

    private void OnCopyProcessName(object sender, RoutedEventArgs e)
    {
        if (_viewModel.SelectedProcess != null)
            Clipboard.SetText(_viewModel.SelectedProcess.ProcessName);
    }

    private void OnCopyPid(object sender, RoutedEventArgs e)
    {
        if (_viewModel.SelectedProcess != null)
            Clipboard.SetText(_viewModel.SelectedProcess.Pid.ToString());
    }

    private void OnCopyExecutablePath(object sender, RoutedEventArgs e)
    {
        if (_viewModel.SelectedProcess != null)
            Clipboard.SetText(_viewModel.SelectedProcess.ExecutablePath);
    }

    private string? GetPropertyName(DataGridColumn column)
    {
        var header = column.Header?.ToString() ?? "";
        return header switch
        {
            "PID" => "Pid",
            "Process" => "ProcessName",
            "RAM" => "WorkingSetBytes",
            "CPU" => "CpuPercentage",
            "Executable Path" => "ExecutablePath",
            "Command Line" => "CommandLine",
            _ => null
        };
    }

    private static void DestroyIcon(IntPtr hIcon)
        => TrayIconRenderer.DestroyIconIndirect(hIcon);

    public void FocusSearchBox()
    {
        SearchBox?.Focus();
        SearchBox?.SelectAll();
    }

    private void RestoreColumnLayout()
    {
        try
        {
            var settings = Core.Settings.AppSettings.Load();
            _viewModel.RestoreSortState(settings.SortColumn, settings.SortDirection);
        }
        catch
        {
        }
    }

    private void RestoreColumnWidths()
    {
        try
        {
            if (ProcessDataGrid == null)
                return;

            var settings = Core.Settings.AppSettings.Load();
            foreach (var column in ProcessDataGrid.Columns)
            {
                var key = column.SortMemberPath ?? column.Header?.ToString() ?? string.Empty;
                if (!string.IsNullOrEmpty(key) && settings.ColumnWidths.TryGetValue(key, out double width))
                {
                    column.Width = new DataGridLength(width);
                }
            }
        }
        catch
        {
        }
    }

    private void SaveColumnLayout()
    {
        try
        {
            var settings = Core.Settings.AppSettings.Load();
            var (sortCol, sortDir) = _viewModel.GetSortState();
            settings.SortColumn = sortCol;
            settings.SortDirection = sortDir;

            if (ProcessDataGrid != null)
            {
                settings.ColumnWidths.Clear();
                foreach (var column in ProcessDataGrid.Columns)
                {
                    var key = column.SortMemberPath ?? column.Header?.ToString() ?? string.Empty;
                    if (!string.IsNullOrEmpty(key) && column.Width.IsAbsolute)
                    {
                        settings.ColumnWidths[key] = column.Width.Value;
                    }
                }
            }

            settings.Save();
        }
        catch
        {
        }
    }
}

using System;
using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
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
        Closed += OnClosed;
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        _viewModel.RefreshProcesses();

        if (ProcessDataGrid != null)
        {
            ProcessDataGrid.Sorting += OnDataGridSorting;
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

            var showItem = new MenuItem { Header = "Show OctoTask" };
            showItem.Click += (_, _) => RestoreFromTray();
            menu.Items.Add(showItem);

            var settingsItem = new MenuItem { Header = "Tray Settings..." };
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
}

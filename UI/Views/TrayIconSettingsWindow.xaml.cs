using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows;
using OctoTask.Core.Settings;

namespace OctoTask.UI.Views;

public partial class TrayIconSettingsWindow : Window, INotifyPropertyChanged
{
    private readonly AppSettings _settings;

    public bool ShowCpu
    {
        get => _settings.TrayDisplayMode == TrayDisplayMode.Cpu;
        set
        {
            if (value) _settings.TrayDisplayMode = TrayDisplayMode.Cpu;
            OnPropertyChanged();
            OnPropertyChanged(nameof(ShowRam));
        }
    }

    public bool ShowRam
    {
        get => _settings.TrayDisplayMode == TrayDisplayMode.Ram;
        set
        {
            if (value) _settings.TrayDisplayMode = TrayDisplayMode.Ram;
            OnPropertyChanged();
            OnPropertyChanged(nameof(ShowCpu));
        }
    }

    public bool MinimizeToTray
    {
        get => _settings.MinimizeToTray;
        set { _settings.MinimizeToTray = value; OnPropertyChanged(); }
    }

    public int UpdateIntervalMs
    {
        get => _settings.UpdateIntervalMs;
        set { _settings.UpdateIntervalMs = value; OnPropertyChanged(); }
    }

    public TrayIconSettingsWindow(AppSettings settings)
    {
        InitializeComponent();
        _settings = settings;
        DataContext = this;
    }

    private void OnOkClick(object sender, RoutedEventArgs e)
    {
        _settings.Save();
        DialogResult = true;
        Close();
    }

    private void OnCancelClick(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
        Close();
    }

    public event PropertyChangedEventHandler? PropertyChanged;
    protected void OnPropertyChanged([CallerMemberName] string? name = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}

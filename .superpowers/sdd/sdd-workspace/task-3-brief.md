# Task 3: Create `PortViewModel`

**Files:**
- Create: `UI/ViewModels/PortViewModel.cs`

**Interfaces:**
- Consumes: `NetworkInterop.GetAllConnections()` from Task 2
- Produces: `PortViewModel.Connections` (ObservableCollection), `PortViewModel.RefreshCommand`, `PortViewModel.SelectedConnection`, `PortViewModel.PortFilter`, `PortViewModel.ProtocolFilter`, `PortViewModel.GoToProcessCommand`, `PortViewModel.GoToProcessRequested` event

## What To Do

Create a ViewModel for the port viewer tab. Follow the existing ViewModel pattern in `UI/ViewModels/MainViewModel.cs` which uses `INotifyPropertyChanged` + `RelayCommand`.

```csharp
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using System.Windows.Threading;
using OctoTask.Core.Models;
using OctoTask.Core.Native;

namespace OctoTask.UI.ViewModels
{
    public class PortViewModel : INotifyPropertyChanged
    {
        private readonly DispatcherTimer _refreshTimer;
        private string _portFilter = string.Empty;
        private string _protocolFilter = "All";
        private ConnectionInfo? _selectedConnection;
        private bool _isAutoRefreshEnabled = true;
        private bool _isBusy;
        private string _statusText = string.Empty;
        private DateTime _lastRefresh = DateTime.MinValue;

        public ObservableCollection<ConnectionInfo> Connections { get; } = new();

        public ICommand RefreshCommand { get; }
        public ICommand ClearFilterCommand { get; }

        public string PortFilter
        {
            get => _portFilter;
            set
            {
                _portFilter = value ?? string.Empty;
                OnPropertyChanged();
                OnPropertyChanged(nameof(CanClearFilter));
                ApplyFilters();
            }
        }

        public string ProtocolFilter
        {
            get => _protocolFilter;
            set
            {
                _protocolFilter = value ?? "All";
                OnPropertyChanged();
                ApplyFilters();
            }
        }

        public ConnectionInfo? SelectedConnection
        {
            get => _selectedConnection;
            set { _selectedConnection = value; OnPropertyChanged(); OnPropertyChanged(nameof(CanGoToProcess)); }
        }

        public bool IsAutoRefreshEnabled
        {
            get => _isAutoRefreshEnabled;
            set
            {
                _isAutoRefreshEnabled = value;
                OnPropertyChanged();
                _refreshTimer.IsEnabled = value;
            }
        }

        public bool IsBusy
        {
            get => _isBusy;
            set { _isBusy = value; OnPropertyChanged(); }
        }

        public string StatusText
        {
            get => _statusText;
            set { _statusText = value; OnPropertyChanged(); }
        }

        public bool CanClearFilter => !string.IsNullOrEmpty(_portFilter);
        public bool CanGoToProcess => SelectedConnection?.Pid > 0;

        // Fired when user wants to jump to a process — MainWindow listens to this
        public event Action<int>? GoToProcessRequested;

        public PortViewModel()
        {
            RefreshCommand = new RelayCommand(_ => Refresh(), _ => !IsBusy);
            ClearFilterCommand = new RelayCommand(_ => PortFilter = string.Empty, _ => CanClearFilter);

            _refreshTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(5) };
            _refreshTimer.Tick += (_, _) => Refresh();
            _refreshTimer.IsEnabled = IsAutoRefreshEnabled;
        }

        public void Refresh()
        {
            if (IsBusy)
                return;

            try
            {
                IsBusy = true;
                StatusText = "Loading connections...";

                var allConnections = NetworkInterop.GetAllConnections();

                // Cache process names to avoid duplicate Process.GetProcessById calls
                var processCache = new Dictionary<int, string>();
                foreach (var conn in allConnections)
                {
                    if (conn.Pid > 0 && !processCache.ContainsKey(conn.Pid))
                    {
                        try
                        {
                            using var proc = System.Diagnostics.Process.GetProcessById(conn.Pid);
                            processCache[conn.Pid] = proc.ProcessName;
                        }
                        catch
                        {
                            processCache[conn.Pid] = $"PID {conn.Pid}";
                        }
                    }
                }

                // Update process names from cache
                foreach (var conn in allConnections)
                {
                    if (conn.Pid > 0 && processCache.TryGetValue(conn.Pid, out string? name))
                        conn.ProcessName = name;
                }

                Connections.Clear();
                foreach (var conn in allConnections)
                    Connections.Add(conn);

                _lastRefresh = DateTime.Now;
                ApplyFilters();

                int tcpCount = allConnections.Count(c => c.Protocol == ConnectionProtocol.TCP);
                int udpCount = allConnections.Count(c => c.Protocol == ConnectionProtocol.UDP);
                StatusText = $"Loaded {tcpCount} TCP + {udpCount} UDP connections";
            }
            catch (Exception ex)
            {
                StatusText = $"Error: {ex.Message}";
            }
            finally
            {
                IsBusy = false;
            }
        }

        public void GoToProcess()
        {
            if (SelectedConnection?.Pid > 0)
                GoToProcessRequested?.Invoke(SelectedConnection.Pid);
        }

        private void ApplyFilters()
        {
            // Filtering is done via CollectionView in the control
            // For now, we filter by rebuilding the visible set
            // The actual filtering happens in the XAML CollectionViewSource
        }

        public event PropertyChangedEventHandler? PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string? name = null)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }
}
```

## Verification

Run: `dotnet build OctoTask.csproj`
Expected: BUILD SUCCEEDED

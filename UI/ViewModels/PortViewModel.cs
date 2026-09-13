using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
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
        private DateTime _lastBandwidthSnapshot = DateTime.MinValue;
        private readonly Dictionary<int, int> _pidConnectionSnapshot = new();

        public ObservableCollection<ConnectionInfo> Connections { get; } = new();

        private readonly List<ConnectionInfo> _allConnections = new();

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
        public bool CanGoToPorts => SelectedProcessPid > 0;

        public int SelectedProcessPid { get; private set; }

        // Fired when user wants to jump to a process — MainWindow listens to this
        public event Action<int>? GoToProcessRequested;

        public void FilterByPid(int pid)
        {
            SelectedProcessPid = pid;
            OnPropertyChanged(nameof(CanGoToPorts));
            _ = Refresh();
        }

        public PortViewModel()
        {
            RefreshCommand = new RelayCommand(_ => { _ = Refresh(); }, _ => !IsBusy);
            ClearFilterCommand = new RelayCommand(_ => PortFilter = string.Empty, _ => CanClearFilter);

            _refreshTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(5) };
            _refreshTimer.Tick += async (_, _) => await Refresh();
            _refreshTimer.IsEnabled = IsAutoRefreshEnabled;
        }

        public async Task Refresh()
        {
            if (IsBusy)
                return;

            try
            {
                IsBusy = true;
                StatusText = "Loading connections...";

                var allConnections = await Task.Run(() => NetworkInterop.GetAllConnections());

                var processCache = new Dictionary<int, string>();
                await Task.Run(() =>
                {
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
                });

                Connections.Clear();
                foreach (var conn in allConnections)
                {
                    if (conn.Pid > 0 && processCache.TryGetValue(conn.Pid, out string? name))
                        conn.ProcessName = name;
                    Connections.Add(conn);
                }

                _allConnections.Clear();
                foreach (var conn in allConnections)
                    _allConnections.Add(conn);

                _lastRefresh = DateTime.Now;
                ComputeBandwidth(allConnections);
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

        private void ComputeBandwidth(List<ConnectionInfo> connections)
        {
            var now = DateTime.Now;
            var pidCounts = new Dictionary<int, int>();
            foreach (var conn in connections)
            {
                if (conn.Pid > 0)
                    pidCounts[conn.Pid] = pidCounts.GetValueOrDefault(conn.Pid) + 1;
            }

            if (_lastBandwidthSnapshot == DateTime.MinValue || (now - _lastBandwidthSnapshot).TotalSeconds < 1)
            {
                foreach (var conn in connections)
                    conn.NetworkRate = null;
            }
            else
            {
                double elapsed = (now - _lastBandwidthSnapshot).TotalSeconds;
                foreach (var kvp in pidCounts)
                {
                    int previous = _pidConnectionSnapshot.GetValueOrDefault(kvp.Key);
                    int delta = kvp.Value - previous;
                    double rate = delta / elapsed;
                    string rateStr = rate switch
                    {
                        > 0 => $"+{rate:F0}/s",
                        < 0 => $"{rate:F0}/s",
                        _ => "0/s"
                    };
                    foreach (var conn in connections.Where(c => c.Pid == kvp.Key))
                        conn.NetworkRate = rateStr;
                }
            }

            _pidConnectionSnapshot.Clear();
            foreach (var kvp in pidCounts)
                _pidConnectionSnapshot[kvp.Key] = kvp.Value;
            _lastBandwidthSnapshot = now;
        }

        private void ApplyFilters()
        {
            var filtered = new List<ConnectionInfo>();

            foreach (var conn in _allConnections)
            {
                if (_protocolFilter != "All" && conn.Protocol.ToString() != _protocolFilter)
                    continue;

                if (SelectedProcessPid > 0 && conn.Pid != SelectedProcessPid)
                    continue;

                if (!string.IsNullOrWhiteSpace(_portFilter))
                {
                    string filter = _portFilter.ToLowerInvariant();
                    bool match = conn.LocalPort.ToString().Contains(filter) ||
                                 conn.RemotePort.ToString().Contains(filter) ||
                                 conn.ProcessName.ToLowerInvariant().Contains(filter) ||
                                 conn.LocalAddress.Contains(filter) ||
                                 conn.RemoteAddress.Contains(filter);
                    if (!match)
                        continue;
                }

                filtered.Add(conn);
            }

            Connections.Clear();
            foreach (var conn in filtered)
                Connections.Add(conn);
        }

        public event PropertyChangedEventHandler? PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string? name = null)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }
}

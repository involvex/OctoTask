using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Threading;
using OctoTask.Core.Models;
using OctoTask.Core.Native;
using OctoTask.Core.Registry;

namespace OctoTask.UI.ViewModels
{
    public class MainViewModel : INotifyPropertyChanged
    {
        private readonly DispatcherTimer _refreshTimer;
        private readonly ICollectionView _collectionView;
        private ProcessInfo? _selectedProcess;
        private bool _isAutoRefreshEnabled = true;
        private bool _isBusy;
        private string _statusText = "Ready";
        private string _filterText = string.Empty;

        private string _currentSortColumn = string.Empty;
        private int _sortClickCount;

        // CPU sampling: store last TotalProcessorTime per PID
        private readonly ConcurrentDictionary<int, TimeSpan> _lastCpuTimes = new();
        private readonly Stopwatch _cpuStopwatch = new();

        public ObservableCollection<ProcessInfo> Processes { get; }

        public ICommand RefreshCommand { get; }
        public ICommand InstallHookCommand { get; }
        public ICommand UninstallHookCommand { get; }
        public ICommand RestoreHookCommand { get; }
        public ICommand KillProcessCommand { get; }
        public ICommand ToggleAutoRefreshCommand { get; }
        public ICommand ClearFilterCommand { get; }

        private ProcessDetails? _processDetails;

        public ProcessDetails? ProcessDetails
        {
            get => _processDetails;
            set { _processDetails = value; OnPropertyChanged(); }
        }

        public ProcessInfo? SelectedProcess
        {
            get => _selectedProcess;
            set
            {
                _selectedProcess = value;
                OnPropertyChanged();
                _ = LoadDetailsAsync(value?.Pid);
            }
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

        public bool CanKillProcess => SelectedProcess != null && !IsBusy;
        public bool CanRestore => TaskmgrHook.HasBackup() && !IsBusy;
        public string BackupStatusText => TaskmgrHook.HasBackup()
            ? "Backup available — use 'Restore' to revert Task Manager"
            : "No backup found — install will create one";

        public string FilterText
        {
            get => _filterText;
            set
            {
                _filterText = value ?? string.Empty;
                OnPropertyChanged();
                OnPropertyChanged(nameof(CanClearFilter));
                _collectionView.Filter = string.IsNullOrWhiteSpace(_filterText) ? null : FilterPredicate;
                _collectionView.Refresh();
            }
        }

        public bool CanClearFilter => !string.IsNullOrEmpty(_filterText);

        public MainViewModel()
        {
            Processes = new ObservableCollection<ProcessInfo>();

            // Start the CPU stopwatch
            _cpuStopwatch.Start();

            // 5-second auto-refresh timer
            _refreshTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromSeconds(5)
            };
            _refreshTimer.Tick += (_, _) => RefreshProcesses();
            _refreshTimer.IsEnabled = IsAutoRefreshEnabled;

            // Collection view for sorting
            _collectionView = CollectionViewSource.GetDefaultView(Processes);
            _collectionView.SortDescriptions.Add(new SortDescription(nameof(ProcessInfo.ProcessName), ListSortDirection.Ascending));

            RefreshCommand = new RelayCommand(_ => RefreshProcesses(), _ => !IsBusy);
            InstallHookCommand = new RelayCommand(_ => InstallHook(), _ => !IsBusy);
            UninstallHookCommand = new RelayCommand(_ => UninstallHook(), _ => !IsBusy);
            RestoreHookCommand = new RelayCommand(_ => RestoreHook(), _ => CanRestore);
            KillProcessCommand = new RelayCommand(_ => KillSelectedProcess(), _ => CanKillProcess);
            ToggleAutoRefreshCommand = new RelayCommand(_ => IsAutoRefreshEnabled = !IsAutoRefreshEnabled);
            ClearFilterCommand = new RelayCommand(_ => FilterText = string.Empty, _ => CanClearFilter);

            _currentSortColumn = nameof(ProcessInfo.ProcessName);
            _sortClickCount = 1;
        }

        private bool FilterPredicate(object? obj)
        {
            if (obj is not ProcessInfo process)
                return false;

            string filter = _filterText ?? string.Empty;
            if (string.IsNullOrWhiteSpace(filter))
                return true;

            string lowerFilter = filter.ToLowerInvariant();
            return process.ProcessName.ToLowerInvariant().Contains(lowerFilter) ||
                   process.Pid.ToString().Contains(filter) ||
                   (process.ExecutablePath?.ToLowerInvariant().Contains(lowerFilter) ?? false) ||
                   (process.CommandLine?.ToLowerInvariant().Contains(lowerFilter) ?? false);
        }

        public async void RefreshProcesses()
        {
            if (IsBusy)
                return;

            try
            {
                IsBusy = true;
                StatusText = "Refreshing processes...";

                var elapsed = _cpuStopwatch.Elapsed;
                var processList = await Task.Run(() => ProcessInterop.GetAllProcesses());

                // Calculate CPU for each process
                int processorCount = Environment.ProcessorCount;
                var newCpuTimes = new Dictionary<int, TimeSpan>();
                var processLookup = processList.ToDictionary(p => p.Pid, p => p);

                // Refresh process handles for CPU sampling
                foreach (Process proc in Process.GetProcesses().Where(p => { try { return !p.HasExited; } catch { return false; } }))
                {
                    try
                    {
                        TimeSpan currentCpu = proc.TotalProcessorTime;
                        newCpuTimes[proc.Id] = currentCpu;

                        if (_lastCpuTimes.TryGetValue(proc.Id, out TimeSpan lastCpu) && elapsed.TotalSeconds > 0)
                        {
                            double cpuTimeDeltaMs = (currentCpu - lastCpu).TotalMilliseconds;
                            double wallClockMs = elapsed.TotalMilliseconds;
                            double cpuPercent = Math.Max(0, Math.Min(100, (cpuTimeDeltaMs / wallClockMs / processorCount) * 100));

                            if (processLookup.TryGetValue(proc.Id, out ProcessInfo? info) && info != null)
                            {
                                info.CpuPercentage = cpuPercent;
                            }
                        }
                    }
                    catch
                    {
                        // Process may have exited
                    }
                }

                _lastCpuTimes.Clear();
                foreach (var kvp in newCpuTimes)
                    _lastCpuTimes[kvp.Key] = kvp.Value;

                _cpuStopwatch.Restart();

                Processes.Clear();
                foreach (var p in processList.OrderBy(p => p.ProcessName))
                    Processes.Add(p);

                StatusText = $"Loaded {Processes.Count} processes";
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

        private void InstallHook()
        {
            try
            {
                StatusText = "Installing Task Manager hook...";
                var exePath = System.Diagnostics.Process.GetCurrentProcess().MainModule?.FileName ?? "";
                if (string.IsNullOrEmpty(exePath))
                {
                    StatusText = "Error: Cannot determine executable path";
                    return;
                }

                if (TaskmgrHook.Install(exePath))
                {
                    StatusText = "Task Manager hook installed successfully";
                    OnPropertyChanged(nameof(BackupStatusText));
                }
                else
                {
                    StatusText = "Failed to install hook — are you running as administrator?";
                }
            }
            catch (Exception ex)
            {
                StatusText = $"Error: {ex.Message}";
            }
        }

        private void UninstallHook()
        {
            try
            {
                StatusText = "Removing Task Manager hook...";
                if (TaskmgrHook.Uninstall())
                {
                    StatusText = "Task Manager hook removed successfully";
                    OnPropertyChanged(nameof(BackupStatusText));
                }
                else
                {
                    StatusText = "Failed to remove hook — are you running as administrator?";
                }
            }
            catch (Exception ex)
            {
                StatusText = $"Error: {ex.Message}";
            }
        }

        private void RestoreHook()
        {
            try
            {
                StatusText = "Restoring original Task Manager...";
                if (TaskmgrHook.Restore())
                {
                    StatusText = "Task Manager restored successfully";
                    OnPropertyChanged(nameof(BackupStatusText));
                }
                else
                {
                    StatusText = "Failed to restore — are you running as administrator?";
                }
            }
            catch (Exception ex)
            {
                StatusText = $"Error: {ex.Message}";
            }
        }

        private async Task LoadDetailsAsync(int? pid)
        {
            if (pid == null)
            {
                ProcessDetails = null;
                return;
            }

            ProcessDetails = null;
            StatusText = "Loading process details...";

            try
            {
                var details = await Task.Run(() => ProcessInterop.LoadProcessDetails(pid.Value));
                ProcessDetails = details;

                StatusText = details != null
                    ? $"Loaded details for PID {pid}"
                    : $"Could not load details for PID {pid}";
            }
            catch (Exception ex)
            {
                StatusText = $"Error: {ex.Message}";
            }
        }

        private void KillSelectedProcess()
        {
            if (SelectedProcess == null)
                return;

            if (ProcessInterop.KillProcess(SelectedProcess.Pid))
            {
                Processes.Remove(SelectedProcess);
                _lastCpuTimes.TryRemove(SelectedProcess.Pid, out _);
                SelectedProcess = null;
                StatusText = "Process terminated";
            }
            else
            {
                StatusText = "Failed to kill process — access denied or process already exited";
            }
        }

        #region Sorting

        public void SetSort(string columnName)
        {
            // 3-click cycle: Ascending → Descending → None (unsorted)
            if (_currentSortColumn == columnName)
            {
                _sortClickCount++;
                if (_sortClickCount > 3)
                    _sortClickCount = 1;
            }
            else
            {
                _currentSortColumn = columnName;
                _sortClickCount = 1;
            }

            _collectionView.SortDescriptions.Clear();

            switch (_sortClickCount)
            {
                case 1:
                    _collectionView.SortDescriptions.Add(new SortDescription(columnName, ListSortDirection.Ascending));
                    break;
                case 2:
                    _collectionView.SortDescriptions.Add(new SortDescription(columnName, ListSortDirection.Descending));
                    break;
                case 3:
                    // No sort — unsorted
                    break;
            }
        }

        #endregion

        public event PropertyChangedEventHandler? PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string? name = null)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }
}

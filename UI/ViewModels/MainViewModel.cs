using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Windows.Data;
using System.Windows.Input;
using System.Management;
using System.Windows.Threading;
using OctoTask.Core.Models;
using OctoTask.Core.Native;
using OctoTask.Core.Registry;
using OctoTask.UI.Views;

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
        private double _systemCpuUsage;
        private double _systemRamUsage;
        private ulong _systemRamTotal;
        private ulong _systemRamUsed;
        private bool _isTreeView;

        private readonly ConcurrentDictionary<int, TimeSpan> _lastCpuTimes;
        private readonly Stopwatch _cpuStopwatch;
        private readonly ConcurrentDictionary<int, int> _parentPidCache;
        private DateTime _parentPidCacheExpiry;
        private readonly PortViewModel _portVM;

        private const int ParentPidCacheSeconds = 60;

        public ObservableCollection<ProcessInfo> Processes { get; }
        public ObservableCollection<ProcessInfo> ProcessTree { get; } = new();
        public PortViewModel PortVM => _portVM;

        public bool IsTreeView
        {
            get => _isTreeView;
            set { _isTreeView = value; OnPropertyChanged(); }
        }

        public ICommand RefreshCommand { get; }
        public ICommand InstallHookCommand { get; }
        public ICommand UninstallHookCommand { get; }
        public ICommand RestoreHookCommand { get; }
        public ICommand KillProcessCommand { get; }
        public ICommand ToggleAutoRefreshCommand { get; }
        public ICommand ClearFilterCommand { get; }
        public ICommand ToggleViewCommand { get; }
        public ICommand SuspendProcessCommand { get; }
        public ICommand ResumeProcessCommand { get; }
        public ICommand ExportCsvCommand { get; }
        public ICommand ExportJsonCommand { get; }
        public ICommand OpenTraySettingsCommand { get; }

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

        public double SystemCpuUsage
        {
            get => _systemCpuUsage;
            set { _systemCpuUsage = value; OnPropertyChanged(); }
        }

        public double SystemRamUsage
        {
            get => _systemRamUsage;
            set { _systemRamUsage = value; OnPropertyChanged(); }
        }

        public ulong SystemRamTotal
        {
            get => _systemRamTotal;
            set { _systemRamTotal = value; OnPropertyChanged(); }
        }

        public ulong SystemRamUsed
        {
            get => _systemRamUsed;
            set { _systemRamUsed = value; OnPropertyChanged(); }
        }

        public string SystemRamDisplay
        {
            get
            {
                string used = FormatBytes((long)_systemRamUsed);
                string total = FormatBytes((long)_systemRamTotal);
                return $"{used} / {total} ({_systemRamUsage:F1}%)";
            }
        }

        public string SystemCpuTotalDisplay
        {
            get => $"{_systemCpuUsage:F1}%";
            set { OnPropertyChanged(nameof(SystemCpuTotalDisplay)); }
        }

        private static string FormatBytes(long bytes)
        {
            if (bytes < 1024)
                return $"{bytes} B";
            if (bytes < 1024 * 1024)
                return $"{bytes / 1024.0:F1} KB";
            if (bytes < 1024 * 1024 * 1024)
                return $"{bytes / (1024.0 * 1024):F1} MB";
            return $"{bytes / (1024.0 * 1024 * 1024):F1} GB";
        }

        public MainViewModel()
        {
            Processes = new ObservableCollection<ProcessInfo>();

            _parentPidCache = new ConcurrentDictionary<int, int>();
            _parentPidCacheExpiry = DateTime.MinValue;

            _lastCpuTimes = new ConcurrentDictionary<int, TimeSpan>();
            _cpuStopwatch = new();
            _cpuStopwatch.Start();

            _refreshTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromSeconds(5)
            };
            _refreshTimer.Tick += (_, _) => RefreshProcesses();
            _refreshTimer.IsEnabled = IsAutoRefreshEnabled;

            _collectionView = CollectionViewSource.GetDefaultView(Processes);
            _collectionView.SortDescriptions.Add(new SortDescription(nameof(ProcessInfo.ProcessName), ListSortDirection.Ascending));

            RefreshCommand = new RelayCommand(_ => RefreshProcesses(), _ => !IsBusy);
            InstallHookCommand = new RelayCommand(_ => InstallHook(), _ => !IsBusy);
            UninstallHookCommand = new RelayCommand(_ => UninstallHook(), _ => !IsBusy);
            RestoreHookCommand = new RelayCommand(_ => RestoreHook(), _ => CanRestore);
            KillProcessCommand = new RelayCommand(_ => KillSelectedProcess(), _ => CanKillProcess);
            ToggleAutoRefreshCommand = new RelayCommand(_ => IsAutoRefreshEnabled = !IsAutoRefreshEnabled);
            ClearFilterCommand = new RelayCommand(_ => FilterText = string.Empty, _ => CanClearFilter);
            ToggleViewCommand = new RelayCommand(_ => IsTreeView = !IsTreeView);
            SuspendProcessCommand = new RelayCommand(_ => SuspendSelectedProcess(), _ => CanKillProcess);
            ResumeProcessCommand = new RelayCommand(_ => ResumeSelectedProcess(), _ => CanKillProcess);
            ExportCsvCommand = new RelayCommand(_ => ExportProcesses("csv"));
            ExportJsonCommand = new RelayCommand(_ => ExportProcesses("json"));
            OpenTraySettingsCommand = new RelayCommand(_ =>
            {
                var win = new TrayIconSettingsWindow(Core.Settings.AppSettings.Load());
                win.Owner = System.Windows.Application.Current.MainWindow;
                if (win.ShowDialog() == true)
                {
                    // Settings reloaded by MainWindow if needed
                }
            });

            _currentSortColumn = nameof(ProcessInfo.ProcessName);
            _sortClickCount = 1;

            _portVM = new PortViewModel();
            _portVM.GoToProcessRequested += SelectProcessByPid;
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
                var snapshot = await Task.Run(() => RefreshProcessesInternal(elapsed));

                // Apply incremental changes on UI thread
                ApplyRefreshResult(snapshot);

                // Update system telemetry on UI thread
                SystemRamTotal = snapshot.TotalRam;
                SystemRamUsed = snapshot.TotalWorkingSet;
                SystemRamUsage = snapshot.TotalRam > 0 ? (snapshot.TotalWorkingSet / (double)snapshot.TotalRam) * 100 : 0;
                SystemCpuUsage = Math.Min(100, snapshot.TotalCpu);
                OnPropertyChanged(nameof(SystemRamDisplay));
                OnPropertyChanged(nameof(SystemCpuTotalDisplay));

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

        private class ProcessSnapshot
        {
            public int Pid { get; set; }
            public string ProcessName { get; set; } = string.Empty;
            public string ExecutablePath { get; set; } = string.Empty;
            public string CommandLine { get; set; } = string.Empty;
            public long WorkingSetBytes { get; set; }
            public double WorkingSetPercentage { get; set; }
            public double CpuPercentage { get; set; }
            public TimeSpan TotalProcessorTime { get; set; }
            public int ParentPid { get; set; }
            public List<ProcessSnapshot> ProcessList { get; set; } = new();
            public ulong TotalRam { get; set; }
            public ulong TotalWorkingSet { get; set; }
            public double TotalCpu { get; set; }
        }

        private ProcessSnapshot RefreshProcessesInternal(TimeSpan elapsed)
        {
            SystemInfo.Refresh();
            ulong totalRam = SystemInfo.TotalPhysicalMemory;

            int processorCount = Environment.ProcessorCount;
            var newCpuTimes = new Dictionary<int, TimeSpan>();
            var cpuLookup = new Dictionary<int, TimeSpan>();

            // Single enumeration: get CPU times for all processes
            foreach (Process proc in Process.GetProcesses().Where(p => { try { return !p.HasExited; } catch { return false; } }))
            {
                try
                {
                    TimeSpan currentCpu = proc.TotalProcessorTime;
                    newCpuTimes[proc.Id] = currentCpu;

                    if (elapsed.TotalSeconds > 0)
                        cpuLookup[proc.Id] = currentCpu;
                }
                catch
                {
                }
            }

            // Build the process snapshot list
            var processList = new List<ProcessSnapshot>();
            ulong totalWorkingSet = 0;
            double totalCpu = 0;

            foreach (var proc in Process.GetProcesses().Where(p => { try { return !p.HasExited; } catch { return false; } }))
            {
                try
                {
                    var info = ProcessInterop.ReadProcessFromPeb(proc);
                    if (info == null)
                        continue;

                    var snap = new ProcessSnapshot
                    {
                        Pid = info.Pid,
                        ProcessName = info.ProcessName,
                        ExecutablePath = info.ExecutablePath,
                        CommandLine = info.CommandLine,
                        WorkingSetBytes = proc.WorkingSet64,
                    };

                    if (_lastCpuTimes.TryGetValue(proc.Id, out TimeSpan lastCpu) && cpuLookup.TryGetValue(proc.Id, out TimeSpan currentCpu))
                    {
                        double cpuTimeDeltaMs = (currentCpu - lastCpu).TotalMilliseconds;
                        double wallClockMs = elapsed.TotalMilliseconds;
                        double cpuPercent = Math.Max(0, Math.Min(100, (cpuTimeDeltaMs / wallClockMs / processorCount) * 100));
                        snap.CpuPercentage = cpuPercent;
                        snap.TotalProcessorTime = currentCpu;
                        totalCpu += cpuPercent;
                    }
                    else
                    {
                        snap.TotalProcessorTime = cpuLookup.GetValueOrDefault(proc.Id, TimeSpan.Zero);
                    }

                    if (totalRam > 0)
                    {
                        snap.WorkingSetPercentage = (snap.WorkingSetBytes / (double)totalRam) * 100;
                        totalWorkingSet += (ulong)snap.WorkingSetBytes;
                    }

                    processList.Add(snap);
                }
                catch
                {
                }
            }

            _lastCpuTimes.Clear();
            foreach (var kvp in newCpuTimes)
                _lastCpuTimes[kvp.Key] = kvp.Value;

            _cpuStopwatch.Restart();

            return new ProcessSnapshot
            {
                TotalRam = totalRam,
                TotalWorkingSet = totalWorkingSet,
                TotalCpu = totalCpu,
                ProcessList = processList
            };
        }

        private void ApplyRefreshResult(ProcessSnapshot snapshot)
        {
            var processList = snapshot.ProcessList;

            foreach (var p in processList)
            {
                if (_parentPidCache.TryGetValue(p.Pid, out int parentPid))
                    p.ParentPid = parentPid;
            }

            var oldLookup = new Dictionary<int, ProcessInfo>();
            foreach (var p in Processes)
                oldLookup[p.Pid] = p;

            var newLookup = new Dictionary<int, ProcessInfo>();
            foreach (var p in processList)
            {
                var info = new ProcessInfo
                {
                    Pid = p.Pid,
                    ProcessName = p.ProcessName,
                    ExecutablePath = p.ExecutablePath,
                    CommandLine = p.CommandLine,
                    WorkingSetBytes = p.WorkingSetBytes,
                    WorkingSetPercentage = p.WorkingSetPercentage,
                    CpuPercentage = p.CpuPercentage,
                    TotalProcessorTime = p.TotalProcessorTime,
                    ParentPid = p.ParentPid
                };
                newLookup[p.Pid] = info;
            }

            var toRemove = oldLookup.Keys.Except(newLookup.Keys).ToList();
            var toUpdate = oldLookup.Keys.Intersect(newLookup.Keys).ToList();
            var toAdd = newLookup.Keys.Except(oldLookup.Keys).ToList();

            foreach (int pid in toRemove)
                Processes.Remove(oldLookup[pid]);

            foreach (int pid in toUpdate)
            {
                var oldP = oldLookup[pid];
                var newP = newLookup[pid];
                oldP.WorkingSetBytes = newP.WorkingSetBytes;
                oldP.WorkingSetPercentage = newP.WorkingSetPercentage;
                oldP.CpuPercentage = newP.CpuPercentage;
                oldP.TotalProcessorTime = newP.TotalProcessorTime;
                oldP.ExecutablePath = newP.ExecutablePath;
                oldP.CommandLine = newP.CommandLine;
                oldP.ProcessName = newP.ProcessName;
            }

            var sortedNew = toAdd.Select(pid => newLookup[pid]).OrderBy(p => p.ProcessName).ToList();
            foreach (var p in sortedNew)
                Processes.Add(p);
        }

        private void BuildProcessTree(List<ProcessInfo> processList)
        {
            ProcessTree.Clear();
            foreach (var p in processList)
                p.Children.Clear();

            var lookup = new Dictionary<int, ProcessInfo>();
            foreach (var p in processList)
                lookup[p.Pid] = p;

            // Use cached parent PIDs if fresh; otherwise query WMI
            bool useCache = _parentPidCacheExpiry > DateTime.UtcNow
                            && _parentPidCache.Count > 0
                            && _parentPidCache.Count >= processList.Count * 80 / 100;

            if (!useCache)
            {
                _parentPidCache.Clear();
                try
                {
                    using var searcher = new ManagementObjectSearcher(
                        "SELECT ProcessId, ParentProcessId FROM Win32_Process");
                    foreach (ManagementObject mo in searcher.Get())
                    {
                        try
                        {
                            int pid = Convert.ToInt32(mo["ProcessId"]);
                            int parentPid = Convert.ToInt32(mo["ParentProcessId"]);
                            _parentPidCache[pid] = parentPid;
                        }
                        catch { }
                    }
                }
                catch { }

                _parentPidCacheExpiry = DateTime.UtcNow.AddSeconds(ParentPidCacheSeconds);
            }

            foreach (var p in processList)
            {
                if (_parentPidCache.TryGetValue(p.Pid, out int parentPid) && parentPid != 0)
                    p.ParentPid = parentPid;
                else
                    p.ParentPid = 0;

                if (p.ParentPid != 0 && lookup.TryGetValue(p.ParentPid, out var parent))
                    parent.Children.Add(p);
                else
                    ProcessTree.Add(p);
            }
        }

        private class RefreshResult
        {
            public ulong TotalRam { get; set; }
            public ulong TotalWorkingSet { get; set; }
            public double TotalCpu { get; set; }
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

        private void SuspendSelectedProcess()
        {
            if (SelectedProcess == null)
                return;

            if (ProcessInterop.SuspendProcess(SelectedProcess.Pid))
                StatusText = $"Suspended PID {SelectedProcess.Pid}";
            else
                StatusText = $"Failed to suspend PID {SelectedProcess.Pid} — access denied";
        }

        private void ResumeSelectedProcess()
        {
            if (SelectedProcess == null)
                return;

            if (ProcessInterop.ResumeProcess(SelectedProcess.Pid))
                StatusText = $"Resumed PID {SelectedProcess.Pid}";
            else
                StatusText = $"Failed to resume PID {SelectedProcess.Pid} — access denied";
        }

        private void ExportProcesses(string format)
        {
            var dialog = new Microsoft.Win32.SaveFileDialog
            {
                FileName = $"processes.{format}",
                DefaultExt = $".{format}",
                Filter = format == "csv"
                    ? "CSV files (*.csv)|*.csv|All files (*.*)|*.*"
                    : "JSON files (*.json)|*.json|All files (*.*)|*.*"
            };

            if (dialog.ShowDialog() != true)
                return;

            try
            {
                string content = format == "csv" ? ExportAsCsv() : ExportAsJson();
                File.WriteAllText(dialog.FileName, content);
                StatusText = $"Exported {Processes.Count} processes to {dialog.FileName}";
            }
            catch (Exception ex)
            {
                StatusText = $"Export failed: {ex.Message}";
            }
        }

        private string ExportAsJson()
        {
            var export = Processes.Select(p => new
            {
                p.Pid,
                p.ProcessName,
                p.ExecutablePath,
                WorkingSetMB = Math.Round(p.WorkingSetBytes / (1024.0 * 1024), 1),
                CpuPercentage = Math.Round(p.CpuPercentage, 1)
            });
            return System.Text.Json.JsonSerializer.Serialize(export, new System.Text.Json.JsonSerializerOptions { WriteIndented = true });
        }

        private string ExportAsCsv()
        {
            var sb = new System.Text.StringBuilder();
            sb.AppendLine("PID,ProcessName,ExecutablePath,WorkingSetMB,CpuPercentage");
            foreach (var p in Processes)
            {
                string name = EscapeCsvField(p.ProcessName);
                string path = EscapeCsvField(p.ExecutablePath);
                double wsMB = Math.Round(p.WorkingSetBytes / (1024.0 * 1024), 1);
                double cpu = Math.Round(p.CpuPercentage, 1);
                sb.AppendLine($"{p.Pid},{name},{path},{wsMB},{cpu}");
            }
            return sb.ToString();
        }

        private static string EscapeCsvField(string? field)
        {
            if (string.IsNullOrEmpty(field))
                return "\"\"";
            if (field.Contains(',') || field.Contains('"') || field.Contains('\n'))
                return "\"" + field.Replace("\"", "\"\"") + "\"";
            return "\"" + field + "\"";
        }

        public void SelectProcessByPid(int pid)
        {
            var match = Processes.FirstOrDefault(p => p.Pid == pid);
            if (match != null)
            {
                SelectedProcess = match;
                IsTreeView = false;
            }
            else
            {
                StatusText = $"PID {pid} not found in process list — try refreshing first";
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

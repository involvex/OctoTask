using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
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

        private string _currentSortColumn = string.Empty;
        private int _sortClickCount;

        public ObservableCollection<ProcessInfo> Processes { get; }

        public ICommand RefreshCommand { get; }
        public ICommand InstallHookCommand { get; }
        public ICommand UninstallHookCommand { get; }
        public ICommand RestoreHookCommand { get; }
        public ICommand KillProcessCommand { get; }
        public ICommand ToggleAutoRefreshCommand { get; }

        public ProcessInfo? SelectedProcess
        {
            get => _selectedProcess;
            set { _selectedProcess = value; OnPropertyChanged(); }
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

        public MainViewModel()
        {
            Processes = new ObservableCollection<ProcessInfo>();

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

            _currentSortColumn = nameof(ProcessInfo.ProcessName);
            _sortClickCount = 1;
        }

        public async void RefreshProcesses()
        {
            if (IsBusy)
                return;

            try
            {
                IsBusy = true;
                StatusText = "Refreshing processes...";

                var processList = await Task.Run(() => ProcessInterop.GetAllProcesses());

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

        private void KillSelectedProcess()
        {
            if (SelectedProcess == null)
                return;

            if (ProcessInterop.KillProcess(SelectedProcess.Pid))
            {
                Processes.Remove(SelectedProcess);
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

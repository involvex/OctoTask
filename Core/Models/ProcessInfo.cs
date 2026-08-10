using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace OctoTask.Core.Models
{
    public class ProcessInfo : INotifyPropertyChanged
    {
        private long _workingSetBytes;
        private double _cpuPercentage;
        private TimeSpan _totalProcessorTime;
        private double _workingSetPercentage;
        private ProcessDetails? _details;

        public int Pid { get; set; }
        public int ParentPid { get; set; }
        public string ProcessName { get; set; } = string.Empty;
        public string ExecutablePath { get; set; } = string.Empty;
        public string CommandLine { get; set; } = string.Empty;
        public ObservableCollection<ProcessInfo> Children { get; } = new();

        public long WorkingSetBytes
        {
            get => _workingSetBytes;
            set
            {
                _workingSetBytes = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(WorkingSetDisplay));
            }
        }

        public double WorkingSetPercentage
        {
            get => _workingSetPercentage;
            set
            {
                _workingSetPercentage = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(WorkingSetDisplay));
            }
        }

        public string WorkingSetDisplay
        {
            get
            {
                string bytes = FormatBytes(_workingSetBytes);
                if (_workingSetPercentage > 0)
                    return $"{bytes} ({_workingSetPercentage:F1}%)";
                return bytes;
            }
        }

        public double CpuPercentage
        {
            get => _cpuPercentage;
            set
            {
                _cpuPercentage = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(CpuPercentageDisplay));
            }
        }

        public TimeSpan TotalProcessorTime
        {
            get => _totalProcessorTime;
            set
            {
                _totalProcessorTime = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(CpuPercentageDisplay));
            }
        }

        public string CpuPercentageDisplay
        {
            get
            {
                string cpuTime = FormatTime(_totalProcessorTime);
                return $"{_cpuPercentage:F1}% ({cpuTime})";
            }
        }

        public ProcessDetails? Details
        {
            get => _details;
            set { _details = value; OnPropertyChanged(); }
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

        private static string FormatTime(TimeSpan ts)
        {
            if (ts.TotalHours >= 1)
                return $"{(int)ts.TotalHours}h{ts.Minutes}m";
            if (ts.TotalMinutes >= 1)
                return $"{ts.Minutes}m{ts.Seconds}s";
            return $"{ts.Seconds}s";
        }

        public event PropertyChangedEventHandler? PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string? name = null)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }

    public class ProcessDetails : INotifyPropertyChanged
    {
        private string _userName = string.Empty;
        private string _domain = string.Empty;
        private string _processOwner = string.Empty;
        private string _description = string.Empty;
        private string _company = string.Empty;
        private string _version = string.Empty;
        private string _fileVersion = string.Empty;
        private string _productVersion = string.Empty;
        private string _workingDirectory = string.Empty;
        private string _parentProcess = string.Empty;
        private int _parentId;
        private string _startTime = string.Empty;
        private string _runningTime = string.Empty;
        private string _handles = string.Empty;
        private string _threads = string.Empty;
        private string _priority = string.Empty;
        private bool _isResponding = true;
        private string _session = string.Empty;
        private List<string>? _environmentVariables;
        private List<ModuleInfo>? _modules;

        public string UserName { get => _userName; set { _userName = value; OnPropertyChanged(); } }
        public string Domain { get => _domain; set { _domain = value; OnPropertyChanged(); } }
        public string ProcessOwner { get => _processOwner; set { _processOwner = value; OnPropertyChanged(); } }
        public string Description { get => _description; set { _description = value; OnPropertyChanged(); } }
        public string Company { get => _company; set { _company = value; OnPropertyChanged(); } }
        public string Version { get => _version; set { _version = value; OnPropertyChanged(); } }
        public string FileVersion { get => _fileVersion; set { _fileVersion = value; OnPropertyChanged(); } }
        public string ProductVersion { get => _productVersion; set { _productVersion = value; OnPropertyChanged(); } }
        public string WorkingDirectory { get => _workingDirectory; set { _workingDirectory = value; OnPropertyChanged(); } }
        public string ParentProcess { get => _parentProcess; set { _parentProcess = value; OnPropertyChanged(); } }
        public int ParentId { get => _parentId; set { _parentId = value; OnPropertyChanged(); } }
        public string StartTime { get => _startTime; set { _startTime = value; OnPropertyChanged(); } }
        public string RunningTime { get => _runningTime; set { _runningTime = value; OnPropertyChanged(); } }
        public string Handles { get => _handles; set { _handles = value; OnPropertyChanged(); } }
        public string Threads { get => _threads; set { _threads = value; OnPropertyChanged(); } }
        public string Priority { get => _priority; set { _priority = value; OnPropertyChanged(); } }
        public bool IsResponding { get => _isResponding; set { _isResponding = value; OnPropertyChanged(); } }
        public string Session { get => _session; set { _session = value; OnPropertyChanged(); } }
        public List<string>? EnvironmentVariables { get => _environmentVariables; set { _environmentVariables = value; OnPropertyChanged(); } }
        public List<ModuleInfo>? Modules { get => _modules; set { _modules = value; OnPropertyChanged(); } }

        public event PropertyChangedEventHandler? PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string? name = null)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }

    public class ModuleInfo
    {
        public string Name { get; set; } = string.Empty;
        public string FileName { get; set; } = string.Empty;
        public string Version { get; set; } = string.Empty;
        public long Size { get; set; }
        public string SizeDisplay => FormatSize(Size);

        private static string FormatSize(long bytes)
        {
            if (bytes < 1024)
                return $"{bytes} B";
            if (bytes < 1024 * 1024)
                return $"{bytes / 1024.0:F1} KB";
            if (bytes < 1024 * 1024 * 1024)
                return $"{bytes / (1024.0 * 1024):F1} MB";
            return $"{bytes / (1024.0 * 1024 * 1024):F1} GB";
        }
    }
}

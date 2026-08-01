using System;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace OctoTask.Core.Models
{
    public class ProcessInfo : INotifyPropertyChanged
    {
        private long _workingSetBytes;

        public int Pid { get; set; }
        public string ProcessName { get; set; } = string.Empty;
        public string ExecutablePath { get; set; } = string.Empty;
        public string CommandLine { get; set; } = string.Empty;

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

        public string WorkingSetDisplay => FormatBytes(_workingSetBytes);

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

        public event PropertyChangedEventHandler? PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string? name = null)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }
}

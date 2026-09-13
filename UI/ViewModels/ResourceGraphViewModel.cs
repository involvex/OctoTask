using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Threading;
using OctoTask.Core.Settings;

namespace OctoTask.UI.ViewModels
{
    public class ResourceGraphViewModel : INotifyPropertyChanged
    {
        private const int BufferSize = 60;

        private readonly CircularBuffer _cpuBuffer = new(BufferSize);
        private readonly CircularBuffer _ramBuffer = new(BufferSize);

        private readonly DispatcherTimer _sampleTimer;
        private readonly Func<double> _getCpu;
        private readonly Func<double> _getRam;

        private bool _isExpanded = true;
        private int _pointCount;

        public List<Point> CpuPoints { get; } = new();
        public List<Point> RamPoints { get; } = new();

        public string CpuDisplay => $"CPU: {(_cpuBuffer.Count > 0 ? _cpuBuffer[_cpuBuffer.Count - 1] : 0):F1}%";

        public bool IsExpanded
        {
            get => _isExpanded;
            set { _isExpanded = value; OnPropertyChanged(); }
        }

        public ResourceGraphViewModel(Func<double> getCpu, Func<double> getRam)
        {
            _getCpu = getCpu;
            _getRam = getRam;

            _sampleTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromSeconds(1)
            };
            _sampleTimer.Tick += (_, _) => Sample();
        }

        public void Start()
        {
            _sampleTimer.Start();
            Sample();
        }

        public void Stop()
        {
            _sampleTimer.Stop();
        }

        private void Sample()
        {
            double cpu = _getCpu();
            double ram = _getRam();

            _cpuBuffer.Add(cpu);
            _ramBuffer.Add(ram);
            _pointCount++;

            RebuildPoints();
            OnPropertyChanged(nameof(CpuPoints));
            OnPropertyChanged(nameof(RamPoints));
        }

        private void RebuildPoints()
        {
            CpuPoints.Clear();
            RamPoints.Clear();

            int count = _cpuBuffer.Count;
            for (int i = 0; i < count; i++)
            {
                double x = i;
                CpuPoints.Add(new Point(x, _cpuBuffer[i]));
                RamPoints.Add(new Point(x, _ramBuffer[i]));
            }
        }

        public event PropertyChangedEventHandler? PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string? name = null)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));

        private class CircularBuffer
        {
            private readonly double[] _data;
            private int _index;
            private int _count;
            private readonly int _capacity;

            public CircularBuffer(int capacity)
            {
                _capacity = capacity;
                _data = new double[capacity];
                _index = 0;
                _count = 0;
            }

            public int Count => _count;

            public void Add(double value)
            {
                _data[_index] = value;
                _index = (_index + 1) % _capacity;
                if (_count < _capacity) _count++;
            }

            public double this[int i]
            {
                get
                {
                    if (i < 0 || i >= _count) throw new IndexOutOfRangeException();
                    int actual = (_index - _count + i + _capacity) % _capacity;
                    return _data[actual];
                }
            }
        }
    }
}

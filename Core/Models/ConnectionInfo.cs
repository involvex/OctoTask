using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace OctoTask.Core.Models
{
    public enum ConnectionProtocol
    {
        TCP,
        UDP
    }

    public enum ConnectionState
    {
        Closed = 1,
        Listen = 2,
        SynSent = 3,
        SynReceived = 4,
        Established = 5,
        FinWait1 = 6,
        FinWait2 = 7,
        CloseWait = 8,
        Closing = 9,
        LastAck = 10,
        TimeWait = 11,
        DeleteTcb = 12,
        Unknown = 0
    }

    public class ConnectionInfo : INotifyPropertyChanged
    {
        private int _pid;
        private string _processName = string.Empty;
        private string _localAddress = string.Empty;
        private ushort _localPort;
        private string _remoteAddress = string.Empty;
        private ushort _remotePort;
        private ConnectionState _state;
        private ConnectionProtocol _protocol;

        public int Pid
        {
            get => _pid;
            set { _pid = value; OnPropertyChanged(); OnPropertyChanged(nameof(PidDisplay)); }
        }

        public string ProcessName
        {
            get => _processName;
            set { _processName = value; OnPropertyChanged(); }
        }

        public string LocalAddress
        {
            get => _localAddress;
            set { _localAddress = value; OnPropertyChanged(); }
        }

        public ushort LocalPort
        {
            get => _localPort;
            set { _localPort = value; OnPropertyChanged(); OnPropertyChanged(nameof(LocalPortDisplay)); }
        }

        public string LocalPortDisplay => _localPort > 0 ? _localPort.ToString() : "*";

        public string RemoteAddress
        {
            get => _remoteAddress;
            set { _remoteAddress = value; OnPropertyChanged(); }
        }

        public ushort RemotePort
        {
            get => _remotePort;
            set { _remotePort = value; OnPropertyChanged(); OnPropertyChanged(nameof(RemotePortDisplay)); }
        }

        public string RemotePortDisplay => _remotePort > 0 ? _remotePort.ToString() : "*";

        public ConnectionState State
        {
            get => _state;
            set { _state = value; OnPropertyChanged(); OnPropertyChanged(nameof(StateDisplay)); }
        }

        public string StateDisplay => _state switch
        {
            ConnectionState.Listen => "LISTENING",
            ConnectionState.Established => "ESTABLISHED",
            ConnectionState.TimeWait => "TIME_WAIT",
            ConnectionState.CloseWait => "CLOSE_WAIT",
            ConnectionState.SynSent => "SYN_SENT",
            ConnectionState.SynReceived => "SYN_RECEIVED",
            ConnectionState.FinWait1 => "FIN_WAIT_1",
            ConnectionState.FinWait2 => "FIN_WAIT_2",
            ConnectionState.Closing => "CLOSING",
            ConnectionState.LastAck => "LAST_ACK",
            ConnectionState.Closed => "CLOSED",
            ConnectionState.DeleteTcb => "DELETE_TCB",
            _ => ""
        };

        public ConnectionProtocol Protocol
        {
            get => _protocol;
            set { _protocol = value; OnPropertyChanged(); OnPropertyChanged(nameof(ProtocolDisplay)); }
        }

        public string ProtocolDisplay => _protocol.ToString();

        public string LocalEndpoint => _localPort > 0 ? $"{_localAddress}:{_localPort}" : _localAddress;
        public string RemoteEndpoint => _remotePort > 0 ? $"{_remoteAddress}:{_remotePort}" : _remoteAddress;

        public string PidDisplay => _pid > 0 ? _pid.ToString() : "-";

        public event PropertyChangedEventHandler? PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string? name = null)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }
}

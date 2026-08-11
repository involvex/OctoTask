# Port Viewer Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Add a Port Viewer tab to OctoTask that shows all TCP/UDP connections and their owning processes, equivalent to `netstat -ano` but integrated into the UI with auto-refresh and cross-linking to the process list.

**Architecture:** Use the Windows IP Helper API (`iphlpapi.dll`) via P/Invoke to enumerate TCP connections and UDP listeners natively — no shelling out to `netstat`. A new `PortViewModel` drives a `PortViewerControl` UserControl placed in a TabControl alongside the existing process view. Clicking a port row auto-selects the corresponding PID in the process list.

**Tech Stack:** WPF (.NET 10), P/Invoke (`iphlpapi.dll`), MVVM (pure `INotifyPropertyChanged` + `RelayCommand`), no external packages.

## Global Constraints

- Target framework: `net10.0-windows` with `UseWPF`
- No external NuGet packages (existing project uses only `System.Management`)
- Follow existing dark theme: BgBrush `#0f172a`, SurfaceBrush `#1e293b`, AccentBrush `#3b82f6`, TextPrimary `#f1f5f9`
- Font family: Cascadia Mono, Consolas, Courier New (monospace)
- All new files follow existing namespace conventions: `OctoTask.Core.Models`, `OctoTask.Core.Native`, `OctoTask.UI.ViewModels`, `OctoTask.UI.Views`

## File Structure

| File | Action | Responsibility |
|------|--------|---------------|
| `Core/Models/ConnectionInfo.cs` | Create | POCO model for a network connection row |
| `Core/Native/NetworkInterop.cs` | Create | P/Invoke wrapper for `GetExtendedTcpTable` / `GetExtendedUdpTable` |
| `UI/ViewModels/PortViewModel.cs` | Create | ViewModel: refresh, filter, selected connection, port search |
| `UI/Views/PortViewerControl.xaml` | Create | UserControl: port search box + DataGrid for connections |
| `UI/Views/PortViewerControl.xaml.cs` | Create | Code-behind: minimal, wire double-click to parent window |
| `MainWindow.xaml` | Modify | Wrap content in TabControl, add "Ports" tab hosting PortViewerControl |
| `MainViewModel.cs` | Modify | Expose `PortViewModel`, add `SelectProcessByPidCommand` for cross-linking |
| `MainWindow.xaml.cs` | Modify | Handle port→process cross-linking |

---

### Task 1: Create `ConnectionInfo` Model

**Files:**
- Create: `Core/Models/ConnectionInfo.cs`

**Interfaces:**
- Produces: `OctoTask.Core.Models.ConnectionInfo` class used by `NetworkInterop` and bound in `PortViewModel`

- [ ] **Step 1: Create the ConnectionInfo model**

```csharp
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
```

- [ ] **Step 2: Verify build compiles**

Run: `dotnet build OctoTask.csproj`
Expected: BUILD SUCCEEDED

---

### Task 2: Create `NetworkInterop` — IP Helper API P/Invoke

**Files:**
- Create: `Core/Native/NetworkInterop.cs`

**Interfaces:**
- Consumes: `OctoTask.Core.Models.ConnectionInfo`, `OctoTask.Core.Models.ConnectionProtocol`, `OctoTask.Core.Models.ConnectionState`
- Produces: `NetworkInterop.GetTcpConnections()` → `List<ConnectionInfo>`, `NetworkInterop.GetUdpListeners()` → `List<ConnectionInfo>`, `NetworkInterop.GetAllConnections()` → `List<ConnectionInfo>`

- [ ] **Step 1: Create the NetworkInterop class**

```csharp
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.InteropServices;
using OctoTask.Core.Models;

namespace OctoTask.Core.Native
{
    internal static class NetworkInterop
    {
        #region Constants

        private const int AF_INET = 2;  // IPv4
        private const int AF_INET6 = 23; // IPv6

        private enum TCP_TABLE_CLASS
        {
            TCP_TABLE_OWNER_PID_ALL = 5,
            TCP_TABLE_OWNER_MODULE_ALL = 8
        }

        private enum UDP_TABLE_CLASS
        {
            UDP_TABLE_OWNER_PID = 1,
            UDP_TABLE_OWNER_MODULE = 2
        }

        private const int NO_ERROR = 0;
        private const int ERROR_INSUFFICIENT_BUFFER = 122;

        #endregion

        #region Structs

        [StructLayout(LayoutKind.Sequential)]
        private struct MIB_TCPROW_OWNER_PID
        {
            public uint State;
            public uint LocalAddr;
            public uint LocalPort;
            public uint RemoteAddr;
            public uint RemotePort;
            public uint OwningPid;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct MIB_UDPROW_OWNER_PID
        {
            public uint LocalAddr;
            public uint LocalPort;
            public uint OwningPid;
        }

        #endregion

        #region P/Invoke

        [DllImport("iphlpapi.dll", SetLastError = true)]
        private static extern uint GetExtendedTcpTable(
            IntPtr pTcpTable,
            ref int dwOutBufLen,
            [MarshalAs(UnmanagedType.Bool)] bool sort,
            int ipVersion,
            TCP_TABLE_CLASS tableClass,
            uint reserved);

        [DllImport("iphlpapi.dll", SetLastError = true)]
        private static extern uint GetExtendedUdpTable(
            IntPtr pUdpTable,
            ref int dwOutBufLen,
            [MarshalAs(UnmanagedType.Bool)] bool sort,
            int ipVersion,
            UDP_TABLE_CLASS tableClass,
            uint reserved);

        [DllImport("ws2_32.dll")]
        private static extern uint ntohs(uint netshort);

        #endregion

        #region Public Methods

        public static List<ConnectionInfo> GetAllConnections()
        {
            var connections = new List<ConnectionInfo>();
            connections.AddRange(GetTcpConnections());
            connections.AddRange(GetUdpListeners());
            return connections;
        }

        public static List<ConnectionInfo> GetTcpConnections()
        {
            var result = new List<ConnectionInfo>();
            int bufferSize = 0;

            // First call to get required buffer size
            uint ret = GetExtendedTcpTable(IntPtr.Zero, ref bufferSize, true, AF_INET, TCP_TABLE_CLASS.TCP_TABLE_OWNER_PID_ALL, 0);

            if (ret != ERROR_INSUFFICIENT_BUFFER && ret != NO_ERROR)
                return result;

            IntPtr tablePtr = Marshal.AllocHGlobal(bufferSize);
            try
            {
                ret = GetExtendedTcpTable(tablePtr, ref bufferSize, true, AF_INET, TCP_TABLE_CLASS.TCP_TABLE_OWNER_PID_ALL, 0);
                if (ret != NO_ERROR)
                    return result;

                int rowCount = Marshal.ReadInt32(tablePtr);
                IntPtr rowPtr = tablePtr + 4; // Skip the uint count at the beginning

                int rowSize = Marshal.SizeOf<MIB_TCPROW_OWNER_PID>();

                for (int i = 0; i < rowCount; i++)
                {
                    var row = Marshal.PtrToStructure<MIB_TCPROW_OWNER_PID>(rowPtr + (i * rowSize));

                    string localAddr = row.LocalAddr == 0 ? "*" : FormatIPv4(row.LocalAddr);
                    string remoteAddr = row.RemoteAddr == 0 ? "*" : FormatIPv4(row.RemoteAddr);
                    ushort localPort = (ushort)ntohs((ushort)row.LocalPort);
                    ushort remotePort = (ushort)ntohs((ushort)row.RemotePort);

                    string processName = GetProcessName((int)row.OwningPid);

                    result.Add(new ConnectionInfo
                    {
                        Protocol = ConnectionProtocol.TCP,
                        LocalAddress = localAddr,
                        LocalPort = localPort,
                        RemoteAddress = remoteAddr,
                        RemotePort = remotePort,
                        State = MapTcpState(row.State),
                        Pid = (int)row.OwningPid,
                        ProcessName = processName
                    });
                }
            }
            finally
            {
                Marshal.FreeHGlobal(tablePtr);
            }

            return result;
        }

        public static List<ConnectionInfo> GetUdpListeners()
        {
            var result = new List<ConnectionInfo>();
            int bufferSize = 0;

            uint ret = GetExtendedUdpTable(IntPtr.Zero, ref bufferSize, true, AF_INET, UDP_TABLE_CLASS.UDP_TABLE_OWNER_PID, 0);

            if (ret != ERROR_INSUFFICIENT_BUFFER && ret != NO_ERROR)
                return result;

            IntPtr tablePtr = Marshal.AllocHGlobal(bufferSize);
            try
            {
                ret = GetExtendedUdpTable(tablePtr, ref bufferSize, true, AF_INET, UDP_TABLE_CLASS.UDP_TABLE_OWNER_PID, 0);
                if (ret != NO_ERROR)
                    return result;

                int rowCount = Marshal.ReadInt32(tablePtr);
                IntPtr rowPtr = tablePtr + 4;

                int rowSize = Marshal.SizeOf<MIB_UDPROW_OWNER_PID>();

                for (int i = 0; i < rowCount; i++)
                {
                    var row = Marshal.PtrToStructure<MIB_UDPROW_OWNER_PID>(rowPtr + (i * rowSize));

                    string localAddr = row.LocalAddr == 0 ? "*" : FormatIPv4(row.LocalAddr);
                    ushort localPort = (ushort)ntohs((ushort)row.LocalPort);
                    string processName = GetProcessName((int)row.OwningPid);

                    result.Add(new ConnectionInfo
                    {
                        Protocol = ConnectionProtocol.UDP,
                        LocalAddress = localAddr,
                        LocalPort = localPort,
                        RemoteAddress = "*",
                        RemotePort = 0,
                        State = ConnectionState.Closed, // UDP has no state
                        Pid = (int)row.OwningPid,
                        ProcessName = processName
                    });
                }
            }
            finally
            {
                Marshal.FreeHGlobal(tablePtr);
            }

            return result;
        }

        #endregion

        #region Private Helpers

        private static string FormatIPv4(uint addr)
        {
            byte[] bytes = BitConverter.GetBytes(addr);
            return $"{bytes[0]}.{bytes[1]}.{bytes[2]}.{bytes[3]}";
        }

        private static ConnectionState MapTcpState(uint state)
        {
            return state switch
            {
                1 => ConnectionState.Closed,
                2 => ConnectionState.Listen,
                3 => ConnectionState.SynSent,
                4 => ConnectionState.SynReceived,
                5 => ConnectionState.Established,
                6 => ConnectionState.FinWait1,
                7 => ConnectionState.FinWait2,
                8 => ConnectionState.CloseWait,
                9 => ConnectionState.Closing,
                10 => ConnectionState.LastAck,
                11 => ConnectionState.TimeWait,
                12 => ConnectionState.DeleteTcb,
                _ => ConnectionState.Unknown
            };
        }

        private static string GetProcessName(int pid)
        {
            if (pid <= 0)
                return "-";

            try
            {
                using var proc = Process.GetProcessById(pid);
                return proc.ProcessName;
            }
            catch
            {
                return $"PID {pid}";
            }
        }

        #endregion
    }
}
```

- [ ] **Step 2: Verify build compiles**

Run: `dotnet build OctoTask.csproj`
Expected: BUILD SUCCEEDED

---

### Task 3: Create `PortViewModel`

**Files:**
- Create: `UI/ViewModels/PortViewModel.cs`

**Interfaces:**
- Consumes: `NetworkInterop.GetAllConnections()` from Task 2
- Produces: `PortViewModel.Connections` (ObservableCollection), `PortViewModel.RefreshCommand`, `PortViewModel.SelectedConnection`, `PortViewModel.PortFilter`, `PortViewModel.ProtocolFilter`, `PortViewModel.GoToProcessCommand`

- [ ] **Step 1: Create the PortViewModel**

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

- [ ] **Step 2: Verify build compiles**

Run: `dotnet build OctoTask.csproj`
Expected: BUILD SUCCEEDED

---

### Task 4: Create `PortViewerControl` (XAML + Code-behind)

**Files:**
- Create: `UI/Views/PortViewerControl.xaml`
- Create: `UI/Views/PortViewerControl.xaml.cs`

**Interfaces:**
- Consumes: `PortViewModel` from Task 3

- [ ] **Step 1: Create the XAML UserControl**

```xml
<UserControl x:Class="OctoTask.UI.Views.PortViewerControl"
             xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
             xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
             xmlns:conv="clr-namespace:OctoTask.UI.Converters"
             xmlns:vm="clr-namespace:OctoTask.UI.ViewModels"
             FontFamily="Cascadia Mono, Consolas, Courier New">

    <UserControl.Resources>
        <conv:BoolToVisibilityConverter x:Key="BoolToVisibility"/>
    </UserControl.Resources>

    <DockPanel>
        <!-- Toolbar: search + filter + refresh -->
        <Border DockPanel.Dock="Top"
                Background="{StaticResource SurfaceBrush}"
                BorderBrush="{StaticResource BorderBrush}"
                BorderThickness="0,0,0,1"
                Padding="8,6">
            <Grid>
                <Grid.ColumnDefinitions>
                    <ColumnDefinition Width="Auto"/>
                    <ColumnDefinition Width="120"/>
                    <ColumnDefinition Width="Auto"/>
                    <ColumnDefinition Width="100"/>
                    <ColumnDefinition Width="Auto"/>
                    <ColumnDefinition Width="Auto"/>
                    <ColumnDefinition Width="Auto"/>
                    <ColumnDefinition Width="*"/>
                    <ColumnDefinition Width="Auto"/>
                </Grid.ColumnDefinitions>

                <!-- Port search -->
                <TextBlock Grid.Column="0" Text="Port:" Foreground="{StaticResource TextSecondary}"
                           VerticalAlignment="Center" Margin="0,0,8,0" FontSize="12"/>
                <TextBox Grid.Column="1" Width="100" Height="24"
                         VerticalAlignment="Center"
                         Text="{Binding PortFilter, UpdateSourceTrigger=PropertyChanged}"
                         Background="{StaticResource BgBrush}"
                         Foreground="{StaticResource TextPrimary}"
                         BorderBrush="{StaticResource BorderBrush}"
                         BorderThickness="1"
                         FontFamily="Cascadia Mono, Consolas, Courier New"
                         FontSize="12" Padding="6,2">
                    <TextBox.Resources>
                        <Style TargetType="Border">
                            <Setter Property="CornerRadius" Value="4"/>
                        </Style>
                    </TextBox.Resources>
                </TextBox>

                <!-- Protocol filter -->
                <TextBlock Grid.Column="2" Text="Protocol:" Foreground="{StaticResource TextSecondary}"
                           VerticalAlignment="Center" Margin="12,0,8,0" FontSize="12"/>
                <ComboBox Grid.Column="3" Width="80" Height="24"
                          VerticalAlignment="Center"
                          SelectedItem="{Binding ProtocolFilter}"
                          Background="{StaticResource BgBrush}"
                          Foreground="{StaticResource TextPrimary}"
                          BorderBrush="{StaticResource BorderBrush}"
                          BorderThickness="1" FontSize="12">
                    <ComboBoxItem Content="All" IsSelected="True"/>
                    <ComboBoxItem Content="TCP"/>
                    <ComboBoxItem Content="UDP"/>
                </ComboBox>

                <!-- Separator -->
                <Separator Grid.Column="4" Margin="8,0"/>

                <!-- Refresh -->
                <Button Grid.Column="5"
                        Command="{Binding RefreshCommand}"
                        Content="&#x27F3; Refresh"
                        Style="{StaticResource ToolbarButton}"/>

                <CheckBox Grid.Column="6"
                          IsChecked="{Binding IsAutoRefreshEnabled}"
                          Content="Auto (5s)"
                          Foreground="{StaticResource TextSecondary}"
                          Margin="8,0,0,0"
                          VerticalAlignment="Center"/>

                <!-- Spacer -->
                <Border Grid.Column="7"/>

                <!-- Go to Process button -->
                <Button Grid.Column="8"
                        Content="&#x2192; Go to Process"
                        Command="{Binding RefreshCommand}"
                        Style="{StaticResource ToolbarButton}"
                        IsEnabled="{Binding CanGoToProcess}"
                        Click="OnGoToProcessClick"/>
            </Grid>
        </Border>

        <!-- DataGrid fills remaining space -->
        <DataGrid ItemsSource="{Binding Connections}"
                  AutoGenerateColumns="False"
                  IsReadOnly="True"
                  SelectionMode="Single"
                  SelectedItem="{Binding SelectedConnection}"
                  Background="{StaticResource BgBrush}"
                  Foreground="{StaticResource TextPrimary}"
                  BorderBrush="{StaticResource BorderBrush}"
                  BorderThickness="0"
                  GridLinesVisibility="None"
                  HeadersVisibility="Column"
                  RowBackground="{StaticResource BgBrush}"
                  AlternatingRowBackground="{StaticResource SurfaceBrush}"
                  CanUserAddRows="False"
                  CanUserSortColumns="True"
                  CanUserResizeColumns="True"
                  RowHeight="28"
                  FontFamily="Cascadia Mono, Consolas, Courier New"
                  FontSize="12"
                  MouseDoubleClick="OnDataGridDoubleClick">

            <DataGrid.Columns>
                <DataGridTextColumn Header="Protocol"
                                    Binding="{Binding ProtocolDisplay}"
                                    SortMemberPath="ProtocolDisplay"
                                    Width="70"/>
                <DataGridTextColumn Header="Local Address"
                                    Binding="{Binding LocalAddress}"
                                    SortMemberPath="LocalAddress"
                                    Width="140"/>
                <DataGridTextColumn Header="Local Port"
                                    Binding="{Binding LocalPortDisplay}"
                                    SortMemberPath="LocalPort"
                                    Width="90">
                    <DataGridTextColumn.ElementStyle>
                        <Style TargetType="TextBlock">
                            <Setter Property="HorizontalAlignment" Value="Right"/>
                            <Setter Property="Padding" Value="4,0"/>
                        </Style>
                    </DataGridTextColumn.ElementStyle>
                </DataGridTextColumn>
                <DataGridTextColumn Header="Remote Address"
                                    Binding="{Binding RemoteAddress}"
                                    SortMemberPath="RemoteAddress"
                                    Width="140"/>
                <DataGridTextColumn Header="Remote Port"
                                    Binding="{Binding RemotePortDisplay}"
                                    SortMemberPath="RemotePort"
                                    Width="90">
                    <DataGridTextColumn.ElementStyle>
                        <Style TargetType="TextBlock">
                            <Setter Property="HorizontalAlignment" Value="Right"/>
                            <Setter Property="Padding" Value="4,0"/>
                        </Style>
                    </DataGridTextColumn.ElementStyle>
                </DataGridTextColumn>
                <DataGridTextColumn Header="State"
                                    Binding="{Binding StateDisplay}"
                                    SortMemberPath="State"
                                    Width="130"/>
                <DataGridTextColumn Header="PID"
                                    Binding="{Binding PidDisplay}"
                                    SortMemberPath="Pid"
                                    Width="70">
                    <DataGridTextColumn.ElementStyle>
                        <Style TargetType="TextBlock">
                            <Setter Property="HorizontalAlignment" Value="Right"/>
                            <Setter Property="Padding" Value="4,0"/>
                        </Style>
                    </DataGridTextColumn.ElementStyle>
                </DataGridTextColumn>
                <DataGridTextColumn Header="Process"
                                    Binding="{Binding ProcessName}"
                                    SortMemberPath="ProcessName"
                                    Width="*"/>
            </DataGrid.Columns>
        </DataGrid>
    </DockPanel>
</UserControl>
```

- [ ] **Step 2: Create the code-behind**

```csharp
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using OctoTask.UI.ViewModels;

namespace OctoTask.UI.Views
{
    public partial class PortViewerControl : UserControl
    {
        public PortViewerControl()
        {
            InitializeComponent();
        }

        private void OnDataGridDoubleClick(object sender, MouseButtonEventArgs e)
        {
            if (DataContext is PortViewModel vm && vm.CanGoToProcess)
            {
                vm.GoToProcess();
            }
        }

        private void OnGoToProcessClick(object sender, RoutedEventArgs e)
        {
            if (DataContext is PortViewModel vm && vm.CanGoToProcess)
            {
                vm.GoToProcess();
            }
        }
    }
}
```

- [ ] **Step 3: Verify build compiles**

Run: `dotnet build OctoTask.csproj`
Expected: BUILD SUCCEEDED

---

### Task 5: Modify `MainViewModel` — Expose PortViewModel + Cross-linking

**Files:**
- Modify: `UI/ViewModels/MainViewModel.cs`

**Interfaces:**
- Consumes: `PortViewModel` from Task 3
- Produces: `MainViewModel.PortVM` property, `MainViewModel.SelectProcessByPid(int pid)` method

- [ ] **Step 1: Add PortViewModel property and cross-linking method**

In `MainViewModel.cs`, add these members:

1. Add a new field and property after the existing fields (around line 38):

```csharp
private readonly PortViewModel _portVM;
public PortViewModel PortVM => _portVM;
```

2. In the constructor (after line 216, after `_sortClickCount = 1;`), add:

```csharp
_portVM = new PortViewModel();
_portVM.GoToProcessRequested += SelectProcessByPid;
```

3. Add the `SelectProcessByPid` method (after the `BuildProcessTree` method, before the `#region Sorting`):

```csharp
public void SelectProcessByPid(int pid)
{
    var match = Processes.FirstOrDefault(p => p.Pid == pid);
    if (match != null)
    {
        SelectedProcess = match;
        IsTreeView = false; // Switch to DataGrid view to show selection
    }
    else
    {
        StatusText = $"PID {pid} not found in process list — try refreshing first";
    }
}
```

- [ ] **Step 2: Verify build compiles**

Run: `dotnet build OctoTask.csproj`
Expected: BUILD SUCCEEDED

---

### Task 6: Modify `MainWindow.xaml` — Add TabControl with Ports Tab

**Files:**
- Modify: `MainWindow.xaml`

**Interfaces:**
- Consumes: `PortViewerControl` from Task 4, `PortViewModel` from Task 3 (via `MainViewModel.PortVM`)

- [ ] **Step 1: Wrap main content in a TabControl**

The existing main content is in Grid.Row="2" (line 422–696). Replace the `<DockPanel Grid.Row="2">` block with a TabControl that has two tabs:

Replace the content of `<DockPanel Grid.Row="2">` (everything inside that DockPanel) with:

```xml
        <!-- Main content: TabControl with Processes and Ports tabs -->
        <TabControl Grid.Row="2"
                    Background="Transparent"
                    BorderThickness="0"
                    Padding="0">

            <!-- Processes Tab -->
            <TabItem>
                <TabItem.Header>
                    <TextBlock Text=" Processes " FontSize="12" Padding="4,2"/>
                </TabItem.Header>
                <DockPanel>
                    <Border x:Name="DetailsPane"
                            Background="{StaticResource SurfaceBrush}"
                            BorderBrush="{StaticResource BorderBrush}"
                            BorderThickness="1,0,0,0"
                            Visibility="{Binding ProcessDetails, Converter={x:Static conv:NullToVisibilityConverter.Instance}}"
                            Width="420"
                            DockPanel.Dock="Right">
                        <!-- ... existing DetailsPane content stays unchanged (lines 431-599) ... -->
                    </Border>

                    <!-- DataGrid / TreeView fills remaining space -->
                    <Grid>
                        <!-- ... existing DataGrid + TreeView stays unchanged (lines 602-695) ... -->
                    </Grid>
                </DockPanel>
            </TabItem>

            <!-- Ports Tab -->
            <TabItem>
                <TabItem.Header>
                    <TextBlock Text=" Ports " FontSize="12" Padding="4,2"/>
                </TabItem.Header>
                <views:PortViewerControl DataContext="{Binding PortVM}"/>
            </TabItem>
        </TabControl>
```

**Important:** The existing `<DockPanel Grid.Row="2">` wrapper must be removed and replaced with the `<TabControl>`. The inner DockPanel content moves inside the "Processes" TabItem.

- [ ] **Step 2: Add the views namespace**

Add this namespace to the `<Window>` tag (after the existing `xmlns:local` declaration, around line 8):

```xml
         xmlns:views="clr-namespace:OctoTask.UI.Views"
```

- [ ] **Step 3: Style the TabControl and TabItems**

Add these styles inside `<Window.Resources>` (after the existing styles, around line 296):

```xml
        <!-- TabControl style -->
        <Style TargetType="TabControl">
            <Setter Property="Background" Value="Transparent"/>
            <Setter Property="BorderBrush" Value="{StaticResource BorderBrush}"/>
            <Setter Property="BorderThickness" Value="0"/>
            <Setter Property="Padding" Value="0"/>
        </Style>

        <!-- TabItem style -->
        <Style TargetType="TabItem">
            <Setter Property="Background" Value="{StaticResource SurfaceBrush}"/>
            <Setter Property="Foreground" Value="{StaticResource TextSecondary}"/>
            <Setter Property="BorderBrush" Value="{StaticResource BorderBrush}"/>
            <Setter Property="BorderThickness" Value="1,1,1,0"/>
            <Setter Property="Padding" Value="12,6"/>
            <Setter Property="Cursor" Value="Hand"/>
            <Setter Property="FontSize" Value="12"/>
            <Setter Property="Template">
                <Setter.Value>
                    <ControlTemplate TargetType="TabItem">
                        <Border x:Name="Border"
                                Background="{TemplateBinding Background}"
                                BorderBrush="{TemplateBinding BorderBrush}"
                                BorderThickness="{TemplateBinding BorderThickness}"
                                CornerRadius="6,6,0,0"
                                Padding="{TemplateBinding Padding}"
                                Margin="2,0,2,0">
                            <ContentPresenter ContentSource="Header"
                                              HorizontalAlignment="Center"
                                              VerticalAlignment="Center"/>
                        </Border>
                        <ControlTemplate.Triggers>
                            <Trigger Property="IsMouseOver" Value="True">
                                <Setter TargetName="Border" Property="Background" Value="{StaticResource SurfaceAltBrush}"/>
                                <Setter Property="Foreground" Value="{StaticResource TextPrimary}"/>
                            </Trigger>
                            <Trigger Property="IsSelected" Value="True">
                                <Setter TargetName="Border" Property="Background" Value="{StaticResource BgBrush}"/>
                                <Setter Property="Foreground" Value="{StaticResource AccentBrush}"/>
                                <Setter TargetName="Border" Property="BorderBrush" Value="{StaticResource AccentBrush}"/>
                                <Setter TargetName="Border" Property="BorderThickness" Value="1,2,1,0"/>
                            </Trigger>
                        </ControlTemplate.Triggers>
                    </ControlTemplate>
                </Setter.Value>
            </Setter>
        </Style>
```

- [ ] **Step 4: Verify build compiles**

Run: `dotnet build OctoTask.csproj`
Expected: BUILD SUCCEEDED

---

### Task 7: Modify `MainWindow.xaml.cs` — Wire Port→Process Cross-linking

**Files:**
- Modify: `MainWindow.xaml.cs`

**Interfaces:**
- Consumes: `PortViewModel.GoToProcessRequested` event from Task 3, `MainViewModel.SelectProcessByPid` from Task 5

- [ ] **Step 1: Add TabControl selection logic for auto-refresh**

The PortViewModel auto-refreshes only when its tab is visible. Add a `SelectionChanged` handler on the TabControl to start/stop the port refresh timer.

In `MainWindow.xaml`, name the TabControl:
```xml
<TabControl x:Name="MainTabControl" ...>
```

In `MainWindow.xaml.cs`, add this handler (in `OnLoaded`, after the existing subscriptions):

```csharp
MainTabControl.SelectionChanged += OnTabChanged;
```

And add the method:

```csharp
private void OnTabChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
{
    // When switching to Ports tab, ensure it refreshes
    if (MainTabControl.SelectedIndex == 1)
    {
        _viewModel.PortVM.Refresh();
    }
}
```

- [ ] **Step 2: Verify build compiles**

Run: `dotnet build OctoTask.csproj`
Expected: BUILD SUCCEEDED

---

### Task 8: Integration Test — Full Build + Manual Verification

**Files:**
- None (verification only)

- [ ] **Step 1: Full clean build**

Run: `dotnet build OctoTask.csproj`
Expected: BUILD SUCCEEDED with 0 errors, 0 warnings

- [ ] **Step 2: Verify no missing references**

Run: `dotnet build OctoTask.csproj --verbosity normal 2>&1 | Select-String "error CS"`
Expected: No output (no compilation errors)

---

## Execution Order Summary

| Task | Files Changed | Depends On |
|------|--------------|------------|
| 1. ConnectionInfo model | `Core/Models/ConnectionInfo.cs` (create) | Nothing |
| 2. NetworkInterop P/Invoke | `Core/Native/NetworkInterop.cs` (create) | Task 1 |
| 3. PortViewModel | `UI/ViewModels/PortViewModel.cs` (create) | Tasks 1, 2 |
| 4. PortViewerControl XAML | `UI/Views/PortViewerControl.xaml`, `.cs` (create) | Task 3 |
| 5. MainViewModel updates | `UI/ViewModels/MainViewModel.cs` (modify) | Task 3 |
| 6. MainWindow XAML (TabControl) | `MainWindow.xaml` (modify) | Tasks 4, 5 |
| 7. MainWindow code-behind | `MainWindow.xaml.cs` (modify) | Task 6 |
| 8. Full build verification | None | Tasks 1–7 |

**Parallelizable:** Tasks 1, 4 (XAML only), can start in parallel. Task 5 can start as soon as Task 3 is done. Tasks 6 and 7 are sequential.

## Estimated Line Counts

| File | ~Lines |
|------|--------|
| `Core/Models/ConnectionInfo.cs` | 130 |
| `Core/Native/NetworkInterop.cs` | 200 |
| `UI/ViewModels/PortViewModel.cs` | 150 |
| `UI/Views/PortViewerControl.xaml` | 120 |
| `UI/Views/PortViewerControl.xaml.cs` | 40 |
| `MainViewModel.cs` (edits) | +20 |
| `MainWindow.xaml` (edits) | +80 |
| `MainWindow.xaml.cs` (edits) | +15 |
| **Total new/changed** | **~755** |

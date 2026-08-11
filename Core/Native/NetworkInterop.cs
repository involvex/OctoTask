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

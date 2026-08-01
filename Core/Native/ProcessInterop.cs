using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using OctoTask.Core.Models;

namespace OctoTask.Core.Native
{
    internal static class ProcessInterop
    {
        #region Constants

        private const int PROCESS_QUERY_INFORMATION = 0x0400;
        private const int PROCESS_VM_READ = 0x0010;
        private const int PROCESS_TERMINATE = 0x0001;
        private const int ProcessBasicInformationClass = 0;

        #endregion

        #region Structs

        [StructLayout(LayoutKind.Sequential)]
        private struct ProcessBasicInformation
        {
            public IntPtr Reserved1;      // PebBaseAddress
            public IntPtr Reserved2;      // AffinityMask
            public IntPtr UniqueProcessId;
            public IntPtr Reserved3;      // InheritedBoundary
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct UnicodeString
        {
            public ushort Length;
            public ushort MaximumLength;
            public IntPtr Buffer;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct RtlUserProcessParameters
        {
            public uint MaximumNumberOfCommandLineParameters;
            // At offset 0x10 (x86) / 0x20 (x64) comes CommandLine (UNICODE_STRING)
            // At offset 0x30 (x86) / 0x50 (x64) comes ImagePathName (UNICODE_STRING)
            // We read these manually via offsets rather than struct layout
        }

        #endregion

        #region P/Invoke (DllImport for compatibility with unsafe code generation)

        [DllImport("ntdll.dll", SetLastError = true)]
        private static extern int NtQueryInformationProcess(
            IntPtr processHandle,
            int processInformationClass,
            out ProcessBasicInformation processInformation,
            int processInformationLength,
            IntPtr returnLength);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern IntPtr OpenProcess(
            uint dwDesiredAccess,
            [MarshalAs(UnmanagedType.Bool)] bool bInheritHandle,
            int dwProcessId);

        [DllImport("kernel32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool CloseHandle(IntPtr hObject);

        [DllImport("kernel32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool ReadProcessMemory(
            IntPtr hProcess,
            IntPtr lpBaseAddress,
            [Out] byte[] lpBuffer,
            int dwSize,
            out IntPtr lpNumberOfBytesRead);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool IsWow64Process(IntPtr hProcess, out bool isWow64);

        [DllImport("psapi.dll", SetLastError = true)]
        private static extern uint GetModuleFileNameEx(IntPtr hProcess, IntPtr hModule,
            [Out] StringBuilder lpBaseName, int nSize);

        #endregion

        #region PEB Offsets

        // PEB.ProcessParameters is at offset 0x20 on x64, 0x10 on x86
        private static readonly int PebProcessParametersOffset = IntPtr.Size == 8 ? 0x20 : 0x10;

        // RTL_USER_PROCESS_PARAMETERS.CommandLine is at offset 0x40 on x64, 0x28 on x86
        private static readonly int RtlCommandLineOffset = IntPtr.Size == 8 ? 0x40 : 0x28;

        // RTL_USER_PROCESS_PARAMETERS.ImagePathName is at offset 0x50 on x64, 0x30 on x86
        private static readonly int RtlImagePathOffset = IntPtr.Size == 8 ? 0x50 : 0x30;

        #endregion

        #region Public Methods

        public static List<ProcessInfo> GetAllProcesses()
        {
            var processes = new List<ProcessInfo>();
            var procArray = Process.GetProcesses();

            foreach (var proc in procArray)
            {
                try
                {
                    var info = ReadProcessFromPeb(proc);
                    if (info != null)
                        processes.Add(info);
                }
                catch
                {
                    // Skip any process that causes an error
                }
            }

            return processes;
        }

        public static bool KillProcess(int pid)
        {
            try
            {
                using var proc = Process.GetProcessById(pid);
                proc.Kill(true);
                proc.WaitForExit(5000);
                return true;
            }
            catch
            {
                return false;
            }
        }

        #endregion

        #region Private Helpers

        private static ProcessInfo? ReadProcessFromPeb(Process proc)
        {
            if (proc.HasExited)
                return null;

            IntPtr hProcess = IntPtr.Zero;
            try
            {
                hProcess = OpenProcess(
                    (uint)(PROCESS_QUERY_INFORMATION | PROCESS_VM_READ),
                    false,
                    proc.Id);

                if (hProcess == IntPtr.Zero)
                    return CreateFallbackInfo(proc);

                var pbi = new ProcessBasicInformation();
                int status = NtQueryInformationProcess(hProcess, ProcessBasicInformationClass, out pbi,
                    Marshal.SizeOf<ProcessBasicInformation>(), IntPtr.Zero);

                if (status != 0)
                    return CreateFallbackInfo(proc);

                IntPtr pebBase = pbi.Reserved1;
                if (pebBase == IntPtr.Zero)
                    return CreateFallbackInfo(proc);

                // Read PEB -> ProcessParameters pointer
                if (!ReadIntPtrFromMemory(hProcess, pebBase + PebProcessParametersOffset, out IntPtr pProcessParams))
                    return CreateFallbackInfo(proc);

                // Read CommandLine UNICODE_STRING from RTL_USER_PROCESS_PARAMETERS
                string commandLine = ReadUnicodeStringFromMemory(hProcess, pProcessParams + RtlCommandLineOffset);

                // Read ImagePathName UNICODE_STRING from RTL_USER_PROCESS_PARAMETERS
                string exePath = ReadUnicodeStringFromMemory(hProcess, pProcessParams + RtlImagePathOffset);
                exePath = ConvertNtToDosPath(exePath);

                // Fallback for any missing fields
                if (string.IsNullOrEmpty(exePath))
                    exePath = GetModuleFileNameFallback(hProcess, proc);

                if (string.IsNullOrEmpty(commandLine))
                    commandLine = string.Empty;

                return new ProcessInfo
                {
                    Pid = proc.Id,
                    ProcessName = proc.ProcessName,
                    ExecutablePath = exePath,
                    CommandLine = commandLine,
                    WorkingSetBytes = proc.WorkingSet64,
                };
            }
            catch
            {
                return CreateFallbackInfo(proc);
            }
            finally
            {
                if (hProcess != IntPtr.Zero)
                    CloseHandle(hProcess);
            }
        }

        private static bool ReadIntPtrFromMemory(IntPtr hProcess, IntPtr address, out IntPtr value)
        {
            value = IntPtr.Zero;
            byte[] buffer = new byte[IntPtr.Size];
            IntPtr bytesRead;

            if (!ReadProcessMemory(hProcess, address, buffer, IntPtr.Size, out bytesRead) ||
                bytesRead != (IntPtr)IntPtr.Size)
                return false;

            value = IntPtr.Size == 8
                ? (IntPtr)BitConverter.ToInt64(buffer, 0)
                : (IntPtr)BitConverter.ToInt32(buffer, 0);

            return value != IntPtr.Zero;
        }

        private static string ReadUnicodeStringFromMemory(IntPtr hProcess, IntPtr unicodeStringPtr)
        {
            if (unicodeStringPtr == IntPtr.Zero)
                return string.Empty;

            // UNICODE_STRING layout:
            // Offset 0: USHORT Length
            // Offset 2: USHORT MaximumLength
            // Offset 4 (x86) / 8 (x64): PVOID Buffer

            byte[] headerBuffer = new byte[IntPtr.Size + 4]; // Length + MaxLength + Buffer pointer
            IntPtr bytesRead;

            if (!ReadProcessMemory(hProcess, unicodeStringPtr, headerBuffer, headerBuffer.Length, out bytesRead))
                return string.Empty;

            ushort length = BitConverter.ToUInt16(headerBuffer, 0);
            if (length == 0)
                return string.Empty;

            IntPtr bufferPtr = IntPtr.Size == 8
                ? (IntPtr)BitConverter.ToInt64(headerBuffer, 8)
                : (IntPtr)BitConverter.ToInt32(headerBuffer, 4);

            if (bufferPtr == IntPtr.Zero)
                return string.Empty;

            byte[] stringBuffer = new byte[length];
            if (!ReadProcessMemory(hProcess, bufferPtr, stringBuffer, length, out _))
                return string.Empty;

            return Encoding.Unicode.GetString(stringBuffer).TrimEnd('\0');
        }

        private static string ConvertNtToDosPath(string ntPath)
        {
            if (string.IsNullOrEmpty(ntPath))
                return string.Empty;

            if (!ntPath.StartsWith("\\Device\\HarddiskVolume", StringComparison.OrdinalIgnoreCase))
                return ntPath;

            // Try to find matching drive letter using System.Management
            try
            {
                using var searcher = new System.Management.ManagementObjectSearcher(
                    "SELECT Name, DeviceID FROM Win32_LogicalDisk WHERE DriveType = 3");
                foreach (System.Management.ManagementObject disk in searcher.Get())
                {
                    string deviceId = disk["DeviceID"]?.ToString() ?? "";
                    string name = disk["Name"]?.ToString() ?? "";

                    // DeviceID looks like "\\.\\C:", we need to compare with volume number
                    string volumePart = ntPath.Split('\\')[2]; // e.g., "HarddiskVolume3"
                    if (deviceId.Contains(volumePart.Replace("Harddisk", ""), StringComparison.OrdinalIgnoreCase))
                    {
                        return ntPath.Replace(
                            "\\Device\\" + ntPath.Split('\\')[1] + "\\" + ntPath.Split('\\')[2],
                            name);
                    }
                }
            }
            catch
            {
                // Fallback: return as-is
            }

            return ntPath;
        }

        private static string GetModuleFileNameFallback(IntPtr hProcess, Process proc)
        {
            try
            {
                var sb = new StringBuilder(260);
                uint len = GetModuleFileNameEx(hProcess, IntPtr.Zero, sb, sb.Capacity);
                if (len > 0)
                    return sb.ToString();
            }
            catch
            {
                // Fall through
            }

            try
            {
                return proc.MainModule?.FileName ?? proc.ProcessName + ".exe";
            }
            catch
            {
                return proc.ProcessName + ".exe";
            }
        }

        private static ProcessInfo CreateFallbackInfo(Process proc)
        {
            try
            {
                string exePath = string.Empty;
                try
                {
                    exePath = proc.MainModule?.FileName ?? string.Empty;
                }
                catch
                {
                    exePath = string.IsNullOrEmpty(proc.ProcessName)
                        ? string.Empty
                        : proc.ProcessName + ".exe";
                }

                return new ProcessInfo
                {
                    Pid = proc.Id,
                    ProcessName = proc.ProcessName ?? "Unknown",
                    ExecutablePath = exePath,
                    CommandLine = string.Empty,
                    WorkingSetBytes = proc.WorkingSet64,
                };
            }
            catch
            {
                return new ProcessInfo
                {
                    Pid = proc.Id,
                    ProcessName = proc.ProcessName ?? "Unknown",
                    ExecutablePath = string.Empty,
                    CommandLine = string.Empty,
                    WorkingSetBytes = 0,
                };
            }
        }

        #endregion
    }
}

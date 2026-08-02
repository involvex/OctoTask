using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Management;
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
        private const uint TOKEN_ADJUST_PRIVILEGES = 0x0020;
        private const uint TOKEN_QUERY = 0x0008;
        private const uint SE_PRIVILEGE_ENABLED = 0x00000002;

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
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct TokenPrivileges
        {
            public long PrivilegeCount;
            public long Luid;
            public long Attributes;
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

        [DllImport("advapi32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool OpenProcessToken(IntPtr ProcessHandle, uint DesiredAccess,
            out IntPtr ProcessToken);

        [DllImport("advapi32.dll", SetLastError = true, CharSet = CharSet.Auto)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool LookupPrivilegeValue(string lpSystemName, string lpName,
            out long lpLuid);

        [DllImport("advapi32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool AdjustTokenPrivileges(IntPtr TokenHandle,
            [MarshalAs(UnmanagedType.Bool)] bool DisableAllPrivileges,
            ref TokenPrivileges NewState, int BufferLength,
            IntPtr PreviousState, IntPtr ReturnLength);

        [DllImport("kernel32.dll")]
        private static extern IntPtr GetCurrentProcess();

        #endregion

        #region PEB Offsets

        // PEB.ProcessParameters is at offset 0x20 on x64, 0x10 on x86
        private static readonly int PebProcessParametersOffset = IntPtr.Size == 8 ? 0x20 : 0x10;

        // RTL_USER_PROCESS_PARAMETERS.CommandLine is at offset 0x40 on x64, 0x28 on x86
        private static readonly int RtlCommandLineOffset = IntPtr.Size == 8 ? 0x40 : 0x28;

        // RTL_USER_PROCESS_PARAMETERS.Environment (pointer to env block) is at offset 0x80 on x64, 0x48 on x86
        private static readonly int RtlEnvironmentOffset = IntPtr.Size == 8 ? 0x80 : 0x48;

        // RTL_USER_PROCESS_PARAMETERS.ImagePathName is at offset 0x50 on x64, 0x30 on x86
        private static readonly int RtlImagePathOffset = IntPtr.Size == 8 ? 0x50 : 0x30;

        #endregion

        #region Private Methods

        private static void EnableDebugPrivilege()
        {
            try
            {
                if (!OpenProcessToken(GetCurrentProcess(), TOKEN_QUERY | TOKEN_ADJUST_PRIVILEGES, out IntPtr token))
                    return;

                if (!LookupPrivilegeValue("", "SeDebugPrivilege", out long luid))
                {
                    CloseHandle(token);
                    return;
                }

                var tp = new TokenPrivileges
                {
                    PrivilegeCount = 1,
                    Luid = luid,
                    Attributes = SE_PRIVILEGE_ENABLED
                };

                AdjustTokenPrivileges(token, false, ref tp, 0, IntPtr.Zero, IntPtr.Zero);
                CloseHandle(token);
            }
            catch
            {
                // Silently fail — we may not have the privilege
            }
        }

        #endregion

        #region Public Methods

        public static List<ProcessInfo> GetAllProcesses()
        {
            EnableDebugPrivilege();
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

        public static ProcessDetails? LoadProcessDetails(int pid)
        {
            try
            {
                EnableDebugPrivilege();
                using var proc = Process.GetProcessById(pid);
                if (proc.HasExited)
                    return null;

                var details = new ProcessDetails();

                // --- Basic process info ---
                try { details.StartTime = proc.StartTime.ToString("yyyy-MM-dd HH:mm:ss"); } catch { details.StartTime = "N/A"; }
                try { details.RunningTime = (DateTime.Now - proc.StartTime).ToString(@"dd\.hh\:mm\:ss"); } catch { details.RunningTime = "N/A"; }
                try { details.Threads = proc.Threads.Count.ToString(); } catch { details.Threads = "N/A"; }
                try { details.Handles = proc.HandleCount.ToString(); } catch { details.Handles = "N/A"; }
                try { details.Priority = proc.PriorityClass.ToString(); } catch { details.Priority = "N/A"; }
                try { details.IsResponding = proc.Responding; } catch { details.IsResponding = false; }
                try { details.Session = proc.SessionId.ToString(); } catch { details.Session = "N/A"; }

                // --- Parent process ---
                try
                {
                    using var searcher = new ManagementObjectSearcher(
                        $"SELECT ParentProcessId FROM Win32_Process WHERE ProcessId = {pid}");
                    foreach (ManagementObject mo in searcher.Get())
                    {
                        var parentPid = Convert.ToInt32(mo["ParentProcessId"] ?? 0);
                        details.ParentId = parentPid;
                        try
                        {
                            using var parent = Process.GetProcessById(parentPid);
                            details.ParentProcess = parent.ProcessName;
                        }
                        catch
                        {
                            details.ParentProcess = parentPid > 0 ? $"PID {parentPid}" : "N/A";
                        }
                        break;
                    }
                }
                catch { details.ParentProcess = "N/A"; }

                // --- User context (owner) ---
                try
                {
                    using var searcher = new ManagementObjectSearcher(
                        $"SELECT * FROM Win32_Process WHERE ProcessId = {pid}");
                    foreach (ManagementObject mo in searcher.Get())
                    {
                        var ownerUser = mo["Owner"]?.ToString() ?? "";
                        var ownerDomain = mo["Domain"]?.ToString() ?? "";
                        details.ProcessOwner = string.IsNullOrEmpty(ownerUser) ? "N/A" : ownerUser;
                        details.UserName = string.IsNullOrEmpty(ownerUser) ? "N/A" : ownerUser;
                        details.Domain = string.IsNullOrEmpty(ownerDomain) ? "N/A" : ownerDomain;
                        break;
                    }
                }
                catch { details.ProcessOwner = "N/A"; details.UserName = "N/A"; details.Domain = "N/A"; }

                // --- File version info ---
                try
                {
                    string filePath = proc.MainModule?.FileName ?? "";
                    if (!string.IsNullOrEmpty(filePath))
                    {
                        var fv = FileVersionInfo.GetVersionInfo(filePath);
                        details.Description = fv.FileDescription ?? "N/A";
                        details.Company = fv.CompanyName ?? "N/A";
                        details.Version = fv.FileVersion ?? "N/A";
                        details.FileVersion = fv.FileVersion ?? "N/A";
                        details.ProductVersion = fv.ProductVersion ?? "N/A";
                        details.WorkingDirectory = System.IO.Path.GetDirectoryName(filePath) ?? "N/A";
                    }
                }
                catch { /* leave defaults */ }

                // --- Loaded modules ---
                try
                {
                    var moduleList = new List<ModuleInfo>();
                    foreach (ProcessModule module in proc.Modules)
                    {
                        try
                        {
                            var fvi = FileVersionInfo.GetVersionInfo(module.FileName);
                            moduleList.Add(new ModuleInfo
                            {
                                Name = module.ModuleName,
                                FileName = module.FileName,
                                Version = fvi.FileVersion ?? "",
                                Size = module.ModuleMemorySize
                            });
                        }
                        catch
                        {
                            moduleList.Add(new ModuleInfo
                            {
                                Name = module.ModuleName,
                                FileName = module.FileName
                            });
                        }
                    }
                    details.Modules = moduleList;
                }
                catch { /* leave null */ }

                // --- Environment variables ---
                try
                {
                    var envList = new List<string>();

                    // Try to read environment variables from the target process's PEB
                    IntPtr hEnvProcess = OpenProcess(
                        (uint)(PROCESS_QUERY_INFORMATION | PROCESS_VM_READ),
                        false, pid);

                    if (hEnvProcess != IntPtr.Zero)
                    {
                        try
                        {
                            // Get ProcessBasicInformation
                            var pbi = new ProcessBasicInformation();
                            int status = NtQueryInformationProcess(hEnvProcess, ProcessBasicInformationClass,
                                out pbi, Marshal.SizeOf<ProcessBasicInformation>(), IntPtr.Zero);

                            if (status == 0 && pbi.Reserved1 != IntPtr.Zero)
                            {
                                    // Read PEB -> ProcessParameters pointer
                                 if (ReadIntPtrFromMemory(hEnvProcess, pbi.Reserved1 + PebProcessParametersOffset, out IntPtr pParams) && pParams != IntPtr.Zero)
                                 {
                                     // RTL_USER_PROCESS_PARAMETERS.Environment pointer
                                     if (ReadIntPtrFromMemory(hEnvProcess, pParams + RtlEnvironmentOffset, out IntPtr envBlockPtr) && envBlockPtr != IntPtr.Zero)
                                     {
                                         // Read the environment block (it's a sequence of null-terminated strings, double-null at end)
                                         var envData = ReadEnvironmentBlockFromMemory(hEnvProcess, envBlockPtr);
                                        foreach (var kv in envData)
                                            envList.Add($"{kv.Key}={kv.Value}");
                                    }
                                }
                            }
                        }
                        catch
                        {
                            // Fall through to current process env vars
                        }
                        finally
                        {
                            CloseHandle(hEnvProcess);
                        }
                    }

                    // Fallback: show current process environment variables if target could not be read
                    if (envList.Count == 0)
                    {
                        envList.Add("NOTE: Showing current process environment (access to target env denied)");
                        foreach (System.Collections.DictionaryEntry entry in Environment.GetEnvironmentVariables())
                        {
                            envList.Add($"{entry.Key}={entry.Value}");
                        }
                    }

                    details.EnvironmentVariables = envList;
                }
                catch { /* leave null */ }

                return details;
            }
            catch
            {
                return null;
            }
        }


        public static bool KillProcess(int pid)
        {
            EnableDebugPrivilege();
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

        private static Dictionary<string, string> ReadEnvironmentBlockFromMemory(IntPtr hProcess, IntPtr envBlockPtr)
        {
            var result = new Dictionary<string, string>();
            try
            {
                // Read the environment block: it's a sequence of null-terminated strings
                // terminated by an empty (double null) string.
                byte[] buffer = new byte[65536];
                IntPtr bytesRead;

                if (!ReadProcessMemory(hProcess, envBlockPtr, buffer, buffer.Length, out bytesRead))
                    return result;

                int totalRead = (int)bytesRead;
                int pos = 0;

                while (pos < totalRead)
                {
                    // Each entry is a null-terminated Unicode string
                    int start = pos;
                    while (pos < totalRead - 1 && buffer[pos] != 0)
                        pos += 2;

                    // Check for double-null terminator (end of environment block)
                    if (pos >= totalRead - 1 || (pos + 1 < totalRead && buffer[pos + 2] == 0))
                        break;

                    int len = pos - start;
                    if (len > 0)
                    {
                        string entry = Encoding.Unicode.GetString(buffer, start, len);
                        int sep = entry.IndexOf('=');
                        if (sep > 0)
                        {
                            string key = entry.Substring(0, sep);
                            string value = entry.Substring(sep + 1);
                            result[key] = value;
                        }
                    }

                    pos += 2; // skip the null terminator
                }
            }
            catch
            {
                // Return whatever we have
            }

            return result;
        }

        #endregion
    }
}

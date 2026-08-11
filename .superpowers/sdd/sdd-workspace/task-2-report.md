# Task 2 Report: NetworkInterop P/Invoke Wrapper

**Status:** COMPLETE

## What Was Done

Created `Core/Native/NetworkInterop.cs` exactly as specified in the task brief. The file provides a P/Invoke wrapper around the Windows IP Helper API (`iphlpapi.dll`) to enumerate TCP connections and UDP listeners without shelling out to `netstat`.

## Files Created

- `Core/Native/NetworkInterop.cs`

## Public API

| Method | Returns | Description |
|--------|---------|-------------|
| `GetTcpConnections()` | `List<ConnectionInfo>` | Enumerates all TCP connections with owner PID info |
| `GetUdpListeners()` | `List<ConnectionInfo>` | Enumerates all UDP listeners with owner PID info |
| `GetAllConnections()` | `List<ConnectionInfo>` | Combines TCP and UDP results |

## Implementation Details

- Uses `GetExtendedTcpTable` / `GetExtendedUdpTable` from `iphlpapi.dll`
- Uses `ntohs` from `ws2_32.dll` for port byte-order conversion
- Maps MIB TCP states (1-12) to `ConnectionState` enum values
- Resolves process names via `Process.GetProcessById` with fallback to PID number
- IPv4 only (`AF_INET`) as specified
- Proper memory management with `AllocHGlobal`/`FreeHGlobal` in try/finally

## Build Result

```
dotnet build OctoTask.csproj
  OctoTask -> E:\repos\OctoTask\bin\Debug\net10.0-windows\OctoTask.dll
  Build succeeded. 0 Warning(s) 0 Error(s)
```

## Dependencies

- Consumes: `ConnectionInfo`, `ConnectionProtocol`, `ConnectionState` from `Core/Models/ConnectionInfo.cs` (Task 1)

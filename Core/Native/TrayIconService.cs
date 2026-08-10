using System;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows;
using System.Windows.Interop;

namespace OctoTask.Core.Native;

internal class TrayIconService : IDisposable
{
    private const uint NIM_ADD = 0x00000000;
    private const uint NIM_MODIFY = 0x00000001;
    private const uint NIM_DELETE = 0x00000002;

    private const uint NIF_MESSAGE = 0x00000001;
    private const uint NIF_ICON = 0x00000002;
    private const uint NIF_TIP = 0x00000004;

    private const uint WM_LBUTTONDBLCLK = 0x0203;
    private const uint WM_RBUTTONUP = 0x0205;

    private const int WM_TRAYICON = 0x8000 + 1;

    private NOTIFYICONDATA _data;
    private IntPtr _hwnd;
    private HwndSource? _hwndSource;
    private bool _added;

    public event EventHandler? DoubleClick;
    public event EventHandler? RightClick;

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct NOTIFYICONDATA
    {
        public int cbSize;
        public IntPtr hWnd;
        public int uID;
        public uint uFlags;
        public uint uCallbackMessage;
        public IntPtr hIcon;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)]
        public string szTip;
        public uint dwState;
        public uint dwStateMask;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 256)]
        public string szInfo;
        public uint uTimeout;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 64)]
        public string szInfoTitle;
        public uint dwInfoFlags;
    }

    [DllImport("shell32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern bool Shell_NotifyIcon(uint dwMessage, ref NOTIFYICONDATA lpData);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern IntPtr SendMessage(IntPtr hWnd, uint Msg, IntPtr wParam, IntPtr lParam);

    public void AddIcon(IntPtr windowHandle, IntPtr hIcon, string tooltip)
    {
        _hwnd = windowHandle;

        _data = new NOTIFYICONDATA
        {
            cbSize = Marshal.SizeOf<NOTIFYICONDATA>(),
            hWnd = windowHandle,
            uID = 1,
            uFlags = NIF_MESSAGE | NIF_ICON | NIF_TIP,
            uCallbackMessage = WM_TRAYICON,
            hIcon = hIcon,
            szTip = tooltip.Length > 127 ? tooltip[..127] : tooltip
        };

        _added = Shell_NotifyIcon(NIM_ADD, ref _data);

        var source = HwndSource.FromHwnd(windowHandle);
        if (source != null)
        {
            _hwndSource = source;
            source.AddHook(WndProc);
        }
    }

    public void UpdateIcon(IntPtr hIcon, string tooltip)
    {
        if (!_added)
            return;

        _data.hIcon = hIcon;
        _data.uFlags = NIF_MESSAGE | NIF_ICON | NIF_TIP;
        _data.szTip = tooltip.Length > 127 ? tooltip[..127] : tooltip;

        Shell_NotifyIcon(NIM_MODIFY, ref _data);
    }

    public void RemoveIcon()
    {
        if (!_added)
            return;

        Shell_NotifyIcon(NIM_DELETE, ref _data);
        _added = false;

        if (_hwndSource != null)
        {
            _hwndSource.RemoveHook(WndProc);
            _hwndSource = null;
        }
    }

    private IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        if (msg == WM_TRAYICON)
        {
            uint eventType = (uint)lParam.ToInt32();
            switch (eventType)
            {
                case WM_LBUTTONDBLCLK:
                    DoubleClick?.Invoke(this, EventArgs.Empty);
                    handled = true;
                    break;
                case WM_RBUTTONUP:
                    RightClick?.Invoke(this, EventArgs.Empty);
                    handled = true;
                    break;
            }
        }

        return IntPtr.Zero;
    }

    public void Dispose()
    {
        RemoveIcon();
    }
}

using System;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Input;
using System.Windows.Interop;

namespace OctoTask.Core.Native
{
    internal static partial class GlobalHotKey
    {
        public const int WM_HOTKEY = 0x0312;

        public const uint MOD_ALT = 0x0001;
        public const uint MOD_CONTROL = 0x0002;
        public const uint MOD_SHIFT = 0x0004;
        public const uint MOD_WIN = 0x0008;

        public const int ID_REFRESH = 1;
        public const int ID_KILL = 2;
        public const int ID_SUSPEND = 3;

        [LibraryImport("user32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static partial bool RegisterHotKey(
            IntPtr hWnd, int id, uint fsModifiers, uint vk);

        [LibraryImport("user32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static partial bool UnregisterHotKey(IntPtr hWnd, int id);

        public static bool Register(Window window, ICommand refreshCommand, ICommand killCommand, ICommand? suspendCommand = null)
        {
            var hwnd = new WindowInteropHelper(window).Handle;
            if (hwnd == IntPtr.Zero)
                return false;

            var source = HwndSource.FromHwnd(hwnd);
            if (source == null)
                return false;

            source.AddHook((hwnd2, msg, wParam, lParam, ref handled) =>
            {
                if (msg == WM_HOTKEY)
                {
                    int id = wParam.ToInt32();
                    switch (id)
                    {
                        case ID_REFRESH:
                            if (refreshCommand.CanExecute(null))
                                refreshCommand.Execute(null);
                            handled = true;
                            break;
                        case ID_KILL:
                            if (killCommand.CanExecute(null))
                                killCommand.Execute(null);
                            handled = true;
                            break;
                        case ID_SUSPEND:
                            if (suspendCommand is not null && suspendCommand.CanExecute(null))
                                suspendCommand.Execute(null);
                            handled = true;
                            break;
                    }
                }
                return IntPtr.Zero;
            });

            return true;
        }

        public static void UnregisterAll(Window window)
        {
            var hwnd = new WindowInteropHelper(window).Handle;
            UnregisterHotKey(hwnd, ID_REFRESH);
            UnregisterHotKey(hwnd, ID_KILL);
            UnregisterHotKey(hwnd, ID_SUSPEND);
        }
    }
}

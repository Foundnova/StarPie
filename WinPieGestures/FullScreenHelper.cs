using System;
using System.Runtime.InteropServices;
using System.Text;

namespace WinPieGestures
{
    public static class FullScreenHelper
    {
        [DllImport("user32.dll")]
        private static extern IntPtr GetForegroundWindow();

        [DllImport("user32.dll")]
        private static extern IntPtr GetShellWindow();

        [DllImport("user32.dll")]
        private static extern IntPtr GetDesktopWindow();

        [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        private static extern int GetClassName(IntPtr hWnd, StringBuilder lpClassName, int nMaxCount);

        [DllImport("user32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool GetWindowRect(IntPtr hWnd, out RECT lpRect);

        [DllImport("user32.dll")]
        private static extern IntPtr MonitorFromWindow(IntPtr hwnd, uint dwFlags);

        [DllImport("user32.dll", CharSet = CharSet.Auto)]
        private static extern bool GetMonitorInfo(IntPtr hMonitor, ref MONITORINFO lpmi);

        [StructLayout(LayoutKind.Sequential)]
        private struct RECT
        {
            public int Left;
            public int Top;
            public int Right;
            public int Bottom;
        }

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Auto)]
        private struct MONITORINFO
        {
            public int cbSize;
            public RECT rcMonitor;
            public RECT rcWork;
            public uint dwFlags;
        }

        private const uint MONITOR_DEFAULTTONEAREST = 2;

        private static bool IsDesktopShellWindow(IntPtr hWnd)
        {
            if (hWnd == GetShellWindow() || hWnd == GetDesktopWindow())
            {
                return true;
            }

            var className = new StringBuilder(256);
            if (GetClassName(hWnd, className, className.Capacity) <= 0)
            {
                return false;
            }

            return string.Equals(className.ToString(), "Progman", StringComparison.Ordinal) ||
                   string.Equals(className.ToString(), "WorkerW", StringComparison.Ordinal);
        }

        /// <summary>
        /// Determines if the current active foreground window is in full-screen mode.
        /// </summary>
        public static bool IsActiveWindowFullScreen()
        {
            IntPtr hWnd = GetForegroundWindow();
            if (hWnd == IntPtr.Zero) return false;

            // Exclude the desktop and its WorkerW/Progman host windows. Win+D can make
            // one of these hosts the foreground window, and their bounds cover the monitor.
            if (IsDesktopShellWindow(hWnd)) return false;

            RECT windowRect;
            if (!GetWindowRect(hWnd, out windowRect)) return false;

            IntPtr hMonitor = MonitorFromWindow(hWnd, MONITOR_DEFAULTTONEAREST);
            if (hMonitor == IntPtr.Zero) return false;

            MONITORINFO monitorInfo = new MONITORINFO();
            monitorInfo.cbSize = Marshal.SizeOf(monitorInfo);

            if (!GetMonitorInfo(hMonitor, ref monitorInfo)) return false;

            // Check if active window rect covers the entire monitor rect
            return windowRect.Left <= monitorInfo.rcMonitor.Left &&
                   windowRect.Top <= monitorInfo.rcMonitor.Top &&
                   windowRect.Right >= monitorInfo.rcMonitor.Right &&
                   windowRect.Bottom >= monitorInfo.rcMonitor.Bottom;
        }
    }
}

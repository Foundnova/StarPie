using System;
using System.Runtime.InteropServices;
using System.Text;

namespace WinPieGestures;

public static class FullScreenHelper
{
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

	private const uint MONITOR_DEFAULTTONEAREST = 2u;

	[DllImport("user32.dll")]
	private static extern nint GetForegroundWindow();

	[DllImport("user32.dll")]
	private static extern nint GetShellWindow();

	[DllImport("user32.dll")]
	private static extern nint GetDesktopWindow();

	[DllImport("user32.dll", SetLastError = true)]
	[return: MarshalAs(UnmanagedType.Bool)]
	private static extern bool GetWindowRect(nint hWnd, out RECT lpRect);

	[DllImport("user32.dll")]
	private static extern nint MonitorFromWindow(nint hwnd, uint dwFlags);

	[DllImport("user32.dll", CharSet = CharSet.Auto)]
	private static extern bool GetMonitorInfo(nint hMonitor, ref MONITORINFO lpmi);

	[DllImport("user32.dll", SetLastError = true, CharSet = CharSet.Auto)]
	private static extern int GetClassName(nint hWnd, StringBuilder lpClassName, int nMaxCount);

	public static bool IsActiveWindowFullScreen()
	{
		nint foregroundWindow = GetForegroundWindow();
		if (foregroundWindow == IntPtr.Zero)
		{
			return false;
		}
		if (foregroundWindow == GetShellWindow() || foregroundWindow == GetDesktopWindow())
		{
			return false;
		}

		StringBuilder sbClass = new StringBuilder(256);
		GetClassName(foregroundWindow, sbClass, 256);
		string className = sbClass.ToString();

		if (string.Equals(className, "Progman", StringComparison.OrdinalIgnoreCase) ||
		    string.Equals(className, "WorkerW", StringComparison.OrdinalIgnoreCase) ||
		    string.Equals(className, "SHELLDLL_DefView", StringComparison.OrdinalIgnoreCase) ||
		    string.Equals(className, "SysListView32", StringComparison.OrdinalIgnoreCase) ||
		    string.Equals(className, "Shell_TrayWnd", StringComparison.OrdinalIgnoreCase) ||
		    string.Equals(className, "Shell_SecondaryTrayWnd", StringComparison.OrdinalIgnoreCase) ||
		    string.Equals(className, "Windows.UI.Core.CoreWindow", StringComparison.OrdinalIgnoreCase))
		{
			return false;
		}

		string activeProc = ActiveWindowHelper.GetActiveWindowProcessName();
		if (string.Equals(activeProc, "explorer.exe", StringComparison.OrdinalIgnoreCase))
		{
			return false;
		}

		if (!GetWindowRect(foregroundWindow, out var lpRect))
		{
			return false;
		}
		nint hMonitor = MonitorFromWindow(foregroundWindow, 2u);
		if (hMonitor == IntPtr.Zero)
		{
			return false;
		}
		MONITORINFO lpmi = default(MONITORINFO);
		lpmi.cbSize = Marshal.SizeOf(lpmi);
		if (!GetMonitorInfo(hMonitor, ref lpmi))
		{
			return false;
		}

		if (lpRect.Left <= lpmi.rcMonitor.Left && lpRect.Top <= lpmi.rcMonitor.Top && lpRect.Right >= lpmi.rcMonitor.Right && lpRect.Bottom >= lpmi.rcMonitor.Bottom)
		{
			return true;
		}
		return false;
	}
}

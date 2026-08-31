using System;
using System.Diagnostics;
using System.Runtime.InteropServices;

namespace WinPieGestures;

public static class ActiveWindowHelper
{
	[DllImport("user32.dll")]
	private static extern nint GetForegroundWindow();

	[DllImport("user32.dll", SetLastError = true)]
	private static extern uint GetWindowThreadProcessId(nint hWnd, out uint lpdwProcessId);

	public static string GetActiveWindowProcessName()
	{
		try
		{
			nint foregroundWindow = GetForegroundWindow();
			if (foregroundWindow == IntPtr.Zero)
			{
				return "unknown.exe";
			}
			GetWindowThreadProcessId(foregroundWindow, out var lpdwProcessId);
			if (lpdwProcessId == 0)
			{
				return "unknown.exe";
			}
			using Process process = Process.GetProcessById((int)lpdwProcessId);
			string processName = process.ProcessName;
			if (string.IsNullOrEmpty(processName))
			{
				return "unknown.exe";
			}
			return processName.ToLower() + ".exe";
		}
		catch (Exception)
		{
			return "unknown.exe";
		}
	}
}

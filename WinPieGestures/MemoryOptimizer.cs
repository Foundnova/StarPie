using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;

namespace WinPieGestures;

public static class MemoryOptimizer
{
	private static int _isTrimming = 0;

	private static DateTime _lastTrimTime = DateTime.MinValue;

	[DllImport("psapi.dll", SetLastError = true)]
	private static extern int EmptyWorkingSet(nint hwProc);

	[DllImport("kernel32.dll", SetLastError = true)]
	private static extern bool SetProcessWorkingSetSize(nint proc, nint min, nint max);

	public static void TrimMemory(bool force = false)
	{
		if ((!force && (DateTime.UtcNow - _lastTrimTime).TotalSeconds < 2.0) || Interlocked.Exchange(ref _isTrimming, 1) == 1)
		{
			return;
		}
		Task.Run(delegate
		{
			try
			{
				_lastTrimTime = DateTime.UtcNow;
				GC.Collect(2, GCCollectionMode.Forced, blocking: true, compacting: true);
				GC.WaitForPendingFinalizers();
				GC.Collect(2, GCCollectionMode.Forced, blocking: true, compacting: true);
				if (Environment.OSVersion.Platform == PlatformID.Win32NT)
				{
					nint handle = Process.GetCurrentProcess().Handle;
					EmptyWorkingSet(handle);
					SetProcessWorkingSetSize(handle, new IntPtr(-1), new IntPtr(-1));
				}
			}
			catch (Exception)
			{
			}
			finally
			{
				Interlocked.Exchange(ref _isTrimming, 0);
			}
		});
	}
}

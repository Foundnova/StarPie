using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace WinPieGestures;

/// <summary>
/// 任务栏第 N 窗口：按"任务栏按钮"语义枚举当前桌面窗口、激活到前台、提取窗口图标。
/// 托盘驻留(隐藏/离屏)程序不参与计数，避免被误切换。
/// </summary>
public static class WindowTaskbarHelper
{
	// ---- 枚举与过滤 ----
	[DllImport("user32.dll")]
	private static extern bool EnumWindows(EnumWindowsProc lpEnumFunc, nint lParam);

	private delegate bool EnumWindowsProc(nint hWnd, nint lParam);

	// ---- 任务栏工具栏（Win+N 槽位顺序）----
	[DllImport("user32.dll", CharSet = CharSet.Unicode)]
	private static extern nint FindWindow(string lpClassName, string? lpWindowName);

	[DllImport("user32.dll", CharSet = CharSet.Unicode)]
	private static extern nint FindWindowEx(nint hwndParent, nint hwndChildAfter, string lpszClass, string? lpszWindow);

	private const uint TB_BUTTONCOUNT = 0x0418;
	private const uint TB_GETBUTTON = 0x0417;
	private const uint TB_GETIMAGELIST = 0x0419;
	private const uint TB_GETBUTTONINFO = 0x0441;
	private const uint TBIF_IMAGE = 0x00000002;
	private const uint ILD_TRANSPARENT = 0x00000001;

	[StructLayout(LayoutKind.Sequential)]
	private struct TBBUTTONINFO
	{
		public uint cbSize;
		public uint dwMask;
		public int idCommand;
		public int iImage;
		public byte fsState;
		public byte fsStyle;
		public ushort cx;
		public nint lParam;
		public nint pszText;
		public int cchText;
	}

	[DllImport("comctl32.dll")]
	private static extern nint ImageList_GetIcon(nint himl, int i, uint flags);

	[StructLayout(LayoutKind.Sequential)]
	private struct TBBUTTON
	{
		public int iBitmap;
		public int idCommand;
		public byte fsState;
		public byte fsStyle;
		public byte bReserved0;
		public byte bReserved1;
		public nint dwData;
		public nint iString;
	}

	// ---- 模拟 Win+N（N=1..9, 0=10）----
	[DllImport("user32.dll")]
	private static extern void keybd_event(byte bVk, byte bScan, uint dwFlags, nint dwExtraInfo);

	private const byte VK_LWIN = 0x5B;
	private const uint KEYEVENTF_KEYUP = 0x0002;

	[DllImport("user32.dll")]
	private static extern bool IsWindowVisible(nint hWnd);

	[DllImport("user32.dll")]
	private static extern nint GetWindow(nint hWnd, uint uCmd);

	private const uint GW_OWNER = 4;

	[DllImport("user32.dll", EntryPoint = "GetWindowLongPtrW")]
	private static extern nint GetWindowLongPtr(nint hWnd, int nIndex);

	private const int GWL_EXSTYLE = -20;
	private const long WS_EX_TOOLWINDOW = 0x00000080L;

	[DllImport("user32.dll")]
	private static extern bool GetWindowRect(nint hWnd, out RECT lpRect);

	[DllImport("user32.dll")]
	private static extern nint MonitorFromWindow(nint hwnd, uint dwFlags);

	private const uint MONITOR_DEFAULTTONEAREST = 2;

	[DllImport("user32.dll")]
	private static extern bool GetMonitorInfo(nint hMonitor, ref MONITORINFO lpmi);

	[StructLayout(LayoutKind.Sequential)]
	private struct RECT
	{
		public int Left;
		public int Top;
		public int Right;
		public int Bottom;
	}

	[StructLayout(LayoutKind.Sequential)]
	private struct MONITORINFO
	{
		public uint cbSize;
		public RECT rcMonitor;
		public RECT rcWork;
		public uint dwFlags;
	}

	// ---- 前台激活 ----
	[DllImport("user32.dll")]
	private static extern uint GetWindowThreadProcessId(nint hWnd, out uint lpdwProcessId);

	[DllImport("user32.dll")]
	private static extern bool IsIconic(nint hWnd);

	[DllImport("user32.dll")]
	private static extern bool ShowWindow(nint hWnd, int nCmdShow);

	private const int SW_RESTORE = 9;
	private const int SW_MINIMIZE = 6;

	[DllImport("user32.dll")]
	private static extern bool BringWindowToTop(nint hWnd);

	[DllImport("user32.dll")]
	private static extern bool SetForegroundWindow(nint hWnd);

	[DllImport("user32.dll")]
	private static extern nint GetForegroundWindow();

	[DllImport("kernel32.dll")]
	private static extern uint GetCurrentThreadId();

	[DllImport("user32.dll")]
	private static extern bool AttachThreadInput(uint idAttach, uint idAttachTo, bool fAttach);

	// ---- 图标提取 ----
	[DllImport("user32.dll", CharSet = CharSet.Unicode)]
	private static extern nint SendMessage(nint hWnd, uint msg, nint wParam, nint lParam);

	private const uint WM_GETICON = 0x007F;

	[DllImport("user32.dll", CharSet = CharSet.Unicode)]
	private static extern nint GetClassLongPtr(nint hWnd, int nIndex);

	private const int GCLP_HICON = -14;

	[DllImport("shell32.dll", CharSet = CharSet.Unicode)]
	private static extern uint ExtractIconEx(string lpszFile, int nIconIndex, out nint phiconLarge, out nint phiconSmall, uint nIcons);

	[DllImport("user32.dll")]
	private static extern bool DestroyIcon(nint hIcon);

	// ---- 当前虚拟桌面过滤（Win10+；不可用则跳过）----
	[ComImport, Guid("a5cd92ff-29be-454c-8d04-d82879fb3f1b")]
	private class VirtualDesktopManager
	{
	}

	[ComImport, Guid("aa509086-5ca9-4c25-8f95-589d3c07b48a"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
	private interface IVirtualDesktopManager
	{
		int IsWindowOnCurrentVirtualDesktop(nint topLevelWindow, out int onCurrentDesktop);

		int GetWindowDesktopId(nint topLevelWindow, out Guid desktopId);

		int MoveWindowToDesktop(nint topLevelWindow, ref Guid desktopId);
	}

	private static readonly IVirtualDesktopManager? s_vdm = CreateVdm();

	private static IVirtualDesktopManager? CreateVdm()
	{
		try
		{
			return (IVirtualDesktopManager)new VirtualDesktopManager();
		}
		catch
		{
			return null;
		}
	}

	/// <summary>当前进程 PID（轮盘/设置窗口自身不计入候选）。</summary>
	private static uint SelfPid => (uint)Environment.ProcessId;

	/// <summary>
	/// 按任务栏按钮语义枚举当前桌面的可切换窗口（z 序自上而下，即任务栏从左到右）。
	/// 过滤：可见、无 owner、非 WS_EX_TOOLWINDOW、排除 StarPie 自身、与所属显示器工作区相交（防离屏"托盘驻留"窗口）、当前虚拟桌面。
	/// </summary>
	public static List<nint> GetTaskbarWindows()
	{
		List<nint> list = new List<nint>();
		uint selfPid = SelfPid;
		EnumWindows(delegate (nint hWnd, nint lParam)
		{
			try
			{
				if (!IsWindowVisible(hWnd))
				{
					return true;
				}
				if (GetWindow(hWnd, GW_OWNER) != IntPtr.Zero)
				{
					return true;
				}
				if ((GetWindowLongPtr(hWnd, GWL_EXSTYLE).ToInt64() & WS_EX_TOOLWINDOW) != 0L)
				{
					return true;
				}
				GetWindowThreadProcessId(hWnd, out uint pid);
				if (pid == selfPid)
				{
					return true;
				}
				// 最小化窗口的矩形是任务栏处占位矩形，不参与"离屏"判定（其任务栏按钮必然存在）
				if (!IsIconic(hWnd) && GetWindowRect(hWnd, out RECT rc))
				{
					nint mon = MonitorFromWindow(hWnd, MONITOR_DEFAULTTONEAREST);
					if (mon != IntPtr.Zero)
					{
						MONITORINFO mi = default;
						mi.cbSize = (uint)Marshal.SizeOf<MONITORINFO>();
						if (GetMonitorInfo(mon, ref mi))
						{
							bool intersects = rc.Left < mi.rcWork.Right && rc.Right > mi.rcWork.Left && rc.Top < mi.rcWork.Bottom && rc.Bottom > mi.rcWork.Top;
							if (!intersects)
							{
								return true;
							}
						}
					}
				}
				if (s_vdm != null)
				{
					int onCurrent = 0;
					if (s_vdm.IsWindowOnCurrentVirtualDesktop(hWnd, out onCurrent) == 0 && onCurrent == 0)
					{
						return true;
					}
				}
				list.Add(hWnd);
			}
			catch
			{
			}
			return true;
		}, IntPtr.Zero);
		return list;
	}

	/// <summary>找到任务栏应用按钮工具栏（Win10 经典任务栏）；Win11 XAML 任务栏返回 0。</summary>
	private static nint FindTaskbarToolbar()
	{
		try
		{
			nint taskbar = FindWindow("Shell_TrayWnd", null);
			if (taskbar == IntPtr.Zero)
			{
				return IntPtr.Zero;
			}
			nint toolbar = FindWindowEx(taskbar, IntPtr.Zero, "MSTaskSwWClass", null);
			if (toolbar == IntPtr.Zero)
			{
				toolbar = FindWindowEx(taskbar, IntPtr.Zero, "MSTaskListWClass", null);
			}
			return toolbar;
		}
		catch
		{
			return IntPtr.Zero;
		}
	}

	/// <summary>
	/// 任务栏"应用槽位"（与 Win+N 计数一致）：按任务栏按钮顺序，只保留应用按钮——
	/// 运行窗口（idCommand=窗口句柄）与固定应用（未运行，idCommand=0 但 dwData 有应用标识）；
	/// 系统按钮（任务视图/搜索等 idCommand=0 且 dwData=0）被剔除。
	/// </summary>
	private static (nint toolbar, List<(int rawIndex, nint hwnd)> slots)? GetTaskbarAppSlots()
	{
		nint toolbar = FindTaskbarToolbar();
		if (toolbar == IntPtr.Zero)
		{
			return null;
		}
		try
		{
			int count = (int)SendMessage(toolbar, TB_BUTTONCOUNT, IntPtr.Zero, IntPtr.Zero);
			if (count <= 0)
			{
				return null;
			}
			List<(int, nint)> slots = new List<(int, nint)>(count);
			byte[] buf = new byte[Marshal.SizeOf<TBBUTTON>()];
			GCHandle pin = GCHandle.Alloc(buf, GCHandleType.Pinned);
			try
			{
				for (int i = 0; i < count; i++)
				{
					if (SendMessage(toolbar, TB_GETBUTTON, (nint)i, pin.AddrOfPinnedObject()).ToInt64() <= 0)
					{
						continue;
					}
					TBBUTTON btn = Marshal.PtrToStructure<TBBUTTON>(pin.AddrOfPinnedObject());
					// 应用按钮：运行窗口 idCommand=窗口句柄；固定应用 dwData 携带应用标识。系统按钮两者皆 0。
					if (btn.idCommand != 0 || btn.dwData != IntPtr.Zero)
					{
						slots.Add((i, (nint)btn.idCommand));
					}
				}
			}
			finally
			{
				pin.Free();
			}
			return slots.Count > 0 ? (toolbar, slots) : null;
		}
		catch
		{
			return null;
		}
	}

	/// <summary>任务栏第 n 个应用槽位的窗口句柄（与 Win+N 位置对齐；固定未运行返回 0）；工具栏不可用回退 filtered 枚举。</summary>
	public static nint GetNthTaskbarWindow(int n)
	{
		if (n <= 0)
		{
			return IntPtr.Zero;
		}
		var app = GetTaskbarAppSlots();
		if (app != null)
		{
			if (n > app.Value.slots.Count)
			{
				return IntPtr.Zero;
			}
			return app.Value.slots[n - 1].hwnd;
		}
		List<nint> list = GetTaskbarWindows();
		if (n > list.Count)
		{
			return IntPtr.Zero;
		}
		return list[n - 1];
	}

	/// <summary>任务栏第 n 个应用槽位的图标：运行窗口取窗口图标；固定未运行取任务栏按钮图像；失败返回 null（默认程序图标兜底）。</summary>
	public static BitmapSource? GetNthWindowIcon(int n)
	{
		if (n <= 0)
		{
			return null;
		}
		var app = GetTaskbarAppSlots();
		if (app != null)
		{
			if (n > app.Value.slots.Count)
			{
				return null;
			}
			(int rawIndex, nint hwnd) = app.Value.slots[n - 1];
			if (hwnd != IntPtr.Zero)
			{
				return GetWindowIcon(hwnd);
			}
			return TryGetToolbarButtonIcon(app.Value.toolbar, rawIndex);
		}
		nint fallback = GetNthTaskbarWindow(n);
		return fallback != IntPtr.Zero ? GetWindowIcon(fallback) : null;
	}

	/// <summary>读取任务栏按钮在工具栏图像列表里的图标（固定未运行应用也可取到）。</summary>
	private static BitmapSource? TryGetToolbarButtonIcon(nint toolbar, int rawIndex)
	{
		try
		{
			nint imageList = SendMessage(toolbar, TB_GETIMAGELIST, IntPtr.Zero, IntPtr.Zero);
			if (imageList == IntPtr.Zero)
			{
				return null;
			}
			byte[] buf2 = new byte[Marshal.SizeOf<TBBUTTONINFO>()];
			GCHandle pin = GCHandle.Alloc(buf2, GCHandleType.Pinned);
			try
			{
				if (SendMessage(toolbar, TB_GETBUTTONINFO, (nint)rawIndex, pin.AddrOfPinnedObject()).ToInt64() == 0)
				{
					return null;
				}
				TBBUTTONINFO info2 = Marshal.PtrToStructure<TBBUTTONINFO>(pin.AddrOfPinnedObject());
				return GetImageListIcon(imageList, info2.iImage);
			}
			finally
			{
				pin.Free();
			}
		}
		catch
		{
			return null;
		}
	}

	private static BitmapSource? GetImageListIcon(nint imageList, int index)
	{
		try
		{
			nint hIcon = ImageList_GetIcon(imageList, index, ILD_TRANSPARENT);
			if (hIcon == IntPtr.Zero)
			{
				return null;
			}
			BitmapSource? bmp = ToBitmapSource(hIcon);
			DestroyIcon(hIcon);
			return bmp;
		}
		catch
		{
			return null;
		}
	}

	/// <summary>
	/// 切换到任务栏第 n 个槽位：n=1..10 直接模拟 Win+N（与系统快捷键完全一致，固定项也生效）；
	/// n=11..20 使用任务栏应用槽位顺序激活（回退 filtered 枚举）。
	/// </summary>
	public static bool ActivateTaskbarSlot(int n)
	{
		if (n <= 0)
		{
			return false;
		}
		if (n <= 10)
		{
			// Win+1..9, Win+0(=10)：原生快捷键（顺序稳定、含固定项；与"切换窗口1=Win+1"一致）
			byte digit = (byte)((n == 10) ? '0' : ('0' + n));
			keybd_event(VK_LWIN, 0, 0, IntPtr.Zero);
			keybd_event(digit, 0, 0, IntPtr.Zero);
			keybd_event(digit, 0, KEYEVENTF_KEYUP, IntPtr.Zero);
			keybd_event(VK_LWIN, 0, KEYEVENTF_KEYUP, IntPtr.Zero);
			return true;
		}
		nint hWnd = GetNthTaskbarWindow(n);
		if (hWnd == IntPtr.Zero)
		{
			return false;
		}
		return ActivateWindow(hWnd);
	}

	/// <summary>将窗口激活到前台：处理 Windows 前台锁（AttachThreadInput 到前台/目标线程 + BringWindowToTop + SetForegroundWindow，失败再最小化还原兜底强制前台）。</summary>
	public static bool ActivateWindow(nint hWnd)
	{
		if (hWnd == IntPtr.Zero)
		{
			return false;
		}
		try
		{
			nint hForeground = GetForegroundWindow();
			uint foregroundThread = (hForeground != IntPtr.Zero) ? GetWindowThreadProcessId(hForeground, out _) : 0u;
			uint targetThread = GetWindowThreadProcessId(hWnd, out _);
			uint currentThread = GetCurrentThreadId();

			bool attachedForeground = false;
			bool attachedTarget = false;
			if (foregroundThread != 0u && foregroundThread != currentThread && foregroundThread != targetThread)
			{
				attachedForeground = AttachThreadInput(foregroundThread, currentThread, true);
			}
			if (targetThread != currentThread)
			{
				attachedTarget = AttachThreadInput(currentThread, targetThread, true);
			}

			if (IsIconic(hWnd))
			{
				ShowWindow(hWnd, SW_RESTORE);
			}
			BringWindowToTop(hWnd);
			bool ok = SetForegroundWindow(hWnd);

			// 兜底：最小化再还原可强制获得前台（否则仅闪烁任务栏）
			if (!ok)
			{
				ShowWindow(hWnd, SW_MINIMIZE);
				ShowWindow(hWnd, SW_RESTORE);
				BringWindowToTop(hWnd);
				ok = SetForegroundWindow(hWnd);
			}

			if (attachedTarget)
			{
				AttachThreadInput(currentThread, targetThread, false);
			}
			if (attachedForeground)
			{
				AttachThreadInput(foregroundThread, currentThread, false);
			}
			return ok;
		}
		catch
		{
			return false;
		}
	}

	/// <summary>
	/// 提取窗口图标：WM_GETICON(大)→(小)→类图标 → 进程 exe 图标(ExtractIconEx，需 DestroyIcon)；
	/// 系统持有的窗口/类图标不销毁，只做一次性拷贝。
	/// </summary>
	public static BitmapSource? GetWindowIcon(nint hWnd)
	{
		if (hWnd == IntPtr.Zero)
		{
			return null;
		}
		try
		{
			nint hIcon = SendMessage(hWnd, WM_GETICON, (nint)1, IntPtr.Zero); // ICON_BIG
			if (hIcon == IntPtr.Zero)
			{
				hIcon = SendMessage(hWnd, WM_GETICON, IntPtr.Zero, IntPtr.Zero); // ICON_SMALL
			}
			if (hIcon == IntPtr.Zero)
			{
				hIcon = GetClassLongPtr(hWnd, GCLP_HICON);
			}
			if (hIcon != IntPtr.Zero)
			{
				BitmapSource? bmp = ToBitmapSource(hIcon);
				if (bmp != null)
				{
					return bmp;
				}
			}

			// 进程 exe 图标兜底
			GetWindowThreadProcessId(hWnd, out uint pid);
			try
			{
				using (System.Diagnostics.Process proc = System.Diagnostics.Process.GetProcessById((int)pid))
				{
					string? exe = proc?.MainModule?.FileName;
					if (!string.IsNullOrEmpty(exe) && ExtractIconEx(exe, 0, out nint big, out _, 1) > 0 && big != IntPtr.Zero)
					{
						BitmapSource? bmp = ToBitmapSource(big);
						DestroyIcon(big);
						if (bmp != null)
						{
							return bmp;
						}
					}
				}
			}
			catch
			{
			}
			return null;
		}
		catch
		{
			return null;
		}
	}

	private static BitmapSource? ToBitmapSource(nint hIcon)
	{
		try
		{
			BitmapSource bmp = Imaging.CreateBitmapSourceFromHIcon(hIcon, Int32Rect.Empty, BitmapSizeOptions.FromEmptyOptions());
			bmp.Freeze();
			return bmp;
		}
		catch
		{
			return null;
		}
	}
}
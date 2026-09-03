using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows;

namespace WinPieGestures;

/// <summary>
/// 平铺窗口执行器：把当前虚拟桌面上的可见窗口按预置布局平铺到各自主显示器工作区。
/// 纯物理像素 SetWindowPos；不激活、不抢焦点；由 ActionExecutor 的后台队列调用。
/// </summary>
public static class WindowTiler
{
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
		public int cbSize;
		public RECT rcMonitor;
		public RECT rcWork;
		public uint dwFlags;
	}

	[StructLayout(LayoutKind.Sequential)]
	private struct POINT
	{
		public int x;
		public int y;
	}

	private const uint MONITOR_DEFAULTTONEAREST = 2;

	[DllImport("user32.dll")]
	private static extern nint MonitorFromPoint(POINT pt, uint dwFlags);

	[DllImport("user32.dll")]
	private static extern nint MonitorFromWindow(nint hWnd, uint dwFlags);

	[DllImport("user32.dll")]
	private static extern bool GetMonitorInfo(nint hMonitor, ref MONITORINFO lpmi);

	[DllImport("user32.dll", SetLastError = true)]
	private static extern bool SetWindowPos(nint hWnd, nint hWndInsertAfter, int X, int Y, int cx, int cy, uint uFlags);

	[DllImport("user32.dll")]
	private static extern bool ShowWindow(nint hWnd, int nCmdShow);

	[DllImport("user32.dll")]
	private static extern bool IsWindowVisible(nint hWnd);

	[DllImport("user32.dll")]
	private static extern bool IsIconic(nint hWnd);

	[DllImport("user32.dll")]
	private static extern bool IsZoomed(nint hWnd);

	[DllImport("user32.dll")]
	private static extern int GetWindowText(nint hWnd, StringBuilder lpString, int nMaxCount);

	[DllImport("user32.dll")]
	private static extern uint GetWindowThreadProcessId(nint hWnd, out uint lpdwProcessId);

	private const int SW_RESTORE = 9;
	private const uint SWP_NOZORDER = 0x0004;
	private const uint SWP_NOACTIVATE = 0x0010;
	private const uint SWP_SHOWWINDOW = 0x0040;
	private const uint SWP_ASYNCWINDOWPOS = 0x4000;
	private static readonly nint HWND_TOP = new nint(0);

	/// <summary>可选布局 key 列表（顺序即编辑器下拉顺序）。</summary>
	public static List<string> LayoutKeys { get; } = new List<string>
	{
		"2L", "2T", "3L12", "3R21", "3R", "4G", "6G"
	};

	public static bool IsValidLayout(string key)
	{
		return LayoutKeys.Contains(key);
	}

	public static string LayoutDisplayName(string key)
	{
		return key switch
		{
			"2L" => I18n.T("TileLayout2L"),
			"2T" => I18n.T("TileLayout2T"),
			"3L12" => I18n.T("TileLayout3L12"),
			"3R21" => I18n.T("TileLayout3R21"),
			"3R" => I18n.T("TileLayout3R"),
			"4G" => I18n.T("TileLayout4G"),
			"6G" => I18n.T("TileLayout6G"),
			_ => key
		};
	}

	/// <summary>在后台执行平铺：取对象 → 各窗口主显示器工作区 → 按布局格子 SetWindowPos。</summary>
	public static void ExecuteTile(string? layoutKey)
	{
		try
		{
			string key = string.IsNullOrWhiteSpace(layoutKey) ? "2L" : layoutKey.Trim();
			if (!IsValidLayout(key))
			{
				key = "2L";
			}
			List<nint> targets = GetTileTargets();
			AppLogger.LogInfo($"[Tile] layout='{key}' targets={targets.Count}");
			if (targets.Count == 0)
			{
				return;
			}
			double[][] cells = LayoutCells(key);
			int count = Math.Min(targets.Count, cells.Length);
			for (int i = 0; i < count; i++)
			{
				RECT wa = WorkAreaOf(targets[i]);
				double[] c = cells[i];
				int x = wa.Left + (int)Math.Round((wa.Right - wa.Left) * c[0]);
				int y = wa.Top + (int)Math.Round((wa.Bottom - wa.Top) * c[1]);
				int w = (int)Math.Round((wa.Right - wa.Left) * c[2]) - (x - wa.Left);
				int h = (int)Math.Round((wa.Bottom - wa.Top) * c[3]) - (y - wa.Top);
				w = Math.Max(1, w);
				h = Math.Max(1, h);
				// 最大化/全屏状态会无视 SetWindowPos：先还原，再定位
				if (IsIconic(targets[i]))
				{
					ShowWindow(targets[i], SW_RESTORE);
				}
				else if (IsZoomed(targets[i]))
				{
					ShowWindow(targets[i], SW_RESTORE);
				}
				bool ok = SetWindowPos(targets[i], HWND_TOP, x, y, w, h, SWP_NOZORDER | SWP_NOACTIVATE | SWP_SHOWWINDOW | SWP_ASYNCWINDOWPOS);
				AppLogger.LogInfo($"[Tile] #{i} hwnd=0x{targets[i]:X} rect=({x},{y},{w}x{h}) ok={ok}");
			}
		}
		catch (Exception ex)
		{
			AppLogger.LogError("[Tile] 平铺失败", ex);
		}
	}

	/// <summary>平铺对象：当前虚拟桌面可见的普通顶层窗口（任务栏顺序）；排除无标题/隐藏/本进程自身。</summary>
	private static List<nint> GetTileTargets()
	{
		List<nint> result = new List<nint>();
		uint selfPid = (uint)Environment.ProcessId;
		foreach (nint h in WindowTaskbarHelper.GetTaskbarOrderedWindows())
		{
			if (h == IntPtr.Zero || !IsWindowVisible(h) || IsIconic(h))
			{
				continue; // 隐藏或最小化不参与
			}
			GetWindowThreadProcessId(h, out uint pid);
			if (pid == selfPid)
			{
				continue; // 排除设置窗等自身窗口
			}
			StringBuilder sb = new StringBuilder(128);
			if (GetWindowText(h, sb, 128) <= 0)
			{
				continue; // 无标题的后台窗口不参与
			}
			result.Add(h);
		}
		if (result.Count == 0)
		{
			// 兜底：任务栏快照拿不到时，直接枚举顶层窗口（当前桌面的可见普通窗口）
			result.AddRange(EnumerateTopLevelWindows());
		}
		return result;
	}

	private delegate bool EnumWindowsProc(nint hWnd, nint lParam);

	[DllImport("user32.dll")]
	private static extern bool EnumWindows(EnumWindowsProc lpEnumFunc, nint lParam);

	private static readonly EnumWindowsProc s_enumProc = EnumCallback;

	private static List<nint> s_enumBuffer = new List<nint>();

	private static bool EnumCallback(nint hWnd, nint lParam)
	{
		s_enumBuffer.Add(hWnd);
		return true;
	}

	/// <summary>EnumWindows 兜底枚举：可见、非本进程、有标题的顶层窗口（保留任务栏顺序路径的过滤语义）。</summary>
	private static List<nint> EnumerateTopLevelWindows()
	{
		uint selfPid = (uint)Environment.ProcessId;
		List<nint> buffer = new List<nint>();
		List<nint> previous = s_enumBuffer;
		s_enumBuffer = buffer;
		try
		{
			EnumWindows(s_enumProc, IntPtr.Zero);
		}
		catch
		{
		}
		finally
		{
			s_enumBuffer = previous;
		}
		List<nint> found = new List<nint>();
		foreach (nint h in buffer)
		{
			try
			{
				if (h == IntPtr.Zero || !IsWindowVisible(h) || IsIconic(h))
				{
					continue;
				}
				GetWindowThreadProcessId(h, out uint pid);
				if (pid == selfPid)
				{
					continue;
				}
				StringBuilder sb = new StringBuilder(128);
				if (GetWindowText(h, sb, 128) <= 0)
				{
					continue;
				}
				found.Add(h);
			}
			catch
			{
			}
		}
		return found;
	}

	private static RECT WorkAreaOf(nint hWnd)
	{
		try
		{
			nint mon = MonitorFromWindow(hWnd, MONITOR_DEFAULTTONEAREST);
			MONITORINFO mi = default;
			mi.cbSize = Marshal.SizeOf<MONITORINFO>();
			if (mon != IntPtr.Zero && GetMonitorInfo(mon, ref mi))
			{
				return mi.rcWork;
			}
		}
		catch
		{
		}
		return new RECT
		{
			Left = (int)SystemParameters.VirtualScreenLeft,
			Top = (int)SystemParameters.VirtualScreenTop,
			Right = (int)(SystemParameters.VirtualScreenLeft + SystemParameters.VirtualScreenWidth),
			Bottom = (int)(SystemParameters.VirtualScreenTop + SystemParameters.VirtualScreenHeight)
		};
	}

	/// <summary>每种布局的格子比例 [x0,y0,x1,y1]，格子顺序 = 窗口顺序。</summary>
	private static double[][] LayoutCells(string key)
	{
		return key switch
		{
			"2L" => new[]
			{
				new[] { 0.0, 0.0, 0.5, 1.0 },
				new[] { 0.5, 0.0, 1.0, 1.0 }
			},
			"2T" => new[]
			{
				new[] { 0.0, 0.0, 1.0, 0.5 },
				new[] { 0.0, 0.5, 1.0, 1.0 }
			},
			"3L12" => new[]
			{
				new[] { 0.0, 0.0, 0.5, 1.0 },
				new[] { 0.5, 0.0, 1.0, 0.5 },
				new[] { 0.5, 0.5, 1.0, 1.0 }
			},
			"3R21" => new[]
			{
				new[] { 0.0, 0.0, 0.5, 0.5 },
				new[] { 0.0, 0.5, 0.5, 1.0 },
				new[] { 0.5, 0.0, 1.0, 1.0 }
			},
			"3R" => new[]
			{
				new[] { 0.0, 0.0, 1.0 / 3.0, 1.0 },
				new[] { 1.0 / 3.0, 0.0, 2.0 / 3.0, 1.0 },
				new[] { 2.0 / 3.0, 0.0, 1.0, 1.0 }
			},
			"4G" => new[]
			{
				new[] { 0.0, 0.0, 0.5, 0.5 },
				new[] { 0.5, 0.0, 1.0, 0.5 },
				new[] { 0.0, 0.5, 0.5, 1.0 },
				new[] { 0.5, 0.5, 1.0, 1.0 }
			},
			"6G" => new[]
			{
				new[] { 0.0, 0.0, 1.0 / 3.0, 0.5 },
				new[] { 1.0 / 3.0, 0.0, 2.0 / 3.0, 0.5 },
				new[] { 2.0 / 3.0, 0.0, 1.0, 0.5 },
				new[] { 0.0, 0.5, 1.0 / 3.0, 1.0 },
				new[] { 1.0 / 3.0, 0.5, 2.0 / 3.0, 1.0 },
				new[] { 2.0 / 3.0, 0.5, 1.0, 1.0 }
			},
			_ => new[] { new[] { 0.0, 0.0, 0.5, 1.0 }, new[] { 0.5, 0.0, 1.0, 1.0 } }
		};
	}
}
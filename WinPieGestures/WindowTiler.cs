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
	private static extern nint MonitorFromRect(RECT lprc, uint dwFlags);

	[DllImport("user32.dll")]
	private static extern nint GetShellWindow();

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

	[DllImport("user32.dll")]
	private static extern nint GetForegroundWindow();

	[DllImport("user32.dll")]
	private static extern bool GetWindowRect(nint hWnd, out RECT lpRect);

	[DllImport("user32.dll", EntryPoint = "GetWindowLongPtrW")]
	private static extern nint GetWindowLongPtr(nint hWnd, int nIndex);

	[DllImport("user32.dll", EntryPoint = "SetWindowLongPtrW", SetLastError = true)]
	private static extern nint SetWindowLongPtr(nint hWnd, int nIndex, nint dwNewLong);

	[DllImport("user32.dll")]
	private static extern bool SetLayeredWindowAttributes(nint hWnd, uint crKey, byte bAlpha, uint dwFlags);

	[DllImport("user32.dll")]
	private static extern bool EnumDisplayMonitors(nint hdc, nint lprcClip, EnumMonitorsProc lpfnEnum, nint dwData);

	private delegate bool EnumMonitorsProc(nint hMonitor, nint hdcMonitor, ref RECT lprcMonitor, nint dwData);

	private const int SW_RESTORE = 9;
	private const uint SWP_NOZORDER = 0x0004;
	private const uint SWP_NOMOVE = 0x0002;
	private const uint SWP_NOSIZE = 0x0001;
	private const uint SWP_NOACTIVATE = 0x0010;
	private const uint SWP_SHOWWINDOW = 0x0040;
	private const uint SWP_ASYNCWINDOWPOS = 0x4000;
	private static readonly nint HWND_TOP = new nint(0);
	private const uint WS_EX_LAYERED = 0x00080000;
	private const int GWL_EXSTYLE = -20;
	private const uint LWA_ALPHA = 0x00000002;
	private const uint SW_RESTORE_U = 9u;

	// 上次平铺快照（供「恢复」与内存记忆）
	private static readonly Dictionary<nint, RECT> s_lastSnapshot = new Dictionary<nint, RECT>();
	private static int s_cycleIndex;

	/// <summary>可选布局 key 列表（顺序即编辑器下拉顺序）。</summary>
	public static List<string> LayoutKeys { get; } = new List<string>
	{
		"2L", "2T", "3L12", "3R21", "3R", "4G", "6G"
	};

	/// <summary>轮换模式标记：参数为 Cycle 时循环切换布局。</summary>
	public const string CycleParam = "Cycle";

	/// <summary>还原标记：参数为 Restore 时还原所有窗口到平铺前。</summary>
	public const string RestoreParam = "Restore";

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
			if (string.Equals(key, RestoreParam, StringComparison.OrdinalIgnoreCase))
			{
				RestoreLastLayout();
				return;
			}
			if (string.Equals(key, CycleParam, StringComparison.OrdinalIgnoreCase))
			{
				key = LayoutKeys[s_cycleIndex % LayoutKeys.Count];
				s_cycleIndex = (s_cycleIndex + 1) % LayoutKeys.Count;
			}
			if (!IsValidLayout(key))
			{
				key = "2L";
			}
			// 排除名单（进程 exe 名，逗号/分号分隔）
			HashSet<string> exclude = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
			string? excl = ConfigManager.CurrentConfig?.TileExcludeProcesses;
			if (!string.IsNullOrWhiteSpace(excl))
			{
				foreach (string token in excl.Split(new[] { ',', ';', '，', '；', ' ' }, StringSplitOptions.RemoveEmptyEntries))
				{
					exclude.Add(token.Trim().ToLowerInvariant());
				}
			}
			bool includeMinimized = ConfigManager.CurrentConfig?.TileIncludeMinimized == true;
			List<nint> targets = GetTileTargets(exclude, includeMinimized);
			AppLogger.LogInfo($"[Tile] layout='{key}' targets={targets.Count}");
			if (targets.Count == 0)
			{
				return;
			}
			double[][] cells = LayoutCells(key);
			int count = Math.Min(targets.Count, cells.Length);
			Dictionary<nint, RECT> snapshot = new Dictionary<nint, RECT>();
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
				RECT before;
				GetWindowRect(targets[i], out before);
				// 最小化/最大化会无视 SetWindowPos：先还原，再定位
				if (IsIconic(targets[i]) || IsZoomed(targets[i]))
				{
					ShowWindow(targets[i], SW_RESTORE);
				}
				bool ok = SetWindowPos(targets[i], HWND_TOP, x, y, w, h, SWP_NOZORDER | SWP_NOACTIVATE | SWP_SHOWWINDOW | SWP_ASYNCWINDOWPOS);
				AppLogger.LogInfo($"[Tile] #{i} hwnd=0x{targets[i]:X} rect=({x},{y},{w}x{h}) ok={ok}");
				if (ok)
				{
					snapshot[targets[i]] = before;
				}
			}
			lock (s_lastSnapshot)
			{
				// 只记录"第一次"平铺前的状态：后续换布局/循环不覆盖，
				// 保证「还原所有窗口」始终回到首次平铺之前的样式。
				if (s_lastSnapshot.Count == 0)
				{
					foreach (var kv in snapshot)
					{
						s_lastSnapshot[kv.Key] = kv.Value;
					}
				}
			}
		}
		catch (Exception ex)
		{
			AppLogger.LogError("[Tile] 平铺失败", ex);
		}
	}

	/// <summary>恢复上次平铺前的窗口位置/大小（按需优先还原显示）。</summary>
	public static void RestoreLastLayout()
	{
		try
		{
			Dictionary<nint, RECT> snap;
			lock (s_lastSnapshot)
			{
				snap = new Dictionary<nint, RECT>(s_lastSnapshot);
			}
			AppLogger.LogInfo($"[Tile] restore targets={snap.Count}");
			foreach (var kv in snap)
			{
				try
				{
					RECT r = kv.Value;
					if (IsIconic(kv.Key))
					{
						ShowWindow(kv.Key, SW_RESTORE);
					}
					SetWindowPos(kv.Key, HWND_TOP, r.Left, r.Top, Math.Max(1, r.Right - r.Left), Math.Max(1, r.Bottom - r.Top),
						SWP_NOZORDER | SWP_NOACTIVATE | SWP_SHOWWINDOW | SWP_ASYNCWINDOWPOS);
				}
				catch
				{
				}
			}
		}
		catch (Exception ex)
		{
			AppLogger.LogError("[Tile] 恢复失败", ex);
		}
	}

	/// <summary>平铺对象：当前虚拟桌面可见窗口（任务栏顺序）；排除无标题/隐藏/本进程/排除名单；按配置决定是否含最小化。</summary>
	private static List<nint> GetTileTargets(HashSet<string> excludedExes, bool includeMinimized)
	{
		List<nint> result = new List<nint>();
		uint selfPid = (uint)Environment.ProcessId;
		foreach (nint h in WindowTaskbarHelper.GetTaskbarOrderedWindows())
		{
			if (h == IntPtr.Zero || !IsWindowVisible(h))
			{
				continue;
			}
			if (!includeMinimized && IsIconic(h))
			{
				continue; // 最小化不参与（默认）
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
			if (excludedExes.Count > 0)
			{
				string? exe = WindowTaskbarHelper.GetProcessImageNameByPid(pid);
				if (exe != null && excludedExes.Contains(System.IO.Path.GetFileNameWithoutExtension(exe).ToLowerInvariant()))
				{
					continue; // 命中排除名单
				}
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

	/// <summary>把当前前台窗口平移到下一台显示器（保持相对位置与尺寸）。</summary>
	public static void MoveWindowToNextMonitor()
	{
		try
		{
			nint fg = GetForegroundWindow();
			if (fg == IntPtr.Zero || fg == GetShellWindowHandleSafe())
			{
				return;
			}
			uint fgPid;
			GetWindowThreadProcessId(fg, out fgPid);
			if (fgPid == (uint)Environment.ProcessId)
			{
				return; // 不动自己的窗口
			}
			RECT cur;
			GetWindowRect(fg, out cur);
			RECT curWork = WorkAreaOf(fg);
			List<RECT> monitors = new List<RECT>();
			EnumDisplayMonitors(IntPtr.Zero, IntPtr.Zero, delegate(nint hMon, nint hdc, ref RECT rc, nint data)
			{
				monitors.Add(rc);
				return true;
			}, IntPtr.Zero);
			if (monitors.Count < 2)
			{
				return; // 单显示器无可移动
			}
			// 找到当前所在屏，取下一个（循环）
			int curIdx = -1;
			for (int i = 0; i < monitors.Count; i++)
			{
				RECT m = monitors[i];
				if (cur.Left >= m.Left && cur.Left < m.Right && cur.Top >= m.Top && cur.Top < m.Bottom)
				{
					curIdx = i;
					break;
				}
			}
			int next = (curIdx + 1) % monitors.Count;
			RECT nm = monitors[next];
			RECT nw = WorkAreaOfMonitor(nm);
			// 相对当前屏工作区的比例 → 目标屏工作区
			int curW = Math.Max(1, curWork.Right - curWork.Left);
			int curH = Math.Max(1, curWork.Bottom - curWork.Top);
			int fx = cur.Left - curWork.Left;
			int fy = cur.Top - curWork.Top;
			int targetW = Math.Max(1, nw.Right - nw.Left);
			int targetH = Math.Max(1, nw.Bottom - nw.Top);
			int x = nw.Left + (int)Math.Round((double)fx * targetW / curW);
			int y = nw.Top + (int)Math.Round((double)fy * targetH / curH);
			int w = Math.Max(1, cur.Right - cur.Left);
			int h = Math.Max(1, cur.Bottom - cur.Top);
			w = Math.Min(w, targetW);
			h = Math.Min(h, targetH);
			if (IsIconic(fg))
			{
				ShowWindow(fg, SW_RESTORE);
			}
			SetWindowPos(fg, HWND_TOP, x, y, w, h, SWP_NOZORDER | SWP_NOACTIVATE | SWP_SHOWWINDOW | SWP_ASYNCWINDOWPOS);
			AppLogger.LogInfo($"[Tile] MoveToMonitor: hwnd=0x{fg:X} → monitor#{next} ({x},{y},{w}x{h})");
		}
		catch (Exception ex)
		{
			AppLogger.LogError("[Tile] 移动显示器失败", ex);
		}
	}

	private static nint GetShellWindowHandleSafe()
	{
		try
		{
			return GetShellWindow();
		}
		catch
		{
			return IntPtr.Zero;
		}
	}

	private static RECT WorkAreaOfMonitor(RECT monitor)
	{
		try
		{
			nint mon = MonitorFromRect(monitor, MONITOR_DEFAULTTONEAREST);
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
		return monitor;
	}

	/// <summary>切换当前前台窗口置顶状态；参数 "1"/"on" 强制置顶、"0"/"off" 取消、空参数切换。</summary>
	public static void ToggleWindowTopmost(string? param)
	{
		try
		{
			nint fg = GetForegroundWindow();
			if (fg == IntPtr.Zero || fg == GetShellWindowHandleSafe())
			{
				return;
			}
			uint fgPid;
			GetWindowThreadProcessId(fg, out fgPid);
			if (fgPid == (uint)Environment.ProcessId)
			{
				return;
			}
			bool top = (GetWindowLongPtr(fg, GWL_EXSTYLE).ToInt64() & 0x8L) != 0L; // WS_EX_TOPMOST
			bool want = string.IsNullOrWhiteSpace(param) ? !top : (param.Trim() == "1" || string.Equals(param.Trim(), "on", StringComparison.OrdinalIgnoreCase));
			SetWindowPos(fg, want ? new nint(-1) : new nint(-2), 0, 0, 0, 0, SWP_NOMOVE | SWP_NOSIZE | SWP_NOACTIVATE);
			AppLogger.LogInfo($"[Tile] Topmost hwnd=0x{fg:X} → {want}");
		}
		catch (Exception ex)
		{
			AppLogger.LogError("[Tile] 置顶失败", ex);
		}
	}

	/// <summary>设置当前前台窗口透明度（参数 1~100，空参数默认 50；100 = 不透明）。</summary>
	public static void SetWindowOpacity(string? param)
	{
		try
		{
			nint fg = GetForegroundWindow();
			if (fg == IntPtr.Zero || fg == GetShellWindowHandleSafe())
			{
				return;
			}
			uint fgPid;
			GetWindowThreadProcessId(fg, out fgPid);
			if (fgPid == (uint)Environment.ProcessId)
			{
				return;
			}
			double level = 50.0;
			if (double.TryParse(param, out double v))
			{
				level = v;
			}
			level = Math.Max(1.0, Math.Min(100.0, level));
			nint ex = GetWindowLongPtr(fg, GWL_EXSTYLE);
			SetWindowLongPtr(fg, GWL_EXSTYLE, new nint(ex.ToInt64() | WS_EX_LAYERED));
			SetLayeredWindowAttributes(fg, 0, (byte)Math.Round(level * 255.0 / 100.0), LWA_ALPHA);
			AppLogger.LogInfo($"[Tile] Opacity hwnd=0x{fg:X} → {level}%");
		}
		catch (Exception ex)
		{
			AppLogger.LogError("[Tile] 透明度失败", ex);
		}
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
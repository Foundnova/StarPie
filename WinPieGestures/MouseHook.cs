using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Threading;
using System.Windows;
using System.Windows.Threading;

namespace WinPieGestures;

public class MouseHook
{
	private struct POINT
	{
		public int x;

		public int y;
	}

	[StructLayout(LayoutKind.Sequential)]
	private struct MSG
	{
		public nint hwnd;
		public uint message;
		public nuint wParam;
		public nint lParam;
		public uint time;
		public POINT pt;
		public uint lPrivate;
	}

	private struct MSLLHOOKSTRUCT
	{
		public POINT pt;

		public uint mouseData;

		public uint flags;

		public uint time;

		public nint dwExtraInfo;
	}

	private delegate nint LowLevelMouseProc(int nCode, nint wParam, nint lParam);

	private const int WH_MOUSE_LL = 14;

	private const int WM_MOUSEMOVE = 512;

	private const int WM_LBUTTONDOWN = 513;

	private const int WM_LBUTTONUP = 514;

	private const int WM_RBUTTONDOWN = 516;

	private const int WM_RBUTTONUP = 517;

	private const int WM_MBUTTONDOWN = 519;

	private const int WM_MBUTTONUP = 520;

	private const int WM_XBUTTONDOWN = 523;

	private const int WM_XBUTTONUP = 524;

	private const uint WM_QUIT = 18u;

	private const uint PM_NOREMOVE = 0u;

	private const uint MOUSEEVENTF_RIGHTDOWN = 8u;

	private const uint MOUSEEVENTF_RIGHTUP = 16u;

	private const uint MOUSEEVENTF_MIDDLEDOWN = 32u;

	private const uint MOUSEEVENTF_MIDDLEUP = 64u;

	private const uint MOUSEEVENTF_XDOWN = 128u;

	private const uint MOUSEEVENTF_XUP = 256u;

	private const uint XBUTTON1 = 1u;

	private const uint XBUTTON2 = 2u;

	private LowLevelMouseProc _proc;

	private nint _hookId = IntPtr.Zero;

	private int _ignoreNextButtonDown;

	private int _ignoreNextButtonUp;

	private readonly object _lifecycleSync = new object();

	private Thread? _hookThread;

	private uint _hookThreadId;

	private ManualResetEventSlim? _hookReady;

	private Exception? _hookStartException;

	private volatile bool _stopRequested;

	private int _isPaused;

	private System.Threading.Timer? _healthCheckTimer;

	private POINT _lastSystemCursorPos;

	private int _hookEventsCountSinceLastCheck;

	public bool IsPaused
	{
		get
		{
			return Volatile.Read(ref _isPaused) != 0;
		}
		set
		{
			Volatile.Write(ref _isPaused, value ? 1 : 0);
		}
	}

	public event EventHandler<MouseEventArgs>? OnTriggerButtonDown;

	public event EventHandler<MouseEventArgs>? OnTriggerButtonUp;

	public event EventHandler<MouseEventArgs>? OnMouseMove;

	public event EventHandler<MouseEventArgs>? OnRawMouseEvent;

	public event EventHandler<RawMouseEventArgs>? OnRawMouseButtonEvent;

	public event EventHandler<MouseEventArgs>? OnRightButtonDown
	{
		add
		{
			OnTriggerButtonDown += value;
		}
		remove
		{
			OnTriggerButtonDown -= value;
		}
	}

	public event EventHandler<MouseEventArgs>? OnRightButtonUp
	{
		add
		{
			OnTriggerButtonUp += value;
		}
		remove
		{
			OnTriggerButtonUp -= value;
		}
	}

	[DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
	private static extern nint SetWindowsHookEx(int idHook, LowLevelMouseProc lpfn, nint hMod, uint dwThreadId);

	[DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
	[return: MarshalAs(UnmanagedType.Bool)]
	private static extern bool UnhookWindowsHookEx(nint hhk);

	[DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
	private static extern nint CallNextHookEx(nint hhk, int nCode, nint wParam, nint lParam);

	[DllImport("kernel32.dll", CharSet = CharSet.Auto, SetLastError = true)]
	private static extern nint GetModuleHandle(string lpModuleName);

	[DllImport("kernel32.dll")]
	private static extern uint GetCurrentThreadId();

	[DllImport("user32.dll", SetLastError = true)]
	[return: MarshalAs(UnmanagedType.Bool)]
	private static extern bool PeekMessage(out MSG lpMsg, nint hWnd, uint wMsgFilterMin, uint wMsgFilterMax, uint wRemoveMsg);

	[DllImport("user32.dll", SetLastError = true)]
	private static extern int GetMessage(out MSG lpMsg, nint hWnd, uint wMsgFilterMin, uint wMsgFilterMax);

	[DllImport("user32.dll")]
	[return: MarshalAs(UnmanagedType.Bool)]
	private static extern bool TranslateMessage(ref MSG lpMsg);

	[DllImport("user32.dll")]
	private static extern nint DispatchMessage(ref MSG lpMsg);

	[DllImport("user32.dll", SetLastError = true)]
	[return: MarshalAs(UnmanagedType.Bool)]
	private static extern bool PostThreadMessage(uint idThread, uint msg, nuint wParam, nint lParam);

	[DllImport("user32.dll")]
	private static extern void mouse_event(uint dwFlags, uint dx, uint dy, uint dwData, uint dwExtraInfo);

	/// <summary>StarPie 自发注入鼠标事件的标记：钩子遇到该值直接放行，杜绝自身捕获与竞争。</summary>
	internal const nint StarPieExtraInfo = 0x53544152;

	[DllImport("user32.dll")]
	[return: MarshalAs(UnmanagedType.Bool)]
	private static extern bool GetCursorPos(out POINT lpPoint);

	public MouseHook()
	{
		_proc = HookCallback;
	}

	public void Start()
	{
		ManualResetEventSlim ready;
		lock (_lifecycleSync)
		{
			if (_hookThread != null && _hookThread.IsAlive)
			{
				return;
			}
			_stopRequested = false;
			_hookStartException = null;
			ready = new ManualResetEventSlim(initialState: false);
			_hookReady = ready;
			_hookThread = new Thread(HookThreadMain)
			{
				IsBackground = true,
				Name = "StarPie.MouseHook",
				Priority = ThreadPriority.AboveNormal
			};
			_hookThread.Start();
		}

		try
		{
			if (!ready.Wait(TimeSpan.FromSeconds(5)))
			{
				throw new TimeoutException("Timed out while starting the low-level mouse hook.");
			}
			Exception? startException;
			lock (_lifecycleSync)
			{
				startException = _hookStartException;
			}
			if (startException != null)
			{
				throw new Exception("Failed to set low-level mouse hook.", startException);
			}

			_hookEventsCountSinceLastCheck = 0;
			GetCursorPos(out _lastSystemCursorPos);
			lock (_lifecycleSync)
			{
				_healthCheckTimer ??= new System.Threading.Timer(CheckHookHealth, null, 3000, 3000);
			}
		}
		catch
		{
			Stop();
			throw;
		}
	}

	public void Stop()
	{
		System.Threading.Timer? healthCheckTimer;
		Thread? hookThread;
		uint hookThreadId;
		lock (_lifecycleSync)
		{
			_stopRequested = true;
			healthCheckTimer = _healthCheckTimer;
			_healthCheckTimer = null;
			hookThread = _hookThread;
			hookThreadId = _hookThreadId;
		}
		healthCheckTimer?.Dispose();

		if (hookThreadId != 0)
		{
			PostThreadMessage(hookThreadId, WM_QUIT, 0u, IntPtr.Zero);
		}

		if (hookThread != null && hookThread.ManagedThreadId != Environment.CurrentManagedThreadId)
		{
			hookThread.Join(TimeSpan.FromSeconds(2));
		}

		lock (_lifecycleSync)
		{
			if (_hookThread == hookThread && (hookThread == null || !hookThread.IsAlive))
			{
				_hookThread = null;
				_hookThreadId = 0;
				_hookReady?.Dispose();
				_hookReady = null;
				_hookId = IntPtr.Zero;
			}
		}
	}

	private void HookThreadMain()
	{
		nint hookId = IntPtr.Zero;
		uint threadId = GetCurrentThreadId();
		lock (_lifecycleSync)
		{
			_hookThreadId = threadId;
		}

		try
		{
			// Force creation of this thread's message queue before Start() can
			// post WM_QUIT during shutdown.
			PeekMessage(out MSG _, IntPtr.Zero, 0u, 0u, PM_NOREMOVE);
			hookId = SetHook(_proc);
			if (hookId == IntPtr.Zero)
			{
				throw new InvalidOperationException("SetWindowsHookEx returned a null hook handle.");
			}
			lock (_lifecycleSync)
			{
				_hookId = hookId;
			}
			_hookReady?.Set();

			MSG message;
			while (!_stopRequested)
			{
				int result = GetMessage(out message, IntPtr.Zero, 0u, 0u);
				if (result <= 0)
				{
					break;
				}
				TranslateMessage(ref message);
				DispatchMessage(ref message);
			}
		}
		catch (Exception ex)
		{
			lock (_lifecycleSync)
			{
				_hookStartException = ex;
			}
			_hookReady?.Set();
		}
		finally
		{
			if (hookId != IntPtr.Zero)
			{
				UnhookWindowsHookEx(hookId);
			}
			lock (_lifecycleSync)
			{
				if (_hookId == hookId)
				{
					_hookId = IntPtr.Zero;
				}
				_hookThreadId = 0;
			}
			_hookReady?.Set();
		}
	}

	private void CheckHookHealth(object? state)
	{
		if (_hookId == IntPtr.Zero || !GetCursorPos(out var lpPoint))
		{
			return;
		}
		bool num = lpPoint.x != _lastSystemCursorPos.x || lpPoint.y != _lastSystemCursorPos.y;
		_lastSystemCursorPos = lpPoint;
		if (num)
		{
			if (Interlocked.Exchange(ref _hookEventsCountSinceLastCheck, 0) != 0)
			{
				return;
			}
			Application current = Application.Current;
			if (current == null)
			{
				return;
			}
			Dispatcher dispatcher = ((DispatcherObject)current).Dispatcher;
			if (dispatcher == null)
			{
				return;
			}
			dispatcher.BeginInvoke((Delegate)(Action)delegate
			{
				try
				{
					AppLogger.LogWarn("MouseHook detected stalled events while cursor moved. Restarting hook...");
					Stop();
					Start();
					AppLogger.LogInfo("MouseHook restarted successfully.");
				}
				catch (Exception ex)
				{
					AppLogger.LogError("Failed to restart MouseHook during health check", ex);
				}
			}, Array.Empty<object>());
		}
		else
		{
			Interlocked.Exchange(ref _hookEventsCountSinceLastCheck, 0);
		}
	}

	private nint SetHook(LowLevelMouseProc proc)
	{
		using Process process = Process.GetCurrentProcess();
		using ProcessModule processModule = process.MainModule;
		if (processModule == null)
		{
			throw new InvalidOperationException("MainModule is null.");
		}
		return SetWindowsHookEx(14, proc, GetModuleHandle(processModule.ModuleName), 0u);
	}

	private nint HookCallback(int nCode, nint wParam, nint lParam)
	{
		Interlocked.Increment(ref _hookEventsCountSinceLastCheck);
		if (IsPaused)
		{
			return CallNextHookEx(_hookId, nCode, wParam, lParam);
		}
		if (nCode >= 0)
		{
			int num = (int)wParam;
			MSLLHOOKSTRUCT mSLLHOOKSTRUCT = Marshal.PtrToStructure<MSLLHOOKSTRUCT>(lParam);
			// StarPie 自发模拟的鼠标事件直接快速放行，杜绝自身捕获与竞争
			if (mSLLHOOKSTRUCT.dwExtraInfo == StarPieExtraInfo)
			{
				return CallNextHookEx(_hookId, nCode, wParam, lParam);
			}
			if (num == 512)
			{
				MouseEventArgs e = new MouseEventArgs(mSLLHOOKSTRUCT.pt.x, mSLLHOOKSTRUCT.pt.y);
				OnMouseMove?.Invoke(this, e);
				if (e.Handled)
				{
					return 1;
				}
				return CallNextHookEx(_hookId, nCode, wParam, lParam);
			}
			MouseEventArgs e2 = new MouseEventArgs(mSLLHOOKSTRUCT.pt.x, mSLLHOOKSTRUCT.pt.y);
			OnRawMouseEvent?.Invoke(this, e2);
			string text = "";
			bool flag = false;
			bool flag2 = false;
			switch (num)
			{
			case 519:
			case 520:
				text = "MiddleButton";
				flag = num == 519;
				flag2 = num == 520;
				break;
			case 516:
			case 517:
				text = "RightButton";
				flag = num == 516;
				flag2 = num == 517;
				break;
			case 523:
			case 524:
				text = ((((mSLLHOOKSTRUCT.mouseData >> 16) & 0xFFFF) == 2) ? "XButton2" : "XButton1");
				flag = num == 523;
				flag2 = num == 524;
				break;
			case 513:
			case 514:
				text = "LeftButton";
				flag = num == 513;
				flag2 = num == 514;
				break;
			}
			if (!string.IsNullOrEmpty(text))
			{
				RawMouseEventArgs e3 = new RawMouseEventArgs(num, text, mSLLHOOKSTRUCT.mouseData, flag, mSLLHOOKSTRUCT.pt.x, mSLLHOOKSTRUCT.pt.y);
				OnRawMouseButtonEvent?.Invoke(this, e3);
				if (e3.Handled)
				{
					return 1; // 手势等已接管该按键：拦截原生事件
				}
			}
			string text2 = ConfigManager.CurrentConfig?.Trigger?.MouseButton ?? ConfigManager.CurrentConfig?.TriggerButton ?? "RightButton";
			bool num2 = flag && text == text2;
			bool flag3 = flag2 && text == text2;
			if (num2)
			{
				if (Interlocked.Exchange(ref _ignoreNextButtonDown, 0) != 0)
				{
					return CallNextHookEx(_hookId, nCode, wParam, lParam);
				}
				MouseEventArgs e4 = new MouseEventArgs(mSLLHOOKSTRUCT.pt.x, mSLLHOOKSTRUCT.pt.y);
				OnTriggerButtonDown?.Invoke(this, e4);
				if (e4.Handled)
				{
					return 1;
				}
			}
			else if (flag3)
			{
				if (Interlocked.Exchange(ref _ignoreNextButtonUp, 0) != 0)
				{
					return CallNextHookEx(_hookId, nCode, wParam, lParam);
				}
				MouseEventArgs e5 = new MouseEventArgs(mSLLHOOKSTRUCT.pt.x, mSLLHOOKSTRUCT.pt.y);
				OnTriggerButtonUp?.Invoke(this, e5);
				if (e5.Handled)
				{
					return 1;
				}
			}
		}
		return CallNextHookEx(_hookId, nCode, wParam, lParam);
	}

	public void ReplayTriggerClick(string? triggerButton = null)
	{
		string text = triggerButton ?? ConfigManager.CurrentConfig?.TriggerButton ?? "RightButton";
		Interlocked.Exchange(ref _ignoreNextButtonDown, 1);
		Interlocked.Exchange(ref _ignoreNextButtonUp, 1);
		switch (text)
		{
		case "MiddleButton":
			mouse_event(32u, 0u, 0u, 0u, (uint)StarPieExtraInfo);
			mouse_event(64u, 0u, 0u, 0u, (uint)StarPieExtraInfo);
			break;
		case "XButton1":
			mouse_event(128u, 0u, 0u, 1u, (uint)StarPieExtraInfo);
			mouse_event(256u, 0u, 0u, 1u, (uint)StarPieExtraInfo);
			break;
		case "XButton2":
			mouse_event(128u, 0u, 0u, 2u, (uint)StarPieExtraInfo);
			mouse_event(256u, 0u, 0u, 2u, (uint)StarPieExtraInfo);
			break;
		default:
			mouse_event(8u, 0u, 0u, 0u, (uint)StarPieExtraInfo);
			mouse_event(16u, 0u, 0u, 0u, (uint)StarPieExtraInfo);
			break;
		}
	}

	public void ReplayRightClick()
	{
		ReplayTriggerClick("RightButton");
	}
}

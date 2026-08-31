using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Threading;
using System.Windows.Input;

namespace WinPieGestures;

public class KeyboardHook : IDisposable
{
	private struct KBDLLHOOKSTRUCT
	{
		public uint vkCode;

		public uint scanCode;

		public uint flags;

		public uint time;

		public nint dwExtraInfo;
	}

	private delegate nint LowLevelKeyboardProc(int nCode, nint wParam, nint lParam);

	private const int WH_KEYBOARD_LL = 13;

	private const int WM_KEYDOWN = 256;

	private const int WM_KEYUP = 257;

	private const int WM_SYSKEYDOWN = 260;

	private const int WM_SYSKEYUP = 261;

	private const uint KEYEVENTF_EXTENDEDKEY = 1u;

	private const uint KEYEVENTF_KEYUP = 2u;

	private LowLevelKeyboardProc _proc;

	private nint _hookId = IntPtr.Zero;

	private bool _ignoreNextKeyDown;

	private bool _ignoreNextKeyUp;

	private System.Threading.Timer? _healthCheckTimer;

	private int _hookEventsCountSinceLastCheck;

	public bool IsPaused { get; set; }

	public event EventHandler<GlobalKeyEventArgs>? OnKeyDown;

	public event EventHandler<GlobalKeyEventArgs>? OnKeyUp;

	public event EventHandler<GlobalKeyEventArgs>? OnRawKeyEvent;

	[DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
	private static extern nint SetWindowsHookEx(int idHook, LowLevelKeyboardProc lpfn, nint hMod, uint dwThreadId);

	[DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
	[return: MarshalAs(UnmanagedType.Bool)]
	private static extern bool UnhookWindowsHookEx(nint hhk);

	[DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
	private static extern nint CallNextHookEx(nint hhk, int nCode, nint wParam, nint lParam);

	[DllImport("kernel32.dll", CharSet = CharSet.Auto, SetLastError = true)]
	private static extern nint GetModuleHandle(string lpModuleName);

	[DllImport("user32.dll")]
	private static extern void keybd_event(byte bVk, byte bScan, uint dwFlags, nuint dwExtraInfo);

	[DllImport("user32.dll")]
	private static extern short GetKeyState(int nVirtKey);

	public KeyboardHook()
	{
		_proc = HookCallback;
	}

	public void Start()
	{
		if (_hookId == IntPtr.Zero)
		{
			_hookId = SetHook(_proc);
			if (_hookId == IntPtr.Zero)
			{
				throw new Exception("Failed to set low-level keyboard hook.");
			}
			_hookEventsCountSinceLastCheck = 0;
			_healthCheckTimer = new System.Threading.Timer(CheckHookHealth, null, 5000, 5000);
		}
	}

	public void Stop()
	{
		if (_healthCheckTimer != null)
		{
			_healthCheckTimer.Dispose();
			_healthCheckTimer = null;
		}
		if (_hookId != IntPtr.Zero)
		{
			UnhookWindowsHookEx(_hookId);
			_hookId = IntPtr.Zero;
		}
	}

	private void CheckHookHealth(object? state)
	{
		Interlocked.Exchange(ref _hookEventsCountSinceLastCheck, 0);
	}

	private nint SetHook(LowLevelKeyboardProc proc)
	{
		using Process process = Process.GetCurrentProcess();
		using ProcessModule processModule = process.MainModule;
		if (processModule == null)
		{
			throw new InvalidOperationException("MainModule is null.");
		}
		return SetWindowsHookEx(13, proc, GetModuleHandle(processModule.ModuleName), 0u);
	}

	public static ModifierKeys GetCurrentModifiers()
	{
		//IL_0001: Unknown result type (might be due to invalid IL or missing references)
		//IL_0011: Unknown result type (might be due to invalid IL or missing references)
		//IL_0013: Unknown result type (might be due to invalid IL or missing references)
		//IL_0014: Unknown result type (might be due to invalid IL or missing references)
		//IL_0024: Unknown result type (might be due to invalid IL or missing references)
		//IL_0026: Unknown result type (might be due to invalid IL or missing references)
		//IL_0027: Unknown result type (might be due to invalid IL or missing references)
		//IL_0037: Unknown result type (might be due to invalid IL or missing references)
		//IL_0039: Unknown result type (might be due to invalid IL or missing references)
		//IL_003a: Unknown result type (might be due to invalid IL or missing references)
		//IL_0059: Unknown result type (might be due to invalid IL or missing references)
		//IL_005b: Unknown result type (might be due to invalid IL or missing references)
		//IL_005c: Unknown result type (might be due to invalid IL or missing references)
		//IL_005d: Unknown result type (might be due to invalid IL or missing references)
		ModifierKeys val = (ModifierKeys)0;
		if ((GetKeyState(17) & 0x8000) != 0)
		{
			val = (ModifierKeys)((int)val | 2);
		}
		if ((GetKeyState(16) & 0x8000) != 0)
		{
			val = (ModifierKeys)((int)val | 4);
		}
		if ((GetKeyState(18) & 0x8000) != 0)
		{
			val = (ModifierKeys)((int)val | 1);
		}
		if ((GetKeyState(91) & 0x8000) != 0 || (GetKeyState(92) & 0x8000) != 0)
		{
			val = (ModifierKeys)((int)val | 8);
		}
		return val;
	}

	private nint HookCallback(int nCode, nint wParam, nint lParam)
	{
		//IL_0039: Unknown result type (might be due to invalid IL or missing references)
		//IL_003e: Unknown result type (might be due to invalid IL or missing references)
		//IL_0040: Unknown result type (might be due to invalid IL or missing references)
		//IL_0089: Unknown result type (might be due to invalid IL or missing references)
		//IL_00e0: Unknown result type (might be due to invalid IL or missing references)
		Interlocked.Increment(ref _hookEventsCountSinceLastCheck);
		if (IsPaused)
		{
			return CallNextHookEx(_hookId, nCode, wParam, lParam);
		}
		if (nCode >= 0)
		{
			int num = (int)wParam;
			uint vkCode = Marshal.PtrToStructure<KBDLLHOOKSTRUCT>(lParam).vkCode;
			ModifierKeys currentModifiers = GetCurrentModifiers();
			GlobalKeyEventArgs e = new GlobalKeyEventArgs(vkCode, currentModifiers);
			OnRawKeyEvent?.Invoke(this, e);
			switch (num)
			{
			case 256:
			case 260:
			{
				if (_ignoreNextKeyDown)
				{
					_ignoreNextKeyDown = false;
					return CallNextHookEx(_hookId, nCode, wParam, lParam);
				}
				GlobalKeyEventArgs e3 = new GlobalKeyEventArgs(vkCode, currentModifiers);
				OnKeyDown?.Invoke(this, e3);
				if (e3.Handled)
				{
					return 1;
				}
				break;
			}
			case 257:
			case 261:
			{
				if (_ignoreNextKeyUp)
				{
					_ignoreNextKeyUp = false;
					return CallNextHookEx(_hookId, nCode, wParam, lParam);
				}
				GlobalKeyEventArgs e2 = new GlobalKeyEventArgs(vkCode, currentModifiers);
				OnKeyUp?.Invoke(this, e2);
				if (e2.Handled)
				{
					return 1;
				}
				break;
			}
			}
		}
		return CallNextHookEx(_hookId, nCode, wParam, lParam);
	}

	public void ReplayKeyPress(uint vkCode)
	{
		if (vkCode != 0)
		{
			_ignoreNextKeyDown = true;
			_ignoreNextKeyUp = true;
			keybd_event((byte)vkCode, 0, 0u, UIntPtr.Zero);
			keybd_event((byte)vkCode, 0, 2u, UIntPtr.Zero);
		}
	}

	public void Dispose()
	{
		Stop();
	}
}

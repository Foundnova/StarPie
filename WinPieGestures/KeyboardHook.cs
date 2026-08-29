using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Windows.Input;

namespace WinPieGestures
{
    public class GlobalKeyEventArgs : EventArgs
    {
        public uint VkCode { get; }
        public Key Key { get; }
        public ModifierKeys Modifiers { get; }
        public bool Handled { get; set; }

        public GlobalKeyEventArgs(uint vkCode, ModifierKeys modifiers)
        {
            VkCode = vkCode;
            Key = KeyInterop.KeyFromVirtualKey((int)vkCode);
            Modifiers = modifiers;
            Handled = false;
        }
    }

    public class KeyboardHook : IDisposable
    {
        private const int WH_KEYBOARD_LL = 13;
        private const int WM_KEYDOWN = 0x0100;
        private const int WM_KEYUP = 0x0101;
        private const int WM_SYSKEYDOWN = 0x0104;
        private const int WM_SYSKEYUP = 0x0105;

        [StructLayout(LayoutKind.Sequential)]
        private struct KBDLLHOOKSTRUCT
        {
            public uint vkCode;
            public uint scanCode;
            public uint flags;
            public uint time;
            public IntPtr dwExtraInfo;
        }

        private delegate IntPtr LowLevelKeyboardProc(int nCode, IntPtr wParam, IntPtr lParam);

        [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        private static extern IntPtr SetWindowsHookEx(int idHook, LowLevelKeyboardProc lpfn, IntPtr hMod, uint dwThreadId);

        [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool UnhookWindowsHookEx(IntPtr hhk);

        [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        private static extern IntPtr CallNextHookEx(IntPtr hhk, int nCode, IntPtr wParam, IntPtr lParam);

        [DllImport("kernel32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        private static extern IntPtr GetModuleHandle(string lpModuleName);

        [DllImport("user32.dll")]
        private static extern void keybd_event(byte bVk, byte bScan, uint dwFlags, UIntPtr dwExtraInfo);

        private const uint KEYEVENTF_EXTENDEDKEY = 0x0001;
        private const uint KEYEVENTF_KEYUP = 0x0002;

        public bool IsPaused { get; set; } = false;

        public event EventHandler<GlobalKeyEventArgs>? OnKeyDown;
        public event EventHandler<GlobalKeyEventArgs>? OnKeyUp;
        public event EventHandler<GlobalKeyEventArgs>? OnRawKeyEvent;

        private LowLevelKeyboardProc _proc;
        private IntPtr _hookId = IntPtr.Zero;

        private bool _ignoreNextKeyDown = false;
        private bool _ignoreNextKeyUp = false;

        private System.Threading.Timer? _healthCheckTimer;
        private int _hookEventsCountSinceLastCheck = 0;

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
            System.Threading.Interlocked.Exchange(ref _hookEventsCountSinceLastCheck, 0);
        }

        private IntPtr SetHook(LowLevelKeyboardProc proc)
        {
            using (Process curProcess = Process.GetCurrentProcess())
            using (ProcessModule? curModule = curProcess.MainModule)
            {
                if (curModule == null) throw new InvalidOperationException("MainModule is null.");
                return SetWindowsHookEx(WH_KEYBOARD_LL, proc, GetModuleHandle(curModule.ModuleName), 0);
            }
        }

        private IntPtr HookCallback(int nCode, IntPtr wParam, IntPtr lParam)
        {
            System.Threading.Interlocked.Increment(ref _hookEventsCountSinceLastCheck);

            if (IsPaused)
            {
                return CallNextHookEx(_hookId, nCode, wParam, lParam);
            }

            if (nCode >= 0)
            {
                int message = (int)wParam;
                KBDLLHOOKSTRUCT hookStruct = Marshal.PtrToStructure<KBDLLHOOKSTRUCT>(lParam);
                uint vk = hookStruct.vkCode;

                ModifierKeys modifiers = ModifierKeys.None;
                if ((GetKeyState(0x11) & 0x8000) != 0) modifiers |= ModifierKeys.Control; // VK_CONTROL
                if ((GetKeyState(0x10) & 0x8000) != 0) modifiers |= ModifierKeys.Shift;   // VK_SHIFT
                if ((GetKeyState(0x12) & 0x8000) != 0) modifiers |= ModifierKeys.Alt;     // VK_MENU
                if ((GetKeyState(0x5B) & 0x8000) != 0 || (GetKeyState(0x5C) & 0x8000) != 0) modifiers |= ModifierKeys.Windows;

                var rawArgs = new GlobalKeyEventArgs(vk, modifiers);
                OnRawKeyEvent?.Invoke(this, rawArgs);

                if (message == WM_KEYDOWN || message == WM_SYSKEYDOWN)
                {
                    if (_ignoreNextKeyDown)
                    {
                        _ignoreNextKeyDown = false;
                        return CallNextHookEx(_hookId, nCode, wParam, lParam);
                    }

                    var args = new GlobalKeyEventArgs(vk, modifiers);
                    OnKeyDown?.Invoke(this, args);
                    if (args.Handled)
                    {
                        return (IntPtr)1; // Suppress
                    }
                }
                else if (message == WM_KEYUP || message == WM_SYSKEYUP)
                {
                    if (_ignoreNextKeyUp)
                    {
                        _ignoreNextKeyUp = false;
                        return CallNextHookEx(_hookId, nCode, wParam, lParam);
                    }

                    var args = new GlobalKeyEventArgs(vk, modifiers);
                    OnKeyUp?.Invoke(this, args);
                    if (args.Handled)
                    {
                        return (IntPtr)1; // Suppress
                    }
                }
            }

            return CallNextHookEx(_hookId, nCode, wParam, lParam);
        }

        [DllImport("user32.dll")]
        private static extern short GetKeyState(int nVirtKey);

        public void ReplayKeyPress(uint vkCode)
        {
            _ignoreNextKeyDown = true;
            _ignoreNextKeyUp = true;
            keybd_event((byte)vkCode, 0, 0, UIntPtr.Zero);
            keybd_event((byte)vkCode, 0, KEYEVENTF_KEYUP, UIntPtr.Zero);
        }

        public void Dispose()
        {
            Stop();
        }
    }
}

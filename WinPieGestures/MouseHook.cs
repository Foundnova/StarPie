using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Windows;
using Point = System.Windows.Point;

namespace WinPieGestures
{
    public class MouseEventArgs : EventArgs
    {
        public Point Position { get; }
        public bool Handled { get; set; }

        public MouseEventArgs(double x, double y)
        {
            Position = new Point(x, y);
            Handled = false;
        }
    }

    public class MouseHook
    {
        private const int WH_MOUSE_LL = 14;
        private const int WM_MOUSEMOVE = 0x0200;
        private const int WM_RBUTTONDOWN = 0x0204;
        private const int WM_RBUTTONUP = 0x0205;
        private const int WM_MBUTTONDOWN = 0x0207;
        private const int WM_MBUTTONUP = 0x0208;
        private const int WM_XBUTTONDOWN = 0x020B;
        private const int WM_XBUTTONUP = 0x020C;

        [StructLayout(LayoutKind.Sequential)]
        private struct POINT
        {
            public int x;
            public int y;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct MSLLHOOKSTRUCT
        {
            public POINT pt;
            public uint mouseData;
            public uint flags;
            public uint time;
            public IntPtr dwExtraInfo;
        }

        private delegate IntPtr LowLevelMouseProc(int nCode, IntPtr wParam, IntPtr lParam);

        [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        private static extern IntPtr SetWindowsHookEx(int idHook, LowLevelMouseProc lpfn, IntPtr hMod, uint dwThreadId);

        [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool UnhookWindowsHookEx(IntPtr hhk);

        [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        private static extern IntPtr CallNextHookEx(IntPtr hhk, int nCode, IntPtr wParam, IntPtr lParam);

        [DllImport("kernel32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        private static extern IntPtr GetModuleHandle(string lpModuleName);

        [DllImport("user32.dll")]
        private static extern void mouse_event(uint dwFlags, uint dx, uint dy, uint dwData, uint dwExtraInfo);

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool GetCursorPos(out POINT lpPoint);

        private const uint MOUSEEVENTF_RIGHTDOWN = 0x0008;
        private const uint MOUSEEVENTF_RIGHTUP = 0x0010;
        private const uint MOUSEEVENTF_MIDDLEDOWN = 0x0020;
        private const uint MOUSEEVENTF_MIDDLEUP = 0x0040;
        private const uint MOUSEEVENTF_XDOWN = 0x0080;
        private const uint MOUSEEVENTF_XUP = 0x0100;
        private const uint XBUTTON1 = 0x0001;
        private const uint XBUTTON2 = 0x0002;

        public bool IsPaused { get; set; } = false;

        public event EventHandler<MouseEventArgs>? OnTriggerButtonDown;
        public event EventHandler<MouseEventArgs>? OnTriggerButtonUp;
        public event EventHandler<MouseEventArgs>? OnMouseMove;
        public event EventHandler<MouseEventArgs>? OnRawMouseEvent;

        // Legacy compatibility events
        public event EventHandler<MouseEventArgs>? OnRightButtonDown
        {
            add => OnTriggerButtonDown += value;
            remove => OnTriggerButtonDown -= value;
        }
        public event EventHandler<MouseEventArgs>? OnRightButtonUp
        {
            add => OnTriggerButtonUp += value;
            remove => OnTriggerButtonUp -= value;
        }

        private LowLevelMouseProc _proc;
        private IntPtr _hookId = IntPtr.Zero;

        // Flags to prevent recursive hook interception when we replay click events
        private bool _ignoreNextButtonDown = false;
        private bool _ignoreNextButtonUp = false;

        // Hook stability and health check variables
        private System.Threading.Timer? _healthCheckTimer;
        private POINT _lastSystemCursorPos;
        private int _hookEventsCountSinceLastCheck = 0;

        public MouseHook()
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
                    throw new Exception("Failed to set low-level mouse hook.");
                }

                // Initialize health check
                _hookEventsCountSinceLastCheck = 0;
                GetCursorPos(out _lastSystemCursorPos);
                _healthCheckTimer = new System.Threading.Timer(CheckHookHealth, null, 3000, 3000);
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
            if (_hookId == IntPtr.Zero) return;

            POINT currentPos;
            if (GetCursorPos(out currentPos))
            {
                bool mouseMoved = currentPos.x != _lastSystemCursorPos.x || currentPos.y != _lastSystemCursorPos.y;
                _lastSystemCursorPos = currentPos;

                if (mouseMoved)
                {
                    // If system mouse moved, but we received 0 hook events, hook is likely dead!
                    if (System.Threading.Interlocked.Exchange(ref _hookEventsCountSinceLastCheck, 0) == 0)
                    {
                        System.Windows.Application.Current?.Dispatcher?.BeginInvoke(new Action(() =>
                        {
                            Debug.WriteLine("Mouse hook health check failed. Re-registering hook...");
                            try
                            {
                                Stop();
                                Start();
                            }
                            catch (Exception ex)
                            {
                                Debug.WriteLine($"Failed to re-register hook: {ex.Message}");
                            }
                        }));
                    }
                }
                else
                {
                    // Reset count if mouse did not move to avoid false positive
                    System.Threading.Interlocked.Exchange(ref _hookEventsCountSinceLastCheck, 0);
                }
            }
        }

        private IntPtr SetHook(LowLevelMouseProc proc)
        {
            using (Process curProcess = Process.GetCurrentProcess())
            using (ProcessModule? curModule = curProcess.MainModule)
            {
                if (curModule == null) throw new InvalidOperationException("MainModule is null.");
                return SetWindowsHookEx(WH_MOUSE_LL, proc, GetModuleHandle(curModule.ModuleName), 0);
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
                MSLLHOOKSTRUCT hookStruct = Marshal.PtrToStructure<MSLLHOOKSTRUCT>(lParam);

                if (message == WM_MOUSEMOVE)
                {
                    var args = new MouseEventArgs(hookStruct.pt.x, hookStruct.pt.y);
                    OnMouseMove?.Invoke(this, args);
                    if (args.Handled)
                    {
                        return (IntPtr)1; // Block event
                    }
                    return CallNextHookEx(_hookId, nCode, wParam, lParam);
                }

                var rawArgs = new MouseEventArgs(hookStruct.pt.x, hookStruct.pt.y);
                OnRawMouseEvent?.Invoke(this, rawArgs);

                string trigger = ConfigManager.CurrentConfig?.TriggerButton ?? "RightButton";
                bool isTargetDown = false;
                bool isTargetUp = false;

                if (trigger == "MiddleButton")
                {
                    isTargetDown = (message == WM_MBUTTONDOWN);
                    isTargetUp = (message == WM_MBUTTONUP);
                }
                else if (trigger == "XButton1")
                {
                    uint xBtn = (hookStruct.mouseData >> 16) & 0xFFFF;
                    isTargetDown = (message == WM_XBUTTONDOWN && xBtn == 1);
                    isTargetUp = (message == WM_XBUTTONUP && xBtn == 1);
                }
                else if (trigger == "XButton2")
                {
                    uint xBtn = (hookStruct.mouseData >> 16) & 0xFFFF;
                    isTargetDown = (message == WM_XBUTTONDOWN && xBtn == 2);
                    isTargetUp = (message == WM_XBUTTONUP && xBtn == 2);
                }
                else // Default RightButton
                {
                    isTargetDown = (message == WM_RBUTTONDOWN);
                    isTargetUp = (message == WM_RBUTTONUP);
                }

                if (isTargetDown)
                {
                    if (_ignoreNextButtonDown)
                    {
                        _ignoreNextButtonDown = false;
                        return CallNextHookEx(_hookId, nCode, wParam, lParam);
                    }

                    var args = new MouseEventArgs(hookStruct.pt.x, hookStruct.pt.y);
                    OnTriggerButtonDown?.Invoke(this, args);
                    if (args.Handled)
                    {
                        return (IntPtr)1; // Block the event from propagating
                    }
                }
                else if (isTargetUp)
                {
                    if (_ignoreNextButtonUp)
                    {
                        _ignoreNextButtonUp = false;
                        return CallNextHookEx(_hookId, nCode, wParam, lParam);
                    }

                    var args = new MouseEventArgs(hookStruct.pt.x, hookStruct.pt.y);
                    OnTriggerButtonUp?.Invoke(this, args);
                    if (args.Handled)
                    {
                        return (IntPtr)1; // Block the event from propagating
                    }
                }
            }

            return CallNextHookEx(_hookId, nCode, wParam, lParam);
        }

        /// <summary>
        /// Replays a mouse click for the designated trigger button at the current position.
        /// Temporarily ignores our own hook to avoid infinite loop.
        /// </summary>
        public void ReplayTriggerClick(string? triggerButton = null)
        {
            string button = triggerButton ?? ConfigManager.CurrentConfig?.TriggerButton ?? "RightButton";
            _ignoreNextButtonDown = true;
            _ignoreNextButtonUp = true;

            if (button == "MiddleButton")
            {
                mouse_event(MOUSEEVENTF_MIDDLEDOWN, 0, 0, 0, 0);
                mouse_event(MOUSEEVENTF_MIDDLEUP, 0, 0, 0, 0);
            }
            else if (button == "XButton1")
            {
                mouse_event(MOUSEEVENTF_XDOWN, 0, 0, XBUTTON1, 0);
                mouse_event(MOUSEEVENTF_XUP, 0, 0, XBUTTON1, 0);
            }
            else if (button == "XButton2")
            {
                mouse_event(MOUSEEVENTF_XDOWN, 0, 0, XBUTTON2, 0);
                mouse_event(MOUSEEVENTF_XUP, 0, 0, XBUTTON2, 0);
            }
            else // RightButton
            {
                mouse_event(MOUSEEVENTF_RIGHTDOWN, 0, 0, 0, 0);
                mouse_event(MOUSEEVENTF_RIGHTUP, 0, 0, 0, 0);
            }
        }

        public void ReplayRightClick()
        {
            ReplayTriggerClick("RightButton");
        }
    }
}

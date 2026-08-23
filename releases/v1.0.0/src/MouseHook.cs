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

        private const uint MOUSEEVENTF_RIGHTDOWN = 0x0008;
        private const uint MOUSEEVENTF_RIGHTUP = 0x0010;

        public event EventHandler<MouseEventArgs> OnRightButtonDown;
        public event EventHandler<MouseEventArgs> OnRightButtonUp;
        public event EventHandler<MouseEventArgs> OnMouseMove;

        private LowLevelMouseProc _proc;
        private IntPtr _hookId = IntPtr.Zero;

        // Flags to prevent recursive hook interception when we replay right click events
        private bool _ignoreNextRButtonDown = false;
        private bool _ignoreNextRButtonUp = false;

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
            }
        }

        public void Stop()
        {
            if (_hookId != IntPtr.Zero)
            {
                UnhookWindowsHookEx(_hookId);
                _hookId = IntPtr.Zero;
            }
        }

        private IntPtr SetHook(LowLevelMouseProc proc)
        {
            using (Process curProcess = Process.GetCurrentProcess())
            using (ProcessModule curModule = curProcess.MainModule)
            {
                return SetWindowsHookEx(WH_MOUSE_LL, proc, GetModuleHandle(curModule.ModuleName), 0);
            }
        }

        private IntPtr HookCallback(int nCode, IntPtr wParam, IntPtr lParam)
        {
            if (nCode >= 0)
            {
                int message = (int)wParam;
                MSLLHOOKSTRUCT hookStruct = Marshal.PtrToStructure<MSLLHOOKSTRUCT>(lParam);

                if (message == WM_RBUTTONDOWN)
                {
                    if (_ignoreNextRButtonDown)
                    {
                        _ignoreNextRButtonDown = false;
                        return CallNextHookEx(_hookId, nCode, wParam, lParam);
                    }

                    var args = new MouseEventArgs(hookStruct.pt.x, hookStruct.pt.y);
                    OnRightButtonDown?.Invoke(this, args);
                    if (args.Handled)
                    {
                        return (IntPtr)1; // Block the event from propagating
                    }
                }
                else if (message == WM_RBUTTONUP)
                {
                    if (_ignoreNextRButtonUp)
                    {
                        _ignoreNextRButtonUp = false;
                        return CallNextHookEx(_hookId, nCode, wParam, lParam);
                    }

                    var args = new MouseEventArgs(hookStruct.pt.x, hookStruct.pt.y);
                    OnRightButtonUp?.Invoke(this, args);
                    if (args.Handled)
                    {
                        return (IntPtr)1; // Block the event from propagating
                    }
                }
                else if (message == WM_MOUSEMOVE)
                {
                    var args = new MouseEventArgs(hookStruct.pt.x, hookStruct.pt.y);
                    OnMouseMove?.Invoke(this, args);
                    if (args.Handled)
                    {
                        return (IntPtr)1; // Block the event from propagating
                    }
                }
            }

            return CallNextHookEx(_hookId, nCode, wParam, lParam);
        }

        /// <summary>
        /// Replays a right mouse click at the current position.
        /// Temporarily ignores our own hook to avoid infinite loop.
        /// </summary>
        public void ReplayRightClick()
        {
            _ignoreNextRButtonDown = true;
            _ignoreNextRButtonUp = true;
            mouse_event(MOUSEEVENTF_RIGHTDOWN, 0, 0, 0, 0);
            mouse_event(MOUSEEVENTF_RIGHTUP, 0, 0, 0, 0);
        }
    }
}

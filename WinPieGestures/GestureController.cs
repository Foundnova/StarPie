using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Input;
using System.Windows.Threading;
using Point = System.Windows.Point;
using Application = System.Windows.Application;

namespace WinPieGestures
{
    public class GestureController
    {
        private readonly MouseHook _mouseHook;
        private readonly KeyboardHook? _keyboardHook;
        private RadialWindow? _radialWindow;

        private Point _startPoint;
        private bool _isWaitingForThreshold = false;
        private bool _isGestureActive = false;
        private WheelProfile? _activeProfile;
        private int _selectedSectorIndex = -1;
        private bool _lastEscapedState = false;

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool GetCursorPos(out POINT lpPoint);

        [StructLayout(LayoutKind.Sequential)]
        private struct POINT { public int x; public int y; }

        public GestureController(MouseHook mouseHook, KeyboardHook? keyboardHook = null)
        {
            _mouseHook = mouseHook;
            _keyboardHook = keyboardHook;

            _mouseHook.OnTriggerButtonDown += Hook_OnTriggerButtonDown;
            _mouseHook.OnTriggerButtonUp += Hook_OnTriggerButtonUp;
            _mouseHook.OnMouseMove += Hook_OnMouseMove;

            if (_keyboardHook != null)
            {
                _keyboardHook.OnKeyDown += KeyboardHook_OnKeyDown;
                _keyboardHook.OnKeyUp += KeyboardHook_OnKeyUp;
            }
        }

        private bool CheckIsIsolated(out string processName)
        {
            processName = ActiveWindowHelper.GetActiveWindowProcessName();

            bool isBlacklisted = false;
            if (ConfigManager.CurrentConfig.BlacklistedProcesses != null)
            {
                string normProc = processName.Trim().ToLower();
                foreach (var blacklisted in ConfigManager.CurrentConfig.BlacklistedProcesses)
                {
                    if (blacklisted.Trim().ToLower() == normProc)
                    {
                        isBlacklisted = true;
                        break;
                    }
                }
            }

            var trigger = ConfigManager.CurrentConfig.Trigger ?? new TriggerConfig();
            var curMods = KeyboardHook.GetCurrentModifiers();

            bool isCtrlPressed = ConfigManager.CurrentConfig.DisableOnCtrl && !trigger.RequireCtrl &&
                                 (curMods & ModifierKeys.Control) != 0;
            bool isShiftPressed = ConfigManager.CurrentConfig.DisableOnShift && !trigger.RequireShift &&
                                  (curMods & ModifierKeys.Shift) != 0;
            bool isAltPressed = ConfigManager.CurrentConfig.DisableOnAlt && !trigger.RequireAlt &&
                                (curMods & ModifierKeys.Alt) != 0;
            bool isModifierPressed = isCtrlPressed || isShiftPressed || isAltPressed;

            bool isFullScreen = ConfigManager.CurrentConfig.DisableOnFullScreen && FullScreenHelper.IsActiveWindowFullScreen();

            return isBlacklisted || isModifierPressed || isFullScreen;
        }

        private bool IsModifierKey(uint vkCode)
        {
            return vkCode == 0x11 || vkCode == 0xA2 || vkCode == 0xA3 || // Ctrl
                   vkCode == 0x12 || vkCode == 0xA4 || vkCode == 0xA5 || // Alt
                   vkCode == 0x10 || vkCode == 0xA0 || vkCode == 0xA1 || // Shift
                   vkCode == 0x5B || vkCode == 0x5C;                     // Win
        }

        private void Hook_OnTriggerButtonDown(object? sender, MouseEventArgs e)
        {
            var trigger = ConfigManager.CurrentConfig.Trigger ?? new TriggerConfig();
            if (trigger.TriggerType != "Mouse") return;

            var curMods = KeyboardHook.GetCurrentModifiers();
            if (trigger.RequireCtrl && (curMods & ModifierKeys.Control) == 0) return;
            if (trigger.RequireShift && (curMods & ModifierKeys.Shift) == 0) return;
            if (trigger.RequireAlt && (curMods & ModifierKeys.Alt) == 0) return;
            if (trigger.RequireWin && (curMods & ModifierKeys.Windows) == 0) return;

            if (CheckIsIsolated(out string processName))
            {
                _isWaitingForThreshold = false;
                _isGestureActive = false;
                e.Handled = false;
                return;
            }

            _startPoint = e.Position;
            _isWaitingForThreshold = true;
            _isGestureActive = false;
            _selectedSectorIndex = -1;
            _lastEscapedState = false;

            e.Handled = true; // Block initial mouse down for gesture assessment
            Debug.WriteLine($"TriggerMouseDown pinned at {_startPoint.X}, {_startPoint.Y}. Waiting for threshold.");
        }

        private void Hook_OnTriggerButtonUp(object? sender, MouseEventArgs e)
        {
            var trigger = ConfigManager.CurrentConfig.Trigger ?? new TriggerConfig();
            if (trigger.TriggerType != "Mouse") return;

            if (_isWaitingForThreshold)
            {
                _isWaitingForThreshold = false;
                Debug.WriteLine("Normal click detected. Replaying trigger click.");

                string btn = trigger.MouseButton ?? ConfigManager.CurrentConfig.TriggerButton ?? "RightButton";
                Application.Current.Dispatcher.BeginInvoke(new Action(() =>
                {
                    _mouseHook.ReplayTriggerClick(btn);
                }), DispatcherPriority.Input);
                e.Handled = true;
            }
            else if (_isGestureActive)
            {
                _isGestureActive = false;
                int finalSector = _selectedSectorIndex;
                var finalProfile = _activeProfile;

                Debug.WriteLine($"Gesture completed. Selected sector: {finalSector}");

                Application.Current.Dispatcher.BeginInvoke(new Action(() =>
                {
                    HideRadialUI();

                    if (finalProfile != null && finalSector >= 0 && finalSector < finalProfile.Actions.Count)
                    {
                        var action = finalProfile.Actions[finalSector];
                        if (action != null && !string.IsNullOrEmpty(action.Type))
                        {
                            Debug.WriteLine($"Executing action: {action.Name} ({action.Type}: {action.Parameter})");
                            ActionExecutor.Execute(action);
                        }
                    }
                }), DispatcherPriority.Input);

                e.Handled = true;
            }
        }

        private void KeyboardHook_OnKeyDown(object? sender, GlobalKeyEventArgs e)
        {
            var trigger = ConfigManager.CurrentConfig.Trigger ?? new TriggerConfig();
            if (trigger.TriggerType != "Keyboard") return;

            var curMods = e.Modifiers;

            // Check modifier requirements
            if (trigger.RequireCtrl && (curMods & ModifierKeys.Control) == 0) return;
            if (trigger.RequireShift && (curMods & ModifierKeys.Shift) == 0) return;
            if (trigger.RequireAlt && (curMods & ModifierKeys.Alt) == 0) return;
            if (trigger.RequireWin && (curMods & ModifierKeys.Windows) == 0) return;

            // Check key match
            if (trigger.VkCode != 0 && !IsModifierKey(trigger.VkCode))
            {
                if (e.VkCode != trigger.VkCode) return;
            }

            // Pinning anchor: ignore typematic auto-repeat
            if (_isWaitingForThreshold || _isGestureActive)
            {
                e.Handled = true;
                return;
            }

            if (CheckIsIsolated(out string processName))
            {
                _isWaitingForThreshold = false;
                _isGestureActive = false;
                e.Handled = false;
                return;
            }

            POINT pt;
            GetCursorPos(out pt);
            _startPoint = new Point(pt.x, pt.y);
            _isWaitingForThreshold = true;
            _isGestureActive = false;
            _selectedSectorIndex = -1;
            _lastEscapedState = false;

            e.Handled = true;
            Debug.WriteLine($"TriggerKeyDown ({e.VkCode}) pinned at {_startPoint.X}, {_startPoint.Y}. Waiting for threshold.");
        }

        private void KeyboardHook_OnKeyUp(object? sender, GlobalKeyEventArgs e)
        {
            var trigger = ConfigManager.CurrentConfig.Trigger ?? new TriggerConfig();
            if (trigger.TriggerType != "Keyboard") return;

            bool isOurKey = false;
            if (trigger.VkCode != 0 && e.VkCode == trigger.VkCode) isOurKey = true;
            if (trigger.RequireCtrl && (e.VkCode == 0x11 || e.VkCode == 0xA2 || e.VkCode == 0xA3)) isOurKey = true;
            if (trigger.RequireShift && (e.VkCode == 0x10 || e.VkCode == 0xA0 || e.VkCode == 0xA1)) isOurKey = true;
            if (trigger.RequireAlt && (e.VkCode == 0x12 || e.VkCode == 0xA4 || e.VkCode == 0xA5)) isOurKey = true;
            if (trigger.RequireWin && (e.VkCode == 0x5B || e.VkCode == 0x5C)) isOurKey = true;

            if (!isOurKey) return;

            if (_isWaitingForThreshold)
            {
                _isWaitingForThreshold = false;
                Debug.WriteLine("Normal key tap detected. Replaying key press.");

                uint vk = trigger.VkCode != 0 ? trigger.VkCode : e.VkCode;
                Application.Current.Dispatcher.BeginInvoke(new Action(() =>
                {
                    _keyboardHook?.ReplayKeyPress(vk);
                }), DispatcherPriority.Input);
                e.Handled = true;
            }
            else if (_isGestureActive)
            {
                _isGestureActive = false;
                int finalSector = _selectedSectorIndex;
                var finalProfile = _activeProfile;

                Debug.WriteLine($"Keyboard gesture completed. Selected sector: {finalSector}");

                Application.Current.Dispatcher.BeginInvoke(new Action(() =>
                {
                    HideRadialUI();

                    if (finalProfile != null && finalSector >= 0 && finalSector < finalProfile.Actions.Count)
                    {
                        var action = finalProfile.Actions[finalSector];
                        if (action != null && !string.IsNullOrEmpty(action.Type))
                        {
                            Debug.WriteLine($"Executing action: {action.Name} ({action.Type}: {action.Parameter})");
                            ActionExecutor.Execute(action);
                        }
                    }
                }), DispatcherPriority.Input);

                e.Handled = true;
            }
        }

        private void Hook_OnMouseMove(object? sender, MouseEventArgs e)
        {
            if (_isWaitingForThreshold)
            {
                double dx = e.Position.X - _startPoint.X;
                double dy = e.Position.Y - _startPoint.Y;
                double distanceSq = dx * dx + dy * dy;
                double threshold = ConfigManager.CurrentConfig.DragThreshold;

                if (distanceSq >= threshold * threshold)
                {
                    _isWaitingForThreshold = false;
                    _isGestureActive = true;

                    // Detect foreground process
                    string processName = ActiveWindowHelper.GetActiveWindowProcessName();
                    _activeProfile = ConfigManager.GetProfileForProcess(processName);

                    Debug.WriteLine($"Gesture activated at pinned start point ({_startPoint.X}, {_startPoint.Y}). Process: {processName}");

                    // Show the UI on the main thread non-blockingly
                    var center = _startPoint;
                    var profile = _activeProfile;
                    var initialPos = e.Position;

                    Application.Current.Dispatcher.BeginInvoke(new Action(() =>
                    {
                        ShowRadialUI(center, profile);
                        ProcessMove(initialPos);
                    }), DispatcherPriority.Render);
                }
            }
            else if (_isGestureActive)
            {
                // Pure nanosecond hook thread math without blocking
                ProcessMove(e.Position);
            }
        }

        private void ProcessMove(Point currentPoint)
        {
            double dx = currentPoint.X - _startPoint.X;
            double dy = currentPoint.Y - _startPoint.Y;
            double distance = Math.Sqrt(dx * dx + dy * dy);

            int sectorIndex = -1;
            bool isEscaped = false;

            if (distance >= ConfigManager.CurrentConfig.InnerRadius)
            {
                if (ConfigManager.CurrentConfig.EnableOuterEscapeCancel)
                {
                    double escapeThreshold = ConfigManager.CurrentConfig.OuterEscapeDistance > 0 
                        ? ConfigManager.CurrentConfig.OuterEscapeDistance 
                        : ConfigManager.CurrentConfig.WheelRadius * 1.5;

                    if (distance > escapeThreshold)
                    {
                        isEscaped = true;
                        sectorIndex = -1;
                    }
                }

                if (!isEscaped)
                {
                    double angle = Math.Atan2(dy, dx) * (180.0 / Math.PI);
                    if (angle < 0) angle += 360.0;

                    int sectorCount = _activeProfile?.SectorCount ?? 8;
                    if (sectorCount <= 0) sectorCount = 8;
                    double sectorAngle = 360.0 / sectorCount;
                    sectorIndex = (int)Math.Floor((angle + (sectorAngle / 2.0)) / sectorAngle) % sectorCount;
                }
            }

            // Zero-overhead check: if nothing changed, do NOT dispatch to UI thread!
            if (sectorIndex == _selectedSectorIndex && isEscaped == _lastEscapedState)
            {
                return;
            }

            _selectedSectorIndex = sectorIndex;
            _lastEscapedState = isEscaped;

            int targetSector = sectorIndex;
            bool targetEscape = isEscaped;

            Application.Current.Dispatcher.BeginInvoke(new Action(() =>
            {
                if (_radialWindow != null)
                {
                    _radialWindow.Opacity = targetEscape ? 0.45 : 1.0;
                    _radialWindow.HighlightSector(targetSector);
                }
            }), DispatcherPriority.Input);
        }

        private void ShowRadialUI(Point center, WheelProfile profile)
        {
            if (_radialWindow != null)
            {
                _radialWindow.Close();
            }

            _radialWindow = new RadialWindow(center, profile);
            _radialWindow.Show();
        }

        private void HideRadialUI()
        {
            if (_radialWindow != null)
            {
                _radialWindow.Close();
                _radialWindow = null;
            }
        }
    }
}

using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Input;
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

            // Only check general modifier bypass if Trigger is NOT explicitly configured with those modifiers
            var trigger = ConfigManager.CurrentConfig.Trigger ?? new TriggerConfig();
            bool isCtrlPressed = ConfigManager.CurrentConfig.DisableOnCtrl && !trigger.RequireCtrl &&
                                 (Keyboard.Modifiers & ModifierKeys.Control) != 0;
            bool isShiftPressed = ConfigManager.CurrentConfig.DisableOnShift && !trigger.RequireShift &&
                                  (Keyboard.Modifiers & ModifierKeys.Shift) != 0;
            bool isAltPressed = ConfigManager.CurrentConfig.DisableOnAlt && !trigger.RequireAlt &&
                                (Keyboard.Modifiers & ModifierKeys.Alt) != 0;
            bool isModifierPressed = isCtrlPressed || isShiftPressed || isAltPressed;

            bool isFullScreen = ConfigManager.CurrentConfig.DisableOnFullScreen && FullScreenHelper.IsActiveWindowFullScreen();

            return isBlacklisted || isModifierPressed || isFullScreen;
        }

        private void Hook_OnTriggerButtonDown(object sender, MouseEventArgs e)
        {
            var trigger = ConfigManager.CurrentConfig.Trigger ?? new TriggerConfig();
            if (trigger.TriggerType != "Mouse") return;

            // Check if required modifiers match
            if (trigger.RequireCtrl && (Keyboard.Modifiers & ModifierKeys.Control) == 0) return;
            if (trigger.RequireShift && (Keyboard.Modifiers & ModifierKeys.Shift) == 0) return;
            if (trigger.RequireAlt && (Keyboard.Modifiers & ModifierKeys.Alt) == 0) return;
            if (trigger.RequireWin && (Keyboard.Modifiers & ModifierKeys.Windows) == 0) return;

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

            e.Handled = true; // Block initial mouse down for gesture assessment
            Debug.WriteLine($"TriggerMouseDown at {_startPoint.X}, {_startPoint.Y}. Waiting for threshold.");
        }

        private void Hook_OnTriggerButtonUp(object sender, MouseEventArgs e)
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
                }));
                e.Handled = true;
            }
            else if (_isGestureActive)
            {
                _isGestureActive = false;
                Debug.WriteLine($"Gesture completed. Selected sector: {_selectedSectorIndex}");

                Application.Current.Dispatcher.Invoke(() =>
                {
                    HideRadialUI();

                    if (_activeProfile != null && _selectedSectorIndex >= 0 && _selectedSectorIndex < _activeProfile.Actions.Count)
                    {
                        var action = _activeProfile.Actions[_selectedSectorIndex];
                        if (action != null && !string.IsNullOrEmpty(action.Type))
                        {
                            Debug.WriteLine($"Executing action: {action.Name} ({action.Type}: {action.Parameter})");
                            ActionExecutor.Execute(action);
                        }
                    }
                });

                e.Handled = true;
            }
        }

        private void KeyboardHook_OnKeyDown(object? sender, GlobalKeyEventArgs e)
        {
            var trigger = ConfigManager.CurrentConfig.Trigger ?? new TriggerConfig();
            if (trigger.TriggerType != "Keyboard") return;

            if (trigger.VkCode != 0 && e.VkCode != trigger.VkCode) return;

            if (trigger.RequireCtrl && (e.Modifiers & ModifierKeys.Control) == 0) return;
            if (trigger.RequireShift && (e.Modifiers & ModifierKeys.Shift) == 0) return;
            if (trigger.RequireAlt && (e.Modifiers & ModifierKeys.Alt) == 0) return;
            if (trigger.RequireWin && (e.Modifiers & ModifierKeys.Windows) == 0) return;

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

            e.Handled = true; // Intercept key
            Debug.WriteLine($"TriggerKeyDown ({e.VkCode}) at {_startPoint.X}, {_startPoint.Y}. Waiting for threshold.");
        }

        private void KeyboardHook_OnKeyUp(object? sender, GlobalKeyEventArgs e)
        {
            var trigger = ConfigManager.CurrentConfig.Trigger ?? new TriggerConfig();
            if (trigger.TriggerType != "Keyboard") return;

            if (trigger.VkCode != 0 && e.VkCode != trigger.VkCode) return;

            if (_isWaitingForThreshold)
            {
                _isWaitingForThreshold = false;
                Debug.WriteLine("Normal key tap detected. Replaying key press.");

                uint vk = trigger.VkCode;
                Application.Current.Dispatcher.BeginInvoke(new Action(() =>
                {
                    _keyboardHook?.ReplayKeyPress(vk);
                }));
                e.Handled = true;
            }
            else if (_isGestureActive)
            {
                _isGestureActive = false;
                Debug.WriteLine($"Keyboard gesture completed. Selected sector: {_selectedSectorIndex}");

                Application.Current.Dispatcher.Invoke(() =>
                {
                    HideRadialUI();

                    if (_activeProfile != null && _selectedSectorIndex >= 0 && _selectedSectorIndex < _activeProfile.Actions.Count)
                    {
                        var action = _activeProfile.Actions[_selectedSectorIndex];
                        if (action != null && !string.IsNullOrEmpty(action.Type))
                        {
                            Debug.WriteLine($"Executing action: {action.Name} ({action.Type}: {action.Parameter})");
                            ActionExecutor.Execute(action);
                        }
                    }
                });

                e.Handled = true;
            }
        }

        private void Hook_OnMouseMove(object sender, MouseEventArgs e)
        {
            if (_isWaitingForThreshold)
            {
                double dx = e.Position.X - _startPoint.X;
                double dy = e.Position.Y - _startPoint.Y;
                double distance = Math.Sqrt(dx * dx + dy * dy);

                if (distance >= ConfigManager.CurrentConfig.DragThreshold)
                {
                    _isWaitingForThreshold = false;
                    _isGestureActive = true;

                    // Detect foreground process
                    string processName = ActiveWindowHelper.GetActiveWindowProcessName();
                    _activeProfile = ConfigManager.GetProfileForProcess(processName);

                    Debug.WriteLine($"Gesture activated. Process: {processName}, Profile: {_activeProfile.ProcessName}, Sectors: {_activeProfile.SectorCount}");

                    // Show the UI on the main thread
                    Application.Current.Dispatcher.Invoke(() =>
                    {
                        ShowRadialUI(_startPoint, _activeProfile);
                        UpdateSelectedSector(e.Position);
                    });
                }
            }
            else if (_isGestureActive)
            {
                Application.Current.Dispatcher.Invoke(() =>
                {
                    UpdateSelectedSector(e.Position);
                });
            }
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

        private void UpdateSelectedSector(Point currentPoint)
        {
            if (_radialWindow == null || _activeProfile == null) return;

            double dx = currentPoint.X - _startPoint.X;
            double dy = currentPoint.Y - _startPoint.Y;
            double distance = Math.Sqrt(dx * dx + dy * dy);

            // Center Deadzone check
            if (distance < ConfigManager.CurrentConfig.InnerRadius)
            {
                _selectedSectorIndex = -1;
                _radialWindow.HighlightSector(-1);
                _radialWindow.Opacity = 1.0;
                return;
            }

            // Outer Escape (Overshoot) Cancel check
            if (ConfigManager.CurrentConfig.EnableOuterEscapeCancel)
            {
                double escapeThreshold = ConfigManager.CurrentConfig.OuterEscapeDistance > 0 
                    ? ConfigManager.CurrentConfig.OuterEscapeDistance 
                    : ConfigManager.CurrentConfig.WheelRadius * 1.5;

                if (distance > escapeThreshold)
                {
                    _selectedSectorIndex = -1;
                    _radialWindow.HighlightSector(-1);
                    _radialWindow.Opacity = 0.45;
                    return;
                }
                else
                {
                    _radialWindow.Opacity = 1.0;
                }
            }

            // Calculate Angle: 0 is North (Up), clockwise
            double angle = Math.Atan2(dx, -dy) * (180 / Math.PI);
            if (angle < 0)
            {
                angle += 360;
            }

            int sectorCount = _activeProfile.SectorCount;
            if (sectorCount <= 0) sectorCount = 8;
            double sectorAngle = 360.0 / sectorCount;

            int sectorIndex = (int)Math.Floor((angle + (sectorAngle / 2.0)) / sectorAngle) % sectorCount;
            _selectedSectorIndex = sectorIndex;
            _radialWindow.HighlightSector(sectorIndex);
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

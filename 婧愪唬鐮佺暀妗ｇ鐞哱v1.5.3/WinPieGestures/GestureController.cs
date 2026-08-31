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
        private int _selectedSubSectorIndex = -1;
        private bool _lastEscapedState = false;
        private int _lastSectorIndex = -1; // sector currently displayed (fan style keeps it while aiming at the fan)

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

            bool isIsolatedByProcess = false;
            string normProc = processName.Trim().ToLowerInvariant();

            if (string.Equals(ConfigManager.CurrentConfig.IsolationMode, "Whitelist", StringComparison.OrdinalIgnoreCase))
            {
                // Whitelist mode: ONLY allowed if process is in Whitelist
                bool inWhitelist = false;
                if (ConfigManager.CurrentConfig.WhitelistedProcesses != null)
                {
                    foreach (var white in ConfigManager.CurrentConfig.WhitelistedProcesses)
                    {
                        if (string.Equals(white.Trim(), normProc, StringComparison.OrdinalIgnoreCase))
                        {
                            inWhitelist = true;
                            break;
                        }
                    }
                }
                isIsolatedByProcess = !inWhitelist;
            }
            else
            {
                // Blacklist mode: bypassed if process is in Blacklist
                if (ConfigManager.CurrentConfig.BlacklistedProcesses != null)
                {
                    foreach (var blacklisted in ConfigManager.CurrentConfig.BlacklistedProcesses)
                    {
                        if (string.Equals(blacklisted.Trim(), normProc, StringComparison.OrdinalIgnoreCase))
                        {
                            isIsolatedByProcess = true;
                            break;
                        }
                    }
                }
            }

            // Modifier safety check: don't trigger if configured bypass keys are pressed
            bool isCtrlPressed = ConfigManager.CurrentConfig.DisableOnCtrl && ((Keyboard.GetKeyStates(Key.LeftCtrl) & KeyStates.Down) > 0 || (Keyboard.GetKeyStates(Key.RightCtrl) & KeyStates.Down) > 0);
            bool isShiftPressed = ConfigManager.CurrentConfig.DisableOnShift && ((Keyboard.GetKeyStates(Key.LeftShift) & KeyStates.Down) > 0 || (Keyboard.GetKeyStates(Key.RightShift) & KeyStates.Down) > 0);
            bool isAltPressed = ConfigManager.CurrentConfig.DisableOnAlt && ((Keyboard.GetKeyStates(Key.LeftAlt) & KeyStates.Down) > 0 || (Keyboard.GetKeyStates(Key.RightAlt) & KeyStates.Down) > 0);
            bool isModifierPressed = isCtrlPressed || isShiftPressed || isAltPressed;

            bool isFullScreen = ConfigManager.CurrentConfig.DisableOnFullScreen && FullScreenHelper.IsActiveWindowFullScreen();

            return isIsolatedByProcess || isModifierPressed || isFullScreen;
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
            _selectedSubSectorIndex = -1;
            _lastEscapedState = false;
            _lastSectorIndex = -1;

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
                int finalSubSector = _selectedSubSectorIndex;
                var finalProfile = _activeProfile;

                Debug.WriteLine($"Gesture completed. Selected sector: {finalSector}, sub: {finalSubSector}");

                Application.Current.Dispatcher.BeginInvoke(new Action(() =>
                {
                    HideRadialUI();

                    if (finalProfile != null && finalSector >= 0 && finalSector < finalProfile.Actions.Count)
                    {
                        var action = finalProfile.Actions[finalSector];
                        if (action != null)
                        {
                            if (finalSubSector >= 0 && action.SubActions != null && finalSubSector < action.SubActions.Count)
                            {
                                var subAction = action.SubActions[finalSubSector];
                                if (subAction != null && !string.IsNullOrEmpty(subAction.Type))
                                {
                                    Debug.WriteLine($"Executing sub-action: {subAction.Name} ({subAction.Type}: {subAction.Parameter})");
                                    ActionExecutor.Execute(subAction);
                                    return;
                                }
                            }

                            if (!string.IsNullOrEmpty(action.Type))
                            {
                                Debug.WriteLine($"Executing action: {action.Name} ({action.Type}: {action.Parameter})");
                                ActionExecutor.Execute(action);
                            }
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
            _selectedSubSectorIndex = -1;
            _lastEscapedState = false;
            _lastSectorIndex = -1;

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
                int finalSubSector = _selectedSubSectorIndex;
                var finalProfile = _activeProfile;

                Debug.WriteLine($"Keyboard gesture completed. Selected sector: {finalSector}, sub: {finalSubSector}");

                Application.Current.Dispatcher.BeginInvoke(new Action(() =>
                {
                    HideRadialUI();

                    if (finalProfile != null && finalSector >= 0 && finalSector < finalProfile.Actions.Count)
                    {
                        var action = finalProfile.Actions[finalSector];
                        if (action != null)
                        {
                            if (finalSubSector >= 0 && action.SubActions != null && finalSubSector < action.SubActions.Count)
                            {
                                var subAction = action.SubActions[finalSubSector];
                                if (subAction != null && !string.IsNullOrEmpty(subAction.Type))
                                {
                                    Debug.WriteLine($"Executing sub-action: {subAction.Name} ({subAction.Type}: {subAction.Parameter})");
                                    ActionExecutor.Execute(subAction);
                                    return;
                                }
                            }

                            if (!string.IsNullOrEmpty(action.Type))
                            {
                                Debug.WriteLine($"Executing action: {action.Name} ({action.Type}: {action.Parameter})");
                                ActionExecutor.Execute(action);
                            }
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
            int subSectorIndex = -1;
            bool isEscaped = false;

            double deadzone = Math.Min(ConfigManager.CurrentConfig.CoreRadius, ConfigManager.CurrentConfig.DragThreshold * 0.6);
            if (deadzone <= 0) deadzone = 15.0;

            if (distance >= deadzone)
            {
                double wheelRadius = ConfigManager.CurrentConfig.WheelRadius;
                bool multiTierEnabled = ConfigManager.CurrentConfig.EnableMultiTier;
                double subRatio = ConfigManager.CurrentConfig.SubWheelRadiusRatio > 1.1 ? ConfigManager.CurrentConfig.SubWheelRadiusRatio : 1.55;
                double maxRadius = multiTierEnabled ? (wheelRadius * subRatio + 20.0) : wheelRadius;

                if (ConfigManager.CurrentConfig.EnableOuterEscapeCancel)
                {
                    double escapeThreshold = ConfigManager.CurrentConfig.OuterEscapeDistance > 0 
                        ? ConfigManager.CurrentConfig.OuterEscapeDistance 
                        : maxRadius * 1.5;

                    if (distance > escapeThreshold)
                    {
                        isEscaped = true;
                        sectorIndex = -1;
                        subSectorIndex = -1;
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

                    bool fanStyle = multiTierEnabled &&
                        string.Equals(ConfigManager.CurrentConfig.SubmenuStyle, "Fan", StringComparison.OrdinalIgnoreCase);

                    // Fan style: while the pointer aims within the fan's angular band of the currently
                    // displayed sector, keep that sector (so the fan doesn't jump while reaching for a sub).
                    // Only lock if we are outside the inner radius AND the active sector actually has SubActions!
                    if (fanStyle && _lastSectorIndex >= 0 && distance >= (ConfigManager.CurrentConfig.InnerRadius * 0.75) && _activeProfile != null && _lastSectorIndex < _activeProfile.Actions.Count)
                    {
                        var lastParentAction = _activeProfile.Actions[_lastSectorIndex];
                        if (lastParentAction?.SubActions != null && lastParentAction.SubActions.Count > 0)
                        {
                            int displayed = _lastSectorIndex;
                            double relAng = angle - displayed * sectorAngle;
                            while (relAng < -180.0) relAng += 360.0;
                            while (relAng > 180.0) relAng -= 360.0;
                            if (Math.Abs(relAng) <= GetFanAngularHalfDeg())
                            {
                                sectorIndex = displayed;
                            }
                        }
                    }

                    // Check if mouse moved into the sub-tier zone
                    if (multiTierEnabled && _activeProfile != null && sectorIndex >= 0 && sectorIndex < _activeProfile.Actions.Count)
                    {
                        var parentAction = _activeProfile.Actions[sectorIndex];
                        if (parentAction != null && parentAction.SubActions != null && parentAction.SubActions.Count > 0)
                        {
                            if (fanStyle)
                            {
                                // Honeycomb fan style: nearest sub item wins (DPI scaled & symmetric slot)
                                subSectorIndex = HitTestFanSubs(currentPoint, sectorIndex, sectorAngle);
                            }
                            else if (distance >= (wheelRadius + 6.0))
                            {
                                // Wheel style: outer sub-ring split by angle
                                int subCount = parentAction.SubActions.Count;
                                double parentCenterAngle = sectorIndex * sectorAngle;
                                double parentStartAngle = parentCenterAngle - (sectorAngle / 2.0);

                                double relAngle = angle - parentStartAngle;
                                while (relAngle < 0) relAngle += 360.0;
                                while (relAngle >= 360.0) relAngle -= 360.0;

                                if (relAngle <= sectorAngle)
                                {
                                    int calculatedSub = (int)(relAngle / (sectorAngle / subCount));
                                    subSectorIndex = Math.Clamp(calculatedSub, 0, subCount - 1);
                                }
                            }
                        }
                    }
                }
            }
            else
            {
                _lastSectorIndex = -1;
            }

            // Zero-overhead check: if nothing changed, do NOT dispatch to UI thread!
            if (sectorIndex == _selectedSectorIndex && subSectorIndex == _selectedSubSectorIndex && isEscaped == _lastEscapedState)
            {
                return;
            }

            _selectedSectorIndex = sectorIndex;
            _selectedSubSectorIndex = subSectorIndex;
            _lastEscapedState = isEscaped;
            _lastSectorIndex = sectorIndex;

            int targetSector = sectorIndex;
            int targetSubSector = subSectorIndex;
            bool targetEscape = isEscaped;

            Application.Current.Dispatcher.BeginInvoke(new Action(() =>
            {
                if (_radialWindow != null)
                {
                    _radialWindow.SetOuterEscapeState(targetEscape);
                    _radialWindow.HighlightSector(targetSector, targetSubSector);
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

        /// <summary>
        /// Fan style hit-test: nearest of the (up to 3) honeycomb fan items, in the same frame
        /// the fan is rendered (positions relative to the gesture start, DPI aware).
        /// </summary>
        private int HitTestFanSubs(Point cursor, int sectorIndex, double sectorAngle)
        {
            if (_activeProfile == null || sectorIndex < 0 || sectorIndex >= _activeProfile.Actions.Count) return -1;
            var action = _activeProfile.Actions[sectorIndex];
            if (action == null || action.SubActions == null || action.SubActions.Count == 0) return -1;

            double scaleX = 1.0, scaleY = 1.0;
            if (_radialWindow != null)
            {
                var source = PresentationSource.FromVisual(_radialWindow);
                if (source?.CompositionTarget != null)
                {
                    scaleX = source.CompositionTarget.TransformToDevice.M11;
                    scaleY = source.CompositionTarget.TransformToDevice.M22;
                }
            }

            double outer = ConfigManager.CurrentConfig.WheelRadius;
            double inner = ConfigManager.CurrentConfig.InnerRadius;
            double R = (inner + outer) / 2.0;
            double itemR = (outer - inner) * 0.40;
            double midRad = sectorIndex * sectorAngle * (Math.PI / 180.0);
            double ux = Math.Cos(midRad), uy = Math.Sin(midRad);
            double vx = -Math.Sin(midRad), vy = Math.Cos(midRad);

            int subCount = Math.Min(RadialWindow.FanSubmenuSlotCount, action.SubActions.Count);
            int hit = -1;
            double best = double.MaxValue;
            for (int i = 0; i < subCount; i++)
            {
                int slot = RadialWindow.GetFanSlotIndex(i, subCount);
                var (du, dv) = RadialWindow.GetFanSubOffset(slot);
                double px = _startPoint.X + (ux * (du * R) + vx * (dv * R)) * scaleX;
                double py = _startPoint.Y + (uy * (du * R) + vy * (dv * R)) * scaleY;
                double hitRadius = itemR * 1.25 * scaleX;
                double d = Math.Sqrt((cursor.X - px) * (cursor.X - px) + (cursor.Y - py) * (cursor.Y - py));
                if (d <= hitRadius && d < best)
                {
                    best = d;
                    hit = i;
                }
            }
            return hit;
        }

        /// <summary>Half-width (degrees) of the fan's angular coverage around the sector axis.</summary>
        private static double GetFanAngularHalfDeg()
        {
            double maxDev = 0.0;
            for (int i = 0; i < RadialWindow.FanSubmenuSlotCount; i++)
            {
                var (du, dv) = RadialWindow.GetFanSubOffset(i);
                double dev = Math.Atan2(Math.Abs(dv), Math.Abs(du)) * (180.0 / Math.PI);
                if (dev > maxDev) maxDev = dev;
            }
            return maxDev + 4.0;
        }

        private void HideRadialUI()
        {
            _lastSectorIndex = -1;
            if (_radialWindow != null)
            {
                _radialWindow.Close();
                _radialWindow = null;
            }
        }
    }
}

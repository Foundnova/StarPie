using System;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Input;
using System.Windows.Threading;

namespace WinPieGestures;

public class GestureController
{
<<<<<<< HEAD
	private struct POINT
	{
		public int x;

		public int y;
	}

	private readonly MouseHook _mouseHook;

	private readonly KeyboardHook? _keyboardHook;

	private RadialWindow? _radialWindow;

	private Point _startPoint;

	private bool _isWaitingForThreshold;

	private bool _isGestureActive;

	private WheelProfile? _activeProfile;

	private int _selectedSectorIndex = -1;

	private int _selectedSubSectorIndex = -1;

	private bool _lastEscapedState;

	private bool _lastShowSubTier;

	[DllImport("user32.dll")]
	[return: MarshalAs(UnmanagedType.Bool)]
	private static extern bool GetCursorPos(out POINT lpPoint);

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
		string cleanProcess = (processName ?? "").Trim().ToLowerInvariant();

		bool isWhitelisted = false;
		if (ConfigManager.CurrentConfig.WhitelistedProcesses != null)
		{
			foreach (string whitelistedProcess in ConfigManager.CurrentConfig.WhitelistedProcesses)
			{
				if (string.Equals(whitelistedProcess.Trim(), cleanProcess, StringComparison.OrdinalIgnoreCase))
				{
					isWhitelisted = true;
					break;
				}
			}
		}

		bool isBlacklisted = false;
		if (ConfigManager.CurrentConfig.BlacklistedProcesses != null)
		{
			foreach (string blacklistedProcess in ConfigManager.CurrentConfig.BlacklistedProcesses)
			{
				if (string.Equals(blacklistedProcess.Trim(), cleanProcess, StringComparison.OrdinalIgnoreCase))
				{
					isBlacklisted = true;
					break;
				}
			}
		}

		bool isProcessIsolated = false;
		if (string.Equals(ConfigManager.CurrentConfig.IsolationMode, "Whitelist", StringComparison.OrdinalIgnoreCase))
		{
			isProcessIsolated = !isWhitelisted;
		}
		else
		{
			isProcessIsolated = isBlacklisted;
		}

		bool disableCtrl = ConfigManager.CurrentConfig.DisableOnCtrl && ((int)(Keyboard.GetKeyStates((Key)118) & KeyStates.Down) > 0 || (int)(Keyboard.GetKeyStates((Key)119) & KeyStates.Down) > 0);
		bool disableShift = ConfigManager.CurrentConfig.DisableOnShift && ((int)(Keyboard.GetKeyStates((Key)116) & KeyStates.Down) > 0 || (int)(Keyboard.GetKeyStates((Key)117) & KeyStates.Down) > 0);
		bool disableAlt = ConfigManager.CurrentConfig.DisableOnAlt && ((int)(Keyboard.GetKeyStates((Key)120) & KeyStates.Down) > 0 || (int)(Keyboard.GetKeyStates((Key)121) & KeyStates.Down) > 0);
		bool isModifierSuppressed = disableCtrl | disableShift | disableAlt;

		bool isFullScreenSuppressed = false;
		if (ConfigManager.CurrentConfig.DisableOnFullScreen)
		{
			if (!isWhitelisted && FullScreenHelper.IsActiveWindowFullScreen())
			{
				isFullScreenSuppressed = true;
			}
		}

		return isProcessIsolated || isModifierSuppressed || isFullScreenSuppressed;
	}

	private bool IsModifierKey(uint vkCode)
	{
		if (vkCode != 17 && vkCode != 162 && vkCode != 163 && vkCode != 18 && vkCode != 164 && vkCode != 165 && vkCode != 16 && vkCode != 160 && vkCode != 161 && vkCode != 91)
		{
			return vkCode == 92;
		}
		return true;
	}

	private void Hook_OnTriggerButtonDown(object? sender, MouseEventArgs e)
	{
		//IL_0027: Unknown result type (might be due to invalid IL or missing references)
		//IL_002c: Unknown result type (might be due to invalid IL or missing references)
		//IL_0035: Unknown result type (might be due to invalid IL or missing references)
		//IL_0037: Unknown result type (might be due to invalid IL or missing references)
		//IL_0043: Unknown result type (might be due to invalid IL or missing references)
		//IL_0045: Unknown result type (might be due to invalid IL or missing references)
		//IL_0051: Unknown result type (might be due to invalid IL or missing references)
		//IL_0053: Unknown result type (might be due to invalid IL or missing references)
		//IL_005f: Unknown result type (might be due to invalid IL or missing references)
		//IL_0061: Unknown result type (might be due to invalid IL or missing references)
		//IL_0087: Unknown result type (might be due to invalid IL or missing references)
		//IL_008c: Unknown result type (might be due to invalid IL or missing references)
		TriggerConfig triggerConfig = ConfigManager.CurrentConfig.Trigger ?? new TriggerConfig();
		if (triggerConfig.TriggerType != "Mouse")
		{
			return;
		}
		ModifierKeys currentModifiers = KeyboardHook.GetCurrentModifiers();
		if ((!triggerConfig.RequireCtrl || ((((int)currentModifiers & 2))) != 0) && (!triggerConfig.RequireShift || ((((int)currentModifiers & 4))) != 0) && (!triggerConfig.RequireAlt || ((((int)currentModifiers & 1))) != 0) && (!triggerConfig.RequireWin || ((((int)currentModifiers & 8))) != 0))
		{
			if (CheckIsIsolated(out string _))
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
			e.Handled = true;
		}
	}

	private void Hook_OnTriggerButtonUp(object? sender, MouseEventArgs e)
	{
		TriggerConfig triggerConfig = ConfigManager.CurrentConfig.Trigger ?? new TriggerConfig();
		if (triggerConfig.TriggerType != "Mouse")
		{
			return;
		}
		if (_isWaitingForThreshold)
		{
			_isWaitingForThreshold = false;
			string btn = triggerConfig.MouseButton ?? ConfigManager.CurrentConfig.TriggerButton ?? "RightButton";
			((DispatcherObject)Application.Current).Dispatcher.BeginInvoke((Delegate)(Action)delegate
			{
				_mouseHook.ReplayTriggerClick(btn);
			}, (DispatcherPriority)5, Array.Empty<object>());
			e.Handled = true;
		}
		else
		{
			if (!_isGestureActive)
			{
				return;
			}
			_isGestureActive = false;
			int finalSector = _selectedSectorIndex;
			int finalSubSector = _selectedSubSectorIndex;
			WheelProfile finalProfile = _activeProfile;
			((DispatcherObject)Application.Current).Dispatcher.BeginInvoke((Delegate)(Action)delegate
			{
				HideRadialUI();
				if (finalProfile != null && finalSector >= 0 && finalSector < finalProfile.Actions.Count)
				{
					ActionItem actionItem = finalProfile.Actions[finalSector];
					if (actionItem != null)
					{
						if (finalSubSector >= 0 && actionItem.SubActions != null && finalSubSector < actionItem.SubActions.Count)
						{
							ActionItem actionItem2 = actionItem.SubActions[finalSubSector];
							if (actionItem2 != null && !string.IsNullOrEmpty(actionItem2.Type))
							{
								ActionExecutor.Execute(actionItem2);
								return;
							}
						}
						if (!string.IsNullOrEmpty(actionItem.Type))
						{
							ActionExecutor.Execute(actionItem);
						}
					}
				}
			}, (DispatcherPriority)5, Array.Empty<object>());
			e.Handled = true;
		}
	}

	private void KeyboardHook_OnKeyDown(object? sender, GlobalKeyEventArgs e)
	{
		//IL_0028: Unknown result type (might be due to invalid IL or missing references)
		//IL_002d: Unknown result type (might be due to invalid IL or missing references)
		//IL_0036: Unknown result type (might be due to invalid IL or missing references)
		//IL_0038: Unknown result type (might be due to invalid IL or missing references)
		//IL_0044: Unknown result type (might be due to invalid IL or missing references)
		//IL_0046: Unknown result type (might be due to invalid IL or missing references)
		//IL_0052: Unknown result type (might be due to invalid IL or missing references)
		//IL_0054: Unknown result type (might be due to invalid IL or missing references)
		//IL_0060: Unknown result type (might be due to invalid IL or missing references)
		//IL_0062: Unknown result type (might be due to invalid IL or missing references)
		//IL_00da: Unknown result type (might be due to invalid IL or missing references)
		//IL_00df: Unknown result type (might be due to invalid IL or missing references)
		TriggerConfig triggerConfig = ConfigManager.CurrentConfig.Trigger ?? new TriggerConfig();
		if (triggerConfig.TriggerType != "Keyboard")
		{
			return;
		}
		ModifierKeys modifiers = e.Modifiers;
		if ((!triggerConfig.RequireCtrl || ((((int)modifiers & 2))) != 0) && (!triggerConfig.RequireShift || ((((int)modifiers & 4))) != 0) && (!triggerConfig.RequireAlt || ((((int)modifiers & 1))) != 0) && (!triggerConfig.RequireWin || ((((int)modifiers & 8))) != 0) && (triggerConfig.VkCode == 0 || IsModifierKey(triggerConfig.VkCode) || e.VkCode == triggerConfig.VkCode))
		{
			if (_isWaitingForThreshold || _isGestureActive)
			{
				e.Handled = true;
				return;
			}
			if (CheckIsIsolated(out string _))
			{
				_isWaitingForThreshold = false;
				_isGestureActive = false;
				e.Handled = false;
				return;
			}
			GetCursorPos(out var lpPoint);
			_startPoint = new Point((double)lpPoint.x, (double)lpPoint.y);
			_isWaitingForThreshold = true;
			_isGestureActive = false;
			_selectedSectorIndex = -1;
			_selectedSubSectorIndex = -1;
			_lastEscapedState = false;
			e.Handled = true;
		}
	}

	private void KeyboardHook_OnKeyUp(object? sender, GlobalKeyEventArgs e)
	{
		TriggerConfig triggerConfig = ConfigManager.CurrentConfig.Trigger ?? new TriggerConfig();
		if (triggerConfig.TriggerType != "Keyboard")
		{
			return;
		}
		bool flag = false;
		if (triggerConfig.VkCode != 0 && e.VkCode == triggerConfig.VkCode)
		{
			flag = true;
		}
		if (triggerConfig.RequireCtrl && (e.VkCode == 17 || e.VkCode == 162 || e.VkCode == 163))
		{
			flag = true;
		}
		if (triggerConfig.RequireShift && (e.VkCode == 16 || e.VkCode == 160 || e.VkCode == 161))
		{
			flag = true;
		}
		if (triggerConfig.RequireAlt && (e.VkCode == 18 || e.VkCode == 164 || e.VkCode == 165))
		{
			flag = true;
		}
		if (triggerConfig.RequireWin && (e.VkCode == 91 || e.VkCode == 92))
		{
			flag = true;
		}
		if (!flag)
		{
			return;
		}
		if (_isWaitingForThreshold)
		{
			_isWaitingForThreshold = false;
			uint vk = ((triggerConfig.VkCode != 0) ? triggerConfig.VkCode : e.VkCode);
			((DispatcherObject)Application.Current).Dispatcher.BeginInvoke((Delegate)(Action)delegate
			{
				_keyboardHook?.ReplayKeyPress(vk);
			}, (DispatcherPriority)5, Array.Empty<object>());
			e.Handled = true;
		}
		else
		{
			if (!_isGestureActive)
			{
				return;
			}
			_isGestureActive = false;
			int finalSector = _selectedSectorIndex;
			int finalSubSector = _selectedSubSectorIndex;
			WheelProfile finalProfile = _activeProfile;
			((DispatcherObject)Application.Current).Dispatcher.BeginInvoke((Delegate)(Action)delegate
			{
				HideRadialUI();
				if (finalProfile != null && finalSector >= 0 && finalSector < finalProfile.Actions.Count)
				{
					ActionItem actionItem = finalProfile.Actions[finalSector];
					if (actionItem != null)
					{
						if (finalSubSector >= 0 && actionItem.SubActions != null && finalSubSector < actionItem.SubActions.Count)
						{
							ActionItem actionItem2 = actionItem.SubActions[finalSubSector];
							if (actionItem2 != null && !string.IsNullOrEmpty(actionItem2.Type))
							{
								ActionExecutor.Execute(actionItem2);
								return;
							}
						}
						if (!string.IsNullOrEmpty(actionItem.Type))
						{
							ActionExecutor.Execute(actionItem);
						}
					}
				}
			}, (DispatcherPriority)5, Array.Empty<object>());
			e.Handled = true;
		}
	}

	private void Hook_OnMouseMove(object? sender, MouseEventArgs e)
	{
		//IL_0019: Unknown result type (might be due to invalid IL or missing references)
		//IL_001e: Unknown result type (might be due to invalid IL or missing references)
		//IL_0033: Unknown result type (might be due to invalid IL or missing references)
		//IL_0038: Unknown result type (might be due to invalid IL or missing references)
		//IL_00d6: Unknown result type (might be due to invalid IL or missing references)
		//IL_0087: Unknown result type (might be due to invalid IL or missing references)
		//IL_008c: Unknown result type (might be due to invalid IL or missing references)
		//IL_009f: Unknown result type (might be due to invalid IL or missing references)
		//IL_00a4: Unknown result type (might be due to invalid IL or missing references)
		if (_isWaitingForThreshold)
		{
			Point position = e.Position;
			double num = position.X - _startPoint.X;
			position = e.Position;
			double num2 = position.Y - _startPoint.Y;
			double num3 = num * num + num2 * num2;
			double dragThreshold = ConfigManager.CurrentConfig.DragThreshold;
			if (num3 >= dragThreshold * dragThreshold)
			{
				_isWaitingForThreshold = false;
				_isGestureActive = true;
				string activeWindowProcessName = ActiveWindowHelper.GetActiveWindowProcessName();
				_activeProfile = ConfigManager.GetProfileForProcess(activeWindowProcessName);
				Point center = _startPoint;
				WheelProfile profile = _activeProfile;
				Point initialPos = e.Position;
				((DispatcherObject)Application.Current).Dispatcher.BeginInvoke((Delegate)(Action)delegate
				{
					//IL_0007: Unknown result type (might be due to invalid IL or missing references)
					//IL_001e: Unknown result type (might be due to invalid IL or missing references)
					ShowRadialUI(center, profile);
					ProcessMove(initialPos);
				}, (DispatcherPriority)7, Array.Empty<object>());
			}
		}
		else if (_isGestureActive)
		{
			ProcessMove(e.Position);
		}
	}

	private void ProcessMove(Point currentPoint)
	{
		double num = currentPoint.X - _startPoint.X;
		double num2 = currentPoint.Y - _startPoint.Y;
		double num3 = Math.Sqrt(num * num + num2 * num2);
		int num4 = -1;
		int num5 = -1;
		bool flag = false;
		double num6 = Math.Min(ConfigManager.CurrentConfig.CoreRadius, ConfigManager.CurrentConfig.DragThreshold * 0.6);
		if (num6 <= 0.0)
		{
			num6 = 15.0;
		}
		bool flag2 = false;
		double num7 = ((ConfigManager.CurrentConfig.SubWheelTriggerDistance > 20.0) ? ConfigManager.CurrentConfig.SubWheelTriggerDistance : 95.0);
		if (num3 >= num6)
		{
			double wheelRadius = ConfigManager.CurrentConfig.WheelRadius;
			bool enableMultiTier = ConfigManager.CurrentConfig.EnableMultiTier;
			double num8 = ((ConfigManager.CurrentConfig.SubWheelOuterRadius > 0.0) ? ConfigManager.CurrentConfig.SubWheelOuterRadius : (wheelRadius * 1.55));
			double num9 = (enableMultiTier ? (num8 + 20.0) : wheelRadius);
			if (ConfigManager.CurrentConfig.EnableOuterEscapeCancel)
			{
				double num10 = ((ConfigManager.CurrentConfig.OuterEscapeDistance > 0.0) ? ConfigManager.CurrentConfig.OuterEscapeDistance : (num9 * 1.5));
				if (num3 > num10)
				{
					flag = true;
					num4 = -1;
					num5 = -1;
				}
			}
			if (!flag)
			{
				double num11 = Math.Atan2(num2, num) * (180.0 / Math.PI);
				if (num11 < 0.0)
				{
					num11 += 360.0;
				}
				int num12 = _activeProfile?.SectorCount ?? 8;
				if (num12 <= 0)
				{
					num12 = 8;
				}
				double num13 = 360.0 / (double)num12;
				num4 = (int)Math.Floor((num11 + num13 / 2.0) / num13) % num12;
				if (enableMultiTier && _activeProfile != null && num4 >= 0 && num4 < _activeProfile.Actions.Count)
				{
					ActionItem actionItem = _activeProfile.Actions[num4];
					if (actionItem != null && actionItem.SubActions != null && actionItem.SubActions.Count > 0)
					{
if (ConfigManager.CurrentConfig.SubmenuStyle == "Fan")
						{
							if (num3 >= num7)
							{
								flag2 = true;
								num5 = HitTestFanSubs(currentPoint, _startPoint, num4, actionItem.SubActions.Count);
							}
						}
						else
						{
							if (num3 >= num7)
							{
								flag2 = true;
							}
							double num14 = ((ConfigManager.CurrentConfig.SubWheelInnerGap >= 0.0) ? ConfigManager.CurrentConfig.SubWheelInnerGap : 4.0);
							double num15 = wheelRadius + num14 + 2.0;
							if (num3 >= num15)
							{
								int count = actionItem.SubActions.Count;
								double num16 = (double)num4 * num13 - num13 / 2.0;
								double num17;
								for (num17 = num11 - num16; num17 < 0.0; num17 += 360.0)
								{
								}
								while (num17 >= 360.0)
								{
									num17 -= 360.0;
								}
								if (num17 <= num13)
								{
									num5 = Math.Clamp((int)(num17 / (num13 / (double)count)), 0, count - 1);
								}
							}
						}
					}
				}
			}
		}
		if (num4 == _selectedSectorIndex && num5 == _selectedSubSectorIndex && flag == _lastEscapedState && flag2 == _lastShowSubTier)
		{
			return;
		}
		_selectedSectorIndex = num4;
		_selectedSubSectorIndex = num5;
		_lastEscapedState = flag;
		_lastShowSubTier = flag2;
		int targetSector = num4;
		int targetSubSector = num5;
		bool targetEscape = flag;
		bool targetShowSub = flag2;
		((DispatcherObject)Application.Current).Dispatcher.BeginInvoke((Delegate)(Action)delegate
		{
			if (_radialWindow != null)
			{
				_radialWindow.SetOuterEscapeState(targetEscape);
				_radialWindow.HighlightSector(targetSector, targetSubSector, targetShowSub);
			}
		}, (DispatcherPriority)5, Array.Empty<object>());
	}

	private void ShowRadialUI(Point center, WheelProfile profile)
	{
		//IL_0014: Unknown result type (might be due to invalid IL or missing references)
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

	private int HitTestFanSubs(Point currentPoint, Point centerPoint, int parentIndex, int subCount)
	{
		if (parentIndex < 0 || subCount <= 0) return -1;
		
		double dx = currentPoint.X - centerPoint.X;
		double dy = currentPoint.Y - centerPoint.Y;
		double dist = Math.Sqrt(dx * dx + dy * dy);
		
		double outer = ConfigManager.CurrentConfig.WheelRadius;
		double inner = ConfigManager.CurrentConfig.InnerRadius;
		
		if (dist < inner + (outer - inner) * 0.40)
		{
			return -1;
		}

		int n = _activeProfile?.SectorCount ?? 8;
		double sectorSize = 360.0 / n;
		double midRad = parentIndex * sectorSize * (Math.PI / 180.0);
		
		int activeCount = Math.Min(RadialWindow.FanSubmenuSlotCount, subCount);
		if (activeCount == 1)
		{
			return 0;
		}

		double mouseAngle = Math.Atan2(dy, dx);
		
		int bestSub = 0;
		double bestAngleDiff = double.MaxValue;
		
		double ux = Math.Cos(midRad), uy = Math.Sin(midRad);
		double vx = -Math.Sin(midRad), vy = Math.Cos(midRad);
		double R = (inner + outer) / 2.0;

		for (int j = 0; j < activeCount; j++)
		{
			int slot = RadialWindow.GetFanSlotIndex(j, activeCount);
			var (du, dv) = RadialWindow.GetFanSubOffset(slot);
			
			double px = ux * (du * R) + vx * (dv * R);
			double py = uy * (du * R) + vy * (dv * R);
			
			double itemAngle = Math.Atan2(py, px);
			double diff = Math.Abs(NormalizeAngleRad(mouseAngle - itemAngle));
			
			if (diff < bestAngleDiff)
			{
				bestAngleDiff = diff;
				bestSub = j;
			}
		}
		
		return bestSub;
	}

	private static double NormalizeAngleRad(double angle)
	{
		while (angle > Math.PI) angle -= 2.0 * Math.PI;
		while (angle < -Math.PI) angle += 2.0 * Math.PI;
		return angle;
	}

=======
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
                    // displayed sector, keep that sector (so the fan doesn't jump while reaching for a sub)
                    if (fanStyle && _lastSectorIndex >= 0)
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

                    // Check if mouse moved into the sub-tier zone
                    if (multiTierEnabled && _activeProfile != null && sectorIndex >= 0 && sectorIndex < _activeProfile.Actions.Count)
                    {
                        var parentAction = _activeProfile.Actions[sectorIndex];
                        if (parentAction != null && parentAction.SubActions != null && parentAction.SubActions.Count > 0)
                        {
                            if (fanStyle)
                            {
                                // Honeycomb fan style: nearest sub item wins
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
        /// the fan is rendered (positions relative to the gesture start).
        /// </summary>
        private int HitTestFanSubs(Point cursor, int sectorIndex, double sectorAngle)
        {
            if (_activeProfile == null || sectorIndex < 0 || sectorIndex >= _activeProfile.Actions.Count) return -1;
            var action = _activeProfile.Actions[sectorIndex];
            if (action == null || action.SubActions == null || action.SubActions.Count == 0) return -1;

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
                var (du, dv) = RadialWindow.GetFanSubOffset(i);
                double px = _startPoint.X + ux * (du * R) + vx * (dv * R);
                double py = _startPoint.Y + uy * (du * R) + vy * (dv * R);
                double d = Math.Sqrt((cursor.X - px) * (cursor.X - px) + (cursor.Y - py) * (cursor.Y - py));
                if (d <= itemR * 1.25 && d < best)
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
            if (_radialWindow != null)
            {
                _radialWindow.Close();
                _radialWindow = null;
            }
        }
    }
>>>>>>> 3ff691fae314fa72f6cc0244386f8e08f9efbc00
}

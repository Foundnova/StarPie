using System;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Input;
using System.Windows.Threading;

namespace WinPieGestures;

public class GestureController
{
	private struct POINT
	{
		public int x;

		public int y;
	}

	private readonly MouseHook _mouseHook;

	private readonly KeyboardHook? _keyboardHook;

	private RadialWindow? _radialWindow;

	private Point _startPoint;

	// 手势起点所在显示器的 DPI 缩放系数,用于将物理像素位移归一化为 DIP,
	// 保证多显示器混合 DPI 环境下滑动阈值与扇区命中判定一致。
	private double _currentDpiScaleX = 1.0;

	private double _currentDpiScaleY = 1.0;

	private volatile bool _isWaitingForThreshold;

	private volatile bool _isGestureActive;

	private WheelProfile? _activeProfile;

	private int _selectedSectorIndex = -1;

	private int _selectedSubSectorIndex = -1;

	private bool _lastEscapedState;

	private bool _lastShowSubTier;

	// Mouse hooks can outpace WPF rendering. Keep one pending visual update and
	// overwrite it with the newest state instead of queueing every intermediate
	// sector transition on the UI dispatcher.
	private readonly object _uiUpdateSync = new object();

	private long _gestureVersion;

	private bool _highlightUpdateScheduled;

	private int _pendingSectorIndex = -1;

	private int _pendingSubSectorIndex = -1;

	private bool _pendingEscape;

	private bool _pendingShowSubTier;

	private long _pendingGestureVersion;

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

	private long BeginGestureTracking()
	{
		lock (_uiUpdateSync)
		{
			_gestureVersion++;
			_highlightUpdateScheduled = false;
			_pendingSectorIndex = -1;
			_pendingSubSectorIndex = -1;
			_pendingEscape = false;
			_pendingShowSubTier = false;
			_pendingGestureVersion = _gestureVersion;
			_selectedSectorIndex = -1;
			_selectedSubSectorIndex = -1;
			_lastEscapedState = false;
			_lastShowSubTier = false;
			return _gestureVersion;
		}
	}

	private void CancelGestureTracking()
	{
		lock (_uiUpdateSync)
		{
			_gestureVersion++;
			_highlightUpdateScheduled = false;
			_pendingGestureVersion = _gestureVersion;
		}
	}

	private (int Sector, int SubSector, WheelProfile? Profile, RadialWindow? Window) EndActiveGesture()
	{
		lock (_uiUpdateSync)
		{
			var result = (_selectedSectorIndex, _selectedSubSectorIndex, _activeProfile, _radialWindow);
			_isGestureActive = false;
			_isWaitingForThreshold = false;
			_gestureVersion++;
			_highlightUpdateScheduled = false;
			_pendingGestureVersion = _gestureVersion;
			return result;
		}
	}

	private bool IsCurrentGesture(long version)
	{
		lock (_uiUpdateSync)
		{
			return version == _gestureVersion;
		}
	}

	private long GetCurrentGestureVersion()
	{
		lock (_uiUpdateSync)
		{
			return _gestureVersion;
		}
	}

	private void QueueHighlightUpdate(int sectorIndex, int subSectorIndex, bool isEscaped, bool showSubTier, long gestureVersion)
	{
		bool shouldSchedule;
		lock (_uiUpdateSync)
		{
			if (!_isGestureActive || gestureVersion != _gestureVersion)
			{
				return;
			}
			if (sectorIndex == _selectedSectorIndex &&
				subSectorIndex == _selectedSubSectorIndex &&
				isEscaped == _lastEscapedState &&
				showSubTier == _lastShowSubTier)
			{
				return;
			}

			_selectedSectorIndex = sectorIndex;
			_selectedSubSectorIndex = subSectorIndex;
			_lastEscapedState = isEscaped;
			_lastShowSubTier = showSubTier;
			_pendingSectorIndex = sectorIndex;
			_pendingSubSectorIndex = subSectorIndex;
			_pendingEscape = isEscaped;
			_pendingShowSubTier = showSubTier;
			_pendingGestureVersion = gestureVersion;
			shouldSchedule = !_highlightUpdateScheduled;
			_highlightUpdateScheduled = true;
		}

		if (!shouldSchedule)
		{
			return;
		}

		try
		{
			Application.Current.Dispatcher.BeginInvoke((Action)ApplyPendingHighlight, DispatcherPriority.Render);
		}
		catch
		{
			lock (_uiUpdateSync)
			{
				if (_pendingGestureVersion == gestureVersion)
				{
					_highlightUpdateScheduled = false;
				}
			}
		}
	}

	private void ApplyPendingHighlight()
	{
		int targetSector;
		int targetSubSector;
		bool targetEscape;
		bool targetShowSubTier;
		long targetGestureVersion;
		RadialWindow? radialWindow;

		lock (_uiUpdateSync)
		{
			if (!_highlightUpdateScheduled)
			{
				return;
			}
			targetSector = _pendingSectorIndex;
			targetSubSector = _pendingSubSectorIndex;
			targetEscape = _pendingEscape;
			targetShowSubTier = _pendingShowSubTier;
			targetGestureVersion = _pendingGestureVersion;
			if (!_isGestureActive || targetGestureVersion != _gestureVersion)
			{
				_highlightUpdateScheduled = false;
				return;
			}
			radialWindow = _radialWindow;
			// Keep the pending state until the activation callback creates the
			// window. That callback calls this method again after Show().
			if (radialWindow == null)
			{
				return;
			}
			_highlightUpdateScheduled = false;
		}

		if (!IsCurrentGesture(targetGestureVersion) || !ReferenceEquals(_radialWindow, radialWindow))
		{
			return;
		}
		radialWindow.SetOuterEscapeState(targetEscape);
		radialWindow.HighlightSector(targetSector, targetSubSector, targetShowSubTier);
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

		ModifierKeys currentModifiers = KeyboardHook.GetCurrentModifiers();
		bool disableCtrl = ConfigManager.CurrentConfig.DisableOnCtrl && currentModifiers.HasFlag(ModifierKeys.Control);
		bool disableShift = ConfigManager.CurrentConfig.DisableOnShift && currentModifiers.HasFlag(ModifierKeys.Shift);
		bool disableAlt = ConfigManager.CurrentConfig.DisableOnAlt && currentModifiers.HasFlag(ModifierKeys.Alt);
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
				CancelGestureTracking();
				_isWaitingForThreshold = false;
				_isGestureActive = false;
				e.Handled = false;
				return;
			}
			_startPoint = e.Position;
			var (scaleX, scaleY) = RadialWindow.GetMonitorDpiScale(_startPoint);
			_currentDpiScaleX = scaleX;
			_currentDpiScaleY = scaleY;
			BeginGestureTracking();
			_isWaitingForThreshold = true;
			_isGestureActive = false;
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
			CancelGestureTracking();
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
			var finalState = EndActiveGesture();
			int finalSector = finalState.Sector;
			int finalSubSector = finalState.SubSector;
			WheelProfile? finalProfile = finalState.Profile;
			RadialWindow? endedWindow = finalState.Window;
			((DispatcherObject)Application.Current).Dispatcher.BeginInvoke((Delegate)(Action)delegate
			{
				CloseGestureWindow(endedWindow);
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
				CancelGestureTracking();
				_isWaitingForThreshold = false;
				_isGestureActive = false;
				e.Handled = false;
				return;
			}
			GetCursorPos(out var lpPoint);
			_startPoint = new Point((double)lpPoint.x, (double)lpPoint.y);
			var (dpiX, dpiY) = RadialWindow.GetMonitorDpiScale(_startPoint);
			_currentDpiScaleX = dpiX;
			_currentDpiScaleY = dpiY;
			BeginGestureTracking();
			_isWaitingForThreshold = true;
			_isGestureActive = false;
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
			CancelGestureTracking();
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
			var finalState = EndActiveGesture();
			int finalSector = finalState.Sector;
			int finalSubSector = finalState.SubSector;
			WheelProfile? finalProfile = finalState.Profile;
			RadialWindow? endedWindow = finalState.Window;
			((DispatcherObject)Application.Current).Dispatcher.BeginInvoke((Delegate)(Action)delegate
			{
				CloseGestureWindow(endedWindow);
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
			double scaleX = (_currentDpiScaleX > 0.0) ? _currentDpiScaleX : 1.0;
			double scaleY = (_currentDpiScaleY > 0.0) ? _currentDpiScaleY : 1.0;
			double num = (position.X - _startPoint.X) / scaleX;
			position = e.Position;
			double num2 = (position.Y - _startPoint.Y) / scaleY;
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
				long gestureVersion = GetCurrentGestureVersion();
				// Publish the threshold-crossing position before the UI callback is
				// queued. Later move events replace it with the newest state.
				ProcessMove(initialPos);
				((DispatcherObject)Application.Current).Dispatcher.BeginInvoke((Delegate)(Action)delegate
				{
					if (!_isGestureActive || !IsCurrentGesture(gestureVersion))
					{
						return;
					}
					ShowRadialUI(center, profile);
					ApplyPendingHighlight();
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
		double moveScaleX = (_currentDpiScaleX > 0.0) ? _currentDpiScaleX : 1.0;
		double moveScaleY = (_currentDpiScaleY > 0.0) ? _currentDpiScaleY : 1.0;
		double num = (currentPoint.X - _startPoint.X) / moveScaleX;
		double num2 = (currentPoint.Y - _startPoint.Y) / moveScaleY;
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
		QueueHighlightUpdate(num4, num5, flag, flag2, GetCurrentGestureVersion());
	}

	private void ShowRadialUI(Point center, WheelProfile profile)
	{
		//IL_0014: Unknown result type (might be due to invalid IL or missing references)
		lock (_uiUpdateSync)
		{
			if (_radialWindow != null)
			{
				_radialWindow.Close();
			}
			_radialWindow = new RadialWindow(center, profile);
			_radialWindow.Show();
		}
	}

	private void HideRadialUI()
	{
		lock (_uiUpdateSync)
		{
			if (_radialWindow != null)
			{
				_radialWindow.Close();
				_radialWindow = null;
			}
		}
	}

	private void CloseGestureWindow(RadialWindow? gestureWindow)
	{
		if (gestureWindow == null)
		{
			return;
		}

		lock (_uiUpdateSync)
		{
			if (ReferenceEquals(_radialWindow, gestureWindow))
			{
				gestureWindow.Close();
				_radialWindow = null;
				return;
			}
		}

		// A newer gesture may already own the active window. Close only the
		// completed gesture's stale window and leave the newer one untouched.
		try
		{
			gestureWindow.Close();
		}
		catch
		{
		}
	}

	private int HitTestFanSubs(Point currentPoint, Point centerPoint, int parentIndex, int subCount)
	{
		if (parentIndex < 0 || subCount <= 0) return -1;

		double hitScaleX = (_currentDpiScaleX > 0.0) ? _currentDpiScaleX : 1.0;
		double hitScaleY = (_currentDpiScaleY > 0.0) ? _currentDpiScaleY : 1.0;
		double dx = (currentPoint.X - centerPoint.X) / hitScaleX;
		double dy = (currentPoint.Y - centerPoint.Y) / hitScaleY;
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

}

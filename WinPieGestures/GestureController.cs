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

	// 长按触发（可选）：钩子跑在独立后台线程，用 System.Threading.Timer（不依赖线程 Dispatcher），
	// 代数(Generation)防止旧回调触发到新手势；激活经主线程 Dispatcher 执行。
	private System.Threading.Timer? _longPressTimer;

	private int _longPressGeneration;

	private readonly object _longPressLock = new object();

	private bool _mouseTriggerDown;

	// ---- 鼠标手势（画轨迹识别；延迟分段缓冲：短段过滤 + 相邻同向合并 + 完全匹配才触发）----
	private bool _gestureMode;

	private bool _gestureWaiting;

	private bool _gestureTracking;

	private Point _gesturePressPoint;

	private Point _gestureLastSample;

	private readonly List<(int dir, double len)> _gestureRuns = new List<(int, double)>();

	private int _gesturePendingDir = -1;

	private double _gesturePendingLen;

	// 手势轨迹浮层（可视化）
	private GestureTrailOverlay? _trail;

	private double _trailLastX = double.NaN;

	private double _trailLastY = double.NaN;

	private double _trailScaleX = 1.0;

	private double _trailScaleY = 1.0;

	/// <summary>轻点回放后，下一次该键事件放行（SendInput 重放的按下需穿透给应用）。</summary>
	private bool _gestureReplayPending;

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
		_mouseHook.OnRawMouseButtonEvent += Hook_OnRawMouseButton;
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
				_mouseTriggerDown = false;
				CancelLongPressTimer();
				e.Handled = false;
				return;
			}
			string triggerBtn = triggerConfig.MouseButton ?? ConfigManager.CurrentConfig.TriggerButton ?? "RightButton";
			// 手势触发键已被 Raw 事件接管：这里只吞掉，不再走轮盘流程
			if (ConfigManager.CurrentConfig.GestureEnabled &&
				string.Equals(triggerBtn, ConfigManager.CurrentConfig.GestureTriggerButton ?? "MiddleButton", StringComparison.OrdinalIgnoreCase))
			{
				e.Handled = true;
				return;
			}
			_startPoint = e.Position;
			var (scaleX, scaleY) = RadialWindow.GetMonitorDpiScale(_startPoint);
			_currentDpiScaleX = scaleX;
			_currentDpiScaleY = scaleY;
			BeginGestureTracking();
			_isWaitingForThreshold = true;
			_isGestureActive = false;
			_mouseTriggerDown = true;
			// 可选：长按不动超过阈值即呼出轮盘（与拖动呼出共存）
			if (ConfigManager.CurrentConfig.LongPressTrigger)
			{
				StartLongPressTimer();
			}
			e.Handled = true;
		}
	}

	// ==================== 鼠标手势 ====================

	/// <summary>
	/// 任意鼠标按键原始事件：手势触发键由此接管（与"轮盘触发键"可以不同）。
	/// 按下 → 开始画轨迹；抬起 → 识别执行（轻点透传原生点击）。
	/// </summary>
	private void Hook_OnRawMouseButton(object? sender, RawMouseEventArgs e)
	{
		if (!ConfigManager.CurrentConfig.GestureEnabled)
		{
			return;
		}
		string gestureButton = ConfigManager.CurrentConfig.GestureTriggerButton ?? "MiddleButton";
		if (!string.Equals(e.MouseButton, gestureButton, StringComparison.OrdinalIgnoreCase))
		{
			return;
		}
		if (e.IsButtonDown)
		{
			// SendInput 重放回来的按下：放行（不重新开始手势）
			if (_gestureReplayPending)
			{
				_gestureReplayPending = false;
				return;
			}
			if (CheckIsIsolated(out _))
			{
				_gestureMode = false;
				e.Handled = false;
				return;
			}
			BeginGesture(e.Position);
			e.Handled = true; // 拦截原生按下
			return;
		}
		if (!_gestureMode)
		{
			return;
		}
		bool hadPath = _gestureTracking;
		EndGesture(e.Position);
		string pattern = GetPreviewPattern();
		if (hadPath)
		{
			if (pattern.Length > 0)
			{
				ActionItem? ga = FindGestureAction(pattern);
				if (ga != null)
				{
					ActionItem action = ga;
					((DispatcherObject)Application.Current).Dispatcher.BeginInvoke((Delegate)(Action)delegate
					{
						ActionExecutor.Execute(action);
					}, (DispatcherPriority)5, Array.Empty<object>());
				}
			}
			// 已画但未映射/未成图样：吞掉不执行
		}
		else
		{
			// 轻点：SendInput 回放原生点击（回放事件放行，见 _gestureReplayPending）
			_gestureReplayPending = true;
			((DispatcherObject)Application.Current).Dispatcher.BeginInvoke((Delegate)(Action)delegate
			{
				_mouseHook.ReplayTriggerClick(gestureButton);
			}, (DispatcherPriority)5, Array.Empty<object>());
		}
		e.Handled = true; // 拦截原生抬起
	}

	/// <summary>方向码 → 图样字符（8 方向，方位码与图样选项一致：U/D/L/R/UL/UR/DL/DR，屏幕坐标 y 向下）。</summary>
	private static string GestureDirCode(int dir)
	{
		return dir switch
		{
			0 => "R",  // 右
			1 => "DR", // 右下
			2 => "D",  // 下
			3 => "DL", // 左下
			4 => "L",  // 左
			5 => "UL", // 左上
			6 => "U",  // 上
			_ => "UR"  // 右上
		};
	}

	private static int GestureQuantizeDir(double dx, double dy)
	{
		double deg = Math.Atan2(dy, dx) * (180.0 / Math.PI);
		if (deg < 0.0)
		{
			deg += 360.0;
		}
		return (int)Math.Round(deg / 45.0) % 8;
	}

	private void BeginGesture(Point pressPoint)
	{
		_gestureMode = true;
		_gestureWaiting = true;
		_gestureTracking = false;
		_gesturePressPoint = pressPoint;
		_gestureLastSample = pressPoint;
		_gestureRuns.Clear();
		_gesturePendingDir = -1;
		_gesturePendingLen = 0.0;
		var (tScaleX, tScaleY) = RadialWindow.GetMonitorDpiScale(pressPoint);
		_trailScaleX = tScaleX;
		_trailScaleY = tScaleY;
		_trailLastX = pressPoint.X;
		_trailLastY = pressPoint.Y;
		// 按下即清空并隐藏浮层：杜绝上一次手势轨迹在触发瞬间闪现
		DispatchUi(delegate
		{
			if (_trail != null)
			{
				_trail.ClearTrail();
				_trail.Hide();
			}
		});
		// 轨迹在越过阈值后才显示（见 ShowGestureTrail）
	}

	private void ShowGestureTrail()
	{
		Point p0 = _gesturePressPoint;
		double sx = _trailScaleX, sy = _trailScaleY;
		DispatchUi(delegate
		{
			if (_trail == null)
			{
				_trail = new GestureTrailOverlay();
			}
			// 顺序关键：先清空 → 画新起点 → 最后才 Show。
			// Show 会同步触发一次 WM_PAINT（嵌套消息泵），若此时画布还是旧内容就会闪现一次，故必须先清先画后显示。
			_trail.ClearTrail();
			_trail.PositionAt(p0.X, p0.Y, sx, sy);
			_trail.BeginAt(p0.X, p0.Y, sx, sy);
			_trail.Show();
		});
	}

	/// <summary>
	/// 移动采样（延迟分段缓冲）：每步把位移并入当前方向行程；
	/// 方向切换时把上一行程存入缓冲列表。短段/曲线抖动的过滤在构建图样时统一做，
	/// 因此"画的线可以弯曲"，微拐弯不会产生方向段。
	/// </summary>
	private void FeedGesturePoint(Point current)
	{
		double dx = current.X - _gestureLastSample.X;
		double dy = current.Y - _gestureLastSample.Y;
		double dist = Math.Sqrt(dx * dx + dy * dy);
		if (dist < 2.0)
		{
			return; // 微抖动忽略
		}
		int dir = GestureQuantizeDir(dx, dy);
		if (dir == _gesturePendingDir)
		{
			_gesturePendingLen += dist;
		}
		else
		{
			if (_gesturePendingLen > 0.0)
			{
				_gestureRuns.Add((_gesturePendingDir, _gesturePendingLen));
				if (_gestureRuns.Count > 12)
				{
					_gestureRuns.RemoveAt(0);
				}
			}
			_gesturePendingDir = dir;
			_gesturePendingLen = dist;
		}
		_gestureLastSample = current;
		// 轨迹：距离够才加一次，避免污染 Hook 消息调度的消息队列；同时更新"松手将执行"提示
		if (Math.Abs(current.X - _trailLastX) + Math.Abs(current.Y - _trailLastY) >= 4.0)
		{
			_trailLastX = current.X;
			_trailLastY = current.Y;
			double tx = current.X, ty = current.Y;
			string hint = BuildGestureHint(GetPreviewPattern());
			string placement = ConfigManager.CurrentConfig.GestureHintPlacement ?? "Auto";
			if (placement == "Auto")
			{
				int curDir = GestureQuantizeDir(current.X - _gesturePressPoint.X, current.Y - _gesturePressPoint.Y);
				placement = GestureDirCode((curDir + 4) % 8); // 提示放在运动反方向，避免被手遮挡
			}
			string hintPlacement = placement;
			DispatchUi(delegate
			{
				_trail?.AddPoint(tx, ty, _trailScaleX, _trailScaleY);
				_trail?.UpdateHint(hint, tx, ty, _trailScaleX, _trailScaleY, hintPlacement);
			});
		}
	}

	/// <summary>
	/// 构建最终图样：过滤掉长度低于灵敏度的短段（屏蔽过短的线段/曲线抖动），
	/// 相邻同向合并，最多 3 段；只有完整匹配映射才触发。
	/// </summary>
	private string GetPreviewPattern()
	{
		double segMin = ConfigManager.CurrentConfig.GestureSegmentSensitivity > 6.0 ? ConfigManager.CurrentConfig.GestureSegmentSensitivity : 12.0;
		List<(int dir, double len)> runs = new List<(int, double)>(_gestureRuns);
		if (_gesturePendingDir >= 0 && _gesturePendingLen > 0.0)
		{
			runs.Add((_gesturePendingDir, _gesturePendingLen));
		}
		List<int> dirs = new List<int>();
		foreach (var (dir, len) in runs)
		{
			if (len < segMin)
			{
				continue; // 短段屏蔽
			}
			if (dirs.Count > 0 && dirs[dirs.Count - 1] == dir)
			{
				continue; // 相邻同向合并
			}
			dirs.Add(dir);
			if (dirs.Count >= 3)
			{
				break;
			}
		}
		return string.Join("-", dirs.Select(GestureDirCode));
	}

	/// <summary>图样 → 箭头文本（如 "D-R" → "↓→"）。</summary>
	private static string GesturePatternGlyph(string pattern)
	{
		return pattern
			.Replace("UL", "↖")
			.Replace("UR", "↗")
			.Replace("DL", "↙")
			.Replace("DR", "↘")
			.Replace("U", "↑")
			.Replace("D", "↓")
			.Replace("L", "←")
			.Replace("R", "→");
	}

	/// <summary>提示文本：图样箭头 + 映射动作名/参数；未映射只显示图样。</summary>
	private string BuildGestureHint(string pattern)
	{
		string glyph = GesturePatternGlyph(pattern);
		ActionItem? a = FindGestureAction(pattern);
		if (a == null)
		{
			return glyph;
		}
		string label = (!string.IsNullOrEmpty(a.Name) && a.Name != "手势动作") ? a.Name : (a.Parameter ?? "");
		if (string.IsNullOrEmpty(label))
		{
			label = a.Type ?? "";
		}
		return string.IsNullOrEmpty(label) ? glyph : $"{glyph}  {label}";
	}

	private void EndGesture(Point current)
	{
		// 收尾：把最后一段并入缓冲（过滤与合并交给 GetPreviewPattern）
		{
			double dx = current.X - _gestureLastSample.X;
			double dy = current.Y - _gestureLastSample.Y;
			double dist = Math.Sqrt(dx * dx + dy * dy);
			if (dist >= 2.0)
			{
				int dir = GestureQuantizeDir(dx, dy);
				if (dir == _gesturePendingDir)
				{
					_gesturePendingLen += dist;
				}
				else
				{
					if (_gesturePendingLen > 0.0)
					{
						_gestureRuns.Add((_gesturePendingDir, _gesturePendingLen));
					}
					_gesturePendingDir = dir;
					_gesturePendingLen = dist;
				}
			}
			if (_gesturePendingLen > 0.0)
			{
				_gestureRuns.Add((_gesturePendingDir, _gesturePendingLen));
			}
		}
		_gesturePendingDir = -1;
		_gesturePendingLen = 0.0;
		_gestureMode = false;
		_gestureWaiting = false;
		_gestureTracking = false;
		_trailLastX = double.NaN;
		_trailLastY = double.NaN;
		DispatchUi(delegate
		{
			if (_trail != null)
			{
				_trail.ClearTrail();
				_trail.Hide();
			}
		});
	}

	private void DispatchUi(Action action)
	{
		try
		{
			((DispatcherObject)Application.Current).Dispatcher.BeginInvoke(action, DispatcherPriority.Background);
		}
		catch
		{
		}
	}

	/// <summary>查找手势图样映射的动作。</summary>
	private ActionItem? FindGestureAction(string pattern)
	{
		if (ConfigManager.CurrentConfig.GestureMappings != null)
		{
			foreach (GestureMapping m in ConfigManager.CurrentConfig.GestureMappings)
			{
				if (string.Equals(m.Pattern, pattern, StringComparison.OrdinalIgnoreCase))
				{
					return m.Action;
				}
			}
		}
		return null;
	}

	/// <summary>启动长按触发计时（按下时，仅鼠标触发）。</summary>
	private void StartLongPressTimer()
	{
		CancelLongPressTimer();
		double delay = ConfigManager.CurrentConfig.LongPressDelayMs > 0.0 ? ConfigManager.CurrentConfig.LongPressDelayMs : 450.0;
		int generation;
		lock (_longPressLock)
		{
			generation = ++_longPressGeneration;
			_longPressTimer = new System.Threading.Timer(delegate
			{
				LongPressTimerCallback(generation);
			}, null, TimeSpan.FromMilliseconds(delay), System.Threading.Timeout.InfiniteTimeSpan);
		}
	}

	private void CancelLongPressTimer()
	{
		lock (_longPressLock)
		{
			_longPressGeneration++;
			if (_longPressTimer != null)
			{
				_longPressTimer.Dispose();
				_longPressTimer = null;
			}
		}
	}

	/// <summary>长按达阈值：在按下处呼出轮盘（光标仍在中心，移动后再选择）。线程池回调 → 主线程 UI。</summary>
	private void LongPressTimerCallback(int generation)
	{
		lock (_longPressLock)
		{
			if (generation != _longPressGeneration)
			{
				return; // 已被取消/替换的旧回调
			}
			_longPressTimer = null;
		}
		if (!_mouseTriggerDown || !_isWaitingForThreshold || _isGestureActive)
		{
			return;
		}
		try
		{
			_isWaitingForThreshold = false;
			_isGestureActive = true;
			string processName = ActiveWindowHelper.GetActiveWindowProcessName();
			WheelProfile profile = ConfigManager.GetProfileForProcess(processName);
			long gestureVersion = GetCurrentGestureVersion();
			Point startPoint = _startPoint;
			((DispatcherObject)Application.Current).Dispatcher.BeginInvoke((Delegate)(Action)delegate
			{
				try
				{
					if (!_isGestureActive)
					{
						return;
					}
					if (ShowRadialUI(startPoint, profile, gestureVersion))
					{
						ApplyPendingHighlight();
					}
				}
				catch (Exception ex)
				{
					AppLogger.LogError("ShowRadialUI failed in LongPressTimerCallback", ex);
					// 激活失败：恢复状态，保证拖拽等其它触发途径不受影响
					_isWaitingForThreshold = true;
					_isGestureActive = false;
				}
			}, DispatcherPriority.Normal, Array.Empty<object>());
		}
		catch
		{
			// 预检/分派失败：恢复状态
			_isWaitingForThreshold = true;
			_isGestureActive = false;
		}
	}

	private void Hook_OnTriggerButtonUp(object? sender, MouseEventArgs e)
	{
		TriggerConfig triggerConfig = ConfigManager.CurrentConfig.Trigger ?? new TriggerConfig();
		if (triggerConfig.TriggerType != "Mouse")
		{
			return;
		}
		// 手势键抬起已由 Raw 事件处理，这里直接吞掉
		if (_gestureMode)
		{
			e.Handled = true;
			return;
		}
		_mouseTriggerDown = false;
		CancelLongPressTimer();
		if (_isWaitingForThreshold)
		{
			CancelGestureTracking();
			_isWaitingForThreshold = false;
			string btn = triggerConfig.MouseButton ?? ConfigManager.CurrentConfig.TriggerButton ?? "RightButton";
			((DispatcherObject)Application.Current).Dispatcher.BeginInvoke((Delegate)(Action)delegate
			{
				_mouseHook.ReplayTriggerClick(btn);
			}, DispatcherPriority.Normal, Array.Empty<object>());
			e.Handled = true;
		}
		else
		{
			if (!_isGestureActive)
			{
				// 安全兜底：如果等待状态已结束且手势未处于激活态（说明被提前取消或展示失败），补发重放物理按键，杜绝丢键
				string btn = triggerConfig.MouseButton ?? ConfigManager.CurrentConfig.TriggerButton ?? "RightButton";
				((DispatcherObject)Application.Current).Dispatcher.BeginInvoke((Delegate)(Action)delegate
				{
					_mouseHook.ReplayTriggerClick(btn);
				}, DispatcherPriority.Normal, Array.Empty<object>());
				e.Handled = true;
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
				ActionItem? targetAction = null;
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
								targetAction = actionItem2;
							}
						}
						if (targetAction == null && !string.IsNullOrEmpty(actionItem.Type))
						{
							targetAction = actionItem;
						}
					}
				}
				if (targetAction == null)
				{
					// 仅"外甩取消"（释放时处于外甩状态且未选中任何动作）时执行自定义取消动作；
					// 回到中心取消按钮松手仍为默认静默关闭。
					ActionItem? cancelAction = ConfigManager.CurrentConfig?.CancelAction;
					if (_lastEscapedState &&
						ConfigManager.CurrentConfig?.EnableCancelAction == true &&
						cancelAction != null && !string.IsNullOrEmpty(cancelAction.Type))
					{
						targetAction = cancelAction;
					}
				}
				if (targetAction != null)
				{
					ActionExecutor.EnqueueAction(targetAction);
				}
			}, DispatcherPriority.Normal, Array.Empty<object>());
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
			}, DispatcherPriority.Normal, Array.Empty<object>());
			e.Handled = true;
		}
		else
		{
			if (!_isGestureActive)
			{
				uint vk = ((triggerConfig.VkCode != 0) ? triggerConfig.VkCode : e.VkCode);
				((DispatcherObject)Application.Current).Dispatcher.BeginInvoke((Delegate)(Action)delegate
				{
					_keyboardHook?.ReplayKeyPress(vk);
				}, DispatcherPriority.Normal, Array.Empty<object>());
				e.Handled = true;
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
				ActionItem? targetAction = null;
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
								targetAction = actionItem2;
							}
						}
						if (targetAction == null && !string.IsNullOrEmpty(actionItem.Type))
						{
							targetAction = actionItem;
						}
					}
				}
				if (targetAction == null)
				{
					// 仅"外甩取消"（释放时处于外甩状态且未选中任何动作）时执行自定义取消动作；
					// 回到中心取消按钮松手仍为默认静默关闭。
					ActionItem? cancelAction = ConfigManager.CurrentConfig?.CancelAction;
					if (_lastEscapedState &&
						ConfigManager.CurrentConfig?.EnableCancelAction == true &&
						cancelAction != null && !string.IsNullOrEmpty(cancelAction.Type))
					{
						targetAction = cancelAction;
					}
				}
				if (targetAction != null)
				{
					ActionExecutor.EnqueueAction(targetAction);
				}
			}, DispatcherPriority.Normal, Array.Empty<object>());
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
		// 鼠标手势：采集轨迹
		if (_gestureMode)
		{
			if (_gestureWaiting)
			{
				double gScaleX = (_currentDpiScaleX > 0.0) ? _currentDpiScaleX : 1.0;
				double gScaleY = (_currentDpiScaleY > 0.0) ? _currentDpiScaleY : 1.0;
				double gdx = (e.Position.X - _gesturePressPoint.X) / gScaleX;
				double gdy = (e.Position.Y - _gesturePressPoint.Y) / gScaleY;
				double gDist = Math.Sqrt(gdx * gdx + gdy * gdy);
				if (gDist >= ConfigManager.CurrentConfig.DragThreshold)
				{
					_gestureWaiting = false;
					_gestureTracking = true;
					_gestureLastSample = e.Position;
					ShowGestureTrail();
				}
			}
			else if (_gestureTracking)
			{
				FeedGesturePoint(e.Position);
			}
			return;
		}
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
				CancelLongPressTimer(); // 拖动先于长按触发
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
					try
					{
						if (!_isGestureActive)
						{
							return;
						}
						if (ShowRadialUI(center, profile, gestureVersion))
						{
							ApplyPendingHighlight();
						}
					}
					catch (Exception ex)
					{
						AppLogger.LogError("ShowRadialUI failed in Hook_OnMouseMove", ex);
						_isWaitingForThreshold = true;
						_isGestureActive = false;
					}
				}, DispatcherPriority.Normal, Array.Empty<object>());
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

	private bool ShowRadialUI(Point center, WheelProfile profile, long gestureVersion)
	{
		// 预加载的线程调度与 single-flight 统一由 WindowTaskbarHelper 管理。
		try
		{
			WindowTaskbarHelper.Prefetch();
		}
		catch
		{
		}

		// 构造过程可能同步读取缓存图标，必须在手势状态锁外执行。
		RadialWindow newWindow = new RadialWindow(center, profile);
		RadialWindow? previousWindow;
		lock (_uiUpdateSync)
		{
			if (!_isGestureActive)
			{
				try
				{
					newWindow.Close();
				}
				catch
				{
				}
				return false;
			}
			previousWindow = _radialWindow;
			_radialWindow = newWindow;
		}

		if (previousWindow != null)
		{
			try
			{
				previousWindow.Close();
			}
			catch
			{
			}
		}

		try
		{
			newWindow.Show();
			return true;
		}
		catch
		{
			lock (_uiUpdateSync)
			{
				if (ReferenceEquals(_radialWindow, newWindow))
				{
					_radialWindow = null;
				}
			}
			try
			{
				newWindow.Close();
			}
			catch
			{
			}
			throw;
		}
	}

	private void HideRadialUI()
	{
		RadialWindow? windowToClose;
		lock (_uiUpdateSync)
		{
			windowToClose = _radialWindow;
			_radialWindow = null;
		}
		if (windowToClose != null)
		{
			try
			{
				windowToClose.Close();
			}
			catch
			{
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
				_radialWindow = null;
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
			var (du, dv) = RadialWindow.GetFanSubOffsetForShape(ConfigManager.CurrentConfig.Shape, slot);
			
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

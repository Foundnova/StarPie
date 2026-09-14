using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;

namespace WinPieGestures;

public partial class QuickSearchWindow : Window
{
	private static QuickSearchWindow? _instance;
	private string _currentCategory = "All";
	private DispatcherTimer? _debounceTimer;
	private DispatcherTimer? _feedbackTimer;
	private DispatcherTimer? _idleCleanupTimer;
	private int _idleStep;
	private CancellationTokenSource? _searchCts;
	private bool _isContextMenuOpen;
	private bool _isPinned;

	public QuickSearchWindow()
	{
		InitializeComponent();

		if (ConfigManager.CurrentConfig != null)
		{
			if (ConfigManager.CurrentConfig.QuickSearchWidth >= 560)
				Width = ConfigManager.CurrentConfig.QuickSearchWidth;
			if (ConfigManager.CurrentConfig.QuickSearchHeight >= 380)
				Height = ConfigManager.CurrentConfig.QuickSearchHeight;
			_isPinned = ConfigManager.CurrentConfig.QuickSearchPinned;
		}

		_debounceTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(75) };
		_debounceTimer.Tick += DebounceTimer_Tick;

		_feedbackTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(3.5) };
		_feedbackTimer.Tick += (s, e) =>
		{
			ActionFeedbackBanner.Visibility = Visibility.Collapsed;
			_feedbackTimer.Stop();
		};

		IsVisibleChanged += (s, e) =>
		{
			if (IsVisible)
			{
				_idleCleanupTimer?.Stop();
				_idleStep = 0;
			}
			else
			{
				StartIdleCleanup();
			}
		};
	}

	private void StartIdleCleanup()
	{
		_searchCts?.Cancel();
		_idleStep = 0;
		if (_idleCleanupTimer == null)
		{
			_idleCleanupTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(10) };
			_idleCleanupTimer.Tick += IdleCleanupTimer_Tick;
		}
		_idleCleanupTimer.Stop();
		_idleCleanupTimer.Start();
	}

	private void IdleCleanupTimer_Tick(object? sender, EventArgs e)
	{
		if (IsVisible)
		{
			_idleCleanupTimer?.Stop();
			return;
		}

		_idleStep++;
		if (_idleStep == 1)
		{
			// 隐藏 10 秒后：释放 WPF 结果列表视觉树与数据绑定
			ResultsListBox.ItemsSource = null;
		}
		else if (_idleStep >= 3)
		{
			// 隐藏 30 秒后：完全销毁窗口实例并释放搜索引擎及动态图标缓存
			_idleCleanupTimer?.Stop();
			try
			{
				Close();
			}
			catch { }
			_instance = null;
			NativeSearchEngine.ClearCaches();
			IconHelper.TrimDynamicCache();
			MemoryOptimizer.TrimMemory(force: false);
		}
	}

	protected override void OnClosed(EventArgs e)
	{
		base.OnClosed(e);
		_idleCleanupTimer?.Stop();
		_debounceTimer?.Stop();
		_feedbackTimer?.Stop();
		_searchCts?.Cancel();
		ResultsListBox.ItemsSource = null;
		if (ReferenceEquals(_instance, this))
		{
			_instance = null;
		}
	}

	public static void ShowOrActivate(Point? triggerPoint = null)
	{
		if (_instance == null || !_instance.IsLoaded)
		{
			_instance = new QuickSearchWindow();
		}
		_instance.ShowAndPosition(triggerPoint);
	}

	private void ShowAndPosition(Point? triggerPoint = null)
	{
		AppThemeManager.ApplyTheme(this, ConfigManager.CurrentConfig?.AppTheme ?? "System");

		if (ConfigManager.CurrentConfig != null)
		{
			if (ConfigManager.CurrentConfig.QuickSearchWidth >= 560)
				Width = ConfigManager.CurrentConfig.QuickSearchWidth;
			if (ConfigManager.CurrentConfig.QuickSearchHeight >= 380)
				Height = ConfigManager.CurrentConfig.QuickSearchHeight;
			_isPinned = ConfigManager.CurrentConfig.QuickSearchPinned;
		}
		UpdatePinVisual();

		// 1. 获取当前触发时的物理光标坐标并解析所在屏幕上下文
		var physPos = triggerPoint ?? ScreenHelper.GetCursorPhysicalPosition();
		var screenCtx = ScreenHelper.GetScreenContextAtPoint(physPos);
		Point mouseDip = ScreenHelper.PhysicalToDip(physPos, screenCtx.DpiScale);

		// 2. 将搜索框水平居中于鼠标，垂直方向偏上（让顶部搜索条刚好落在光标位置附近，实现指哪搜哪的盲操手感）
		double targetLeft = mouseDip.X - Width / 2.0;
		double targetTop = mouseDip.Y - 45.0;

		// 3. 严格遵循屏幕工作区贴边防溢出规范（支持多显示器与任务栏规避）
		Rect work = screenCtx.DipWorkArea;
		const double margin = 12.0;
		targetLeft = Math.Clamp(targetLeft, work.Left + margin, Math.Max(work.Left + margin, work.Right - Width - margin));
		targetTop = Math.Clamp(targetTop, work.Top + margin, Math.Max(work.Top + margin, work.Bottom - Height - margin));

		Left = targetLeft;
		Top = targetTop;

		SearchInputBox.Text = "";
		_currentCategory = "All";
		UpdateFilterChipsStyle();
		UpdateEngineBadgeVisual(EverythingService.DetectCurrentEngineState());
		TriggerSearch(immediate: true);

		Show();
		Activate();
		SearchInputBox.Focus();
	}

	private void Window_Loaded(object sender, RoutedEventArgs e)
	{
		UpdatePinVisual();
		TriggerSearch(immediate: true);
	}

	private void Window_Deactivated(object? sender, EventArgs e)
	{
		if (_isContextMenuOpen || _isPinned) return;
		// 鼠标点击搜索框外部区域时自动平滑隐藏，避免干扰用户正常工作
		Hide();
	}

	private void PinBtn_Click(object sender, RoutedEventArgs e)
	{
		_isPinned = !_isPinned;
		if (ConfigManager.CurrentConfig != null)
		{
			ConfigManager.CurrentConfig.QuickSearchPinned = _isPinned;
			ConfigManager.SaveConfig();
		}
		UpdatePinVisual();
	}

	private void UpdatePinVisual()
	{
		if (PinBtn == null) return;
		Topmost = true;
		if (_isPinned)
		{
			PinBtn.Background = (Brush)FindResource("AccentPrimaryBrush");
			PinBtn.Foreground = (Brush)FindResource("AccentTextBrush");
			PinBtn.BorderBrush = (Brush)FindResource("AccentHoverBrush");
			PinBtn.ToolTip = "取消置顶 (当前已固定在最前端，失焦不隐藏)";
		}
		else
		{
			PinBtn.ClearValue(BackgroundProperty);
			PinBtn.ClearValue(ForegroundProperty);
			PinBtn.ClearValue(BorderBrushProperty);
			PinBtn.ToolTip = "窗口置顶 (点击固定在最前端，失焦不隐藏)";
		}
	}

	private void WindowResizeGrip_DragDelta(object sender, System.Windows.Controls.Primitives.DragDeltaEventArgs e)
	{
		ResizeWindow(e.HorizontalChange, e.VerticalChange);
	}

	private void RightEdge_DragDelta(object sender, System.Windows.Controls.Primitives.DragDeltaEventArgs e)
	{
		ResizeWindow(e.HorizontalChange, 0);
	}

	private void BottomEdge_DragDelta(object sender, System.Windows.Controls.Primitives.DragDeltaEventArgs e)
	{
		ResizeWindow(0, e.VerticalChange);
	}

	private void ResizeWindow(double deltaW, double deltaH)
	{
		double newW = Math.Max(MinWidth, Width + deltaW);
		double newH = Math.Max(MinHeight, Height + deltaH);

		var screenCtx = ScreenHelper.GetScreenContextAtPoint(new Point(Left, Top));
		Rect work = screenCtx.DipWorkArea;
		newW = Math.Min(newW, work.Width - 24);
		newH = Math.Min(newH, work.Height - 24);

		Width = newW;
		Height = newH;

		if (ConfigManager.CurrentConfig != null)
		{
			ConfigManager.CurrentConfig.QuickSearchWidth = newW;
			ConfigManager.CurrentConfig.QuickSearchHeight = newH;
			ConfigManager.SaveConfig();
		}
	}

	private void ContextMenu_Opened(object sender, RoutedEventArgs e)
	{
		_isContextMenuOpen = true;
	}

	private void ContextMenu_Closed(object sender, RoutedEventArgs e)
	{
		_isContextMenuOpen = false;
	}

	private void Window_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
	{
		if (e.ChangedButton != MouseButton.Left) return;

		// 检查点击的元素：如果点击的是可交互控件（输入框、按钮、列表项、滚动条等），不触发窗口拖拽
		DependencyObject? current = e.OriginalSource as DependencyObject;
		while (current != null && current != this)
		{
			if (current is TextBox ||
			    current is Button ||
			    current is ListBoxItem ||
			    current is System.Windows.Controls.Primitives.ScrollBar ||
			    current is System.Windows.Controls.Primitives.Thumb)
			{
				return;
			}
			current = VisualTreeHelper.GetParent(current);
		}

		if (e.ButtonState == MouseButtonState.Pressed)
		{
			try
			{
				DragMove();
			}
			catch { }
		}
	}

	private static bool IsDescendantOf(DependencyObject? node, DependencyObject target)
	{
		while (node != null)
		{
			if (node == target) return true;
			node = VisualTreeHelper.GetParent(node);
		}
		return false;
	}

	private void Window_KeyDown(object sender, KeyEventArgs e)
	{
		if (e.Key == Key.Escape)
		{
			Hide();
			e.Handled = true;
		}
	}

	private void SearchInputBox_TextChanged(object sender, TextChangedEventArgs e)
	{
		string query = SearchInputBox.Text.Trim();
		PlaceholderText.Visibility = string.IsNullOrEmpty(query) ? Visibility.Visible : Visibility.Collapsed;
		ClearInputBtn.Visibility = string.IsNullOrEmpty(query) ? Visibility.Collapsed : Visibility.Visible;
		TriggerSearch(immediate: false);
	}

	private void TriggerSearch(bool immediate)
	{
		_debounceTimer?.Stop();
		if (immediate)
		{
			ExecuteSearchAsync();
		}
		else
		{
			_debounceTimer?.Start();
		}
	}

	private void DebounceTimer_Tick(object? sender, EventArgs e)
	{
		_debounceTimer?.Stop();
		ExecuteSearchAsync();
	}

	private async void ExecuteSearchAsync()
	{
		_searchCts?.Cancel();
		var cts = new CancellationTokenSource();
		_searchCts = cts;

		string query = SearchInputBox.Text.Trim();
		string category = _currentCategory;

		Stopwatch sw = Stopwatch.StartNew();

		try
		{
			var results = await EverythingService.SearchFilesAndFoldersAsync(query, category, 120);

			if (cts.Token.IsCancellationRequested) return;

			sw.Stop();
			double elapsedMs = sw.Elapsed.TotalMilliseconds;

			ResultsListBox.ItemsSource = results;
			if (results.Count > 0)
			{
				ResultsListBox.SelectedIndex = 0;
				EmptyResultsNotice.Visibility = Visibility.Collapsed;
				ResultsListBox.Visibility = Visibility.Visible;
			}
			else
			{
				ResultsListBox.Visibility = Visibility.Collapsed;
				EmptyResultsNotice.Visibility = Visibility.Visible;

				if (EverythingService.LastEngineState == EverythingService.SearchEngineState.EverythingPermissionBlocked)
				{
					EmptyNoticeEmoji.Text = "🛡️";
					EmptyNoticeTitle.Text = "Everything 正以管理员权限运行，通信受阻";
					EmptyNoticeSub.Text = "受 Windows UIPI 安全隔离限制，StarPie 需以管理员身份重启才能建立底层 0ms 极速通信";
					EmptyActionPanel.Visibility = Visibility.Visible;
					EmptyElevateBtn.Visibility = Visibility.Visible;
					EmptyLaunchEverythingBtn.Visibility = Visibility.Collapsed;
				}
				else if (EverythingService.LastEngineState == EverythingService.SearchEngineState.EverythingNotRunning)
				{
					EmptyNoticeEmoji.Text = "🚀";
					EmptyNoticeTitle.Text = string.IsNullOrEmpty(query) ? "未发现匹配文件" : $"原生引擎未找到关于 \"{query}\" 的结果";
					EmptyNoticeSub.Text = "检测到本地 Everything 未在后台运行。启动后可直接开启 0ms 全盘极速秒搜";
					EmptyActionPanel.Visibility = Visibility.Visible;
					EmptyElevateBtn.Visibility = Visibility.Collapsed;
					EmptyLaunchEverythingBtn.Visibility = Visibility.Visible;
				}
				else
				{
					EmptyNoticeEmoji.Text = "🔍";
					EmptyNoticeTitle.Text = string.IsNullOrEmpty(query) ? "未发现匹配文件" : $"未找到关于 \"{query}\" 的结果";
					EmptyNoticeSub.Text = "尝试换个关键词，或切换上方分类";
					EmptyActionPanel.Visibility = Visibility.Collapsed;
				}
			}

			UpdateEngineBadgeVisual(EverythingService.LastEngineState);

			string countPrefix = string.IsNullOrEmpty(query) ? "常用推荐" : $"找到 {results.Count} 项结果";
			StatusCountText.Text = $"{countPrefix} · {elapsedMs:F0} ms";
		}
		catch (OperationCanceledException)
		{
		}
		catch (Exception ex)
		{
			AppLogger.LogError($"QuickSearchWindow search error: {ex.Message}", ex);
		}
	}

	private void SearchInputBox_KeyDown(object sender, KeyEventArgs e)
	{
		if (e.Key == Key.Down)
		{
			if (ResultsListBox.Items.Count > 0)
			{
				int next = (ResultsListBox.SelectedIndex + 1) % ResultsListBox.Items.Count;
				ResultsListBox.SelectedIndex = next;
				ResultsListBox.ScrollIntoView(ResultsListBox.SelectedItem);
			}
			e.Handled = true;
		}
		else if (e.Key == Key.Up)
		{
			if (ResultsListBox.Items.Count > 0)
			{
				int prev = (ResultsListBox.SelectedIndex - 1 + ResultsListBox.Items.Count) % ResultsListBox.Items.Count;
				ResultsListBox.SelectedIndex = prev;
				ResultsListBox.ScrollIntoView(ResultsListBox.SelectedItem);
			}
			e.Handled = true;
		}
		else if (e.Key == Key.Enter)
		{
			if (ResultsListBox.SelectedItem is EverythingService.SearchResultItem item)
			{
				if (Keyboard.Modifiers.HasFlag(ModifierKeys.Shift))
				{
					RevealItemInExplorer(item);
				}
				else
				{
					OpenItem(item);
				}
			}
			e.Handled = true;
		}
	}

	private void ClearInputBtn_Click(object sender, RoutedEventArgs e)
	{
		SearchInputBox.Text = "";
		SearchInputBox.Focus();
	}

	private void CloseButton_Click(object sender, RoutedEventArgs e)
	{
		Hide();
		ResultsListBox.ItemsSource = null;
	}

	private void FilterChip_Click(object sender, RoutedEventArgs e)
	{
		if (sender is Button btn && btn.Tag is string cat)
		{
			_currentCategory = cat;
			UpdateFilterChipsStyle();
			TriggerSearch(immediate: true);
			SearchInputBox.Focus();
		}
	}

	private void UpdateFilterChipsStyle()
	{
		var chips = new[] { FilterAllBtn, FilterAppsBtn, FilterCadBtn, FilterDocsBtn, FilterVideoBtn, FilterFoldersBtn, FilterSystemBtn };
		foreach (var chip in chips)
		{
			if (chip == null) continue;
			bool isSelected = string.Equals(chip.Tag as string, _currentCategory, StringComparison.OrdinalIgnoreCase);
			if (isSelected)
			{
				chip.Background = (Brush)FindResource("AccentPrimaryBrush");
				chip.Foreground = (Brush)FindResource("AccentTextBrush");
				chip.BorderBrush = (Brush)FindResource("AccentHoverBrush");
			}
			else
			{
				chip.Background = (Brush)FindResource("ButtonDefaultBgBrush");
				chip.Foreground = (Brush)FindResource("ButtonDefaultFgBrush");
				chip.BorderBrush = (Brush)FindResource("ButtonDefaultBorderBrush");
			}
		}
	}

	private void ResultsListBox_MouseDoubleClick(object sender, MouseButtonEventArgs e)
	{
		if (ResultsListBox.SelectedItem is EverythingService.SearchResultItem item)
		{
			OpenItem(item);
		}
	}

	private void ItemOpen_Click(object sender, RoutedEventArgs e)
	{
		if (sender is Button btn && btn.Tag is EverythingService.SearchResultItem item)
		{
			OpenItem(item);
		}
	}

	private void ContextOpen_Click(object sender, RoutedEventArgs e)
	{
		if (ResultsListBox.SelectedItem is EverythingService.SearchResultItem item)
		{
			OpenItem(item);
		}
	}

	private void ContextReveal_Click(object sender, RoutedEventArgs e)
	{
		if (ResultsListBox.SelectedItem is EverythingService.SearchResultItem item)
		{
			RevealItemInExplorer(item);
		}
	}

	private void ContextRunAsAdmin_Click(object sender, RoutedEventArgs e)
	{
		if (ResultsListBox.SelectedItem is EverythingService.SearchResultItem item)
		{
			RunItemAsAdmin(item);
		}
	}

	private void ContextCopyPath_Click(object sender, RoutedEventArgs e)
	{
		if (ResultsListBox.SelectedItem is EverythingService.SearchResultItem item)
		{
			try
			{
				System.Windows.Clipboard.SetText(item.FullPath);
				ShowFeedbackBanner($"📋 已复制完整路径: {item.FullPath}", isWarning: false);
			}
			catch (Exception ex)
			{
				ShowFeedbackBanner($"复制失败: {ex.Message}", isWarning: true);
			}
		}
	}

	private void ContextCopyFileName_Click(object sender, RoutedEventArgs e)
	{
		if (ResultsListBox.SelectedItem is EverythingService.SearchResultItem item)
		{
			try
			{
				System.Windows.Clipboard.SetText(item.FileName);
				ShowFeedbackBanner($"📝 已复制文件名: {item.FileName}", isWarning: false);
			}
			catch (Exception ex)
			{
				ShowFeedbackBanner($"复制失败: {ex.Message}", isWarning: true);
			}
		}
	}

	private void OpenItem(EverythingService.SearchResultItem item)
	{
		try
		{
			Process.Start(new ProcessStartInfo
			{
				FileName = item.FullPath,
				UseShellExecute = true
			});
			Hide();
		}
		catch (Exception ex)
		{
			AppLogger.LogError($"Failed to open item '{item.FullPath}': {ex.Message}", ex);
			ShowFeedbackBanner($"无法打开目标文件: {ex.Message}", isWarning: true);
		}
	}

	private void RevealItemInExplorer(EverythingService.SearchResultItem item)
	{
		try
		{
			if (File.Exists(item.FullPath) || Directory.Exists(item.FullPath))
			{
				Process.Start("explorer.exe", $"/select,\"{item.FullPath}\"");
				Hide();
			}
			else
			{
				ShowFeedbackBanner("目标路径不存在或已被移动", isWarning: true);
			}
		}
		catch (Exception ex)
		{
			AppLogger.LogError($"Failed to reveal item in explorer '{item.FullPath}': {ex.Message}", ex);
			ShowFeedbackBanner($"定位失败: {ex.Message}", isWarning: true);
		}
	}

	private void RunItemAsAdmin(EverythingService.SearchResultItem item)
	{
		try
		{
			Process.Start(new ProcessStartInfo
			{
				FileName = item.FullPath,
				Verb = "runas",
				UseShellExecute = true
			});
			Hide();
		}
		catch (Exception ex)
		{
			AppLogger.LogError($"Failed to run as admin '{item.FullPath}': {ex.Message}", ex);
			ShowFeedbackBanner($"以管理员身份启动失败: {ex.Message}", isWarning: true);
		}
	}

	private void ShowFeedbackBanner(string message, bool isWarning = false)
	{
		ActionFeedbackText.Text = message;
		if (isWarning)
		{
			ActionFeedbackBanner.Background = new SolidColorBrush(Color.FromArgb(0x18, 0xF5, 0x9E, 0x0B));
			ActionFeedbackBanner.BorderBrush = new SolidColorBrush(Color.FromArgb(0x60, 0xF5, 0x9E, 0x0B));
			ActionFeedbackText.Foreground = new SolidColorBrush(Color.FromRgb(0xF5, 0x9E, 0x0B));
			ActionFeedbackIcon.Text = "⚠️";
		}
		else
		{
			ActionFeedbackBanner.Background = new SolidColorBrush(Color.FromArgb(0x18, 0x10, 0xB9, 0x81));
			ActionFeedbackBanner.BorderBrush = new SolidColorBrush(Color.FromArgb(0x60, 0x10, 0xB9, 0x81));
			ActionFeedbackText.Foreground = new SolidColorBrush(Color.FromRgb(0x10, 0xB9, 0x81));
			ActionFeedbackIcon.Text = "✨";
		}

		ActionFeedbackBanner.Visibility = Visibility.Visible;
		_feedbackTimer?.Stop();
		_feedbackTimer?.Start();
	}

	private void CloseFeedbackBtn_Click(object sender, RoutedEventArgs e)
	{
		ActionFeedbackBanner.Visibility = Visibility.Collapsed;
		_feedbackTimer?.Stop();
	}

	private void UpdateEngineBadgeVisual(EverythingService.SearchEngineState state)
	{
		if (EngineStatusBadge == null || EngineStatusIcon == null || EngineStatusText == null) return;

		switch (state)
		{
			case EverythingService.SearchEngineState.EverythingConnected:
				EngineStatusBadge.Background = new SolidColorBrush(Color.FromArgb(0x18, 0x10, 0xB9, 0x81));
				EngineStatusBadge.BorderBrush = new SolidColorBrush(Color.FromArgb(0x50, 0x10, 0xB9, 0x81));
				EngineStatusIcon.Text = "⚡";
				EngineStatusIcon.Foreground = new SolidColorBrush(Color.FromRgb(0x10, 0xB9, 0x81));
				EngineStatusText.Text = "Everything 极速";
				EngineStatusText.Foreground = new SolidColorBrush(Color.FromRgb(0x10, 0xB9, 0x81));
				EngineStatusBadge.ToolTip = "Everything 数据库直连就绪 (IPC 0ms 响应)";
				break;

			case EverythingService.SearchEngineState.EverythingPermissionBlocked:
				EngineStatusBadge.Background = new SolidColorBrush(Color.FromArgb(0x22, 0xF5, 0x9E, 0x0B));
				EngineStatusBadge.BorderBrush = new SolidColorBrush(Color.FromArgb(0x60, 0xF5, 0x9E, 0x0B));
				EngineStatusIcon.Text = "⚠️";
				EngineStatusIcon.Foreground = new SolidColorBrush(Color.FromRgb(0xF5, 0x9E, 0x0B));
				EngineStatusText.Text = "Everything 权限受阻 (点击提权)";
				EngineStatusText.Foreground = new SolidColorBrush(Color.FromRgb(0xF5, 0x9E, 0x0B));
				EngineStatusBadge.ToolTip = "Everything 正以管理员权限运行。受 Windows UIPI 安全隔离限制，StarPie 需以管理员身份运行才能直连 0ms 秒搜。\n点击立即以管理员身份重启 StarPie。";
				break;

			case EverythingService.SearchEngineState.EverythingNotRunning:
				EngineStatusBadge.Background = new SolidColorBrush(Color.FromArgb(0x18, 0xF9, 0x73, 0x16));
				EngineStatusBadge.BorderBrush = new SolidColorBrush(Color.FromArgb(0x50, 0xF9, 0x73, 0x16));
				EngineStatusIcon.Text = "🐢";
				EngineStatusIcon.Foreground = new SolidColorBrush(Color.FromRgb(0xF9, 0x73, 0x16));
				EngineStatusText.Text = "原生并发 (点击启动 Everything)";
				EngineStatusText.Foreground = new SolidColorBrush(Color.FromRgb(0xF9, 0x73, 0x16));
				EngineStatusBadge.ToolTip = "未检测到 Everything 正在运行，当前使用内置原生引擎。\n点击立即启动本地 Everything 以享受 0ms 全盘秒搜。";
				break;

			case EverythingService.SearchEngineState.NativeOnly:
			default:
				EngineStatusBadge.Background = new SolidColorBrush(Color.FromArgb(0x14, 0x64, 0x74, 0x8B));
				EngineStatusBadge.BorderBrush = new SolidColorBrush(Color.FromArgb(0x40, 0x64, 0x74, 0x8B));
				EngineStatusIcon.Text = "📁";
				EngineStatusIcon.Foreground = new SolidColorBrush(Color.FromRgb(0x64, 0x74, 0x8B));
				EngineStatusText.Text = "内置原生引擎";
				EngineStatusText.Foreground = new SolidColorBrush(Color.FromRgb(0x64, 0x74, 0x8B));
				EngineStatusBadge.ToolTip = "当前使用 StarPie 内置自包含原生并发引擎检索文件与常用应用";
				break;
		}
	}

	private void EngineStatusBadge_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
	{
		e.Handled = true;
		var state = EverythingService.LastEngineState;
		if (state == EverythingService.SearchEngineState.EverythingPermissionBlocked)
		{
			PromptElevateRestart();
		}
		else if (state == EverythingService.SearchEngineState.EverythingNotRunning)
		{
			LaunchEverythingAndRefresh();
		}
		else if (state == EverythingService.SearchEngineState.EverythingConnected)
		{
			ShowFeedbackBanner("⚡ Everything 数据库直连就绪 (IPC 0ms 响应)", isWarning: false);
		}
		else
		{
			ShowFeedbackBanner("📁 当前使用 StarPie 内置原生轻量并发引擎", isWarning: false);
		}
	}

	private void EmptyElevateBtn_Click(object sender, RoutedEventArgs e)
	{
		PromptElevateRestart();
	}

	private void EmptyLaunchEverythingBtn_Click(object sender, RoutedEventArgs e)
	{
		LaunchEverythingAndRefresh();
	}

	private void PromptElevateRestart()
	{
		var result = MessageBox.Show(
			"Everything 当前正在以管理员权限运行。\n\n受 Windows UIPI (用户界面特权隔离) 机制限制，StarPie 需要以管理员身份运行才能建立底层 IPC 通信，实现 0ms 毫秒级秒搜。\n\n是否立即以管理员身份重启 StarPie？",
			"StarPie - 管理员提权同步 Everything",
			MessageBoxButton.YesNo,
			MessageBoxImage.Information);

		if (result == MessageBoxResult.Yes)
		{
			App.RestartElevated();
		}
	}

	private void LaunchEverythingAndRefresh()
	{
		bool launched = EverythingService.TryLaunchEverything();
		if (launched)
		{
			ShowFeedbackBanner("🚀 已启动本地 Everything，正在连接数据库...", isWarning: false);
			var timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(700) };
			timer.Tick += (s, ev) =>
			{
				timer.Stop();
				TriggerSearch(immediate: true);
			};
			timer.Start();
		}
		else
		{
			ShowFeedbackBanner("未找到本地 Everything.exe，请确认已安装或放置在桌面", isWarning: true);
		}
	}
}

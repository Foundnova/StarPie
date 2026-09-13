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
	private CancellationTokenSource? _searchCts;
	private bool _isEverythingActive;

	public QuickSearchWindow()
	{
		InitializeComponent();

		_debounceTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(75) };
		_debounceTimer.Tick += DebounceTimer_Tick;

		_feedbackTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(3.5) };
		_feedbackTimer.Tick += (s, e) =>
		{
			ActionFeedbackBanner.Visibility = Visibility.Collapsed;
			_feedbackTimer.Stop();
		};
	}

	public static void ShowOrActivate()
	{
		if (_instance == null || !_instance.IsLoaded)
		{
			_instance = new QuickSearchWindow();
		}
		_instance.ShowAndPosition();
	}

	private void ShowAndPosition()
	{
		AppThemeManager.ApplyTheme(this, ConfigManager.CurrentConfig?.AppTheme ?? "System");

		// 居中偏上（经典 Spotlight / 极速搜索最佳视觉热区）
		var primaryScreen = System.Windows.Forms.Screen.PrimaryScreen;
		if (primaryScreen != null)
		{
			var bounds = primaryScreen.WorkingArea;
			Left = bounds.Left + (bounds.Width - Width) / 2;
			Top = bounds.Top + bounds.Height * 0.16;
		}

		UpdateEverythingStatus();
		SearchInputBox.Text = "";
		_currentCategory = "All";
		UpdateFilterChipsStyle();
		TriggerSearch(immediate: true);

		Show();
		Activate();
		SearchInputBox.Focus();
	}

	private void Window_Loaded(object sender, RoutedEventArgs e)
	{
		UpdateEverythingStatus();
		TriggerSearch(immediate: true);
	}

	private void Window_Deactivated(object? sender, EventArgs e)
	{
		// 鼠标点击搜索框外部区域时自动平滑隐藏，避免干扰用户正常工作
		Hide();
	}

	private void Window_KeyDown(object sender, KeyEventArgs e)
	{
		if (e.Key == Key.Escape)
		{
			Hide();
			e.Handled = true;
		}
	}

	private void UpdateEverythingStatus()
	{
		bool dllOk = EverythingService.IsDllAvailable();
		bool running = EverythingService.IsEverythingRunning();
		_isEverythingActive = dllOk && running;

		if (_isEverythingActive)
		{
			StatusDot.Fill = new SolidColorBrush(Color.FromRgb(0x10, 0xB9, 0x81)); // 翠绿
			StatusChipText.Text = "⚡ Everything 已连接";
			StatusChipText.Foreground = (Brush)FindResource("AccentPrimaryBrush");
			EverythingStatusChip.ToolTip = "Everything IPC 极速引擎正常运行中 (响应时间 < 2ms)";
		}
		else
		{
			StatusDot.Fill = new SolidColorBrush(Color.FromRgb(0xF5, 0x9E, 0x0B)); // 暖黄警告
			StatusChipText.Text = "⚠️ Everything 未运行 (点击启动)";
			StatusChipText.Foreground = new SolidColorBrush(Color.FromRgb(0xF5, 0x9E, 0x0B));
			EverythingStatusChip.ToolTip = "未检测到后台运行的 Everything。点击可尝试一键唤起，或使用本地优雅降级检索。";
		}
	}

	private void EverythingStatusChip_Click(object sender, MouseButtonEventArgs e)
	{
		if (!_isEverythingActive)
		{
			bool launched = EverythingService.TryLaunchEverything();
			if (launched)
			{
				ShowFeedbackBanner("🚀 正在尝试唤起 Everything，请稍候...", isWarning: false);
				var checkTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1.2) };
				checkTimer.Tick += (s, ev) =>
				{
					checkTimer.Stop();
					UpdateEverythingStatus();
					TriggerSearch(immediate: true);
				};
				checkTimer.Start();
			}
			else
			{
				ShowFeedbackBanner("💡 未检测到本地 Everything.exe 安装，当前启用本地降级扫描模式。", isWarning: true);
			}
		}
		else
		{
			ShowFeedbackBanner("⚡ Everything IPC 极速检索核心已就绪", isWarning: false);
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
				EmptyNoticeTitle.Text = string.IsNullOrEmpty(query) ? "未发现匹配文件" : $"未找到关于 \"{query}\" 的结果";
				EmptyNoticeSub.Text = _isEverythingActive ? "尝试换个关键词，或切换上方分类" : "Everything 未运行，降级模式仅扫描桌面与常用目录";
				EmptyResultsNotice.Visibility = Visibility.Visible;
				ResultsListBox.Visibility = Visibility.Collapsed;
			}

			string engineTag = _isEverythingActive ? "Everything 极速索引" : "本地降级搜索";
			StatusCountText.Text = $"找到 {results.Count} 项结果 · {elapsedMs:F1} ms ({engineTag})";
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
		var chips = new[] { FilterAllBtn, FilterAppsBtn, FilterCadBtn, FilterDocsBtn, FilterFoldersBtn, FilterSystemBtn };
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
}

using System;
using System.CodeDom.Compiler;
using System.Collections;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.Drawing;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Security.Principal;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Forms;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Markup;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Effects;
using System.Windows.Media.Imaging;
using System.Windows.Resources;
using System.Windows.Shapes;
using System.Windows.Threading;
using Microsoft.Win32;

namespace WinPieGestures;

public partial class SettingsWindow : Window
{
	private const double SidebarExpandedWidth = 230.0;

	private const double SidebarCollapsedWidth = 68.0;

	private bool _isSidebarCollapsed = true;

	private int _selectedLayoutTier = 1; // 1: 主轮盘, 2: 二级级联轮盘

	private int _selectedLayoutSlotIndex = -1;

	private int _selectedLayoutSubSlotIndex = -1;

	private string GetDirectionDisplayName(int index, int totalCount)
	{
		string[] dirArray = totalCount switch
		{
			4 => Directions4,
			12 => Directions12,
			_ => Directions8,
		};
		return (index >= 0 && index < dirArray.Length) ? dirArray[index] : $"扇区 {index + 1}";
	}

	private ActionItem? GetCurrentEditingAction()
	{
		WheelProfile? profile = _selectedProfile ?? ConfigManager.CurrentConfig?.Profiles.FirstOrDefault();
		if (profile?.Actions == null || _selectedLayoutSlotIndex < 0 || _selectedLayoutSlotIndex >= profile.Actions.Count)
		{
			return null;
		}
		ActionItem parentAction = profile.Actions[_selectedLayoutSlotIndex];
		if (_selectedLayoutTier == 2 && _selectedLayoutSubSlotIndex >= 0)
		{
			if (parentAction.SubActions != null && _selectedLayoutSubSlotIndex < parentAction.SubActions.Count)
			{
				return parentAction.SubActions[_selectedLayoutSubSlotIndex];
			}
			return null;
		}
		return parentAction;
	}

	private bool _isRecordingTrigger;

	private System.Windows.Media.Brush? _originalBadgeBorderBrush;

	private NotifyIcon _notifyIcon;

	private bool _isClosingFromTray;

	private WheelProfile? _selectedProfile;

	private readonly ObservableCollection<SlotViewModel> _slotViewModels = new ObservableCollection<SlotViewModel>();

	private bool _isUpdatingUi = true;

	private bool _isRenderingPreview;

	private readonly List<System.Windows.Shapes.Path> _previewSectorPaths = new List<System.Windows.Shapes.Path>();

	private readonly List<TranslateTransform> _previewTransforms = new List<TranslateTransform>();

	private readonly List<double> _previewAngles = new List<double>();

	private readonly List<System.Windows.Shapes.Path> _previewSubSectorPaths = new List<System.Windows.Shapes.Path>();

	private readonly List<TranslateTransform> _previewSubTransforms = new List<TranslateTransform>();

	private readonly List<int> _previewSubParentIndices = new List<int>();

	private readonly List<int> _previewSubIndices = new List<int>();

	private readonly List<double> _previewSubAngles = new List<double>();

	private System.Windows.Media.Brush? _previewSubDefaultBrush;

	private System.Windows.Media.Brush? _previewSubHighlightBrush;

	private System.Windows.Media.Brush? _previewSubBorderBrush;

	private System.Windows.Media.Brush? _previewSubHighlightBorderBrush;

	private System.Windows.Media.Brush? _previewSubTextBrush;

	private readonly List<Grid> _previewSubContainers = new List<Grid>();

	private IRadialStyleRenderer? _previewStyleRenderer;

	private IRadialStyleRenderer? _previewSubStyleRenderer;

	private System.Windows.Media.Brush? _previewDefaultBrush;

	private System.Windows.Media.Brush? _previewHighlightBrush;

	private System.Windows.Media.Brush? _previewBorderBrush;

	private System.Windows.Media.Brush? _previewHighlightBorderBrush;

	private System.Windows.Media.Brush? _previewTextBrush;

	private System.Windows.Media.Brush? _previewCoreBgBrush;

	private System.Windows.Media.Brush? _previewCoreBorderBrush;

	private Ellipse? _previewCoreCircle;

	private Grid? _previewCoreGrid;

	private ScaleTransform? _previewCoreScale;

	private System.Windows.Shapes.Path? _previewExitIcon;

	private UIElement? _previewCoreIconElement;

	private Visibility _previewCoreIconDefaultVisibility = Visibility.Collapsed;

	private double _previewCoreIconDefaultOpacity = 1.0;

	private Effect? _previewCoreIconDefaultEffect;

	private bool _previewCoreUsesCustomImage;

	private Ellipse? _previewCoreSelectionOverlay;

	private TextBlock? _previewCoreSelectionText;

	// Tab 2 Mappings Focus Editor & Canvas Interactivity
	private int _selectedSlotIndex = 0; // -1: Center Core, 0..11: Sector slot
	private int? _selectedSubActionIndex = null; // null: Primary slot / Center Core; 0..3: Secondary subaction
	private bool _isUpdatingFocusUi = false;
	private Point? _mappingsDragStartPos = null;
	private int _dragSourceSlotIndex = -999; // -1: Center Core, >=0: Sector slot
	private bool _isDraggingSlot = false;
	private Point? _mappingsPanStartPoint = null;
	private Point _mappingsPanStartTranslate = default;
	private readonly List<System.Windows.Shapes.Path> _mappingsSectorPaths = new List<System.Windows.Shapes.Path>();
	private readonly List<System.Windows.Shapes.Path> _mappingsSubSectorPaths = new List<System.Windows.Shapes.Path>();
	private readonly List<Tuple<int, int>> _mappingsSubSectorKeys = new List<Tuple<int, int>>();

	private int _lastHoveredSector = -2;

	private int _lastHoveredSubIndex = -2;

	private ReleaseInfo? _latestReleaseInfo = null;
	private CancellationTokenSource? _downloadCts = null;
	private string? _downloadedZipPath = null;

	private static readonly string[] Directions4 = new string[4] { "右 (E / 0°)", "下 (S / 90°)", "左 (W / 180°)", "上 (N / 270°)" };

	private static readonly string[] Directions8 = new string[8] { "右 (E / 0°)", "右下 (SE / 45°)", "下 (S / 90°)", "左下 (SW / 135°)", "左 (W / 180°)", "左上 (NW / 225°)", "上 (N / 270°)", "右上 (NE / 315°)" };

	private static readonly string[] Directions12 = new string[12]
	{
		"右 3点钟 (E / 0°)", "右下 4点钟 (30°)", "右下 5点钟 (60°)", "下 6点钟 (S / 90°)", "左下 7点钟 (120°)", "左下 8点钟 (150°)", "左 9点钟 (W / 180°)", "左上 10点钟 (210°)", "左上 11点钟 (240°)", "上 12点钟 (N / 270°)",
		"右上 1点钟 (300°)", "右上 2点钟 (330°)"
	};

	private static readonly ActionItem[] DefaultPresets4 = new ActionItem[4]
	{
		new ActionItem
		{
			Type = "Hotkey",
			Name = "复制 (Copy)",
			Parameter = "Ctrl+C",
			IconKey = "Copy"
		},
		new ActionItem
		{
			Type = "System",
			Name = "显示桌面 (Desktop)",
			Parameter = "ShowDesktop",
			IconKey = "ShowDesktop"
		},
		new ActionItem
		{
			Type = "Hotkey",
			Name = "粘贴 (Paste)",
			Parameter = "Ctrl+V",
			IconKey = "Paste"
		},
		new ActionItem
		{
			Type = "System",
			Name = "关闭窗口 (Close)",
			Parameter = "CloseWindow",
			IconKey = "CloseWindow"
		}
	};

	private static readonly ActionItem[] DefaultPresets12 = new ActionItem[12]
	{
		new ActionItem
		{
			Type = "Hotkey",
			Name = "复制 (Copy)",
			Parameter = "Ctrl+C",
			IconKey = "Copy"
		},
		new ActionItem
		{
			Type = "Hotkey",
			Name = "剪切 (Cut)",
			Parameter = "Ctrl+X",
			IconKey = "Cut"
		},
		new ActionItem
		{
			Type = "System",
			Name = "锁定电脑 (Lock)",
			Parameter = "Lock",
			IconKey = "Lock"
		},
		new ActionItem
		{
			Type = "System",
			Name = "显示桌面 (Desktop)",
			Parameter = "ShowDesktop",
			IconKey = "ShowDesktop"
		},
		new ActionItem
		{
			Type = "System",
			Name = "任务视图 (TaskView)",
			Parameter = "TaskView",
			IconKey = "TaskView"
		},
		new ActionItem
		{
			Type = "System",
			Name = "屏幕截图 (Screenshot)",
			Parameter = "Screenshot",
			IconKey = "Screenshot"
		},
		new ActionItem
		{
			Type = "Hotkey",
			Name = "粘贴 (Paste)",
			Parameter = "Ctrl+V",
			IconKey = "Paste"
		},
		new ActionItem
		{
			Type = "Hotkey",
			Name = "撤销 (Undo)",
			Parameter = "Ctrl+Z",
			IconKey = "Undo"
		},
		new ActionItem
		{
			Type = "System",
			Name = "音量减小 (Vol-)",
			Parameter = "VolumeDown",
			IconKey = "VolumeDown"
		},
		new ActionItem
		{
			Type = "System",
			Name = "关闭窗口 (Close)",
			Parameter = "CloseWindow",
			IconKey = "CloseWindow"
		},
		new ActionItem
		{
			Type = "System",
			Name = "音量增加 (Vol+)",
			Parameter = "VolumeUp",
			IconKey = "VolumeUp"
		},
		new ActionItem
		{
			Type = "System",
			Name = "任务管理器 (TaskMgr)",
			Parameter = "TaskManager",
			IconKey = "TaskManager"
		}
	};

	private ToolStripMenuItem? _pauseResumeMenuItem;

	private DispatcherTimer? _autoSaveDebounceTimer;

	private bool _isChangingSectorCount;

	private bool _previewRenderPending;

	public SettingsWindow()
	{
		_isUpdatingUi = true;
		ConfigManager.LoadConfig();
		InitializeComponent();
		InitializeTrayIcon();
		string text = "v" + (Assembly.GetExecutingAssembly().GetName().Version?.ToString(3) ?? "1.6.2");
		if (SidebarVersionText != null)
		{
			SidebarVersionText.Text = text;
		}
		if (AboutVersionBadgeText != null)
		{
			AboutVersionBadgeText.Text = text;
		}
		try
		{
			AppThemeManager.ApplyTheme(this, ConfigManager.CurrentConfig.AppTheme ?? "System");
			ApplySidebarLayout();
			LoadConfigToUi();
			SlotsItemsControl.ItemsSource = _slotViewModels;
			bool flag4 = IsRunningAsAdmin();
			UacWarningCard.Visibility = (flag4 ? Visibility.Collapsed : Visibility.Visible);
			RefreshSlots();
		}
		finally
		{
			_isUpdatingUi = false;
		}
		base.Loaded += delegate
		{
			ApplySidebarLayout();
			if (AppearanceSettingsGrid.Visibility == Visibility.Visible)
			{
				RenderLiveWheelPreview();
			}
			MemoryOptimizer.TrimMemory();
			if (ConfigManager.CurrentConfig?.AutoCheckUpdate == true)
			{
				Task.Run(async () =>
				{
					await Task.Delay(2500);
					await Dispatcher.InvokeAsync(() => CheckForUpdateInternalAsync(silent: true));
				});
			}
		};
	}

	private void SidebarToggleButton_Click(object sender, RoutedEventArgs e)
	{
		_isSidebarCollapsed = !_isSidebarCollapsed;
		ApplySidebarLayout();
	}

	private void ApplySidebarLayout()
	{
		if (SidebarColumn == null || SidebarBorder == null || SidebarBrandGrid == null || SidebarBrandTextPanel == null || SidebarFooterPanel == null || SidebarToggleIcon == null || SidebarToggleButton == null)
		{
			return;
		}
		bool isCollapsed = _isSidebarCollapsed;
		SidebarColumn.Width = new GridLength(isCollapsed ? SidebarCollapsedWidth : SidebarExpandedWidth);
		SidebarBorder.Padding = isCollapsed ? new Thickness(10, 20, 10, 15) : new Thickness(16, 20, 16, 15);
		SidebarToggleButton.HorizontalAlignment = isCollapsed ? HorizontalAlignment.Center : HorizontalAlignment.Right;
		SidebarBrandGrid.HorizontalAlignment = isCollapsed ? HorizontalAlignment.Center : HorizontalAlignment.Stretch;
		SidebarBrandTextPanel.Visibility = isCollapsed ? Visibility.Collapsed : Visibility.Visible;
		SidebarFooterPanel.Visibility = isCollapsed ? Visibility.Collapsed : Visibility.Visible;
		SidebarToggleIcon.Data = Geometry.Parse(isCollapsed ? "M10,6 L16,12 L10,18" : "M14,6 L8,12 L14,18");
		string toggleText = I18n.T(isCollapsed ? "SidebarExpand" : "SidebarCollapse");
		SidebarToggleButton.ToolTip = toggleText;
		System.Windows.Automation.AutomationProperties.SetName(SidebarToggleButton, toggleText);

		System.Windows.Controls.RadioButton[] navigationButtons = new System.Windows.Controls.RadioButton[5] { NavTab0, NavTab1, NavTab2, NavTab3, NavTab4 };
		TextBlock[] navigationTexts = new TextBlock[5] { NavTab0Text, NavTab1Text, NavTab2Text, NavTab3Text, NavTab4Text };
		for (int i = 0; i < navigationButtons.Length; i++)
		{
			if (navigationButtons[i] == null) continue;
			navigationButtons[i].Padding = isCollapsed ? new Thickness(10) : new Thickness(14, 10, 14, 10);
			if (navigationButtons[i].Content is StackPanel sp)
			{
				sp.HorizontalAlignment = isCollapsed ? HorizontalAlignment.Center : HorizontalAlignment.Left;
				if (sp.Children.Count > 0 && sp.Children[0] is FrameworkElement iconElem)
				{
					iconElem.Margin = isCollapsed ? new Thickness(0) : new Thickness(0, 0, 14, 0);
				}
			}
			if (navigationTexts[i] != null)
			{
				navigationTexts[i].Visibility = isCollapsed ? Visibility.Collapsed : Visibility.Visible;
			}
		}
	}

	private void LoadConfigToUi()
	{
		ProfilesListBox.ItemsSource = null;
		ProfilesListBox.ItemsSource = ConfigManager.CurrentConfig.Profiles;
		if (MappingsProfileComboBox != null)
		{
			MappingsProfileComboBox.ItemsSource = null;
			MappingsProfileComboBox.ItemsSource = ConfigManager.CurrentConfig.Profiles;
			MappingsProfileComboBox.SelectedItem = _selectedProfile ?? ConfigManager.CurrentConfig.Profiles.FirstOrDefault();
		}
		if (FocusActionTypeComboBox != null && FocusActionTypeComboBox.ItemsSource == null)
		{
			FocusActionTypeComboBox.ItemsSource = SlotViewModel.LocalizedActionTypes;
		}
		if (FocusCommandTerminalComboBox != null && FocusCommandTerminalComboBox.ItemsSource == null)
		{
			FocusCommandTerminalComboBox.ItemsSource = SlotViewModel.LocalizedTerminals;
		}
		if (FocusSystemPresetComboBox != null && FocusSystemPresetComboBox.ItemsSource == null)
		{
			FocusSystemPresetComboBox.ItemsSource = SlotViewModel.SystemPresetList;
		}
		UpdateTriggerBadgeDisplay();
		HookRawInputForSensorAndRecorder();

		// Threshold & Outer Escape
		ThresholdSlider.Value = ConfigManager.CurrentConfig.DragThreshold;
		ThresholdValueLabel.Text = ConfigManager.CurrentConfig.DragThreshold.ToString("0");
		if (LongPressTriggerCheckBox != null)
		{
			LongPressTriggerCheckBox.IsChecked = ConfigManager.CurrentConfig.LongPressTrigger;
		}
		if (LongPressDelaySlider != null)
		{
			LongPressDelaySlider.Value = ConfigManager.CurrentConfig.LongPressDelayMs > 0.0 ? ConfigManager.CurrentConfig.LongPressDelayMs : 450.0;
		}
		if (LongPressDelayLabel != null)
		{
			LongPressDelayLabel.Text = $"{LongPressDelaySlider.Value:0} ms";
		}
		if (LongPressDelayPanel != null)
		{
			LongPressDelayPanel.Visibility = ConfigManager.CurrentConfig.LongPressTrigger ? Visibility.Visible : Visibility.Collapsed;
		}
		if (GestureEnabledCheckBox != null)
		{
			GestureEnabledCheckBox.IsChecked = ConfigManager.CurrentConfig.GestureEnabled;
		}
		SetComboBoxSelectedValue(GestureTriggerButtonComboBox, ConfigManager.CurrentConfig.GestureTriggerButton ?? "MiddleButton");
		SetComboBoxSelectedValue(GestureHintPlacementComboBox, ConfigManager.CurrentConfig.GestureHintPlacement ?? "Auto");
		if (GestureSensitivitySlider != null)
		{
			GestureSensitivitySlider.Value = ConfigManager.CurrentConfig.GestureSegmentSensitivity > 0.0 ? ConfigManager.CurrentConfig.GestureSegmentSensitivity : 16.0;
		}
		if (GestureSensitivityLabel != null)
		{
			GestureSensitivityLabel.Text = $"{GestureSensitivitySlider.Value:0} px";
		}
		RefreshGestureMappings();
		RefreshCancelActionEditor();
		if (TileExcludeProcessesTextBox != null)
		{
			TileExcludeProcessesTextBox.Text = ConfigManager.CurrentConfig.TileExcludeProcesses ?? "";
		}
		RefreshTileCycleList();
		if (TileIncludeMinimizedCheckBox != null)
		{
			TileIncludeMinimizedCheckBox.IsChecked = ConfigManager.CurrentConfig.TileIncludeMinimized;
		}
		if (EnableOuterEscapeCheckBox != null)
		{
			EnableOuterEscapeCheckBox.IsChecked = ConfigManager.CurrentConfig.EnableOuterEscapeCancel;
		}
		if (OuterEscapeDistancePanel != null)
		{
			OuterEscapeDistancePanel.Visibility = ((!ConfigManager.CurrentConfig.EnableOuterEscapeCancel) ? Visibility.Collapsed : Visibility.Visible);
		}
		if (OuterEscapeDistanceSlider != null)
		{
			OuterEscapeDistanceSlider.Value = ((ConfigManager.CurrentConfig.OuterEscapeDistance > 0.0) ? ConfigManager.CurrentConfig.OuterEscapeDistance : 190.0);
		}
		if (OuterEscapeDistanceLabel != null)
		{
			OuterEscapeDistanceLabel.Text = $"{OuterEscapeDistanceSlider?.Value ?? 190.0:0} px";
		}

		// Animation Speed
		string animSpeed = ConfigManager.CurrentConfig.AnimationSpeed ?? "Balanced";
		double animVal = ((ConfigManager.CurrentConfig.CustomAnimationDurationMs > 0.0) ? ConfigManager.CurrentConfig.CustomAnimationDurationMs : 80.0);
		if (AnimSpeedSlider != null)
		{
			AnimSpeedSlider.Value = animVal;
		}
		if (AnimSpeedSliderLabel != null)
		{
			AnimSpeedSliderLabel.Text = $"{animVal:0} ms";
		}
		switch (animSpeed)
		{
		case "Elegant":
			if (AnimSpeedElegantRadio != null) AnimSpeedElegantRadio.IsChecked = true;
			break;
		case "Fast":
			if (AnimSpeedFastRadio != null) AnimSpeedFastRadio.IsChecked = true;
			break;
		case "Custom":
			if (AnimSpeedCustomRadio != null) AnimSpeedCustomRadio.IsChecked = true;
			break;
		default:
			if (AnimSpeedBalancedRadio != null) AnimSpeedBalancedRadio.IsChecked = true;
			break;
		}

		// App Theme & Presets
		SetComboBoxSelectedValue(AppThemeComboBox, ConfigManager.CurrentConfig.AppTheme ?? "System");
		ReloadThemePresets();
		SetComboBoxSelectedValue(ThemeComboBox, ConfigManager.CurrentConfig.Theme);
		SetComboBoxSelectedValue(UiStyleComboBox, ConfigManager.CurrentConfig.UiStyle);

		// Custom Colors
		CustomSectorBgTextBox.Text = ConfigManager.CurrentConfig.CustomSectorBg;
		CustomSectorBorderTextBox.Text = ConfigManager.CurrentConfig.CustomSectorBorder;
		CustomHighlightBgTextBox.Text = ConfigManager.CurrentConfig.CustomHighlightBg;
		CustomHighlightBorderTextBox.Text = ConfigManager.CurrentConfig.CustomHighlightBorder;
		CustomTextTextBox.Text = ConfigManager.CurrentConfig.CustomText;
		bool isCustomTheme = (ConfigManager.CurrentConfig.Theme ?? "").StartsWith("CustomPreset_");
		if (CustomColorsPanel != null)
		{
			CustomColorsPanel.Visibility = Visibility.Visible;
		}
		if (((ConfigManager.CurrentConfig.Theme == "Custom") || isCustomTheme) && CustomColorExpander != null)
		{
			CustomColorExpander.IsExpanded = true;
		}
		if (RenameCustomColorPresetButton != null)
		{
			RenameCustomColorPresetButton.Visibility = ((!isCustomTheme) ? Visibility.Collapsed : Visibility.Visible);
		}
		if (DeleteCustomColorPresetButton != null)
		{
			DeleteCustomColorPresetButton.Visibility = ((!isCustomTheme) ? Visibility.Collapsed : Visibility.Visible);
		}
		if (DeletePresetInPanelButton != null)
		{
			DeletePresetInPanelButton.Visibility = ((!isCustomTheme) ? Visibility.Collapsed : Visibility.Visible);
		}
		if (SavePresetChangesButton != null)
		{
			SavePresetChangesButton.Content = (isCustomTheme ? I18n.T("SavePresetChangesButton") : I18n.T("SaveAsNewPresetButton"));
		}

		// Primary Glow
		SetComboBoxSelectedValue(HighlightGlowPresetComboBox, ConfigManager.CurrentConfig.HighlightGlowPreset ?? "Auto");
		HighlightGlowColorTextBox.Text = ConfigManager.CurrentConfig.HighlightGlowColor ?? "";
		HighlightGlowRadiusSlider.Value = ((ConfigManager.CurrentConfig.HighlightGlowRadius > 0.0) ? ConfigManager.CurrentConfig.HighlightGlowRadius : 24.0);
		HighlightGlowRadiusLabel.Text = $"{HighlightGlowRadiusSlider.Value:0} px";
		HighlightGlowOpacitySlider.Value = ((ConfigManager.CurrentConfig.HighlightGlowOpacity >= 0.0) ? ConfigManager.CurrentConfig.HighlightGlowOpacity : 0.85) * 100.0;
		HighlightGlowOpacityLabel.Text = $"{HighlightGlowOpacitySlider.Value:0}%";
		CustomHighlightGlowPanel.Visibility = ((!(ConfigManager.CurrentConfig.HighlightGlowPreset == "Custom") && string.IsNullOrEmpty(ConfigManager.CurrentConfig.HighlightGlowColor)) ? Visibility.Collapsed : Visibility.Visible);

		// Secondary Glow
		SetComboBoxSelectedValue(SubHighlightGlowPresetComboBox, ConfigManager.CurrentConfig.SubWheelHighlightGlowPreset ?? "FollowPrimary");
		if (SubHighlightGlowColorTextBox != null)
		{
			SubHighlightGlowColorTextBox.Text = ConfigManager.CurrentConfig.SubWheelHighlightGlowColor ?? "";
		}
		if (SubHighlightGlowRadiusSlider != null)
		{
			SubHighlightGlowRadiusSlider.Value = ((ConfigManager.CurrentConfig.SubWheelHighlightGlowRadius > 0.0) ? ConfigManager.CurrentConfig.SubWheelHighlightGlowRadius : 24.0);
			if (SubHighlightGlowRadiusLabel != null)
			{
				SubHighlightGlowRadiusLabel.Text = $"{SubHighlightGlowRadiusSlider.Value:0} px";
			}
		}
		if (SubHighlightGlowOpacitySlider != null)
		{
			SubHighlightGlowOpacitySlider.Value = ((ConfigManager.CurrentConfig.SubWheelHighlightGlowOpacity >= 0.0) ? ConfigManager.CurrentConfig.SubWheelHighlightGlowOpacity : 0.85) * 100.0;
			if (SubHighlightGlowOpacityLabel != null)
			{
				SubHighlightGlowOpacityLabel.Text = $"{SubHighlightGlowOpacitySlider.Value:0}%";
			}
		}
		if (SubCustomHighlightGlowPanel != null)
		{
			string subGlow = ConfigManager.CurrentConfig.SubWheelHighlightGlowPreset ?? "FollowPrimary";
			SubCustomHighlightGlowPanel.Visibility = ((!(subGlow == "Custom") && string.IsNullOrEmpty(ConfigManager.CurrentConfig.SubWheelHighlightGlowColor)) ? Visibility.Collapsed : Visibility.Visible);
		}

		// Primary Geometry & Dimensions
		WheelRadiusSlider.Value = ConfigManager.CurrentConfig.WheelRadius;
		WheelRadiusLabel.Text = ConfigManager.CurrentConfig.WheelRadius.ToString("0");
		InnerRadiusSlider.Value = ConfigManager.CurrentConfig.InnerRadius;
		InnerRadiusLabel.Text = ConfigManager.CurrentConfig.InnerRadius.ToString("0");
		CoreRadiusSlider.Value = ConfigManager.CurrentConfig.CoreRadius;
		CoreRadiusLabel.Text = ConfigManager.CurrentConfig.CoreRadius.ToString("0");
		SectorGapSlider.Value = ConfigManager.CurrentConfig.SectorGap;
		SectorGapLabel.Text = $"{ConfigManager.CurrentConfig.SectorGap:0} px";
		SectorCornerRadiusSlider.Value = ConfigManager.CurrentConfig.SectorCornerRadius;
		SectorCornerRadiusLabel.Text = $"{ConfigManager.CurrentConfig.SectorCornerRadius:0} px";
		SectorIconSizeSlider.Value = ((ConfigManager.CurrentConfig.SectorIconSize > 0.0) ? ConfigManager.CurrentConfig.SectorIconSize : 20.0);
		SectorIconSizeLabel.Text = $"{SectorIconSizeSlider.Value:0} px";
		SectorFontSizeSlider.Value = ((ConfigManager.CurrentConfig.SectorFontSize > 0.0) ? ConfigManager.CurrentConfig.SectorFontSize : 10.5);
		SectorFontSizeLabel.Text = $"{SectorFontSizeSlider.Value:0.0} px";

		// Sub-Wheel Dimensions
		if (SubWheelOuterRadiusSlider != null)
		{
			SubWheelOuterRadiusSlider.Value = ((ConfigManager.CurrentConfig.SubWheelOuterRadius > 0.0) ? ConfigManager.CurrentConfig.SubWheelOuterRadius : 210.0);
			SubWheelOuterRadiusLabel.Text = $"{SubWheelOuterRadiusSlider.Value:0} px";
		}
		if (SubWheelInnerGapSlider != null)
		{
			SubWheelInnerGapSlider.Value = ((ConfigManager.CurrentConfig.SubWheelInnerGap >= 0.0) ? ConfigManager.CurrentConfig.SubWheelInnerGap : 4.0);
			SubWheelInnerGapLabel.Text = $"{SubWheelInnerGapSlider.Value:0} px";
		}
		if (SubWheelCornerRadiusSlider != null)
		{
			SubWheelCornerRadiusSlider.Value = ((ConfigManager.CurrentConfig.SubWheelCornerRadius >= 0.0) ? ConfigManager.CurrentConfig.SubWheelCornerRadius : 4.0);
			SubWheelCornerRadiusLabel.Text = $"{SubWheelCornerRadiusSlider.Value:0} px";
		}
		if (SubWheelIconSizeSlider != null)
		{
			SubWheelIconSizeSlider.Value = ((ConfigManager.CurrentConfig.SubWheelIconSize > 0.0) ? ConfigManager.CurrentConfig.SubWheelIconSize : 18.0);
			SubWheelIconSizeLabel.Text = $"{SubWheelIconSizeSlider.Value:0} px";
		}
		if (SubWheelFontSizeSlider != null)
		{
			SubWheelFontSizeSlider.Value = ((ConfigManager.CurrentConfig.SubWheelFontSize > 0.0) ? ConfigManager.CurrentConfig.SubWheelFontSize : 9.5);
			SubWheelFontSizeLabel.Text = $"{SubWheelFontSizeSlider.Value:0.0} px";
		}
		if (SubWheelTriggerDistanceSlider != null)
		{
			SubWheelTriggerDistanceSlider.Value = ((ConfigManager.CurrentConfig.SubWheelTriggerDistance > 0.0) ? ConfigManager.CurrentConfig.SubWheelTriggerDistance : 95.0);
			if (SubWheelTriggerDistanceValueText != null)
			{
				SubWheelTriggerDistanceValueText.Text = $"{SubWheelTriggerDistanceSlider.Value:0} px";
			}
		}

		// Shapes & Layouts
		SetComboBoxSelectedValue(ShapeComboBox, ConfigManager.CurrentConfig.Shape);
		RefreshLayoutOptionsUi();
		SetComboBoxSelectedValue(SubmenuStyleComboBox, ConfigManager.CurrentConfig.SubmenuStyle ?? "Wheel");
		if (ShowSelectedActionTextCheckBox != null)
		{
			ShowSelectedActionTextCheckBox.IsChecked = ConfigManager.CurrentConfig.ShowSelectedActionText;
		}
		if (CoreFontFamilyComboBox != null)
		{
			PopulateCoreFontFamilies();
			SetComboBoxSelectedValue(CoreFontFamilyComboBox, ConfigManager.CurrentConfig.CoreFontFamily ?? "Microsoft YaHei UI, Segoe UI");
		}
		if (CoreFontSizeSlider != null)
		{
			CoreFontSizeSlider.Value = (ConfigManager.CurrentConfig.CoreFontSize > 0.0) ? ConfigManager.CurrentConfig.CoreFontSize : 13.0;
			if (CoreFontSizeLabel != null)
			{
				CoreFontSizeLabel.Text = $"{CoreFontSizeSlider.Value:0.0} px";
			}
		}
		if (CoreTextColorTextBox != null)
		{
			CoreTextColorTextBox.Text = ConfigManager.CurrentConfig.CoreTextColor ?? "#FFFFFFFF";
			UpdateColorPreviewBorder(CoreTextColorPreview, CoreTextColorTextBox.Text);
		}
		if (EnableMultiTierCheckBox != null)
		{
			EnableMultiTierCheckBox.IsChecked = ConfigManager.CurrentConfig.EnableMultiTier;
		}

		// Sub Wheel Themes & Colors
		if (SubWheelUiStyleComboBox != null)
		{
			SetComboBoxSelectedValue(SubWheelUiStyleComboBox, ConfigManager.CurrentConfig.SubWheelUiStyle ?? "FollowPrimary");
		}
		if (SubWheelThemeComboBox != null)
		{
			SetComboBoxSelectedValue(SubWheelThemeComboBox, ConfigManager.CurrentConfig.SubWheelTheme ?? "FollowPrimary");
		}
		if (SubCustomSectorBgTextBox != null)
		{
			SubCustomSectorBgTextBox.Text = ConfigManager.CurrentConfig.SubWheelCustomSectorBg ?? "";
		}
		if (SubCustomSectorBorderTextBox != null)
		{
			SubCustomSectorBorderTextBox.Text = ConfigManager.CurrentConfig.SubWheelCustomSectorBorder ?? "";
		}
		if (SubCustomHighlightBgTextBox != null)
		{
			SubCustomHighlightBgTextBox.Text = ConfigManager.CurrentConfig.SubWheelCustomHighlightBg ?? "";
		}
		if (SubCustomHighlightBorderTextBox != null)
		{
			SubCustomHighlightBorderTextBox.Text = ConfigManager.CurrentConfig.SubWheelCustomHighlightBorder ?? "";
		}
		if (SubCustomTextTextBox != null)
		{
			SubCustomTextTextBox.Text = ConfigManager.CurrentConfig.SubWheelCustomText ?? "";
		}
		bool isSubCustomTheme = (ConfigManager.CurrentConfig.SubWheelTheme ?? "").StartsWith("CustomPreset_");
		if (((ConfigManager.CurrentConfig.SubWheelTheme == "Custom") || isSubCustomTheme) && SubCustomColorExpander != null)
		{
			SubCustomColorExpander.IsExpanded = true;
		}
		if (RenameSubCustomColorPresetButton != null)
		{
			RenameSubCustomColorPresetButton.Visibility = ((!isSubCustomTheme) ? Visibility.Collapsed : Visibility.Visible);
		}
		if (DeleteSubCustomColorPresetButton != null)
		{
			DeleteSubCustomColorPresetButton.Visibility = ((!isSubCustomTheme) ? Visibility.Collapsed : Visibility.Visible);
		}
		if (DeleteSubPresetInPanelButton != null)
		{
			DeleteSubPresetInPanelButton.Visibility = ((!isSubCustomTheme) ? Visibility.Collapsed : Visibility.Visible);
		}
		if (SaveSubPresetChangesButton != null)
		{
			SaveSubPresetChangesButton.Content = (isSubCustomTheme ? I18n.T("SavePresetChangesButton") : I18n.T("SaveAsNewPresetButton"));
		}
		UpdateSubColorPreviews();

		// Center Core Icon
		ShowCoreIconCheckBox.IsChecked = ConfigManager.CurrentConfig.ShowCoreIcon;
		SetComboBoxSelectedValue(CoreIconTypeComboBox, ConfigManager.CurrentConfig.CoreIconType ?? "Exit");
		CoreImagePathTextBox.Text = ConfigManager.CurrentConfig.CoreCustomImagePath ?? "";
		double coreScale = ((ConfigManager.CurrentConfig.CoreIconScale > 0.0) ? ConfigManager.CurrentConfig.CoreIconScale : 1.0);
		if (CoreIconScaleSlider != null)
		{
			CoreIconScaleSlider.Value = coreScale;
		}
		if (CoreIconScaleLabel != null)
		{
			CoreIconScaleLabel.Text = $"{Math.Round(coreScale * 100.0)}%";
		}
		if (CoreImageOffsetXSlider != null)
		{
			CoreImageOffsetXSlider.Value = ConfigManager.CurrentConfig.CoreImageOffsetX;
		}
		if (CoreImageOffsetXLabel != null)
		{
			CoreImageOffsetXLabel.Text = $"{(int)ConfigManager.CurrentConfig.CoreImageOffsetX} px";
		}
		if (CoreImageOffsetYSlider != null)
		{
			CoreImageOffsetYSlider.Value = ConfigManager.CurrentConfig.CoreImageOffsetY;
		}
		if (CoreImageOffsetYLabel != null)
		{
			CoreImageOffsetYLabel.Text = $"{(int)ConfigManager.CurrentConfig.CoreImageOffsetY} px";
		}
		UpdateCoreIconPreviewUI();

		// Scene Isolation
		DisableOnFullScreenCheckBox.IsChecked = ConfigManager.CurrentConfig.DisableOnFullScreen;
		CtrlModifierCheckBox.IsChecked = ConfigManager.CurrentConfig.DisableOnCtrl;
		ShiftModifierCheckBox.IsChecked = ConfigManager.CurrentConfig.DisableOnShift;
		AltModifierCheckBox.IsChecked = ConfigManager.CurrentConfig.DisableOnAlt;
		bool isWhitelist = string.Equals(ConfigManager.CurrentConfig.IsolationMode, "Whitelist", StringComparison.OrdinalIgnoreCase);
		if (IsolationWhitelistRadio != null)
		{
			IsolationWhitelistRadio.IsChecked = isWhitelist;
		}
		if (IsolationBlacklistRadio != null)
		{
			IsolationBlacklistRadio.IsChecked = !isWhitelist;
		}
		RefreshProcessListUI();

		// AutoStart
		AutoStartCheckBox.IsChecked = ConfigManager.IsAutoStartEnabled();
		if (AutoStartAsAdminCheckBox != null)
		{
			AutoStartAsAdminCheckBox.IsChecked = ConfigManager.CurrentConfig.AutoStartAsAdmin;
		}

		// Language & Previews
		SetComboBoxSelectedValue(LanguageComboBox, ConfigManager.CurrentConfig.Language ?? "Auto");
		ApplyLocalization();
		UpdateColorPreviews();

		// Profiles
		_selectedProfile = ConfigManager.CurrentConfig.Profiles.FirstOrDefault();
		if (_selectedProfile != null)
		{
			ProfilesListBox.SelectedItem = _selectedProfile;
			if (SectorCount4Radio != null) SectorCount4Radio.IsChecked = _selectedProfile.SectorCount == 4;
			if (SectorCount8Radio != null) SectorCount8Radio.IsChecked = _selectedProfile.SectorCount == 8;
			if (SectorCount12Radio != null) SectorCount12Radio.IsChecked = _selectedProfile.SectorCount == 12;
		}

		// System Update Settings
		if (AutoCheckUpdateCheckBox != null)
		{
			AutoCheckUpdateCheckBox.IsChecked = ConfigManager.CurrentConfig.AutoCheckUpdate;
		}
		SetComboBoxSelectedValue(UpdateChannelComboBox, ConfigManager.CurrentConfig.UpdateChannel ?? "Stable");
		SetComboBoxSelectedValue(UpdateProxyComboBox, ConfigManager.CurrentConfig.UpdateProxySource ?? "ghproxy");

		bool isStandalone = UpdateManager.Instance.IsCurrentInstallationStandalone();
		if (UpdatePkgStandaloneRadio != null) UpdatePkgStandaloneRadio.IsChecked = isStandalone;
		if (UpdatePkgLightweightRadio != null) UpdatePkgLightweightRadio.IsChecked = !isStandalone;

		string lastCheck = string.IsNullOrEmpty(ConfigManager.CurrentConfig.LastCheckUpdateTime) ? "未检查" : ConfigManager.CurrentConfig.LastCheckUpdateTime;
		if (UpdateStatusDescText != null)
		{
			UpdateStatusDescText.Text = $"当前运行版本: StarPie v{Assembly.GetExecutingAssembly().GetName().Version?.ToString(3) ?? "1.6.2"} (64位)。上次检查: {lastCheck}";
		}
	}

	[DllImport("user32.dll")]
	[return: MarshalAs(UnmanagedType.Bool)]
	private static extern bool SetForegroundWindow(nint hWnd);

	private void InitializeTrayIcon()
	{
		Icon icon = null;
		try
		{
			string text = Environment.ProcessPath;
			if (string.IsNullOrEmpty(text))
			{
				text = Process.GetCurrentProcess().MainModule?.FileName;
			}
			if (!string.IsNullOrEmpty(text) && File.Exists(text))
			{
				icon = System.Drawing.Icon.ExtractAssociatedIcon(text);
			}
		}
		catch
		{
		}
		if (icon == null)
		{
			try
			{
				StreamResourceInfo resourceStream = System.Windows.Application.GetResourceStream(new Uri("pack://application:,,,/app_icon.ico"));
				if (resourceStream != null)
				{
					using Stream stream = resourceStream.Stream;
					icon = new Icon(stream);
				}
			}
			catch
			{
			}
		}
		if (icon == null)
		{
			try
			{
				string text2 = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "app_icon.ico");
				if (File.Exists(text2))
				{
					icon = new Icon(text2);
				}
			}
			catch
			{
			}
		}
		if (icon == null)
		{
			icon = SystemIcons.Application;
		}
		_notifyIcon = new NotifyIcon
		{
			Icon = icon,
			Visible = true,
			Text = I18n.T("TrayTooltip")
		};
		_notifyIcon.DoubleClick += delegate
		{
			ShowSettings();
		};
		BuildTrayContextMenu();
	}

	private void BuildTrayContextMenu()
	{
		if (_notifyIcon != null)
		{
			ContextMenuStrip contextMenuStrip = new ContextMenuStrip();
			ToolStripMenuItem value = new ToolStripMenuItem("StarPie v" + (Assembly.GetExecutingAssembly().GetName().Version?.ToString(3) ?? "1.6.2"))
			{
				Enabled = false,
				Font = new Font(System.Drawing.SystemFonts.DefaultFont, System.Drawing.FontStyle.Bold)
			};
			contextMenuStrip.Items.Add(value);
			contextMenuStrip.Items.Add(new ToolStripSeparator());
			string text = ((App.MainMouseHook != null && App.MainMouseHook.IsPaused) ? I18n.T("TrayResume") : I18n.T("TrayPause"));
			_pauseResumeMenuItem = new ToolStripMenuItem(text, null, delegate
			{
				TogglePauseGestures();
			});
			contextMenuStrip.Items.Add(_pauseResumeMenuItem);
			contextMenuStrip.Items.Add(I18n.T("TrayPreferences"), null, delegate
			{
				ShowSettings();
			});
			contextMenuStrip.Items.Add(I18n.T("TrayAppearance"), null, delegate
			{
				ShowSettings(1);
			});
			contextMenuStrip.Items.Add(I18n.T("TrayGestures"), null, delegate
			{
				ShowSettings(2);
			});
			contextMenuStrip.Items.Add(I18n.T("TrayAbout"), null, delegate
			{
				ShowSettings(4);
			});
			contextMenuStrip.Items.Add(I18n.T("TrayElevate"), null, delegate(object? s, EventArgs e)
			{
				ElevatePrivileges_Click(s, new RoutedEventArgs());
			});
			contextMenuStrip.Items.Add(new ToolStripSeparator());
			contextMenuStrip.Items.Add(I18n.T("TrayExit"), null, delegate
			{
				ExitApplication();
			});
			_notifyIcon.ContextMenuStrip = contextMenuStrip;
		}
	}

	private void LanguageComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
	{
		if (!_isUpdatingUi && LanguageComboBox.SelectedItem is ComboBoxItem { Tag: string tag })
		{
			ConfigManager.CurrentConfig.Language = tag;
			I18n.SetLanguage(tag);
			ApplyLocalization();
			ConfigManager.SaveConfig();
		}
	}

	public void ApplyLocalization()
	{
		base.Title = I18n.T("WindowTitle");
		if (SidebarSubtitleText != null)
		{
			SidebarSubtitleText.Text = I18n.T("AppSubtitle");
		}
		if (NavTab0Text != null)
		{
			NavTab0Text.Text = I18n.T("TabTrigger");
		}
		if (NavTab1Text != null)
		{
			NavTab1Text.Text = I18n.T("TabAppearance");
		}
		if (NavTab2Text != null)
		{
			NavTab2Text.Text = I18n.T("TabGestures");
		}
		if (NavTab3Text != null)
		{
			NavTab3Text.Text = I18n.T("TabAdvanced");
		}
		if (NavTab4Text != null)
		{
			NavTab4Text.Text = I18n.T("TabAbout");
		}
		if (SidebarToggleButton != null)
		{
			string toggleText = I18n.T(_isSidebarCollapsed ? "SidebarExpand" : "SidebarCollapse");
			SidebarToggleButton.ToolTip = toggleText;
			System.Windows.Automation.AutomationProperties.SetName(SidebarToggleButton, toggleText);
		}
		if (BottomNoteText != null)
		{
			BottomNoteText.Text = I18n.T("BottomStatusNote");
		}
		if (SaveButton != null)
		{
			SaveButton.Content = I18n.T("BtnSave");
		}
		if (CloseButton != null)
		{
			CloseButton.Content = I18n.T("BtnClose");
		}
		if (TriggerPageHeader != null)
		{
			TriggerPageHeader.Text = I18n.T("TriggerHeader");
		}
		if (LongPressTriggerTitleText != null)
		{
			LongPressTriggerTitleText.Text = I18n.T("LongPressTriggerTitle");
		}
		if (LongPressTriggerDescText != null)
		{
			LongPressTriggerDescText.Text = I18n.T("LongPressTriggerDesc");
		}
		if (GestureTitleText != null)
		{
			GestureTitleText.Text = I18n.T("GestureTitle");
		}
		if (GestureDescText != null)
		{
			GestureDescText.Text = I18n.T("GestureDesc");
		}
		if (GestureEnableText != null)
		{
			GestureEnableText.Text = I18n.T("GestureEnableText");
		}
		if (GestureEnableDescText != null)
		{
			GestureEnableDescText.Text = I18n.T("GestureEnableDescText");
		}
		if (GestureTriggerLabelText != null)
		{
			GestureTriggerLabelText.Text = I18n.T("GestureTriggerLabelText");
		}
		if (GestureHintPlaceText != null)
		{
			GestureHintPlaceText.Text = I18n.T("GestureHintPlaceText");
		}
		if (GestureSensitivityTitleText != null)
		{
			GestureSensitivityTitleText.Text = I18n.T("GestureSensitivityTitle");
		}
		if (GestureMappingTitleText != null)
		{
			GestureMappingTitleText.Text = I18n.T("GestureMappingTitleText");
		}
		if (TileGlobalTitleText != null)
		{
			TileGlobalTitleText.Text = I18n.T("TileGlobalTitleText");
		}
		if (TileGlobalDescText != null)
		{
			TileGlobalDescText.Text = I18n.T("TileGlobalDescText");
		}
		if (TileMinimizeText != null)
		{
			TileMinimizeText.Text = I18n.T("TileMinimizeText");
		}
		if (TileExcludeText != null)
		{
			TileExcludeText.Text = I18n.T("TileExcludeText");
		}
		if (TileCycleRangeText != null)
		{
			TileCycleRangeText.Text = I18n.T("TileCycleRangeText");
		}
		if (CancelActionTitleText != null)
		{
			CancelActionTitleText.Text = I18n.T("CancelActionTitleText");
		}
		if (CancelActionDescText != null)
		{
			CancelActionDescText.Text = I18n.T("CancelActionDescText");
		}
		if (CancelActionEnableText != null)
		{
			CancelActionEnableText.Text = I18n.T("CancelActionEnableText");
		}
		if (TriggerPageSubheader != null)
		{
			TriggerPageSubheader.Text = I18n.T("TriggerSubheader");
		}
		if (TriggerRecorderTitleText != null)
		{
			TriggerRecorderTitleText.Text = I18n.T("TriggerRecorderTitle");
		}
		if (TriggerRecorderDescText != null)
		{
			TriggerRecorderDescText.Text = I18n.T("TriggerRecorderDesc");
		}
		if (CurrentBindingLabelText != null)
		{
			CurrentBindingLabelText.Text = I18n.T("CurrentBindingLabel");
		}
		if (RecordTriggerButton != null && !_isRecordingTrigger)
		{
			RecordTriggerButton.Content = I18n.T("BtnRecordTrigger");
		}
		if (ResetDefaultTriggerButton != null)
		{
			ResetDefaultTriggerButton.Content = I18n.T("BtnResetDefaultTrigger");
		}
		UpdateTriggerBadgeDisplay();
		if (SensitivityTitleText != null)
		{
			SensitivityTitleText.Text = I18n.T("SensitivityTitle");
		}
		if (SensitivityDescText != null)
		{
			SensitivityDescText.Text = I18n.T("SensitivityDesc");
		}
		if (AnimSpeedTitleText != null)
		{
			AnimSpeedTitleText.Text = I18n.T("AnimSpeedTitle");
		}
		if (AnimSpeedDescText != null)
		{
			AnimSpeedDescText.Text = I18n.T("AnimSpeedDesc");
		}
		if (AnimSpeedElegantRadio != null)
		{
			AnimSpeedElegantRadio.Content = I18n.T("AnimSpeedElegant");
		}
		if (AnimSpeedBalancedRadio != null)
		{
			AnimSpeedBalancedRadio.Content = I18n.T("AnimSpeedBalanced");
		}
		if (AnimSpeedFastRadio != null)
		{
			AnimSpeedFastRadio.Content = I18n.T("AnimSpeedFast");
		}
		if (SceneIsolationTitleText != null)
		{
			SceneIsolationTitleText.Text = I18n.T("SceneIsolationTitle");
		}
		if (SceneIsolationDescText != null)
		{
			SceneIsolationDescText.Text = I18n.T("SceneIsolationDesc");
		}
		if (FullScreenOptionTitleText != null)
		{
			FullScreenOptionTitleText.Text = I18n.T("FullScreenOption");
		}
		if (FullScreenOptionDescText != null)
		{
			FullScreenOptionDescText.Text = I18n.T("FullScreenOptionDesc");
		}
		if (ModifierPassTitleText != null)
		{
			ModifierPassTitleText.Text = I18n.T("ModifierPassTitle");
		}
		if (CtrlModifierCheckBox != null)
		{
			CtrlModifierCheckBox.Content = I18n.T("ModifierCtrl");
		}
		if (ShiftModifierCheckBox != null)
		{
			ShiftModifierCheckBox.Content = I18n.T("ModifierShift");
		}
		if (AltModifierCheckBox != null)
		{
			AltModifierCheckBox.Content = I18n.T("ModifierAlt");
		}
		if (IsolationModeTitleText != null)
		{
			IsolationModeTitleText.Text = I18n.T("IsolationModeTitle");
		}
		if (IsolationBlacklistRadio != null)
		{
			IsolationBlacklistRadio.Content = I18n.T("IsolationBlacklistRadio");
		}
		if (IsolationWhitelistRadio != null)
		{
			IsolationWhitelistRadio.Content = I18n.T("IsolationWhitelistRadio");
		}
		if (ProcessListDescText != null)
		{
			bool flag = string.Equals(ConfigManager.CurrentConfig?.IsolationMode, "Whitelist", StringComparison.OrdinalIgnoreCase);
			ProcessListDescText.Text = (flag ? I18n.T("WhitelistDesc") : I18n.T("BlacklistDesc"));
		}
		if (BrowseBlacklistButton != null)
		{
			BrowseBlacklistButton.Content = I18n.T("BtnPickProcess");
		}
		if (AddBlacklistButton != null)
		{
			AddBlacklistButton.Content = I18n.T("BtnAddProcess");
		}
		if (DeleteBlacklistButton != null)
		{
			DeleteBlacklistButton.Content = I18n.T("BtnDeleteProcess");
		}
		if (NewBlacklistProcessTextBox != null)
		{
			NewBlacklistProcessTextBox.ToolTip = I18n.T("BlacklistPlaceholder");
		}
		if (OuterEscapeTitleText != null)
		{
			OuterEscapeTitleText.Text = I18n.T("OuterEscapeTitle");
		}
		if (OuterEscapeDescText != null)
		{
			OuterEscapeDescText.Text = I18n.T("OuterEscapeDesc");
		}
		if (OuterEscapeCheckboxTitleText != null)
		{
			OuterEscapeCheckboxTitleText.Text = I18n.T("OuterEscapeCheckbox");
		}
		if (OuterEscapeDistanceTitleText != null)
		{
			OuterEscapeDistanceTitleText.Text = I18n.T("OuterEscapeDistanceTitle");
		}
		if (OuterEscapeDistanceDescText != null)
		{
			OuterEscapeDistanceDescText.Text = I18n.T("OuterEscapeDistanceDesc");
		}
		if (NewCustomColorPresetButton != null)
		{
			NewCustomColorPresetButton.Content = I18n.T("NewCustomPresetButton");
		}
		if (RenameCustomColorPresetButton != null)
		{
			RenameCustomColorPresetButton.Content = I18n.T("RenameCustomPresetButton");
		}
		if (DeleteCustomColorPresetButton != null)
		{
			DeleteCustomColorPresetButton.Content = I18n.T("DeletePresetButton");
		}
		if (SaveAsNewPresetButton != null)
		{
			SaveAsNewPresetButton.Content = I18n.T("SaveAsNewPresetButton");
		}
		if (DeletePresetInPanelButton != null)
		{
			DeletePresetInPanelButton.Content = I18n.T("DeletePresetButton");
		}
		if (CustomColorsExpanderTitleText != null)
		{
			CustomColorsExpanderTitleText.Text = I18n.T("CustomColorsExpanderTitle");
		}
		if (CustomColorsExpanderDescText != null)
		{
			CustomColorsExpanderDescText.Text = I18n.T("CustomColorsExpanderDesc");
		}
		if (WheelFontFamilyTitleText != null)
		{
			WheelFontFamilyTitleText.Text = I18n.T("WheelFontFamily");
		}
		if (LayoutTargetGlobalRadio != null)
		{
			LayoutTargetGlobalRadio.Content = I18n.T("LayoutTargetGlobal");
		}
		if (LayoutTargetSlotRadio != null)
		{
			LayoutTargetSlotRadio.Content = I18n.T("LayoutTargetSlot");
		}
		if (ResetSlotLayoutButton != null)
		{
			ResetSlotLayoutButton.Content = I18n.T("ResetToGlobalLayout");
		}
		if (SectorTextColorTitleText != null)
		{
			SectorTextColorTitleText.Text = I18n.T("SectorTextColor");
		}
		if (CoreTextOptionsSectionTitle != null)
		{
			CoreTextOptionsSectionTitle.Text = I18n.T("CoreTextOptions");
		}
		if (CoreFontFamilyTitleText != null)
		{
			CoreFontFamilyTitleText.Text = I18n.T("CoreFontFamily");
		}
		if (CoreFontSizeTitleText != null)
		{
			CoreFontSizeTitleText.Text = I18n.T("CoreFontSize");
		}
		if (CoreTextColorTitleText != null)
		{
			CoreTextColorTitleText.Text = I18n.T("CoreTextColor");
		}
		if (ShowSelectedActionTextCheckBox != null)
		{
			ShowSelectedActionTextCheckBox.Content = I18n.T("ShowSelectedActionText");
		}
		if (FindName("OlderMilestonesExpander") is Expander expander)
		{
			expander.Header = I18n.T("MilestonesOlderExpander");
		}
		if (AppearancePageHeader != null)
		{
			AppearancePageHeader.Text = I18n.T("AppearanceHeader");
		}
		if (AppearancePageSubheader != null)
		{
			AppearancePageSubheader.Text = I18n.T("AppearanceSubheader");
		}
		if (ResetDimensionsButton != null)
		{
			ResetDimensionsButton.Content = I18n.T("BtnResetGeometry");
		}
		if (CoreTransformSectionTitle != null)
		{
			CoreTransformSectionTitle.Text = I18n.T("CoreTransformSectionTitle");
		}
		if (CoreIconScaleTitleText != null)
		{
			CoreIconScaleTitleText.Text = I18n.T("CoreIconScaleTitle");
		}
		if (CoreImageOffsetXTitleText != null)
		{
			CoreImageOffsetXTitleText.Text = I18n.T("CoreImageOffsetXTitle");
		}
		if (CoreImageOffsetYTitleText != null)
		{
			CoreImageOffsetYTitleText.Text = I18n.T("CoreImageOffsetYTitle");
		}
		if (ResetCoreTransformButton != null)
		{
			ResetCoreTransformButton.Content = I18n.T("BtnResetCoreTransform");
		}
		if (CoreImagePerformanceTipText != null)
		{
			CoreImagePerformanceTipText.Text = I18n.T("CoreImagePerformanceTip");
		}
		if (EnableMultiTierCheckBox != null)
		{
			EnableMultiTierCheckBox.Content = I18n.T("EnableMultiTier");
		}
		
		if (SubmenuStyleWheelItem != null) SubmenuStyleWheelItem.Content = I18n.T("SubmenuStyleWheel");
		if (SubmenuStyleFanItem != null) SubmenuStyleFanItem.Content = I18n.T("SubmenuStyleFan");

		if (GesturesPageHeader != null)
		{
			GesturesPageHeader.Text = I18n.T("GesturesHeader");
		}
		if (AddProfileButton != null)
		{
			AddProfileButton.Content = I18n.T("BtnAddAppProfile");
		}
		if (AddCustomProfileButton != null)
		{
			AddCustomProfileButton.Content = I18n.T("BtnAddCustomProfile");
		}
		if (RenameProfileButton != null)
		{
			RenameProfileButton.Content = I18n.T("BtnRenameProfile");
		}
		if (DeleteProfileButton != null)
		{
			DeleteProfileButton.Content = I18n.T("BtnDeleteProfile");
		}
		if (SectorCount4Radio != null)
		{
			SectorCount4Radio.Content = I18n.T("SectorCount4");
		}
		if (SectorCount8Radio != null)
		{
			SectorCount8Radio.Content = I18n.T("SectorCount8");
		}
		if (SectorCount12Radio != null)
		{
			SectorCount12Radio.Content = I18n.T("SectorCount12");
		}
		if (AdvancedPageHeader != null)
		{
			AdvancedPageHeader.Text = I18n.T("AdvancedHeader");
		}
		if (LanguageTitleText != null)
		{
			LanguageTitleText.Text = I18n.T("LanguageTitle");
		}
		if (LanguageDescText != null)
		{
			LanguageDescText.Text = I18n.T("LanguageDesc");
		}
		if (StartupTitleText != null)
		{
			StartupTitleText.Text = I18n.T("StartupTitle");
		}
		if (StartupDescText != null)
		{
			StartupDescText.Text = I18n.T("StartupDesc");
		}
		if (ElevateTitleText != null)
		{
			ElevateTitleText.Text = I18n.T("ElevateTitle");
		}
		if (ElevateDescText != null)
		{
			ElevateDescText.Text = I18n.T("ElevateDesc");
		}
		if (ElevateButton != null)
		{
			ElevateButton.Content = I18n.T("BtnElevate");
		}
		if (MemoryOptTitleText != null)
		{
			MemoryOptTitleText.Text = I18n.T("MemoryTitle");
		}
		if (MemoryOptDescText != null)
		{
			MemoryOptDescText.Text = I18n.T("MemoryDesc");
		}
		if (TrimMemoryButton != null)
		{
			TrimMemoryButton.Content = I18n.T("BtnTrimMemory");
		}
		if (BackupTitleText != null)
		{
			BackupTitleText.Text = I18n.T("BackupTitle");
		}
		if (ExportConfigButton != null)
		{
			ExportConfigButton.Content = I18n.T("BtnExportConfig");
		}
		if (ImportConfigButton != null)
		{
			ImportConfigButton.Content = I18n.T("BtnImportConfig");
		}
		if (LogsTitleText != null)
		{
			LogsTitleText.Text = I18n.T("LogsTitle");
		}
		if (LogsDescText != null)
		{
			LogsDescText.Text = I18n.T("LogsDesc");
		}
		if (OpenLogFolderButton != null)
		{
			OpenLogFolderButton.Content = I18n.T("BtnOpenLogFolder");
		}
		if (ViewTodayLogButton != null)
		{
			ViewTodayLogButton.Content = I18n.T("BtnViewTodayLog");
		}
		BuildTrayContextMenu();
	}

	private void TogglePauseGestures()
	{
		if (App.MainMouseHook == null)
		{
			return;
		}
		App.MainMouseHook.IsPaused = !App.MainMouseHook.IsPaused;
		if (App.MainMouseHook.IsPaused)
		{
			if (_pauseResumeMenuItem != null)
			{
				_pauseResumeMenuItem.Text = I18n.T("TrayResume");
			}
			_notifyIcon.Text = "StarPie (" + I18n.T("TrayPause") + ")";
		}
		else
		{
			if (_pauseResumeMenuItem != null)
			{
				_pauseResumeMenuItem.Text = I18n.T("TrayPause");
			}
			_notifyIcon.Text = I18n.T("TrayTooltip");
		}
	}

	public void ShowSettings(int tabIndex = -1)
	{
		if (!((DispatcherObject)this).Dispatcher.CheckAccess())
		{
			((DispatcherObject)this).Dispatcher.Invoke((Action)delegate
			{
				ShowSettings(tabIndex);
			});
			return;
		}
		if (tabIndex >= 0)
		{
			SwitchToTab(tabIndex);
		}
		BeginAnimation(UIElement.OpacityProperty, null);
		base.Opacity = 1.0;
		if (base.Visibility != Visibility.Visible)
		{
			Show();
		}
		if (base.WindowState == WindowState.Minimized)
		{
			base.WindowState = WindowState.Normal;
		}
		Activate();
		Focus();
		try
		{
			nint handle = new WindowInteropHelper(this).Handle;
			if (handle != IntPtr.Zero)
			{
				SetForegroundWindow(handle);
			}
		}
		catch
		{
		}
	}

	private void NavTab_Checked(object sender, RoutedEventArgs e)
	{
		if (!_isUpdatingUi && sender is FrameworkElement { Tag: var tag } && int.TryParse(tag?.ToString(), out var result))
		{
			SwitchToTab(result);
		}
	}

	public void SwitchToTab(int index)
	{
		if (TriggerSettingsGrid == null || AppearanceSettingsGrid == null || MappingsSettingsGrid == null || SystemSettingsGrid == null || AboutSettingsGrid == null)
		{
			return;
		}
		TriggerSettingsGrid.Visibility = ((index != 0) ? Visibility.Collapsed : Visibility.Visible);
		AppearanceSettingsGrid.Visibility = ((index != 1) ? Visibility.Collapsed : Visibility.Visible);
		MappingsSettingsGrid.Visibility = ((index != 2) ? Visibility.Collapsed : Visibility.Visible);
		SystemSettingsGrid.Visibility = ((index != 3) ? Visibility.Collapsed : Visibility.Visible);
		AboutSettingsGrid.Visibility = ((index != 4) ? Visibility.Collapsed : Visibility.Visible);
		_isUpdatingUi = true;
		try
		{
			if (NavTab0 != null)
			{
				NavTab0.IsChecked = index == 0;
			}
			if (NavTab1 != null)
			{
				NavTab1.IsChecked = index == 1;
			}
			if (NavTab2 != null)
			{
				NavTab2.IsChecked = index == 2;
			}
			if (NavTab3 != null)
			{
				NavTab3.IsChecked = index == 3;
			}
			if (NavTab4 != null)
			{
				NavTab4.IsChecked = index == 4;
			}
		}
		finally
		{
			_isUpdatingUi = false;
		}
		switch (index)
		{
		case 2:
			if (_selectedProfile == null && ConfigManager.CurrentConfig.Profiles.Count > 0)
			{
				_selectedProfile = ConfigManager.CurrentConfig.Profiles[0];
			}
			if (ProfilesListBox != null)
			{
				ProfilesListBox.SelectedItem = _selectedProfile;
			}
			if (MappingsProfileComboBox != null)
			{
				MappingsProfileComboBox.SelectedItem = _selectedProfile;
			}
			if (_selectedProfile == null)
			{
				break;
			}
			_isUpdatingUi = true;
			try
			{
				if (SectorCount4Radio != null) SectorCount4Radio.IsChecked = _selectedProfile.SectorCount == 4;
				if (SectorCount8Radio != null) SectorCount8Radio.IsChecked = _selectedProfile.SectorCount == 8;
				if (SectorCount12Radio != null) SectorCount12Radio.IsChecked = _selectedProfile.SectorCount == 12;
				if (MappingsSectorCount4Radio != null) MappingsSectorCount4Radio.IsChecked = _selectedProfile.SectorCount == 4;
				if (MappingsSectorCount8Radio != null) MappingsSectorCount8Radio.IsChecked = _selectedProfile.SectorCount == 8;
				if (MappingsSectorCount12Radio != null) MappingsSectorCount12Radio.IsChecked = _selectedProfile.SectorCount == 12;
				RefreshSlots();
				UpdateFocusEditorUi();
				RenderMappingsWheelPreview();
				break;
			}
			finally
			{
				_isUpdatingUi = false;
			}
		case 1:
			RenderLiveWheelPreview();
			break;
		}
	}

	private void ScheduleAutoSave()
	{
		//IL_0019: Unknown result type (might be due to invalid IL or missing references)
		//IL_001e: Unknown result type (might be due to invalid IL or missing references)
		//IL_0037: Expected O, but got Unknown
		if (_isUpdatingUi || ConfigManager.CurrentConfig == null)
		{
			return;
		}
		if (_autoSaveDebounceTimer == null)
		{
			_autoSaveDebounceTimer = new DispatcherTimer
			{
				Interval = TimeSpan.FromMilliseconds(400.0)
			};
			_autoSaveDebounceTimer.Tick += delegate
			{
				_autoSaveDebounceTimer.Stop();
				SyncUiToConfigAndSave();
			};
		}
		_autoSaveDebounceTimer.Stop();
		_autoSaveDebounceTimer.Start();
	}

	private void SyncUiToConfigAndSave(bool saveToDisk = true)
	{
		if (_isUpdatingUi || ConfigManager.CurrentConfig == null)
		{
			return;
		}
		try
		{
			if (AppThemeComboBox?.SelectedItem is ComboBoxItem comboBoxItem)
			{
				ConfigManager.CurrentConfig.AppTheme = comboBoxItem.Tag?.ToString() ?? "System";
			}
			if (UiStyleComboBox?.SelectedItem is ComboBoxItem comboBoxItem2)
			{
				ConfigManager.CurrentConfig.UiStyle = comboBoxItem2.Tag?.ToString() ?? "ClassicRing";
			}
			if (ThemeComboBox?.SelectedItem is ComboBoxItem comboBoxItem3)
			{
				ConfigManager.CurrentConfig.Theme = comboBoxItem3.Tag?.ToString() ?? "System";
			}
			if (ShapeComboBox?.SelectedItem is ComboBoxItem comboBoxItem4)
			{
				ConfigManager.CurrentConfig.Shape = comboBoxItem4.Tag?.ToString() ?? "Original";
			}
			if (_selectedLayoutSlotIndex < 0)
			{
				if (IconLayoutModeComboBox?.SelectedItem is ComboBoxItem comboBoxItem5)
				{
					ConfigManager.CurrentConfig.IconLayoutMode = comboBoxItem5.Tag?.ToString() ?? "IconAndText";
				}
				if (WheelFontFamilyComboBox?.SelectedItem is ComboBoxItem wheelFontItem)
				{
					ConfigManager.CurrentConfig.WheelFontFamily = wheelFontItem.Tag?.ToString() ?? "Microsoft YaHei UI, Segoe UI";
				}
				if (SectorIconSizeSlider != null)
				{
					ConfigManager.CurrentConfig.SectorIconSize = SectorIconSizeSlider.Value;
				}
				if (SectorFontSizeSlider != null)
				{
					ConfigManager.CurrentConfig.SectorFontSize = SectorFontSizeSlider.Value;
				}
			}
			if (ShowSelectedActionTextCheckBox != null)
			{
				ConfigManager.CurrentConfig.ShowSelectedActionText = ShowSelectedActionTextCheckBox.IsChecked == true;
			}
			if (ShowCoreIconCheckBox != null)
			{
				ConfigManager.CurrentConfig.ShowCoreIcon = ShowCoreIconCheckBox.IsChecked == true;
			}
			if (CoreIconTypeComboBox?.SelectedItem is ComboBoxItem comboBoxItem6)
			{
				ConfigManager.CurrentConfig.CoreIconType = comboBoxItem6.Tag?.ToString() ?? "Exit";
			}
			if (CoreImagePathTextBox != null)
			{
				ConfigManager.CurrentConfig.CoreCustomImagePath = CoreImagePathTextBox.Text.Trim();
			}
			if (CoreIconScaleSlider != null)
			{
				ConfigManager.CurrentConfig.CoreIconScale = CoreIconScaleSlider.Value;
			}
			if (CoreImageOffsetXSlider != null)
			{
				ConfigManager.CurrentConfig.CoreImageOffsetX = CoreImageOffsetXSlider.Value;
			}
			if (CoreImageOffsetYSlider != null)
			{
				ConfigManager.CurrentConfig.CoreImageOffsetY = CoreImageOffsetYSlider.Value;
			}
			if (HighlightGlowPresetComboBox?.SelectedItem is ComboBoxItem comboBoxItem7)
			{
				ConfigManager.CurrentConfig.HighlightGlowPreset = comboBoxItem7.Tag?.ToString() ?? "Auto";
			}
			if (HighlightGlowColorTextBox != null)
			{
				ConfigManager.CurrentConfig.HighlightGlowColor = HighlightGlowColorTextBox.Text.Trim();
			}
			if (HighlightGlowRadiusSlider != null)
			{
				ConfigManager.CurrentConfig.HighlightGlowRadius = HighlightGlowRadiusSlider.Value;
			}
			if (HighlightGlowOpacitySlider != null)
			{
				ConfigManager.CurrentConfig.HighlightGlowOpacity = HighlightGlowOpacitySlider.Value / 100.0;
			}
			if (SubHighlightGlowPresetComboBox?.SelectedItem is ComboBoxItem comboBoxItem8)
			{
				ConfigManager.CurrentConfig.SubWheelHighlightGlowPreset = comboBoxItem8.Tag?.ToString() ?? "FollowPrimary";
			}
			if (SubHighlightGlowColorTextBox != null)
			{
				ConfigManager.CurrentConfig.SubWheelHighlightGlowColor = SubHighlightGlowColorTextBox.Text.Trim();
			}
			if (SubHighlightGlowRadiusSlider != null)
			{
				ConfigManager.CurrentConfig.SubWheelHighlightGlowRadius = SubHighlightGlowRadiusSlider.Value;
			}
			if (SubHighlightGlowOpacitySlider != null)
			{
				ConfigManager.CurrentConfig.SubWheelHighlightGlowOpacity = SubHighlightGlowOpacitySlider.Value / 100.0;
			}
			if (WheelRadiusSlider != null)
			{
				ConfigManager.CurrentConfig.WheelRadius = WheelRadiusSlider.Value;
			}
			if (InnerRadiusSlider != null)
			{
				ConfigManager.CurrentConfig.InnerRadius = InnerRadiusSlider.Value;
			}
			if (CoreRadiusSlider != null)
			{
				ConfigManager.CurrentConfig.CoreRadius = CoreRadiusSlider.Value;
			}
			if (SectorGapSlider != null)
			{
				ConfigManager.CurrentConfig.SectorGap = SectorGapSlider.Value;
			}
			if (SectorCornerRadiusSlider != null)
			{
				ConfigManager.CurrentConfig.SectorCornerRadius = SectorCornerRadiusSlider.Value;
			}
			if (ThresholdSlider != null)
			{
				ConfigManager.CurrentConfig.DragThreshold = ThresholdSlider.Value;
			}
			if (EnableOuterEscapeCheckBox != null)
			{
				ConfigManager.CurrentConfig.EnableOuterEscapeCancel = EnableOuterEscapeCheckBox.IsChecked == true;
			}
			if (OuterEscapeDistanceSlider != null)
			{
				ConfigManager.CurrentConfig.OuterEscapeDistance = OuterEscapeDistanceSlider.Value;
			}
			if (CustomSectorBgTextBox != null)
			{
				ConfigManager.CurrentConfig.CustomSectorBg = CustomSectorBgTextBox.Text.Trim();
			}
			if (CustomSectorBorderTextBox != null)
			{
				ConfigManager.CurrentConfig.CustomSectorBorder = CustomSectorBorderTextBox.Text.Trim();
			}
			if (CustomHighlightBgTextBox != null)
			{
				ConfigManager.CurrentConfig.CustomHighlightBg = CustomHighlightBgTextBox.Text.Trim();
			}
			if (CustomHighlightBorderTextBox != null)
			{
				ConfigManager.CurrentConfig.CustomHighlightBorder = CustomHighlightBorderTextBox.Text.Trim();
			}
			if (CustomTextTextBox != null)
			{
				ConfigManager.CurrentConfig.CustomText = CustomTextTextBox.Text.Trim();
			}
			if (DisableOnFullScreenCheckBox != null)
			{
				ConfigManager.CurrentConfig.DisableOnFullScreen = DisableOnFullScreenCheckBox.IsChecked == true;
			}
			if (CtrlModifierCheckBox != null)
			{
				ConfigManager.CurrentConfig.DisableOnCtrl = CtrlModifierCheckBox.IsChecked == true;
			}
			if (ShiftModifierCheckBox != null)
			{
				ConfigManager.CurrentConfig.DisableOnShift = ShiftModifierCheckBox.IsChecked == true;
			}
			if (AltModifierCheckBox != null)
			{
				ConfigManager.CurrentConfig.DisableOnAlt = AltModifierCheckBox.IsChecked == true;
			}
			if (saveToDisk)
			{
				ConfigManager.SaveConfig();
			}
		}
		catch (Exception)
		{
		}
	}

	private void ExitApplication()
	{
		_isClosingFromTray = true;
		try
		{
			SyncUiToConfigAndSave();
		}
		catch
		{
		}
		_notifyIcon.Visible = false;
		_notifyIcon.Dispose();
		System.Windows.Application.Current.Shutdown();
	}

	private void Window_Closing(object sender, CancelEventArgs e)
	{
		SyncUiToConfigAndSave();
		if (_isClosingFromTray)
		{
			DisposeSlotViewModels();
		}
		if (!_isClosingFromTray)
		{
			e.Cancel = true;
			DoubleAnimation doubleAnimation = new DoubleAnimation(1.0, 0.0, new Duration(TimeSpan.FromMilliseconds(120.0)));
			doubleAnimation.Completed += delegate
			{
				Hide();
				base.Opacity = 1.0;
			};
			BeginAnimation(UIElement.OpacityProperty, doubleAnimation);
			_notifyIcon.ShowBalloonTip(2000, "WinPieGestures", "应用已最小化至系统托盘，将在后台继续运行鼠标笔势监视。", ToolTipIcon.Info);
		}
	}

	private void ProfilesListBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
	{
		if (_isUpdatingUi)
		{
			return;
		}
		_selectedProfile = ProfilesListBox.SelectedItem as WheelProfile;
		if (_selectedProfile == null)
		{
			return;
		}
		_isUpdatingUi = true;
		try
		{
			if (SectorCount4Radio != null)
			{
				SectorCount4Radio.IsChecked = _selectedProfile.SectorCount == 4;
			}
			if (SectorCount8Radio != null)
			{
				SectorCount8Radio.IsChecked = _selectedProfile.SectorCount == 8;
			}
			if (SectorCount12Radio != null)
			{
				SectorCount12Radio.IsChecked = _selectedProfile.SectorCount == 12;
			}
			if (MappingsSectorCount4Radio != null) MappingsSectorCount4Radio.IsChecked = _selectedProfile.SectorCount == 4;
			if (MappingsSectorCount8Radio != null) MappingsSectorCount8Radio.IsChecked = _selectedProfile.SectorCount == 8;
			if (MappingsSectorCount12Radio != null) MappingsSectorCount12Radio.IsChecked = _selectedProfile.SectorCount == 12;
			if (MappingsProfileComboBox != null && MappingsProfileComboBox.SelectedItem != _selectedProfile)
			{
				MappingsProfileComboBox.SelectedItem = _selectedProfile;
			}
			RefreshSlots();
			UpdateFocusEditorUi();
		}
		finally
		{
			_isUpdatingUi = false;
		}
		if (AppearanceSettingsGrid != null && AppearanceSettingsGrid.Visibility == Visibility.Visible)
		{
			RenderLiveWheelPreview();
		}
		if (MappingsSettingsGrid != null && MappingsSettingsGrid.Visibility == Visibility.Visible)
		{
			RenderMappingsWheelPreview();
		}
	}

	private void RefreshSlots()
	{
		try
		{
			WheelProfile? profile = _selectedProfile;
			if (profile == null)
			{
				profile = ProfilesListBox?.SelectedItem as WheelProfile ?? ConfigManager.CurrentConfig.Profiles.FirstOrDefault();
				_selectedProfile = profile;
			}

			const int maxSectorCount = 12;
			EnsureSlotViewModels(maxSectorCount);

			if (profile == null)
			{
				for (int i = 0; i < _slotViewModels.Count; i++)
				{
					_slotViewModels[i].Update(i, 8, string.Empty, null, false);
				}
				return;
			}

			int count = NormalizeSectorCount(profile.SectorCount);
			if (profile.SectorCount != count)
			{
				profile.SectorCount = count;
			}

			string[] directions = count switch
			{
				4 => Directions4,
				12 => Directions12,
				_ => Directions8
			};

			profile.Actions ??= new List<ActionItem>();
			while (profile.Actions.Count < count)
			{
				int index = profile.Actions.Count;
				if (count == 12 && index < DefaultPresets12.Length)
				{
					ActionItem preset = DefaultPresets12[index];
					profile.Actions.Add(new ActionItem
					{
						Type = preset.Type,
						Name = preset.Name,
						Parameter = preset.Parameter,
						IconKey = preset.IconKey
					});
				}
				else if (count == 4 && index < DefaultPresets4.Length)
				{
					ActionItem preset = DefaultPresets4[index];
					profile.Actions.Add(new ActionItem
					{
						Type = preset.Type,
						Name = preset.Name,
						Parameter = preset.Parameter,
						IconKey = preset.IconKey
					});
				}
				else
				{
					profile.Actions.Add(new ActionItem
					{
						Type = "Hotkey",
						Name = $"快捷动作 {index + 1}",
						Parameter = ""
					});
				}
			}

			for (int i = 0; i < _slotViewModels.Count; i++)
			{
				ActionItem? action = i < profile.Actions.Count ? profile.Actions[i] : null;
				bool isVisible = i < count;
				string direction = isVisible ? directions[i] : string.Empty;
				_slotViewModels[i].Update(i, count, direction, action, isVisible);
			}
		}
		catch (Exception ex)
		{
			Debug.WriteLine($"[RefreshSlots Error]: {ex}");
		}
	}

	private void DisposeSlotViewModels()
	{
		foreach (SlotViewModel slot in _slotViewModels)
		{
			slot.Dispose();
		}
	}

	private static int NormalizeSectorCount(int sectorCount)
	{
		return sectorCount is 4 or 8 or 12 ? sectorCount : 8;
	}

	private void EnsureSlotViewModels(int count)
	{
		while (_slotViewModels.Count < count)
		{
			int positionIndex = _slotViewModels.Count;
			_slotViewModels.Add(new SlotViewModel(
				positionIndex,
				8,
				string.Empty,
				new ActionItem
				{
					Type = "Hotkey",
					Name = $"快捷动作 {positionIndex + 1}",
					Parameter = ""
				}));
		}
	}

	private void MoveSlotUp_Click(object sender, RoutedEventArgs e)
	{
		MoveSlot(sender, -1);
		e.Handled = true;
	}

	private void MoveSlotDown_Click(object sender, RoutedEventArgs e)
	{
		MoveSlot(sender, 1);
		e.Handled = true;
	}

	private void MoveSlot(object sender, int offset)
	{
		if (sender is not FrameworkElement element ||
			element.DataContext is not SlotViewModel slot ||
			_selectedProfile?.Actions == null)
		{
			return;
		}

		int sourceIndex = _slotViewModels.IndexOf(slot);
		int activeCount = NormalizeSectorCount(_selectedProfile.SectorCount);
		int targetIndex = sourceIndex + offset;
		if (sourceIndex < 0 || sourceIndex >= activeCount ||
			targetIndex < 0 || targetIndex >= activeCount ||
			targetIndex >= _selectedProfile.Actions.Count)
		{
			return;
		}

		(_selectedProfile.Actions[sourceIndex], _selectedProfile.Actions[targetIndex]) =
			(_selectedProfile.Actions[targetIndex], _selectedProfile.Actions[sourceIndex]);

		RefreshSlots();
		SyncUiToConfigAndSave(true);
		if (AppearanceSettingsGrid?.Visibility == Visibility.Visible)
		{
			ScheduleLiveWheelPreviewRender();
		}
	}

	private void SectorCountRadio_Checked(object sender, RoutedEventArgs e)
	{
		if (_isUpdatingUi || _isChangingSectorCount)
		{
			return;
		}
		if (_selectedProfile == null)
		{
			_selectedProfile = (ProfilesListBox?.SelectedItem as WheelProfile) ?? ConfigManager.CurrentConfig.Profiles.FirstOrDefault();
		}
		if (_selectedProfile == null)
		{
			return;
		}
		int sectorCount = 8;
		if (SectorCount4Radio?.IsChecked == true)
		{
			sectorCount = 4;
		}
		else if (SectorCount8Radio?.IsChecked == true)
		{
			sectorCount = 8;
		}
		else if (SectorCount12Radio?.IsChecked == true)
		{
			sectorCount = 12;
		}

		if (_selectedProfile.SectorCount == sectorCount)
		{
			return;
		}

		_isChangingSectorCount = true;
		try
		{
			_isUpdatingUi = true;
			try
			{
				_selectedProfile.SectorCount = sectorCount;
				RefreshSlots();
			}
			finally
			{
				_isUpdatingUi = false;
			}

			if (AppearanceSettingsGrid?.Visibility == Visibility.Visible)
			{
				ScheduleLiveWheelPreviewRender();
			}
			SyncUiToConfigAndSave();
		}
		finally
		{
			_isChangingSectorCount = false;
		}
	}

	private void ScheduleLiveWheelPreviewRender()
	{
		if (_previewRenderPending || LiveWheelPreviewCanvas == null ||
			AppearanceSettingsGrid?.Visibility != Visibility.Visible)
		{
			return;
		}

		_previewRenderPending = true;
		Dispatcher.BeginInvoke(
			new Action(() =>
			{
				_previewRenderPending = false;
				if (!IsLoaded || AppearanceSettingsGrid?.Visibility != Visibility.Visible)
				{
					return;
				}
				RenderLiveWheelPreview();
			}),
			DispatcherPriority.Render);
	}

	private void AddProfileButton_Click(object sender, RoutedEventArgs e)
	{
		ProgramPickerWindow programPickerWindow = new ProgramPickerWindow();
		programPickerWindow.Owner = this;
		if (programPickerWindow.ShowDialog() != true || string.IsNullOrEmpty(programPickerWindow.SelectedPath))
		{
			return;
		}
		string procName = System.IO.Path.GetFileName(programPickerWindow.SelectedPath).ToLower();
		if (ConfigManager.CurrentConfig.Profiles.Any((WheelProfile p) => p.ProcessName.Equals(procName, StringComparison.OrdinalIgnoreCase)))
		{
			System.Windows.MessageBox.Show("已存在该程序的配置方案！", "提示", MessageBoxButton.OK, MessageBoxImage.Asterisk);
			return;
		}
		int num = _selectedProfile?.SectorCount ?? 8;
		WheelProfile wheelProfile = new WheelProfile
		{
			ProcessName = procName,
			SectorCount = num,
			Actions = new List<ActionItem>()
		};
		for (int num2 = 0; num2 < num; num2++)
		{
			wheelProfile.Actions.Add(new ActionItem
			{
				Type = "Hotkey",
				Name = $"动作 {num2 + 1}",
				Parameter = ""
			});
		}
		ConfigManager.CurrentConfig.Profiles.Add(wheelProfile);
		ProfilesListBox.Items.Refresh();
		ProfilesListBox.SelectedItem = wheelProfile;
		SyncUiToConfigAndSave();
	}

	private void AddCustomProfileButton_Click(object sender, RoutedEventArgs e)
	{
		InputDialog inputDialog = new InputDialog("新建自定义配置", "请输入新配置方案名称（如：游戏模式、绘图工作流、PS修图 或 myapp.exe）：", $"自定义配置_{ConfigManager.CurrentConfig.Profiles.Count}", (string input) => ConfigManager.CurrentConfig.Profiles.Any((WheelProfile p) => p.ProcessName.Equals(input, StringComparison.OrdinalIgnoreCase)) ? (IsValid: false, ErrorMessage: "已存在同名的配置方案，请换一个名称！") : (IsValid: true, ErrorMessage: ""));
		inputDialog.Owner = this;
		if (inputDialog.ShowDialog() == true && !string.IsNullOrEmpty(inputDialog.InputText))
		{
			int num = _selectedProfile?.SectorCount ?? 8;
			WheelProfile wheelProfile = new WheelProfile
			{
				ProcessName = inputDialog.InputText,
				SectorCount = num,
				Actions = new List<ActionItem>()
			};
			for (int num2 = 0; num2 < num; num2++)
			{
				wheelProfile.Actions.Add(new ActionItem
				{
					Type = "Hotkey",
					Name = $"动作 {num2 + 1}",
					Parameter = ""
				});
			}
			ConfigManager.CurrentConfig.Profiles.Add(wheelProfile);
			ProfilesListBox.Items.Refresh();
			ProfilesListBox.SelectedItem = wheelProfile;
			SyncUiToConfigAndSave();
		}
	}

	private void RenameProfileButton_Click(object sender, RoutedEventArgs e)
	{
		if (_selectedProfile == null)
		{
			System.Windows.MessageBox.Show("请先在列表中选择要重命名的配置方案！", "提示", MessageBoxButton.OK, MessageBoxImage.Asterisk);
			return;
		}
		if (_selectedProfile.ProcessName.Equals("Global", StringComparison.OrdinalIgnoreCase))
		{
			System.Windows.MessageBox.Show("「Global」为系统全局默认基础配置，不可重命名。", "提示", MessageBoxButton.OK, MessageBoxImage.Asterisk);
			return;
		}
		string oldName = _selectedProfile.ProcessName;
		InputDialog inputDialog = new InputDialog("重命名配置方案", "请输入配置方案「" + oldName + "」的新名称：", oldName, delegate(string input)
		{
			if (input.Equals(oldName, StringComparison.OrdinalIgnoreCase))
			{
				return (IsValid: true, ErrorMessage: "");
			}
			return ConfigManager.CurrentConfig.Profiles.Any((WheelProfile p) => p.ProcessName.Equals(input, StringComparison.OrdinalIgnoreCase)) ? (IsValid: false, ErrorMessage: "已存在同名的配置方案，请换一个名称！") : (IsValid: true, ErrorMessage: "");
		});
		inputDialog.Owner = this;
		if (inputDialog.ShowDialog() == true && !string.IsNullOrEmpty(inputDialog.InputText))
		{
			_selectedProfile.ProcessName = inputDialog.InputText;
			ProfilesListBox.Items.Refresh();
			SyncUiToConfigAndSave();
		}
	}

	private void ProfilesListBox_MouseDoubleClick(object sender, MouseButtonEventArgs e)
	{
		if (ProfilesListBox.SelectedItem is WheelProfile wheelProfile && !wheelProfile.ProcessName.Equals("Global", StringComparison.OrdinalIgnoreCase))
		{
			RenameProfileButton_Click(sender, e);
		}
	}

	private void DeleteProfileButton_Click(object sender, RoutedEventArgs e)
	{
		if (_selectedProfile != null)
		{
			if (_selectedProfile.ProcessName.Equals("Global", StringComparison.OrdinalIgnoreCase))
			{
				System.Windows.MessageBox.Show("全局默认配置 (Global) 不能删除！", "提示", MessageBoxButton.OK, MessageBoxImage.Exclamation);
			}
			else if (System.Windows.MessageBox.Show("确定要删除配置方案 [" + _selectedProfile.ProcessName + "] 吗？", "确认删除", MessageBoxButton.YesNo, MessageBoxImage.Question) == MessageBoxResult.Yes)
			{
				ConfigManager.CurrentConfig.Profiles.Remove(_selectedProfile);
				ProfilesListBox.Items.Refresh();
				ProfilesListBox.SelectedIndex = 0;
				SyncUiToConfigAndSave();
			}
		}
	}

	#region Tab 2 Mappings Dual-Column Canvas & Focus Editor

	private void MappingsViewMode_Checked(object sender, RoutedEventArgs e)
	{
		if (MappingsCanvasModeGrid == null || MappingsListModeGrid == null) return;
		if (MappingsViewModeCanvasRadio != null && MappingsViewModeCanvasRadio.IsChecked == true)
		{
			MappingsCanvasModeGrid.Visibility = Visibility.Visible;
			MappingsListModeGrid.Visibility = Visibility.Collapsed;
			RenderMappingsWheelPreview();
		}
		else
		{
			MappingsCanvasModeGrid.Visibility = Visibility.Collapsed;
			MappingsListModeGrid.Visibility = Visibility.Visible;
		}
	}

	private void MappingsProfileComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
	{
		if (_isUpdatingUi) return;
		if (MappingsProfileComboBox.SelectedItem is WheelProfile profile)
		{
			_selectedProfile = profile;
			_isUpdatingUi = true;
			try
			{
				if (ProfilesListBox != null) ProfilesListBox.SelectedItem = profile;
				if (SectorCount4Radio != null) SectorCount4Radio.IsChecked = profile.SectorCount == 4;
				if (SectorCount8Radio != null) SectorCount8Radio.IsChecked = profile.SectorCount == 8;
				if (SectorCount12Radio != null) SectorCount12Radio.IsChecked = profile.SectorCount == 12;
				if (MappingsSectorCount4Radio != null) MappingsSectorCount4Radio.IsChecked = profile.SectorCount == 4;
				if (MappingsSectorCount8Radio != null) MappingsSectorCount8Radio.IsChecked = profile.SectorCount == 8;
				if (MappingsSectorCount12Radio != null) MappingsSectorCount12Radio.IsChecked = profile.SectorCount == 12;
				RefreshSlots();
				UpdateFocusEditorUi();
				RenderMappingsWheelPreview();
			}
			finally
			{
				_isUpdatingUi = false;
			}
		}
	}

	private void DuplicateProfileBtn_Click(object sender, RoutedEventArgs e)
	{
		if (_selectedProfile == null) return;
		InputDialog inputDialog = new InputDialog("复制配置方案", "请输入新配置方案名称（如程序名或工作流名）：", _selectedProfile.ProcessName + " - 副本");
		inputDialog.Owner = this;
		if (inputDialog.ShowDialog() == true && !string.IsNullOrWhiteSpace(inputDialog.InputText))
		{
			string newName = inputDialog.InputText.Trim();
			if (ConfigManager.CurrentConfig.Profiles.Any(p => p.ProcessName.Equals(newName, StringComparison.OrdinalIgnoreCase)))
			{
				System.Windows.MessageBox.Show(this, "已存在同名的配置方案，请换一个名称！", "提示", MessageBoxButton.OK, MessageBoxImage.Warning);
				return;
			}
			WheelProfile newProfile = new WheelProfile
			{
				ProcessName = newName,
				SectorCount = _selectedProfile.SectorCount,
				EnableCenterAction = _selectedProfile.EnableCenterAction,
				CenterAction = _selectedProfile.CenterAction != null ? new ActionItem
				{
					Name = _selectedProfile.CenterAction.Name,
					Type = _selectedProfile.CenterAction.Type,
					Parameter = _selectedProfile.CenterAction.Parameter,
					Arguments = _selectedProfile.CenterAction.Arguments,
					IconKey = _selectedProfile.CenterAction.IconKey,
					CustomIconSvg = _selectedProfile.CenterAction.CustomIconSvg,
					CommandTerminal = _selectedProfile.CenterAction.CommandTerminal,
					BrowserChoice = _selectedProfile.CenterAction.BrowserChoice,
					BrowserPath = _selectedProfile.CenterAction.BrowserPath
				} : null,
				Actions = _selectedProfile.Actions.Select(a => new ActionItem
				{
					Name = a.Name,
					Type = a.Type,
					Parameter = a.Parameter,
					Arguments = a.Arguments,
					IconKey = a.IconKey,
					CustomIconSvg = a.CustomIconSvg,
					CommandTerminal = a.CommandTerminal,
					BrowserChoice = a.BrowserChoice,
					BrowserPath = a.BrowserPath,
					SubActions = a.SubActions?.Select(sub => new ActionItem
					{
						Name = sub.Name,
						Type = sub.Type,
						Parameter = sub.Parameter,
						Arguments = sub.Arguments,
						IconKey = sub.IconKey,
						CustomIconSvg = sub.CustomIconSvg,
						CommandTerminal = sub.CommandTerminal,
						BrowserChoice = sub.BrowserChoice,
						BrowserPath = sub.BrowserPath
					}).ToList()
				}).ToList()
			};
			ConfigManager.CurrentConfig.Profiles.Add(newProfile);
			ConfigManager.SaveConfig();
			ProfilesListBox.Items.Refresh();
			if (MappingsProfileComboBox != null)
			{
				MappingsProfileComboBox.ItemsSource = null;
				MappingsProfileComboBox.ItemsSource = ConfigManager.CurrentConfig.Profiles;
				MappingsProfileComboBox.SelectedItem = newProfile;
			}
			ProfilesListBox.SelectedItem = newProfile;
		}
	}

	private void MappingsSectorCountRadio_Checked(object sender, RoutedEventArgs e)
	{
		if (_isUpdatingUi || _selectedProfile == null) return;
		int count = 8;
		if (MappingsSectorCount4Radio != null && MappingsSectorCount4Radio.IsChecked == true) count = 4;
		else if (MappingsSectorCount12Radio != null && MappingsSectorCount12Radio.IsChecked == true) count = 12;

		_selectedProfile.SectorCount = count;
		_isUpdatingUi = true;
		try
		{
			if (SectorCount4Radio != null) SectorCount4Radio.IsChecked = count == 4;
			if (SectorCount8Radio != null) SectorCount8Radio.IsChecked = count == 8;
			if (SectorCount12Radio != null) SectorCount12Radio.IsChecked = count == 12;
			RefreshSlots();
			UpdateFocusEditorUi();
			RenderMappingsWheelPreview();
			ScheduleAutoSave();
		}
		finally
		{
			_isUpdatingUi = false;
		}
	}

	public void SelectPrimarySlot(int slotIndex)
	{
		int count = _selectedProfile?.SectorCount ?? 8;
		_selectedSlotIndex = Math.Max(0, Math.Min(slotIndex, count - 1));
		_selectedSubActionIndex = null;
		UpdateFocusEditorUi();
		RenderMappingsWheelPreview();
	}

	public void SelectCenterCore()
	{
		_selectedSlotIndex = -1;
		_selectedSubActionIndex = null;
		UpdateFocusEditorUi();
		RenderMappingsWheelPreview();
	}

	public void SelectSubAction(int parentSlotIndex, int subIndex)
	{
		_selectedSlotIndex = parentSlotIndex;
		_selectedSubActionIndex = subIndex;
		UpdateFocusEditorUi();
		RenderMappingsWheelPreview();
	}

	private void FocusBackToParentBtn_Click(object sender, RoutedEventArgs e)
	{
		SelectPrimarySlot(_selectedSlotIndex);
	}

	private void FocusPrevSlotBtn_Click(object sender, RoutedEventArgs e)
	{
		int count = _selectedProfile?.SectorCount ?? 8;
		if (_selectedSlotIndex == -1)
		{
			SelectPrimarySlot(count - 1);
		}
		else if (_selectedSlotIndex == 0)
		{
			SelectCenterCore();
		}
		else
		{
			SelectPrimarySlot(_selectedSlotIndex - 1);
		}
	}

	private void FocusNextSlotBtn_Click(object sender, RoutedEventArgs e)
	{
		int count = _selectedProfile?.SectorCount ?? 8;
		if (_selectedSlotIndex == -1)
		{
			SelectPrimarySlot(0);
		}
		else if (_selectedSlotIndex == count - 1)
		{
			SelectCenterCore();
		}
		else
		{
			SelectPrimarySlot(_selectedSlotIndex + 1);
		}
	}

	private void FocusCenterCoreBtn_Click(object sender, RoutedEventArgs e)
	{
		SelectCenterCore();
	}

	private ActionItem? GetCurrentFocusActionItem()
	{
		WheelProfile? profile = _selectedProfile ?? ConfigManager.CurrentConfig?.Profiles.FirstOrDefault();
		if (profile == null) return null;
		if (_selectedSlotIndex == -1)
		{
			profile.CenterAction ??= new ActionItem
			{
				Name = "StarPie控制台",
				Type = "System",
				Parameter = "OpenSettings",
				IconKey = "Settings"
			};
			return profile.CenterAction;
		}
		if (profile.Actions == null || _selectedSlotIndex < 0 || _selectedSlotIndex >= profile.Actions.Count)
		{
			return null;
		}
		ActionItem primaryAction = profile.Actions[_selectedSlotIndex];
		if (_selectedSubActionIndex.HasValue)
		{
			if (primaryAction.SubActions != null && _selectedSubActionIndex.Value >= 0 && _selectedSubActionIndex.Value < primaryAction.SubActions.Count)
			{
				return primaryAction.SubActions[_selectedSubActionIndex.Value];
			}
			return null;
		}
		return primaryAction;
	}

	private void UpdateFocusEditorUi()
	{
		if (_isUpdatingFocusUi) return;
		_isUpdatingFocusUi = true;
		try
		{
			ActionItem? item = GetCurrentFocusActionItem();
			if (item == null) return;

			WheelProfile profile = _selectedProfile ?? ConfigManager.CurrentConfig?.Profiles.FirstOrDefault() ?? new WheelProfile();
			int sectorCount = profile.SectorCount > 0 ? profile.SectorCount : 8;
			string[] directions = sectorCount switch
			{
				4 => Directions4,
				12 => Directions12,
				_ => Directions8
			};

			if (_selectedSlotIndex == -1)
			{
				// Center Core
				if (FocusSlotBadgeBorder != null) FocusSlotBadgeBorder.Background = new SolidColorBrush(System.Windows.Media.Color.FromRgb(245, 158, 11));
				if (FocusSlotBadgeText != null) FocusSlotBadgeText.Text = "🎯";
				if (FocusSlotTitleText != null) FocusSlotTitleText.Text = "中心核心圆动作 (Center Core)";
				if (FocusSlotTagText != null) FocusSlotTagText.Text = "核心圆";
				if (FocusSlotSubtitleText != null) FocusSlotSubtitleText.Text = "在开启外甩脱离取消时，鼠标在中心内径死区内松开即可触发";
				if (FocusBackToParentBtn != null) FocusBackToParentBtn.Visibility = Visibility.Collapsed;
				if (FocusCenterCoreBanner != null) FocusCenterCoreBanner.Visibility = Visibility.Visible;
				if (EnableCenterActionCheckBox != null) EnableCenterActionCheckBox.IsChecked = profile.EnableCenterAction;
				if (FocusSubActionsBorder != null) FocusSubActionsBorder.Visibility = Visibility.Collapsed;
			}
			else if (_selectedSubActionIndex.HasValue)
			{
				// Secondary SubAction
				if (FocusSlotBadgeBorder != null) FocusSlotBadgeBorder.Background = new SolidColorBrush(System.Windows.Media.Color.FromRgb(168, 85, 247));
				if (FocusSlotBadgeText != null) FocusSlotBadgeText.Text = "🌟";
				string parentDir = (_selectedSlotIndex >= 0 && _selectedSlotIndex < directions.Length) ? directions[_selectedSlotIndex] : $"{_selectedSlotIndex + 1}";
				if (FocusSlotTitleText != null) FocusSlotTitleText.Text = $"二级动作 [{item.Name}]";
				if (FocusSlotTagText != null) FocusSlotTagText.Text = $"所属父级: 扇区 {_selectedSlotIndex + 1} [{parentDir}]";
				if (FocusSlotSubtitleText != null) FocusSlotSubtitleText.Text = "向外划动二级扇区即可触发此动作";
				if (FocusBackToParentBtn != null) FocusBackToParentBtn.Visibility = Visibility.Visible;
				if (FocusCenterCoreBanner != null) FocusCenterCoreBanner.Visibility = Visibility.Collapsed;
				if (FocusSubActionsBorder != null) FocusSubActionsBorder.Visibility = Visibility.Collapsed;
			}
			else
			{
				// Primary Sector
				if (FocusSlotBadgeBorder != null) FocusSlotBadgeBorder.Background = (System.Windows.Media.Brush)FindResource("AccentPrimaryBrush");
				string dirName = (_selectedSlotIndex >= 0 && _selectedSlotIndex < directions.Length) ? directions[_selectedSlotIndex] : $"{_selectedSlotIndex + 1}";
				string badgeChar = dirName.Length > 0 ? dirName.Substring(0, 1) : $"{_selectedSlotIndex + 1}";
				if (FocusSlotBadgeText != null) FocusSlotBadgeText.Text = badgeChar;
				if (FocusSlotTitleText != null) FocusSlotTitleText.Text = $"扇区 {_selectedSlotIndex + 1} [{dirName}]";
				if (FocusSlotTagText != null) FocusSlotTagText.Text = "一级主扇区";
				if (FocusSlotSubtitleText != null) FocusSlotSubtitleText.Text = "点击右侧轮盘直接选中扇区，或在下方配置动作与级联子菜单";
				if (FocusBackToParentBtn != null) FocusBackToParentBtn.Visibility = Visibility.Collapsed;
				if (FocusCenterCoreBanner != null) FocusCenterCoreBanner.Visibility = Visibility.Collapsed;
				if (FocusSubActionsBorder != null) FocusSubActionsBorder.Visibility = Visibility.Visible;
				RefreshFocusSubActionsChips();
			}

			// Name & Icon
			if (FocusActionNameTextBox != null) FocusActionNameTextBox.Text = item.Name ?? "";
			string iconKey = item.IconKey ?? "";
			if (FocusIconLabel != null) FocusIconLabel.Text = !string.IsNullOrEmpty(iconKey) ? iconKey : "图标...";
			string svg = !string.IsNullOrEmpty(item.CustomIconSvg) ? item.CustomIconSvg : IconHelper.GetSvgPathByKey(iconKey);
			if (FocusIconPath != null)
			{
				try
				{
					FocusIconPath.Data = !string.IsNullOrEmpty(svg) ? Geometry.Parse(svg) : null;
				}
				catch
				{
					FocusIconPath.Data = null;
				}
			}

			// Action Type & Panels
			string type = !string.IsNullOrEmpty(item.Type) ? item.Type : "Hotkey";
			if (FocusActionTypeComboBox != null) FocusActionTypeComboBox.SelectedValue = type;

			if (FocusHotkeyPanel != null) FocusHotkeyPanel.Visibility = type == "Hotkey" ? Visibility.Visible : Visibility.Collapsed;
			if (FocusLaunchPanel != null) FocusLaunchPanel.Visibility = (type == "Launch" || type == "App") ? Visibility.Visible : Visibility.Collapsed;
			if (FocusWebUrlPanel != null) FocusWebUrlPanel.Visibility = (type == "WebUrl" || type == "Url") ? Visibility.Visible : Visibility.Collapsed;
			if (FocusFolderPanel != null) FocusFolderPanel.Visibility = (type == "Folder" || type == "OpenFolder") ? Visibility.Visible : Visibility.Collapsed;
			if (FocusCommandPanel != null) FocusCommandPanel.Visibility = type == "Command" ? Visibility.Visible : Visibility.Collapsed;
			if (FocusSwitchWindowPanel != null) FocusSwitchWindowPanel.Visibility = type == "SwitchWindow" ? Visibility.Visible : Visibility.Collapsed;
			if (FocusSystemPanel != null) FocusSystemPanel.Visibility = type == "System" ? Visibility.Visible : Visibility.Collapsed;

			// Parameters
			if (FocusHotkeyRecorder != null) FocusHotkeyRecorder.HotkeyText = item.Parameter ?? "";
			if (FocusLaunchPathTextBox != null) FocusLaunchPathTextBox.Text = item.Parameter ?? "";
			if (FocusLaunchArgsTextBox != null) FocusLaunchArgsTextBox.Text = item.Arguments ?? "";
			if (FocusWebUrlTextBox != null) FocusWebUrlTextBox.Text = item.Parameter ?? "";
			if (FocusWebBrowserComboBox != null)
			{
				string browser = item.BrowserChoice ?? "Default";
				foreach (ComboBoxItem cbi in FocusWebBrowserComboBox.Items)
				{
					if (string.Equals(cbi.Tag?.ToString(), browser, StringComparison.OrdinalIgnoreCase))
					{
						FocusWebBrowserComboBox.SelectedItem = cbi;
						break;
					}
				}
			}
			if (FocusCustomBrowserPathPanel != null)
			{
				FocusCustomBrowserPathPanel.Visibility = string.Equals(item.BrowserChoice, "Custom", StringComparison.OrdinalIgnoreCase) ? Visibility.Visible : Visibility.Collapsed;
			}
			if (FocusCustomBrowserPathTextBox != null) FocusCustomBrowserPathTextBox.Text = item.BrowserPath ?? "";
			if (FocusFolderPathTextBox != null) FocusFolderPathTextBox.Text = item.Parameter ?? "";
			if (FocusCommandTextBox != null) FocusCommandTextBox.Text = item.Parameter ?? "";
			if (FocusCommandTerminalComboBox != null) FocusCommandTerminalComboBox.SelectedValue = item.CommandTerminal ?? "cmd";
			if (FocusSwitchWindowTextBox != null) FocusSwitchWindowTextBox.Text = item.Parameter ?? "1";
			if (FocusSystemPresetComboBox != null) FocusSystemPresetComboBox.SelectedValue = item.Parameter ?? "OpenSettings";
		}
		finally
		{
			_isUpdatingFocusUi = false;
		}
	}

	private void RefreshFocusSubActionsChips()
	{
		if (FocusSubActionsChipsPanel == null) return;
		FocusSubActionsChipsPanel.Children.Clear();
		if (_selectedSlotIndex < 0) return;
		WheelProfile? profile = _selectedProfile ?? ConfigManager.CurrentConfig?.Profiles.FirstOrDefault();
		if (profile?.Actions == null || _selectedSlotIndex >= profile.Actions.Count) return;
		ActionItem primaryAction = profile.Actions[_selectedSlotIndex];
		var subActions = primaryAction.SubActions;
		int count = subActions?.Count ?? 0;
		if (FocusSubActionsCountLabel != null)
		{
			FocusSubActionsCountLabel.Text = $"({count} 项)";
		}
		if (subActions == null || subActions.Count == 0)
		{
			TextBlock emptyText = new TextBlock
			{
				Text = "暂无二级级联子动作，点击上方【➕ 添加二级动作】添加",
				FontSize = 11,
				Foreground = (System.Windows.Media.Brush)FindResource("TextMutedBrush"),
				Margin = new Thickness(2, 4, 0, 4)
			};
			FocusSubActionsChipsPanel.Children.Add(emptyText);
			return;
		}

		for (int i = 0; i < subActions.Count; i++)
		{
			int subIdx = i;
			ActionItem subItem = subActions[i];
			bool isSelected = (_selectedSubActionIndex == subIdx);

			Border chip = new Border
			{
				Background = isSelected 
					? (System.Windows.Media.Brush)FindResource("NavTabActiveBgBrush") 
					: (System.Windows.Media.Brush)FindResource("CardBackgroundBrush"),
				BorderBrush = isSelected 
					? (System.Windows.Media.Brush)FindResource("AccentPrimaryBrush") 
					: (System.Windows.Media.Brush)FindResource("CardBorderBrush"),
				BorderThickness = new Thickness(isSelected ? 1.5 : 1.0),
				CornerRadius = new CornerRadius(6),
				Padding = new Thickness(8, 4, 6, 4),
				Margin = new Thickness(0, 0, 6, 6),
				Cursor = System.Windows.Input.Cursors.Hand
			};

			Grid chipGrid = new Grid();
			chipGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
			chipGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
			chipGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

			string iconSvg = !string.IsNullOrEmpty(subItem.CustomIconSvg) 
				? subItem.CustomIconSvg 
				: IconHelper.GetSvgPathByKey(subItem.IconKey);
			if (!string.IsNullOrEmpty(iconSvg))
			{
				try
				{
					System.Windows.Shapes.Path iconPath = new System.Windows.Shapes.Path
					{
						Data = Geometry.Parse(iconSvg),
						Fill = (System.Windows.Media.Brush)FindResource("AccentPrimaryBrush"),
						Width = 12,
						Height = 12,
						Stretch = Stretch.Uniform,
						Margin = new Thickness(0, 0, 4, 0),
						VerticalAlignment = VerticalAlignment.Center
					};
					Grid.SetColumn(iconPath, 0);
					chipGrid.Children.Add(iconPath);
				}
				catch { }
			}

			TextBlock textBlock = new TextBlock
			{
				Text = string.IsNullOrEmpty(subItem.Name) ? $"子动作 {subIdx + 1}" : subItem.Name,
				FontSize = 11,
				FontWeight = isSelected ? FontWeights.SemiBold : FontWeights.Normal,
				Foreground = isSelected 
					? (System.Windows.Media.Brush)FindResource("AccentPrimaryBrush") 
					: (System.Windows.Media.Brush)FindResource("TextPrimaryBrush"),
				VerticalAlignment = VerticalAlignment.Center,
				Margin = new Thickness(0, 0, 6, 0)
			};
			Grid.SetColumn(textBlock, 1);
			chipGrid.Children.Add(textBlock);

			TextBlock deleteBtn = new TextBlock
			{
				Text = "✕",
				FontSize = 10,
				Foreground = (System.Windows.Media.Brush)FindResource("TextSecondaryBrush"),
				VerticalAlignment = VerticalAlignment.Center,
				Cursor = System.Windows.Input.Cursors.Hand,
				Padding = new Thickness(2)
			};
			deleteBtn.MouseEnter += (s, e) => deleteBtn.Foreground = new SolidColorBrush(System.Windows.Media.Color.FromRgb(239, 68, 68));
			deleteBtn.MouseLeave += (s, e) => deleteBtn.Foreground = (System.Windows.Media.Brush)FindResource("TextSecondaryBrush");
			deleteBtn.MouseLeftButtonDown += (s, e) =>
			{
				e.Handled = true;
				primaryAction.SubActions.RemoveAt(subIdx);
				if (_selectedSubActionIndex == subIdx) _selectedSubActionIndex = null;
				else if (_selectedSubActionIndex > subIdx) _selectedSubActionIndex--;
				UpdateFocusEditorUi();
				RefreshSlots();
				RenderMappingsWheelPreview();
				ScheduleAutoSave();
			};
			Grid.SetColumn(deleteBtn, 2);
			chipGrid.Children.Add(deleteBtn);

			chip.Child = chipGrid;
			chip.MouseLeftButtonDown += (s, e) =>
			{
				e.Handled = true;
				SelectSubAction(_selectedSlotIndex, subIdx);
			};

			FocusSubActionsChipsPanel.Children.Add(chip);
		}
	}

	private void FocusAddSubActionBtn_Click(object sender, RoutedEventArgs e)
	{
		if (_selectedSlotIndex < 0) return;
		WheelProfile? profile = _selectedProfile ?? ConfigManager.CurrentConfig?.Profiles.FirstOrDefault();
		if (profile?.Actions == null || _selectedSlotIndex >= profile.Actions.Count) return;
		ActionItem primaryAction = profile.Actions[_selectedSlotIndex];
		primaryAction.SubActions ??= new List<ActionItem>();
		if (primaryAction.SubActions.Count >= 4)
		{
			System.Windows.MessageBox.Show(this, "每个主扇区最多支持配置 4 个二级级联子动作。", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
			return;
		}
		int newIdx = primaryAction.SubActions.Count + 1;
		primaryAction.SubActions.Add(new ActionItem
		{
			Name = $"子动作 {newIdx}",
			Type = "Hotkey",
			Parameter = "",
			IconKey = ""
		});
		RefreshFocusSubActionsChips();
		RefreshSlots();
		RenderMappingsWheelPreview();
		ScheduleAutoSave();
	}

	private void FocusClearSubActionsBtn_Click(object sender, RoutedEventArgs e)
	{
		if (_selectedSlotIndex < 0) return;
		WheelProfile? profile = _selectedProfile ?? ConfigManager.CurrentConfig?.Profiles.FirstOrDefault();
		if (profile?.Actions == null || _selectedSlotIndex >= profile.Actions.Count) return;
		ActionItem primaryAction = profile.Actions[_selectedSlotIndex];
		if (primaryAction.SubActions != null && primaryAction.SubActions.Count > 0)
		{
			if (System.Windows.MessageBox.Show(this, "确定要清空该扇区的所有二级动作吗？", "确认清空", MessageBoxButton.YesNo, MessageBoxImage.Question) == MessageBoxResult.Yes)
			{
				primaryAction.SubActions.Clear();
				_selectedSubActionIndex = null;
				UpdateFocusEditorUi();
				RefreshSlots();
				RenderMappingsWheelPreview();
				ScheduleAutoSave();
			}
		}
	}

	private void EnableCenterActionCheckBox_Changed(object sender, RoutedEventArgs e)
	{
		if (_isUpdatingFocusUi || _selectedProfile == null) return;
		_selectedProfile.EnableCenterAction = (EnableCenterActionCheckBox.IsChecked == true);
		ScheduleAutoSave();
	}

	private void CenterPresetsToggleBtn_Click(object sender, RoutedEventArgs e)
	{
		if (CenterPresetsContainer == null) return;
		CenterPresetsContainer.Visibility = (CenterPresetsContainer.Visibility == Visibility.Visible) ? Visibility.Collapsed : Visibility.Visible;
	}

	private void CenterInfoToggleBtn_Click(object sender, RoutedEventArgs e)
	{
		if (CenterInfoContainer == null) return;
		CenterInfoContainer.Visibility = (CenterInfoContainer.Visibility == Visibility.Visible) ? Visibility.Collapsed : Visibility.Visible;
	}

	private void ApplyCenterPreset_OpenSettings(object sender, RoutedEventArgs e)
	{
		SetCenterCoreAction("StarPie控制台", "System", "OpenSettings", "Settings");
	}

	private void ApplyCenterPreset_Desktop(object sender, RoutedEventArgs e)
	{
		SetCenterCoreAction("显示桌面", "System", "ShowDesktop", "ShowDesktop");
	}

	private void ApplyCenterPreset_Lock(object sender, RoutedEventArgs e)
	{
		SetCenterCoreAction("锁定屏幕", "System", "Lock", "Lock");
	}

	private void ApplyCenterPreset_WebUrl(object sender, RoutedEventArgs e)
	{
		SetCenterCoreAction("GitHub", "WebUrl", "https://github.com", "Globe");
	}

	private void ApplyCenterPreset_Explorer(object sender, RoutedEventArgs e)
	{
		SetCenterCoreAction("资源管理", "System", "Explorer", "Explorer");
	}

	private void SetCenterCoreAction(string name, string type, string parameter, string iconKey)
	{
		if (_selectedProfile == null) return;
		_selectedProfile.CenterAction ??= new ActionItem();
		_selectedProfile.CenterAction.Name = name;
		_selectedProfile.CenterAction.Type = type;
		_selectedProfile.CenterAction.Parameter = parameter;
		_selectedProfile.CenterAction.IconKey = iconKey;
		_selectedProfile.EnableCenterAction = true;
		if (EnableCenterActionCheckBox != null) EnableCenterActionCheckBox.IsChecked = true;
		UpdateFocusEditorUi();
		RefreshSlots();
		RenderMappingsWheelPreview();
		ScheduleAutoSave();
	}

	private void FocusPickIcon_Click(object sender, RoutedEventArgs e)
	{
		ActionItem? item = GetCurrentFocusActionItem();
		if (item == null) return;
		IconPickerWindow iconPickerWindow = new IconPickerWindow(item.IconKey);
		iconPickerWindow.Owner = this;
		if (iconPickerWindow.ShowDialog() == true)
		{
			item.IconKey = iconPickerWindow.SelectedIconKey ?? "";
			UpdateFocusEditorUi();
			RefreshSlots();
			RenderMappingsWheelPreview();
			ScheduleAutoSave();
		}
	}

	private void FocusActionNameTextBox_TextChanged(object sender, TextChangedEventArgs e)
	{
		if (_isUpdatingFocusUi) return;
		ActionItem? item = GetCurrentFocusActionItem();
		if (item != null)
		{
			item.Name = FocusActionNameTextBox.Text;
			RefreshSlots();
			RenderMappingsWheelPreview();
			ScheduleAutoSave();
		}
	}

	private void FocusActionTypeComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
	{
		if (_isUpdatingFocusUi) return;
		ActionItem? item = GetCurrentFocusActionItem();
		if (item != null && FocusActionTypeComboBox.SelectedValue is string newType)
		{
			item.Type = newType;
			if ((newType == "Folder" || newType == "OpenFolder") && string.IsNullOrEmpty(item.IconKey))
			{
				item.IconKey = "Folder";
			}
			else if ((newType == "WebUrl" || newType == "Url") && string.IsNullOrEmpty(item.IconKey))
			{
				item.IconKey = "Globe";
			}
			UpdateFocusEditorUi();
			RefreshSlots();
			RenderMappingsWheelPreview();
			ScheduleAutoSave();
		}
	}

	private void FocusTestActionBtn_Click(object sender, RoutedEventArgs e)
	{
		ActionItem? item = GetCurrentFocusActionItem();
		if (item != null)
		{
			ActionExecutor.Execute(item);
		}
	}

	private void FocusHotkeyBuilder_Click(object sender, RoutedEventArgs e)
	{
		ActionItem? item = GetCurrentFocusActionItem();
		if (item == null) return;
		HotkeyBuilderDialog dlg = new HotkeyBuilderDialog(item.Parameter ?? "")
		{
			Owner = this
		};
		if (dlg.ShowDialog() == true)
		{
			item.Parameter = dlg.ResultHotkey;
			if (FocusHotkeyRecorder != null)
			{
				FocusHotkeyRecorder.HotkeyText = dlg.ResultHotkey;
			}
			RefreshSlots();
			RenderMappingsWheelPreview();
			ScheduleAutoSave();
		}
	}

	private void FocusCaptureProcess_Click(object sender, RoutedEventArgs e)
	{
		ActionItem? item = GetCurrentFocusActionItem();
		if (item == null) return;
		ProgramPickerWindow programPicker = new ProgramPickerWindow();
		programPicker.Owner = this;
		if (programPicker.ShowDialog() == true && !string.IsNullOrEmpty(programPicker.SelectedPath))
		{
			item.Parameter = programPicker.SelectedPath;
			FocusLaunchPathTextBox.Text = programPicker.SelectedPath;
			if (string.IsNullOrWhiteSpace(item.Name) || item.Name.StartsWith("快捷动作") || item.Name.StartsWith("启动"))
			{
				string autoName = !string.IsNullOrEmpty(programPicker.SelectedName) 
					? programPicker.SelectedName 
					: System.IO.Path.GetFileNameWithoutExtension(programPicker.SelectedPath);
				item.Name = autoName;
				FocusActionNameTextBox.Text = autoName;
			}
			RefreshSlots();
			RenderMappingsWheelPreview();
			ScheduleAutoSave();
		}
	}

	private void FocusBrowseLaunchExe_Click(object sender, RoutedEventArgs e)
	{
		ActionItem? item = GetCurrentFocusActionItem();
		if (item == null) return;
		Microsoft.Win32.OpenFileDialog dlg = new Microsoft.Win32.OpenFileDialog
		{
			Filter = "应用程序 (*.exe;*.lnk;*.bat;*.cmd)|*.exe;*.lnk;*.bat;*.cmd|所有文件 (*.*)|*.*",
			Title = "选择要启动的应用程序或快捷方式"
		};
		if (dlg.ShowDialog(this) == true)
		{
			item.Parameter = dlg.FileName;
			FocusLaunchPathTextBox.Text = dlg.FileName;
			if (string.IsNullOrWhiteSpace(item.Name) || item.Name.StartsWith("快捷动作") || item.Name.StartsWith("启动"))
			{
				string autoName = System.IO.Path.GetFileNameWithoutExtension(dlg.FileName);
				item.Name = autoName;
				FocusActionNameTextBox.Text = autoName;
			}
			RefreshSlots();
			RenderMappingsWheelPreview();
			ScheduleAutoSave();
		}
	}

	private void FocusLaunchPathTextBox_TextChanged(object sender, TextChangedEventArgs e)
	{
		if (_isUpdatingFocusUi) return;
		ActionItem? item = GetCurrentFocusActionItem();
		if (item != null) { item.Parameter = FocusLaunchPathTextBox.Text; ScheduleAutoSave(); }
	}

	private void FocusLaunchArgsTextBox_TextChanged(object sender, TextChangedEventArgs e)
	{
		if (_isUpdatingFocusUi) return;
		ActionItem? item = GetCurrentFocusActionItem();
		if (item != null) { item.Arguments = FocusLaunchArgsTextBox.Text; ScheduleAutoSave(); }
	}

	private void FocusWebUrlTextBox_TextChanged(object sender, TextChangedEventArgs e)
	{
		if (_isUpdatingFocusUi) return;
		ActionItem? item = GetCurrentFocusActionItem();
		if (item != null) { item.Parameter = FocusWebUrlTextBox.Text; ScheduleAutoSave(); }
	}

	private void FocusWebBrowserComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
	{
		if (_isUpdatingFocusUi) return;
		ActionItem? item = GetCurrentFocusActionItem();
		if (item != null && FocusWebBrowserComboBox.SelectedItem is ComboBoxItem cbi)
		{
			item.BrowserChoice = cbi.Tag?.ToString() ?? "Default";
			if (FocusCustomBrowserPathPanel != null)
			{
				FocusCustomBrowserPathPanel.Visibility = string.Equals(item.BrowserChoice, "Custom", StringComparison.OrdinalIgnoreCase) ? Visibility.Visible : Visibility.Collapsed;
			}
			ScheduleAutoSave();
		}
	}

	private void FocusCustomBrowserPathTextBox_TextChanged(object sender, TextChangedEventArgs e)
	{
		if (_isUpdatingFocusUi) return;
		ActionItem? item = GetCurrentFocusActionItem();
		if (item != null) { item.BrowserPath = FocusCustomBrowserPathTextBox.Text; ScheduleAutoSave(); }
	}

	private void FocusBrowseCustomBrowser_Click(object sender, RoutedEventArgs e)
	{
		ActionItem? item = GetCurrentFocusActionItem();
		if (item == null) return;
		Microsoft.Win32.OpenFileDialog dlg = new Microsoft.Win32.OpenFileDialog
		{
			Filter = "浏览器执行程序 (*.exe)|*.exe|所有文件 (*.*)|*.*",
			Title = "选择自定义浏览器执行程序"
		};
		if (dlg.ShowDialog(this) == true)
		{
			item.BrowserPath = dlg.FileName;
			FocusCustomBrowserPathTextBox.Text = dlg.FileName;
			ScheduleAutoSave();
		}
	}

	private void ApplyUrlPreset_GitHub(object sender, RoutedEventArgs e) { SetFocusWebUrl("https://github.com", "GitHub"); }
	private void ApplyUrlPreset_Bilibili(object sender, RoutedEventArgs e) { SetFocusWebUrl("https://www.bilibili.com", "哔哩哔哩"); }
	private void ApplyUrlPreset_Bing(object sender, RoutedEventArgs e) { SetFocusWebUrl("https://www.bing.com", "Bing 搜索"); }
	private void ApplyUrlPreset_Google(object sender, RoutedEventArgs e) { SetFocusWebUrl("https://www.google.com", "Google"); }

	private void SetFocusWebUrl(string url, string name)
	{
		ActionItem? item = GetCurrentFocusActionItem();
		if (item == null) return;
		item.Type = "WebUrl";
		item.Parameter = url;
		item.Name = name;
		if (string.IsNullOrEmpty(item.IconKey)) item.IconKey = "Globe";
		UpdateFocusEditorUi();
		RefreshSlots();
		RenderMappingsWheelPreview();
		ScheduleAutoSave();
	}

	private void FocusFolderPathTextBox_TextChanged(object sender, TextChangedEventArgs e)
	{
		if (_isUpdatingFocusUi) return;
		ActionItem? item = GetCurrentFocusActionItem();
		if (item != null) { item.Parameter = FocusFolderPathTextBox.Text; ScheduleAutoSave(); }
	}

	private void FocusBrowseFolder_Click(object sender, RoutedEventArgs e)
	{
		ActionItem? item = GetCurrentFocusActionItem();
		if (item == null) return;
		using System.Windows.Forms.FolderBrowserDialog fbd = new System.Windows.Forms.FolderBrowserDialog
		{
			Description = "选择要打开的文件夹",
			UseDescriptionForTitle = true,
			ShowNewFolderButton = true
		};
		if (fbd.ShowDialog() == System.Windows.Forms.DialogResult.OK)
		{
			item.Parameter = fbd.SelectedPath;
			FocusFolderPathTextBox.Text = fbd.SelectedPath;
			if (string.IsNullOrWhiteSpace(item.Name) || item.Name.StartsWith("快捷动作") || item.Name.StartsWith("文件夹"))
			{
				string autoName = System.IO.Path.GetFileName(fbd.SelectedPath);
				if (string.IsNullOrEmpty(autoName)) autoName = fbd.SelectedPath;
				item.Name = autoName;
				FocusActionNameTextBox.Text = autoName;
			}
			if (string.IsNullOrEmpty(item.IconKey))
			{
				item.IconKey = "Folder";
				FocusIconLabel.Text = "Folder";
				string folderSvg = IconHelper.GetSvgPathByKey("Folder");
				FocusIconPath.Data = !string.IsNullOrEmpty(folderSvg) ? Geometry.Parse(folderSvg) : null;
			}
			RefreshSlots();
			RenderMappingsWheelPreview();
			ScheduleAutoSave();
		}
	}

	private void ApplyFolderPreset_Desktop(object sender, RoutedEventArgs e) { SetFocusFolder(Environment.GetFolderPath(Environment.SpecialFolder.Desktop), "桌面"); }
	private void ApplyFolderPreset_Downloads(object sender, RoutedEventArgs e) { SetFocusFolder(System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Downloads"), "下载"); }
	private void ApplyFolderPreset_Documents(object sender, RoutedEventArgs e) { SetFocusFolder(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "文档"); }

	private void SetFocusFolder(string path, string name)
	{
		ActionItem? item = GetCurrentFocusActionItem();
		if (item == null) return;
		item.Type = "Folder";
		item.Parameter = path;
		item.Name = name;
		item.IconKey = "Folder";
		UpdateFocusEditorUi();
		RefreshSlots();
		RenderMappingsWheelPreview();
		ScheduleAutoSave();
	}

	private void FocusCommandTextBox_TextChanged(object sender, TextChangedEventArgs e)
	{
		if (_isUpdatingFocusUi) return;
		ActionItem? item = GetCurrentFocusActionItem();
		if (item != null) { item.Parameter = FocusCommandTextBox.Text; ScheduleAutoSave(); }
	}

	private void FocusCommandTerminalComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
	{
		if (_isUpdatingFocusUi) return;
		ActionItem? item = GetCurrentFocusActionItem();
		if (item != null && FocusCommandTerminalComboBox.SelectedValue is string terminal)
		{
			item.CommandTerminal = terminal;
			ScheduleAutoSave();
		}
	}

	private void FocusSwitchWindowTextBox_TextChanged(object sender, TextChangedEventArgs e)
	{
		if (_isUpdatingFocusUi) return;
		ActionItem? item = GetCurrentFocusActionItem();
		if (item != null) { item.Parameter = FocusSwitchWindowTextBox.Text; ScheduleAutoSave(); }
	}

	private void FocusSystemPresetComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
	{
		if (_isUpdatingFocusUi) return;
		ActionItem? item = GetCurrentFocusActionItem();
		if (item != null && FocusSystemPresetComboBox.SelectedValue is string presetKey)
		{
			item.Parameter = presetKey;
			SystemPresetItem? presetItem = SlotViewModel.SystemPresetList.FirstOrDefault(p => p.Key == presetKey);
			if (presetItem != null)
			{
				if (string.IsNullOrEmpty(item.Name) || item.Name.StartsWith("快捷动作"))
				{
					item.Name = presetItem.DefaultName;
					FocusActionNameTextBox.Text = presetItem.DefaultName;
				}
				if (string.IsNullOrEmpty(item.IconKey))
				{
					item.IconKey = presetItem.DefaultIconKey;
					FocusIconLabel.Text = presetItem.DefaultIconKey;
					string sysSvg = IconHelper.GetSvgPathByKey(presetItem.DefaultIconKey);
					FocusIconPath.Data = !string.IsNullOrEmpty(sysSvg) ? Geometry.Parse(sysSvg) : null;
				}
			}
			RefreshSlots();
			RenderMappingsWheelPreview();
			ScheduleAutoSave();
		}
	}

	private void MappingsTierSegmentRadio_Checked(object sender, RoutedEventArgs e)
	{
		RenderMappingsWheelPreview();
	}

	private void RenderMappingsWheelPreview()
	{
		if (MappingsWheelPreviewCanvas == null || ConfigManager.CurrentConfig == null) return;
		try
		{
			MappingsWheelPreviewCanvas.Children.Clear();
			_mappingsSectorPaths.Clear();
			_mappingsSubSectorPaths.Clear();
			_mappingsSubSectorKeys.Clear();

			WheelProfile profile = _selectedProfile ?? ConfigManager.CurrentConfig.Profiles.FirstOrDefault() ?? new WheelProfile
			{
				SectorCount = 8,
				Actions = new List<ActionItem>()
			};

			double centerX = 150.0;
			double centerY = 150.0;
			int sectorCount = profile.SectorCount > 0 ? profile.SectorCount : 8;
			double sweepAngle = 360.0 / sectorCount;

			double baseScaleRef = Math.Max(215.0, ConfigManager.CurrentConfig.WheelRadius * 1.55);
			double scaleFactor = 135.0 / baseScaleRef;
			double outerR = Math.Max(30.0, ConfigManager.CurrentConfig.WheelRadius * scaleFactor);
			double innerR = Math.Max(15.0, ConfigManager.CurrentConfig.InnerRadius * scaleFactor);
			double coreR = Math.Max(10.0, ConfigManager.CurrentConfig.CoreRadius * scaleFactor);
			double gap = Math.Max(0.0, ConfigManager.CurrentConfig.SectorGap * scaleFactor);
			double cornerRadius = Math.Max(0.0, ConfigManager.CurrentConfig.SectorCornerRadius * scaleFactor);

			if (innerR >= outerR) innerR = outerR * 0.5;
			if (coreR >= innerR) coreR = innerR * 0.8;

			string uiStyle = ConfigManager.CurrentConfig.UiStyle ?? "ClassicRing";
			string theme = ConfigManager.CurrentConfig.Theme ?? "System";
			string shape = ConfigManager.CurrentConfig.Shape ?? "Original";

			IRadialStyleRenderer renderer = StyleRendererFactory.CreateRenderer(uiStyle);
			renderer.Initialize(theme, ConfigManager.CurrentConfig);

			Brush defaultBrush = renderer.DefaultSectorBrush;
			Brush borderBrush = renderer.SectorBorderBrush;
			Brush textBrush = renderer.TextColorBrush;
			Brush coreBgBrush = renderer.CoreBgBrush;
			Brush coreBorderBrush = renderer.CoreBorderBrush;

			// 1. Draw Center Core Circle
			Grid coreGrid = new Grid
			{
				Width = coreR * 2.0,
				Height = coreR * 2.0,
				RenderTransformOrigin = new Point(0.5, 0.5),
				Cursor = System.Windows.Input.Cursors.Hand,
				IsHitTestVisible = false
			};
			bool isCenterSelected = (_selectedSlotIndex == -1);

			Ellipse coreCircle = new Ellipse
			{
				Width = coreR * 2.0,
				Height = coreR * 2.0,
				Fill = isCenterSelected ? new SolidColorBrush(System.Windows.Media.Color.FromArgb(40, 245, 158, 11)) : coreBgBrush,
				Stroke = isCenterSelected ? new SolidColorBrush(System.Windows.Media.Color.FromRgb(245, 158, 11)) : coreBorderBrush,
				StrokeThickness = isCenterSelected ? 2.6 : 1.5
			};
			if (isCenterSelected)
			{
				coreCircle.Effect = new DropShadowEffect
				{
					Color = System.Windows.Media.Color.FromRgb(245, 158, 11),
					BlurRadius = 14.0,
					ShadowDepth = 0.0,
					Opacity = 0.95
				};
			}
			coreGrid.Children.Add(coreCircle);

			string? customCoreImage = ConfigManager.CurrentConfig.CoreCustomImagePath;
			if (!string.IsNullOrEmpty(customCoreImage) && File.Exists(customCoreImage))
			{
				try
				{
					BitmapImage bitmapImage = new BitmapImage();
					bitmapImage.BeginInit();
					bitmapImage.UriSource = new Uri(customCoreImage, UriKind.Absolute);
					bitmapImage.CacheOption = BitmapCacheOption.OnLoad;
					bitmapImage.EndInit();
					((Freezable)bitmapImage).Freeze();
					ImageBrush imageBrush = new ImageBrush(bitmapImage)
					{
						Stretch = Stretch.UniformToFill,
						AlignmentX = AlignmentX.Center,
						AlignmentY = AlignmentY.Center
					};
					Ellipse imgEllipse = new Ellipse
					{
						Width = coreR * 1.8,
						Height = coreR * 1.8,
						Fill = imageBrush,
						HorizontalAlignment = HorizontalAlignment.Center,
						VerticalAlignment = VerticalAlignment.Center,
						IsHitTestVisible = false
					};
					coreGrid.Children.Add(imgEllipse);
				}
				catch { }
			}
			else
			{
				ActionItem? centerItem = profile.CenterAction;
				string centerIconKey = centerItem?.IconKey ?? (!string.IsNullOrEmpty(ConfigManager.CurrentConfig.CoreCustomIconKey) ? ConfigManager.CurrentConfig.CoreCustomIconKey : "Settings");
				string centerSvg = IconHelper.GetSvgPathByKey(centerIconKey);
				if (!string.IsNullOrEmpty(centerSvg))
				{
					try
					{
						System.Windows.Shapes.Path coreIconPath = new System.Windows.Shapes.Path
						{
							Data = Geometry.Parse(centerSvg),
							Fill = isCenterSelected ? new SolidColorBrush(System.Windows.Media.Color.FromRgb(245, 158, 11)) : textBrush,
							Width = Math.Max(12.0, coreR * 0.75),
							Height = Math.Max(12.0, coreR * 0.75),
							Stretch = Stretch.Uniform,
							HorizontalAlignment = HorizontalAlignment.Center,
							VerticalAlignment = VerticalAlignment.Center,
							IsHitTestVisible = false
						};
						coreGrid.Children.Add(coreIconPath);
					}
					catch { }
				}
			}

			Canvas.SetLeft(coreGrid, centerX - coreR);
			Canvas.SetTop(coreGrid, centerY - coreR);
			Panel.SetZIndex(coreGrid, 10);
			MappingsWheelPreviewCanvas.Children.Add(coreGrid);

			// 2. Draw Sectors (Icon-Only, Clean Aesthetic Style matching Appearance tab)
			string[] directions = sectorCount switch
			{
				4 => Directions4,
				12 => Directions12,
				_ => Directions8
			};

			bool isTier2Mode = (MappingsTier2SegmentRadio != null && MappingsTier2SegmentRadio.IsChecked == true);

			for (int i = 0; i < sectorCount; i++)
			{
				int slotIdx = i;
				double midAngle = (double)i * sweepAngle;
				double startAngle = midAngle - sweepAngle / 2.0;
				double endAngle = midAngle + sweepAngle / 2.0;
				double rad = midAngle * (Math.PI / 180.0);
				double midR = (innerR + outerR) / 2.0;
				double contentX = centerX + Math.Cos(rad) * midR;
				double contentY = centerY + Math.Sin(rad) * midR;

				Geometry sectorGeo = IconHelper.CreateAdvancedSectorGeometry(centerX, centerY, startAngle, endAngle, innerR, outerR, shape, gap, cornerRadius);
				bool isSectorSelected = (_selectedSlotIndex == slotIdx && _selectedSubActionIndex == null);

				System.Windows.Shapes.Path sectorPath = new System.Windows.Shapes.Path
				{
					Data = sectorGeo,
					Fill = isSectorSelected ? new SolidColorBrush(System.Windows.Media.Color.FromArgb(50, 56, 189, 248)) : defaultBrush,
					Stroke = isSectorSelected ? new SolidColorBrush(System.Windows.Media.Color.FromRgb(56, 189, 248)) : borderBrush,
					StrokeThickness = isSectorSelected ? 2.4 : renderer.BorderThickness,
					Tag = slotIdx,
					Cursor = System.Windows.Input.Cursors.Hand,
					IsHitTestVisible = false
				};

				if (isSectorSelected)
				{
					sectorPath.Effect = new DropShadowEffect
					{
						Color = System.Windows.Media.Color.FromRgb(56, 189, 248),
						BlurRadius = 14.0,
						ShadowDepth = 0.0,
						Opacity = 0.95
					};
				}

				Panel.SetZIndex(sectorPath, 1);
				MappingsWheelPreviewCanvas.Children.Add(sectorPath);
				_mappingsSectorPaths.Add(sectorPath);

				ActionItem? action = (profile.Actions != null && slotIdx < profile.Actions.Count) ? profile.Actions[slotIdx] : null;

				string iconKey = action?.IconKey ?? "";
				string iconSvg = "";
				IconHelper.CustomIconItem? customIconItem = null;

				if (!string.IsNullOrEmpty(action?.CustomIconSvg))
				{
					iconSvg = action.CustomIconSvg;
				}
				else if (!string.IsNullOrEmpty(action?.IconKey) && action.IconKey.StartsWith("custom:", StringComparison.OrdinalIgnoreCase))
				{
					customIconItem = IconHelper.GetCustomIcons().FirstOrDefault((IconHelper.CustomIconItem c) => string.Equals(c.Key, action.IconKey, StringComparison.OrdinalIgnoreCase));
					if (customIconItem != null && customIconItem.IsSvg)
					{
						iconSvg = customIconItem.SvgData;
					}
				}
				if (string.IsNullOrEmpty(iconSvg) && customIconItem == null)
				{
					if (!string.IsNullOrEmpty(action?.IconKey))
					{
						iconSvg = IconHelper.GetSvgPathByKey(action.IconKey);
					}
					else if (action != null)
					{
						iconSvg = action.Type switch
						{
							"WebUrl" or "Url" => IconHelper.GetSvgPathByKey("Browser") ?? IconHelper.GetSvgPathByKey("ShowDesktop"),
							"Folder" or "OpenFolder" => IconHelper.GetSvgPathByKey("Folder"),
							"System" when !string.IsNullOrEmpty(action.Parameter) => IconHelper.GetSvgPathByKey(action.Parameter),
							_ => ""
						};
					}
				}

				Brush iconBrush = isSectorSelected ? new SolidColorBrush(System.Windows.Media.Color.FromRgb(56, 189, 248)) : textBrush;
				if (action != null && !string.IsNullOrWhiteSpace(action.CustomTextColor) && !isSectorSelected)
				{
					iconBrush = CreateBrushFromHexSafe(action.CustomTextColor, textBrush);
				}

				double baseIconSize = (action != null && action.CustomIconSize.HasValue && action.CustomIconSize.Value > 0.0)
					? action.CustomIconSize.Value
					: ((ConfigManager.CurrentConfig.SectorIconSize > 0.0) ? ConfigManager.CurrentConfig.SectorIconSize : 20.0);
				double sectorRatio = sectorCount switch { 4 => 1.25, 12 => 0.85, _ => 1.0 };
				double iconSize = Math.Max(15.0, Math.Min(28.0, baseIconSize * 1.15 * sectorRatio * scaleFactor));

				FrameworkElement? iconElement = null;

				if (!string.IsNullOrEmpty(iconSvg))
				{
					try
					{
						iconElement = new System.Windows.Shapes.Path
						{
							Data = Geometry.Parse(iconSvg),
							Fill = iconBrush,
							Width = iconSize,
							Height = iconSize,
							Stretch = Stretch.Uniform,
							HorizontalAlignment = HorizontalAlignment.Center,
							VerticalAlignment = VerticalAlignment.Center,
							IsHitTestVisible = false
						};
					}
					catch { }
				}
				else if (customIconItem != null && !customIconItem.IsSvg)
				{
					try
					{
						ImageSource imgSource = IconHelper.GetCustomImageSource(customIconItem.FilePath);
						if (imgSource != null)
						{
							iconElement = new System.Windows.Controls.Image
							{
								Source = imgSource,
								Width = iconSize,
								Height = iconSize,
								Stretch = Stretch.Uniform,
								HorizontalAlignment = HorizontalAlignment.Center,
								VerticalAlignment = VerticalAlignment.Center,
								IsHitTestVisible = false
							};
						}
					}
					catch { }
				}
				else if (action != null && action.Type == "Launch" && !string.IsNullOrEmpty(action.Parameter) && File.Exists(action.Parameter))
				{
					try
					{
						ImageSource? appIcon = IconHelper.GetIcon(action.Parameter);
						if (appIcon != null)
						{
							iconElement = new System.Windows.Controls.Image
							{
								Source = appIcon,
								Width = iconSize,
								Height = iconSize,
								Stretch = Stretch.Uniform,
								HorizontalAlignment = HorizontalAlignment.Center,
								VerticalAlignment = VerticalAlignment.Center,
								IsHitTestVisible = false
							};
						}
					}
					catch { }
				}

				if (iconElement == null)
				{
					try
					{
						string fallbackKey = (slotIdx < directions.Length) ? directions[slotIdx] : "Settings";
						string fallbackSvg = IconHelper.GetSvgPathByKey(fallbackKey);
						if (string.IsNullOrEmpty(fallbackSvg)) fallbackSvg = IconHelper.GetSvgPathByKey("Settings");
						iconElement = new System.Windows.Shapes.Path
						{
							Data = Geometry.Parse(fallbackSvg),
							Fill = iconBrush,
							Width = iconSize,
							Height = iconSize,
							Stretch = Stretch.Uniform,
							HorizontalAlignment = HorizontalAlignment.Center,
							VerticalAlignment = VerticalAlignment.Center,
							IsHitTestVisible = false
						};
					}
					catch { }
				}

				if (iconElement != null)
				{
					Canvas.SetLeft(iconElement, contentX - iconSize / 2.0);
					Canvas.SetTop(iconElement, contentY - iconSize / 2.0);
					Panel.SetZIndex(iconElement, 5);
					MappingsWheelPreviewCanvas.Children.Add(iconElement);
				}

				// 3. Draw SubActions (Icons only, NO TEXT!)
				if (action?.SubActions != null && action.SubActions.Count > 0 && (isTier2Mode || isSectorSelected))
				{
					double subInnerR = outerR + 4.0;
					double subOuterR = subInnerR + 22.0;
					int subCount = action.SubActions.Count;
					double subSweep = sweepAngle / subCount;

					for (int j = 0; j < subCount; j++)
					{
						int subIdx = j;
						double subMidAngle = startAngle + (j + 0.5) * subSweep;
						double subStart = startAngle + j * subSweep;
						double subEnd = startAngle + (j + 1) * subSweep;
						double subRad = subMidAngle * (Math.PI / 180.0);
						double subMidR = (subInnerR + subOuterR) / 2.0;
						double subContentX = centerX + Math.Cos(subRad) * subMidR;
						double subContentY = centerY + Math.Sin(subRad) * subMidR;

						Geometry subGeo = IconHelper.CreateAdvancedSectorGeometry(centerX, centerY, subStart, subEnd, subInnerR, subOuterR, "Original", 1.5, 3.0);
						bool isSubSelected = (_selectedSlotIndex == slotIdx && _selectedSubActionIndex == subIdx);

						System.Windows.Shapes.Path subPath = new System.Windows.Shapes.Path
						{
							Data = subGeo,
							Fill = isSubSelected 
								? new SolidColorBrush(System.Windows.Media.Color.FromArgb(60, 168, 85, 247)) 
								: new SolidColorBrush(System.Windows.Media.Color.FromArgb(40, 168, 85, 247)),
							Stroke = isSubSelected 
								? new SolidColorBrush(System.Windows.Media.Color.FromRgb(168, 85, 247)) 
								: new SolidColorBrush(System.Windows.Media.Color.FromArgb(120, 168, 85, 247)),
							StrokeThickness = isSubSelected ? 2.2 : 1.0,
							Cursor = System.Windows.Input.Cursors.Hand,
							IsHitTestVisible = false
						};

						if (isSubSelected)
						{
							subPath.Effect = new DropShadowEffect
							{
								Color = System.Windows.Media.Color.FromRgb(168, 85, 247),
								BlurRadius = 12.0,
								ShadowDepth = 0.0,
								Opacity = 0.95
							};
						}

						Panel.SetZIndex(subPath, 2);
						MappingsWheelPreviewCanvas.Children.Add(subPath);
						_mappingsSubSectorPaths.Add(subPath);
						_mappingsSubSectorKeys.Add(Tuple.Create(slotIdx, subIdx));

						ActionItem subItem = action.SubActions[j];
						string subIconKey = subItem.IconKey ?? "";
						string subSvg = !string.IsNullOrEmpty(subItem.CustomIconSvg) ? subItem.CustomIconSvg : IconHelper.GetSvgPathByKey(subIconKey);
						if (string.IsNullOrEmpty(subSvg))
						{
							subSvg = subItem.Type switch
							{
								"WebUrl" or "Url" => IconHelper.GetSvgPathByKey("Browser") ?? IconHelper.GetSvgPathByKey("ShowDesktop"),
								"Folder" or "OpenFolder" => IconHelper.GetSvgPathByKey("Folder"),
								"System" when !string.IsNullOrEmpty(subItem.Parameter) => IconHelper.GetSvgPathByKey(subItem.Parameter),
								_ => ""
							};
						}

						Brush subIconBrush = isSubSelected ? new SolidColorBrush(System.Windows.Media.Color.FromRgb(168, 85, 247)) : textBrush;
						double subIconSize = 11.0;
						if (!string.IsNullOrEmpty(subSvg))
						{
							try
							{
								System.Windows.Shapes.Path subIconPath = new System.Windows.Shapes.Path
								{
									Data = Geometry.Parse(subSvg),
									Fill = subIconBrush,
									Width = subIconSize,
									Height = subIconSize,
									Stretch = Stretch.Uniform,
									HorizontalAlignment = HorizontalAlignment.Center,
									VerticalAlignment = VerticalAlignment.Center,
									IsHitTestVisible = false
								};
								Canvas.SetLeft(subIconPath, subContentX - subIconSize / 2.0);
								Canvas.SetTop(subIconPath, subContentY - subIconSize / 2.0);
								Panel.SetZIndex(subIconPath, 6);
								MappingsWheelPreviewCanvas.Children.Add(subIconPath);
							}
							catch { }
						}
					}
				}
			}

			if (MappingsCurrentEditIndicator != null)
			{
				if (_selectedSlotIndex == -1)
				{
					MappingsCurrentEditIndicator.Text = "🎯 正在编辑: 中心核心圆动作";
					MappingsCurrentEditIndicator.Foreground = new SolidColorBrush(System.Windows.Media.Color.FromRgb(245, 158, 11));
				}
				else if (_selectedSubActionIndex.HasValue)
				{
					ActionItem? parent = (profile.Actions != null && _selectedSlotIndex < profile.Actions.Count) ? profile.Actions[_selectedSlotIndex] : null;
					string subName = (parent?.SubActions != null && _selectedSubActionIndex.Value < parent.SubActions.Count) ? parent.SubActions[_selectedSubActionIndex.Value].Name : "";
					MappingsCurrentEditIndicator.Text = $"🌟 正在编辑: 二级动作 [{subName}]";
					MappingsCurrentEditIndicator.Foreground = new SolidColorBrush(System.Windows.Media.Color.FromRgb(168, 85, 247));
				}
				else
				{
					string dirName = (_selectedSlotIndex >= 0 && _selectedSlotIndex < directions.Length) ? directions[_selectedSlotIndex] : $"{_selectedSlotIndex + 1}";
					MappingsCurrentEditIndicator.Text = $"🎯 正在编辑: 扇区 {_selectedSlotIndex + 1} [{dirName}]";
					MappingsCurrentEditIndicator.Foreground = new SolidColorBrush(System.Windows.Media.Color.FromRgb(56, 189, 248));
				}
			}
		}
		catch (Exception ex)
		{
			AppLogger.LogError("RenderMappingsWheelPreview failed", ex);
		}
	}

	private void MappingsPreviewViewport_MouseDown(object sender, MouseButtonEventArgs e)
	{
		if (e.ChangedButton == MouseButton.Left)
		{
			Point pos = e.GetPosition(MappingsWheelPreviewCanvas);
			_mappingsDragStartPos = pos;
			_dragSourceSlotIndex = -999;
			_isDraggingSlot = false;

			double dx = pos.X - 150.0;
			double dy = pos.Y - 150.0;
			double dist = Math.Sqrt(dx * dx + dy * dy);

			double baseScaleRef = Math.Max(215.0, (ConfigManager.CurrentConfig?.WheelRadius ?? 100.0) * 1.55);
			double scaleFactor = 135.0 / baseScaleRef;
			double coreR = Math.Max(10.0, (ConfigManager.CurrentConfig?.CoreRadius ?? 25.0) * scaleFactor);
			double outerR = Math.Max(30.0, (ConfigManager.CurrentConfig?.WheelRadius ?? 100.0) * scaleFactor);

			if (dist <= coreR)
			{
				_dragSourceSlotIndex = -1;
			}
			else if (dist <= outerR + 4.0)
			{
				WheelProfile profile = _selectedProfile ?? ConfigManager.CurrentConfig?.Profiles.FirstOrDefault() ?? new WheelProfile();
				int sectorCount = profile.SectorCount > 0 ? profile.SectorCount : 8;
				double angleDeg = Math.Atan2(dy, dx) * (180.0 / Math.PI);
				if (angleDeg < 0) angleDeg += 360.0;
				double sweep = 360.0 / sectorCount;
				int slot = (int)Math.Floor((angleDeg + sweep / 2.0) / sweep) % sectorCount;
				_dragSourceSlotIndex = slot;
			}
			else
			{
				bool isTier2Mode = (MappingsTier2SegmentRadio != null && MappingsTier2SegmentRadio.IsChecked == true);
				WheelProfile profile = _selectedProfile ?? ConfigManager.CurrentConfig?.Profiles.FirstOrDefault() ?? new WheelProfile();
				if (isTier2Mode && dist <= outerR + 32.0)
				{
					int sectorCount = profile.SectorCount > 0 ? profile.SectorCount : 8;
					double angleDeg = Math.Atan2(dy, dx) * (180.0 / Math.PI);
					if (angleDeg < 0) angleDeg += 360.0;
					double sweep = 360.0 / sectorCount;
					int slot = (int)Math.Floor((angleDeg + sweep / 2.0) / sweep) % sectorCount;
					if (slot >= 0 && slot < profile.Actions.Count && profile.Actions[slot].SubActions != null && profile.Actions[slot].SubActions.Count > 0)
					{
						int subCount = profile.Actions[slot].SubActions.Count;
						double slotStartAngle = (double)slot * sweep - sweep / 2.0;
						double relAngle = angleDeg - slotStartAngle;
						while (relAngle < 0) relAngle += 360.0;
						while (relAngle >= 360.0) relAngle -= 360.0;
						double subSweep = sweep / subCount;
						int subIdx = (int)Math.Floor(relAngle / subSweep);
						if (subIdx >= 0 && subIdx < subCount)
						{
							_dragSourceSlotIndex = -100 - (slot * 100 + subIdx);
						}
					}
				}
			}

			MappingsPreviewViewportContainer.CaptureMouse();
		}
		else if (e.ChangedButton == MouseButton.Right || e.ChangedButton == MouseButton.Middle)
		{
			_mappingsPanStartPoint = e.GetPosition(MappingsPreviewViewportContainer);
			_mappingsPanStartTranslate = new Point(MappingsPreviewTranslateTransform.X, MappingsPreviewTranslateTransform.Y);
			MappingsPreviewViewportContainer.CaptureMouse();
		}
	}

	private void MappingsPreviewViewport_MouseMove(object sender, System.Windows.Input.MouseEventArgs e)
	{
		if (_mappingsPanStartPoint.HasValue && MappingsPreviewViewportContainer.IsMouseCaptured && (e.RightButton == MouseButtonState.Pressed || e.MiddleButton == MouseButtonState.Pressed))
		{
			Point current = e.GetPosition(MappingsPreviewViewportContainer);
			Vector delta = current - _mappingsPanStartPoint.Value;
			MappingsPreviewTranslateTransform.X = _mappingsPanStartTranslate.X + delta.X;
			MappingsPreviewTranslateTransform.Y = _mappingsPanStartTranslate.Y + delta.Y;
			return;
		}

		if (_mappingsDragStartPos.HasValue && MappingsPreviewViewportContainer.IsMouseCaptured && e.LeftButton == MouseButtonState.Pressed && _dragSourceSlotIndex != -999)
		{
			Point currentPos = e.GetPosition(MappingsWheelPreviewCanvas);
			double dragDist = (currentPos - _mappingsDragStartPos.Value).Length;
			if (dragDist > 8.0 && !_isDraggingSlot)
			{
				_isDraggingSlot = true;
			}

			if (_isDraggingSlot)
			{
				MappingsPreviewViewportContainer.Cursor = System.Windows.Input.Cursors.Hand;
				WheelProfile? profile = _selectedProfile ?? ConfigManager.CurrentConfig?.Profiles.FirstOrDefault();
				string srcName = "";
				if (_dragSourceSlotIndex == -1)
				{
					srcName = profile?.CenterAction?.Name ?? "中心核圆";
				}
				else if (_dragSourceSlotIndex >= 0 && profile != null && _dragSourceSlotIndex < profile.Actions.Count)
				{
					srcName = profile.Actions[_dragSourceSlotIndex].Name ?? $"扇区 {_dragSourceSlotIndex + 1}";
				}

				if (MappingsCurrentEditIndicator != null && !string.IsNullOrEmpty(srcName))
				{
					MappingsCurrentEditIndicator.Text = $"🔄 正在拖拽 [{srcName}]，释放到目标扇区以对调功能...";
					MappingsCurrentEditIndicator.Foreground = new SolidColorBrush(System.Windows.Media.Color.FromRgb(245, 158, 11));
				}
			}
		}
	}

	private void MappingsPreviewViewport_MouseUp(object sender, MouseButtonEventArgs e)
	{
		if (MappingsPreviewViewportContainer.IsMouseCaptured)
		{
			MappingsPreviewViewportContainer.ReleaseMouseCapture();
			_mappingsPanStartPoint = null;
		}

		MappingsPreviewViewportContainer.Cursor = System.Windows.Input.Cursors.Arrow;

		if (e.ChangedButton == MouseButton.Left && _mappingsDragStartPos.HasValue && _dragSourceSlotIndex != -999)
		{
			Point upPos = e.GetPosition(MappingsWheelPreviewCanvas);
			double dragDist = (upPos - _mappingsDragStartPos.Value).Length;

			if (_isDraggingSlot && dragDist > 10.0 && _dragSourceSlotIndex >= -1)
			{
				double dx = upPos.X - 150.0;
				double dy = upPos.Y - 150.0;
				double dist = Math.Sqrt(dx * dx + dy * dy);

				double baseScaleRef = Math.Max(215.0, (ConfigManager.CurrentConfig?.WheelRadius ?? 100.0) * 1.55);
				double scaleFactor = 135.0 / baseScaleRef;
				double coreR = Math.Max(10.0, (ConfigManager.CurrentConfig?.CoreRadius ?? 25.0) * scaleFactor);
				double outerR = Math.Max(30.0, (ConfigManager.CurrentConfig?.WheelRadius ?? 100.0) * scaleFactor);

				int targetSlot = -999;
				if (dist <= coreR)
				{
					targetSlot = -1;
				}
				else if (dist <= outerR + 15.0)
				{
					WheelProfile profile = _selectedProfile ?? ConfigManager.CurrentConfig?.Profiles.FirstOrDefault() ?? new WheelProfile();
					int sectorCount = profile.SectorCount > 0 ? profile.SectorCount : 8;
					double angleDeg = Math.Atan2(dy, dx) * (180.0 / Math.PI);
					if (angleDeg < 0) angleDeg += 360.0;
					double sweep = 360.0 / sectorCount;
					targetSlot = (int)Math.Floor((angleDeg + sweep / 2.0) / sweep) % sectorCount;
				}

				if (targetSlot != -999 && targetSlot != _dragSourceSlotIndex)
				{
					WheelProfile? profile = _selectedProfile ?? ConfigManager.CurrentConfig?.Profiles.FirstOrDefault();
					if (profile != null && profile.Actions != null)
					{
						string srcName = "";
						string tgtName = "";

						if (_dragSourceSlotIndex == -1)
						{
							srcName = profile.CenterAction?.Name ?? "中心核圆";
							tgtName = (targetSlot >= 0 && targetSlot < profile.Actions.Count) ? profile.Actions[targetSlot].Name : $"槽 {targetSlot + 1}";
							ActionItem temp = profile.CenterAction ?? new ActionItem { Name = "StarPie控制台", Type = "System", Parameter = "OpenSettings", IconKey = "Settings" };
							profile.CenterAction = profile.Actions[targetSlot];
							profile.Actions[targetSlot] = temp;
							SelectPrimarySlot(targetSlot);
						}
						else if (targetSlot == -1)
						{
							srcName = (_dragSourceSlotIndex >= 0 && _dragSourceSlotIndex < profile.Actions.Count) ? profile.Actions[_dragSourceSlotIndex].Name : $"槽 {_dragSourceSlotIndex + 1}";
							tgtName = profile.CenterAction?.Name ?? "中心核圆";
							ActionItem temp = profile.CenterAction ?? new ActionItem { Name = "StarPie控制台", Type = "System", Parameter = "OpenSettings", IconKey = "Settings" };
							profile.CenterAction = profile.Actions[_dragSourceSlotIndex];
							profile.Actions[_dragSourceSlotIndex] = temp;
							SelectCenterCore();
						}
						else
						{
							srcName = (_dragSourceSlotIndex >= 0 && _dragSourceSlotIndex < profile.Actions.Count) ? profile.Actions[_dragSourceSlotIndex].Name : $"槽 {_dragSourceSlotIndex + 1}";
							tgtName = (targetSlot >= 0 && targetSlot < profile.Actions.Count) ? profile.Actions[targetSlot].Name : $"槽 {targetSlot + 1}";
							ActionItem temp = profile.Actions[_dragSourceSlotIndex];
							profile.Actions[_dragSourceSlotIndex] = profile.Actions[targetSlot];
							profile.Actions[targetSlot] = temp;
							SelectPrimarySlot(targetSlot);
						}

						RefreshSlots();
						RenderMappingsWheelPreview();
						ScheduleAutoSave();
						if (MappingsCurrentEditIndicator != null)
						{
							MappingsCurrentEditIndicator.Text = $"🎯 已将 [{srcName}] 与 [{tgtName}] 成功对调位置！";
							MappingsCurrentEditIndicator.Foreground = new SolidColorBrush(System.Windows.Media.Color.FromRgb(16, 185, 129));
						}
					}
				}
				else
				{
					RenderMappingsWheelPreview();
				}
			}
			else
			{
				if (_dragSourceSlotIndex == -1)
				{
					SelectCenterCore();
				}
				else if (_dragSourceSlotIndex >= 0)
				{
					SelectPrimarySlot(_dragSourceSlotIndex);
				}
				else if (_dragSourceSlotIndex <= -100)
				{
					int raw = -_dragSourceSlotIndex - 100;
					int slot = raw / 100;
					int subIdx = raw % 100;
					SelectSubAction(slot, subIdx);
				}
			}

			_mappingsDragStartPos = null;
			_dragSourceSlotIndex = -999;
			_isDraggingSlot = false;
			e.Handled = true;
		}
	}

	private void MappingsPreviewViewport_MouseWheel(object sender, MouseWheelEventArgs e)
	{
		double delta = e.Delta > 0 ? 0.1 : -0.1;
		double newScale = Math.Max(0.5, Math.Min(3.0, MappingsPreviewScaleTransform.ScaleX + delta));
		MappingsPreviewScaleTransform.ScaleX = newScale;
		MappingsPreviewScaleTransform.ScaleY = newScale;
		MappingsZoomLabel.Text = $"{newScale * 100:0}%";
		e.Handled = true;
	}

	private void MappingsZoomInBtn_Click(object sender, RoutedEventArgs e)
	{
		double newScale = Math.Min(3.0, MappingsPreviewScaleTransform.ScaleX + 0.15);
		MappingsPreviewScaleTransform.ScaleX = newScale;
		MappingsPreviewScaleTransform.ScaleY = newScale;
		MappingsZoomLabel.Text = $"{newScale * 100:0}%";
	}

	private void MappingsZoomOutBtn_Click(object sender, RoutedEventArgs e)
	{
		double newScale = Math.Max(0.5, MappingsPreviewScaleTransform.ScaleX - 0.15);
		MappingsPreviewScaleTransform.ScaleX = newScale;
		MappingsPreviewScaleTransform.ScaleY = newScale;
		MappingsZoomLabel.Text = $"{newScale * 100:0}%";
	}

	private void MappingsResetViewBtn_Click(object sender, RoutedEventArgs e)
	{
		MappingsPreviewScaleTransform.ScaleX = 1.0;
		MappingsPreviewScaleTransform.ScaleY = 1.0;
		MappingsPreviewTranslateTransform.X = 0;
		MappingsPreviewTranslateTransform.Y = 0;
		MappingsZoomLabel.Text = "100%";
	}

	private void MappingsZoomLabel_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
	{
		MappingsResetViewBtn_Click(sender, e);
	}

	#endregion

	private void HookRawInputForSensorAndRecorder()
	{
		if (App.MainMouseHook != null)
		{
			App.MainMouseHook.OnRawMouseButtonEvent += delegate(object? s, RawMouseEventArgs e)
			{
				if (e.IsButtonDown)
				{
					((DispatcherObject)this).Dispatcher.BeginInvoke((Delegate)(Action)delegate
					{
						ProcessRawMouseButton(e.MouseButton, e.MouseData);
					}, Array.Empty<object>());
				}
			};
		}
		if (App.MainKeyboardHook == null)
		{
			return;
		}
		App.MainKeyboardHook.OnRawKeyEvent += delegate(object? s, GlobalKeyEventArgs e)
		{
			((DispatcherObject)this).Dispatcher.BeginInvoke((Delegate)(Action)delegate
			{
				ProcessRawKeyEvent(e);
			}, Array.Empty<object>());
		};
	}

	private void UpdateTriggerBadgeDisplay()
	{
		if (CurrentTriggerBadgeText != null && ConfigManager.CurrentConfig != null)
		{
			TriggerConfig triggerConfig = ConfigManager.CurrentConfig.Trigger;
			if (triggerConfig == null)
			{
				triggerConfig = new TriggerConfig();
				ConfigManager.CurrentConfig.Trigger = triggerConfig;
			}
			string text = FormatTriggerDisplay(triggerConfig);
			CurrentTriggerBadgeText.Text = text;
		}
	}

	private string FormatTriggerDisplay(TriggerConfig trigger)
	{
		string text = "";
		if (trigger.RequireCtrl)
		{
			text += "Ctrl + ";
		}
		if (trigger.RequireShift)
		{
			text += "Shift + ";
		}
		if (trigger.RequireAlt)
		{
			text += "Alt + ";
		}
		if (trigger.RequireWin)
		{
			text += "Win + ";
		}
		if (trigger.TriggerType == "Keyboard")
		{
			string text2 = trigger.Key;
			if (trigger.VkCode == 20 || text2 == "Capital")
			{
				text2 = "CapsLock (大写锁定)";
			}
			else if (trigger.VkCode == 192 || text2 == "Oem3" || text2 == "OemTilde")
			{
				text2 = "~ (波浪键)";
			}
			else if (trigger.VkCode == 32 || text2 == "Space")
			{
				text2 = "Space (空格)";
			}
			else if (trigger.VkCode == 9 || text2 == "Tab")
			{
				text2 = "Tab (制表键)";
			}
			else if (text2 == "None" || string.IsNullOrEmpty(text2))
			{
				if (!string.IsNullOrEmpty(text))
				{
					return "⌨\ufe0f " + text.TrimEnd(' ', '+') + " (长按拖动)";
				}
				return "\ud83d\uddb1\ufe0f 鼠标右键 (Right Button)";
			}
			return text + "⌨\ufe0f " + text2 + " (长按拖动)";
		}
		string text3 = trigger.MouseButton switch
		{
			"MiddleButton" => "\ud83d\uddb1\ufe0f 鼠标中键 / 滚轮按压 (Middle Button)", 
			"XButton1" => "\ud83d\uddb1\ufe0f 鼠标侧键 1 / 后退键 (XButton 1 / Back)", 
			"XButton2" => "\ud83d\uddb1\ufe0f 鼠标侧键 2 / 前进键 (XButton 2 / Forward)", 
			"LeftButton" => "\ud83d\uddb1\ufe0f 鼠标左键 (Left Button)", 
			_ => "\ud83d\uddb1\ufe0f 鼠标右键 (Right Button) [推荐 / 默认]", 
		};
		if (!string.IsNullOrEmpty(text))
		{
			return text + text3;
		}
		return text3;
	}

	private void RecordTriggerButton_Click(object sender, RoutedEventArgs e)
	{
		if (!_isRecordingTrigger)
		{
			StartTriggerRecording();
		}
		else
		{
			StopTriggerRecording(saved: false);
		}
	}

	private void StartTriggerRecording()
	{
		_isRecordingTrigger = true;
		if (RecordTriggerButton != null)
		{
			RecordTriggerButton.Content = "⚡ 正在监听... 请按下任意按键 / 组合键 (ESC取消)";
			RecordTriggerButton.Background = new SolidColorBrush((System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#EF4444"));
		}
		if (LiveSensorStatusText != null)
		{
			LiveSensorStatusText.Text = "\ud83d\udd34 录制模式中：请直接按下你想作为轮盘唤醒键的鼠标按键、键盘按键或组合键（按 ESC 键取消录制）...";
		}
		if (LiveSensorDot != null)
		{
			LiveSensorDot.Fill = new SolidColorBrush((System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#EF4444"));
		}
	}

	private void StopTriggerRecording(bool saved)
	{
		_isRecordingTrigger = false;
		if (RecordTriggerButton != null)
		{
			RecordTriggerButton.Content = "\ud83d\udd34 点击录制触发键 / 组合键";
			((DependencyObject)RecordTriggerButton).ClearValue(System.Windows.Controls.Control.BackgroundProperty);
		}
		if (LiveSensorStatusText != null)
		{
			LiveSensorStatusText.Text = (saved ? "\ud83d\udfe2 触发按键录制成功并已保存！" : "\ud83d\udca1 硬件感知器已就绪：随时按下鼠标任意侧键、中键或键盘按键，此处将实时高亮反馈对应按键与键码。");
		}
		if (LiveSensorDot != null)
		{
			LiveSensorDot.Fill = new SolidColorBrush((System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#10B981"));
		}
		UpdateTriggerBadgeDisplay();
	}

	private void ResetDefaultTriggerButton_Click(object sender, RoutedEventArgs e)
	{
		if (ConfigManager.CurrentConfig != null)
		{
			ConfigManager.CurrentConfig.Trigger = new TriggerConfig
			{
				TriggerType = "Mouse",
				MouseButton = "RightButton",
				DisplayText = "\ud83d\uddb1\ufe0f 鼠标右键 (Right Button)"
			};
			ConfigManager.CurrentConfig.TriggerButton = "RightButton";
			StopTriggerRecording(saved: true);
			ScheduleAutoSave();
			if (LiveSensorStatusText != null)
			{
				LiveSensorStatusText.Text = "\ud83d\udfe2 已恢复默认触发按键：\ud83d\uddb1\ufe0f 鼠标右键 (Right Button)";
			}
		}
	}

	public void ProcessRawMouseButton(string mouseButton, uint mouseData = 0u)
	{
		//IL_0077: Unknown result type (might be due to invalid IL or missing references)
		//IL_007c: Unknown result type (might be due to invalid IL or missing references)
		//IL_0083: Unknown result type (might be due to invalid IL or missing references)
		//IL_0085: Unknown result type (might be due to invalid IL or missing references)
		//IL_0094: Unknown result type (might be due to invalid IL or missing references)
		//IL_0096: Unknown result type (might be due to invalid IL or missing references)
		//IL_00a5: Unknown result type (might be due to invalid IL or missing references)
		//IL_00a7: Unknown result type (might be due to invalid IL or missing references)
		//IL_00b6: Unknown result type (might be due to invalid IL or missing references)
		//IL_00b8: Unknown result type (might be due to invalid IL or missing references)
		//IL_013a: Unknown result type (might be due to invalid IL or missing references)
		//IL_013c: Unknown result type (might be due to invalid IL or missing references)
		//IL_013e: Invalid comparison between Unknown and I4
		//IL_0146: Unknown result type (might be due to invalid IL or missing references)
		//IL_0148: Unknown result type (might be due to invalid IL or missing references)
		//IL_014a: Invalid comparison between Unknown and I4
		//IL_0152: Unknown result type (might be due to invalid IL or missing references)
		//IL_0154: Unknown result type (might be due to invalid IL or missing references)
		//IL_0156: Invalid comparison between Unknown and I4
		//IL_015e: Unknown result type (might be due to invalid IL or missing references)
		//IL_0160: Unknown result type (might be due to invalid IL or missing references)
		//IL_0162: Invalid comparison between Unknown and I4
		if ((base.IsVisible || _isRecordingTrigger) && ConfigManager.CurrentConfig != null)
		{
			string text = mouseButton switch
			{
				"MiddleButton" => "\ud83d\uddb1\ufe0f 鼠标中键 / 滚轮按压 (Middle Button)", 
				"XButton1" => "\ud83d\uddb1\ufe0f 鼠标侧键 1 / 后退键 (XButton 1 / Back)", 
				"XButton2" => "\ud83d\uddb1\ufe0f 鼠标侧键 2 / 前进键 (XButton 2 / Forward)", 
				"LeftButton" => "\ud83d\uddb1\ufe0f 鼠标左键 (Left Button)", 
				_ => "\ud83d\uddb1\ufe0f 鼠标右键 (Right Button)", 
			};
			ModifierKeys currentModifiers = KeyboardHook.GetCurrentModifiers();
			string text2 = "";
			if (((((int)currentModifiers & 2))) != 0)
			{
				text2 += "Ctrl + ";
			}
			if (((((int)currentModifiers & 4))) != 0)
			{
				text2 += "Shift + ";
			}
			if (((((int)currentModifiers & 1))) != 0)
			{
				text2 += "Alt + ";
			}
			if (((((int)currentModifiers & 8))) != 0)
			{
				text2 += "Win + ";
			}
			if (LiveSensorStatusText != null)
			{
				LiveSensorStatusText.Text = "\ud83d\udfe2 实时捕获输入: " + text2 + text + " | 状态: 硬件信号正常响应";
			}
			if (LiveSensorDot != null)
			{
				LiveSensorDot.Fill = new SolidColorBrush((System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#10B981"));
			}
			if (_isRecordingTrigger)
			{
				ConfigManager.CurrentConfig.Trigger = new TriggerConfig
				{
					TriggerType = "Mouse",
					MouseButton = mouseButton,
					RequireCtrl = (((((int)currentModifiers & 2))) > 0),
					RequireShift = (((((int)currentModifiers & 4))) > 0),
					RequireAlt = (((((int)currentModifiers & 1))) > 0),
					RequireWin = (((((int)currentModifiers & 8))) > 0)
				};
				ConfigManager.CurrentConfig.Trigger.DisplayText = FormatTriggerDisplay(ConfigManager.CurrentConfig.Trigger);
				ConfigManager.CurrentConfig.TriggerButton = mouseButton;
				ScheduleAutoSave();
				StopTriggerRecording(saved: true);
			}
		}
	}

	private void ProcessRawKeyEvent(GlobalKeyEventArgs e)
	{
		//IL_0001: Unknown result type (might be due to invalid IL or missing references)
		//IL_000a: Unknown result type (might be due to invalid IL or missing references)
		//IL_0011: Invalid comparison between Unknown and I4
		//IL_0024: Unknown result type (might be due to invalid IL or missing references)
		//IL_0029: Unknown result type (might be due to invalid IL or missing references)
		//IL_0082: Unknown result type (might be due to invalid IL or missing references)
		//IL_0087: Unknown result type (might be due to invalid IL or missing references)
		//IL_008e: Unknown result type (might be due to invalid IL or missing references)
		//IL_0090: Unknown result type (might be due to invalid IL or missing references)
		//IL_00b3: Unknown result type (might be due to invalid IL or missing references)
		//IL_00b5: Unknown result type (might be due to invalid IL or missing references)
		//IL_0094: Unknown result type (might be due to invalid IL or missing references)
		//IL_009b: Invalid comparison between Unknown and I4
		//IL_00d8: Unknown result type (might be due to invalid IL or missing references)
		//IL_00da: Unknown result type (might be due to invalid IL or missing references)
		//IL_00b9: Unknown result type (might be due to invalid IL or missing references)
		//IL_00c0: Invalid comparison between Unknown and I4
		//IL_009e: Unknown result type (might be due to invalid IL or missing references)
		//IL_00a5: Invalid comparison between Unknown and I4
		//IL_00fd: Unknown result type (might be due to invalid IL or missing references)
		//IL_00ff: Unknown result type (might be due to invalid IL or missing references)
		//IL_00de: Unknown result type (might be due to invalid IL or missing references)
		//IL_00e5: Invalid comparison between Unknown and I4
		//IL_00c3: Unknown result type (might be due to invalid IL or missing references)
		//IL_00ca: Invalid comparison between Unknown and I4
		//IL_0103: Unknown result type (might be due to invalid IL or missing references)
		//IL_010a: Invalid comparison between Unknown and I4
		//IL_00e8: Unknown result type (might be due to invalid IL or missing references)
		//IL_00ef: Invalid comparison between Unknown and I4
		//IL_010d: Unknown result type (might be due to invalid IL or missing references)
		//IL_0114: Invalid comparison between Unknown and I4
		//IL_01bf: Unknown result type (might be due to invalid IL or missing references)
		//IL_01c6: Invalid comparison between Unknown and I4
		//IL_01c9: Unknown result type (might be due to invalid IL or missing references)
		//IL_01d0: Invalid comparison between Unknown and I4
		//IL_01d3: Unknown result type (might be due to invalid IL or missing references)
		//IL_01da: Invalid comparison between Unknown and I4
		//IL_01dd: Unknown result type (might be due to invalid IL or missing references)
		//IL_01e4: Invalid comparison between Unknown and I4
		//IL_01e7: Unknown result type (might be due to invalid IL or missing references)
		//IL_01ee: Invalid comparison between Unknown and I4
		//IL_01f1: Unknown result type (might be due to invalid IL or missing references)
		//IL_01f8: Invalid comparison between Unknown and I4
		//IL_01fb: Unknown result type (might be due to invalid IL or missing references)
		//IL_0202: Invalid comparison between Unknown and I4
		//IL_0205: Unknown result type (might be due to invalid IL or missing references)
		//IL_020c: Invalid comparison between Unknown and I4
		//IL_0226: Unknown result type (might be due to invalid IL or missing references)
		//IL_022b: Unknown result type (might be due to invalid IL or missing references)
		//IL_024b: Unknown result type (might be due to invalid IL or missing references)
		//IL_024d: Unknown result type (might be due to invalid IL or missing references)
		//IL_024f: Invalid comparison between Unknown and I4
		//IL_0257: Unknown result type (might be due to invalid IL or missing references)
		//IL_0259: Unknown result type (might be due to invalid IL or missing references)
		//IL_025b: Invalid comparison between Unknown and I4
		//IL_0263: Unknown result type (might be due to invalid IL or missing references)
		//IL_0265: Unknown result type (might be due to invalid IL or missing references)
		//IL_0267: Invalid comparison between Unknown and I4
		//IL_026f: Unknown result type (might be due to invalid IL or missing references)
		//IL_0271: Unknown result type (might be due to invalid IL or missing references)
		//IL_0273: Invalid comparison between Unknown and I4
		if ((int)e.Key == 0)
		{
			return;
		}
		if ((int)e.Key == 13 && _isRecordingTrigger)
		{
			StopTriggerRecording(saved: false);
			return;
		}
		string value = ((object)e.Key/*cast due to constrained. prefix*/).ToString();
		if (e.VkCode == 20)
		{
			value = "CapsLock (大写锁定)";
		}
		else if (e.VkCode == 192)
		{
			value = "~ (波浪键)";
		}
		else if (e.VkCode == 32)
		{
			value = "Space (空格)";
		}
		else if (e.VkCode == 9)
		{
			value = "Tab (制表键)";
		}
		ModifierKeys modifiers = e.Modifiers;
		string text = "";
		if (((((int)modifiers & 2))) != 0 && (int)e.Key != 118 && (int)e.Key != 119)
		{
			text += "Ctrl + ";
		}
		if (((((int)modifiers & 4))) != 0 && (int)e.Key != 116 && (int)e.Key != 117)
		{
			text += "Shift + ";
		}
		if (((((int)modifiers & 1))) != 0 && (int)e.Key != 120 && (int)e.Key != 121)
		{
			text += "Alt + ";
		}
		if (((((int)modifiers & 8))) != 0 && (int)e.Key != 70 && (int)e.Key != 71)
		{
			text += "Win + ";
		}
		if (LiveSensorStatusText != null)
		{
			LiveSensorStatusText.Text = $"\ud83d\udfe2 实时捕获键盘输入: {text}⌨\ufe0f {value} | 虚拟键码 VkCode: 0x{e.VkCode:X2}";
		}
		if (LiveSensorDot != null)
		{
			LiveSensorDot.Fill = new SolidColorBrush((System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#10B981"));
		}
		if (_isRecordingTrigger && (int)e.Key != 118 && (int)e.Key != 119 && (int)e.Key != 116 && (int)e.Key != 117 && (int)e.Key != 120 && (int)e.Key != 121 && (int)e.Key != 70 && (int)e.Key != 71)
		{
			ConfigManager.CurrentConfig.Trigger = new TriggerConfig
			{
				TriggerType = "Keyboard",
				Key = ((object)e.Key/*cast due to constrained. prefix*/).ToString(),
				VkCode = e.VkCode,
				RequireCtrl = (((((int)modifiers & 2))) > 0),
				RequireShift = (((((int)modifiers & 4))) > 0),
				RequireAlt = (((((int)modifiers & 1))) > 0),
				RequireWin = (((((int)modifiers & 8))) > 0)
			};
			ConfigManager.CurrentConfig.Trigger.DisplayText = FormatTriggerDisplay(ConfigManager.CurrentConfig.Trigger);
			ScheduleAutoSave();
			StopTriggerRecording(saved: true);
		}
	}

	private void ThresholdSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
	{
		if (ThresholdValueLabel != null && ConfigManager.CurrentConfig != null)
		{
			ThresholdValueLabel.Text = e.NewValue.ToString("0");
			ConfigManager.CurrentConfig.DragThreshold = e.NewValue;
			ScheduleAutoSave();
		}
	}

	private void UiStyleComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
	{
		if (!_isUpdatingUi && UiStyleComboBox != null && ConfigManager.CurrentConfig != null && UiStyleComboBox.SelectedItem is ComboBoxItem comboBoxItem)
		{
			ConfigManager.CurrentConfig.UiStyle = comboBoxItem.Tag?.ToString() ?? "ClassicRing";
			Grid appearanceSettingsGrid = AppearanceSettingsGrid;
			if (appearanceSettingsGrid != null && appearanceSettingsGrid.Visibility == Visibility.Visible)
			{
				RenderLiveWheelPreview();
			}
		}
	}

	private void SubWheelUiStyleComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
	{
		if (!_isUpdatingUi && SubWheelUiStyleComboBox != null && ConfigManager.CurrentConfig != null && SubWheelUiStyleComboBox.SelectedItem is ComboBoxItem { Tag: var tag })
		{
			string text = tag?.ToString() ?? "FollowPrimary";
			ConfigManager.CurrentConfig.SubWheelUiStyle = text;
			ConfigManager.CurrentConfig.UseIndependentSubWheelTheme = text != "FollowPrimary" || ConfigManager.CurrentConfig.SubWheelTheme != "FollowPrimary";
			Grid appearanceSettingsGrid = AppearanceSettingsGrid;
			if (appearanceSettingsGrid != null && appearanceSettingsGrid.Visibility == Visibility.Visible)
			{
				RenderLiveWheelPreview();
			}
			ScheduleAutoSave();
		}
	}

	private void SubWheelThemeComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
	{
		if (_isUpdatingUi || SubWheelThemeComboBox == null || ConfigManager.CurrentConfig == null || !(SubWheelThemeComboBox.SelectedItem is ComboBoxItem { Tag: var tag }))
		{
			return;
		}
		string text = tag?.ToString() ?? "FollowPrimary";
		ConfigManager.CurrentConfig.SubWheelTheme = text;
		ConfigManager.CurrentConfig.UseIndependentSubWheelTheme = text != "FollowPrimary" || ConfigManager.CurrentConfig.SubWheelUiStyle != "FollowPrimary";
		bool flag = text.StartsWith("CustomPreset_");
		if (RenameSubCustomColorPresetButton != null)
		{
			RenameSubCustomColorPresetButton.Visibility = ((!flag) ? Visibility.Collapsed : Visibility.Visible);
		}
		if (DeleteSubCustomColorPresetButton != null)
		{
			DeleteSubCustomColorPresetButton.Visibility = ((!flag) ? Visibility.Collapsed : Visibility.Visible);
		}
		if (DeleteSubPresetInPanelButton != null)
		{
			DeleteSubPresetInPanelButton.Visibility = ((!flag) ? Visibility.Collapsed : Visibility.Visible);
		}
		if (SaveSubPresetChangesButton != null)
		{
			SaveSubPresetChangesButton.Content = (flag ? I18n.T("SavePresetChangesButton") : I18n.T("SaveAsNewPresetButton"));
		}
		_isUpdatingUi = true;
		if (flag)
		{
			string presetId = text.Substring("CustomPreset_".Length);
			CustomColorPreset customColorPreset = ConfigManager.CurrentConfig.CustomColorPresets?.Find((CustomColorPreset p) => p.Id == presetId);
			if (customColorPreset != null)
			{
				if (SubCustomSectorBgTextBox != null)
				{
					SubCustomSectorBgTextBox.Text = customColorPreset.SectorBg;
				}
				if (SubCustomSectorBorderTextBox != null)
				{
					SubCustomSectorBorderTextBox.Text = customColorPreset.SectorBorder;
				}
				if (SubCustomHighlightBgTextBox != null)
				{
					SubCustomHighlightBgTextBox.Text = customColorPreset.HighlightBg;
				}
				if (SubCustomHighlightBorderTextBox != null)
				{
					SubCustomHighlightBorderTextBox.Text = customColorPreset.HighlightBorder;
				}
				if (SubCustomTextTextBox != null)
				{
					SubCustomTextTextBox.Text = customColorPreset.TextColor;
				}
				ConfigManager.CurrentConfig.SubWheelCustomSectorBg = customColorPreset.SectorBg;
				ConfigManager.CurrentConfig.SubWheelCustomSectorBorder = customColorPreset.SectorBorder;
				ConfigManager.CurrentConfig.SubWheelCustomHighlightBg = customColorPreset.HighlightBg;
				ConfigManager.CurrentConfig.SubWheelCustomHighlightBorder = customColorPreset.HighlightBorder;
				ConfigManager.CurrentConfig.SubWheelCustomText = customColorPreset.TextColor;
			}
		}
		else if (text != "FollowPrimary")
		{
			string text2 = ConfigManager.CurrentConfig.SubWheelUiStyle;
			if (string.IsNullOrEmpty(text2) || text2 == "FollowPrimary")
			{
				text2 = ConfigManager.CurrentConfig.UiStyle ?? "ClassicRing";
			}
			IRadialStyleRenderer radialStyleRenderer = StyleRendererFactory.CreateRenderer(text2);
			radialStyleRenderer.Initialize(text, ConfigManager.CurrentConfig);
			if (radialStyleRenderer.DefaultSectorBrush is SolidColorBrush solidColorBrush && SubCustomSectorBgTextBox != null)
			{
				SubCustomSectorBgTextBox.Text = $"#{solidColorBrush.Color.A:X2}{solidColorBrush.Color.R:X2}{solidColorBrush.Color.G:X2}{solidColorBrush.Color.B:X2}";
			}
			if (radialStyleRenderer.SectorBorderBrush is SolidColorBrush solidColorBrush2 && SubCustomSectorBorderTextBox != null)
			{
				SubCustomSectorBorderTextBox.Text = $"#{solidColorBrush2.Color.A:X2}{solidColorBrush2.Color.R:X2}{solidColorBrush2.Color.G:X2}{solidColorBrush2.Color.B:X2}";
			}
			if (radialStyleRenderer.HighlightSectorBrush is SolidColorBrush solidColorBrush3 && SubCustomHighlightBgTextBox != null)
			{
				SubCustomHighlightBgTextBox.Text = $"#{solidColorBrush3.Color.A:X2}{solidColorBrush3.Color.R:X2}{solidColorBrush3.Color.G:X2}{solidColorBrush3.Color.B:X2}";
			}
			if (radialStyleRenderer.HighlightBorderBrush is SolidColorBrush solidColorBrush4 && SubCustomHighlightBorderTextBox != null)
			{
				SubCustomHighlightBorderTextBox.Text = $"#{solidColorBrush4.Color.A:X2}{solidColorBrush4.Color.R:X2}{solidColorBrush4.Color.G:X2}{solidColorBrush4.Color.B:X2}";
			}
			if (radialStyleRenderer.TextColorBrush is SolidColorBrush solidColorBrush5 && SubCustomTextTextBox != null)
			{
				SubCustomTextTextBox.Text = $"#{solidColorBrush5.Color.A:X2}{solidColorBrush5.Color.R:X2}{solidColorBrush5.Color.G:X2}{solidColorBrush5.Color.B:X2}";
			}
		}
		_isUpdatingUi = false;
		UpdateSubColorPreviews();
		Grid appearanceSettingsGrid = AppearanceSettingsGrid;
		if (appearanceSettingsGrid != null && appearanceSettingsGrid.Visibility == Visibility.Visible)
		{
			RenderLiveWheelPreview();
		}
		ScheduleAutoSave();
	}

	private void SubCustomColorTextBox_TextChanged(object sender, TextChangedEventArgs e)
	{
		if (!_isUpdatingUi && ConfigManager.CurrentConfig != null)
		{
			if (SubCustomSectorBgTextBox != null && !string.IsNullOrWhiteSpace(SubCustomSectorBgTextBox.Text))
			{
				ConfigManager.CurrentConfig.SubWheelCustomSectorBg = SubCustomSectorBgTextBox.Text.Trim();
			}
			if (SubCustomSectorBorderTextBox != null && !string.IsNullOrWhiteSpace(SubCustomSectorBorderTextBox.Text))
			{
				ConfigManager.CurrentConfig.SubWheelCustomSectorBorder = SubCustomSectorBorderTextBox.Text.Trim();
			}
			if (SubCustomHighlightBgTextBox != null && !string.IsNullOrWhiteSpace(SubCustomHighlightBgTextBox.Text))
			{
				ConfigManager.CurrentConfig.SubWheelCustomHighlightBg = SubCustomHighlightBgTextBox.Text.Trim();
			}
			if (SubCustomHighlightBorderTextBox != null && !string.IsNullOrWhiteSpace(SubCustomHighlightBorderTextBox.Text))
			{
				ConfigManager.CurrentConfig.SubWheelCustomHighlightBorder = SubCustomHighlightBorderTextBox.Text.Trim();
			}
			if (SubCustomTextTextBox != null && !string.IsNullOrWhiteSpace(SubCustomTextTextBox.Text))
			{
				ConfigManager.CurrentConfig.SubWheelCustomText = SubCustomTextTextBox.Text.Trim();
			}
			UpdateSubColorPreviews();
			Grid appearanceSettingsGrid = AppearanceSettingsGrid;
			if (appearanceSettingsGrid != null && appearanceSettingsGrid.Visibility == Visibility.Visible)
			{
				RenderLiveWheelPreview();
			}
			ScheduleAutoSave();
		}
	}

	private void PopulateSubCustomColorsIfEmpty()
	{
		if (SubCustomSectorBgTextBox != null)
		{
			if (string.IsNullOrWhiteSpace(SubCustomSectorBgTextBox.Text) && _previewSubDefaultBrush is SolidColorBrush solidColorBrush)
			{
				SubCustomSectorBgTextBox.Text = $"#{solidColorBrush.Color.A:X2}{solidColorBrush.Color.R:X2}{solidColorBrush.Color.G:X2}{solidColorBrush.Color.B:X2}";
			}
			if (string.IsNullOrWhiteSpace(SubCustomSectorBorderTextBox.Text) && _previewSubBorderBrush is SolidColorBrush solidColorBrush2)
			{
				SubCustomSectorBorderTextBox.Text = $"#{solidColorBrush2.Color.A:X2}{solidColorBrush2.Color.R:X2}{solidColorBrush2.Color.G:X2}{solidColorBrush2.Color.B:X2}";
			}
			if (string.IsNullOrWhiteSpace(SubCustomHighlightBgTextBox.Text) && _previewSubHighlightBrush is SolidColorBrush solidColorBrush3)
			{
				SubCustomHighlightBgTextBox.Text = $"#{solidColorBrush3.Color.A:X2}{solidColorBrush3.Color.R:X2}{solidColorBrush3.Color.G:X2}{solidColorBrush3.Color.B:X2}";
			}
			if (string.IsNullOrWhiteSpace(SubCustomHighlightBorderTextBox.Text) && _previewSubHighlightBorderBrush is SolidColorBrush solidColorBrush4)
			{
				SubCustomHighlightBorderTextBox.Text = $"#{solidColorBrush4.Color.A:X2}{solidColorBrush4.Color.R:X2}{solidColorBrush4.Color.G:X2}{solidColorBrush4.Color.B:X2}";
			}
			if (string.IsNullOrWhiteSpace(SubCustomTextTextBox.Text) && _previewSubTextBrush is SolidColorBrush solidColorBrush5)
			{
				SubCustomTextTextBox.Text = $"#{solidColorBrush5.Color.A:X2}{solidColorBrush5.Color.R:X2}{solidColorBrush5.Color.G:X2}{solidColorBrush5.Color.B:X2}";
			}
			UpdateSubColorPreviews();
		}
	}

	private void UpdateSubColorPreviews()
	{
		if (SubCustomSectorBgPreview != null && SubCustomSectorBgTextBox != null)
		{
			UpdateColorPreviewBorder(SubCustomSectorBgPreview, SubCustomSectorBgTextBox.Text);
		}
		if (SubCustomSectorBorderPreview != null && SubCustomSectorBorderTextBox != null)
		{
			UpdateColorPreviewBorder(SubCustomSectorBorderPreview, SubCustomSectorBorderTextBox.Text);
		}
		if (SubCustomHighlightBgPreview != null && SubCustomHighlightBgTextBox != null)
		{
			UpdateColorPreviewBorder(SubCustomHighlightBgPreview, SubCustomHighlightBgTextBox.Text);
		}
		if (SubCustomHighlightBorderPreview != null && SubCustomHighlightBorderTextBox != null)
		{
			UpdateColorPreviewBorder(SubCustomHighlightBorderPreview, SubCustomHighlightBorderTextBox.Text);
		}
		if (SubCustomTextPreview != null && SubCustomTextTextBox != null)
		{
			UpdateColorPreviewBorder(SubCustomTextPreview, SubCustomTextTextBox.Text);
		}
	}

	private void NewSubCustomColorPresetButton_Click(object sender, RoutedEventArgs e)
	{
		if (ConfigManager.CurrentConfig == null)
		{
			return;
		}
		string defaultText = $"二级自定义配色 {DateTime.Now:MMdd-HHmm}";
		InputDialog inputDialog = new InputDialog(I18n.T("NewCustomPresetTitle"), I18n.T("NewCustomPresetPrompt"), defaultText, (string input) => string.IsNullOrWhiteSpace(input) ? (IsValid: false, ErrorMessage: "配色方案名称不能为空！") : (IsValid: true, ErrorMessage: ""))
		{
			Owner = this
		};
		if (inputDialog.ShowDialog() == true && !string.IsNullOrWhiteSpace(inputDialog.InputText))
		{
			string text = inputDialog.InputText.Trim();
			if (ConfigManager.CurrentConfig.CustomColorPresets == null)
			{
				ConfigManager.CurrentConfig.CustomColorPresets = new List<CustomColorPreset>();
			}
			PopulateSubCustomColorsIfEmpty();
			CustomColorPreset customColorPreset = new CustomColorPreset
			{
				Name = text,
				SectorBg = ((!string.IsNullOrWhiteSpace(SubCustomSectorBgTextBox?.Text)) ? SubCustomSectorBgTextBox.Text.Trim() : "#EB18181B"),
				SectorBorder = ((!string.IsNullOrWhiteSpace(SubCustomSectorBorderTextBox?.Text)) ? SubCustomSectorBorderTextBox.Text.Trim() : "#30FFFFFF"),
				HighlightBg = ((!string.IsNullOrWhiteSpace(SubCustomHighlightBgTextBox?.Text)) ? SubCustomHighlightBgTextBox.Text.Trim() : "#FF2563EB"),
				HighlightBorder = ((!string.IsNullOrWhiteSpace(SubCustomHighlightBorderTextBox?.Text)) ? SubCustomHighlightBorderTextBox.Text.Trim() : "#FF60A5FA"),
				TextColor = ((!string.IsNullOrWhiteSpace(SubCustomTextTextBox?.Text)) ? SubCustomTextTextBox.Text.Trim() : "#FFF8FAFC")
			};
			ConfigManager.CurrentConfig.CustomColorPresets.Add(customColorPreset);
			ConfigManager.CurrentConfig.SubWheelTheme = "CustomPreset_" + customColorPreset.Id;
			ConfigManager.CurrentConfig.UseIndependentSubWheelTheme = true;
			ConfigManager.SaveConfig();
			ReloadThemePresets();
			SetComboBoxSelectedValue(SubWheelThemeComboBox, "CustomPreset_" + customColorPreset.Id);
			if (SubCustomColorExpander != null)
			{
				SubCustomColorExpander.IsExpanded = true;
			}
			SyncUiToConfigAndSave();
			System.Windows.MessageBox.Show(this, "已成功创建二级自定义配色方案【" + text + "】！\n您可以在下方色彩微调面板中继续定制各项颜色。", "新建配色成功", MessageBoxButton.OK, MessageBoxImage.Asterisk);
		}
	}

	private void SaveSubPresetChangesButton_Click(object sender, RoutedEventArgs e)
	{
		if (ConfigManager.CurrentConfig == null)
		{
			return;
		}
		string text = (SubWheelThemeComboBox?.SelectedItem as ComboBoxItem)?.Tag?.ToString() ?? ConfigManager.CurrentConfig.SubWheelTheme ?? "";
		if (text.StartsWith("CustomPreset_"))
		{
			string presetId = text.Substring("CustomPreset_".Length);
			CustomColorPreset customColorPreset = ConfigManager.CurrentConfig.CustomColorPresets?.Find((CustomColorPreset p) => p.Id == presetId);
			if (customColorPreset != null)
			{
				if (SubCustomSectorBgTextBox != null)
				{
					customColorPreset.SectorBg = SubCustomSectorBgTextBox.Text.Trim();
				}
				if (SubCustomSectorBorderTextBox != null)
				{
					customColorPreset.SectorBorder = SubCustomSectorBorderTextBox.Text.Trim();
				}
				if (SubCustomHighlightBgTextBox != null)
				{
					customColorPreset.HighlightBg = SubCustomHighlightBgTextBox.Text.Trim();
				}
				if (SubCustomHighlightBorderTextBox != null)
				{
					customColorPreset.HighlightBorder = SubCustomHighlightBorderTextBox.Text.Trim();
				}
				if (SubCustomTextTextBox != null)
				{
					customColorPreset.TextColor = SubCustomTextTextBox.Text.Trim();
				}
				ConfigManager.SaveConfig();
				SyncUiToConfigAndSave();
				Grid appearanceSettingsGrid = AppearanceSettingsGrid;
				if (appearanceSettingsGrid != null && appearanceSettingsGrid.Visibility == Visibility.Visible)
				{
					RenderLiveWheelPreview();
				}
				System.Windows.MessageBox.Show(this, "已成功保存对配色预设【" + customColorPreset.Name + "】的修改！", "保存配色修改", MessageBoxButton.OK, MessageBoxImage.Asterisk);
				return;
			}
		}
		SaveAsNewSubPresetButton_Click(sender, e);
	}

	private void SaveAsNewSubPresetButton_Click(object sender, RoutedEventArgs e)
	{
		if (ConfigManager.CurrentConfig == null)
		{
			return;
		}
		string defaultText = $"二级自定义配色 {DateTime.Now:MMdd-HHmm}";
		InputDialog inputDialog = new InputDialog("另存为新配色方案", "请输入新配色方案名称：", defaultText, (string input) => string.IsNullOrWhiteSpace(input) ? (IsValid: false, ErrorMessage: "配色方案名称不能为空！") : (IsValid: true, ErrorMessage: ""))
		{
			Owner = this
		};
		if (inputDialog.ShowDialog() == true && !string.IsNullOrWhiteSpace(inputDialog.InputText))
		{
			string text = inputDialog.InputText.Trim();
			if (ConfigManager.CurrentConfig.CustomColorPresets == null)
			{
				ConfigManager.CurrentConfig.CustomColorPresets = new List<CustomColorPreset>();
			}
			CustomColorPreset customColorPreset = new CustomColorPreset
			{
				Name = text,
				SectorBg = ((!string.IsNullOrWhiteSpace(SubCustomSectorBgTextBox?.Text)) ? SubCustomSectorBgTextBox.Text.Trim() : "#EB18181B"),
				SectorBorder = ((!string.IsNullOrWhiteSpace(SubCustomSectorBorderTextBox?.Text)) ? SubCustomSectorBorderTextBox.Text.Trim() : "#30FFFFFF"),
				HighlightBg = ((!string.IsNullOrWhiteSpace(SubCustomHighlightBgTextBox?.Text)) ? SubCustomHighlightBgTextBox.Text.Trim() : "#FF2563EB"),
				HighlightBorder = ((!string.IsNullOrWhiteSpace(SubCustomHighlightBorderTextBox?.Text)) ? SubCustomHighlightBorderTextBox.Text.Trim() : "#FF60A5FA"),
				TextColor = ((!string.IsNullOrWhiteSpace(SubCustomTextTextBox?.Text)) ? SubCustomTextTextBox.Text.Trim() : "#FFF8FAFC")
			};
			ConfigManager.CurrentConfig.CustomColorPresets.Add(customColorPreset);
			ConfigManager.CurrentConfig.SubWheelTheme = "CustomPreset_" + customColorPreset.Id;
			ConfigManager.CurrentConfig.UseIndependentSubWheelTheme = true;
			ConfigManager.SaveConfig();
			ReloadThemePresets();
			SetComboBoxSelectedValue(SubWheelThemeComboBox, "CustomPreset_" + customColorPreset.Id);
			if (SubCustomColorExpander != null)
			{
				SubCustomColorExpander.IsExpanded = true;
			}
			SyncUiToConfigAndSave();
			System.Windows.MessageBox.Show(this, "配色方案【" + text + "】已成功另存为独立预设！", "另存预设成功", MessageBoxButton.OK, MessageBoxImage.Asterisk);
		}
	}

	private void RenameSubCustomColorPresetButton_Click(object sender, RoutedEventArgs e)
	{
		if (ConfigManager.CurrentConfig == null)
		{
			return;
		}
		string text = (SubWheelThemeComboBox?.SelectedItem as ComboBoxItem)?.Tag?.ToString() ?? ConfigManager.CurrentConfig.SubWheelTheme ?? "";
		if (!text.StartsWith("CustomPreset_"))
		{
			return;
		}
		string presetId = text.Substring("CustomPreset_".Length);
		CustomColorPreset customColorPreset = ConfigManager.CurrentConfig.CustomColorPresets?.Find((CustomColorPreset p) => p.Id == presetId);
		if (customColorPreset != null)
		{
			string name = customColorPreset.Name;
			InputDialog inputDialog = new InputDialog(I18n.T("RenameCustomPresetTitle"), I18n.T("RenameCustomPresetPrompt") + "「" + name + "」", name, (string input) => string.IsNullOrWhiteSpace(input) ? (IsValid: false, ErrorMessage: "配色方案名称不能为空！") : (IsValid: true, ErrorMessage: ""))
			{
				Owner = this
			};
			if (inputDialog.ShowDialog() == true && !string.IsNullOrEmpty(inputDialog.InputText))
			{
				customColorPreset.Name = inputDialog.InputText.Trim();
				ConfigManager.SaveConfig();
				ReloadThemePresets();
				SetComboBoxSelectedValue(SubWheelThemeComboBox, "CustomPreset_" + customColorPreset.Id);
				SyncUiToConfigAndSave();
			}
		}
	}

	private void DeleteSubCustomColorPresetButton_Click(object sender, RoutedEventArgs e)
	{
		if (ConfigManager.CurrentConfig == null)
		{
			return;
		}
		string text = (SubWheelThemeComboBox?.SelectedItem as ComboBoxItem)?.Tag?.ToString() ?? ConfigManager.CurrentConfig.SubWheelTheme ?? "";
		if (!text.StartsWith("CustomPreset_"))
		{
			return;
		}
		string presetId = text.Substring("CustomPreset_".Length);
		CustomColorPreset customColorPreset = ConfigManager.CurrentConfig.CustomColorPresets?.Find((CustomColorPreset p) => p.Id == presetId);
		if (customColorPreset != null && System.Windows.MessageBox.Show(this, "确定要删除自定义配色方案预设【" + customColorPreset.Name + "】吗？", "确认删除配色方案", MessageBoxButton.YesNo, MessageBoxImage.Question) == MessageBoxResult.Yes)
		{
			ConfigManager.CurrentConfig.CustomColorPresets?.Remove(customColorPreset);
			if (ConfigManager.CurrentConfig.Theme == "CustomPreset_" + customColorPreset.Id)
			{
				ConfigManager.CurrentConfig.Theme = "System";
			}
			ConfigManager.CurrentConfig.SubWheelTheme = "FollowPrimary";
			ConfigManager.SaveConfig();
			ReloadThemePresets();
			SetComboBoxSelectedValue(SubWheelThemeComboBox, "FollowPrimary");
			if (RenameSubCustomColorPresetButton != null)
			{
				RenameSubCustomColorPresetButton.Visibility = Visibility.Collapsed;
			}
			if (DeleteSubCustomColorPresetButton != null)
			{
				DeleteSubCustomColorPresetButton.Visibility = Visibility.Collapsed;
			}
			if (DeleteSubPresetInPanelButton != null)
			{
				DeleteSubPresetInPanelButton.Visibility = Visibility.Collapsed;
			}
			Grid appearanceSettingsGrid = AppearanceSettingsGrid;
			if (appearanceSettingsGrid != null && appearanceSettingsGrid.Visibility == Visibility.Visible)
			{
				RenderLiveWheelPreview();
			}
			SyncUiToConfigAndSave();
			System.Windows.MessageBox.Show(this, "自定义配色方案【" + customColorPreset.Name + "】已成功删除！", "提示", MessageBoxButton.OK, MessageBoxImage.Asterisk);
		}
	}

	private void ResetSubThemeButton_Click(object sender, RoutedEventArgs e)
	{
		if (ConfigManager.CurrentConfig == null)
		{
			return;
		}
		_isUpdatingUi = true;
		try
		{
			ConfigManager.CurrentConfig.SubWheelUiStyle = "FollowPrimary";
			ConfigManager.CurrentConfig.SubWheelTheme = "FollowPrimary";
			ConfigManager.CurrentConfig.UseIndependentSubWheelTheme = false;
			ConfigManager.CurrentConfig.SubWheelCustomSectorBg = null;
			ConfigManager.CurrentConfig.SubWheelCustomSectorBorder = null;
			ConfigManager.CurrentConfig.SubWheelCustomHighlightBg = null;
			ConfigManager.CurrentConfig.SubWheelCustomHighlightBorder = null;
			ConfigManager.CurrentConfig.SubWheelCustomText = null;
			ConfigManager.CurrentConfig.SubWheelHighlightGlowPreset = "FollowPrimary";
			ConfigManager.CurrentConfig.SubWheelHighlightGlowColor = "";
			ConfigManager.CurrentConfig.SubWheelHighlightGlowRadius = 24.0;
			ConfigManager.CurrentConfig.SubWheelHighlightGlowOpacity = 0.85;
			SetComboBoxSelectedValue(SubWheelUiStyleComboBox, "FollowPrimary");
			SetComboBoxSelectedValue(SubWheelThemeComboBox, "FollowPrimary");
			SetComboBoxSelectedValue(SubHighlightGlowPresetComboBox, "FollowPrimary");
			if (SubHighlightGlowColorTextBox != null)
			{
				SubHighlightGlowColorTextBox.Text = "";
			}
			if (SubHighlightGlowRadiusSlider != null)
			{
				SubHighlightGlowRadiusSlider.Value = 24.0;
				if (SubHighlightGlowRadiusLabel != null)
				{
					SubHighlightGlowRadiusLabel.Text = "24 px";
				}
			}
			if (SubHighlightGlowOpacitySlider != null)
			{
				SubHighlightGlowOpacitySlider.Value = 85.0;
				if (SubHighlightGlowOpacityLabel != null)
				{
					SubHighlightGlowOpacityLabel.Text = "85%";
				}
			}
			if (SubCustomHighlightGlowPanel != null)
			{
				SubCustomHighlightGlowPanel.Visibility = Visibility.Collapsed;
			}
			if (SubCustomSectorBgTextBox != null)
			{
				SubCustomSectorBgTextBox.Text = "";
			}
			if (SubCustomSectorBorderTextBox != null)
			{
				SubCustomSectorBorderTextBox.Text = "";
			}
			if (SubCustomHighlightBgTextBox != null)
			{
				SubCustomHighlightBgTextBox.Text = "";
			}
			if (SubCustomHighlightBorderTextBox != null)
			{
				SubCustomHighlightBorderTextBox.Text = "";
			}
			if (SubCustomTextTextBox != null)
			{
				SubCustomTextTextBox.Text = "";
			}
		}
		finally
		{
			_isUpdatingUi = false;
		}
		UpdateSubColorPreviews();
		RenderLiveWheelPreview();
		SyncUiToConfigAndSave();
		System.Windows.MessageBox.Show(this, "已重置二级轮盘为跟随一级主轮盘视觉风格与配色！", "重置成功", MessageBoxButton.OK, MessageBoxImage.Asterisk);
	}

	private void ReloadThemePresets()
	{
		string value = ConfigManager.CurrentConfig?.Theme ?? (ThemeComboBox?.SelectedItem as ComboBoxItem)?.Tag?.ToString() ?? "System";
		string value2 = ConfigManager.CurrentConfig?.SubWheelTheme ?? (SubWheelThemeComboBox?.SelectedItem as ComboBoxItem)?.Tag?.ToString() ?? "FollowPrimary";
		if (ThemeComboBox != null)
		{
			List<ComboBoxItem> list = new List<ComboBoxItem>();
			foreach (object item in (IEnumerable)ThemeComboBox.Items)
			{
				if (item is ComboBoxItem { Tag: not null } comboBoxItem && comboBoxItem.Tag.ToString().StartsWith("CustomPreset_"))
				{
					list.Add(comboBoxItem);
				}
			}
			foreach (ComboBoxItem item2 in list)
			{
				ThemeComboBox.Items.Remove(item2);
			}
			int num = -1;
			for (int i = 0; i < ThemeComboBox.Items.Count; i++)
			{
				if (ThemeComboBox.Items[i] is ComboBoxItem { Tag: var tag } && tag?.ToString() == "Custom")
				{
					num = i;
					break;
				}
			}
			if (ConfigManager.CurrentConfig?.CustomColorPresets != null)
			{
				foreach (CustomColorPreset customColorPreset in ConfigManager.CurrentConfig.CustomColorPresets)
				{
					ComboBoxItem comboBoxItem3 = new ComboBoxItem
					{
						Content = "\ud83c\udfa8 " + customColorPreset.Name + " (自定义预设)",
						Tag = "CustomPreset_" + customColorPreset.Id
					};
					if (num >= 0)
					{
						ThemeComboBox.Items.Insert(num, comboBoxItem3);
						num++;
					}
					else
					{
						ThemeComboBox.Items.Add(comboBoxItem3);
					}
				}
			}
			SetComboBoxSelectedValue(ThemeComboBox, value);
		}
		if (SubWheelThemeComboBox == null)
		{
			return;
		}
		List<ComboBoxItem> list2 = new List<ComboBoxItem>();
		foreach (object item3 in (IEnumerable)SubWheelThemeComboBox.Items)
		{
			if (item3 is ComboBoxItem { Tag: not null } comboBoxItem4 && comboBoxItem4.Tag.ToString().StartsWith("CustomPreset_"))
			{
				list2.Add(comboBoxItem4);
			}
		}
		foreach (ComboBoxItem item4 in list2)
		{
			SubWheelThemeComboBox.Items.Remove(item4);
		}
		if (ConfigManager.CurrentConfig?.CustomColorPresets != null)
		{
			foreach (CustomColorPreset customColorPreset2 in ConfigManager.CurrentConfig.CustomColorPresets)
			{
				ComboBoxItem newItem = new ComboBoxItem
				{
					Content = "\ud83c\udfa8 " + customColorPreset2.Name + " (自定义预设)",
					Tag = "CustomPreset_" + customColorPreset2.Id
				};
				SubWheelThemeComboBox.Items.Add(newItem);
			}
		}
		SetComboBoxSelectedValue(SubWheelThemeComboBox, value2);
	}

	private void ThemeComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
	{
		if (_isUpdatingUi || ThemeComboBox == null || ConfigManager.CurrentConfig == null || !(ThemeComboBox.SelectedItem is ComboBoxItem { Tag: var tag }))
		{
			return;
		}
		string text = tag?.ToString() ?? "System";
		ConfigManager.CurrentConfig.Theme = text;
		bool flag = text.StartsWith("CustomPreset_");
		if (RenameCustomColorPresetButton != null)
		{
			RenameCustomColorPresetButton.Visibility = ((!flag) ? Visibility.Collapsed : Visibility.Visible);
		}
		if (DeleteCustomColorPresetButton != null)
		{
			DeleteCustomColorPresetButton.Visibility = ((!flag) ? Visibility.Collapsed : Visibility.Visible);
		}
		if (DeletePresetInPanelButton != null)
		{
			DeletePresetInPanelButton.Visibility = ((!flag) ? Visibility.Collapsed : Visibility.Visible);
		}
		if (SavePresetChangesButton != null)
		{
			SavePresetChangesButton.Content = (flag ? I18n.T("SavePresetChangesButton") : I18n.T("SaveAsNewPresetButton"));
		}
		_isUpdatingUi = true;
		if (flag)
		{
			string presetId = text.Substring("CustomPreset_".Length);
			CustomColorPreset customColorPreset = ConfigManager.CurrentConfig.CustomColorPresets?.Find((CustomColorPreset p) => p.Id == presetId);
			if (customColorPreset != null)
			{
				CustomSectorBgTextBox.Text = customColorPreset.SectorBg;
				CustomSectorBorderTextBox.Text = customColorPreset.SectorBorder;
				CustomHighlightBgTextBox.Text = customColorPreset.HighlightBg;
				CustomHighlightBorderTextBox.Text = customColorPreset.HighlightBorder;
				CustomTextTextBox.Text = customColorPreset.TextColor;
			}
		}
		else
		{
			IRadialStyleRenderer radialStyleRenderer = StyleRendererFactory.CreateRenderer(ConfigManager.CurrentConfig.UiStyle ?? "ClassicRing");
			radialStyleRenderer.Initialize(text, ConfigManager.CurrentConfig);
			if (radialStyleRenderer.DefaultSectorBrush is SolidColorBrush solidColorBrush)
			{
				CustomSectorBgTextBox.Text = $"#{solidColorBrush.Color.A:X2}{solidColorBrush.Color.R:X2}{solidColorBrush.Color.G:X2}{solidColorBrush.Color.B:X2}";
			}
			if (radialStyleRenderer.SectorBorderBrush is SolidColorBrush solidColorBrush2)
			{
				CustomSectorBorderTextBox.Text = $"#{solidColorBrush2.Color.A:X2}{solidColorBrush2.Color.R:X2}{solidColorBrush2.Color.G:X2}{solidColorBrush2.Color.B:X2}";
			}
			if (radialStyleRenderer.HighlightSectorBrush is SolidColorBrush solidColorBrush3)
			{
				CustomHighlightBgTextBox.Text = $"#{solidColorBrush3.Color.A:X2}{solidColorBrush3.Color.R:X2}{solidColorBrush3.Color.G:X2}{solidColorBrush3.Color.B:X2}";
			}
			if (radialStyleRenderer.HighlightBorderBrush is SolidColorBrush solidColorBrush4)
			{
				CustomHighlightBorderTextBox.Text = $"#{solidColorBrush4.Color.A:X2}{solidColorBrush4.Color.R:X2}{solidColorBrush4.Color.G:X2}{solidColorBrush4.Color.B:X2}";
			}
			if (radialStyleRenderer.TextColorBrush is SolidColorBrush solidColorBrush5)
			{
				CustomTextTextBox.Text = $"#{solidColorBrush5.Color.A:X2}{solidColorBrush5.Color.R:X2}{solidColorBrush5.Color.G:X2}{solidColorBrush5.Color.B:X2}";
			}
		}
		_isUpdatingUi = false;
		UpdateColorPreviews();
		Grid appearanceSettingsGrid = AppearanceSettingsGrid;
		if (appearanceSettingsGrid != null && appearanceSettingsGrid.Visibility == Visibility.Visible)
		{
			RenderLiveWheelPreview();
		}
		SyncUiToConfigAndSave();
	}

	private void OuterEscapeCheckBox_Checked(object sender, RoutedEventArgs e)
	{
		if (!_isUpdatingUi && ConfigManager.CurrentConfig != null && ConfigManager.CurrentConfig != null)
		{
			ConfigManager.CurrentConfig.EnableOuterEscapeCancel = true;
			if (OuterEscapeDistancePanel != null)
			{
				OuterEscapeDistancePanel.Visibility = Visibility.Visible;
			}
			UpdateCancelActionAvailability();
			SyncUiToConfigAndSave();
		}
	}

	private void OuterEscapeCheckBox_Unchecked(object sender, RoutedEventArgs e)
	{
		if (!_isUpdatingUi && ConfigManager.CurrentConfig != null && ConfigManager.CurrentConfig != null)
		{
			ConfigManager.CurrentConfig.EnableOuterEscapeCancel = false;
			if (OuterEscapeDistancePanel != null)
			{
				OuterEscapeDistancePanel.Visibility = Visibility.Collapsed;
			}
			UpdateCancelActionAvailability();
			SyncUiToConfigAndSave();
		}
	}

	/// <summary>「外甩取消时执行的动作」依赖顺势外甩取消主开关：主开关关闭时整块禁用。</summary>
	private void UpdateCancelActionAvailability()
	{
		bool master = ConfigManager.CurrentConfig?.EnableOuterEscapeCancel == true;
		if (EnableCancelActionCheckBox != null)
		{
			EnableCancelActionCheckBox.IsEnabled = master;
		}
		if (CancelActionEditorHost != null)
		{
			CancelActionEditorHost.IsEnabled = master;
		}
		if (TestCancelActionButton != null)
		{
			TestCancelActionButton.IsEnabled = master;
		}
	}

	private void OuterEscapeDistanceSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
	{
		if (!_isUpdatingUi && ConfigManager.CurrentConfig != null)
		{
			double num = Math.Round(e.NewValue);
			ConfigManager.CurrentConfig.OuterEscapeDistance = num;
			if (OuterEscapeDistanceLabel != null)
			{
				OuterEscapeDistanceLabel.Text = $"{num:0} px";
			}
			SyncUiToConfigAndSave();
		}
	}

	private void AnimSpeedRadio_Checked(object sender, RoutedEventArgs e)
	{
		if (_isUpdatingUi || ConfigManager.CurrentConfig == null)
		{
			return;
		}
		System.Windows.Controls.RadioButton animSpeedElegantRadio = AnimSpeedElegantRadio;
		if (animSpeedElegantRadio != null && animSpeedElegantRadio.IsChecked == true)
		{
			ConfigManager.CurrentConfig.AnimationSpeed = "Elegant";
			ConfigManager.CurrentConfig.CustomAnimationDurationMs = 130.0;
			if (AnimSpeedSlider != null)
			{
				AnimSpeedSlider.Value = 130.0;
			}
		}
		else
		{
			System.Windows.Controls.RadioButton animSpeedFastRadio = AnimSpeedFastRadio;
			if (animSpeedFastRadio != null && animSpeedFastRadio.IsChecked == true)
			{
				ConfigManager.CurrentConfig.AnimationSpeed = "Fast";
				ConfigManager.CurrentConfig.CustomAnimationDurationMs = 35.0;
				if (AnimSpeedSlider != null)
				{
					AnimSpeedSlider.Value = 35.0;
				}
			}
			else
			{
				System.Windows.Controls.RadioButton animSpeedCustomRadio = AnimSpeedCustomRadio;
				if (animSpeedCustomRadio != null && animSpeedCustomRadio.IsChecked == true)
				{
					ConfigManager.CurrentConfig.AnimationSpeed = "Custom";
				}
				else
				{
					ConfigManager.CurrentConfig.AnimationSpeed = "Balanced";
					ConfigManager.CurrentConfig.CustomAnimationDurationMs = 80.0;
					if (AnimSpeedSlider != null)
					{
						AnimSpeedSlider.Value = 80.0;
					}
				}
			}
		}
		SyncUiToConfigAndSave();
	}

	private void AnimSpeedSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
	{
		if (AnimSpeedSliderLabel == null || ConfigManager.CurrentConfig == null || _isUpdatingUi)
		{
			return;
		}
		double num = Math.Round(e.NewValue);
		ConfigManager.CurrentConfig.CustomAnimationDurationMs = num;
		AnimSpeedSliderLabel.Text = $"{num:0} ms";
		if (Math.Abs(num - 130.0) < 1.0)
		{
			ConfigManager.CurrentConfig.AnimationSpeed = "Elegant";
			if (AnimSpeedElegantRadio != null)
			{
				AnimSpeedElegantRadio.IsChecked = true;
			}
		}
		else if (Math.Abs(num - 80.0) < 1.0)
		{
			ConfigManager.CurrentConfig.AnimationSpeed = "Balanced";
			if (AnimSpeedBalancedRadio != null)
			{
				AnimSpeedBalancedRadio.IsChecked = true;
			}
		}
		else if (Math.Abs(num - 35.0) < 1.0)
		{
			ConfigManager.CurrentConfig.AnimationSpeed = "Fast";
			if (AnimSpeedFastRadio != null)
			{
				AnimSpeedFastRadio.IsChecked = true;
			}
		}
		else
		{
			ConfigManager.CurrentConfig.AnimationSpeed = "Custom";
			if (AnimSpeedCustomRadio != null)
			{
				AnimSpeedCustomRadio.IsChecked = true;
			}
		}
		ScheduleAutoSave();
	}

	private void NewCustomColorPresetButton_Click(object sender, RoutedEventArgs e)
	{
		if (ConfigManager.CurrentConfig == null)
		{
			return;
		}
		string defaultText = $"自定义配色 {DateTime.Now:MMdd-HHmm}";
		InputDialog inputDialog = new InputDialog(I18n.T("NewCustomPresetTitle"), I18n.T("NewCustomPresetPrompt"), defaultText, (string input) => string.IsNullOrWhiteSpace(input) ? (IsValid: false, ErrorMessage: "配色方案名称不能为空！") : (IsValid: true, ErrorMessage: ""))
		{
			Owner = this
		};
		if (inputDialog.ShowDialog() == true && !string.IsNullOrWhiteSpace(inputDialog.InputText))
		{
			string text = inputDialog.InputText.Trim();
			if (ConfigManager.CurrentConfig.CustomColorPresets == null)
			{
				ConfigManager.CurrentConfig.CustomColorPresets = new List<CustomColorPreset>();
			}
			PopulateCustomColorsIfEmpty();
			CustomColorPreset customColorPreset = new CustomColorPreset
			{
				Name = text,
				SectorBg = ((!string.IsNullOrWhiteSpace(CustomSectorBgTextBox.Text)) ? CustomSectorBgTextBox.Text.Trim() : "#EB18181B"),
				SectorBorder = ((!string.IsNullOrWhiteSpace(CustomSectorBorderTextBox.Text)) ? CustomSectorBorderTextBox.Text.Trim() : "#30FFFFFF"),
				HighlightBg = ((!string.IsNullOrWhiteSpace(CustomHighlightBgTextBox.Text)) ? CustomHighlightBgTextBox.Text.Trim() : "#FF2563EB"),
				HighlightBorder = ((!string.IsNullOrWhiteSpace(CustomHighlightBorderTextBox.Text)) ? CustomHighlightBorderTextBox.Text.Trim() : "#FF60A5FA"),
				TextColor = ((!string.IsNullOrWhiteSpace(CustomTextTextBox.Text)) ? CustomTextTextBox.Text.Trim() : "#FFF8FAFC")
			};
			ConfigManager.CurrentConfig.CustomColorPresets.Add(customColorPreset);
			ConfigManager.CurrentConfig.Theme = "CustomPreset_" + customColorPreset.Id;
			ConfigManager.SaveConfig();
			ReloadThemePresets();
			SetComboBoxSelectedValue(ThemeComboBox, "CustomPreset_" + customColorPreset.Id);
			if (CustomColorExpander != null)
			{
				CustomColorExpander.IsExpanded = true;
			}
			SyncUiToConfigAndSave();
			System.Windows.MessageBox.Show(this, "已成功创建自定义配色方案【" + text + "】！\n您可以在下方色彩微调面板中继续定制各项颜色。", "新建配色成功", MessageBoxButton.OK, MessageBoxImage.Asterisk);
		}
	}

	private void SavePresetChangesButton_Click(object sender, RoutedEventArgs e)
	{
		if (ConfigManager.CurrentConfig == null)
		{
			return;
		}
		string text = (ThemeComboBox?.SelectedItem as ComboBoxItem)?.Tag?.ToString() ?? ConfigManager.CurrentConfig.Theme;
		if (text.StartsWith("CustomPreset_"))
		{
			string presetId = text.Substring("CustomPreset_".Length);
			CustomColorPreset customColorPreset = ConfigManager.CurrentConfig.CustomColorPresets?.Find((CustomColorPreset p) => p.Id == presetId);
			if (customColorPreset != null)
			{
				customColorPreset.SectorBg = CustomSectorBgTextBox.Text.Trim();
				customColorPreset.SectorBorder = CustomSectorBorderTextBox.Text.Trim();
				customColorPreset.HighlightBg = CustomHighlightBgTextBox.Text.Trim();
				customColorPreset.HighlightBorder = CustomHighlightBorderTextBox.Text.Trim();
				customColorPreset.TextColor = CustomTextTextBox.Text.Trim();
				ConfigManager.SaveConfig();
				SyncUiToConfigAndSave();
				Grid appearanceSettingsGrid = AppearanceSettingsGrid;
				if (appearanceSettingsGrid != null && appearanceSettingsGrid.Visibility == Visibility.Visible)
				{
					RenderLiveWheelPreview();
				}
				System.Windows.MessageBox.Show(this, "已成功保存对配色预设【" + customColorPreset.Name + "】的修改！", "保存配色修改", MessageBoxButton.OK, MessageBoxImage.Asterisk);
				return;
			}
		}
		SaveAsNewPresetButton_Click(sender, e);
	}

	private void SaveAsNewPresetButton_Click(object sender, RoutedEventArgs e)
	{
		if (ConfigManager.CurrentConfig == null)
		{
			return;
		}
		string defaultText = $"自定义配色 {DateTime.Now:MMdd-HHmm}";
		InputDialog inputDialog = new InputDialog("另存为新配色方案", "请输入新配色方案名称：", defaultText, (string input) => string.IsNullOrWhiteSpace(input) ? (IsValid: false, ErrorMessage: "配色方案名称不能为空！") : (IsValid: true, ErrorMessage: ""))
		{
			Owner = this
		};
		if (inputDialog.ShowDialog() == true && !string.IsNullOrWhiteSpace(inputDialog.InputText))
		{
			string text = inputDialog.InputText.Trim();
			if (ConfigManager.CurrentConfig.CustomColorPresets == null)
			{
				ConfigManager.CurrentConfig.CustomColorPresets = new List<CustomColorPreset>();
			}
			CustomColorPreset customColorPreset = new CustomColorPreset
			{
				Name = text,
				SectorBg = ((!string.IsNullOrWhiteSpace(CustomSectorBgTextBox.Text)) ? CustomSectorBgTextBox.Text.Trim() : "#EB18181B"),
				SectorBorder = ((!string.IsNullOrWhiteSpace(CustomSectorBorderTextBox.Text)) ? CustomSectorBorderTextBox.Text.Trim() : "#30FFFFFF"),
				HighlightBg = ((!string.IsNullOrWhiteSpace(CustomHighlightBgTextBox.Text)) ? CustomHighlightBgTextBox.Text.Trim() : "#FF2563EB"),
				HighlightBorder = ((!string.IsNullOrWhiteSpace(CustomHighlightBorderTextBox.Text)) ? CustomHighlightBorderTextBox.Text.Trim() : "#FF60A5FA"),
				TextColor = ((!string.IsNullOrWhiteSpace(CustomTextTextBox.Text)) ? CustomTextTextBox.Text.Trim() : "#FFF8FAFC")
			};
			ConfigManager.CurrentConfig.CustomColorPresets.Add(customColorPreset);
			ConfigManager.CurrentConfig.Theme = "CustomPreset_" + customColorPreset.Id;
			ConfigManager.SaveConfig();
			ReloadThemePresets();
			SetComboBoxSelectedValue(ThemeComboBox, "CustomPreset_" + customColorPreset.Id);
			if (CustomColorExpander != null)
			{
				CustomColorExpander.IsExpanded = true;
			}
			SyncUiToConfigAndSave();
			System.Windows.MessageBox.Show(this, "配色方案【" + text + "】已成功另存为独立预设！", "另存预设成功", MessageBoxButton.OK, MessageBoxImage.Asterisk);
		}
	}

	private void RenameCustomColorPresetButton_Click(object sender, RoutedEventArgs e)
	{
		if (ConfigManager.CurrentConfig == null)
		{
			return;
		}
		string text = (ThemeComboBox?.SelectedItem as ComboBoxItem)?.Tag?.ToString() ?? ConfigManager.CurrentConfig.Theme;
		if (!text.StartsWith("CustomPreset_"))
		{
			return;
		}
		string presetId = text.Substring("CustomPreset_".Length);
		CustomColorPreset customColorPreset = ConfigManager.CurrentConfig.CustomColorPresets?.Find((CustomColorPreset p) => p.Id == presetId);
		if (customColorPreset != null)
		{
			string name = customColorPreset.Name;
			InputDialog inputDialog = new InputDialog(I18n.T("RenameCustomPresetTitle"), I18n.T("RenameCustomPresetPrompt") + "「" + name + "」", name, (string input) => string.IsNullOrWhiteSpace(input) ? (IsValid: false, ErrorMessage: "配色方案名称不能为空！") : (IsValid: true, ErrorMessage: ""))
			{
				Owner = this
			};
			if (inputDialog.ShowDialog() == true && !string.IsNullOrEmpty(inputDialog.InputText))
			{
				customColorPreset.Name = inputDialog.InputText.Trim();
				ConfigManager.SaveConfig();
				ReloadThemePresets();
				SetComboBoxSelectedValue(ThemeComboBox, "CustomPreset_" + customColorPreset.Id);
				SyncUiToConfigAndSave();
			}
		}
	}

	private void DeleteCustomColorPresetButton_Click(object sender, RoutedEventArgs e)
	{
		if (ConfigManager.CurrentConfig == null)
		{
			return;
		}
		string text = (ThemeComboBox?.SelectedItem as ComboBoxItem)?.Tag?.ToString() ?? ConfigManager.CurrentConfig.Theme;
		if (!text.StartsWith("CustomPreset_"))
		{
			return;
		}
		string presetId = text.Substring("CustomPreset_".Length);
		CustomColorPreset customColorPreset = ConfigManager.CurrentConfig.CustomColorPresets?.Find((CustomColorPreset p) => p.Id == presetId);
		if (customColorPreset != null && System.Windows.MessageBox.Show(this, "确定要删除自定义配色方案预设【" + customColorPreset.Name + "】吗？", "确认删除配色方案", MessageBoxButton.YesNo, MessageBoxImage.Question) == MessageBoxResult.Yes)
		{
			ConfigManager.CurrentConfig.CustomColorPresets?.Remove(customColorPreset);
			ConfigManager.CurrentConfig.Theme = "System";
			ConfigManager.SaveConfig();
			ReloadThemePresets();
			SetComboBoxSelectedValue(ThemeComboBox, "System");
			if (RenameCustomColorPresetButton != null)
			{
				RenameCustomColorPresetButton.Visibility = Visibility.Collapsed;
			}
			if (DeleteCustomColorPresetButton != null)
			{
				DeleteCustomColorPresetButton.Visibility = Visibility.Collapsed;
			}
			if (DeletePresetInPanelButton != null)
			{
				DeletePresetInPanelButton.Visibility = Visibility.Collapsed;
			}
			Grid appearanceSettingsGrid = AppearanceSettingsGrid;
			if (appearanceSettingsGrid != null && appearanceSettingsGrid.Visibility == Visibility.Visible)
			{
				RenderLiveWheelPreview();
			}
			SyncUiToConfigAndSave();
			System.Windows.MessageBox.Show(this, "自定义配色方案【" + customColorPreset.Name + "】已成功删除！", "提示", MessageBoxButton.OK, MessageBoxImage.Asterisk);
		}
	}

	private void WheelRadiusSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
	{
		if (_isUpdatingUi || ConfigManager.CurrentConfig == null || WheelRadiusLabel == null)
		{
			return;
		}
		double num = Math.Round(e.NewValue);
		WheelRadiusLabel.Text = num.ToString("0");
		ConfigManager.CurrentConfig.WheelRadius = num;
		_isUpdatingUi = true;
		try
		{
			double num2 = Math.Max(25.0, num - 18.0);
			if (InnerRadiusSlider != null)
			{
				InnerRadiusSlider.Maximum = num2;
				if (InnerRadiusSlider.Value > num2)
				{
					InnerRadiusSlider.Value = num2;
					InnerRadiusLabel.Text = num2.ToString("0");
					ConfigManager.CurrentConfig.InnerRadius = num2;
				}
			}
			double num3 = Math.Max(20.0, InnerRadiusSlider?.Value ?? ConfigManager.CurrentConfig.InnerRadius);
			if (CoreRadiusSlider != null)
			{
				CoreRadiusSlider.Maximum = num3;
				if (CoreRadiusSlider.Value > num3)
				{
					CoreRadiusSlider.Value = num3;
					CoreRadiusLabel.Text = num3.ToString("0");
					ConfigManager.CurrentConfig.CoreRadius = num3;
				}
			}
		}
		finally
		{
			_isUpdatingUi = false;
		}
		Grid appearanceSettingsGrid = AppearanceSettingsGrid;
		if (appearanceSettingsGrid != null && appearanceSettingsGrid.Visibility == Visibility.Visible)
		{
			RenderLiveWheelPreview();
		}
		ScheduleAutoSave();
	}

	private void InnerRadiusSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
	{
		if (_isUpdatingUi || ConfigManager.CurrentConfig == null || InnerRadiusLabel == null)
		{
			return;
		}
		double num = Math.Round(e.NewValue);
		InnerRadiusLabel.Text = num.ToString("0");
		ConfigManager.CurrentConfig.InnerRadius = num;
		_isUpdatingUi = true;
		try
		{
			if (WheelRadiusSlider != null && num + 18.0 > WheelRadiusSlider.Value)
			{
				double num2 = Math.Min(WheelRadiusSlider.Maximum, num + 18.0);
				WheelRadiusSlider.Value = num2;
				WheelRadiusLabel.Text = num2.ToString("0");
				ConfigManager.CurrentConfig.WheelRadius = num2;
			}
			if (CoreRadiusSlider != null)
			{
				CoreRadiusSlider.Maximum = num;
				if (CoreRadiusSlider.Value > num)
				{
					CoreRadiusSlider.Value = num;
					CoreRadiusLabel.Text = num.ToString("0");
					ConfigManager.CurrentConfig.CoreRadius = num;
				}
			}
		}
		finally
		{
			_isUpdatingUi = false;
		}
		Grid appearanceSettingsGrid = AppearanceSettingsGrid;
		if (appearanceSettingsGrid != null && appearanceSettingsGrid.Visibility == Visibility.Visible)
		{
			RenderLiveWheelPreview();
		}
		ScheduleAutoSave();
	}

	private void CoreRadiusSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
	{
		if (_isUpdatingUi || ConfigManager.CurrentConfig == null || CoreRadiusLabel == null)
		{
			return;
		}
		double num = Math.Round(e.NewValue);
		CoreRadiusLabel.Text = num.ToString("0");
		ConfigManager.CurrentConfig.CoreRadius = num;
		_isUpdatingUi = true;
		try
		{
			if (InnerRadiusSlider != null && num > InnerRadiusSlider.Value)
			{
				double num2 = Math.Min(InnerRadiusSlider.Maximum, num);
				InnerRadiusSlider.Value = num2;
				InnerRadiusLabel.Text = num2.ToString("0");
				ConfigManager.CurrentConfig.InnerRadius = num2;
				if (WheelRadiusSlider != null && num2 + 18.0 > WheelRadiusSlider.Value)
				{
					double num3 = Math.Min(WheelRadiusSlider.Maximum, num2 + 18.0);
					WheelRadiusSlider.Value = num3;
					WheelRadiusLabel.Text = num3.ToString("0");
					ConfigManager.CurrentConfig.WheelRadius = num3;
				}
			}
		}
		finally
		{
			_isUpdatingUi = false;
		}
		Grid appearanceSettingsGrid = AppearanceSettingsGrid;
		if (appearanceSettingsGrid != null && appearanceSettingsGrid.Visibility == Visibility.Visible)
		{
			RenderLiveWheelPreview();
		}
		ScheduleAutoSave();
	}

	private void SectorGapSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
	{
		if (SectorGapLabel == null || ConfigManager.CurrentConfig == null)
		{
			return;
		}
		SectorGapLabel.Text = $"{e.NewValue:0} px";
		ConfigManager.CurrentConfig.SectorGap = e.NewValue;
		if (!_isUpdatingUi)
		{
			Grid appearanceSettingsGrid = AppearanceSettingsGrid;
			if (appearanceSettingsGrid != null && appearanceSettingsGrid.Visibility == Visibility.Visible)
			{
				RenderLiveWheelPreview();
			}
		}
		ScheduleAutoSave();
	}

	private void SectorCornerRadiusSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
	{
		if (SectorCornerRadiusLabel == null || ConfigManager.CurrentConfig == null)
		{
			return;
		}
		SectorCornerRadiusLabel.Text = $"{e.NewValue:0} px";
		ConfigManager.CurrentConfig.SectorCornerRadius = e.NewValue;
		if (!_isUpdatingUi)
		{
			Grid appearanceSettingsGrid = AppearanceSettingsGrid;
			if (appearanceSettingsGrid != null && appearanceSettingsGrid.Visibility == Visibility.Visible)
			{
				RenderLiveWheelPreview();
			}
		}
		ScheduleAutoSave();
	}

	private void ShapeComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
	{
		if (!_isUpdatingUi && ShapeComboBox != null && ConfigManager.CurrentConfig != null && ShapeComboBox.SelectedItem is ComboBoxItem comboBoxItem)
		{
			ConfigManager.CurrentConfig.Shape = comboBoxItem.Tag?.ToString() ?? "Original";
			Grid appearanceSettingsGrid = AppearanceSettingsGrid;
			if (appearanceSettingsGrid != null && appearanceSettingsGrid.Visibility == Visibility.Visible)
			{
				RenderLiveWheelPreview();
			}
			SyncUiToConfigAndSave();
		}
	}

	private void LayoutTargetRadio_Checked(object sender, RoutedEventArgs e)
	{
		if (_isUpdatingUi)
		{
			return;
		}
		if (LayoutTargetSlotRadio != null && LayoutTargetSlotRadio.IsChecked == true)
		{
			if (_selectedLayoutSlotIndex < 0)
			{
				_selectedLayoutSlotIndex = 0;
			}
		}
		else
		{
			_selectedLayoutSlotIndex = -1;
		}
		RefreshLayoutOptionsUi();
		RenderLiveWheelPreview();
	}

	private void PopulateLayoutModeComboBox(bool includeInherit)
	{
		if (IconLayoutModeComboBox == null)
		{
			return;
		}
		IconLayoutModeComboBox.Items.Clear();
		if (includeInherit)
		{
			IconLayoutModeComboBox.Items.Add(new ComboBoxItem
			{
				Content = "跟随全局默认 (Inherit Global)",
				Tag = "Inherit"
			});
		}
		IconLayoutModeComboBox.Items.Add(new ComboBoxItem
		{
			Content = "图标 + 文字 (双行居中)",
			Tag = "IconAndText"
		});
		IconLayoutModeComboBox.Items.Add(new ComboBoxItem
		{
			Content = "仅显示图标 (极大化居中)",
			Tag = "IconOnly"
		});
		IconLayoutModeComboBox.Items.Add(new ComboBoxItem
		{
			Content = "仅显示文字 (纯文字居中)",
			Tag = "TextOnly"
		});
	}

	private void RefreshLayoutOptionsUi()
	{
		if (ConfigManager.CurrentConfig == null)
		{
			return;
		}
		_isUpdatingUi = true;
		try
		{
			PopulateWheelFontFamilies();
			PopulateCoreFontFamilies();
			if (_selectedLayoutSlotIndex < 0)
			{
				if (LayoutTargetGlobalRadio != null)
				{
					LayoutTargetGlobalRadio.IsChecked = true;
				}
				if (SlotSelectionContainer != null)
				{
					SlotSelectionContainer.Visibility = Visibility.Collapsed;
				}
				PopulateLayoutModeComboBox(includeInherit: false);
				SetComboBoxSelectedValue(IconLayoutModeComboBox, ConfigManager.CurrentConfig.IconLayoutMode ?? "IconAndText");
				SetComboBoxSelectedValue(WheelFontFamilyComboBox, ConfigManager.CurrentConfig.WheelFontFamily ?? "Microsoft YaHei UI, Segoe UI");
				if (SectorTextColorTextBox != null)
				{
					SectorTextColorTextBox.Text = ConfigManager.CurrentConfig.CustomText ?? "#FFF8FAFC";
					UpdateColorPreviewBorder(SectorTextColorPreview, SectorTextColorTextBox.Text);
				}
				if (SectorIconSizeSlider != null)
				{
					double sz = ((ConfigManager.CurrentConfig.SectorIconSize > 0.0) ? ConfigManager.CurrentConfig.SectorIconSize : 20.0);
					SectorIconSizeSlider.Value = sz;
					if (SectorIconSizeLabel != null)
					{
						SectorIconSizeLabel.Text = $"{sz:0} px";
					}
				}
				if (SectorFontSizeSlider != null)
				{
					double fsz = ((ConfigManager.CurrentConfig.SectorFontSize > 0.0) ? ConfigManager.CurrentConfig.SectorFontSize : 11.0);
					SectorFontSizeSlider.Value = fsz;
					if (SectorFontSizeLabel != null)
					{
						SectorFontSizeLabel.Text = $"{fsz:0.0} px";
					}
				}
			}
			else
			{
				if (LayoutTargetSlotRadio != null)
				{
					LayoutTargetSlotRadio.IsChecked = true;
				}
				if (SlotSelectionContainer != null)
				{
					SlotSelectionContainer.Visibility = Visibility.Visible;
				}
				WheelProfile? profile = _selectedProfile ?? ConfigManager.CurrentConfig.Profiles.FirstOrDefault();
				ActionItem? action = GetCurrentEditingAction();

				if (CurrentTargetSlotLabel != null)
				{
					string tierName = (_selectedLayoutTier == 2) ? "二级级联轮盘" : "一级主轮盘";
					string slotDirName = GetDirectionDisplayName(_selectedLayoutSlotIndex, profile?.SectorCount ?? 8);
					string actName = action?.Name ?? "未设置动作";
					if (_selectedLayoutTier == 2 && _selectedLayoutSubSlotIndex >= 0)
					{
						CurrentTargetSlotLabel.Text = $"📍 正在定制: {tierName} [{slotDirName}] -> 子项 {_selectedLayoutSubSlotIndex + 1}: {actName}";
					}
					else
					{
						CurrentTargetSlotLabel.Text = $"📍 正在定制: {tierName} - 扇区 {_selectedLayoutSlotIndex + 1} [{slotDirName}]: {actName}";
					}
				}

				PopulateLayoutModeComboBox(includeInherit: true);
				string currentMode = ((action != null && !string.IsNullOrWhiteSpace(action.LayoutMode)) ? action.LayoutMode : "Inherit");
				SetComboBoxSelectedValue(IconLayoutModeComboBox, currentMode);
				string currentFont = ((action != null && !string.IsNullOrWhiteSpace(action.CustomFontFamily)) ? action.CustomFontFamily : (ConfigManager.CurrentConfig.WheelFontFamily ?? "Microsoft YaHei UI, Segoe UI"));
				SetComboBoxSelectedValue(WheelFontFamilyComboBox, currentFont);
				if (SectorTextColorTextBox != null)
				{
					SectorTextColorTextBox.Text = ((action != null && !string.IsNullOrWhiteSpace(action.CustomTextColor)) ? action.CustomTextColor : (ConfigManager.CurrentConfig.CustomText ?? "#FFF8FAFC"));
					UpdateColorPreviewBorder(SectorTextColorPreview, SectorTextColorTextBox.Text);
				}
				if (SectorIconSizeSlider != null)
				{
					double sz = ((action != null && action.CustomIconSize.HasValue && action.CustomIconSize.Value > 0.0) ? action.CustomIconSize.Value : ((ConfigManager.CurrentConfig.SectorIconSize > 0.0) ? ConfigManager.CurrentConfig.SectorIconSize : 20.0));
					SectorIconSizeSlider.Value = sz;
					if (SectorIconSizeLabel != null)
					{
						SectorIconSizeLabel.Text = $"{sz:0} px";
					}
				}
				if (SectorFontSizeSlider != null)
				{
					double fsz = ((action != null && action.CustomFontSize.HasValue && action.CustomFontSize.Value > 0.0) ? action.CustomFontSize.Value : ((ConfigManager.CurrentConfig.SectorFontSize > 0.0) ? ConfigManager.CurrentConfig.SectorFontSize : 11.0));
					SectorFontSizeSlider.Value = fsz;
					if (SectorFontSizeLabel != null)
					{
						SectorFontSizeLabel.Text = $"{fsz:0.0} px";
					}
				}
			}
		}
		finally
		{
			_isUpdatingUi = false;
		}
	}

	public void OnPreviewSectorClicked(int sectorIndex)
	{
		_selectedLayoutTier = 1;
		_selectedLayoutSlotIndex = sectorIndex;
		_selectedLayoutSubSlotIndex = -1;
		if (LayoutTargetSlotRadio != null)
		{
			LayoutTargetSlotRadio.IsChecked = true;
		}
		RefreshLayoutOptionsUi();
		RenderLiveWheelPreview();
		WheelProfile? profile = _selectedProfile ?? ConfigManager.CurrentConfig?.Profiles.FirstOrDefault();
		UpdatePreviewCoreSelection(sectorIndex, -1, profile);
	}

	public void OnPreviewSubSectorClicked(int parentIndex, int subIndex)
	{
		_selectedLayoutTier = 2;
		_selectedLayoutSlotIndex = parentIndex;
		_selectedLayoutSubSlotIndex = subIndex;
		if (LayoutTargetSlotRadio != null)
		{
			LayoutTargetSlotRadio.IsChecked = true;
		}
		RefreshLayoutOptionsUi();
		RenderLiveWheelPreview();
		WheelProfile? profile = _selectedProfile ?? ConfigManager.CurrentConfig?.Profiles.FirstOrDefault();
		UpdatePreviewCoreSelection(parentIndex, subIndex, profile);
	}

	private void ResetSlotLayoutButton_Click(object sender, RoutedEventArgs e)
	{
		ActionItem? action = GetCurrentEditingAction();
		if (action != null)
		{
			action.LayoutMode = "Inherit";
			action.CustomTextColor = null;
			action.CustomFontFamily = null;
			action.CustomIconSize = null;
			action.CustomFontSize = null;
			RefreshLayoutOptionsUi();
			RenderLiveWheelPreview();
			ScheduleAutoSave();
		}
	}

	private void IconLayoutModeComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
	{
		if (IconLayoutModeComboBox == null || ConfigManager.CurrentConfig == null || _isUpdatingUi || !(IconLayoutModeComboBox.SelectedItem is ComboBoxItem { Tag: var tag }))
		{
			return;
		}
		string text = tag?.ToString() ?? "IconAndText";
		if (_selectedLayoutSlotIndex < 0)
		{
			ConfigManager.CurrentConfig.IconLayoutMode = text;
			WheelProfile? profile = _selectedProfile ?? ConfigManager.CurrentConfig.Profiles?.FirstOrDefault();
			if (profile?.Actions != null)
			{
				foreach (var act in profile.Actions)
				{
					if (act != null)
					{
						act.LayoutMode = "Inherit";
						if (act.SubActions != null)
						{
							foreach (var subAct in act.SubActions)
							{
								if (subAct != null)
								{
									subAct.LayoutMode = "Inherit";
								}
							}
						}
					}
				}
			}
		}
		else
		{
			ActionItem? action = GetCurrentEditingAction();
			if (action != null)
			{
				action.LayoutMode = text;
			}
		}
		Grid appearanceSettingsGrid = AppearanceSettingsGrid;
		if (appearanceSettingsGrid != null && appearanceSettingsGrid.Visibility == Visibility.Visible)
		{
			RenderLiveWheelPreview();
		}
		ScheduleAutoSave();
	}

	private void PopulateWheelFontFamilies()
	{
		if (WheelFontFamilyComboBox == null)
		{
			return;
		}
		WheelFontFamilyComboBox.Items.Clear();
		List<(string, string)> obj = new List<(string, string)>
		{
			("\ud83d\udda5\ufe0f 系统默认 (Microsoft YaHei UI / Segoe UI)", "Microsoft YaHei UI, Segoe UI"),
			("\ud83d\udd24 微软雅黑 (Microsoft YaHei UI)", "Microsoft YaHei UI"),
			("\ud83d\udd24 Segoe UI (Windows Fluent)", "Segoe UI"),
			("\ud83d\udd24 鸿蒙字体 (HarmonyOS Sans SC)", "HarmonyOS Sans SC"),
			("\ud83d\udd24 苹方字体 (PingFang SC)", "PingFang SC"),
			("\ud83d\udd24 小米兰亭 (MiSans)", "MiSans"),
			("\ud83d\udd24 思源黑体 (Source Han Sans SC)", "Source Han Sans SC"),
			("\ud83d\udd24 Inter (Modern Sans)", "Inter"),
			("\ud83d\udd24 Arial", "Arial"),
			("\ud83d\udd24 黑体 (SimHei)", "SimHei"),
			("\ud83d\udd24 楷体 (KaiTi)", "KaiTi"),
			("\ud83d\udd24 仿宋 (FangSong)", "FangSong"),
			("\ud83d\udd24 等宽代码体 (Consolas / Cascadia)", "Consolas, Cascadia Code"),
			("\ud83d\udd24 JetBrains Mono", "JetBrains Mono, Consolas")
		};
		HashSet<string> hashSet = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
		foreach (var item2 in obj)
		{
			WheelFontFamilyComboBox.Items.Add(new ComboBoxItem
			{
				Content = item2.Item1,
				Tag = item2.Item2,
				FontFamily = new System.Windows.Media.FontFamily(item2.Item2)
			});
			hashSet.Add(item2.Item2);
			string item = item2.Item2.Split(',')[0].Trim();
			hashSet.Add(item);
		}
		WheelFontFamilyComboBox.Items.Add(new Separator());
		try
		{
			foreach (System.Windows.Media.FontFamily item3 in Fonts.SystemFontFamilies.OrderBy<System.Windows.Media.FontFamily, string>((System.Windows.Media.FontFamily f) => GetFontDisplayName(f), StringComparer.CurrentCultureIgnoreCase).ToList())
			{
				string source = item3.Source;
				if (!string.IsNullOrWhiteSpace(source) && !hashSet.Contains(source))
				{
					string fontDisplayName = GetFontDisplayName(item3);
					string content = (string.Equals(fontDisplayName, source, StringComparison.OrdinalIgnoreCase) ? ("\ud83d\udd24 " + fontDisplayName) : $"\ud83d\udd24 {fontDisplayName} ({source})");
					WheelFontFamilyComboBox.Items.Add(new ComboBoxItem
					{
						Content = content,
						Tag = source,
						FontFamily = item3
					});
					hashSet.Add(source);
				}
			}
		}
		catch
		{
		}
	}

	private void PopulateCoreFontFamilies()
	{
		if (CoreFontFamilyComboBox == null)
		{
			return;
		}
		CoreFontFamilyComboBox.Items.Clear();
		List<(string, string)> obj = new List<(string, string)>
		{
			("\ud83d\udda5\ufe0f 系统默认 (Microsoft YaHei UI / Segoe UI)", "Microsoft YaHei UI, Segoe UI"),
			("\ud83d\udd24 微软雅黑 (Microsoft YaHei UI)", "Microsoft YaHei UI"),
			("\ud83d\udd24 Segoe UI (Windows Fluent)", "Segoe UI"),
			("\ud83d\udd24 鸿蒙字体 (HarmonyOS Sans SC)", "HarmonyOS Sans SC"),
			("\ud83d\udd24 苹方字体 (PingFang SC)", "PingFang SC"),
			("\ud83d\udd24 小米兰亭 (MiSans)", "MiSans"),
			("\ud83d\udd24 思源黑体 (Source Han Sans SC)", "Source Han Sans SC"),
			("\ud83d\udd24 Inter (Modern Sans)", "Inter"),
			("\ud83d\udd24 Arial", "Arial"),
			("\ud83d\udd24 黑体 (SimHei)", "SimHei"),
			("\ud83d\udd24 楷体 (KaiTi)", "KaiTi"),
			("\ud83d\udd24 仿宋 (FangSong)", "FangSong"),
			("\ud83d\udd24 等宽代码体 (Consolas / Cascadia)", "Consolas, Cascadia Code"),
			("\ud83d\udd24 JetBrains Mono", "JetBrains Mono, Consolas")
		};
		HashSet<string> hashSet = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
		foreach (var item2 in obj)
		{
			CoreFontFamilyComboBox.Items.Add(new ComboBoxItem
			{
				Content = item2.Item1,
				Tag = item2.Item2,
				FontFamily = new System.Windows.Media.FontFamily(item2.Item2)
			});
			hashSet.Add(item2.Item2);
			string item = item2.Item2.Split(',')[0].Trim();
			hashSet.Add(item);
		}
		CoreFontFamilyComboBox.Items.Add(new Separator());
		try
		{
			foreach (System.Windows.Media.FontFamily item3 in Fonts.SystemFontFamilies.OrderBy<System.Windows.Media.FontFamily, string>((System.Windows.Media.FontFamily f) => GetFontDisplayName(f), StringComparer.CurrentCultureIgnoreCase).ToList())
			{
				string source = item3.Source;
				if (!string.IsNullOrWhiteSpace(source) && !hashSet.Contains(source))
				{
					string fontDisplayName = GetFontDisplayName(item3);
					string content = (string.Equals(fontDisplayName, source, StringComparison.OrdinalIgnoreCase) ? ("\ud83d\udd24 " + fontDisplayName) : $"\ud83d\udd24 {fontDisplayName} ({source})");
					CoreFontFamilyComboBox.Items.Add(new ComboBoxItem
					{
						Content = content,
						Tag = source,
						FontFamily = item3
					});
					hashSet.Add(source);
				}
			}
		}
		catch
		{
		}
	}

	private static string GetFontDisplayName(System.Windows.Media.FontFamily font)
	{
		try
		{
			XmlLanguage language = XmlLanguage.GetLanguage(CultureInfo.CurrentUICulture.IetfLanguageTag);
			if (font.FamilyNames.ContainsKey(language))
			{
				return font.FamilyNames[language];
			}
			XmlLanguage language2 = XmlLanguage.GetLanguage("en-US");
			if (font.FamilyNames.ContainsKey(language2))
			{
				return font.FamilyNames[language2];
			}
			return font.FamilyNames.Values.FirstOrDefault() ?? font.Source;
		}
		catch
		{
			return font.Source;
		}
	}

	private void WheelFontFamilyComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
	{
		if (_isUpdatingUi || WheelFontFamilyComboBox == null || ConfigManager.CurrentConfig == null || WheelFontFamilyComboBox.SelectedItem is not ComboBoxItem { Tag: var tag })
		{
			return;
		}
		string wheelFontFamily = tag?.ToString() ?? "Microsoft YaHei UI, Segoe UI";
		if (_selectedLayoutSlotIndex < 0)
		{
			ConfigManager.CurrentConfig.WheelFontFamily = wheelFontFamily;
		}
		else
		{
			ActionItem? action = GetCurrentEditingAction();
			if (action != null)
			{
				action.CustomFontFamily = wheelFontFamily;
			}
		}
		Grid appearanceSettingsGrid = AppearanceSettingsGrid;
		if (appearanceSettingsGrid != null && appearanceSettingsGrid.Visibility == Visibility.Visible)
		{
			RenderLiveWheelPreview();
		}
		ScheduleAutoSave();
	}

	private void CoreFontFamilyComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
	{
		if (_isUpdatingUi || CoreFontFamilyComboBox == null || ConfigManager.CurrentConfig == null || CoreFontFamilyComboBox.SelectedItem is not ComboBoxItem { Tag: var tag })
		{
			return;
		}
		string coreFontFamily = tag?.ToString() ?? "Microsoft YaHei UI, Segoe UI";
		ConfigManager.CurrentConfig.CoreFontFamily = coreFontFamily;
		Grid appearanceSettingsGrid = AppearanceSettingsGrid;
		if (appearanceSettingsGrid != null && appearanceSettingsGrid.Visibility == Visibility.Visible)
		{
			RenderLiveWheelPreview();
		}
		SyncUiToConfigAndSave();
	}

	private void SectorTextColorTextBox_TextChanged(object sender, TextChangedEventArgs e)
	{
		if (SectorTextColorTextBox == null || ConfigManager.CurrentConfig == null)
		{
			return;
		}
		string hex = SectorTextColorTextBox.Text.Trim();
		UpdateColorPreviewBorder(SectorTextColorPreview, hex);
		if (_isUpdatingUi)
		{
			return;
		}
		if (_selectedLayoutSlotIndex < 0)
		{
			ConfigManager.CurrentConfig.CustomText = hex;
			if (CustomTextTextBox != null && CustomTextTextBox.Text != hex)
			{
				CustomTextTextBox.Text = hex;
			}
		}
		else
		{
			ActionItem? action = GetCurrentEditingAction();
			if (action != null)
			{
				action.CustomTextColor = hex;
			}
		}
		Grid appearanceSettingsGrid = AppearanceSettingsGrid;
		if (appearanceSettingsGrid != null && appearanceSettingsGrid.Visibility == Visibility.Visible)
		{
			RenderLiveWheelPreview();
		}
		ScheduleAutoSave();
	}

	private void CoreTextColorTextBox_TextChanged(object sender, TextChangedEventArgs e)
	{
		if (CoreTextColorTextBox == null || ConfigManager.CurrentConfig == null)
		{
			return;
		}
		string hex = CoreTextColorTextBox.Text.Trim();
		UpdateColorPreviewBorder(CoreTextColorPreview, hex);
		if (_isUpdatingUi)
		{
			return;
		}
		ConfigManager.CurrentConfig.CoreTextColor = hex;
		Grid appearanceSettingsGrid = AppearanceSettingsGrid;
		if (appearanceSettingsGrid != null && appearanceSettingsGrid.Visibility == Visibility.Visible)
		{
			RenderLiveWheelPreview();
		}
		ScheduleAutoSave();
	}

	private void CoreFontSizeSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
	{
		if (CoreFontSizeSlider != null && CoreFontSizeLabel != null && ConfigManager.CurrentConfig != null && !_isUpdatingUi)
		{
			ConfigManager.CurrentConfig.CoreFontSize = e.NewValue;
			CoreFontSizeLabel.Text = $"{e.NewValue:0.0} px";
			Grid appearanceSettingsGrid = AppearanceSettingsGrid;
			if (appearanceSettingsGrid != null && appearanceSettingsGrid.Visibility == Visibility.Visible)
			{
				RenderLiveWheelPreview();
			}
			ScheduleAutoSave();
		}
	}

	private void ShowSelectedActionTextCheckBox_Changed(object sender, RoutedEventArgs e)
	{
		if (ShowSelectedActionTextCheckBox == null || ConfigManager.CurrentConfig == null || _isUpdatingUi)
		{
			return;
		}

		ConfigManager.CurrentConfig.ShowSelectedActionText = ShowSelectedActionTextCheckBox.IsChecked == true;
		if (AppearanceSettingsGrid != null && AppearanceSettingsGrid.Visibility == Visibility.Visible)
		{
			RenderLiveWheelPreview();
		}
		SyncUiToConfigAndSave();
	}

	private void SectorIconSizeSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
	{
		if (SectorIconSizeSlider != null && SectorIconSizeLabel != null && ConfigManager.CurrentConfig != null && !_isUpdatingUi)
		{
			double val = e.NewValue;
			SectorIconSizeLabel.Text = $"{val:0} px";
			if (_selectedLayoutSlotIndex < 0)
			{
				ConfigManager.CurrentConfig.SectorIconSize = val;
			}
			else
			{
				ActionItem? action = GetCurrentEditingAction();
				if (action != null)
				{
					action.CustomIconSize = val;
				}
			}
			Grid appearanceSettingsGrid = AppearanceSettingsGrid;
			if (appearanceSettingsGrid != null && appearanceSettingsGrid.Visibility == Visibility.Visible)
			{
				RenderLiveWheelPreview();
			}
			ScheduleAutoSave();
		}
	}

	private void SectorFontSizeSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
	{
		if (SectorFontSizeSlider != null && SectorFontSizeLabel != null && ConfigManager.CurrentConfig != null && !_isUpdatingUi)
		{
			double val = e.NewValue;
			SectorFontSizeLabel.Text = $"{val:0.0} px";
			if (_selectedLayoutSlotIndex < 0)
			{
				ConfigManager.CurrentConfig.SectorFontSize = val;
			}
			else
			{
				ActionItem? action = GetCurrentEditingAction();
				if (action != null)
				{
					action.CustomFontSize = val;
				}
			}
			Grid appearanceSettingsGrid = AppearanceSettingsGrid;
			if (appearanceSettingsGrid != null && appearanceSettingsGrid.Visibility == Visibility.Visible)
			{
				RenderLiveWheelPreview();
			}
			ScheduleAutoSave();
		}
	}

	private void ResetDimensionsButton_Click(object sender, RoutedEventArgs e)
	{
		_isUpdatingUi = true;
		try
		{
			WheelRadiusSlider.Value = 138.0;
			WheelRadiusLabel.Text = "138";
			InnerRadiusSlider.Value = 52.0;
			InnerRadiusLabel.Text = "52";
			CoreRadiusSlider.Value = 50.0;
			CoreRadiusLabel.Text = "50";
			SectorGapSlider.Value = 2.0;
			SectorGapLabel.Text = "2 px";
			SectorCornerRadiusSlider.Value = 4.0;
			SectorCornerRadiusLabel.Text = "4 px";
			SectorIconSizeSlider.Value = 20.0;
			SectorIconSizeLabel.Text = "20 px";
			SectorFontSizeSlider.Value = 10.5;
			SectorFontSizeLabel.Text = "10.5 px";
			ConfigManager.CurrentConfig.WheelRadius = 138.0;
			ConfigManager.CurrentConfig.InnerRadius = 52.0;
			ConfigManager.CurrentConfig.CoreRadius = 50.0;
			ConfigManager.CurrentConfig.SectorGap = 2.0;
			ConfigManager.CurrentConfig.SectorCornerRadius = 4.0;
			ConfigManager.CurrentConfig.SectorIconSize = 20.0;
			ConfigManager.CurrentConfig.SectorFontSize = 10.5;
		}
		finally
		{
			_isUpdatingUi = false;
		}
		RenderLiveWheelPreview();
		SyncUiToConfigAndSave();
	}

	private void TierDimensionRadio_Checked(object sender, RoutedEventArgs e)
	{
		if (_isUpdatingUi)
		{
			return;
		}
		bool flag = sender == Tier2ConfigSegmentRadio || (sender == null && ((Tier2ConfigSegmentRadio?.IsChecked == true)));
		_selectedLayoutTier = flag ? 2 : 1;
		if (!flag)
		{
			_selectedLayoutSubSlotIndex = -1;
		}
		else
		{
			WheelProfile? profile = _selectedProfile ?? ConfigManager.CurrentConfig?.Profiles.FirstOrDefault();
			if (profile?.Actions != null)
			{
				bool currentHasSub = (_selectedLayoutSlotIndex >= 0 && _selectedLayoutSlotIndex < profile.Actions.Count && profile.Actions[_selectedLayoutSlotIndex]?.SubActions?.Count > 0);
				if (!currentHasSub)
				{
					for (int k = 0; k < profile.Actions.Count; k++)
					{
						if (profile.Actions[k]?.SubActions?.Count > 0)
						{
							_selectedLayoutSlotIndex = k;
							_selectedLayoutSubSlotIndex = 0;
							currentHasSub = true;
							break;
						}
					}
				}
				if (currentHasSub)
				{
					if (_selectedLayoutSubSlotIndex < 0 || _selectedLayoutSubSlotIndex >= (profile.Actions[_selectedLayoutSlotIndex]?.SubActions?.Count ?? 0))
					{
						_selectedLayoutSubSlotIndex = 0;
					}
				}
			}
		}
		_isUpdatingUi = true;
		try
		{
			if (Tier1ConfigSegmentRadio != null)
			{
				Tier1ConfigSegmentRadio.IsChecked = !flag;
			}
			if (Tier2ConfigSegmentRadio != null)
			{
				Tier2ConfigSegmentRadio.IsChecked = flag;
			}
			if (Tier1DimensionsPanel != null)
			{
				Tier1DimensionsPanel.Visibility = (flag ? Visibility.Collapsed : Visibility.Visible);
			}
			if (Tier2DimensionsPanel != null)
			{
				Tier2DimensionsPanel.Visibility = ((!flag) ? Visibility.Collapsed : Visibility.Visible);
			}
			if (Tier1ThemePanel != null)
			{
				Tier1ThemePanel.Visibility = (flag ? Visibility.Collapsed : Visibility.Visible);
			}
			if (Tier2ThemePanel != null)
			{
				Tier2ThemePanel.Visibility = ((!flag) ? Visibility.Collapsed : Visibility.Visible);
			}
			if (VisualThemeCardTitleText != null)
			{
				VisualThemeCardTitleText.Text = (flag ? "轮盘视觉风格与色彩配置 (二级级联轮盘)" : "轮盘视觉风格与色彩配置 (一级主轮盘)");
			}
			if (DimensionsCardTitleText != null)
			{
				DimensionsCardTitleText.Text = (flag ? "几何形态与尺寸微调 (二级级联轮盘)" : "几何形态与尺寸微调 (一级主轮盘)");
			}
		}
		finally
		{
			_isUpdatingUi = false;
		}
		RefreshLayoutOptionsUi();
		Grid appearanceSettingsGrid = AppearanceSettingsGrid;
		if (appearanceSettingsGrid != null && appearanceSettingsGrid.Visibility == Visibility.Visible)
		{
			RenderLiveWheelPreview();
		}
	}

	private void SubWheelOuterRadiusSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
	{
		if (SubWheelOuterRadiusLabel != null && ConfigManager.CurrentConfig != null && !_isUpdatingUi)
		{
			double num = Math.Round(e.NewValue);
			ConfigManager.CurrentConfig.SubWheelOuterRadius = num;
			SubWheelOuterRadiusLabel.Text = $"{num:0} px";
			Grid appearanceSettingsGrid = AppearanceSettingsGrid;
			if (appearanceSettingsGrid != null && appearanceSettingsGrid.Visibility == Visibility.Visible)
			{
				RenderLiveWheelPreview();
			}
			ScheduleAutoSave();
		}
	}

	private void SubWheelInnerGapSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
	{
		if (SubWheelInnerGapLabel != null && ConfigManager.CurrentConfig != null && !_isUpdatingUi)
		{
			double num = Math.Round(e.NewValue);
			ConfigManager.CurrentConfig.SubWheelInnerGap = num;
			SubWheelInnerGapLabel.Text = $"{num:0} px";
			Grid appearanceSettingsGrid = AppearanceSettingsGrid;
			if (appearanceSettingsGrid != null && appearanceSettingsGrid.Visibility == Visibility.Visible)
			{
				RenderLiveWheelPreview();
			}
			ScheduleAutoSave();
		}
	}

	private void SubWheelCornerRadiusSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
	{
		if (SubWheelCornerRadiusLabel != null && ConfigManager.CurrentConfig != null && !_isUpdatingUi)
		{
			double num = Math.Round(e.NewValue);
			ConfigManager.CurrentConfig.SubWheelCornerRadius = num;
			SubWheelCornerRadiusLabel.Text = $"{num:0} px";
			Grid appearanceSettingsGrid = AppearanceSettingsGrid;
			if (appearanceSettingsGrid != null && appearanceSettingsGrid.Visibility == Visibility.Visible)
			{
				RenderLiveWheelPreview();
			}
			ScheduleAutoSave();
		}
	}

	private void SubWheelIconSizeSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
	{
		if (SubWheelIconSizeLabel != null && ConfigManager.CurrentConfig != null && !_isUpdatingUi)
		{
			double num = Math.Round(e.NewValue);
			ConfigManager.CurrentConfig.SubWheelIconSize = num;
			SubWheelIconSizeLabel.Text = $"{num:0} px";
			Grid appearanceSettingsGrid = AppearanceSettingsGrid;
			if (appearanceSettingsGrid != null && appearanceSettingsGrid.Visibility == Visibility.Visible)
			{
				RenderLiveWheelPreview();
			}
			ScheduleAutoSave();
		}
	}

	private void SubWheelFontSizeSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
	{
		if (SubWheelFontSizeLabel != null && ConfigManager.CurrentConfig != null && !_isUpdatingUi)
		{
			ConfigManager.CurrentConfig.SubWheelFontSize = e.NewValue;
			SubWheelFontSizeLabel.Text = $"{e.NewValue:0.0} px";
			Grid appearanceSettingsGrid = AppearanceSettingsGrid;
			if (appearanceSettingsGrid != null && appearanceSettingsGrid.Visibility == Visibility.Visible)
			{
				RenderLiveWheelPreview();
			}
			ScheduleAutoSave();
		}
	}

	private void ResetSubDimensionsButton_Click(object sender, RoutedEventArgs e)
	{
		_isUpdatingUi = true;
		try
		{
			if (SubWheelOuterRadiusSlider != null)
			{
				SubWheelOuterRadiusSlider.Value = 210.0;
				SubWheelOuterRadiusLabel.Text = "210 px";
			}
			if (SubWheelInnerGapSlider != null)
			{
				SubWheelInnerGapSlider.Value = 4.0;
				SubWheelInnerGapLabel.Text = "4 px";
			}
			if (SubWheelCornerRadiusSlider != null)
			{
				SubWheelCornerRadiusSlider.Value = 4.0;
				SubWheelCornerRadiusLabel.Text = "4 px";
			}
			if (SubWheelIconSizeSlider != null)
			{
				SubWheelIconSizeSlider.Value = 18.0;
				SubWheelIconSizeLabel.Text = "18 px";
			}
			if (SubWheelFontSizeSlider != null)
			{
				SubWheelFontSizeSlider.Value = 9.5;
				SubWheelFontSizeLabel.Text = "9.5 px";
			}
			ConfigManager.CurrentConfig.SubWheelOuterRadius = 210.0;
			ConfigManager.CurrentConfig.SubWheelInnerGap = 4.0;
			ConfigManager.CurrentConfig.SubWheelCornerRadius = 4.0;
			ConfigManager.CurrentConfig.SubWheelIconSize = 18.0;
			ConfigManager.CurrentConfig.SubWheelFontSize = 9.5;
		}
		finally
		{
			_isUpdatingUi = false;
		}
		RenderLiveWheelPreview();
		SyncUiToConfigAndSave();
	}

	private void SubWheelTriggerDistanceSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
	{
		if (SubWheelTriggerDistanceValueText != null && ConfigManager.CurrentConfig != null && !_isUpdatingUi)
		{
			double num = Math.Round(e.NewValue);
			ConfigManager.CurrentConfig.SubWheelTriggerDistance = num;
			SubWheelTriggerDistanceValueText.Text = $"{num:0} px";
			ScheduleAutoSave();
		}
	}

	private void ShowCoreIconCheckBox_Changed(object sender, RoutedEventArgs e)
	{
		if (!_isUpdatingUi && ConfigManager.CurrentConfig != null)
		{
			ConfigManager.CurrentConfig.ShowCoreIcon = ShowCoreIconCheckBox.IsChecked == true;
			Grid appearanceSettingsGrid = AppearanceSettingsGrid;
			if (appearanceSettingsGrid != null && appearanceSettingsGrid.Visibility == Visibility.Visible)
			{
				RenderLiveWheelPreview();
			}
			SyncUiToConfigAndSave();
		}
	}

	private void CoreIconTypeComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
	{
		if (!_isUpdatingUi && ConfigManager.CurrentConfig != null && CoreIconTypeComboBox.SelectedItem is ComboBoxItem { Tag: var tag })
		{
			string coreIconType = tag?.ToString() ?? "Exit";
			ConfigManager.CurrentConfig.CoreIconType = coreIconType;
			UpdateCoreIconPreviewUI();
			Grid appearanceSettingsGrid = AppearanceSettingsGrid;
			if (appearanceSettingsGrid != null && appearanceSettingsGrid.Visibility == Visibility.Visible)
			{
				RenderLiveWheelPreview();
			}
			SyncUiToConfigAndSave();
		}
	}

	private void PickCoreIconButton_Click(object sender, RoutedEventArgs e)
	{
		IconPickerWindow iconPickerWindow = new IconPickerWindow(ConfigManager.CurrentConfig.CoreCustomIconKey);
		iconPickerWindow.Owner = this;
		if (iconPickerWindow.ShowDialog() == true)
		{
			ConfigManager.CurrentConfig.CoreCustomIconKey = iconPickerWindow.SelectedIconKey ?? "";
			UpdateCoreIconPreviewUI();
			Grid appearanceSettingsGrid = AppearanceSettingsGrid;
			if (appearanceSettingsGrid != null && appearanceSettingsGrid.Visibility == Visibility.Visible)
			{
				RenderLiveWheelPreview();
			}
			SyncUiToConfigAndSave();
		}
	}

	private void UpdateCoreIconPreviewUI()
	{
		if (ConfigManager.CurrentConfig == null)
		{
			return;
		}
		string text = ConfigManager.CurrentConfig.CoreIconType ?? "Exit";
		if (CustomCoreIconPanel != null)
		{
			CustomCoreIconPanel.Visibility = ((!(text == "Custom")) ? Visibility.Collapsed : Visibility.Visible);
		}
		if (CustomCoreImagePanel != null)
		{
			CustomCoreImagePanel.Visibility = ((!(text == "Image")) ? Visibility.Collapsed : Visibility.Visible);
			if (text == "Image")
			{
				UpdateCoreImageThumbnail(ConfigManager.CurrentConfig.CoreCustomImagePath);
			}
		}
		if (CustomCoreIconPreviewPath != null && CustomCoreIconNameLabel != null)
		{
			Geometry coreIconGeometry = IconHelper.GetCoreIconGeometry(text, ConfigManager.CurrentConfig.CoreCustomIconKey, ConfigManager.CurrentConfig.CoreCustomIconSvg);
			CustomCoreIconPreviewPath.Data = coreIconGeometry;
			if (!string.IsNullOrEmpty(ConfigManager.CurrentConfig.CoreCustomIconKey))
			{
				CustomCoreIconNameLabel.Text = ConfigManager.CurrentConfig.CoreCustomIconKey;
			}
			else if (!string.IsNullOrEmpty(ConfigManager.CurrentConfig.CoreCustomIconSvg))
			{
				CustomCoreIconNameLabel.Text = "自定义 SVG 图标";
			}
			else
			{
				CustomCoreIconNameLabel.Text = "默认五角星 (点击更换)";
			}
		}
	}

	private void UpdateCoreImageThumbnail(string? imagePath)
	{
		if (CoreImageThumbnail == null)
		{
			return;
		}
		if (!string.IsNullOrEmpty(imagePath) && File.Exists(imagePath))
		{
			try
			{
				BitmapImage bitmapImage = new BitmapImage();
				bitmapImage.BeginInit();
				bitmapImage.UriSource = new Uri(imagePath, UriKind.Absolute);
				bitmapImage.CacheOption = BitmapCacheOption.OnLoad;
				bitmapImage.EndInit();
				CoreImageThumbnail.Source = bitmapImage;
				return;
			}
			catch
			{
				CoreImageThumbnail.Source = null;
				return;
			}
		}
		CoreImageThumbnail.Source = null;
	}

	private void CoreImagePathTextBox_TextChanged(object sender, TextChangedEventArgs e)
	{
		if (_isUpdatingUi || ConfigManager.CurrentConfig == null || CoreImagePathTextBox == null)
		{
			return;
		}
		string text = CoreImagePathTextBox.Text.Trim();
		ConfigManager.CurrentConfig.CoreCustomImagePath = text;
		if (!string.IsNullOrEmpty(text))
		{
			ConfigManager.CurrentConfig.CoreIconType = "Image";
			ConfigManager.CurrentConfig.ShowCoreIcon = true;
			SetComboBoxSelectedValue(CoreIconTypeComboBox, "Image");
			if (ShowCoreIconCheckBox != null)
			{
				ShowCoreIconCheckBox.IsChecked = true;
			}
		}
		UpdateCoreIconPreviewUI();
		UpdateCoreImageThumbnail(ConfigManager.CurrentConfig.CoreCustomImagePath);
		Grid appearanceSettingsGrid = AppearanceSettingsGrid;
		if (appearanceSettingsGrid != null && appearanceSettingsGrid.Visibility == Visibility.Visible)
		{
			RenderLiveWheelPreview();
		}
		ScheduleAutoSave();
	}

	private void BrowseCoreImageButton_Click(object sender, RoutedEventArgs e)
	{
		Microsoft.Win32.OpenFileDialog openFileDialog = new Microsoft.Win32.OpenFileDialog
		{
			Title = "选择中心核圆图案图片",
			Filter = "图片文件 (*.png;*.jpg;*.jpeg;*.bmp;*.webp;*.ico;*.gif)|*.png;*.jpg;*.jpeg;*.bmp;*.webp;*.ico;*.gif|所有文件 (*.*)|*.*",
			CheckFileExists = true
		};
		if (openFileDialog.ShowDialog() == true)
		{
			string fileName = openFileDialog.FileName;
			if (CoreImagePathTextBox != null)
			{
				CoreImagePathTextBox.Text = fileName;
			}
			ConfigManager.CurrentConfig.CoreCustomImagePath = fileName;
			ConfigManager.CurrentConfig.CoreIconType = "Image";
			ConfigManager.CurrentConfig.ShowCoreIcon = true;
			SetComboBoxSelectedValue(CoreIconTypeComboBox, "Image");
			if (ShowCoreIconCheckBox != null)
			{
				ShowCoreIconCheckBox.IsChecked = true;
			}
			UpdateCoreIconPreviewUI();
			UpdateCoreImageThumbnail(fileName);
			Grid appearanceSettingsGrid = AppearanceSettingsGrid;
			if (appearanceSettingsGrid != null && appearanceSettingsGrid.Visibility == Visibility.Visible)
			{
				RenderLiveWheelPreview();
			}
			SyncUiToConfigAndSave();
		}
	}

	private void ClearCoreImageButton_Click(object sender, RoutedEventArgs e)
	{
		if (CoreImagePathTextBox != null)
		{
			CoreImagePathTextBox.Text = "";
		}
		ConfigManager.CurrentConfig.CoreCustomImagePath = "";
		UpdateCoreImageThumbnail("");
		Grid appearanceSettingsGrid = AppearanceSettingsGrid;
		if (appearanceSettingsGrid != null && appearanceSettingsGrid.Visibility == Visibility.Visible)
		{
			RenderLiveWheelPreview();
		}
		SyncUiToConfigAndSave();
	}

	private void CoreIconScaleSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
	{
		if (!_isUpdatingUi && ConfigManager.CurrentConfig != null)
		{
			if (CoreIconScaleLabel != null && CoreIconScaleSlider != null)
			{
				CoreIconScaleLabel.Text = $"{Math.Round(CoreIconScaleSlider.Value * 100.0)}%";
			}
			if (CoreIconScaleSlider != null)
			{
				ConfigManager.CurrentConfig.CoreIconScale = CoreIconScaleSlider.Value;
			}
			Grid appearanceSettingsGrid = AppearanceSettingsGrid;
			if (appearanceSettingsGrid != null && appearanceSettingsGrid.Visibility == Visibility.Visible)
			{
				RenderLiveWheelPreview();
			}
			SyncUiToConfigAndSave();
		}
	}

	private void CoreImageOffsetSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
	{
		if (!_isUpdatingUi && ConfigManager.CurrentConfig != null)
		{
			if (CoreImageOffsetXLabel != null && CoreImageOffsetXSlider != null)
			{
				CoreImageOffsetXLabel.Text = $"{(int)CoreImageOffsetXSlider.Value} px";
			}
			if (CoreImageOffsetYLabel != null && CoreImageOffsetYSlider != null)
			{
				CoreImageOffsetYLabel.Text = $"{(int)CoreImageOffsetYSlider.Value} px";
			}
			if (CoreImageOffsetXSlider != null)
			{
				ConfigManager.CurrentConfig.CoreImageOffsetX = CoreImageOffsetXSlider.Value;
			}
			if (CoreImageOffsetYSlider != null)
			{
				ConfigManager.CurrentConfig.CoreImageOffsetY = CoreImageOffsetYSlider.Value;
			}
			Grid appearanceSettingsGrid = AppearanceSettingsGrid;
			if (appearanceSettingsGrid != null && appearanceSettingsGrid.Visibility == Visibility.Visible)
			{
				RenderLiveWheelPreview();
			}
			SyncUiToConfigAndSave();
		}
	}

	private void ResetCoreTransformButton_Click(object sender, RoutedEventArgs e)
	{
		if (ConfigManager.CurrentConfig != null)
		{
			_isUpdatingUi = true;
			if (CoreIconScaleSlider != null)
			{
				CoreIconScaleSlider.Value = 1.0;
			}
			if (CoreIconScaleLabel != null)
			{
				CoreIconScaleLabel.Text = "100%";
			}
			if (CoreImageOffsetXSlider != null)
			{
				CoreImageOffsetXSlider.Value = 0.0;
			}
			if (CoreImageOffsetXLabel != null)
			{
				CoreImageOffsetXLabel.Text = "0 px";
			}
			if (CoreImageOffsetYSlider != null)
			{
				CoreImageOffsetYSlider.Value = 0.0;
			}
			if (CoreImageOffsetYLabel != null)
			{
				CoreImageOffsetYLabel.Text = "0 px";
			}
			ConfigManager.CurrentConfig.CoreIconScale = 1.0;
			ConfigManager.CurrentConfig.CoreImageOffsetX = 0.0;
			ConfigManager.CurrentConfig.CoreImageOffsetY = 0.0;
			_isUpdatingUi = false;
			Grid appearanceSettingsGrid = AppearanceSettingsGrid;
			if (appearanceSettingsGrid != null && appearanceSettingsGrid.Visibility == Visibility.Visible)
			{
				RenderLiveWheelPreview();
			}
			SyncUiToConfigAndSave();
		}
	}

	private void HighlightGlowPresetComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
	{
		if (!_isUpdatingUi && HighlightGlowPresetComboBox != null && ConfigManager.CurrentConfig != null && HighlightGlowPresetComboBox.SelectedItem is ComboBoxItem { Tag: var tag })
		{
			string text = tag?.ToString() ?? "Auto";
			ConfigManager.CurrentConfig.HighlightGlowPreset = text;
			switch (text)
			{
			case "Lilac":
				ConfigManager.CurrentConfig.HighlightGlowColor = "#A855F7";
				HighlightGlowColorTextBox.Text = "#A855F7";
				break;
			case "Blue":
				ConfigManager.CurrentConfig.HighlightGlowColor = "#3B82F6";
				HighlightGlowColorTextBox.Text = "#3B82F6";
				break;
			case "Emerald":
				ConfigManager.CurrentConfig.HighlightGlowColor = "#10B981";
				HighlightGlowColorTextBox.Text = "#10B981";
				break;
			case "Rose":
				ConfigManager.CurrentConfig.HighlightGlowColor = "#EC4899";
				HighlightGlowColorTextBox.Text = "#EC4899";
				break;
			case "Amber":
				ConfigManager.CurrentConfig.HighlightGlowColor = "#F59E0B";
				HighlightGlowColorTextBox.Text = "#F59E0B";
				break;
			case "Red":
				ConfigManager.CurrentConfig.HighlightGlowColor = "#EF4444";
				HighlightGlowColorTextBox.Text = "#EF4444";
				break;
			case "White":
				ConfigManager.CurrentConfig.HighlightGlowColor = "#FFFFFF";
				HighlightGlowColorTextBox.Text = "#FFFFFF";
				break;
			case "Auto":
				ConfigManager.CurrentConfig.HighlightGlowColor = "";
				HighlightGlowColorTextBox.Text = "";
				break;
			}
			if (CustomHighlightGlowPanel != null)
			{
				CustomHighlightGlowPanel.Visibility = ((!(text == "Custom") && string.IsNullOrEmpty(HighlightGlowColorTextBox.Text)) ? Visibility.Collapsed : Visibility.Visible);
			}
			UpdateColorPreviews();
			Grid appearanceSettingsGrid = AppearanceSettingsGrid;
			if (appearanceSettingsGrid != null && appearanceSettingsGrid.Visibility == Visibility.Visible)
			{
				RenderLiveWheelPreview();
			}
			SyncUiToConfigAndSave();
		}
	}

	private void HighlightGlowColorTextBox_TextChanged(object sender, TextChangedEventArgs e)
	{
		if (!_isUpdatingUi && ConfigManager.CurrentConfig != null && HighlightGlowColorTextBox != null)
		{
			ConfigManager.CurrentConfig.HighlightGlowColor = HighlightGlowColorTextBox.Text.Trim();
			UpdateColorPreviews();
			Grid appearanceSettingsGrid = AppearanceSettingsGrid;
			if (appearanceSettingsGrid != null && appearanceSettingsGrid.Visibility == Visibility.Visible)
			{
				RenderLiveWheelPreview();
			}
			ScheduleAutoSave();
		}
	}

	private void HighlightGlowRadiusSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
	{
		if (!_isUpdatingUi && HighlightGlowRadiusLabel != null && ConfigManager.CurrentConfig != null)
		{
			HighlightGlowRadiusLabel.Text = $"{e.NewValue:0} px";
			ConfigManager.CurrentConfig.HighlightGlowRadius = e.NewValue;
			Grid appearanceSettingsGrid = AppearanceSettingsGrid;
			if (appearanceSettingsGrid != null && appearanceSettingsGrid.Visibility == Visibility.Visible)
			{
				RenderLiveWheelPreview();
			}
			ScheduleAutoSave();
		}
	}

	private void HighlightGlowOpacitySlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
	{
		if (!_isUpdatingUi && HighlightGlowOpacityLabel != null && ConfigManager.CurrentConfig != null)
		{
			HighlightGlowOpacityLabel.Text = $"{e.NewValue:0}%";
			ConfigManager.CurrentConfig.HighlightGlowOpacity = e.NewValue / 100.0;
			Grid appearanceSettingsGrid = AppearanceSettingsGrid;
			if (appearanceSettingsGrid != null && appearanceSettingsGrid.Visibility == Visibility.Visible)
			{
				RenderLiveWheelPreview();
			}
			ScheduleAutoSave();
		}
	}

	private void SubHighlightGlowPresetComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
	{
		if (_isUpdatingUi || SubHighlightGlowPresetComboBox == null || ConfigManager.CurrentConfig == null || !(SubHighlightGlowPresetComboBox.SelectedItem is ComboBoxItem { Tag: var tag }))
		{
			return;
		}
		string text = tag?.ToString() ?? "FollowPrimary";
		ConfigManager.CurrentConfig.SubWheelHighlightGlowPreset = text;
		switch (text)
		{
		case "Lilac":
			ConfigManager.CurrentConfig.SubWheelHighlightGlowColor = "#A855F7";
			if (SubHighlightGlowColorTextBox != null)
			{
				SubHighlightGlowColorTextBox.Text = "#A855F7";
			}
			break;
		case "Blue":
			ConfigManager.CurrentConfig.SubWheelHighlightGlowColor = "#3B82F6";
			if (SubHighlightGlowColorTextBox != null)
			{
				SubHighlightGlowColorTextBox.Text = "#3B82F6";
			}
			break;
		case "Emerald":
			ConfigManager.CurrentConfig.SubWheelHighlightGlowColor = "#10B981";
			if (SubHighlightGlowColorTextBox != null)
			{
				SubHighlightGlowColorTextBox.Text = "#10B981";
			}
			break;
		case "Rose":
			ConfigManager.CurrentConfig.SubWheelHighlightGlowColor = "#EC4899";
			if (SubHighlightGlowColorTextBox != null)
			{
				SubHighlightGlowColorTextBox.Text = "#EC4899";
			}
			break;
		case "Amber":
			ConfigManager.CurrentConfig.SubWheelHighlightGlowColor = "#F59E0B";
			if (SubHighlightGlowColorTextBox != null)
			{
				SubHighlightGlowColorTextBox.Text = "#F59E0B";
			}
			break;
		case "Red":
			ConfigManager.CurrentConfig.SubWheelHighlightGlowColor = "#EF4444";
			if (SubHighlightGlowColorTextBox != null)
			{
				SubHighlightGlowColorTextBox.Text = "#EF4444";
			}
			break;
		case "White":
			ConfigManager.CurrentConfig.SubWheelHighlightGlowColor = "#FFFFFF";
			if (SubHighlightGlowColorTextBox != null)
			{
				SubHighlightGlowColorTextBox.Text = "#FFFFFF";
			}
			break;
		case "None":
		case "Auto":
		case "FollowPrimary":
			ConfigManager.CurrentConfig.SubWheelHighlightGlowColor = "";
			if (SubHighlightGlowColorTextBox != null)
			{
				SubHighlightGlowColorTextBox.Text = "";
			}
			break;
		}
		if (SubCustomHighlightGlowPanel != null)
		{
			SubCustomHighlightGlowPanel.Visibility = ((!(text == "Custom") && string.IsNullOrEmpty(SubHighlightGlowColorTextBox?.Text)) ? Visibility.Collapsed : Visibility.Visible);
		}
		UpdateColorPreviews();
		Grid appearanceSettingsGrid = AppearanceSettingsGrid;
		if (appearanceSettingsGrid != null && appearanceSettingsGrid.Visibility == Visibility.Visible)
		{
			RenderLiveWheelPreview();
		}
		SyncUiToConfigAndSave();
	}

	private void SubHighlightGlowColorTextBox_TextChanged(object sender, TextChangedEventArgs e)
	{
		if (!_isUpdatingUi && ConfigManager.CurrentConfig != null && SubHighlightGlowColorTextBox != null)
		{
			ConfigManager.CurrentConfig.SubWheelHighlightGlowColor = SubHighlightGlowColorTextBox.Text.Trim();
			UpdateColorPreviews();
			Grid appearanceSettingsGrid = AppearanceSettingsGrid;
			if (appearanceSettingsGrid != null && appearanceSettingsGrid.Visibility == Visibility.Visible)
			{
				RenderLiveWheelPreview();
			}
			ScheduleAutoSave();
		}
	}

	private void SubHighlightGlowRadiusSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
	{
		if (!_isUpdatingUi && SubHighlightGlowRadiusLabel != null && ConfigManager.CurrentConfig != null)
		{
			SubHighlightGlowRadiusLabel.Text = $"{e.NewValue:0} px";
			ConfigManager.CurrentConfig.SubWheelHighlightGlowRadius = e.NewValue;
			Grid appearanceSettingsGrid = AppearanceSettingsGrid;
			if (appearanceSettingsGrid != null && appearanceSettingsGrid.Visibility == Visibility.Visible)
			{
				RenderLiveWheelPreview();
			}
			ScheduleAutoSave();
		}
	}

	private void SubHighlightGlowOpacitySlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
	{
		if (!_isUpdatingUi && SubHighlightGlowOpacityLabel != null && ConfigManager.CurrentConfig != null)
		{
			SubHighlightGlowOpacityLabel.Text = $"{e.NewValue:0}%";
			ConfigManager.CurrentConfig.SubWheelHighlightGlowOpacity = e.NewValue / 100.0;
			Grid appearanceSettingsGrid = AppearanceSettingsGrid;
			if (appearanceSettingsGrid != null && appearanceSettingsGrid.Visibility == Visibility.Visible)
			{
				RenderLiveWheelPreview();
			}
			ScheduleAutoSave();
		}
	}

	private void PickIcon_Click(object sender, RoutedEventArgs e)
	{
		if (!(sender is FrameworkElement { DataContext: SlotViewModel dataContext }))
		{
			return;
		}
		IconPickerWindow iconPickerWindow = new IconPickerWindow(dataContext.IconKey);
		iconPickerWindow.Owner = this;
		if (iconPickerWindow.ShowDialog() == true)
		{
			dataContext.IconKey = iconPickerWindow.SelectedIconKey ?? "";
			Grid appearanceSettingsGrid = AppearanceSettingsGrid;
			if (appearanceSettingsGrid != null && appearanceSettingsGrid.Visibility == Visibility.Visible)
			{
				RenderLiveWheelPreview();
			}
		}
	}

	private void ManageSubActions_Click(object sender, RoutedEventArgs e)
	{
		if (!(sender is FrameworkElement { DataContext: SlotViewModel dataContext }))
		{
			return;
		}
		try
		{
			SubActionEditorWindow subActionEditorWindow = new SubActionEditorWindow(dataContext.DirectionLabel, dataContext.Name, dataContext.Action.SubActions);
			subActionEditorWindow.Owner = this;
			if (subActionEditorWindow.ShowDialog() == true)
			{
				dataContext.Action.SubActions = subActionEditorWindow.ResultSubActions;
				dataContext.NotifySubActionsChanged();
				ConfigManager.SaveConfig();
				RenderLiveWheelPreview();
			}
		}
		catch (Exception ex)
		{
			System.Windows.MessageBox.Show("打开级联子菜单编辑器失败: " + ex.Message, "错误", MessageBoxButton.OK, MessageBoxImage.Hand);
		}
	}

	private void EnableMultiTierCheckBox_Changed(object sender, RoutedEventArgs e)
	{
		if (!_isUpdatingUi && ConfigManager.CurrentConfig != null && EnableMultiTierCheckBox != null)
		{
			ConfigManager.CurrentConfig.EnableMultiTier = EnableMultiTierCheckBox.IsChecked == true;
			ConfigManager.SaveConfig();
			Grid appearanceSettingsGrid = AppearanceSettingsGrid;
			if (appearanceSettingsGrid != null && appearanceSettingsGrid.Visibility == Visibility.Visible)
			{
				RenderLiveWheelPreview();
			}
		}
	}

	private void CustomColorExpander_Expanded(object sender, RoutedEventArgs e)
	{
		PopulateCustomColorsIfEmpty();
		Grid appearanceSettingsGrid = AppearanceSettingsGrid;
		if (appearanceSettingsGrid != null && appearanceSettingsGrid.Visibility == Visibility.Visible)
		{
			RenderLiveWheelPreview();
		}
	}

	private void CustomColorExpander_Collapsed(object sender, RoutedEventArgs e)
	{
		Grid appearanceSettingsGrid = AppearanceSettingsGrid;
		if (appearanceSettingsGrid != null && appearanceSettingsGrid.Visibility == Visibility.Visible)
		{
			RenderLiveWheelPreview();
		}
	}

	private void PopulateCustomColorsIfEmpty()
	{
		if (CustomSectorBgTextBox != null && _previewStyleRenderer != null)
		{
			if (string.IsNullOrWhiteSpace(CustomSectorBgTextBox.Text) && _previewDefaultBrush is SolidColorBrush solidColorBrush)
			{
				CustomSectorBgTextBox.Text = $"#{solidColorBrush.Color.A:X2}{solidColorBrush.Color.R:X2}{solidColorBrush.Color.G:X2}{solidColorBrush.Color.B:X2}";
			}
			if (string.IsNullOrWhiteSpace(CustomSectorBorderTextBox.Text) && _previewBorderBrush is SolidColorBrush solidColorBrush2)
			{
				CustomSectorBorderTextBox.Text = $"#{solidColorBrush2.Color.A:X2}{solidColorBrush2.Color.R:X2}{solidColorBrush2.Color.G:X2}{solidColorBrush2.Color.B:X2}";
			}
			if (string.IsNullOrWhiteSpace(CustomHighlightBgTextBox.Text) && _previewHighlightBrush is SolidColorBrush solidColorBrush3)
			{
				CustomHighlightBgTextBox.Text = $"#{solidColorBrush3.Color.A:X2}{solidColorBrush3.Color.R:X2}{solidColorBrush3.Color.G:X2}{solidColorBrush3.Color.B:X2}";
			}
			if (string.IsNullOrWhiteSpace(CustomHighlightBorderTextBox.Text) && _previewHighlightBorderBrush is SolidColorBrush solidColorBrush4)
			{
				CustomHighlightBorderTextBox.Text = $"#{solidColorBrush4.Color.A:X2}{solidColorBrush4.Color.R:X2}{solidColorBrush4.Color.G:X2}{solidColorBrush4.Color.B:X2}";
			}
			if (string.IsNullOrWhiteSpace(CustomTextTextBox.Text) && _previewTextBrush is SolidColorBrush solidColorBrush5)
			{
				CustomTextTextBox.Text = $"#{solidColorBrush5.Color.A:X2}{solidColorBrush5.Color.R:X2}{solidColorBrush5.Color.G:X2}{solidColorBrush5.Color.B:X2}";
			}
			UpdateColorPreviews();
		}
	}

	private void CustomColorTextBox_TextChanged(object sender, TextChangedEventArgs e)
	{
		if (!_isUpdatingUi && ConfigManager.CurrentConfig != null)
		{
			ConfigManager.CurrentConfig.CustomSectorBg = CustomSectorBgTextBox.Text.Trim();
			ConfigManager.CurrentConfig.CustomSectorBorder = CustomSectorBorderTextBox.Text.Trim();
			ConfigManager.CurrentConfig.CustomHighlightBg = CustomHighlightBgTextBox.Text.Trim();
			ConfigManager.CurrentConfig.CustomHighlightBorder = CustomHighlightBorderTextBox.Text.Trim();
			ConfigManager.CurrentConfig.CustomText = CustomTextTextBox.Text.Trim();
			UpdateColorPreviews();
			Grid appearanceSettingsGrid = AppearanceSettingsGrid;
			if (appearanceSettingsGrid != null && appearanceSettingsGrid.Visibility == Visibility.Visible)
			{
				RenderLiveWheelPreview();
			}
		}
	}

	private void UpdateColorPreviews()
	{
		UpdateColorPreviewBorder(CustomSectorBgPreview, CustomSectorBgTextBox.Text);
		UpdateColorPreviewBorder(CustomSectorBorderPreview, CustomSectorBorderTextBox.Text);
		UpdateColorPreviewBorder(CustomHighlightBgPreview, CustomHighlightBgTextBox.Text);
		UpdateColorPreviewBorder(CustomHighlightBorderPreview, CustomHighlightBorderTextBox.Text);
		UpdateColorPreviewBorder(CustomTextPreview, CustomTextTextBox.Text);
		if (HighlightGlowColorPreview != null && HighlightGlowColorTextBox != null)
		{
			UpdateColorPreviewBorder(HighlightGlowColorPreview, HighlightGlowColorTextBox.Text);
		}
		if (SubHighlightGlowColorPreview != null && SubHighlightGlowColorTextBox != null)
		{
			UpdateColorPreviewBorder(SubHighlightGlowColorPreview, SubHighlightGlowColorTextBox.Text);
		}
		if (SubCustomSectorBgPreview != null && SubCustomSectorBgTextBox != null)
		{
			UpdateColorPreviewBorder(SubCustomSectorBgPreview, SubCustomSectorBgTextBox.Text);
		}
		if (SubCustomSectorBorderPreview != null && SubCustomSectorBorderTextBox != null)
		{
			UpdateColorPreviewBorder(SubCustomSectorBorderPreview, SubCustomSectorBorderTextBox.Text);
		}
		if (SubCustomHighlightBgPreview != null && SubCustomHighlightBgTextBox != null)
		{
			UpdateColorPreviewBorder(SubCustomHighlightBgPreview, SubCustomHighlightBgTextBox.Text);
		}
		if (SubCustomHighlightBorderPreview != null && SubCustomHighlightBorderTextBox != null)
		{
			UpdateColorPreviewBorder(SubCustomHighlightBorderPreview, SubCustomHighlightBorderTextBox.Text);
		}
		if (SubCustomTextPreview != null && SubCustomTextTextBox != null)
		{
			UpdateColorPreviewBorder(SubCustomTextPreview, SubCustomTextTextBox.Text);
		}
	}

	private void UpdateColorPreviewBorder(Border border, string hex)
	{
		try
		{
			if (!string.IsNullOrEmpty(hex))
			{
				System.Windows.Media.Color color = (System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString(hex);
				border.Background = new SolidColorBrush(color);
			}
			else
			{
				border.Background = System.Windows.Media.Brushes.Transparent;
			}
		}
		catch
		{
			border.Background = System.Windows.Media.Brushes.Transparent;
		}
	}

	private void PickCustomColor_Click(object sender, RoutedEventArgs e)
	{
		if (!(sender is FrameworkElement { Tag: string tag }))
		{
			return;
		}
		System.Windows.Controls.TextBox targetBox = GetColorTextBoxByTag(tag);
		if (targetBox != null)
		{
			string text = targetBox.Text;
			ColorPickerWindow colorPickerWindow = new ColorPickerWindow(text)
			{
				Owner = this
			};
			colorPickerWindow.ColorChangedCallback = delegate(string hex)
			{
				targetBox.Text = hex;
			};
			if (colorPickerWindow.ShowDialog() == true && !string.IsNullOrEmpty(colorPickerWindow.SelectedHexColor))
			{
				targetBox.Text = colorPickerWindow.SelectedHexColor;
			}
			else
			{
				targetBox.Text = text;
			}
		}
	}

	private void PickEyedropper_Click(object sender, RoutedEventArgs e)
	{
		if (!(sender is FrameworkElement { Tag: string tag }))
		{
			return;
		}
		System.Windows.Controls.TextBox colorTextBoxByTag = GetColorTextBoxByTag(tag);
		if (colorTextBoxByTag != null)
		{
			ScreenEyedropperOverlay screenEyedropperOverlay = new ScreenEyedropperOverlay();
			if (screenEyedropperOverlay.ShowDialog() == true && !string.IsNullOrEmpty(screenEyedropperOverlay.CapturedHexColor))
			{
				colorTextBoxByTag.Text = screenEyedropperOverlay.CapturedHexColor;
			}
		}
	}

	private System.Windows.Controls.TextBox? GetColorTextBoxByTag(string tag)
	{
		return tag switch
		{
			"CustomSectorBg" => CustomSectorBgTextBox, 
			"CustomSectorBorder" => CustomSectorBorderTextBox, 
			"CustomHighlightBg" => CustomHighlightBgTextBox, 
			"CustomHighlightBorder" => CustomHighlightBorderTextBox, 
			"CustomText" => CustomTextTextBox, 
			"SectorCustomText" => SectorTextColorTextBox,
			"CoreCustomText" => CoreTextColorTextBox,
			"HighlightGlowColor" => HighlightGlowColorTextBox, 
			"SubHighlightGlowColor" => SubHighlightGlowColorTextBox, 
			"SubCustomSectorBg" => SubCustomSectorBgTextBox, 
			"SubCustomSectorBorder" => SubCustomSectorBorderTextBox, 
			"SubCustomHighlightBg" => SubCustomHighlightBgTextBox, 
			"SubCustomHighlightBorder" => SubCustomHighlightBorderTextBox, 
			"SubCustomText" => SubCustomTextTextBox, 
			_ => null, 
		};
	}

	private void AppThemeComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
	{
		if (!_isUpdatingUi && ConfigManager.CurrentConfig != null)
		{
			string text = (AppThemeComboBox.SelectedItem as ComboBoxItem)?.Tag?.ToString() ?? "System";
			ConfigManager.CurrentConfig.AppTheme = text;
			AppThemeManager.ApplyTheme(this, text);
			Grid appearanceSettingsGrid = AppearanceSettingsGrid;
			if (appearanceSettingsGrid != null && appearanceSettingsGrid.Visibility == Visibility.Visible)
			{
				RenderLiveWheelPreview();
			}
			SyncUiToConfigAndSave();
		}
	}

	private void DisableOnFullScreenCheckBox_Changed(object sender, RoutedEventArgs e)
	{
		if (!_isUpdatingUi && ConfigManager.CurrentConfig != null)
		{
			ConfigManager.CurrentConfig.DisableOnFullScreen = DisableOnFullScreenCheckBox.IsChecked == true;
			SyncUiToConfigAndSave();
		}
	}

	private void ModifierCheckBox_Changed(object sender, RoutedEventArgs e)
	{
		if (!_isUpdatingUi && ConfigManager.CurrentConfig != null)
		{
			ConfigManager.CurrentConfig.DisableOnCtrl = CtrlModifierCheckBox.IsChecked == true;
			ConfigManager.CurrentConfig.DisableOnShift = ShiftModifierCheckBox.IsChecked == true;
			ConfigManager.CurrentConfig.DisableOnAlt = AltModifierCheckBox.IsChecked == true;
			SyncUiToConfigAndSave();
		}
	}

	private void BrowseBlacklistButton_Click(object sender, RoutedEventArgs e)
	{
		try
		{
			ProgramPickerWindow programPickerWindow = new ProgramPickerWindow();
			programPickerWindow.Owner = this;
			if (programPickerWindow.ShowDialog() == true && !string.IsNullOrEmpty(programPickerWindow.SelectedPath))
			{
				string proc = System.IO.Path.GetFileName(programPickerWindow.SelectedPath).ToLower();
				AddBlacklistProcess(proc);
			}
		}
		catch (Exception)
		{
		}
	}

	private void AddBlacklistButton_Click(object sender, RoutedEventArgs e)
	{
		string text = NewBlacklistProcessTextBox.Text.Trim().ToLower();
		if (string.IsNullOrEmpty(text))
		{
			BrowseBlacklistButton_Click(sender, e);
		}
		else
		{
			AddBlacklistProcess(text);
		}
	}

	private void NewBlacklistProcessTextBox_KeyDown(object sender, System.Windows.Input.KeyEventArgs e)
	{
		//IL_0001: Unknown result type (might be due to invalid IL or missing references)
		//IL_0007: Invalid comparison between Unknown and I4
		if ((int)e.Key == 6)
		{
			AddBlacklistButton_Click(sender, e);
			e.Handled = true;
		}
	}

	private void IsolationModeRadio_Checked(object sender, RoutedEventArgs e)
	{
		if (!_isUpdatingUi && ConfigManager.CurrentConfig != null)
		{
			System.Windows.Controls.RadioButton isolationWhitelistRadio = IsolationWhitelistRadio;
			string isolationMode = ((isolationWhitelistRadio != null && isolationWhitelistRadio.IsChecked == true) ? "Whitelist" : "Blacklist");
			ConfigManager.CurrentConfig.IsolationMode = isolationMode;
			RefreshProcessListUI();
			SyncUiToConfigAndSave();
		}
	}

	private void RefreshProcessListUI()
	{
		if (BlacklistListBox == null || ConfigManager.CurrentConfig == null)
		{
			return;
		}
		bool flag = string.Equals(ConfigManager.CurrentConfig.IsolationMode, "Whitelist", StringComparison.OrdinalIgnoreCase);
		if (IsolationWhitelistRadio != null)
		{
			IsolationWhitelistRadio.IsChecked = flag;
		}
		if (IsolationBlacklistRadio != null)
		{
			IsolationBlacklistRadio.IsChecked = !flag;
		}
		if (ProcessListDescText != null)
		{
			ProcessListDescText.Text = (flag ? I18n.T("WhitelistDesc") : I18n.T("BlacklistDesc"));
		}
		BlacklistListBox.Items.Clear();
		List<string> list = (flag ? ConfigManager.CurrentConfig.WhitelistedProcesses : ConfigManager.CurrentConfig.BlacklistedProcesses);
		if (list == null)
		{
			return;
		}
		foreach (string item in list)
		{
			BlacklistListBox.Items.Add(item);
		}
	}

	private void BlacklistListBox_KeyDown(object sender, System.Windows.Input.KeyEventArgs e)
	{
		//IL_0001: Unknown result type (might be due to invalid IL or missing references)
		//IL_0008: Invalid comparison between Unknown and I4
		//IL_000b: Unknown result type (might be due to invalid IL or missing references)
		//IL_0011: Invalid comparison between Unknown and I4
		if ((int)e.Key == 32 || (int)e.Key == 2)
		{
			DeleteBlacklistButton_Click(sender, e);
			e.Handled = true;
		}
	}

	private void AddBlacklistProcess(string proc)
	{
		if (string.IsNullOrWhiteSpace(proc))
		{
			return;
		}
		proc = proc.Trim().ToLower();
		if (!proc.EndsWith(".exe", StringComparison.OrdinalIgnoreCase))
		{
			proc += ".exe";
		}
		object obj;
		if (!string.Equals(ConfigManager.CurrentConfig.IsolationMode, "Whitelist", StringComparison.OrdinalIgnoreCase))
		{
			AppConfig currentConfig = ConfigManager.CurrentConfig;
			obj = currentConfig.BlacklistedProcesses ?? (currentConfig.BlacklistedProcesses = new List<string>());
		}
		else
		{
			AppConfig currentConfig = ConfigManager.CurrentConfig;
			obj = currentConfig.WhitelistedProcesses ?? (currentConfig.WhitelistedProcesses = new List<string>());
		}
		List<string> list3 = (List<string>)obj;
		if (!BlacklistListBox.Items.Contains(proc))
		{
			BlacklistListBox.Items.Add(proc);
			BlacklistListBox.SelectedItem = proc;
			BlacklistListBox.ScrollIntoView(proc);
			if (!list3.Contains(proc))
			{
				list3.Add(proc);
			}
			NewBlacklistProcessTextBox.Clear();
			SyncUiToConfigAndSave();
		}
		else
		{
			BlacklistListBox.SelectedItem = proc;
			BlacklistListBox.ScrollIntoView(proc);
		}
	}

	private void DeleteBlacklistButton_Click(object sender, RoutedEventArgs e)
	{
		string text = BlacklistListBox.SelectedItem?.ToString();
		if (string.IsNullOrEmpty(text) && BlacklistListBox.Items.Count > 0)
		{
			text = BlacklistListBox.Items[BlacklistListBox.Items.Count - 1]?.ToString();
		}
		if (!string.IsNullOrEmpty(text))
		{
			BlacklistListBox.Items.Remove(text);
			if (string.Equals(ConfigManager.CurrentConfig.IsolationMode, "Whitelist", StringComparison.OrdinalIgnoreCase))
			{
				ConfigManager.CurrentConfig.WhitelistedProcesses?.Remove(text);
			}
			else
			{
				ConfigManager.CurrentConfig.BlacklistedProcesses?.Remove(text);
			}
			SyncUiToConfigAndSave();
		}
	}

	private async void CheckUpdateNowBtn_Click(object sender, RoutedEventArgs e)
	{
		await CheckForUpdateInternalAsync(silent: false);
	}

	private async Task CheckForUpdateInternalAsync(bool silent = false)
	{
		try
		{
			if (CheckUpdateNowBtn != null)
			{
				CheckUpdateNowBtn.IsEnabled = false;
				CheckUpdateNowBtn.Content = "⏳ 正在检查...";
			}
			if (UpdateStatusBadgeText != null)
			{
				UpdateStatusBadgeText.Text = "正在检查更新...";
			}

			string channel = ConfigManager.CurrentConfig?.UpdateChannel ?? "Stable";
			string proxy = ConfigManager.CurrentConfig?.UpdateProxySource ?? "ghproxy";
			string customProxy = ConfigManager.CurrentConfig?.CustomProxyUrl ?? "";

			ReleaseInfo? rel = await UpdateManager.Instance.CheckForUpdateAsync(channel, proxy, customProxy);

			if (ConfigManager.CurrentConfig != null)
			{
				ConfigManager.CurrentConfig.LastCheckUpdateTime = DateTime.Now.ToString("yyyy-MM-dd HH:mm");
				ScheduleAutoSave();
			}

			if (rel != null && rel.IsNewerVersion)
			{
				_latestReleaseInfo = rel;

				if (UpdateStatusBadgeText != null)
				{
					UpdateStatusBadgeText.Text = $"发现新版本 {rel.TagName}";
					UpdateStatusBadgeText.Foreground = new SolidColorBrush(System.Windows.Media.Color.FromRgb(245, 158, 11));
				}
				if (UpdateStatusBadge != null)
				{
					UpdateStatusBadge.Background = new SolidColorBrush(System.Windows.Media.Color.FromArgb(40, 245, 158, 11));
				}
				if (UpdateStatusDescText != null)
				{
					UpdateStatusDescText.Text = $"GitHub Releases 探测到更高版本 {rel.TagName} 可供升级！";
				}

				if (UpdateNewVersionTagText != null)
				{
					UpdateNewVersionTagText.Text = $"🎉 发现新版本 {rel.TagName}";
				}
				if (UpdateReleaseChannelTag != null)
				{
					UpdateReleaseChannelTag.Text = rel.IsPrerelease ? "尝鲜体验版 (Pre-release)" : "正式稳定版 (Stable)";
				}
				if (UpdateReleaseDateText != null)
				{
					UpdateReleaseDateText.Text = $"发布于 {rel.PublishedAt:yyyy-MM-dd HH:mm} · GitHub Releases";
				}
				if (UpdateChangelogTextBlock != null)
				{
					UpdateChangelogTextBlock.Text = string.IsNullOrWhiteSpace(rel.Body) ? "作者暂未提供更新日志说明。" : rel.Body;
				}

				if (UpdateNewVersionPanel != null)
				{
					UpdateNewVersionPanel.Visibility = Visibility.Visible;
				}
				if (UpdateReadyToInstallPanel != null)
				{
					UpdateReadyToInstallPanel.Visibility = Visibility.Collapsed;
				}
				if (UpdateDownloadProgressPanel != null)
				{
					UpdateDownloadProgressPanel.Visibility = Visibility.Collapsed;
				}
			}
			else if (rel != null)
			{
				_latestReleaseInfo = rel;
				if (UpdateStatusBadgeText != null)
				{
					UpdateStatusBadgeText.Text = "当前已是最新版本";
					UpdateStatusBadgeText.Foreground = new SolidColorBrush(System.Windows.Media.Color.FromRgb(16, 185, 129));
				}
				if (UpdateStatusBadge != null)
				{
					UpdateStatusBadge.Background = new SolidColorBrush(System.Windows.Media.Color.FromArgb(30, 16, 185, 129));
				}
				if (UpdateStatusDescText != null)
				{
					UpdateStatusDescText.Text = $"当前运行版本: StarPie v{Assembly.GetExecutingAssembly().GetName().Version?.ToString(3) ?? "1.6.2"} (64位)。上次检查: {ConfigManager.CurrentConfig?.LastCheckUpdateTime}";
				}
				if (UpdateNewVersionPanel != null)
				{
					UpdateNewVersionPanel.Visibility = Visibility.Collapsed;
				}
			}
			else
			{
				if (!silent)
				{
					if (UpdateStatusBadgeText != null)
					{
						UpdateStatusBadgeText.Text = "检查更新超时";
						UpdateStatusBadgeText.Foreground = new SolidColorBrush(System.Windows.Media.Color.FromRgb(239, 68, 68));
					}
					if (UpdateStatusBadge != null)
					{
						UpdateStatusBadge.Background = new SolidColorBrush(System.Windows.Media.Color.FromArgb(30, 239, 68, 68));
					}
					if (UpdateStatusDescText != null)
					{
						UpdateStatusDescText.Text = "无法连接至 GitHub Releases API，建议在下方切换为国内加速镜像源重试。";
					}
				}
			}
		}
		catch (Exception ex)
		{
			AppLogger.LogError("CheckForUpdateInternalAsync error", ex);
		}
		finally
		{
			if (CheckUpdateNowBtn != null)
			{
				CheckUpdateNowBtn.IsEnabled = true;
				CheckUpdateNowBtn.Content = "🔄 立即检查更新";
			}
		}
	}

	private async void StartDownloadUpdateBtn_Click(object sender, RoutedEventArgs e)
	{
		if (_latestReleaseInfo == null) return;

		bool isStandalone = (UpdatePkgStandaloneRadio?.IsChecked == true);
		string? rawAssetUrl = isStandalone ? _latestReleaseInfo.StandaloneAssetUrl : _latestReleaseInfo.LightweightAssetUrl;

		if (string.IsNullOrEmpty(rawAssetUrl))
		{
			OpenWebReleaseBtn_Click(sender, e);
			return;
		}

		string proxy = ConfigManager.CurrentConfig?.UpdateProxySource ?? "ghproxy";
		string customProxy = ConfigManager.CurrentConfig?.CustomProxyUrl ?? "";
		string downloadUrl = UpdateManager.Instance.GetProxiedDownloadUrl(rawAssetUrl, proxy, customProxy);

		string fileName = isStandalone
			? $"StarPie-{_latestReleaseInfo.TagName}-Standalone-win-x64.zip"
			: $"StarPie-{_latestReleaseInfo.TagName}-Lightweight-win-x64.zip";

		string destPath = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "StarPie_Updates", fileName);
		_downloadedZipPath = destPath;

		if (UpdateNewVersionPanel != null) UpdateNewVersionPanel.Visibility = Visibility.Collapsed;
		if (UpdateReadyToInstallPanel != null) UpdateReadyToInstallPanel.Visibility = Visibility.Collapsed;
		if (UpdateDownloadProgressPanel != null) UpdateDownloadProgressPanel.Visibility = Visibility.Visible;

		if (UpdateDownloadingTitleText != null) UpdateDownloadingTitleText.Text = $"正在高速下载 {fileName}...";
		if (UpdateDownloadPercentText != null) UpdateDownloadPercentText.Text = "0%";
		if (UpdateDownloadProgressBar != null) UpdateDownloadProgressBar.Value = 0;
		if (UpdateDownloadSpeedText != null) UpdateDownloadSpeedText.Text = "⚡ 连接下载源中...";

		_downloadCts?.Dispose();
		_downloadCts = new CancellationTokenSource();

		Progress<UpdateProgressInfo> progress = new Progress<UpdateProgressInfo>(info =>
		{
			if (UpdateDownloadProgressBar != null) UpdateDownloadProgressBar.Value = info.Percent;
			if (UpdateDownloadPercentText != null) UpdateDownloadPercentText.Text = $"{info.Percent}%";
			if (UpdateDownloadSpeedText != null) UpdateDownloadSpeedText.Text = $"⚡ {info.FormattedSpeed}";
			if (UpdateDownloadSizeText != null) UpdateDownloadSizeText.Text = info.FormattedProgress;
		});

		try
		{
			await UpdateManager.Instance.DownloadAssetAsync(downloadUrl, destPath, progress, _downloadCts.Token);

			if (UpdateDownloadProgressPanel != null) UpdateDownloadProgressPanel.Visibility = Visibility.Collapsed;
			if (UpdateReadyToInstallPanel != null) UpdateReadyToInstallPanel.Visibility = Visibility.Visible;

			if (UpdateStatusBadgeText != null)
			{
				UpdateStatusBadgeText.Text = "下载完成 · 就绪安装";
				UpdateStatusBadgeText.Foreground = new SolidColorBrush(System.Windows.Media.Color.FromRgb(16, 185, 129));
			}
			if (UpdateStatusBadge != null)
			{
				UpdateStatusBadge.Background = new SolidColorBrush(System.Windows.Media.Color.FromArgb(30, 16, 185, 129));
			}
		}
		catch (OperationCanceledException)
		{
			if (UpdateDownloadProgressPanel != null) UpdateDownloadProgressPanel.Visibility = Visibility.Collapsed;
			if (UpdateNewVersionPanel != null) UpdateNewVersionPanel.Visibility = Visibility.Visible;
		}
		catch (Exception ex)
		{
			AppLogger.LogError("DownloadUpdate failed", ex);
			if (UpdateDownloadProgressPanel != null) UpdateDownloadProgressPanel.Visibility = Visibility.Collapsed;
			if (UpdateNewVersionPanel != null) UpdateNewVersionPanel.Visibility = Visibility.Visible;
			System.Windows.MessageBox.Show($"下载更新包失败：{ex.Message}\n建议切换加速镜像源重试或点击前往网页下载。", "StarPie 更新", MessageBoxButton.OK, MessageBoxImage.Warning);
		}
	}

	private void CancelDownloadBtn_Click(object sender, RoutedEventArgs e)
	{
		_downloadCts?.Cancel();
	}

	private void ApplyRestartUpdateBtn_Click(object sender, RoutedEventArgs e)
	{
		if (!string.IsNullOrEmpty(_downloadedZipPath) && File.Exists(_downloadedZipPath))
		{
			UpdateManager.Instance.RestartAndApplyUpdate(_downloadedZipPath);
		}
		else
		{
			System.Windows.MessageBox.Show("未找到已下载的更新包，请重新点击下载。", "StarPie 更新", MessageBoxButton.OK, MessageBoxImage.Information);
		}
	}

	private void OpenUpdateFolderBtn_Click(object sender, RoutedEventArgs e)
	{
		try
		{
			if (!string.IsNullOrEmpty(_downloadedZipPath) && File.Exists(_downloadedZipPath))
			{
				Process.Start("explorer.exe", $"/select,\"{_downloadedZipPath}\"");
			}
			else
			{
				string folder = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "StarPie_Updates");
				if (!Directory.Exists(folder)) Directory.CreateDirectory(folder);
				Process.Start("explorer.exe", folder);
			}
		}
		catch (Exception ex)
		{
			AppLogger.LogError("OpenUpdateFolder failed", ex);
		}
	}

	private void OpenWebReleaseBtn_Click(object sender, RoutedEventArgs e)
	{
		try
		{
			string url = _latestReleaseInfo?.HtmlUrl ?? "https://github.com/SoftBlack42/StarPie/releases";
			Process.Start(new ProcessStartInfo(url) { UseShellExecute = true });
		}
		catch (Exception ex)
		{
			AppLogger.LogError("OpenWebRelease failed", ex);
		}
	}

	private void AutoCheckUpdateCheckBox_Changed(object sender, RoutedEventArgs e)
	{
		if (!_isUpdatingUi && ConfigManager.CurrentConfig != null)
		{
			ConfigManager.CurrentConfig.AutoCheckUpdate = (AutoCheckUpdateCheckBox.IsChecked == true);
			ScheduleAutoSave();
		}
	}

	private void UpdateChannelComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
	{
		if (!_isUpdatingUi && ConfigManager.CurrentConfig != null && UpdateChannelComboBox.SelectedItem is ComboBoxItem item)
		{
			ConfigManager.CurrentConfig.UpdateChannel = item.Tag?.ToString() ?? "Stable";
			ScheduleAutoSave();
		}
	}

	private void UpdateProxyComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
	{
		if (!_isUpdatingUi && ConfigManager.CurrentConfig != null && UpdateProxyComboBox.SelectedItem is ComboBoxItem item)
		{
			ConfigManager.CurrentConfig.UpdateProxySource = item.Tag?.ToString() ?? "ghproxy";
			ScheduleAutoSave();
		}
	}

	private async void AboutCheckUpdateBtn_Click(object sender, RoutedEventArgs e)
	{
		SwitchToTab(3);
		await CheckForUpdateInternalAsync(silent: false);
	}

	private void AutoStartCheckBox_Changed(object sender, RoutedEventArgs e)
	{
		if (!_isUpdatingUi)
		{
			bool valueOrDefault = AutoStartCheckBox.IsChecked == true;
			bool asAdmin = (AutoStartAsAdminCheckBox?.IsChecked == true);
			ConfigManager.SetAutoStart(valueOrDefault, asAdmin);
			SyncUiToConfigAndSave();
		}
	}

	private void AutoStartAsAdminCheckBox_Changed(object sender, RoutedEventArgs e)
	{
		if (!_isUpdatingUi)
		{
			bool flag = (AutoStartAsAdminCheckBox?.IsChecked == true);
			if (ConfigManager.CurrentConfig != null)
			{
				ConfigManager.CurrentConfig.AutoStartAsAdmin = flag;
			}
			if (AutoStartCheckBox.IsChecked == true)
			{
				ConfigManager.SetAutoStart(enable: true, flag);
			}
			SyncUiToConfigAndSave();
		}
	}

	private void ElevatePrivileges_Click(object sender, RoutedEventArgs e)
	{
		try
		{
			string fileName = Environment.ProcessPath ?? System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "WinPieGestures.exe");
			Process.Start(new ProcessStartInfo
			{
				FileName = fileName,
				UseShellExecute = true,
				Verb = "runas"
			});
			ExitApplication();
		}
		catch (Exception ex)
		{
			System.Windows.MessageBox.Show("提权重启失败或已取消: " + ex.Message, "管理员提权", MessageBoxButton.OK, MessageBoxImage.Exclamation);
		}
	}

	private void ExportConfigButton_Click(object sender, RoutedEventArgs e)
	{
		Microsoft.Win32.SaveFileDialog saveFileDialog = new Microsoft.Win32.SaveFileDialog
		{
			Filter = "JSON 配置文件 (*.json)|*.json",
			FileName = $"WinPieGestures_Config_Backup_{DateTime.Now:yyyyMMdd}.json",
			Title = "导出配置文件"
		};
		if (saveFileDialog.ShowDialog() == true)
		{
			if (ConfigManager.ExportConfig(saveFileDialog.FileName))
			{
				System.Windows.MessageBox.Show("配置导出成功！", "提示", MessageBoxButton.OK, MessageBoxImage.Asterisk);
			}
			else
			{
				System.Windows.MessageBox.Show("配置导出失败，请检查写入权限。", "错误", MessageBoxButton.OK, MessageBoxImage.Hand);
			}
		}
	}

	private void ImportConfigButton_Click(object sender, RoutedEventArgs e)
	{
		Microsoft.Win32.OpenFileDialog openFileDialog = new Microsoft.Win32.OpenFileDialog
		{
			Filter = "JSON 配置文件 (*.json)|*.json",
			Title = "选择要导入的配置文件"
		};
		if (openFileDialog.ShowDialog() != true)
		{
			return;
		}
		if (ConfigManager.ImportConfig(openFileDialog.FileName))
		{
			_isUpdatingUi = true;
			try
			{
				LoadConfigToUi();
				AppThemeManager.ApplyTheme(this, ConfigManager.CurrentConfig.AppTheme ?? "System");
			}
			finally
			{
				_isUpdatingUi = false;
			}
			RefreshSlots();
			RenderLiveWheelPreview();
			System.Windows.MessageBox.Show("配置导入成功！已即时应用所有轮盘尺寸、主题与动作方案。", "提示", MessageBoxButton.OK, MessageBoxImage.Asterisk);
		}
		else
		{
			System.Windows.MessageBox.Show("导入失败：文件格式不匹配或已损坏。", "错误", MessageBoxButton.OK, MessageBoxImage.Hand);
		}
	}

	private void TrimMemoryButton_Click(object sender, RoutedEventArgs e)
	{
		MemoryOptimizer.TrimMemory(force: true);
		System.Windows.MessageBox.Show(this, "物理工作集内存已深度压缩！", "提示", MessageBoxButton.OK, MessageBoxImage.Asterisk);
	}

	private void OpenLogFolderButton_Click(object sender, RoutedEventArgs e)
	{
		AppLogger.OpenLogFolder();
	}

	private void ViewTodayLogButton_Click(object sender, RoutedEventArgs e)
	{
		AppLogger.OpenTodayLogFile();
	}

	private void OpenReleasesFolderButton_Click(object sender, RoutedEventArgs e)
	{
		try
		{
			string text = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "releases");
			if (!Directory.Exists(text))
			{
				text = AppDomain.CurrentDomain.BaseDirectory;
			}
			Process.Start(new ProcessStartInfo("explorer.exe", text)
			{
				UseShellExecute = true
			});
		}
		catch (Exception ex)
		{
			System.Windows.MessageBox.Show("无法打开目录: " + ex.Message);
		}
	}

	private void OpenAppFolderButton_Click(object sender, RoutedEventArgs e)
	{
		try
		{
			string baseDirectory = AppDomain.CurrentDomain.BaseDirectory;
			Process.Start(new ProcessStartInfo("explorer.exe", baseDirectory)
			{
				UseShellExecute = true
			});
		}
		catch (Exception ex)
		{
			System.Windows.MessageBox.Show("无法打开目录: " + ex.Message);
		}
	}

	private void OpenChangelogButton_Click(object sender, RoutedEventArgs e)
	{
		try
		{
			string text = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "CHANGELOG.md");
			if (File.Exists(text))
			{
				Process.Start(new ProcessStartInfo(text)
				{
					UseShellExecute = true
				});
			}
			else
			{
				System.Windows.MessageBox.Show("CHANGELOG.md 文件位于根目录。", "提示");
			}
		}
		catch (Exception ex)
		{
			System.Windows.MessageBox.Show("无法打开文件: " + ex.Message);
		}
	}

	private void SaveButton_Click(object sender, RoutedEventArgs e)
	{
		SyncUiToConfigAndSave();
		System.Windows.MessageBox.Show("配置已成功保存至硬盘！", "成功", MessageBoxButton.OK, MessageBoxImage.Asterisk);
	}

	private void CloseButton_Click(object sender, RoutedEventArgs e)
	{
		Close();
	}

	private void Browse_Click(object sender, RoutedEventArgs e)
	{
		if (!(sender is FrameworkElement { DataContext: SlotViewModel dataContext }))
		{
			return;
		}
		ProgramPickerWindow programPickerWindow = new ProgramPickerWindow();
		programPickerWindow.Owner = this;
		if (programPickerWindow.ShowDialog() == true && !string.IsNullOrEmpty(programPickerWindow.SelectedPath))
		{
			dataContext.Parameter = programPickerWindow.SelectedPath;
			if (string.IsNullOrEmpty(dataContext.Name) || dataContext.Name.StartsWith("动作") || dataContext.Name == "快捷动作")
			{
				dataContext.Name = ((!string.IsNullOrEmpty(programPickerWindow.SelectedName)) ? programPickerWindow.SelectedName : System.IO.Path.GetFileNameWithoutExtension(programPickerWindow.SelectedPath));
			}
		}
	}

	private void BrowseFolder_Click(object sender, RoutedEventArgs e)
	{
		if (!(sender is FrameworkElement { DataContext: SlotViewModel dataContext }))
		{
			return;
		}
		try
		{
			OpenFolderDialog openFolderDialog = new OpenFolderDialog
			{
				Title = I18n.T("BtnBrowseFolder"),
				Multiselect = false
			};
			if (!string.IsNullOrWhiteSpace(dataContext.Parameter) && Directory.Exists(dataContext.Parameter))
			{
				openFolderDialog.InitialDirectory = dataContext.Parameter;
			}
			if (openFolderDialog.ShowDialog(this) != true)
			{
				return;
			}
			string folderName = openFolderDialog.FolderName;
			if (!string.IsNullOrEmpty(folderName))
			{
				dataContext.Parameter = folderName;
				if (string.IsNullOrEmpty(dataContext.Name) || dataContext.Name.StartsWith("快捷动作") || dataContext.Name.StartsWith("动作") || dataContext.Name == "打开文件夹")
				{
					DirectoryInfo directoryInfo = new DirectoryInfo(folderName);
					dataContext.Name = directoryInfo.Name;
				}
				if (string.IsNullOrEmpty(dataContext.IconKey))
				{
					dataContext.IconKey = "Folder";
				}
				SyncUiToConfigAndSave();
			}
		}
		catch (Exception)
		{
		}
	}

	private void Test_Click(object sender, RoutedEventArgs e)
	{
		if (sender is FrameworkElement { DataContext: SlotViewModel dataContext })
		{
			ActionExecutor.Execute(dataContext.Action);
		}
	}

	private void SetComboBoxSelectedValue(System.Windows.Controls.ComboBox comboBox, string value)
	{
		if (comboBox == null || string.IsNullOrEmpty(value))
		{
			return;
		}
		string b = value;
		if (value == "RoundedRect" || value == "FloatingCapsules" || value == "Capsule")
		{
			b = "RoundedCapsule";
		}
		switch (value)
		{
		case "OrganicPetals":
		case "ArcTracker":
		case "LiquidDroplets":
		case "MinimalArc":
			b = "Original";
			break;
		}
		foreach (object item in (IEnumerable)comboBox.Items)
		{
			if (item is ComboBoxItem { Tag: var tag } comboBoxItem)
			{
				string text = tag?.ToString() ?? "";
				if (string.Equals(text, value, StringComparison.OrdinalIgnoreCase) || string.Equals(text, b, StringComparison.OrdinalIgnoreCase) || text.StartsWith(value, StringComparison.OrdinalIgnoreCase))
				{
					comboBox.SelectedItem = comboBoxItem;
					return;
				}
			}
		}
		if (comboBox == WheelFontFamilyComboBox)
		{
			ComboBoxItem comboBoxItem2 = new ComboBoxItem
			{
				Content = "\ud83d\udd24 " + value,
				Tag = value,
				FontFamily = new System.Windows.Media.FontFamily(value)
			};
			comboBox.Items.Insert(0, comboBoxItem2);
			comboBox.SelectedItem = comboBoxItem2;
		}
	}

	private bool IsRunningAsAdmin()
	{
		try
		{
			using WindowsIdentity ntIdentity = WindowsIdentity.GetCurrent();
			return new WindowsPrincipal(ntIdentity).IsInRole(WindowsBuiltInRole.Administrator);
		}
		catch
		{
			return false;
		}
	}

	private void RenderLiveWheelPreview()
	{
		//IL_09d6: Unknown result type (might be due to invalid IL or missing references)
		if (_isRenderingPreview || LiveWheelPreviewCanvas == null || ConfigManager.CurrentConfig == null)
		{
			return;
		}
		_isRenderingPreview = true;
		try
		{
			LiveWheelPreviewCanvas.Children.Clear();
			_previewSectorPaths.Clear();
			_previewTransforms.Clear();
			_previewAngles.Clear();
			_previewSubSectorPaths.Clear();
			_previewSubTransforms.Clear();
			_previewSubParentIndices.Clear();
			_previewSubIndices.Clear();
			_previewSubAngles.Clear();
			_previewCoreIconElement = null;
			_previewCoreIconDefaultVisibility = Visibility.Collapsed;
			_previewCoreIconDefaultOpacity = 1.0;
			_previewCoreIconDefaultEffect = null;
			_previewCoreUsesCustomImage = false;
			_previewCoreSelectionOverlay = null;
			_previewCoreSelectionText = null;
			_lastHoveredSector = -2;
			_lastHoveredSubIndex = -2;
			double num = 300.0 / 2.0;
			double num2 = 300.0 / 2.0;
			bool enableMultiTier = ConfigManager.CurrentConfig.EnableMultiTier;
			double num3 = ((ConfigManager.CurrentConfig.SubWheelRadiusRatio > 1.1) ? ConfigManager.CurrentConfig.SubWheelRadiusRatio : 1.45);
			WheelProfile wheelProfile = _selectedProfile ?? ConfigManager.CurrentConfig.Profiles.FirstOrDefault() ?? new WheelProfile
			{
				SectorCount = 8,
				Actions = new List<ActionItem>()
			};
			bool num4 = enableMultiTier && wheelProfile.Actions != null && wheelProfile.Actions.Any((ActionItem a) => a != null && a.SubActions != null && a.SubActions.Count > 0);
			double num5 = Math.Max(80.0, ConfigManager.CurrentConfig.WheelRadius);
			double num6 = ((ConfigManager.CurrentConfig.SubWheelOuterRadius > 0.0) ? ConfigManager.CurrentConfig.SubWheelOuterRadius : (ConfigManager.CurrentConfig.WheelRadius * num3));
			double baseScaleRef = Math.Max(215.0, ConfigManager.CurrentConfig.WheelRadius * 1.55);
			double num7 = 135.0 / baseScaleRef;
			double num8 = Math.Max(30.0, ConfigManager.CurrentConfig.WheelRadius * num7);
			double num9 = Math.Max(15.0, ConfigManager.CurrentConfig.InnerRadius * num7);
			double num10 = Math.Max(10.0, ConfigManager.CurrentConfig.CoreRadius * num7);
			double gap = Math.Max(0.0, ConfigManager.CurrentConfig.SectorGap * num7);
			double cornerRadius = Math.Max(0.0, ConfigManager.CurrentConfig.SectorCornerRadius * num7);
			double num11 = Math.Max(0.0, ((ConfigManager.CurrentConfig.SubWheelInnerGap >= 0.0) ? ConfigManager.CurrentConfig.SubWheelInnerGap : 4.0) * num7);
			double num12 = num8 + num11 + 2.0;
			double num13 = Math.Max(num12 + 10.0, num6 * num7);
			double cornerRadius2 = Math.Max(0.0, ((ConfigManager.CurrentConfig.SubWheelCornerRadius >= 0.0) ? ConfigManager.CurrentConfig.SubWheelCornerRadius : 4.0) * num7);
			if (num9 >= num8)
			{
				num9 = num8 * 0.5;
			}
			if (num10 >= num9)
			{
				num10 = num9 * 0.8;
			}
			string text = ConfigManager.CurrentConfig.UiStyle ?? "ClassicRing";
			string text2 = ConfigManager.CurrentConfig.Theme ?? "System";
			string shape = ConfigManager.CurrentConfig.Shape ?? "Original";
			string text3 = ConfigManager.CurrentConfig.IconLayoutMode ?? "IconAndText";
			bool flag = ConfigManager.CurrentConfig.ShowText && text3 != "IconOnly";
			_previewStyleRenderer = StyleRendererFactory.CreateRenderer(text);
			_previewStyleRenderer.Initialize(text2, ConfigManager.CurrentConfig);
			_previewDefaultBrush = _previewStyleRenderer.DefaultSectorBrush;
			_previewHighlightBrush = _previewStyleRenderer.HighlightSectorBrush;
			_previewBorderBrush = _previewStyleRenderer.SectorBorderBrush;
			_previewHighlightBorderBrush = _previewStyleRenderer.HighlightBorderBrush;
			_previewTextBrush = _previewStyleRenderer.TextColorBrush;
			_previewCoreBgBrush = _previewStyleRenderer.CoreBgBrush;
			_previewCoreBorderBrush = _previewStyleRenderer.CoreBorderBrush;
			if (CustomColorExpander != null && CustomColorExpander.IsExpanded)
			{
				try
				{
					if (CustomSectorBgTextBox != null && !string.IsNullOrWhiteSpace(CustomSectorBgTextBox.Text))
					{
						System.Windows.Media.Color color = (System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString(CustomSectorBgTextBox.Text.Trim());
						_previewDefaultBrush = new SolidColorBrush(color);
						_previewCoreBgBrush = _previewDefaultBrush;
					}
					if (CustomSectorBorderTextBox != null && !string.IsNullOrWhiteSpace(CustomSectorBorderTextBox.Text))
					{
						System.Windows.Media.Color color2 = (System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString(CustomSectorBorderTextBox.Text.Trim());
						_previewBorderBrush = new SolidColorBrush(color2);
						_previewCoreBorderBrush = _previewBorderBrush;
					}
					if (CustomHighlightBgTextBox != null && !string.IsNullOrWhiteSpace(CustomHighlightBgTextBox.Text))
					{
						System.Windows.Media.Color color3 = (System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString(CustomHighlightBgTextBox.Text.Trim());
						_previewHighlightBrush = new SolidColorBrush(color3);
					}
					if (CustomHighlightBorderTextBox != null && !string.IsNullOrWhiteSpace(CustomHighlightBorderTextBox.Text))
					{
						System.Windows.Media.Color color4 = (System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString(CustomHighlightBorderTextBox.Text.Trim());
						_previewHighlightBorderBrush = new SolidColorBrush(color4);
					}
					if (CustomTextTextBox != null && !string.IsNullOrWhiteSpace(CustomTextTextBox.Text))
					{
						System.Windows.Media.Color color5 = (System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString(CustomTextTextBox.Text.Trim());
						_previewTextBrush = new SolidColorBrush(color5);
					}
				}
				catch
				{
				}
			}
			string text4 = ((!string.IsNullOrEmpty(ConfigManager.CurrentConfig.SubWheelUiStyle) && ConfigManager.CurrentConfig.SubWheelUiStyle != "FollowPrimary") ? ConfigManager.CurrentConfig.SubWheelUiStyle : text);
			string text5 = ((!string.IsNullOrEmpty(ConfigManager.CurrentConfig.SubWheelTheme) && ConfigManager.CurrentConfig.SubWheelTheme != "FollowPrimary") ? ConfigManager.CurrentConfig.SubWheelTheme : text2);
			if (ConfigManager.CurrentConfig.UseIndependentSubWheelTheme || text4 != text || text5 != text2)
			{
				try
				{
					_previewSubStyleRenderer = StyleRendererFactory.CreateRenderer(text4);
					_previewSubStyleRenderer.Initialize(text5, ConfigManager.CurrentConfig);
					_previewSubDefaultBrush = _previewSubStyleRenderer.DefaultSectorBrush;
					_previewSubHighlightBrush = _previewSubStyleRenderer.HighlightSectorBrush;
					_previewSubBorderBrush = _previewSubStyleRenderer.SectorBorderBrush;
					_previewSubHighlightBorderBrush = _previewSubStyleRenderer.HighlightBorderBrush;
					_previewSubTextBrush = _previewSubStyleRenderer.TextColorBrush;
					if (text5 == "Custom")
					{
						if (!string.IsNullOrWhiteSpace(ConfigManager.CurrentConfig.SubWheelCustomSectorBg))
						{
							_previewSubDefaultBrush = new SolidColorBrush((System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString(ConfigManager.CurrentConfig.SubWheelCustomSectorBg));
						}
						if (!string.IsNullOrWhiteSpace(ConfigManager.CurrentConfig.SubWheelCustomSectorBorder))
						{
							_previewSubBorderBrush = new SolidColorBrush((System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString(ConfigManager.CurrentConfig.SubWheelCustomSectorBorder));
						}
						if (!string.IsNullOrWhiteSpace(ConfigManager.CurrentConfig.SubWheelCustomHighlightBg))
						{
							_previewSubHighlightBrush = new SolidColorBrush((System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString(ConfigManager.CurrentConfig.SubWheelCustomHighlightBg));
						}
						if (!string.IsNullOrWhiteSpace(ConfigManager.CurrentConfig.SubWheelCustomHighlightBorder))
						{
							_previewSubHighlightBorderBrush = new SolidColorBrush((System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString(ConfigManager.CurrentConfig.SubWheelCustomHighlightBorder));
						}
						if (!string.IsNullOrWhiteSpace(ConfigManager.CurrentConfig.SubWheelCustomText))
						{
							_previewSubTextBrush = new SolidColorBrush((System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString(ConfigManager.CurrentConfig.SubWheelCustomText));
						}
					}
				}
				catch
				{
					_previewSubStyleRenderer = _previewStyleRenderer;
					_previewSubDefaultBrush = _previewDefaultBrush;
					_previewSubHighlightBrush = _previewHighlightBrush;
					_previewSubBorderBrush = _previewBorderBrush;
					_previewSubHighlightBorderBrush = _previewHighlightBorderBrush;
					_previewSubTextBrush = _previewTextBrush;
				}
			}
			else
			{
				_previewSubStyleRenderer = _previewStyleRenderer;
				_previewSubDefaultBrush = _previewDefaultBrush;
				_previewSubHighlightBrush = _previewHighlightBrush;
				_previewSubBorderBrush = _previewBorderBrush;
				_previewSubHighlightBorderBrush = _previewHighlightBorderBrush;
				_previewSubTextBrush = _previewTextBrush;
			}
			if (SubCustomColorExpander != null && SubCustomColorExpander.IsExpanded)
			{
				try
				{
					if (SubCustomSectorBgTextBox != null && !string.IsNullOrWhiteSpace(SubCustomSectorBgTextBox.Text))
					{
						_previewSubDefaultBrush = new SolidColorBrush((System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString(SubCustomSectorBgTextBox.Text.Trim()));
					}
					if (SubCustomSectorBorderTextBox != null && !string.IsNullOrWhiteSpace(SubCustomSectorBorderTextBox.Text))
					{
						_previewSubBorderBrush = new SolidColorBrush((System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString(SubCustomSectorBorderTextBox.Text.Trim()));
					}
					if (SubCustomHighlightBgTextBox != null && !string.IsNullOrWhiteSpace(SubCustomHighlightBgTextBox.Text))
					{
						_previewSubHighlightBrush = new SolidColorBrush((System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString(SubCustomHighlightBgTextBox.Text.Trim()));
					}
					if (SubCustomHighlightBorderTextBox != null && !string.IsNullOrWhiteSpace(SubCustomHighlightBorderTextBox.Text))
					{
						_previewSubHighlightBorderBrush = new SolidColorBrush((System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString(SubCustomHighlightBorderTextBox.Text.Trim()));
					}
					if (SubCustomTextTextBox != null && !string.IsNullOrWhiteSpace(SubCustomTextTextBox.Text))
					{
						_previewSubTextBrush = new SolidColorBrush((System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString(SubCustomTextTextBox.Text.Trim()));
					}
				}
				catch
				{
				}
			}
			Grid grid = new Grid
			{
				Width = num10 * 2.0,
				Height = num10 * 2.0,
				RenderTransformOrigin = new Point(0.5, 0.5)
			};
			_previewCoreScale = new ScaleTransform(1.0, 1.0);
			grid.RenderTransform = _previewCoreScale;
			_previewCoreGrid = grid;
			_previewCoreCircle = new Ellipse
			{
				Width = num10 * 2.0,
				Height = num10 * 2.0,
				Fill = _previewCoreBgBrush,
				Stroke = _previewCoreBorderBrush,
				StrokeThickness = 1.5
			};
			grid.Children.Add(_previewCoreCircle);
			double num14 = Math.Max(12.0, num10 * 0.42);
			string text6 = ConfigManager.CurrentConfig.CoreIconType ?? "Exit";
			bool num15 = text6 == "Custom";
			IconHelper.CustomIconItem customIconItem = null;
			if (num15 && !string.IsNullOrEmpty(ConfigManager.CurrentConfig.CoreCustomIconKey))
			{
				customIconItem = IconHelper.GetCustomIcons().FirstOrDefault((IconHelper.CustomIconItem c) => string.Equals(c.Key, ConfigManager.CurrentConfig.CoreCustomIconKey, StringComparison.OrdinalIgnoreCase));
			}
			bool flag2 = customIconItem != null && !customIconItem.IsSvg && File.Exists(customIconItem.FilePath);
			bool flag3 = !string.IsNullOrEmpty(ConfigManager.CurrentConfig.CoreCustomImagePath) && File.Exists(ConfigManager.CurrentConfig.CoreCustomImagePath);
			bool num16 = ((text6 == "Image") | flag2) || (flag3 && text6 != "Custom" && text6 != "Exit");
			string text7 = (flag2 ? customIconItem.FilePath : (flag3 ? ConfigManager.CurrentConfig.CoreCustomImagePath : null));
			double num17 = ((ConfigManager.CurrentConfig.CoreIconScale > 0.0) ? ConfigManager.CurrentConfig.CoreIconScale : 1.0);
			double coreImageOffsetX = ConfigManager.CurrentConfig.CoreImageOffsetX;
			double coreImageOffsetY = ConfigManager.CurrentConfig.CoreImageOffsetY;
			TranslateTransform renderTransform = ((coreImageOffsetX != 0.0 || coreImageOffsetY != 0.0) ? new TranslateTransform(coreImageOffsetX, coreImageOffsetY) : null);
			if (num16 && !string.IsNullOrEmpty(text7) && File.Exists(text7))
			{
				double num18 = num10 * 1.85;
				Ellipse ellipse = new Ellipse
				{
					Width = num18,
					Height = num18,
					HorizontalAlignment = System.Windows.HorizontalAlignment.Center,
					VerticalAlignment = VerticalAlignment.Center,
					IsHitTestVisible = false,
					Visibility = ((!ConfigManager.CurrentConfig.ShowCoreIcon) ? Visibility.Collapsed : Visibility.Visible)
				};
				try
				{
					BitmapImage bitmapImage = new BitmapImage();
					bitmapImage.BeginInit();
					bitmapImage.UriSource = new Uri(text7, UriKind.Absolute);
					bitmapImage.CacheOption = BitmapCacheOption.OnLoad;
					bitmapImage.EndInit();
					((Freezable)bitmapImage).Freeze();
					ImageBrush imageBrush = new ImageBrush(bitmapImage)
					{
						Stretch = ParseStretchMode(ConfigManager.CurrentConfig.CoreCustomImageStretch),
						AlignmentX = AlignmentX.Center,
						AlignmentY = AlignmentY.Center
					};
					TransformGroup transformGroup = new TransformGroup();
					if (Math.Abs(num17 - 1.0) > 0.001)
					{
						transformGroup.Children.Add(new ScaleTransform(num17, num17, num18 / 2.0, num18 / 2.0));
					}
					if (Math.Abs(coreImageOffsetX) > 0.001 || Math.Abs(coreImageOffsetY) > 0.001)
					{
						transformGroup.Children.Add(new TranslateTransform(coreImageOffsetX, coreImageOffsetY));
					}
					if (transformGroup.Children.Count > 0)
					{
						imageBrush.Transform = transformGroup;
					}
					RenderOptions.SetBitmapScalingMode((DependencyObject)(object)imageBrush, BitmapScalingMode.HighQuality);
					RenderOptions.SetEdgeMode((DependencyObject)(object)ellipse, EdgeMode.Unspecified);
					ellipse.Fill = imageBrush;
				}
				catch
				{
				}
				_previewCoreIconElement = ellipse;
				_previewCoreIconDefaultVisibility = ellipse.Visibility;
				_previewCoreIconDefaultOpacity = ellipse.Opacity;
				_previewCoreIconDefaultEffect = ellipse.Effect;
				_previewCoreUsesCustomImage = true;
				grid.Children.Add(ellipse);
			}
			else
			{
				_previewExitIcon = new System.Windows.Shapes.Path
				{
					Name = "CoreExitIcon",
					Data = IconHelper.GetCoreIconGeometry(text6, ConfigManager.CurrentConfig.CoreCustomIconKey, ConfigManager.CurrentConfig.CoreCustomIconSvg),
					Fill = _previewTextBrush,
					Width = num14 * num17,
					Height = num14 * num17,
					RenderTransform = renderTransform,
					Stretch = Stretch.Uniform,
					HorizontalAlignment = System.Windows.HorizontalAlignment.Center,
					VerticalAlignment = VerticalAlignment.Center,
					IsHitTestVisible = false,
					Visibility = ((!ConfigManager.CurrentConfig.ShowCoreIcon) ? Visibility.Collapsed : Visibility.Visible)
				};
				_previewCoreIconElement = _previewExitIcon;
				_previewCoreIconDefaultVisibility = _previewExitIcon.Visibility;
				_previewCoreIconDefaultOpacity = _previewExitIcon.Opacity;
				_previewCoreIconDefaultEffect = _previewExitIcon.Effect;
				_previewCoreUsesCustomImage = false;
				grid.Children.Add(_previewExitIcon);
			}
			_previewCoreSelectionOverlay = new Ellipse
			{
				Width = num10 * 2.0,
				Height = num10 * 2.0,
				Fill = CreateFrostedCoreBrush(_previewCoreBgBrush),
				Opacity = 0.92,
				Stroke = new SolidColorBrush(System.Windows.Media.Color.FromArgb(130, 255, 255, 255)),
				StrokeThickness = 1.1,
				Visibility = Visibility.Collapsed,
				IsHitTestVisible = false,
				Effect = new BlurEffect { Radius = 7.0, RenderingBias = RenderingBias.Performance }
			};
			grid.Children.Add(_previewCoreSelectionOverlay);
			double previewCoreFontSize = (ConfigManager.CurrentConfig?.CoreFontSize > 0.0)
				? ConfigManager.CurrentConfig.CoreFontSize
				: Math.Max(8.0, Math.Min(16.0, num10 / 4.0));
			Brush previewCoreTextBrush = (!string.IsNullOrWhiteSpace(ConfigManager.CurrentConfig?.CoreTextColor))
				? CreateBrushFromHexSafe(ConfigManager.CurrentConfig.CoreTextColor, _previewTextBrush)
				: _previewTextBrush;
			string previewCoreFontFamily = (!string.IsNullOrWhiteSpace(ConfigManager.CurrentConfig?.CoreFontFamily))
				? ConfigManager.CurrentConfig.CoreFontFamily
				: (ConfigManager.CurrentConfig?.WheelFontFamily ?? "Microsoft YaHei UI, Segoe UI");
			_previewCoreSelectionText = new TextBlock
			{
				Width = Math.Max(24.0, Math.Min(num10 * 1.75, 150.0)),
				Foreground = previewCoreTextBrush,
				FontSize = Math.Max(7.5, previewCoreFontSize * 0.82),
				FontFamily = new System.Windows.Media.FontFamily(previewCoreFontFamily),
				FontWeight = FontWeights.SemiBold,
				HorizontalAlignment = HorizontalAlignment.Center,
				VerticalAlignment = VerticalAlignment.Center,
				TextAlignment = TextAlignment.Center,
				TextWrapping = TextWrapping.Wrap,
				TextTrimming = TextTrimming.CharacterEllipsis,
				MaxHeight = Math.Max(24.0, num10 * 1.15),
				Visibility = Visibility.Collapsed,
				IsHitTestVisible = false,
				Effect = new DropShadowEffect
				{
					BlurRadius = 2.0,
					ShadowDepth = 1.0,
					Opacity = 0.4
				}
			};
			grid.Children.Add(_previewCoreSelectionText);
			_previewStyleRenderer.RenderDecorations(LiveWheelPreviewCanvas, grid, num, num2, num8, num10, 1);
			int num19 = ((wheelProfile.SectorCount > 0) ? wheelProfile.SectorCount : 8);
			double num20 = 360.0 / (double)num19;
			for (int num21 = 0; num21 < num19; num21++)
			{
				double num22 = (double)num21 * num20;
				double num23 = num22 - num20 / 2.0;
				double endAngle = num22 + num20 / 2.0;
				double num24 = num22 * (Math.PI / 180.0);
				double num25 = (num9 + num8) / 2.0;
				double num26 = num + Math.Cos(num24) * num25;
				double num27 = num2 + Math.Sin(num24) * num25;
				Geometry data = IconHelper.CreateAdvancedSectorGeometry(num, num2, num23, endAngle, num9, num8, shape, gap, cornerRadius);
				TranslateTransform translateTransform = new TranslateTransform(0.0, 0.0);
				System.Windows.Shapes.Path path = new System.Windows.Shapes.Path
				{
					Data = data,
					Fill = _previewDefaultBrush,
					Stroke = _previewBorderBrush,
					StrokeThickness = _previewStyleRenderer.BorderThickness,
					RenderTransform = translateTransform,
					Tag = num21,
					Cursor = System.Windows.Input.Cursors.Hand
				};
				int clickedSectorIndex = num21;
				path.MouseLeftButtonDown += (s, e) =>
				{
					e.Handled = true;
					OnPreviewSectorClicked(clickedSectorIndex);
				};
				if (_selectedLayoutSlotIndex == num21 && LayoutTargetSlotRadio != null && LayoutTargetSlotRadio.IsChecked == true)
				{
					path.Stroke = new SolidColorBrush(System.Windows.Media.Color.FromRgb(56, 189, 248));
					path.StrokeThickness = 2.2;
					path.Effect = new DropShadowEffect
					{
						Color = System.Windows.Media.Color.FromRgb(56, 189, 248),
						BlurRadius = 12.0,
						ShadowDepth = 0.0,
						Opacity = 0.95
					};
				}
				System.Windows.Controls.Panel.SetZIndex(path, 0);
				LiveWheelPreviewCanvas.Children.Add(path);
				_previewStyleRenderer?.ApplySectorHighlight(path, isHighlighted: false);
				_previewSectorPaths.Add(path);
				_previewTransforms.Add(translateTransform);
				_previewAngles.Add(num24);
				StackPanel stackPanel = new StackPanel
				{
					Orientation = System.Windows.Controls.Orientation.Vertical,
					HorizontalAlignment = System.Windows.HorizontalAlignment.Center,
					VerticalAlignment = VerticalAlignment.Center,
					IsHitTestVisible = false,
					RenderTransform = translateTransform
				};
				string text8 = "";
				string text9 = "";
				string text10 = "";
				string text11 = null;
				IconHelper.CustomIconItem customIconItem2 = null;
				ActionItem? action = (wheelProfile.Actions != null && num21 < wheelProfile.Actions.Count) ? wheelProfile.Actions[num21] : null;
				if (action != null)
				{
					text8 = action.Name ?? "";
					text9 = action.Type ?? "Hotkey";
					text10 = action.Parameter ?? "";
					if (!string.IsNullOrEmpty(action.CustomIconSvg))
					{
						text11 = action.CustomIconSvg;
					}
					else if (!string.IsNullOrEmpty(action.IconKey) && action.IconKey.StartsWith("custom:", StringComparison.OrdinalIgnoreCase))
					{
						customIconItem2 = IconHelper.GetCustomIcons().FirstOrDefault((IconHelper.CustomIconItem c) => string.Equals(c.Key, action.IconKey, StringComparison.OrdinalIgnoreCase));
						if (customIconItem2 != null && customIconItem2.IsSvg)
						{
							text11 = customIconItem2.SvgData;
						}
					}
					if (string.IsNullOrEmpty(text11) && customIconItem2 == null)
					{
						if (!string.IsNullOrEmpty(action.IconKey))
						{
							text11 = IconHelper.GetSvgPathByKey(action.IconKey);
						}
						else
						{
							switch (text9)
							{
							case "Folder":
							case "OpenFolder":
								text11 = IconHelper.GetSvgPathByKey("Folder");
								break;
							case "System":
								if (!string.IsNullOrEmpty(text10))
								{
									text11 = IconHelper.GetSvgPathByKey(text10);
								}
								break;
							}
						}
					}
				}

				string sectorLayout = text3;
				if (action != null && !string.IsNullOrWhiteSpace(action.LayoutMode) && action.LayoutMode != "Inherit")
				{
					sectorLayout = action.LayoutMode;
				}
				bool shouldShowIcon = (sectorLayout != "TextOnly");
				bool shouldShowText = (sectorLayout != "IconOnly");

				Brush sectorPreviewTextBrush = _previewTextBrush;
				if (action != null && !string.IsNullOrWhiteSpace(action.CustomTextColor))
				{
					sectorPreviewTextBrush = CreateBrushFromHexSafe(action.CustomTextColor, _previewTextBrush);
				}

				if (shouldShowIcon)
				{
					double baseIconSize = (action != null && action.CustomIconSize.HasValue && action.CustomIconSize.Value > 0.0)
						? action.CustomIconSize.Value
						: ((ConfigManager.CurrentConfig.SectorIconSize > 0.0) ? ConfigManager.CurrentConfig.SectorIconSize : 20.0);
					double num29 = num19 switch
					{
						4 => 1.2, 
						12 => 0.8, 
						_ => 1.0, 
					};
					double num30 = ((sectorLayout == "IconOnly") ? (baseIconSize * 1.35) : baseIconSize) * 0.72 * num29 * (num7 / (135.0 / num5));
					if (!string.IsNullOrEmpty(text11))
					{
						try
						{
							System.Windows.Shapes.Path element = new System.Windows.Shapes.Path
							{
								Data = Geometry.Parse(text11),
								Fill = sectorPreviewTextBrush,
								Width = num30,
								Height = num30,
								Stretch = Stretch.Uniform,
								HorizontalAlignment = System.Windows.HorizontalAlignment.Center,
								Margin = new Thickness(0.0, 0.0, 0.0, shouldShowText ? 1 : 0)
							};
							stackPanel.Children.Add(element);
						}
						catch
						{
						}
					}
					else if (customIconItem2 != null && !customIconItem2.IsSvg)
					{
						ImageSource customImageSource = IconHelper.GetCustomImageSource(customIconItem2.FilePath);
						if (customImageSource != null)
						{
							System.Windows.Controls.Image element2 = new System.Windows.Controls.Image
							{
								Source = customImageSource,
								Width = num30,
								Height = num30,
								Stretch = Stretch.Uniform,
								HorizontalAlignment = System.Windows.HorizontalAlignment.Center,
								Margin = new Thickness(0.0, 0.0, 0.0, shouldShowText ? 1 : 0)
							};
							stackPanel.Children.Add(element2);
						}
					}
					else if (text9 == "Launch" && !string.IsNullOrEmpty(text10))
					{
						BitmapSource icon = IconHelper.GetIcon(text10);
						if (icon != null)
						{
							System.Windows.Controls.Image element3 = new System.Windows.Controls.Image
							{
								Source = icon,
								Width = num30,
								Height = num30,
								Stretch = Stretch.Uniform,
								HorizontalAlignment = System.Windows.HorizontalAlignment.Center,
								Margin = new Thickness(0.0, 0.0, 0.0, shouldShowText ? 1 : 0)
							};
							stackPanel.Children.Add(element3);
						}
					}
					else
					{
						try
						{
							System.Windows.Shapes.Path element4 = new System.Windows.Shapes.Path
							{
								Data = Geometry.Parse("M19,15H5V5H19M19,3H5C3.89,3 3,3.89 3,5V15C3,16.1 3.89,17 5,17H19C20.1,17 21,16.1 21,15V5C21,3.89 20.1,3 19,3M2,18H22V20H2V18Z"),
								Fill = sectorPreviewTextBrush,
								Width = num30,
								Height = num30,
								Stretch = Stretch.Uniform,
								HorizontalAlignment = System.Windows.HorizontalAlignment.Center,
								Margin = new Thickness(0.0, 0.0, 0.0, shouldShowText ? 1 : 0)
							};
							stackPanel.Children.Add(element4);
						}
						catch
						{
						}
					}
				}
				if (shouldShowText && !string.IsNullOrEmpty(text8))
				{
					double baseFontSize = (action != null && action.CustomFontSize.HasValue && action.CustomFontSize.Value > 0.0)
						? action.CustomFontSize.Value
						: ((ConfigManager.CurrentConfig.SectorFontSize > 0.0) ? ConfigManager.CurrentConfig.SectorFontSize : 11.0);
					double num32 = num19 switch
					{
						4 => 1.2, 
						12 => 0.8, 
						_ => 1.0, 
					};
					double val2 = ((sectorLayout == "TextOnly") ? (baseFontSize + 1.2) : baseFontSize) * 0.82 * num32 * (num7 / (135.0 / num5));
					double maxWidth = num19 switch
					{
						4 => 96.0, 
						12 => 52.0, 
						_ => 80.0, 
					} * num7;
					string sectorFont = (action != null && !string.IsNullOrWhiteSpace(action.CustomFontFamily))
						? action.CustomFontFamily
						: (ConfigManager.CurrentConfig.WheelFontFamily ?? "Microsoft YaHei UI, Segoe UI");
					TextBlock element5 = new TextBlock
					{
						Text = text8,
						FontSize = Math.Max(6.0, val2),
						FontFamily = new System.Windows.Media.FontFamily(sectorFont),
						Foreground = sectorPreviewTextBrush,
						FontWeight = (sectorLayout == "TextOnly") ? FontWeights.SemiBold : FontWeights.Medium,
						HorizontalAlignment = System.Windows.HorizontalAlignment.Center,
						TextAlignment = TextAlignment.Center,
						TextTrimming = TextTrimming.CharacterEllipsis,
						MaxWidth = maxWidth
					};
					stackPanel.Children.Add(element5);
				}
				double num33 = num19 switch
				{
					4 => 96.0, 
					12 => 56.0, 
					_ => 80.0, 
				} * num7;
				double num34 = num19 switch
				{
					4 => 64.0, 
					12 => 44.0, 
					_ => 54.0, 
				} * num7;
				Grid grid2 = new Grid
				{
					Width = num33,
					Height = num34,
					IsHitTestVisible = false,
					RenderTransform = translateTransform
				};
				grid2.Children.Add(stackPanel);
				Canvas.SetLeft(grid2, num26 - num33 / 2.0);
				Canvas.SetTop(grid2, num27 - num34 / 2.0);
				System.Windows.Controls.Panel.SetZIndex(grid2, 10);
				LiveWheelPreviewCanvas.Children.Add(grid2);

				bool isTier2Mode = (Tier2ConfigSegmentRadio != null && Tier2ConfigSegmentRadio.IsChecked == true);
				if (!enableMultiTier || !isTier2Mode || wheelProfile.Actions == null || num21 >= wheelProfile.Actions.Count || wheelProfile.Actions[num21] == null)
				{
					continue;
				}
				ActionItem actionItem = wheelProfile.Actions[num21];
				if (actionItem.SubActions == null || actionItem.SubActions.Count <= 0)
				{
					continue;
				}
				bool isFan = string.Equals(ConfigManager.CurrentConfig.SubmenuStyle, "Fan", StringComparison.OrdinalIgnoreCase);
				int count = actionItem.SubActions.Count;
				int activeCount = isFan ? Math.Min(3, count) : count;
				double num35 = num20 / (double)count;
				for (int num36 = 0; num36 < activeCount; num36++)
				{
					double num37 = num23 + (double)num36 * num35;
					double num38 = num37 + num35;
					double num39 = (num37 + num38) / 2.0 * (Math.PI / 180.0);
					double num40 = (num12 + num13) / 2.0;
					double num41 = num + Math.Cos(num39) * num40;
					double num42 = num2 + Math.Sin(num39) * num40;

					Geometry data2;
					if (isFan)
					{
						int slot = RadialWindow.GetFanSlotIndex(num36, activeCount);
						var (du, dv) = RadialWindow.GetFanSubOffsetForShape(ConfigManager.CurrentConfig.Shape, slot);
						double ratio = (ConfigManager.CurrentConfig.SubWheelOuterRadius > 0.0 && ConfigManager.CurrentConfig.WheelRadius > 0.0)
							? (ConfigManager.CurrentConfig.SubWheelOuterRadius / (ConfigManager.CurrentConfig.WheelRadius * 1.55))
							: 1.0;
						double itemR_sub = (num8 - num9) * 0.40 * Math.Max(0.5, Math.Min(2.5, ratio));
						double R_sub = ((num9 + num8) / 2.0 * ratio) + num11;
						double ux = Math.Cos(num24), uy = Math.Sin(num24);
						double vx = -Math.Sin(num24), vy = Math.Cos(num24);
						num41 = num + ux * (du * R_sub) + vx * (dv * R_sub);
						num42 = num2 + uy * (du * R_sub) + vy * (dv * R_sub);
						data2 = RadialWindow.CreateSubMenuGeometry(shape, num41, num42, itemR_sub, num24, num, num2, cornerRadius2);
					}
					else
					{
						data2 = IconHelper.CreateAdvancedSectorGeometry(num, num2, num37, num38, num12, num13, shape, num11, cornerRadius2);
					}

					TranslateTransform translateTransform2 = new TranslateTransform(0.0, 0.0);
					bool isSelectedSub = (LayoutTargetSlotRadio != null && LayoutTargetSlotRadio.IsChecked == true && _selectedLayoutSlotIndex >= 0 && _selectedLayoutTier == 2 && num21 == _selectedLayoutSlotIndex && num36 == _selectedLayoutSubSlotIndex);
					System.Windows.Shapes.Path path2 = new System.Windows.Shapes.Path
					{
						Data = data2,
						Fill = _previewSubDefaultBrush,
						Stroke = isSelectedSub ? new SolidColorBrush(System.Windows.Media.Color.FromRgb(56, 189, 248)) : _previewSubBorderBrush,
						StrokeThickness = isSelectedSub ? 2.4 : (_previewSubStyleRenderer?.BorderThickness ?? _previewStyleRenderer?.BorderThickness ?? 1.2),
						RenderTransform = translateTransform2,
						Tag = $"sub_{num21}_{num36}",
						Opacity = 0.95,
						Cursor = System.Windows.Input.Cursors.Hand,
						Effect = isSelectedSub ? new DropShadowEffect
						{
							Color = System.Windows.Media.Color.FromRgb(56, 189, 248),
							BlurRadius = 14.0,
							ShadowDepth = 0.0,
							Opacity = 0.95
						} : null
					};
					int clickedParentIdx = num21;
					int clickedSubIdx = num36;
					path2.MouseLeftButtonDown += (s, e) =>
					{
						e.Handled = true;
						OnPreviewSubSectorClicked(clickedParentIdx, clickedSubIdx);
					};
					if (!isSelectedSub)
					{
						_previewSubStyleRenderer?.ApplySectorHighlight(path2, isHighlighted: false);
					}
					System.Windows.Controls.Panel.SetZIndex(path2, isSelectedSub ? 25 : 15);
					LiveWheelPreviewCanvas.Children.Add(path2);
					_previewSubSectorPaths.Add(path2);
					_previewSubTransforms.Add(translateTransform2);
					_previewSubParentIndices.Add(num21);
					_previewSubIndices.Add(num36);
					_previewSubAngles.Add(isFan ? num24 : num39);
					ActionItem actionItem2 = actionItem.SubActions[num36];
					StackPanel stackPanel2 = new StackPanel
					{
						Orientation = Orientation.Vertical,
						HorizontalAlignment = HorizontalAlignment.Center,
						VerticalAlignment = VerticalAlignment.Center,
						IsHitTestVisible = false,
						RenderTransform = translateTransform2
					};

					string subLayout = (actionItem2 != null && !string.IsNullOrWhiteSpace(actionItem2.LayoutMode) && actionItem2.LayoutMode != "Inherit")
						? actionItem2.LayoutMode
						: (ConfigManager.CurrentConfig.IconLayoutMode ?? "IconAndText");
					bool subShouldShowIcon = (subLayout != "TextOnly");
					bool subShouldShowText = (subLayout != "IconOnly");
					Brush subSectorPreviewTextBrush = _previewSubTextBrush;
					if (actionItem2 != null && !string.IsNullOrWhiteSpace(actionItem2.CustomTextColor))
					{
						subSectorPreviewTextBrush = CreateBrushFromHexSafe(actionItem2.CustomTextColor, _previewSubTextBrush);
					}

					if (subShouldShowIcon)
					{
						double subBaseIconSize = (actionItem2 != null && actionItem2.CustomIconSize.HasValue && actionItem2.CustomIconSize.Value > 0.0)
							? actionItem2.CustomIconSize.Value
							: ((ConfigManager.CurrentConfig.SubWheelIconSize > 0.0) ? ConfigManager.CurrentConfig.SubWheelIconSize : 18.0);
						double num43 = ((subLayout == "IconOnly") ? (subBaseIconSize * 1.35) : subBaseIconSize) * 0.65 * num7;
						string text12 = null;
						if (!string.IsNullOrEmpty(actionItem2.CustomIconSvg))
						{
							text12 = actionItem2.CustomIconSvg;
						}
						else if (!string.IsNullOrEmpty(actionItem2.IconKey))
						{
							text12 = IconHelper.GetSvgPathByKey(actionItem2.IconKey);
						}
						else if (actionItem2.Type == "Folder" || actionItem2.Type == "OpenFolder")
						{
							text12 = IconHelper.GetSvgPathByKey("Folder");
						}
						else if (actionItem2.Type == "System" && !string.IsNullOrEmpty(actionItem2.Parameter))
						{
							text12 = IconHelper.GetSvgPathByKey(actionItem2.Parameter);
						}
						if (!string.IsNullOrEmpty(text12))
						{
							try
							{
								System.Windows.Shapes.Path element6 = new System.Windows.Shapes.Path
								{
									Data = Geometry.Parse(text12),
									Fill = subSectorPreviewTextBrush,
									Width = num43,
									Height = num43,
									Stretch = Stretch.Uniform,
									HorizontalAlignment = HorizontalAlignment.Center,
									Margin = new Thickness(0, 0, 0, subShouldShowText ? 1 : 0)
								};
								stackPanel2.Children.Add(element6);
							}
							catch
							{
							}
						}
						else if (actionItem2.Type == "Launch" && !string.IsNullOrEmpty(actionItem2.Parameter))
						{
							BitmapSource icon2 = IconHelper.GetIcon(actionItem2.Parameter);
							if (icon2 != null)
							{
								Image element7 = new Image
								{
									Source = icon2,
									Width = num43,
									Height = num43,
									Stretch = Stretch.Uniform,
									HorizontalAlignment = HorizontalAlignment.Center,
									Margin = new Thickness(0, 0, 0, subShouldShowText ? 1 : 0)
								};
								stackPanel2.Children.Add(element7);
							}
						}
					}
					if (subShouldShowText && !string.IsNullOrEmpty(actionItem2.Name))
					{
						double subBaseFontSize = (actionItem2 != null && actionItem2.CustomFontSize.HasValue && actionItem2.CustomFontSize.Value > 0.0)
							? actionItem2.CustomFontSize.Value
							: ((ConfigManager.CurrentConfig.SubWheelFontSize > 0.0) ? ConfigManager.CurrentConfig.SubWheelFontSize : 10.0);
						string subFontFamily = (actionItem2 != null && !string.IsNullOrWhiteSpace(actionItem2.CustomFontFamily))
							? actionItem2.CustomFontFamily
							: (ConfigManager.CurrentConfig.WheelFontFamily ?? "Microsoft YaHei UI, Segoe UI");
						TextBlock element8 = new TextBlock
						{
							Text = actionItem2.Name,
							FontSize = Math.Max(5.0, ((subLayout == "TextOnly") ? (subBaseFontSize + 1.0) : subBaseFontSize) * 0.75 * num7),
							FontFamily = new FontFamily(subFontFamily),
							Foreground = subSectorPreviewTextBrush,
							FontWeight = (subLayout == "TextOnly") ? FontWeights.SemiBold : FontWeights.Normal,
							HorizontalAlignment = HorizontalAlignment.Center,
							TextAlignment = TextAlignment.Center,
							TextTrimming = TextTrimming.CharacterEllipsis,
							MaxWidth = 64.0 * num7
						};
						stackPanel2.Children.Add(element8);
					}
					double num45 = 68.0 * num7;
					double num46 = 34.0 * num7;
					Grid grid3 = new Grid
					{
						Width = num45,
						Height = num46,
						IsHitTestVisible = false,
						RenderTransform = translateTransform2,
						Opacity = 1.0
					};
					grid3.Children.Add(stackPanel2);
					Canvas.SetLeft(grid3, num41 - num45 / 2.0);
					Canvas.SetTop(grid3, num42 - num46 / 2.0);
					System.Windows.Controls.Panel.SetZIndex(grid3, 50);
					LiveWheelPreviewCanvas.Children.Add(grid3);
					_previewSubContainers.Add(grid3);
				}
			}
			Canvas.SetLeft(grid, num - num10);
			Canvas.SetTop(grid, num2 - num10);
			System.Windows.Controls.Panel.SetZIndex(grid, 15);
			LiveWheelPreviewCanvas.Children.Add(grid);
		}
		catch (Exception)
		{
		}
		finally
		{
			_isRenderingPreview = false;
		}
	}

	private static Stretch ParseStretchMode(string? stretch)
	{
		if (string.Equals(stretch, "Uniform", StringComparison.OrdinalIgnoreCase))
		{
			return Stretch.Uniform;
		}
		if (string.Equals(stretch, "Fill", StringComparison.OrdinalIgnoreCase))
		{
			return Stretch.Fill;
		}
		if (string.Equals(stretch, "None", StringComparison.OrdinalIgnoreCase))
		{
			return Stretch.None;
		}
		return Stretch.UniformToFill;
	}

	private static System.Windows.Media.Brush CreateFrostedCoreBrush(System.Windows.Media.Brush baseBrush)
	{
		System.Windows.Media.Color baseColor = (baseBrush as SolidColorBrush)?.Color ?? System.Windows.Media.Color.FromRgb(36, 44, 60);
		double luminance = (0.2126 * baseColor.R) + (0.7152 * baseColor.G) + (0.0722 * baseColor.B);
		if (luminance >= 165.0)
		{
			return new SolidColorBrush(System.Windows.Media.Color.FromArgb(88, byte.MaxValue, byte.MaxValue, byte.MaxValue));
		}

		return new SolidColorBrush(System.Windows.Media.Color.FromArgb(
			112,
			(byte)Math.Min(255, baseColor.R + 48),
			(byte)Math.Min(255, baseColor.G + 56),
			(byte)Math.Min(255, baseColor.B + 72)));
	}

	private static System.Windows.Media.Brush CreateBrushFromHexSafe(string? hex, System.Windows.Media.Brush fallback)
	{
		if (string.IsNullOrWhiteSpace(hex))
		{
			return fallback;
		}
		try
		{
			return new SolidColorBrush((System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString(hex));
		}
		catch
		{
			return fallback;
		}
	}

	private void UpdatePreviewCoreSelection(int mainIndex, int subIndex, WheelProfile? wheelProfile)
	{
		bool shouldShow = ConfigManager.CurrentConfig?.ShowSelectedActionText == true && mainIndex >= 0 && wheelProfile != null;
		string selectedName = string.Empty;
		if (shouldShow && wheelProfile!.Actions != null && mainIndex < wheelProfile.Actions.Count)
		{
			ActionItem action = wheelProfile.Actions[mainIndex];
			if (subIndex >= 0 && subIndex < _previewSubIndices.Count && subIndex < _previewSubParentIndices.Count && _previewSubParentIndices[subIndex] == mainIndex)
			{
				int localSubIndex = _previewSubIndices[subIndex];
				if (action?.SubActions != null && localSubIndex >= 0 && localSubIndex < action.SubActions.Count)
				{
					selectedName = action.SubActions[localSubIndex]?.Name ?? string.Empty;
				}
			}
			if (string.IsNullOrWhiteSpace(selectedName))
			{
				selectedName = action?.Name ?? string.Empty;
			}
		}

		if (shouldShow && !string.IsNullOrWhiteSpace(selectedName) && _previewCoreSelectionOverlay != null && _previewCoreSelectionText != null)
		{
			_previewCoreSelectionText.Text = selectedName;
			_previewCoreSelectionOverlay.Visibility = Visibility.Visible;
			_previewCoreSelectionText.Visibility = Visibility.Visible;
			if (_previewCoreIconElement != null)
			{
				_previewCoreIconElement.Opacity = _previewCoreUsesCustomImage ? _previewCoreIconDefaultOpacity : 0.18;
				_previewCoreIconElement.Effect = _previewCoreUsesCustomImage
					? new BlurEffect { Radius = 5.5, RenderingBias = RenderingBias.Performance }
					: _previewCoreIconDefaultEffect;
			}
			return;
		}

		if (_previewCoreSelectionOverlay != null)
		{
			_previewCoreSelectionOverlay.Visibility = Visibility.Collapsed;
		}
		if (_previewCoreSelectionText != null)
		{
			_previewCoreSelectionText.Visibility = Visibility.Collapsed;
		}
		if (_previewCoreIconElement != null)
		{
			_previewCoreIconElement.Visibility = _previewCoreIconDefaultVisibility;
			_previewCoreIconElement.Opacity = _previewCoreIconDefaultOpacity;
			_previewCoreIconElement.Effect = _previewCoreIconDefaultEffect;
		}
	}

	private void LiveWheelPreviewCanvas_MouseMove(object sender, System.Windows.Input.MouseEventArgs e)
	{
		if (_previewSectorPaths.Count == 0 || ConfigManager.CurrentConfig == null)
		{
			return;
		}
		try
		{
			Point position = e.GetPosition(LiveWheelPreviewCanvas);
			double num = position.X - 150.0;
			double num2 = position.Y - 150.0;
			double num3 = Math.Sqrt(num * num + num2 * num2);
			bool enableMultiTier = ConfigManager.CurrentConfig.EnableMultiTier;
			double num4 = ((ConfigManager.CurrentConfig.SubWheelRadiusRatio > 1.1) ? ConfigManager.CurrentConfig.SubWheelRadiusRatio : 1.45);
			WheelProfile wheelProfile = _selectedProfile ?? ConfigManager.CurrentConfig.Profiles.FirstOrDefault();
			bool num5 = enableMultiTier && wheelProfile?.Actions != null && wheelProfile.Actions.Any((ActionItem a) => a != null && a.SubActions != null && a.SubActions.Count > 0);
			double num6 = Math.Max(80.0, ConfigManager.CurrentConfig.WheelRadius);
			double num7 = ((ConfigManager.CurrentConfig.SubWheelOuterRadius > 0.0) ? ConfigManager.CurrentConfig.SubWheelOuterRadius : (ConfigManager.CurrentConfig.WheelRadius * num4));
			double val = (num5 ? (Math.Max(num6, num7) + 10.0) : num6);
			double num8 = 135.0 / Math.Max(135.0, val);
			double num9 = ConfigManager.CurrentConfig.WheelRadius * num8;
			double num10 = ConfigManager.CurrentConfig.InnerRadius * num8;
			double num11 = ConfigManager.CurrentConfig.CoreRadius * num8;
			double num12 = Math.Max(0.0, ((ConfigManager.CurrentConfig.SubWheelInnerGap >= 0.0) ? ConfigManager.CurrentConfig.SubWheelInnerGap : 4.0) * num8);
			double num13 = num9 + num12 + 2.0;
			double num14 = Math.Max(num13 + 10.0, num7 * num8);
			int num15 = -2;
			int num16 = -1;
			bool isFan = string.Equals(ConfigManager.CurrentConfig.SubmenuStyle, "Fan", StringComparison.OrdinalIgnoreCase);
			bool isTier2Mode = (Tier2ConfigSegmentRadio != null && Tier2ConfigSegmentRadio.IsChecked == true);

			if (num3 <= num11)
			{
				num15 = -1;
			}
			else if (num3 >= num10 * 0.75)
			{
				double num17 = (Math.Atan2(num2, num) * (180.0 / Math.PI) + 360.0) % 360.0;
				double num18 = 360.0 / (double)_previewSectorPaths.Count;
				num15 = (int)Math.Floor((num17 + num18 / 2.0) / num18) % _previewSectorPaths.Count;

				if (enableMultiTier && num15 >= 0)
				{
					for (int num22 = 0; num22 < _previewSubSectorPaths.Count; num22++)
					{
						if (_previewSubParentIndices[num22] == num15)
						{
							System.Windows.Shapes.Path path = _previewSubSectorPaths[num22];
							if (path.Data != null && path.Data.FillContains(position))
							{
								num16 = num22;
								break;
							}
						}
					}
				}
			}

			if (num15 == _lastHoveredSector && num16 == _lastHoveredSubIndex)
			{
				return;
			}
			_lastHoveredSector = num15;
			_lastHoveredSubIndex = num16;
			UpdatePreviewCoreSelection(num15, num16, wheelProfile);

			for (int num23 = 0; num23 < _previewSectorPaths.Count; num23++)
			{
				System.Windows.Shapes.Path path2 = _previewSectorPaths[num23];
				TranslateTransform translateTransform = _previewTransforms[num23];
				double num24 = _previewAngles[num23];
				if (num23 == num15)
				{
					path2.Fill = _previewHighlightBrush;
					path2.Stroke = _previewHighlightBorderBrush;
					path2.StrokeThickness = _previewStyleRenderer?.HighlightBorderThickness ?? 2.0;
					_previewStyleRenderer?.ApplySectorHighlight(path2, isHighlighted: true);
					translateTransform.X = Math.Cos(num24) * 4.0;
					translateTransform.Y = Math.Sin(num24) * 4.0;
				}
				else
				{
					path2.Fill = _previewDefaultBrush;
					path2.Stroke = _previewBorderBrush;
					path2.StrokeThickness = _previewStyleRenderer?.BorderThickness ?? 1.5;
					_previewStyleRenderer?.ApplySectorHighlight(path2, isHighlighted: false);
					translateTransform.X = 0.0;
					translateTransform.Y = 0.0;
				}
			}
			ApplyPreviewSelectedVisuals();

			for (int num25 = 0; num25 < _previewSubSectorPaths.Count; num25++)
			{
				System.Windows.Shapes.Path path3 = _previewSubSectorPaths[num25];
				TranslateTransform translateTransform2 = _previewSubTransforms[num25];
				int num26 = _previewSubParentIndices[num25];
				double num27 = _previewSubAngles[num25];
				Grid grid = ((num25 < _previewSubContainers.Count) ? _previewSubContainers[num25] : null);

				if (num25 == num16)
				{
					path3.Fill = _previewSubHighlightBrush ?? _previewHighlightBrush;
					path3.Stroke = _previewSubHighlightBorderBrush ?? _previewHighlightBorderBrush;
					path3.StrokeThickness = (_previewSubStyleRenderer?.HighlightBorderThickness ?? _previewStyleRenderer?.HighlightBorderThickness ?? 2.0);
					_previewSubStyleRenderer?.ApplySectorHighlight(path3, isHighlighted: true);
					path3.Opacity = 1.0;
					if (grid != null) grid.Opacity = 1.0;
					System.Windows.Controls.Panel.SetZIndex(path3, 20);
					translateTransform2.X = Math.Cos(num27) * 4.0;
					translateTransform2.Y = Math.Sin(num27) * 4.0;
					ApplySubSectorGlow(path3, isHighlighted: true);
					if (grid != null)
					{
						System.Windows.Controls.Panel.SetZIndex(grid, 50);
						if (grid.Children.Count > 0 && grid.Children[0] is StackPanel stackPanel)
						{
							foreach (object child in stackPanel.Children)
							{
								if (child is System.Windows.Shapes.Path path4)
								{
									path4.Fill = Brushes.White;
								}
								else if (child is TextBlock textBlock)
								{
									textBlock.Foreground = Brushes.White;
									textBlock.FontWeight = FontWeights.SemiBold;
								}
							}
						}
					}
					continue;
				}

				if (num26 == num15)
				{
					path3.Fill = _previewSubDefaultBrush ?? _previewDefaultBrush;
					path3.Stroke = _previewSubHighlightBorderBrush ?? _previewHighlightBorderBrush;
					path3.StrokeThickness = (_previewSubStyleRenderer?.BorderThickness ?? _previewStyleRenderer?.BorderThickness ?? 1.5);
					_previewSubStyleRenderer?.ApplySectorHighlight(path3, isHighlighted: false);
					path3.Opacity = 1.0;
					if (grid != null) grid.Opacity = 1.0;
					System.Windows.Controls.Panel.SetZIndex(path3, 15);
					translateTransform2.X = 0.0;
					translateTransform2.Y = 0.0;
					ApplySubSectorGlow(path3, isHighlighted: false);
					if (grid != null)
					{
						System.Windows.Controls.Panel.SetZIndex(grid, 50);
						if (grid.Children.Count > 0 && grid.Children[0] is StackPanel stackPanel2)
						{
							foreach (object child2 in stackPanel2.Children)
							{
								if (child2 is System.Windows.Shapes.Path path5)
								{
									path5.Fill = _previewSubTextBrush ?? _previewTextBrush;
								}
								else if (child2 is TextBlock textBlock2)
								{
									textBlock2.Foreground = _previewSubTextBrush ?? _previewTextBrush;
									textBlock2.FontWeight = FontWeights.Normal;
								}
							}
						}
					}
					continue;
				}

				path3.Fill = _previewSubDefaultBrush ?? _previewDefaultBrush;
				path3.Stroke = _previewSubBorderBrush ?? _previewBorderBrush;
				path3.StrokeThickness = (_previewSubStyleRenderer?.BorderThickness ?? _previewStyleRenderer?.BorderThickness ?? 1.2);
				_previewSubStyleRenderer?.ApplySectorHighlight(path3, isHighlighted: false);
				path3.Opacity = (isFan ? (isTier2Mode ? 0.95 : 0.0) : 0.85);
				if (grid != null) grid.Opacity = (isFan ? (isTier2Mode ? 1.0 : 0.0) : 0.85);
				System.Windows.Controls.Panel.SetZIndex(path3, 15);
				translateTransform2.X = 0.0;
				translateTransform2.Y = 0.0;
				ApplySubSectorGlow(path3, isHighlighted: false);
				if (grid != null)
				{
					System.Windows.Controls.Panel.SetZIndex(grid, 50);
					if (grid.Children.Count > 0 && grid.Children[0] is StackPanel stackPanel3)
					{
						foreach (object child3 in stackPanel3.Children)
						{
							if (child3 is System.Windows.Shapes.Path path6)
							{
								path6.Fill = _previewSubTextBrush ?? _previewTextBrush;
							}
							else if (child3 is TextBlock textBlock3)
							{
								textBlock3.Foreground = _previewSubTextBrush ?? _previewTextBrush;
								textBlock3.FontWeight = FontWeights.Normal;
							}
						}
					}
				}
			}
			ApplyPreviewSelectedVisuals();
		}
		catch (Exception)
		{
		}
	}

	private void LiveWheelPreviewCanvas_MouseLeave(object sender, System.Windows.Input.MouseEventArgs e)
	{
		try
		{
			_lastHoveredSector = -2;
			_lastHoveredSubIndex = -2;
			UpdatePreviewCoreSelection(-1, -1, _selectedProfile ?? ConfigManager.CurrentConfig?.Profiles.FirstOrDefault());
			for (int i = 0; i < _previewSectorPaths.Count; i++)
			{
				System.Windows.Shapes.Path path = _previewSectorPaths[i];
				TranslateTransform translateTransform = _previewTransforms[i];
				path.Fill = _previewDefaultBrush;
				path.Stroke = _previewBorderBrush;
				path.StrokeThickness = _previewStyleRenderer?.BorderThickness ?? 1.5;
				_previewStyleRenderer?.ApplySectorHighlight(path, isHighlighted: false);
				translateTransform.X = 0.0;
				translateTransform.Y = 0.0;
			}

			bool isFan = (ConfigManager.CurrentConfig?.SubmenuStyle ?? "Wheel") == "Fan";
			bool isTier2Mode = (Tier2ConfigSegmentRadio?.IsChecked == true);
			for (int num28 = 0; num28 < _previewSubSectorPaths.Count; num28++)
			{
				System.Windows.Shapes.Path path4 = _previewSubSectorPaths[num28];
				path4.Fill = _previewSubDefaultBrush ?? _previewDefaultBrush;
				path4.Stroke = _previewSubBorderBrush ?? _previewBorderBrush;
				path4.StrokeThickness = (_previewSubStyleRenderer?.BorderThickness ?? _previewStyleRenderer?.BorderThickness ?? 1.2);
				_previewSubStyleRenderer?.ApplySectorHighlight(path4, isHighlighted: false);
				ApplySubSectorGlow(path4, isHighlighted: false);
				path4.Opacity = (isFan ? (isTier2Mode ? 0.95 : 0.0) : 0.85);
				if (num28 < _previewSubContainers.Count && _previewSubContainers[num28] != null)
				{
					_previewSubContainers[num28].Opacity = (isFan ? (isTier2Mode ? 1.0 : 0.0) : 0.85);
				}
			}
			ApplyPreviewSelectedVisuals();

			if (_previewCoreCircle != null)
			{
				_previewCoreCircle.Fill = _previewCoreBgBrush;
			}
			if (_previewExitIcon != null)
			{
				_previewExitIcon.Fill = _previewTextBrush;
			}
			if (_previewCoreScale != null)
			{
				DoubleAnimation animation = new DoubleAnimation(1.0, TimeSpan.FromMilliseconds(120.0));
				_previewCoreScale.BeginAnimation(ScaleTransform.ScaleXProperty, animation);
				_previewCoreScale.BeginAnimation(ScaleTransform.ScaleYProperty, animation);
			}
		}
		catch
		{
		}
	}

	private void ApplyPreviewSelectedVisuals()
	{
		bool isSlotMode = (LayoutTargetSlotRadio != null && LayoutTargetSlotRadio.IsChecked == true && _selectedLayoutSlotIndex >= 0);
		
		if (_previewSectorPaths != null)
		{
			for (int i = 0; i < _previewSectorPaths.Count; i++)
			{
				System.Windows.Shapes.Path path = _previewSectorPaths[i];
				if (isSlotMode && _selectedLayoutTier == 1 && i == _selectedLayoutSlotIndex)
				{
					path.Stroke = new SolidColorBrush(System.Windows.Media.Color.FromRgb(56, 189, 248));
					path.StrokeThickness = 2.4;
					path.Effect = new DropShadowEffect
					{
						Color = System.Windows.Media.Color.FromRgb(56, 189, 248),
						BlurRadius = 14.0,
						ShadowDepth = 0.0,
						Opacity = 0.95
					};
					System.Windows.Controls.Panel.SetZIndex(path, 10);
				}
				else if (isSlotMode && _selectedLayoutTier == 2 && i == _selectedLayoutSlotIndex)
				{
					// 二级定制时，父级扇区带有柔和天蓝关联指示轮廓
					path.Stroke = new SolidColorBrush(System.Windows.Media.Color.FromArgb(200, 56, 189, 248));
					path.StrokeThickness = 1.8;
					path.Effect = new DropShadowEffect
					{
						Color = System.Windows.Media.Color.FromRgb(56, 189, 248),
						BlurRadius = 8.0,
						ShadowDepth = 0.0,
						Opacity = 0.55
					};
					System.Windows.Controls.Panel.SetZIndex(path, 8);
				}
				else if (i != _lastHoveredSector)
				{
					path.Stroke = _previewBorderBrush;
					path.StrokeThickness = _previewStyleRenderer?.BorderThickness ?? 1.5;
					path.Effect = null;
					System.Windows.Controls.Panel.SetZIndex(path, 0);
				}
			}
		}

		if (_previewSubSectorPaths != null)
		{
			for (int j = 0; j < _previewSubSectorPaths.Count; j++)
			{
				System.Windows.Shapes.Path subPath = _previewSubSectorPaths[j];
				int pIdx = (j < _previewSubParentIndices.Count) ? _previewSubParentIndices[j] : -1;
				int sIdx = (j < _previewSubIndices.Count) ? _previewSubIndices[j] : -1;

				if (isSlotMode && _selectedLayoutTier == 2 && pIdx == _selectedLayoutSlotIndex && sIdx == _selectedLayoutSubSlotIndex)
				{
					subPath.Stroke = new SolidColorBrush(System.Windows.Media.Color.FromRgb(56, 189, 248));
					subPath.StrokeThickness = 2.4;
					subPath.Effect = new DropShadowEffect
					{
						Color = System.Windows.Media.Color.FromRgb(56, 189, 248),
						BlurRadius = 14.0,
						ShadowDepth = 0.0,
						Opacity = 0.95
					};
					System.Windows.Controls.Panel.SetZIndex(subPath, 25);
				}
				else if (j != _lastHoveredSubIndex)
				{
					subPath.Stroke = _previewSubBorderBrush ?? _previewBorderBrush;
					subPath.StrokeThickness = (_previewSubStyleRenderer?.BorderThickness ?? _previewStyleRenderer?.BorderThickness ?? 1.2);
					subPath.Effect = null;
					System.Windows.Controls.Panel.SetZIndex(subPath, 15);
				}
			}
		}
	}

	private void ApplySubSectorGlow(System.Windows.Shapes.Path path, bool isHighlighted)
	{
		if (!isHighlighted)
		{
			path.Effect = null;
			return;
		}
		string text = ConfigManager.CurrentConfig?.SubWheelHighlightGlowPreset ?? "FollowPrimary";
		if (text == "FollowPrimary")
		{
			text = ConfigManager.CurrentConfig?.HighlightGlowPreset ?? "Auto";
		}
		if (text == "None")
		{
			path.Effect = null;
			return;
		}
		System.Windows.Media.Color color;
		if (!(text == "Custom") || string.IsNullOrEmpty(ConfigManager.CurrentConfig?.SubWheelHighlightGlowColor))
		{
			color = text switch
			{
				"Lilac" => System.Windows.Media.Color.FromRgb(168, 85, 247), 
				"Blue" => System.Windows.Media.Color.FromRgb(59, 130, 246), 
				"Emerald" => System.Windows.Media.Color.FromRgb(16, 185, 129), 
				"Rose" => System.Windows.Media.Color.FromRgb(236, 72, 153), 
				"Amber" => System.Windows.Media.Color.FromRgb(245, 158, 11), 
				"Red" => System.Windows.Media.Color.FromRgb(239, 68, 68), 
				"White" => System.Windows.Media.Color.FromRgb(byte.MaxValue, byte.MaxValue, byte.MaxValue), 
				_ => (_previewSubHighlightBorderBrush is SolidColorBrush { Color: { A: >0 } } solidColorBrush) ? solidColorBrush.Color : ((_previewSubHighlightBrush is SolidColorBrush { Color: { A: >0 } } solidColorBrush2) ? solidColorBrush2.Color : ((!(_previewHighlightBorderBrush is SolidColorBrush { Color: { A: >0 } } solidColorBrush3)) ? System.Windows.Media.Color.FromRgb(59, 130, 246) : solidColorBrush3.Color)), 
			};
		}
		else
		{
			try
			{
				color = (System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString(ConfigManager.CurrentConfig.SubWheelHighlightGlowColor);
			}
			catch
			{
				color = System.Windows.Media.Color.FromRgb(168, 85, 247);
			}
		}
		double num;
		if (!(ConfigManager.CurrentConfig?.SubWheelHighlightGlowPreset == "FollowPrimary"))
		{
			AppConfig currentConfig = ConfigManager.CurrentConfig;
			num = ((currentConfig != null && currentConfig.SubWheelHighlightGlowRadius > 0.0) ? ConfigManager.CurrentConfig.SubWheelHighlightGlowRadius : 24.0);
		}
		else
		{
			AppConfig currentConfig2 = ConfigManager.CurrentConfig;
			num = ((currentConfig2 != null && currentConfig2.HighlightGlowRadius > 0.0) ? ConfigManager.CurrentConfig.HighlightGlowRadius : 24.0);
		}
		double blurRadius = num;
		double num2;
		if (!(ConfigManager.CurrentConfig?.SubWheelHighlightGlowPreset == "FollowPrimary"))
		{
			AppConfig currentConfig3 = ConfigManager.CurrentConfig;
			num2 = ((currentConfig3 != null && currentConfig3.SubWheelHighlightGlowOpacity >= 0.0) ? ConfigManager.CurrentConfig.SubWheelHighlightGlowOpacity : 0.85);
		}
		else
		{
			AppConfig currentConfig4 = ConfigManager.CurrentConfig;
			num2 = ((currentConfig4 != null && currentConfig4.HighlightGlowOpacity >= 0.0) ? ConfigManager.CurrentConfig.HighlightGlowOpacity : 0.85);
		}
		double opacity = num2;
		path.Effect = new DropShadowEffect
		{
			Color = color,
			BlurRadius = blurRadius,
			ShadowDepth = 0.0,
			Opacity = opacity
		};
	}

	private void SubmenuStyleComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
	{
		if (!_isUpdatingUi && SubmenuStyleComboBox != null && ConfigManager.CurrentConfig != null)
		{
			string val = (SubmenuStyleComboBox.SelectedItem as ComboBoxItem)?.Tag?.ToString() ?? "Wheel";
			ConfigManager.CurrentConfig.SubmenuStyle = val;
			Grid appearanceSettingsGrid = AppearanceSettingsGrid;
			if (appearanceSettingsGrid != null && appearanceSettingsGrid.Visibility == Visibility.Visible)
			{
				RenderLiveWheelPreview();
			}
			SyncUiToConfigAndSave();
		}
	}

	private void LongPressTriggerCheckBox_Changed(object sender, RoutedEventArgs e)
	{
		if (_isUpdatingUi || ConfigManager.CurrentConfig == null)
		{
			return;
		}
		bool enabled = LongPressTriggerCheckBox.IsChecked == true;
		ConfigManager.CurrentConfig.LongPressTrigger = enabled;
		if (LongPressDelayPanel != null)
		{
			LongPressDelayPanel.Visibility = enabled ? Visibility.Visible : Visibility.Collapsed;
		}
		SyncUiToConfigAndSave();
	}

	private void LongPressDelaySlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
	{
		if (_isUpdatingUi || ConfigManager.CurrentConfig == null)
		{
			return;
		}
		ConfigManager.CurrentConfig.LongPressDelayMs = e.NewValue;
		if (LongPressDelayLabel != null)
		{
			LongPressDelayLabel.Text = $"{e.NewValue:0} ms";
		}
		SyncUiToConfigAndSave();
	}

	// ==================== 鼠标手势 ====================

	private void RefreshGestureMappings()
	{
		if (GestureMappingsItemsControl == null || ConfigManager.CurrentConfig == null)
		{
			return;
		}
		List<GestureMappingViewModel> list = new List<GestureMappingViewModel>();
		foreach (GestureMapping m in ConfigManager.CurrentConfig.GestureMappings ?? new List<GestureMapping>())
		{
			list.Add(new GestureMappingViewModel(m));
		}
		GestureMappingsItemsControl.ItemsSource = list;
	}

	private void GestureEnabledCheckBox_Changed(object sender, RoutedEventArgs e)
	{
		if (_isUpdatingUi || ConfigManager.CurrentConfig == null)
		{
			return;
		}
		ConfigManager.CurrentConfig.GestureEnabled = GestureEnabledCheckBox.IsChecked == true;
		SyncUiToConfigAndSave();
	}

	private void GestureTriggerButtonComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
	{
		if (_isUpdatingUi || ConfigManager.CurrentConfig == null)
		{
			return;
		}
		if (GestureTriggerButtonComboBox.SelectedItem is ComboBoxItem item && item.Tag is string tag)
		{
			ConfigManager.CurrentConfig.GestureTriggerButton = tag;
			SyncUiToConfigAndSave();
		}
	}

	private void GestureHintPlacementComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
	{
		if (_isUpdatingUi || ConfigManager.CurrentConfig == null)
		{
			return;
		}
		if (GestureHintPlacementComboBox.SelectedItem is ComboBoxItem item && item.Tag is string tag)
		{
			ConfigManager.CurrentConfig.GestureHintPlacement = tag;
			SyncUiToConfigAndSave();
		}
	}

	private void GestureSensitivitySlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
	{
		if (_isUpdatingUi || ConfigManager.CurrentConfig == null)
		{
			return;
		}
		ConfigManager.CurrentConfig.GestureSegmentSensitivity = e.NewValue;
		if (GestureSensitivityLabel != null)
		{
			GestureSensitivityLabel.Text = $"{e.NewValue:0} px";
		}
		SyncUiToConfigAndSave();
	}

	private void AddGestureMappingButton_Click(object sender, RoutedEventArgs e)
	{
		if (ConfigManager.CurrentConfig == null)
		{
			return;
		}
		ConfigManager.CurrentConfig.GestureMappings ??= new List<GestureMapping>();
		ConfigManager.CurrentConfig.GestureMappings.Add(new GestureMapping
		{
			Pattern = "D",
			Action = new ActionItem { Type = "Hotkey", Name = "手势动作", Parameter = "" }
		});
		RefreshGestureMappings();
		SyncUiToConfigAndSave();
	}

	private void DeleteGestureMapping_Click(object sender, RoutedEventArgs e)
	{
		if (ConfigManager.CurrentConfig == null || sender is not FrameworkElement fe || fe.DataContext is not GestureMappingViewModel vm)
		{
			return;
		}
		ConfigManager.CurrentConfig.GestureMappings?.RemoveAll((GestureMapping m) => ReferenceEquals(m, vm.Mapping));
		RefreshGestureMappings();
		SyncUiToConfigAndSave();
	}

	private void TestGesture_Click(object sender, RoutedEventArgs e)
	{
		if (sender is FrameworkElement fe && fe.DataContext is GestureMappingViewModel vm)
		{
			ActionExecutor.Execute(vm.Mapping.Action);
		}
	}

	// ==================== 取消后动作 ====================

	private void RefreshCancelActionEditor()
	{
		if (ConfigManager.CurrentConfig == null)
		{
			return;
		}
		if (EnableCancelActionCheckBox != null)
		{
			EnableCancelActionCheckBox.IsChecked = ConfigManager.CurrentConfig.EnableCancelAction;
		}
		if (CancelActionEditorHost != null && CancelActionEditorHost.DataContext == null)
		{
			GestureMappingViewModel vm = new GestureMappingViewModel(new GestureMapping
			{
				Pattern = "",
				Action = ConfigManager.CurrentConfig.CancelAction ?? new ActionItem { Type = "Hotkey", Name = "取消动作", Parameter = "" }
			});
			CancelActionEditorHost.DataContext = vm;
		}
		UpdateCancelActionAvailability();
	}

	private void EnableCancelActionCheckBox_Changed(object sender, RoutedEventArgs e)
	{
		if (_isUpdatingUi || ConfigManager.CurrentConfig == null)
		{
			return;
		}
		ConfigManager.CurrentConfig.EnableCancelAction = EnableCancelActionCheckBox.IsChecked == true;
		SyncUiToConfigAndSave();
	}

	private void TestCancelAction_Click(object sender, RoutedEventArgs e)
	{
		if (ConfigManager.CurrentConfig?.CancelAction != null)
		{
			ActionExecutor.Execute(ConfigManager.CurrentConfig.CancelAction);
		}
	}

	// ==================== 平铺窗口 ====================

	private void TilePresetSubs_Click(object sender, RoutedEventArgs e)
	{
		if (_isUpdatingUi || ConfigManager.CurrentConfig == null)
		{
			return;
		}
		if (sender is FrameworkElement fe && fe.DataContext is SlotViewModel vm)
		{
			vm.PopulateTileSubActions();
			SyncUiToConfigAndSave();
		}
	}

	private void TileIncludeMinimizedCheckBox_Changed(object sender, RoutedEventArgs e)
	{
		if (_isUpdatingUi || ConfigManager.CurrentConfig == null)
		{
			return;
		}
		ConfigManager.CurrentConfig.TileIncludeMinimized = TileIncludeMinimizedCheckBox.IsChecked == true;
		SyncUiToConfigAndSave();
	}

	private void TileExcludeProcessesTextBox_TextChanged(object sender, TextChangedEventArgs e)
	{
		if (_isUpdatingUi || ConfigManager.CurrentConfig == null)
		{
			return;
		}
		ConfigManager.CurrentConfig.TileExcludeProcesses = TileExcludeProcessesTextBox.Text ?? "";
		SyncUiToConfigAndSave();
	}

	// ==================== 循环布局选择器 ====================

	private sealed class LayoutCycleItem : INotifyPropertyChanged
	{
		public string Key { get; }
		public string Display { get; }
		private bool _isChecked;

		public bool IsChecked
		{
			get
			{
				return _isChecked;
			}
			set
			{
				if (_isChecked != value)
				{
					_isChecked = value;
					PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsChecked)));
				}
			}
		}

		public LayoutCycleItem(string key, string display, bool isChecked)
		{
			Key = key;
			Display = display;
			_isChecked = isChecked;
		}

		public event PropertyChangedEventHandler? PropertyChanged;
	}

	private List<LayoutCycleItem> _cycleItems = new List<LayoutCycleItem>();

	private void RefreshTileCycleList()
	{
		List<string> cfg = new List<string>();
		string? raw = ConfigManager.CurrentConfig?.TileCycleLayouts;
		if (!string.IsNullOrWhiteSpace(raw))
		{
			foreach (string t in raw.Split(new[] { ',', ';', '，', '；', ' ' }, StringSplitOptions.RemoveEmptyEntries))
			{
				string k = t.Trim();
				if (WindowTiler.IsValidLayout(k) && !cfg.Contains(k))
				{
					cfg.Add(k);
				}
			}
		}
		List<LayoutCycleItem> items = new List<LayoutCycleItem>();
		foreach (string key in cfg)
		{
			LayoutCycleItem item = new LayoutCycleItem(key, WindowTiler.LayoutDisplayName(key), true);
			item.PropertyChanged += LayoutCycleItem_PropertyChanged;
			items.Add(item);
		}
		foreach (string key in WindowTiler.LayoutKeys)
		{
			if (!cfg.Contains(key))
			{
				LayoutCycleItem item = new LayoutCycleItem(key, WindowTiler.LayoutDisplayName(key), false);
				item.PropertyChanged += LayoutCycleItem_PropertyChanged;
				items.Add(item);
			}
		}
		_cycleItems = items;
		if (TileCycleListBox != null)
		{
			TileCycleListBox.ItemsSource = null;
			TileCycleListBox.ItemsSource = _cycleItems;
		}
	}

	/// <summary>勾选/取消任意一项立即持久化（循环范围即时生效）。</summary>
	private void LayoutCycleItem_PropertyChanged(object? sender, PropertyChangedEventArgs e)
	{
		if (_isUpdatingUi || ConfigManager.CurrentConfig == null)
		{
			return;
		}
		if (e.PropertyName == nameof(LayoutCycleItem.IsChecked))
		{
			PersistTileCycleSelection();
		}
	}

	private void PersistTileCycleSelection()
	{
		ConfigManager.CurrentConfig.TileCycleLayouts = string.Join(",", _cycleItems.Where((LayoutCycleItem i) => i.IsChecked).Select((LayoutCycleItem i) => i.Key));
		SyncUiToConfigAndSave();
	}

	private void TileCycleUp_Click(object sender, RoutedEventArgs e)
	{
		if (_isUpdatingUi || ConfigManager.CurrentConfig == null || TileCycleListBox?.SelectedItem is not LayoutCycleItem sel)
		{
			return;
		}
		int idx = _cycleItems.IndexOf(sel);
		if (idx > 0)
		{
			LayoutCycleItem tmp = _cycleItems[idx - 1];
			_cycleItems[idx - 1] = sel;
			_cycleItems[idx] = tmp;
			RefreshTileCycleItemsOnly();
			TileCycleListBox.SelectedItem = sel;
			PersistTileCycleSelection();
		}
	}

	private void TileCycleDown_Click(object sender, RoutedEventArgs e)
	{
		if (_isUpdatingUi || ConfigManager.CurrentConfig == null || TileCycleListBox?.SelectedItem is not LayoutCycleItem sel)
		{
			return;
		}
		int idx = _cycleItems.IndexOf(sel);
		if (idx >= 0 && idx < _cycleItems.Count - 1)
		{
			LayoutCycleItem tmp = _cycleItems[idx + 1];
			_cycleItems[idx + 1] = sel;
			_cycleItems[idx] = tmp;
			RefreshTileCycleItemsOnly();
			TileCycleListBox.SelectedItem = sel;
			PersistTileCycleSelection();
		}
	}

	private void RefreshTileCycleItemsOnly()
	{
		List<LayoutCycleItem> snapshot = new List<LayoutCycleItem>(_cycleItems);
		TileCycleListBox.ItemsSource = null;
		TileCycleListBox.ItemsSource = snapshot;
		_cycleItems = snapshot;
	}

	private void TileCycleAll_Click(object sender, RoutedEventArgs e)
	{
		if (_isUpdatingUi || ConfigManager.CurrentConfig == null)
		{
			return;
		}
		foreach (LayoutCycleItem item in _cycleItems)
		{
			item.IsChecked = true;
		}
		PersistTileCycleSelection();
	}

	private void TileCycleNone_Click(object sender, RoutedEventArgs e)
	{
		if (_isUpdatingUi || ConfigManager.CurrentConfig == null)
		{
			return;
		}
		foreach (LayoutCycleItem item in _cycleItems)
		{
			item.IsChecked = false;
		}
		PersistTileCycleSelection();
	}

	private void GestureBrowse_Click(object sender, RoutedEventArgs e)
	{
		if (sender is FrameworkElement fe && fe.DataContext is GestureMappingViewModel vm)
		{
			ProgramPickerWindow picker = new ProgramPickerWindow();
			picker.Owner = this;
			if (picker.ShowDialog() == true && !string.IsNullOrEmpty(picker.SelectedPath))
			{
				vm.Parameter = picker.SelectedPath;
				if (string.IsNullOrEmpty(vm.Name))
				{
					vm.Name = !string.IsNullOrEmpty(picker.SelectedName) ? picker.SelectedName : System.IO.Path.GetFileNameWithoutExtension(picker.SelectedPath);
				}
			}
		}
	}

	private void GestureBrowseFolder_Click(object sender, RoutedEventArgs e)
	{
		if (sender is FrameworkElement fe && fe.DataContext is GestureMappingViewModel vm)
		{
			using (System.Windows.Forms.FolderBrowserDialog dialog = new System.Windows.Forms.FolderBrowserDialog())
			{
				dialog.Description = "选择要打开的本地文件夹";
				dialog.UseDescriptionForTitle = true;
				dialog.ShowNewFolderButton = true;
				if (dialog.ShowDialog() == System.Windows.Forms.DialogResult.OK)
				{
					vm.Parameter = dialog.SelectedPath;
					if (string.IsNullOrEmpty(vm.Name))
					{
						vm.Name = System.IO.Path.GetFileName(dialog.SelectedPath);
					}
				}
			}
		}
	}

		private void HotkeyBuilderButton_Click(object sender, RoutedEventArgs e)
	{
		try
		{
			if (sender is Button btn && btn.DataContext is SlotViewModel slotVm)
			{
				HotkeyBuilderDialog dlg = new HotkeyBuilderDialog(slotVm.Parameter ?? "")
				{
					Owner = this
				};
				if (dlg.ShowDialog() == true)
				{
					slotVm.Parameter = dlg.ResultHotkey;
					SyncUiToConfigAndSave();
				}
			}
		}
		catch (Exception ex)
		{
			System.Windows.MessageBox.Show(this, "打开按键拼装器失败: " + ex.Message, "错误", MessageBoxButton.OK, MessageBoxImage.Error);
		}
	}

	private bool _isPanning;
	private Point _panStartPoint;
	private double _startTranslateX;
	private double _startTranslateY;

	private void PreviewViewport_MouseWheel(object sender, System.Windows.Input.MouseWheelEventArgs e)
	{
		if (PreviewScaleTransform == null)
		{
			return;
		}
		double zoomStep = (e.Delta > 0) ? 1.15 : (1.0 / 1.15);
		double currentScale = PreviewScaleTransform.ScaleX;
		double newScale = Math.Clamp(currentScale * zoomStep, 0.4, 3.0);
		PreviewScaleTransform.ScaleX = newScale;
		PreviewScaleTransform.ScaleY = newScale;
		UpdateZoomLabel();
		e.Handled = true;
	}

	private void PreviewViewport_MouseDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
	{
		if (e.ClickCount == 2)
		{
			ResetPreviewViewport();
			e.Handled = true;
			return;
		}

		if (e.MiddleButton == System.Windows.Input.MouseButtonState.Pressed || e.RightButton == System.Windows.Input.MouseButtonState.Pressed)
		{
			_isPanning = true;
			_panStartPoint = e.GetPosition(PreviewViewportContainer);
			_startTranslateX = PreviewTranslateTransform?.X ?? 0.0;
			_startTranslateY = PreviewTranslateTransform?.Y ?? 0.0;
			PreviewViewportContainer?.CaptureMouse();
			if (PreviewViewportContainer != null)
			{
				PreviewViewportContainer.Cursor = System.Windows.Input.Cursors.SizeAll;
			}
			e.Handled = true;
		}
	}

	private void PreviewViewport_MouseMove(object sender, System.Windows.Input.MouseEventArgs e)
	{
		if (!_isPanning || PreviewViewportContainer == null || PreviewTranslateTransform == null)
		{
			return;
		}
		Point currentPoint = e.GetPosition(PreviewViewportContainer);
		PreviewTranslateTransform.X = _startTranslateX + (currentPoint.X - _panStartPoint.X);
		PreviewTranslateTransform.Y = _startTranslateY + (currentPoint.Y - _panStartPoint.Y);
	}

	private void PreviewViewport_MouseUp(object sender, System.Windows.Input.MouseButtonEventArgs e)
	{
		if (_isPanning)
		{
			_isPanning = false;
			PreviewViewportContainer?.ReleaseMouseCapture();
			if (PreviewViewportContainer != null)
			{
				PreviewViewportContainer.Cursor = System.Windows.Input.Cursors.Arrow;
			}
			e.Handled = true;
		}
	}

	private void PreviewZoomInBtn_Click(object sender, RoutedEventArgs e)
	{
		if (PreviewScaleTransform == null) return;
		double newScale = Math.Min(3.0, PreviewScaleTransform.ScaleX + 0.15);
		PreviewScaleTransform.ScaleX = newScale;
		PreviewScaleTransform.ScaleY = newScale;
		UpdateZoomLabel();
	}

	private void PreviewZoomOutBtn_Click(object sender, RoutedEventArgs e)
	{
		if (PreviewScaleTransform == null) return;
		double newScale = Math.Max(0.4, PreviewScaleTransform.ScaleX - 0.15);
		PreviewScaleTransform.ScaleX = newScale;
		PreviewScaleTransform.ScaleY = newScale;
		UpdateZoomLabel();
	}

	private void PreviewZoomLabel_MouseLeftButtonDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
	{
		ResetPreviewViewport();
	}

	private void PreviewResetViewBtn_Click(object sender, RoutedEventArgs e)
	{
		ResetPreviewViewport();
	}

	public void ResetPreviewViewport()
	{
		if (PreviewScaleTransform != null)
		{
			PreviewScaleTransform.ScaleX = 1.0;
			PreviewScaleTransform.ScaleY = 1.0;
		}
		if (PreviewTranslateTransform != null)
		{
			PreviewTranslateTransform.X = 0.0;
			PreviewTranslateTransform.Y = 0.0;
		}
		UpdateZoomLabel();
	}

	private void UpdateZoomLabel()
	{
		if (PreviewZoomLabel != null && PreviewScaleTransform != null)
		{
			int pct = (int)Math.Round(PreviewScaleTransform.ScaleX * 100.0);
			PreviewZoomLabel.Text = $"{pct}%";
		}
	}
}

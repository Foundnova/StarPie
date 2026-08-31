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

	private int _lastHoveredSector = -2;

	private int _lastHoveredSubIndex = -2;

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

	public SettingsWindow()
	{
		_isUpdatingUi = true;
		ConfigManager.LoadConfig();
		InitializeComponent();
		InitializeTrayIcon();
		string text = "v" + (Assembly.GetExecutingAssembly().GetName().Version?.ToString(3) ?? "1.5.2");
		if (SidebarVersionText != null)
		{
			SidebarVersionText.Text = text;
		}
		try
		{
			ProfilesListBox.ItemsSource = ConfigManager.CurrentConfig.Profiles;
			UpdateTriggerBadgeDisplay();
			HookRawInputForSensorAndRecorder();
			ThresholdSlider.Value = ConfigManager.CurrentConfig.DragThreshold;
			ThresholdValueLabel.Text = ConfigManager.CurrentConfig.DragThreshold.ToString("0");
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
			string text2 = ConfigManager.CurrentConfig.AnimationSpeed ?? "Balanced";
			double value = ((ConfigManager.CurrentConfig.CustomAnimationDurationMs > 0.0) ? ConfigManager.CurrentConfig.CustomAnimationDurationMs : 80.0);
			if (AnimSpeedSlider != null)
			{
				AnimSpeedSlider.Value = value;
			}
			if (AnimSpeedSliderLabel != null)
			{
				AnimSpeedSliderLabel.Text = $"{value:0} ms";
			}
			switch (text2)
			{
			case "Elegant":
				if (AnimSpeedElegantRadio != null)
				{
					AnimSpeedElegantRadio.IsChecked = true;
				}
				break;
			case "Fast":
				if (AnimSpeedFastRadio != null)
				{
					AnimSpeedFastRadio.IsChecked = true;
				}
				break;
			case "Custom":
				if (AnimSpeedCustomRadio != null)
				{
					AnimSpeedCustomRadio.IsChecked = true;
				}
				break;
			default:
				if (AnimSpeedBalancedRadio != null)
				{
					AnimSpeedBalancedRadio.IsChecked = true;
				}
				break;
			}
			SetComboBoxSelectedValue(AppThemeComboBox, ConfigManager.CurrentConfig.AppTheme ?? "System");
			AppThemeManager.ApplyTheme(this, ConfigManager.CurrentConfig.AppTheme ?? "System");
			ReloadThemePresets();
			SetComboBoxSelectedValue(ThemeComboBox, ConfigManager.CurrentConfig.Theme);
			SetComboBoxSelectedValue(UiStyleComboBox, ConfigManager.CurrentConfig.UiStyle);
			CustomSectorBgTextBox.Text = ConfigManager.CurrentConfig.CustomSectorBg;
			CustomSectorBorderTextBox.Text = ConfigManager.CurrentConfig.CustomSectorBorder;
			CustomHighlightBgTextBox.Text = ConfigManager.CurrentConfig.CustomHighlightBg;
			CustomHighlightBorderTextBox.Text = ConfigManager.CurrentConfig.CustomHighlightBorder;
			CustomTextTextBox.Text = ConfigManager.CurrentConfig.CustomText;
			bool flag = (ConfigManager.CurrentConfig.Theme ?? "").StartsWith("CustomPreset_");
			if (CustomColorsPanel != null)
			{
				CustomColorsPanel.Visibility = Visibility.Visible;
			}
			if (((ConfigManager.CurrentConfig.Theme == "Custom") | flag) && CustomColorExpander != null)
			{
				CustomColorExpander.IsExpanded = true;
			}
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
			SetComboBoxSelectedValue(HighlightGlowPresetComboBox, ConfigManager.CurrentConfig.HighlightGlowPreset ?? "Auto");
			HighlightGlowColorTextBox.Text = ConfigManager.CurrentConfig.HighlightGlowColor ?? "";
			HighlightGlowRadiusSlider.Value = ((ConfigManager.CurrentConfig.HighlightGlowRadius > 0.0) ? ConfigManager.CurrentConfig.HighlightGlowRadius : 24.0);
			HighlightGlowRadiusLabel.Text = $"{HighlightGlowRadiusSlider.Value:0} px";
			HighlightGlowOpacitySlider.Value = ((ConfigManager.CurrentConfig.HighlightGlowOpacity >= 0.0) ? ConfigManager.CurrentConfig.HighlightGlowOpacity : 0.85) * 100.0;
			HighlightGlowOpacityLabel.Text = $"{HighlightGlowOpacitySlider.Value:0}%";
			CustomHighlightGlowPanel.Visibility = ((!(ConfigManager.CurrentConfig.HighlightGlowPreset == "Custom") && string.IsNullOrEmpty(ConfigManager.CurrentConfig.HighlightGlowColor)) ? Visibility.Collapsed : Visibility.Visible);
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
				string text3 = ConfigManager.CurrentConfig.SubWheelHighlightGlowPreset ?? "FollowPrimary";
				SubCustomHighlightGlowPanel.Visibility = ((!(text3 == "Custom") && string.IsNullOrEmpty(ConfigManager.CurrentConfig.SubWheelHighlightGlowColor)) ? Visibility.Collapsed : Visibility.Visible);
			}
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
			SetComboBoxSelectedValue(ShapeComboBox, ConfigManager.CurrentConfig.Shape);
			SetComboBoxSelectedValue(IconLayoutModeComboBox, ConfigManager.CurrentConfig.IconLayoutMode);
			PopulateWheelFontFamilies();
			SetComboBoxSelectedValue(SubmenuStyleComboBox, ConfigManager.CurrentConfig.SubmenuStyle ?? "Wheel");
			SetComboBoxSelectedValue(WheelFontFamilyComboBox, ConfigManager.CurrentConfig.WheelFontFamily ?? "Microsoft YaHei UI, Segoe UI");
			ShowTextCheckBox.IsChecked = ConfigManager.CurrentConfig.ShowText;
			if (EnableMultiTierCheckBox != null)
			{
				EnableMultiTierCheckBox.IsChecked = ConfigManager.CurrentConfig.EnableMultiTier;
			}
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
			bool flag2 = (ConfigManager.CurrentConfig.SubWheelTheme ?? "").StartsWith("CustomPreset_");
			if (((ConfigManager.CurrentConfig.SubWheelTheme == "Custom") | flag2) && SubCustomColorExpander != null)
			{
				SubCustomColorExpander.IsExpanded = true;
			}
			if (RenameSubCustomColorPresetButton != null)
			{
				RenameSubCustomColorPresetButton.Visibility = ((!flag2) ? Visibility.Collapsed : Visibility.Visible);
			}
			if (DeleteSubCustomColorPresetButton != null)
			{
				DeleteSubCustomColorPresetButton.Visibility = ((!flag2) ? Visibility.Collapsed : Visibility.Visible);
			}
			if (DeleteSubPresetInPanelButton != null)
			{
				DeleteSubPresetInPanelButton.Visibility = ((!flag2) ? Visibility.Collapsed : Visibility.Visible);
			}
			if (SaveSubPresetChangesButton != null)
			{
				SaveSubPresetChangesButton.Content = (flag2 ? I18n.T("SavePresetChangesButton") : I18n.T("SaveAsNewPresetButton"));
			}
			UpdateSubColorPreviews();
			ShowCoreIconCheckBox.IsChecked = ConfigManager.CurrentConfig.ShowCoreIcon;
			SetComboBoxSelectedValue(CoreIconTypeComboBox, ConfigManager.CurrentConfig.CoreIconType ?? "Exit");
			CoreImagePathTextBox.Text = ConfigManager.CurrentConfig.CoreCustomImagePath ?? "";
			double num = ((ConfigManager.CurrentConfig.CoreIconScale > 0.0) ? ConfigManager.CurrentConfig.CoreIconScale : 1.0);
			if (CoreIconScaleSlider != null)
			{
				CoreIconScaleSlider.Value = num;
			}
			if (CoreIconScaleLabel != null)
			{
				CoreIconScaleLabel.Text = $"{Math.Round(num * 100.0)}%";
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
			DisableOnFullScreenCheckBox.IsChecked = ConfigManager.CurrentConfig.DisableOnFullScreen;
			CtrlModifierCheckBox.IsChecked = ConfigManager.CurrentConfig.DisableOnCtrl;
			ShiftModifierCheckBox.IsChecked = ConfigManager.CurrentConfig.DisableOnShift;
			AltModifierCheckBox.IsChecked = ConfigManager.CurrentConfig.DisableOnAlt;
			bool flag3 = string.Equals(ConfigManager.CurrentConfig.IsolationMode, "Whitelist", StringComparison.OrdinalIgnoreCase);
			if (IsolationWhitelistRadio != null)
			{
				IsolationWhitelistRadio.IsChecked = flag3;
			}
			if (IsolationBlacklistRadio != null)
			{
				IsolationBlacklistRadio.IsChecked = !flag3;
			}
			RefreshProcessListUI();
			AutoStartCheckBox.IsChecked = ConfigManager.IsAutoStartEnabled();
			if (AutoStartAsAdminCheckBox != null)
			{
				AutoStartAsAdminCheckBox.IsChecked = ConfigManager.CurrentConfig.AutoStartAsAdmin || ConfigManager.IsAdminTaskAutoStartEnabled();
			}
			SetComboBoxSelectedValue(LanguageComboBox, ConfigManager.CurrentConfig.Language ?? "Auto");
			ApplyLocalization();
			UpdateColorPreviews();
			SlotsItemsControl.ItemsSource = _slotViewModels;
			bool flag4 = IsRunningAsAdmin();
			UacWarningCard.Visibility = (flag4 ? Visibility.Collapsed : Visibility.Visible);
			_selectedProfile = ConfigManager.CurrentConfig.Profiles.FirstOrDefault();
			if (_selectedProfile != null)
			{
				ProfilesListBox.SelectedItem = _selectedProfile;
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
				RefreshSlots();
			}
		}
		finally
		{
			_isUpdatingUi = false;
		}
		base.Loaded += delegate
		{
			if (AppearanceSettingsGrid.Visibility == Visibility.Visible)
			{
				RenderLiveWheelPreview();
			}
			MemoryOptimizer.TrimMemory();
		};
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
			ToolStripMenuItem value = new ToolStripMenuItem("StarPie v" + (Assembly.GetExecutingAssembly().GetName().Version?.ToString(3) ?? "1.5.2"))
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
		
		if (SubmenuStyleTitleText != null) SubmenuStyleTitleText.Text = I18n.T("SubmenuStyleTitle");
		if (SubmenuStyleDescText != null) SubmenuStyleDescText.Text = I18n.T("SubmenuStyleDesc");
		if (SubmenuStyleWheelItem != null) SubmenuStyleWheelItem.Content = I18n.T("SubmenuStyleWheel");
		if (SubmenuStyleFanItem != null) SubmenuStyleFanItem.Content = I18n.T("SubmenuStyleFan");

		if (EnableMultiTierDescText != null)
		{
			EnableMultiTierDescText.Text = I18n.T("EnableMultiTierDesc");
		}
		if (GesturesPageHeader != null)
		{
			GesturesPageHeader.Text = I18n.T("GesturesHeader");
		}
		if (ProfileCardTitleText != null)
		{
			ProfileCardTitleText.Text = I18n.T("ProfileCardTitle");
		}
		if (ProfileCardDescText != null)
		{
			ProfileCardDescText.Text = I18n.T("ProfileCardDesc");
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
		if (SectorCountTitleText != null)
		{
			SectorCountTitleText.Text = I18n.T("SectorCountOptionTitle");
		}
		if (SectorCountDescText != null)
		{
			SectorCountDescText.Text = I18n.T("SectorCountOptionDesc");
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
		if (SectorActionListTitleText != null)
		{
			SectorActionListTitleText.Text = I18n.T("SectorActionListTitle");
		}
		if (SectorActionListDescText != null)
		{
			SectorActionListDescText.Text = I18n.T("SectorActionListDesc");
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

	public void ShowSettings(int tabIndex = 0)
	{
		if (!((DispatcherObject)this).Dispatcher.CheckAccess())
		{
			((DispatcherObject)this).Dispatcher.Invoke((Action)delegate
			{
				ShowSettings(tabIndex);
			});
			return;
		}
		SwitchToTab(tabIndex);
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
				if (ProfilesListBox != null)
				{
					ProfilesListBox.SelectedItem = _selectedProfile;
				}
			}
			if (_selectedProfile == null)
			{
				break;
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
				RefreshSlots();
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
			if (IconLayoutModeComboBox?.SelectedItem is ComboBoxItem comboBoxItem5)
			{
				ConfigManager.CurrentConfig.IconLayoutMode = comboBoxItem5.Tag?.ToString() ?? "IconAndText";
			}
			if (ShowTextCheckBox != null)
			{
				ConfigManager.CurrentConfig.ShowText = ShowTextCheckBox.IsChecked == true;
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
			if (SectorIconSizeSlider != null)
			{
				ConfigManager.CurrentConfig.SectorIconSize = SectorIconSizeSlider.Value;
			}
			if (SectorFontSizeSlider != null)
			{
				ConfigManager.CurrentConfig.SectorFontSize = SectorFontSizeSlider.Value;
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
		MemoryOptimizer.TrimMemory();
		if (!_isClosingFromTray)
		{
			e.Cancel = true;
			DoubleAnimation doubleAnimation = new DoubleAnimation(1.0, 0.0, new Duration(TimeSpan.FromMilliseconds(120.0)));
			doubleAnimation.Completed += delegate
			{
				Hide();
				base.Opacity = 1.0;
				MemoryOptimizer.TrimMemory();
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
			RefreshSlots();
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
	}

	private void RefreshSlots()
	{
		try
		{
			_slotViewModels.Clear();
			if (_selectedProfile == null)
			{
				_selectedProfile = (ProfilesListBox?.SelectedItem as WheelProfile) ?? ConfigManager.CurrentConfig.Profiles.FirstOrDefault();
			}
			if (_selectedProfile == null)
			{
				return;
			}
			int num = _selectedProfile.SectorCount;
			if (num != 4 && num != 8 && num != 12)
			{
				num = 8;
			}
			string[] array = num switch
			{
				4 => Directions4, 
				12 => Directions12, 
				_ => Directions8, 
			};
			if (_selectedProfile.Actions == null)
			{
				_selectedProfile.Actions = new List<ActionItem>();
			}
			while (_selectedProfile.Actions.Count < num)
			{
				int count = _selectedProfile.Actions.Count;
				if (num == 12 && count < DefaultPresets12.Length)
				{
					ActionItem actionItem = DefaultPresets12[count];
					_selectedProfile.Actions.Add(new ActionItem
					{
						Type = actionItem.Type,
						Name = actionItem.Name,
						Parameter = actionItem.Parameter,
						IconKey = actionItem.IconKey
					});
				}
				else if (num == 4 && count < DefaultPresets4.Length)
				{
					ActionItem actionItem2 = DefaultPresets4[count];
					_selectedProfile.Actions.Add(new ActionItem
					{
						Type = actionItem2.Type,
						Name = actionItem2.Name,
						Parameter = actionItem2.Parameter,
						IconKey = actionItem2.IconKey
					});
				}
				else
				{
					_selectedProfile.Actions.Add(new ActionItem
					{
						Type = "Hotkey",
						Name = $"快捷动作 {count + 1}",
						Parameter = ""
					});
				}
			}
			for (int i = 0; i < num; i++)
			{
				ActionItem action = _selectedProfile.Actions[i];
				SlotViewModel item = new SlotViewModel(array[i], action);
				_slotViewModels.Add(item);
			}
		}
		catch (Exception)
		{
		}
	}

	private void SectorCountRadio_Checked(object sender, RoutedEventArgs e)
	{
		if (_isUpdatingUi)
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
		System.Windows.Controls.RadioButton sectorCount4Radio = SectorCount4Radio;
		if (sectorCount4Radio != null && sectorCount4Radio.IsChecked == true)
		{
			sectorCount = 4;
		}
		else
		{
			System.Windows.Controls.RadioButton sectorCount8Radio = SectorCount8Radio;
			if (sectorCount8Radio != null && sectorCount8Radio.IsChecked == true)
			{
				sectorCount = 8;
			}
			else
			{
				System.Windows.Controls.RadioButton sectorCount12Radio = SectorCount12Radio;
				if (sectorCount12Radio != null && sectorCount12Radio.IsChecked == true)
				{
					sectorCount = 12;
				}
			}
		}
		_selectedProfile.SectorCount = sectorCount;
		RefreshSlots();
		Grid appearanceSettingsGrid = AppearanceSettingsGrid;
		if (appearanceSettingsGrid != null && appearanceSettingsGrid.Visibility == Visibility.Visible)
		{
			RenderLiveWheelPreview();
		}
		SyncUiToConfigAndSave();
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
			SyncUiToConfigAndSave();
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

	private void IconLayoutModeComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
	{
		if (IconLayoutModeComboBox == null || ConfigManager.CurrentConfig == null || _isUpdatingUi || !(IconLayoutModeComboBox.SelectedItem is ComboBoxItem { Tag: var tag }))
		{
			return;
		}
		string text = tag?.ToString() ?? "IconAndText";
		ConfigManager.CurrentConfig.IconLayoutMode = text;
		_isUpdatingUi = true;
		try
		{
			if (text == "IconOnly")
			{
				ConfigManager.CurrentConfig.ShowText = false;
				if (ShowTextCheckBox != null)
				{
					ShowTextCheckBox.IsChecked = false;
				}
			}
			else
			{
				ConfigManager.CurrentConfig.ShowText = true;
				if (ShowTextCheckBox != null)
				{
					ShowTextCheckBox.IsChecked = true;
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
		SyncUiToConfigAndSave();
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
		if (!_isUpdatingUi && WheelFontFamilyComboBox != null && ConfigManager.CurrentConfig != null && WheelFontFamilyComboBox.SelectedItem is ComboBoxItem { Tag: var tag })
		{
			string wheelFontFamily = tag?.ToString() ?? "Microsoft YaHei UI, Segoe UI";
			ConfigManager.CurrentConfig.WheelFontFamily = wheelFontFamily;
			Grid appearanceSettingsGrid = AppearanceSettingsGrid;
			if (appearanceSettingsGrid != null && appearanceSettingsGrid.Visibility == Visibility.Visible)
			{
				RenderLiveWheelPreview();
			}
			SyncUiToConfigAndSave();
		}
	}

	private void ShowTextCheckBox_Changed(object sender, RoutedEventArgs e)
	{
		if (ShowTextCheckBox == null || ConfigManager.CurrentConfig == null || _isUpdatingUi)
		{
			return;
		}
		bool valueOrDefault = ShowTextCheckBox.IsChecked == true;
		ConfigManager.CurrentConfig.ShowText = valueOrDefault;
		_isUpdatingUi = true;
		try
		{
			if (valueOrDefault)
			{
				if (ConfigManager.CurrentConfig.IconLayoutMode == "IconOnly")
				{
					ConfigManager.CurrentConfig.IconLayoutMode = "IconAndText";
					SetComboBoxSelectedValue(IconLayoutModeComboBox, "IconAndText");
				}
			}
			else if (ConfigManager.CurrentConfig.IconLayoutMode != "IconOnly")
			{
				ConfigManager.CurrentConfig.IconLayoutMode = "IconOnly";
				SetComboBoxSelectedValue(IconLayoutModeComboBox, "IconOnly");
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
		SyncUiToConfigAndSave();
	}

	private void SectorIconSizeSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
	{
		if (SectorIconSizeSlider != null && SectorIconSizeLabel != null && ConfigManager.CurrentConfig != null && !_isUpdatingUi)
		{
			ConfigManager.CurrentConfig.SectorIconSize = e.NewValue;
			SectorIconSizeLabel.Text = $"{e.NewValue:0} px";
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
			ConfigManager.CurrentConfig.SectorFontSize = e.NewValue;
			SectorFontSizeLabel.Text = $"{e.NewValue:0.0} px";
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
			System.Windows.MessageBox.Show("配置导入成功！正在应用新设置...", "提示", MessageBoxButton.OK, MessageBoxImage.Asterisk);
			_isUpdatingUi = true;
			try
			{
				ProfilesListBox.ItemsSource = null;
				ProfilesListBox.ItemsSource = ConfigManager.CurrentConfig.Profiles;
				ProfilesListBox.SelectedIndex = 0;
				UpdateTriggerBadgeDisplay();
				HookRawInputForSensorAndRecorder();
				ThresholdSlider.Value = ConfigManager.CurrentConfig.DragThreshold;
				SetComboBoxSelectedValue(ThemeComboBox, ConfigManager.CurrentConfig.Theme);
				SetComboBoxSelectedValue(UiStyleComboBox, ConfigManager.CurrentConfig.UiStyle);
				SetComboBoxSelectedValue(ShapeComboBox, ConfigManager.CurrentConfig.Shape);
				SetComboBoxSelectedValue(IconLayoutModeComboBox, ConfigManager.CurrentConfig.IconLayoutMode);
				WheelRadiusSlider.Value = ConfigManager.CurrentConfig.WheelRadius;
				InnerRadiusSlider.Value = ConfigManager.CurrentConfig.InnerRadius;
				CoreRadiusSlider.Value = ConfigManager.CurrentConfig.CoreRadius;
				SectorGapSlider.Value = ConfigManager.CurrentConfig.SectorGap;
				SectorCornerRadiusSlider.Value = ConfigManager.CurrentConfig.SectorCornerRadius;
				SectorIconSizeSlider.Value = ((ConfigManager.CurrentConfig.SectorIconSize > 0.0) ? ConfigManager.CurrentConfig.SectorIconSize : 20.0);
				SectorIconSizeLabel.Text = $"{SectorIconSizeSlider.Value:0} px";
				SectorFontSizeSlider.Value = ((ConfigManager.CurrentConfig.SectorFontSize > 0.0) ? ConfigManager.CurrentConfig.SectorFontSize : 10.5);
				SectorFontSizeLabel.Text = $"{SectorFontSizeSlider.Value:0.0} px";
				ShowTextCheckBox.IsChecked = ConfigManager.CurrentConfig.ShowText;
			}
			finally
			{
				_isUpdatingUi = false;
			}
			RenderLiveWheelPreview();
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
			double val = (num4 ? (Math.Max(num5, num6) + 10.0) : num5);
			double num7 = 135.0 / Math.Max(135.0, val);
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
			if (text4 != text || text5 != text2 || ConfigManager.CurrentConfig.UseIndependentSubWheelTheme)
			{
				try
				{
					IRadialStyleRenderer radialStyleRenderer = StyleRendererFactory.CreateRenderer(text4);
					radialStyleRenderer.Initialize(text5, ConfigManager.CurrentConfig);
					_previewSubDefaultBrush = radialStyleRenderer.DefaultSectorBrush;
					_previewSubHighlightBrush = radialStyleRenderer.HighlightSectorBrush;
					_previewSubBorderBrush = radialStyleRenderer.SectorBorderBrush;
					_previewSubHighlightBorderBrush = radialStyleRenderer.HighlightBorderBrush;
					_previewSubTextBrush = radialStyleRenderer.TextColorBrush;
				}
				catch
				{
					_previewSubDefaultBrush = _previewDefaultBrush;
					_previewSubHighlightBrush = _previewHighlightBrush;
					_previewSubBorderBrush = _previewBorderBrush;
					_previewSubHighlightBorderBrush = _previewHighlightBorderBrush;
					_previewSubTextBrush = _previewTextBrush;
				}
			}
			else
			{
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
			else
			{
				if (!string.IsNullOrEmpty(ConfigManager.CurrentConfig.SubWheelCustomSectorBg))
				{
					try
					{
						_previewSubDefaultBrush = new SolidColorBrush((System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString(ConfigManager.CurrentConfig.SubWheelCustomSectorBg));
					}
					catch
					{
					}
				}
				if (!string.IsNullOrEmpty(ConfigManager.CurrentConfig.SubWheelCustomSectorBorder))
				{
					try
					{
						_previewSubBorderBrush = new SolidColorBrush((System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString(ConfigManager.CurrentConfig.SubWheelCustomSectorBorder));
					}
					catch
					{
					}
				}
				if (!string.IsNullOrEmpty(ConfigManager.CurrentConfig.SubWheelCustomHighlightBg))
				{
					try
					{
						_previewSubHighlightBrush = new SolidColorBrush((System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString(ConfigManager.CurrentConfig.SubWheelCustomHighlightBg));
					}
					catch
					{
					}
				}
				if (!string.IsNullOrEmpty(ConfigManager.CurrentConfig.SubWheelCustomHighlightBorder))
				{
					try
					{
						_previewSubHighlightBorderBrush = new SolidColorBrush((System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString(ConfigManager.CurrentConfig.SubWheelCustomHighlightBorder));
					}
					catch
					{
					}
				}
				if (!string.IsNullOrEmpty(ConfigManager.CurrentConfig.SubWheelCustomText))
				{
					try
					{
						_previewSubTextBrush = new SolidColorBrush((System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString(ConfigManager.CurrentConfig.SubWheelCustomText));
					}
					catch
					{
					}
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
				grid.Children.Add(_previewExitIcon);
			}
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
					Tag = num21
				};
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
				if (wheelProfile.Actions != null && num21 < wheelProfile.Actions.Count && wheelProfile.Actions[num21] != null)
				{
					ActionItem action = wheelProfile.Actions[num21];
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
				if (text3 != "TextOnly")
				{
					double num28 = ((ConfigManager.CurrentConfig.SectorIconSize > 0.0) ? ConfigManager.CurrentConfig.SectorIconSize : 20.0);
					double num29 = num19 switch
					{
						4 => 1.2, 
						12 => 0.8, 
						_ => 1.0, 
					};
					double num30 = ((text3 == "IconOnly") ? (num28 * 1.35) : num28) * 0.72 * num29 * (num7 / (135.0 / num5));
					if (!string.IsNullOrEmpty(text11))
					{
						try
						{
							System.Windows.Shapes.Path element = new System.Windows.Shapes.Path
							{
								Data = Geometry.Parse(text11),
								Fill = _previewTextBrush,
								Width = num30,
								Height = num30,
								Stretch = Stretch.Uniform,
								HorizontalAlignment = System.Windows.HorizontalAlignment.Center,
								Margin = new Thickness(0.0, 0.0, 0.0, flag ? 1 : 0)
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
								Margin = new Thickness(0.0, 0.0, 0.0, flag ? 1 : 0)
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
								Margin = new Thickness(0.0, 0.0, 0.0, flag ? 1 : 0)
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
								Fill = _previewTextBrush,
								Width = num30,
								Height = num30,
								Stretch = Stretch.Uniform,
								HorizontalAlignment = System.Windows.HorizontalAlignment.Center,
								Margin = new Thickness(0.0, 0.0, 0.0, flag ? 1 : 0)
							};
							stackPanel.Children.Add(element4);
						}
						catch
						{
						}
					}
				}
				if (flag && !string.IsNullOrEmpty(text8))
				{
					double num31 = ((ConfigManager.CurrentConfig.SectorFontSize > 0.0) ? ConfigManager.CurrentConfig.SectorFontSize : 11.0);
					double num32 = num19 switch
					{
						4 => 1.2, 
						12 => 0.8, 
						_ => 1.0, 
					};
					double val2 = ((text3 == "TextOnly") ? (num31 + 1.0) : num31) * 0.82 * num32 * (num7 / (135.0 / num5));
					double maxWidth = num19 switch
					{
						4 => 96.0, 
						12 => 52.0, 
						_ => 80.0, 
					} * num7;
					TextBlock element5 = new TextBlock
					{
						Text = text8,
						FontSize = Math.Max(6.0, val2),
						FontFamily = new System.Windows.Media.FontFamily(ConfigManager.CurrentConfig.WheelFontFamily ?? "Microsoft YaHei UI, Segoe UI"),
						Foreground = _previewTextBrush,
						FontWeight = FontWeights.Medium,
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
				LiveWheelPreviewCanvas.Children.Add(grid2);if (!enableMultiTier || wheelProfile.Actions == null || num21 >= wheelProfile.Actions.Count || wheelProfile.Actions[num21] == null)
				{
					continue;
				}
				ActionItem actionItem = wheelProfile.Actions[num21];
				if (actionItem.SubActions == null || actionItem.SubActions.Count <= 0)
				{
					continue;
				}
				bool isFan = string.Equals(ConfigManager.CurrentConfig.SubmenuStyle, "Fan", StringComparison.OrdinalIgnoreCase);
				bool isTier2Mode = (Tier2ConfigSegmentRadio != null && Tier2ConfigSegmentRadio.IsChecked == true);
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
						var (du, dv) = RadialWindow.GetFanSubOffset(slot);
						double itemR_sub = (num8 - num9) * 0.40;
						if (ConfigManager.CurrentConfig.SubWheelOuterRadius > 0.0 && ConfigManager.CurrentConfig.WheelRadius > 0.0)
						{
							double ratio = ConfigManager.CurrentConfig.SubWheelOuterRadius / (ConfigManager.CurrentConfig.WheelRadius * 1.55);
							itemR_sub *= Math.Max(0.5, Math.Min(2.5, ratio));
						}
						double R_sub = (num9 + num8) / 2.0 + num11;
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
					System.Windows.Shapes.Path path2 = new System.Windows.Shapes.Path
					{
						Data = data2,
						Fill = _previewSubDefaultBrush,
						Stroke = _previewSubBorderBrush,
						StrokeThickness = (_previewStyleRenderer?.BorderThickness ?? 1.2),
						RenderTransform = translateTransform2,
						Tag = $"sub_{num21}_{num36}",
						Opacity = (isFan ? (isTier2Mode ? 0.95 : 0.0) : 0.88)
					};
					System.Windows.Controls.Panel.SetZIndex(path2, 15);
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
					double num43 = ((ConfigManager.CurrentConfig.SubWheelIconSize > 0.0) ? ConfigManager.CurrentConfig.SubWheelIconSize : 18.0) * 0.65 * num7;
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
								Fill = _previewSubTextBrush,
								Width = num43,
								Height = num43,
								Stretch = Stretch.Uniform,
								HorizontalAlignment = HorizontalAlignment.Center
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
								HorizontalAlignment = HorizontalAlignment.Center
							};
							stackPanel2.Children.Add(element7);
						}
					}
					if (flag && !string.IsNullOrEmpty(actionItem2.Name))
					{
						double num44 = ((ConfigManager.CurrentConfig.SubWheelFontSize > 0.0) ? ConfigManager.CurrentConfig.SubWheelFontSize : 10.0);
						TextBlock element8 = new TextBlock
						{
							Text = actionItem2.Name,
							FontSize = Math.Max(5.0, num44 * 0.75 * num7),
							FontFamily = new FontFamily(ConfigManager.CurrentConfig.WheelFontFamily ?? "Microsoft YaHei UI, Segoe UI"),
							Foreground = _previewSubTextBrush,
							FontWeight = FontWeights.Normal,
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
						Opacity = (isFan ? (isTier2Mode ? 1.0 : 0.0) : 1.0)
					};
					grid3.Children.Add(stackPanel2);
					Canvas.SetLeft(grid3, num41 - num45 / 2.0);
					Canvas.SetTop(grid3, num42 - num46 / 2.0);
					System.Windows.Controls.Panel.SetZIndex(grid3, 30);
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
	}private void LiveWheelPreviewCanvas_MouseMove(object sender, System.Windows.Input.MouseEventArgs e)
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
					path3.StrokeThickness = _previewStyleRenderer?.HighlightBorderThickness ?? 2.0;
					path3.Opacity = 1.0;
					if (grid != null) grid.Opacity = 1.0;
					System.Windows.Controls.Panel.SetZIndex(path3, 20);
					translateTransform2.X = Math.Cos(num27) * 4.0;
					translateTransform2.Y = Math.Sin(num27) * 4.0;
					ApplySubSectorGlow(path3, isHighlighted: true);
					if (grid != null)
					{
						System.Windows.Controls.Panel.SetZIndex(grid, 30);
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
					path3.StrokeThickness = _previewStyleRenderer?.BorderThickness ?? 1.5;
					path3.Opacity = 1.0;
					if (grid != null) grid.Opacity = 1.0;
					System.Windows.Controls.Panel.SetZIndex(path3, 15);
					translateTransform2.X = 0.0;
					translateTransform2.Y = 0.0;
					ApplySubSectorGlow(path3, isHighlighted: false);
					if (grid != null)
					{
						System.Windows.Controls.Panel.SetZIndex(grid, 30);
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
				path3.StrokeThickness = _previewStyleRenderer?.BorderThickness ?? 1.2;
				path3.Opacity = (isFan ? (isTier2Mode ? 0.95 : 0.0) : 0.85);
				if (grid != null) grid.Opacity = (isFan ? (isTier2Mode ? 1.0 : 0.0) : 0.85);
				System.Windows.Controls.Panel.SetZIndex(path3, 15);
				translateTransform2.X = 0.0;
				translateTransform2.Y = 0.0;
				ApplySubSectorGlow(path3, isHighlighted: false);
				if (grid != null)
				{
					System.Windows.Controls.Panel.SetZIndex(grid, 30);
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

}

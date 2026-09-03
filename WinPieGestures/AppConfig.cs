using System.Collections.Generic;

namespace WinPieGestures;

public class AppConfig
{
	public string Language { get; set; } = "Auto";

	public string TriggerButton { get; set; } = "RightButton";

	public TriggerConfig Trigger { get; set; } = new TriggerConfig();

	public double DragThreshold { get; set; } = 25.0;

	/// <summary>可选：长按触发按键（如右键）不动达到长按阈值后呼出轮盘，与拖动呼出共存。</summary>
	public bool LongPressTrigger { get; set; }

	/// <summary>长按响应时长（毫秒）。</summary>
	public double LongPressDelayMs { get; set; } = 450.0;

	// ---- 鼠标手势（画轨迹识别，最多三段图样）----
	public bool GestureEnabled { get; set; }

	/// <summary>手势触发键："RightButton"/"MiddleButton"/"XButton1"/"XButton2"。</summary>
	public string GestureTriggerButton { get; set; } = "MiddleButton";

	public List<GestureMapping> GestureMappings { get; set; } = new List<GestureMapping>();

	/// <summary>手势提示文字位置："Auto" 或 U/D/L/R/UL/UR/DL/DR（相对鼠标）。</summary>
	public string GestureHintPlacement { get; set; } = "Auto";

	/// <summary>手势段灵敏度：最小段长（像素）。越大越难把中途小拐弯误识别为方向段。</summary>
	public double GestureSegmentSensitivity { get; set; } = 16.0;

	/// <summary>取消（回到轮盘中心松手且未选中任何动作）后执行的自定义动作；默认关闭=仅关面板不执行。</summary>
	public bool EnableCancelAction { get; set; }

	public ActionItem CancelAction { get; set; } = new ActionItem { Type = "Hotkey", Name = "取消动作", Parameter = "" };

	/// <summary>平铺排除名单：进程 exe 名（不含扩展名），逗号/分号分隔。</summary>
	public string TileExcludeProcesses { get; set; } = "";

	/// <summary>平铺是否包含最小化窗口（true=还原后参与平铺）。</summary>
	public bool TileIncludeMinimized { get; set; }

	/// <summary>平铺"循环切换"参与范围：布局 key 逗号分隔（空=全部布局参与循环）。</summary>
	public string TileCycleLayouts { get; set; } = "";

	public string AnimationSpeed { get; set; } = "Balanced";

	public double CustomAnimationDurationMs { get; set; } = 80.0;

	public bool EnableOuterEscapeCancel { get; set; }

	public double OuterEscapeDistance { get; set; } = 186.0;

	public string AppTheme { get; set; } = "System";

	public string Theme { get; set; } = "System";

	public string UiStyle { get; set; } = "ClassicRing";
	public string SubmenuStyle { get; set; } = "Wheel";

	public bool EnableMultiTier { get; set; } = true;

	/// <summary>在设置控制台拖拽对调一级扇区时，是否连同其绑定的二级级联子动作一块换位（默认 true）。</summary>
	public bool LinkSubActionsWhenDragging { get; set; } = true;

	public double SubWheelRadiusRatio { get; set; } = 1.55;

	public double SubWheelTriggerDistance { get; set; } = 95.0;

	public double SubWheelOuterRadius { get; set; } = 210.0;

	public double SubWheelInnerGap { get; set; } = 4.0;

	public double SubWheelCornerRadius { get; set; } = 4.0;

	public double SubWheelIconSize { get; set; } = 18.0;

	public double SubWheelFontSize { get; set; } = 9.5;

	public bool UseIndependentSubWheelTheme { get; set; }

	public string SubWheelUiStyle { get; set; } = "ClassicRing";

	public string SubWheelTheme { get; set; } = "FollowPrimary";

	public string SubWheelCustomSectorBg { get; set; } = "#9016161A";

	public string SubWheelCustomSectorBorder { get; set; } = "#35FFFFFF";

	public string SubWheelCustomHighlightBg { get; set; } = "#E06C4DFF";

	public string SubWheelCustomHighlightBorder { get; set; } = "#A0FFFFFF";

	public string SubWheelCustomText { get; set; } = "#E0FFFFFF";

	public string SubWheelHighlightGlowPreset { get; set; } = "FollowPrimary";

	public string SubWheelHighlightGlowColor { get; set; } = "";

	public double SubWheelHighlightGlowRadius { get; set; } = 24.0;

	public double SubWheelHighlightGlowOpacity { get; set; } = 0.85;

	public bool AutoStartAsAdmin { get; set; }

	public bool ShowText { get; set; } = true;

	public bool ShowSelectedActionText { get; set; }

	public double WheelRadius { get; set; } = 138.0;

	public double InnerRadius { get; set; } = 52.0;

	public double CoreRadius { get; set; } = 50.0;

	public string Shape { get; set; } = "Original";

	public double SectorGap { get; set; } = 2.0;

	public double SectorCornerRadius { get; set; } = 4.0;

	public string IconLayoutMode { get; set; } = "IconAndText";

	public string WheelFontFamily { get; set; } = "Microsoft YaHei UI, Segoe UI";

	public double SectorIconSize { get; set; } = 20.0;

	public double SectorFontSize { get; set; } = 11.0;

	public string CoreFontFamily { get; set; } = "Microsoft YaHei UI, Segoe UI";

	public double CoreFontSize { get; set; } = 13.0;

	public string CoreTextColor { get; set; } = "#FFFFFFFF";

	public string CoreTitle { get; set; } = "StarPie";

	public string CoreSubtitle { get; set; } = "RMB Drag";

	public bool ShowCoreIcon { get; set; } = true;

	public string CoreIconType { get; set; } = "Exit";

	public string CoreCustomIconKey { get; set; } = "";

	public string CoreCustomIconSvg { get; set; } = "";

	public string CoreCustomImagePath { get; set; } = "";

	public string CoreCustomImageStretch { get; set; } = "UniformToFill";

	public double CoreIconScale { get; set; } = 1.0;

	public double CoreImageOffsetX { get; set; }

	public double CoreImageOffsetY { get; set; }

	public string HighlightGlowPreset { get; set; } = "Auto";

	public string HighlightGlowColor { get; set; } = "";

	public double HighlightGlowRadius { get; set; } = 24.0;

	public double HighlightGlowOpacity { get; set; } = 0.85;

	public string CustomSectorBg { get; set; } = "#9016161A";

	public string CustomSectorBorder { get; set; } = "#35FFFFFF";

	public string CustomHighlightBg { get; set; } = "#E06C4DFF";

	public string CustomHighlightBorder { get; set; } = "#A0FFFFFF";

	public string CustomText { get; set; } = "#E0FFFFFF";

	public List<CustomColorPreset> CustomColorPresets { get; set; } = new List<CustomColorPreset>();

	public string WheelBgImagePath { get; set; } = "";

	public double WheelBgOpacity { get; set; } = 0.8;

	public string WheelBgStretch { get; set; } = "UniformToFill";

	public string CoreBgImagePath { get; set; } = "";

	public double CoreBgOpacity { get; set; } = 1.0;

	public string CoreBgStretch { get; set; } = "UniformToFill";

	public string HighlightTexturePath { get; set; } = "";

	public double HighlightTextureOpacity { get; set; } = 0.7;

	public List<WheelProfile> Profiles { get; set; } = new List<WheelProfile>();

	public string IsolationMode { get; set; } = "Blacklist";

	public List<string> BlacklistedProcesses { get; set; } = new List<string> { "mstsc.exe", "paint.exe" };

	public List<string> WhitelistedProcesses { get; set; } = new List<string>();

	public bool DisableOnCtrl { get; set; }

	public bool DisableOnShift { get; set; }

	public bool DisableOnAlt { get; set; }

	public bool DisableOnFullScreen { get; set; } = true;

	public bool AutoCheckUpdate { get; set; } = true;

	public string UpdateChannel { get; set; } = "Stable";

	public string UpdateProxySource { get; set; } = "ghproxy";

	public string CustomProxyUrl { get; set; } = "";

	public string LastCheckUpdateTime { get; set; } = "";

	public string IgnoredVersion { get; set; } = "";
}

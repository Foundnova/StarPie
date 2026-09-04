using System.Collections.Generic;

namespace WinPieGestures;

public class ActionItem
{
	public string Type { get; set; } = "Hotkey";

	public string Name { get; set; } = "快捷动作";

	public string Parameter { get; set; } = "";

	public string Arguments { get; set; } = "";

	public string IconKey { get; set; } = "";

	public string CustomIconSvg { get; set; } = "";

	/// <summary>Terminal used to run a "Command" action: "cmd", "powershell", "wsl" or "direct".</summary>
	public string CommandTerminal { get; set; } = "cmd";

	/// <summary>用于 "WebUrl" 动作的目标浏览器："Default" (系统默认), "Chrome", "Edge", "Firefox", "Custom"</summary>
	public string BrowserChoice { get; set; } = "Default";

	/// <summary>自定义浏览器可执行文件完整路径（当 BrowserChoice 为 "Custom" 时使用）</summary>
	public string BrowserPath { get; set; } = "";

	/// <summary>继承图标的本地程序路径（解耦执行动作与视觉图标，如快捷键继承 QQ.exe 图标）</summary>
	public string? InheritAppIconPath { get; set; } = "";

	/// <summary>是否以普通桌面用户常规权限启动（通过 Shell 令牌降权，解决高权限下外部文件无法拖入目标软件的问题）</summary>
	public bool RunAsStandardUser { get; set; } = false;

	/// <summary>独立排版模式覆盖："Inherit" (继承全局), "IconAndText", "IconOnly", "TextOnly"</summary>
	public string? LayoutMode { get; set; } = "Inherit";

	/// <summary>独立文字颜色覆盖：null 或 "" 表示继承全局，支持 "#RRGGBB" 或 "#AARRGGBB"</summary>
	public string? CustomTextColor { get; set; } = "";

	/// <summary>独立字体覆盖：null 或 "" 表示继承全局</summary>
	public string? CustomFontFamily { get; set; } = "";

	/// <summary>独立图标大小覆盖：null 或 <=0 表示继承全局</summary>
	public double? CustomIconSize { get; set; } = null;

	/// <summary>独立文字字号覆盖：null 或 <=0 表示继承全局</summary>
	public double? CustomFontSize { get; set; } = null;

	/// <summary>独立文字相对位置覆盖：null 或 "Inherit" 表示继承全局，"Below", "Above"</summary>
	public string? CustomTextPlacement { get; set; } = "Inherit";

	/// <summary>独立文字水平偏移覆盖：null 表示继承全局</summary>
	public double? CustomTextOffsetX { get; set; } = null;

	/// <summary>独立文字垂直偏移覆盖：null 表示继承全局</summary>
	public double? CustomTextOffsetY { get; set; } = null;

	public List<ActionItem> SubActions { get; set; } = new List<ActionItem>();

	public ActionItem Clone()
	{
		ActionItem clone = new ActionItem
		{
			Type = this.Type,
			Name = this.Name,
			Parameter = this.Parameter,
			Arguments = this.Arguments,
			IconKey = this.IconKey,
			CustomIconSvg = this.CustomIconSvg,
			CommandTerminal = this.CommandTerminal,
			BrowserChoice = this.BrowserChoice,
			BrowserPath = this.BrowserPath,
			InheritAppIconPath = this.InheritAppIconPath,
			RunAsStandardUser = this.RunAsStandardUser,
			LayoutMode = this.LayoutMode,
			CustomTextColor = this.CustomTextColor,
			CustomFontFamily = this.CustomFontFamily,
			CustomIconSize = this.CustomIconSize,
			CustomFontSize = this.CustomFontSize,
			CustomTextPlacement = this.CustomTextPlacement,
			CustomTextOffsetX = this.CustomTextOffsetX,
			CustomTextOffsetY = this.CustomTextOffsetY,
			SubActions = new List<ActionItem>()
		};
		if (this.SubActions != null)
		{
			foreach (var sub in this.SubActions)
			{
				clone.SubActions.Add(sub.Clone());
			}
		}
		return clone;
	}

	public override string ToString()
	{
		return Name;
	}
}

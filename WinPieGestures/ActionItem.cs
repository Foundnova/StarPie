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

	public List<ActionItem> SubActions { get; set; } = new List<ActionItem>();

	public override string ToString()
	{
		return Name;
	}
}

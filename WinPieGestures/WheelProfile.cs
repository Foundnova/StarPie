using System.Collections.Generic;

namespace WinPieGestures;

public class WheelProfile
{
	public string ProcessName { get; set; } = "Global";

	public int SectorCount { get; set; } = 8;

	public List<ActionItem> Actions { get; set; } = new List<ActionItem>();

	/// <summary>中心核心圆死区动作（在外甩脱离取消开启时，松开光标于中心死区触发）</summary>
	public ActionItem? CenterAction { get; set; }

	/// <summary>是否启用中心核心圆动作</summary>
	public bool EnableCenterAction { get; set; } = false;

	public override string ToString()
	{
		return ProcessName;
	}
}

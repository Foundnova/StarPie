using System.Collections.Generic;

namespace WinPieGestures;

public class WheelProfile
{
	public string ProcessName { get; set; } = "Global";

	public int SectorCount { get; set; } = 8;

	public List<ActionItem> Actions { get; set; } = new List<ActionItem>();

	public override string ToString()
	{
		return ProcessName;
	}
}

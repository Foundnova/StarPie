namespace WinPieGestures;

public class SystemPresetItem
{
	public string Key { get; set; } = "";

	public string Category { get; set; } = "";

	public string DisplayName { get; set; } = "";

	public string DefaultName { get; set; } = "";

	public string DefaultIconKey { get; set; } = "";

	public string FormattedDisplay => "[" + Category + "] " + DisplayName;
}

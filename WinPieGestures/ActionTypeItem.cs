namespace WinPieGestures;

public class ActionTypeItem
{
	public string Tag { get; set; } = "";

	public string DisplayText { get; set; } = "";

	/// <summary>
	/// 插件动作引用。仅当本项代表<b>某个具体的插件动作</b>时非空。
	/// <para>
	/// 为什么需要它：所有插件动作的 <see cref="Tag"/> 前缀都是 <c>Plugin:</c>，
	/// 而下拉框用 <c>SelectedValuePath="Tag"</c> 回写类型，纯靠 Tag 字符串无法还原出
	/// 「这个动作属于哪个插件的哪个贡献点」。这里把引用随项一起带出来，选中时直接落库，
	/// 兔去了在 UI 层反查注册表的往返。
	/// </para>
	/// </summary>
	public StarPie.Plugin.PluginActionRef? PluginRef { get; set; }
}

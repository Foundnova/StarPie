using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using StarPie.Plugin;

namespace WinPieGestures.Plugins.BuiltinActions;

/// <summary>
/// 内建动作「窗口置顶」的插件模型实现。
/// <para>
/// <b>无参数。</b> <see cref="WindowTiler.ToggleWindowTopmost"/> 其实还支持一个
/// 「强制置顶」的取值（<c>"1"</c> / <c>"on"</c>），但界面上从来只写入空串 ——
/// 也就是「切换」这一种行为。为一个界面上并不存在的选择声明参数字段，
/// 等于凭空发明一份用户无法理解的配置；将来真需要时再加字段即可。
/// </para>
/// </summary>
internal sealed class BuiltinActionToggleTopmost : IActionContribution
{
	public ActionDescriptor Descriptor => new()
	{
		Id = "toggleTopmost",
		DisplayName = I18n.T("ActionTypeTopmostShort"),
		Description = null,
		Category = "",
		IconKey = null,
		Kind = ActionKind.Sequential,
		TimeoutSeconds = 0,
	};

	/// <summary>
	/// 无参数。
	/// <para>
	/// 注意<b>不要</b>因为「执行体接受一个参数」就补一个字段：那个参数的默认语义
	/// （空串 = 切换）正是界面上唯一会产生的配置，补字段只会让统一表单多出一个
	/// 没人知道该怎么填的输入框。
	/// </para>
	/// </summary>
	public IReadOnlyList<ParameterField> Parameters => Array.Empty<ParameterField>();

	public string? Validate(IReadOnlyDictionary<string, string> parameters) => null;

	public string Preview(IReadOnlyDictionary<string, string> parameters) => "";

	public Task<ActionResult> ExecuteAsync(PluginActionInput input, CancellationToken cancellationToken)
	{
		// 传空串 = 切换当前置顶状态，与原 switch 分支里 action.Parameter 恰好为空时的行为一致。
		WindowTiler.ToggleWindowTopmost("");
		return Task.FromResult(ActionResult.Empty);
	}

	public static BuiltinActionRegistration Create() => new()
	{
		Type = "ToggleTopmost",
		Aliases = Array.Empty<string>(),
		FullId = BuiltinActionCatalog.ProviderId + ".toggleTopmost",
		Contribution = new BuiltinActionToggleTopmost(),
	};
}

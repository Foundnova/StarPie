using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using StarPie.Plugin;

namespace WinPieGestures.Plugins.BuiltinActions;

/// <summary>
/// 内建动作「系统与右键工具」的插件模型实现。
/// <para>
/// <b>参数刻意声明成自由文本，而不是枚举</b>：这个动作有一张 18 项的工具栏，
/// 但它在 UI 上已经有一个比下拉更好的入口 —— <c>ShellActionPickerWindow</c>
/// （带搜索、带分类、带说明的专用挑选器）。把 18 项压进一枚通用下拉是<b>体验降级</b>，
/// 而且会让工具清单出现两份（挑选器一份、这里一份），以后加一个工具要改两处。
/// </para>
/// <para>
/// 所以这里声明成 <see cref="ParameterFieldType.Text"/>，值就是工具 ID
/// （<c>copy_path</c> / <c>lock_screen</c> …）。统一表单将来渲染它时能显示与编辑原始值，
/// 但正式入口仍然是那个专用挑选器 —— 这是「统一形状」与「保留领域专用 UI」并不矛盾的一例。
/// </para>
/// </summary>
internal sealed class BuiltinActionShellTool : IActionContribution
{
	private const string KeyVerb = "verb";

	public ActionDescriptor Descriptor => new()
	{
		Id = "shellTool",
		DisplayName = I18n.T("ActionTypeShellToolShort"),
		Description = null,
		Category = "",
		IconKey = null,
		Kind = ActionKind.Sequential,
		TimeoutSeconds = 0,
	};

	public IReadOnlyList<ParameterField> Parameters => new ParameterField[]
	{
		new()
		{
			Key = KeyVerb,
			Label = "工具标识",
			Type = ParameterFieldType.Text,
			Required = true,
			Placeholder = "copy_path",
			HelpText = "建议通过「⚡ 挑选功能…」按钮选择，那里有搜索、分类与功能说明。",
		},
	};

	/// <summary>
	/// <b>只挡空值</b>：<see cref="ActionExecutor.ExecuteShellTool"/> 的 <c>switch</c>
	/// 为每个工具同时接受两套命名（<c>copy_path</c> 与 <c>Windows.CopyAsPath</c>），
	/// 按某一套白名单校验会把另一套的老配置判死。
	/// </summary>
	public string? Validate(IReadOnlyDictionary<string, string> parameters)
	{
		string verb = parameters != null && parameters.TryGetValue(KeyVerb, out string? value)
			? value ?? ""
			: "";

		return string.IsNullOrWhiteSpace(verb)
			? "未选择系统工具，请点击「⚡ 挑选功能…」指定一个。"
			: null;
	}

	/// <summary>列表副标题。工具 ID 是英文标识，列表里回显它比空白有用。</summary>
	public string Preview(IReadOnlyDictionary<string, string> parameters)
	{
		string verb = parameters != null && parameters.TryGetValue(KeyVerb, out string? value)
			? (value ?? "").Trim()
			: "";

		return verb.Length <= 48 ? verb : verb.Substring(0, 47) + "…";
	}

	public Task<ActionResult> ExecuteAsync(PluginActionInput input, CancellationToken cancellationToken)
	{
		string verb = input?.Parameter(KeyVerb) ?? "";

		if (string.IsNullOrWhiteSpace(verb))
		{
			return Task.FromResult(ActionResult.Fail("未选择系统工具。"));
		}

		ActionExecutor.ExecuteShellTool(verb);
		return Task.FromResult(ActionResult.Empty);
	}

	public static BuiltinActionRegistration Create() => new()
	{
		Type = "ShellTool",
		Aliases = Array.Empty<string>(),
		FullId = BuiltinActionCatalog.ProviderId + ".shellTool",
		Contribution = new BuiltinActionShellTool(),
		ProjectParameters = action =>
		{
			Dictionary<string, string> map = BuiltinActionCatalog.SeedFromExtensionData(action);

			if (!map.ContainsKey(KeyVerb)) map[KeyVerb] = action.Parameter ?? "";

			return map;
		},
	};
}

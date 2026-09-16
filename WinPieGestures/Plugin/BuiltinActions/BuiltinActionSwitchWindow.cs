using System;
using System.Collections.Generic;
using System.Globalization;
using System.Threading;
using System.Threading.Tasks;
using StarPie.Plugin;

namespace WinPieGestures.Plugins.BuiltinActions;

/// <summary>
/// 内建动作「切换应用」的插件模型实现。
/// <para>
/// 参数是任务栏上的第几个槽位（从 1 开始）。执行体通过
/// <c>WindowTaskbarHelper.ActivateTaskbarSlot</c> 激活对应位置的应用。
/// </para>
/// </summary>
internal sealed class BuiltinActionSwitchWindow : IActionContribution
{
	private const string KeyIndex = "index";

	public ActionDescriptor Descriptor => new()
	{
		Id = "switchWindow",
		DisplayName = I18n.T("ActionTypeSwitchWindowShort"),
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
			Key = KeyIndex,
			Label = "任务栏位置",
			Type = ParameterFieldType.Number,
			Required = true,
			DefaultValue = "1",
			Min = 1,
			Max = 20,
			HelpText = "任务栏上从左往右数的第几个图标，从 1 开始。",
		},
	};

	/// <summary>
	/// <b>比原行为更严，是刻意的</b>：原 <see cref="ActionExecutor.ExecuteSwitchWindow"/>
	/// 对解析失败或非正数的参数会静默退回第 1 个槽位 —— 用户配的是「第 3 个」，
	/// 按下去却是第 1 个应用被切出来，而且没有任何提示说明为什么。
	/// </summary>
	public string? Validate(IReadOnlyDictionary<string, string> parameters)
	{
		string raw = parameters != null && parameters.TryGetValue(KeyIndex, out string? value)
			? (value ?? "").Trim()
			: "";

		if (raw.Length == 0) return "未设置任务栏位置。";

		if (!int.TryParse(raw, NumberStyles.Integer, CultureInfo.InvariantCulture, out int parsed))
		{
			return $"「{raw}」不是有效的序号，请填写一个正整数。";
		}

		return parsed < 1
			? "任务栏位置需从 1 开始计数。"
			: null;
	}

	/// <summary>列表副标题。</summary>
	public string Preview(IReadOnlyDictionary<string, string> parameters)
	{
		string raw = parameters != null && parameters.TryGetValue(KeyIndex, out string? value)
			? (value ?? "").Trim()
			: "";

		return raw.Length == 0 ? "" : $"第 {raw} 个";
	}

	public Task<ActionResult> ExecuteAsync(PluginActionInput input, CancellationToken cancellationToken)
	{
		string raw = input?.Parameter(KeyIndex) ?? "";

		if (string.IsNullOrWhiteSpace(raw))
		{
			return Task.FromResult(ActionResult.Fail("未设置任务栏位置。"));
		}

		ActionExecutor.ExecuteSwitchWindow(raw);
		return Task.FromResult(ActionResult.Empty);
	}

	public static BuiltinActionRegistration Create() => new()
	{
		Type = "SwitchWindow",
		Aliases = Array.Empty<string>(),
		FullId = BuiltinActionCatalog.ProviderId + ".switchWindow",
		Contribution = new BuiltinActionSwitchWindow(),
		ProjectParameters = action =>
		{
			Dictionary<string, string> map = BuiltinActionCatalog.SeedFromExtensionData(action);

			if (!map.ContainsKey(KeyIndex)) map[KeyIndex] = action.Parameter ?? "";

			return map;
		},
	};
}

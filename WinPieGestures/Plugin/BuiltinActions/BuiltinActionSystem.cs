using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using StarPie.Plugin;

namespace WinPieGestures.Plugins.BuiltinActions;

/// <summary>
/// 内建动作「系统控制」的插件模型实现。
/// <para>
/// 参数是一个预设键（<c>Minimize</c> / <c>Shutdown</c> …）。手写面板用的是一枚下拉，
/// 数据源是 <see cref="SlotViewModel.SystemPresets"/> —— 这里<b>直接复用同一张表</b>
/// 生成选项，不另抄一份：抄一份的代价是以后加预设要改两处，漏一处就会出现
/// 「新预设在这个面板里能选、在那个面板里选不到」的分裂。
/// </para>
/// </summary>
internal sealed class BuiltinActionSystem : IActionContribution
{
	private const string KeyPreset = "preset";

	/// <summary>
	/// 参数声明。
	/// <para>
	/// 取的是有序的 <see cref="SlotViewModel.SystemPresetList"/> 而不是
	/// <see cref="SlotViewModel.SystemPresets"/>：后者是 <c>Dictionary</c>，
	/// 枚举顺序不保证，下拉里的条目会莫名其妙地跳动。
	/// </para>
	/// </summary>
	public IReadOnlyList<ParameterField> Parameters => new ParameterField[]
	{
		new()
		{
			Key = KeyPreset,
			Label = "系统功能",
			Type = ParameterFieldType.Enum,
			Required = true,
			Options = SlotViewModel.SystemPresetList
				.Select(item => new ParameterOption { Value = item.Key, Label = item.FormattedDisplay })
				.ToList(),
		},
	};

	public ActionDescriptor Descriptor => new()
	{
		Id = "system",
		DisplayName = I18n.T("ActionTypeSystemShort"),
		Description = null,
		Category = "",
		IconKey = null,
		Kind = ActionKind.Sequential,
		TimeoutSeconds = 0,
	};

	/// <summary>
	/// <b>只挡空值，刻意不校验「这个键在不在预设表里」</b>：
	/// <see cref="ActionExecutor.ExecuteSystem"/> 的 <c>switch</c> 里保留了一些
	/// 不在当前预设表里的历史别名，一旦按表校验，那些年代的配置会被判成非法而无法执行 ——
	/// 本想防静默失效，结果造出一个更糟的静默失效。
	/// </summary>
	public string? Validate(IReadOnlyDictionary<string, string> parameters)
	{
		string preset = parameters != null && parameters.TryGetValue(KeyPreset, out string? value)
			? value ?? ""
			: "";

		return string.IsNullOrWhiteSpace(preset)
			? "未选择系统功能，请在动作设置里指定要执行的操作。"
			: null;
	}

	/// <summary>列表副标题：回显预设的中文名（查不到就回显原键，至少不是空白）。</summary>
	public string Preview(IReadOnlyDictionary<string, string> parameters)
	{
		string preset = parameters != null && parameters.TryGetValue(KeyPreset, out string? value)
			? (value ?? "").Trim()
			: "";

		if (preset.Length == 0) return "";

		return SlotViewModel.SystemPresets.TryGetValue(preset, out string? display) && !string.IsNullOrWhiteSpace(display)
			? display
			: preset;
	}

	public Task<ActionResult> ExecuteAsync(PluginActionInput input, CancellationToken cancellationToken)
	{
		string preset = input?.Parameter(KeyPreset) ?? "";

		if (string.IsNullOrWhiteSpace(preset))
		{
			return Task.FromResult(ActionResult.Fail("未选择系统功能。"));
		}

		ActionExecutor.ExecuteSystem(preset);
		return Task.FromResult(ActionResult.Empty);
	}

	public static BuiltinActionRegistration Create() => new()
	{
		Type = "System",
		Aliases = Array.Empty<string>(),
		FullId = BuiltinActionCatalog.ProviderId + ".system",
		Contribution = new BuiltinActionSystem(),
		ProjectParameters = action =>
		{
			Dictionary<string, string> map = BuiltinActionCatalog.SeedFromExtensionData(action);

			if (!map.ContainsKey(KeyPreset)) map[KeyPreset] = action.Parameter ?? "";

			return map;
		},
	};
}

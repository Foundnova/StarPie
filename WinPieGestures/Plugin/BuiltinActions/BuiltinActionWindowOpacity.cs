using System;
using System.Collections.Generic;
using System.Globalization;
using System.Threading;
using System.Threading.Tasks;
using StarPie.Plugin;

namespace WinPieGestures.Plugins.BuiltinActions;

/// <summary>
/// 内建动作「设置窗口透明度」的插件模型实现。
/// <para>
/// 参数是 1~100 的整数百分比（100 = 完全不透明）。这一范围不是这里规定的，
/// 而是执行体 <see cref="WindowTiler.SetWindowOpacity"/> 自己就写着「参数 1~100」并做了钳制 ——
/// 这里只是把它变成一条能提前告诉用户的校验。
/// </para>
/// </summary>
internal sealed class BuiltinActionWindowOpacity : IActionContribution
{
	private const string KeyOpacity = "opacity";
	private const double MinOpacity = 1.0;
	private const double MaxOpacity = 100.0;

	public ActionDescriptor Descriptor => new()
	{
		Id = "windowOpacity",
		DisplayName = I18n.T("ActionTypeOpacityShort"),
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
			Key = KeyOpacity,
			Label = "透明度 (%)",
			Type = ParameterFieldType.Number,
			Required = true,
			DefaultValue = "80",
			Min = MinOpacity,
			Max = MaxOpacity,
			HelpText = "1~100，100 表示完全不透明。",
		},
	};

	/// <summary>
	/// 校验。
	/// <para>
	/// 空值拦下是<b>刻意的收紧</b>：原执行体对解析失败的参数会静默退回 50%，
	/// 用户按下去会看到窗口变半透明 —— 而他配的可能是 80%。
	/// 「用了一个你没设过的值」比「告诉你没设过」糟糕得多。
	/// </para>
	/// <para>
	/// 越界同样拦下：执行体虽然会钳制到 1~100，但填 150 的人显然理解错了这个参数的含义，
	/// 悄悄把它变成 100 只会让误解继续存在。
	/// </para>
	/// </summary>
	public string? Validate(IReadOnlyDictionary<string, string> parameters)
	{
		string raw = parameters != null && parameters.TryGetValue(KeyOpacity, out string? value)
			? (value ?? "").Trim()
			: "";

		if (raw.Length == 0) return "未设置窗口透明度。";

		// 用不变文化解析：宿主写入的是 "80" 这样的不变文化字面量，
		// 按系统区域设置解析在部分区域会得到不同的结果。
		if (!double.TryParse(raw, NumberStyles.Float, CultureInfo.InvariantCulture, out double parsed))
		{
			return $"透明度「{raw}」不是有效数字，请填写 1~100 之间的值。";
		}

		return parsed < MinOpacity || parsed > MaxOpacity
			? $"透明度需在 1~100 之间（当前填写 {raw}）。"
			: null;
	}

	/// <summary>列表副标题：把裸数字补上单位与百分号。</summary>
	public string Preview(IReadOnlyDictionary<string, string> parameters)
	{
		string raw = parameters != null && parameters.TryGetValue(KeyOpacity, out string? value)
			? (value ?? "").Trim()
			: "";

		return raw.Length == 0 ? "" : $"{raw}%";
	}

	public Task<ActionResult> ExecuteAsync(PluginActionInput input, CancellationToken cancellationToken)
	{
		string raw = input?.Parameter(KeyOpacity) ?? "";

		if (string.IsNullOrWhiteSpace(raw))
		{
			return Task.FromResult(ActionResult.Fail("未设置窗口透明度。"));
		}

		// 钳制与默认值都由执行体负责（见 WindowTiler.SetWindowOpacity 的 1~100 clamp），
		// 这里不重复实现一遍 —— 两处各写一份，迟早会不一致。
		WindowTiler.SetWindowOpacity(raw);
		return Task.FromResult(ActionResult.Empty);
	}

	public static BuiltinActionRegistration Create() => new()
	{
		Type = "WindowOpacity",
		Aliases = Array.Empty<string>(),
		FullId = BuiltinActionCatalog.ProviderId + ".windowOpacity",
		Contribution = new BuiltinActionWindowOpacity(),
		ProjectParameters = action =>
		{
			Dictionary<string, string> map = BuiltinActionCatalog.SeedFromExtensionData(action);

			if (!map.ContainsKey(KeyOpacity)) map[KeyOpacity] = action.Parameter ?? "";

			return map;
		},
	};
}

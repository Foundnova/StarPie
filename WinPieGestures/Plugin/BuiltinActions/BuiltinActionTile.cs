using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using StarPie.Plugin;

namespace WinPieGestures.Plugins.BuiltinActions;

/// <summary>
/// 内建动作「平铺窗口」的插件模型实现。
/// <para>
/// <b>这一个动作承载了四种用户可见的语义</b>：指定布局、循环切换、反向循环、还原。
/// 它们在配置里都是 <c>Type="Tile"</c>，靠 <c>Parameter</c> 区分 ——
/// 具体布局码（<c>"2L"</c> 左半屏、<c>"4G"</c> 四宫格…）与三个特殊标记
/// （<c>Cycle</c> / <c>CycleBack</c> / <c>Restore</c>）。UI 侧则是「子模式下拉 +
/// 二级下拉」两级联动来产生这些组合。
/// </para>
/// <para>
/// 这里把两者合并成<b>一枚下拉</b>：17 个布局码加上 3 个特殊标记，共 20 项。
/// 这不是简化了功能，而是发现「子模式」本身就是多余的中间层 ——
/// 用户要表达的东西从头到尾只有「平铺成什么样」这一个选择。
/// </para>
/// <para>
/// <b>布局码表直接取自 <see cref="WindowTiler.LayoutKeys"/>，显示名取自
/// <see cref="WindowTiler.LayoutDisplayName"/></b>，不另抄一份。抄一份的后果是
/// 以后加布局要改两处，漏一处就会出现「新布局在下拉里能选、在动作里不生效」。
/// </para>
/// </summary>
internal sealed class BuiltinActionTile : IActionContribution
{
	private const string KeyLayout = "layout";

	public ActionDescriptor Descriptor => new()
	{
		Id = "tile",
		DisplayName = I18n.T("ActionTypeTileShort"),
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
			Key = KeyLayout,
			Label = "平铺方式",
			Type = ParameterFieldType.Enum,
			Required = true,
			DefaultValue = "2L",
			Options = BuildOptions(),
		},
	};

	/// <summary>
	/// 构建选项表：先铺全部布局码，再追加三个特殊标记。
	/// <para>
	/// 顺序是刻意的 —— 特殊标记放最后，因为它们不是「一种布局」，而是对布局的操作。
	/// 混在布局码中间会让用户以为 <c>循环切换</c> 是某种分屏方式。
	/// </para>
	/// </summary>
	private static IReadOnlyList<ParameterOption> BuildOptions()
	{
		var options = new List<ParameterOption>();

		foreach (string layoutKey in WindowTiler.LayoutKeys)
		{
			options.Add(new ParameterOption
			{
				Value = layoutKey,
				Label = WindowTiler.LayoutDisplayName(layoutKey),
			});
		}

		options.Add(new ParameterOption { Value = WindowTiler.CycleParam, Label = I18n.T("TileCycleLabel") });
		options.Add(new ParameterOption { Value = WindowTiler.CycleBackParam, Label = I18n.T("TileCycleBackLabel") });
		options.Add(new ParameterOption { Value = WindowTiler.RestoreParam, Label = I18n.T("TileRestoreAllLabel") });

		return options;
	}

	/// <summary>
	/// <b>比原行为更严，是刻意的</b>：原 <see cref="ActionExecutor"/> 把空的
	/// <c>Parameter</c> 直接交给 <see cref="WindowTiler.ExecuteTile"/>，后者会<b>静默</b>
	/// 当成默认布局 <c>"2L"</c> 处理 —— 用户按下去会看到窗口被排成左半屏，
	/// 而他根本没有配过这个布局。
	/// </summary>
	public string? Validate(IReadOnlyDictionary<string, string> parameters)
	{
		string layout = parameters != null && parameters.TryGetValue(KeyLayout, out string? value)
			? (value ?? "").Trim()
			: "";

		return string.IsNullOrWhiteSpace(layout)
			? "未选择平铺方式。"
			: null;
	}

	/// <summary>列表副标题：布局码一律翻译成中文名，配置里看不到 <c>2L</c> 这种代号。</summary>
	public string Preview(IReadOnlyDictionary<string, string> parameters)
	{
		string layout = parameters != null && parameters.TryGetValue(KeyLayout, out string? value)
			? (value ?? "").Trim()
			: "";

		if (layout.Length == 0) return "";

		if (string.Equals(layout, WindowTiler.CycleParam, StringComparison.OrdinalIgnoreCase))
		{
			return I18n.T("TileCycleLabel");
		}

		if (string.Equals(layout, WindowTiler.CycleBackParam, StringComparison.OrdinalIgnoreCase))
		{
			return I18n.T("TileCycleBackLabel");
		}

		if (string.Equals(layout, WindowTiler.RestoreParam, StringComparison.OrdinalIgnoreCase))
		{
			return I18n.T("TileRestoreAllLabel");
		}

		return WindowTiler.IsValidLayout(layout)
			? WindowTiler.LayoutDisplayName(layout)
			: layout;
	}

	public Task<ActionResult> ExecuteAsync(PluginActionInput input, CancellationToken cancellationToken)
	{
		string layout = input?.Parameter(KeyLayout) ?? "";

		if (string.IsNullOrWhiteSpace(layout))
		{
			return Task.FromResult(ActionResult.Fail("未选择平铺方式。"));
		}

		// ExecuteTile 内部已经把 Cycle / CycleBack / Restore 三个标记与具体布局码
		// 分派到各自的实现上，这里不需要再分一遍 —— 动作层只负责把参数送到。
		WindowTiler.ExecuteTile(layout);
		return Task.FromResult(ActionResult.Empty);
	}

	public static BuiltinActionRegistration Create() => new()
	{
		Type = "Tile",
		Aliases = Array.Empty<string>(),
		FullId = BuiltinActionCatalog.ProviderId + ".tile",
		Contribution = new BuiltinActionTile(),
		ProjectParameters = action =>
		{
			Dictionary<string, string> map = BuiltinActionCatalog.SeedFromExtensionData(action);

			if (!map.ContainsKey(KeyLayout)) map[KeyLayout] = action.Parameter ?? "";

			return map;
		},
	};
}

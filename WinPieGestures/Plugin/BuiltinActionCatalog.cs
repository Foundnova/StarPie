using System;
using System.Collections.Generic;
using StarPie.Plugin;

// 动作实现按动作各占一个文件放在 BuiltinActions/ 下。父命名空间看不到子命名空间里的类型，
// 所以这里显式引入一次 —— 比在每个登记项前面反复写 "BuiltinActions." 前缀干净。
using WinPieGestures.Plugins.BuiltinActions;

namespace WinPieGestures.Plugins;

/// <summary>
/// 一个<b>内建</b>动作的登记项。
/// <para>
/// <b>为什么需要它</b>：内置动作与插件动作长期是两套模型 —— 前者硬编码在
/// <see cref="ActionExecutor"/> 的 <c>switch</c> 里、参数散落在 <see cref="ActionItem"/>
/// 的若干裸字段上；后者走 <see cref="IActionContribution"/> + 声明式参数表单。
/// 两套并行意味着「加一个内置动作」要同时改 XAML、改事件处理、改 switch、改持久化，
/// 而插件动作只需要写一个实现。
/// </para>
/// <para>
/// 这个类型是内建动作向插件模型<b>收敛</b>的载体：形状与插件完全一致
/// （同一个 <see cref="IActionContribution"/>、同一份 <see cref="ParameterField"/> 声明），
/// 差别只有两点，且都是刻意为之：
/// <list type="number">
/// <item>内建动作在<b>编译期静态注册</b>，不经过扫描 / 安装 / <c>AssemblyLoadContext</c> ——
/// 这是红线 R1「不装插件时零开销」的前提，一旦走了反射加载就破了；</item>
/// <item>内建动作<b>不出现在「插件」列表里</b>，用户无法禁用它们 ——
/// 否则他辛辛苦苦配好的扇区会大面积失效，而且原因他根本找不到。</item>
/// </list>
/// </para>
/// </summary>
internal sealed class BuiltinActionRegistration
{
	/// <summary>
	/// 对应 <see cref="ActionItem.Type"/> 的取值，例如 <c>"Command"</c>。
	/// <para>
	/// <b>刻意不改动这个值</b>：它本身就是内建动作稳定的身份标识，且已被用户配置文件引用。
	/// 若改成像插件那样统一成 <c>Type="Plugin"</c> + 一层间接引用，所有老配置都得迁移 ——
	/// 换不来任何收益，却引入一次「用户配置可能读错」的风险。
	/// </para>
	/// </summary>
	public string Type { get; init; } = "";

	/// <summary>
	/// 该动作在配置里出现过的其它写法。
	/// <para>
	/// 历史遗留：<c>"Folder"</c> 与 <c>"OpenFolder"</c> 是同一个动作的两种写法，
	/// <c>"WebUrl"</c> 与 <c>"Url"</c> 同理。原 <c>switch</c> 里靠 <c>case</c> 落空来实现，
	/// 这里改成显式声明，免得漏掉一个别名就出现「同一个动作时灵时不灵」。
	/// </para>
	/// </summary>
	public IReadOnlyList<string> Aliases { get; init; } = Array.Empty<string>();

	/// <summary>归一化后的全局唯一 ID，形如 <c>starpie.builtin.command</c>。</summary>
	public string FullId { get; init; } = "";

	/// <summary>
	/// 动作实现。与插件贡献点<b>同一个接口</b>，因此参数声明、校验、预览、执行
	/// 四件事的写法与插件完全一致，不再有第二套约定。
	/// </summary>
	public IActionContribution Contribution { get; init; } = null!;

	/// <summary>
	/// 把动作对象投影成参数字典。
	/// <para>
	/// <b>这是「零迁移」的关键</b>：内建动作的参数目前仍存在 <see cref="ActionItem"/> 的裸字段上
	/// （<c>Parameter</c> / <c>CommandTerminal</c> / <c>Arguments</c> …），而这些字段在全项目有近
	/// 两百处引用，整体搬到 <see cref="ActionItem.ExtensionData"/> 是一次高风险大改动。
	/// 这里让宿主在调用前<b>现读现装</b>成字典，于是执行侧完全统一，
	/// 持久化侧却一行都不用动。将来若要真正迁移，只需替换这一个函数。
	/// </para>
	/// <para>
	/// 投影<b>必须无副作用</b>，且返回的字典是给实现方的私有拷贝 ——
	/// 实现方改写它不能污染用户正在编辑的配置对象。
	/// </para>
	/// </summary>
	public Func<ActionItem, Dictionary<string, string>> ProjectParameters { get; init; } =
		BuiltinActionCatalog.SeedFromExtensionData;
}

/// <summary>
/// 内建动作目录。
/// <para>
/// <b>与 <see cref="PluginCatalog"/> 分开是刻意的</b>，不要合并：后者的语义是
/// 「插件贡献点」，它按 <c>pluginId</c> 成批撤销、参与启动健康记账、失败累积到阈值会被
/// 自动隔离。这些行为对插件是对的，对内建动作全是错的 —— 内建动作的生命期就是进程生命期，
/// 不该被撤销、不该被计健康度、更不该因为「坏了三次」就被自动禁用。
/// </para>
/// <para>
/// 两者共用 <see cref="IActionContribution"/> 只是<b>接口复用</b>，不是同一个注册表。
/// 对外呈现（参数表单、动作下拉、执行派发）则统一按同一套形状处理。
/// </para>
/// </summary>
internal static class BuiltinActionCatalog
{
	/// <summary>内建动作的提供方 ID。不是插件，但需要一个稳定的命名空间来生成 <c>FullId</c>。</summary>
	public const string ProviderId = "starpie.builtin";

	private static readonly Dictionary<string, BuiltinActionRegistration> s_byType = Build();

	/// <summary>按 <see cref="ActionItem.Type"/> 查内建动作。未迁移的动作返回 false，调用方应回退到原执行路径。</summary>
	public static bool TryGet(string? type, out BuiltinActionRegistration registration)
	{
		if (string.IsNullOrWhiteSpace(type))
		{
			registration = null!;
			return false;
		}

		return s_byType.TryGetValue(type.Trim(), out registration!);
	}

	/// <summary>全部已登记的内建动作（供后续 UI 层统一渲染用）。</summary>
	public static List<BuiltinActionRegistration> SnapshotAll() =>
		new List<BuiltinActionRegistration>(s_byType.Values.Count == 0
			? Array.Empty<BuiltinActionRegistration>()
			: Deduplicate());

	private static BuiltinActionRegistration[] Deduplicate()
	{
		// 同一个动作会因别名在字典里出现多次，这里按 FullId 去重后再交付，
		// 否则「动作清单」里同一个动作会被列两遍。
		var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
		var list = new List<BuiltinActionRegistration>();

		foreach (BuiltinActionRegistration registration in s_byType.Values)
		{
			if (seen.Add(registration.FullId)) list.Add(registration);
		}

		return list.ToArray();
	}

	/// <summary>
	/// 构建内建动作表。
	/// <para>
	/// <b>这里是唯一需要改动的地方</b>：新增一个内建动作 = 加一起项声明。
	/// 没有反射、没有程序集扫描、没有延迟加载 —— 本方法返回时全部动作都已就绪，
	/// 所以「不装插件时的开销」与「装了插件」完全无关。
	/// </para>
	/// <para>
	/// <b>新增动作时务必把 <c>Type</c> 与历史取值对齐</b>：用户配置里存的就是这个字符串，
	/// 改一个字母就会让老配置里的动作变成「未知类型」而静默失效。
	/// </para>
	/// </summary>
	private static Dictionary<string, BuiltinActionRegistration> Build()
	{
		var list = new List<BuiltinActionRegistration>
		{
			// 顶层动作类型里参数面最清晰的一批：单层参数、无子模式。
			BuiltinActionHotkey.Create(),
			BuiltinActionLaunch.Create(),
			BuiltinActionWebUrl.Create(),
			BuiltinActionFolder.Create(),
			BuiltinActionCommand.Create(),
			BuiltinActionOcr.Create(),
			BuiltinActionSystem.Create(),
			BuiltinActionShellTool.Create(),

			// 尚未收敛的是窗口类动作（Tile / TileCycle / TileRestore / SwitchWindow /
			// MoveMonitor / ToggleTopmost / WindowOpacity）与 Text。
			// 它们的 Parameter 里编码的是「子模式 + 该子模式的取值」这种复合含义
			// （例如 Tile 的 "2L" 表示左半屏、CycleParam 表示轮换），
			// 拆成声明式 schema 之前得先想清楚这套编码要不要一并改掉 ——
			// 那是另一件事，不塞进这批里做。
		};

		var map = new Dictionary<string, BuiltinActionRegistration>(StringComparer.OrdinalIgnoreCase);

		foreach (BuiltinActionRegistration registration in list)
		{
			map[registration.Type] = registration;

			foreach (string alias in registration.Aliases)
			{
				// 别名冲突是声明错误，不能靠「后写的盖掉先写的」蒙混过去 ——
				// 那会让一个动作静默地接管另一个动作的配置。
				if (map.ContainsKey(alias))
				{
					throw new InvalidOperationException(
						$"内建动作别名冲突：'{alias}' 已被占用，无法再指向 {registration.FullId}。");
				}

				map[alias] = registration;
			}
		}

		return map;
	}

	/// <summary>
	/// 从裸字段投影出参数字典的通用前半段：先把 <see cref="ActionItem.ExtensionData"/>
	/// 整体铺进去（新模型写入的参数），再由各动作补上自己的裸字段映射。
	/// </summary>
	internal static Dictionary<string, string> SeedFromExtensionData(ActionItem action)
	{
		var map = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

		if (action.ExtensionData != null)
		{
			foreach (KeyValuePair<string, string> pair in action.ExtensionData)
			{
				if (pair.Key != null) map[pair.Key] = pair.Value ?? "";
			}
		}

		return map;
	}
}

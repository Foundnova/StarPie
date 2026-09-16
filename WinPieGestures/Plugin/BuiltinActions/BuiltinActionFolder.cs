using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using StarPie.Plugin;

namespace WinPieGestures.Plugins.BuiltinActions;

/// <summary>
/// 内建动作「打开文件夹」的插件模型实现。
/// <para>
/// <b>别名 <c>"OpenFolder"</c> 必须保留</b>：与 <c>"WebUrl"/"Url"</c> 同理，
/// 两种写法都进过用户配置。
/// </para>
/// </summary>
internal sealed class BuiltinActionFolder : IActionContribution
{
	private const string KeyPath = "path";

	public ActionDescriptor Descriptor => new()
	{
		Id = "folder",
		DisplayName = I18n.T("ActionTypeFolderShort"),
		Description = null,
		Category = "",
		IconKey = null,
		Kind = ActionKind.Sequential,
		TimeoutSeconds = 0,
	};

	/// <summary>
	/// 参数声明。
	/// <para>
	/// 值<b>不限于真实目录</b>：执行体还认 <c>::{...}</c> 与 <c>shell:</c> 开头的
	/// Shell 命名空间串（手写面板上的「💻 此电脑」「🗑️ 回收站」两枚预设给的就是这类值），
	/// 也认文件路径（此时会打开所在目录并选中它）。所以校验<b>只挡空值</b>，
	/// 一旦加上 <c>Directory.Exists</c> 这类的存在性检查，那两枚预设会被判成非法输入。
	/// </para>
	/// <para>
	/// 「常用目录」那五枚芯片是填值辅助，不是参数本身，本轮不动。
	/// </para>
	/// </summary>
	public IReadOnlyList<ParameterField> Parameters => new ParameterField[]
	{
		new()
		{
			Key = KeyPath,
			Label = "文件夹路径",
			Type = ParameterFieldType.Folder,
			Required = true,
			HelpText = "支持普通路径、文件路径（会定位并选中该文件）以及 shell: 命名空间。",
		},
	};

	/// <summary>
	/// <b>比原行为更严，是刻意的</b>：原 <see cref="ActionExecutor.ExecuteFolder"/>
	/// 遇到空路径直接 <c>return</c>，按下去毫无反应。
	/// </summary>
	public string? Validate(IReadOnlyDictionary<string, string> parameters)
	{
		string path = parameters != null && parameters.TryGetValue(KeyPath, out string? value)
			? value ?? ""
			: "";

		return string.IsNullOrWhiteSpace(path)
			? "未设置文件夹路径，请在动作设置里选择要打开的目录。"
			: null;
	}

	/// <summary>列表副标题：取最后一段目录名，Shell 命名空间串原样回显。</summary>
	public string Preview(IReadOnlyDictionary<string, string> parameters)
	{
		string path = parameters != null && parameters.TryGetValue(KeyPath, out string? value)
			? (value ?? "").Trim().Trim('"')
			: "";

		if (path.Length == 0) return "";

		// Shell 命名空间（::{...} / shell:...）不按路径切分 —— 它的「最后一段」
		// 是一串 GUID，切出来不如整串好认。
		bool isShellNamespace =
			path.StartsWith("::{", StringComparison.OrdinalIgnoreCase) ||
			path.StartsWith("shell:", StringComparison.OrdinalIgnoreCase);

		if (!isShellNamespace)
		{
			int slash = path.LastIndexOfAny(new[] { '\\', '/' });
			if (slash >= 0 && slash < path.Length - 1) path = path.Substring(slash + 1);
		}

		return path.Length <= 48 ? path : path.Substring(0, 47) + "…";
	}

	public Task<ActionResult> ExecuteAsync(PluginActionInput input, CancellationToken cancellationToken)
	{
		string path = input?.Parameter(KeyPath) ?? "";

		if (string.IsNullOrWhiteSpace(path))
		{
			return Task.FromResult(ActionResult.Fail("未设置文件夹路径。"));
		}

		ActionExecutor.ExecuteFolder(path);
		return Task.FromResult(ActionResult.Empty);
	}

	public static BuiltinActionRegistration Create() => new()
	{
		Type = "Folder",
		Aliases = new[] { "OpenFolder" },
		FullId = BuiltinActionCatalog.ProviderId + ".folder",
		Contribution = new BuiltinActionFolder(),
		ProjectParameters = action =>
		{
			Dictionary<string, string> map = BuiltinActionCatalog.SeedFromExtensionData(action);

			if (!map.ContainsKey(KeyPath)) map[KeyPath] = action.Parameter ?? "";

			return map;
		},
	};
}

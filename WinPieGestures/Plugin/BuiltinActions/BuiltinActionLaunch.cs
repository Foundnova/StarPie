using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using StarPie.Plugin;

namespace WinPieGestures.Plugins.BuiltinActions;

/// <summary>
/// 内建动作「启动程序」的插件模型实现。
/// <para>
/// 三个参数正好落在三种字段类型上：路径（<see cref="ParameterFieldType.File"/>）、
/// 启动参数（<see cref="ParameterFieldType.Text"/>）、普通权限开关（<see cref="ParameterFieldType.Bool"/>）。
/// 手写面板上另有三枚辅助按钮（📦 软件库选择 / 🎯 捕捉运行窗口 / 📂 浏览），
/// 它们同样是「帮用户填路径」的辅助，不是参数 —— 本轮保持原样。
/// </para>
/// </summary>
internal sealed class BuiltinActionLaunch : IActionContribution
{
	private const string KeyPath = "path";
	private const string KeyArguments = "arguments";
	private const string KeyRunAsStandardUser = "runAsStandardUser";

	public ActionDescriptor Descriptor => new()
	{
		Id = "launch",
		DisplayName = I18n.T("ActionTypeLaunchShort"),
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
			Key = KeyPath,
			Label = "程序路径",
			Type = ParameterFieldType.File,
			Required = true,
			HelpText = "可执行文件或快捷方式的完整路径，也支持 shell:AppsFolder 形式的应用。",
		},
		new()
		{
			Key = KeyArguments,
			Label = "启动参数",
			Type = ParameterFieldType.Text,
			Placeholder = "可选，例如 --portable",
		},
		new()
		{
			Key = KeyRunAsStandardUser,
			Label = "以常规普通权限启动",
			Type = ParameterFieldType.Bool,
			DefaultValue = "false",
			HelpText = "当 StarPie 以管理员权限运行时，通过 Windows Shell 降权启动目标程序，恢复文件拖拽交互支持。",
		},
	};

	/// <summary>
	/// <b>比原行为更严，是刻意的</b>：原 <see cref="ActionExecutor.ExecuteLaunch"/>
	/// 遇到空路径直接 <c>return</c>，用户按下去什么也不会发生、也没有任何提示。
	/// </summary>
	public string? Validate(IReadOnlyDictionary<string, string> parameters)
	{
		string path = parameters != null && parameters.TryGetValue(KeyPath, out string? value)
			? value ?? ""
			: "";

		return string.IsNullOrWhiteSpace(path)
			? "未设置要启动的程序，请在动作设置里选择可执行文件。"
			: null;
	}

	/// <summary>列表副标题：显示程序文件名，比整条路径更易读。</summary>
	public string Preview(IReadOnlyDictionary<string, string> parameters)
	{
		string path = parameters != null && parameters.TryGetValue(KeyPath, out string? value)
			? (value ?? "").Trim().Trim('"')
			: "";

		if (path.Length == 0) return "";

		// 只截文件名：`C:\Program Files\Very\Long\Path\app.exe` 在列表里读起来毫无意义，
		// 而 `app.exe` 一眼就知道是哪个程序。取不到文件名（例如 shell:AppsFolder\...）就回显原串。
		int slash = path.LastIndexOfAny(new[] { '\\', '/' });
		string name = slash >= 0 && slash < path.Length - 1 ? path.Substring(slash + 1) : path;

		return name.Length <= 48 ? name : name.Substring(0, 47) + "…";
	}

	public Task<ActionResult> ExecuteAsync(PluginActionInput input, CancellationToken cancellationToken)
	{
		string path = input?.Parameter(KeyPath) ?? "";

		if (string.IsNullOrWhiteSpace(path))
		{
			return Task.FromResult(ActionResult.Fail("未设置要启动的程序。"));
		}

		string arguments = input?.Parameter(KeyArguments) ?? "";
		bool runAsStandardUser = input?.Bool(KeyRunAsStandardUser) ?? false;

		ActionExecutor.ExecuteLaunch(path, arguments, runAsStandardUser);
		return Task.FromResult(ActionResult.Empty);
	}

	public static BuiltinActionRegistration Create() => new()
	{
		Type = "Launch",
		Aliases = Array.Empty<string>(),
		FullId = BuiltinActionCatalog.ProviderId + ".launch",
		Contribution = new BuiltinActionLaunch(),
		ProjectParameters = action =>
		{
			Dictionary<string, string> map = BuiltinActionCatalog.SeedFromExtensionData(action);

			if (!map.ContainsKey(KeyPath)) map[KeyPath] = action.Parameter ?? "";
			if (!map.ContainsKey(KeyArguments)) map[KeyArguments] = action.Arguments ?? "";

			// 布尔投影成不变文化字面量：宿主与实现方都按 InvariantCulture 解析，
			// 否则同一份配置在不同区域设置下会解析成不同的值。
			if (!map.ContainsKey(KeyRunAsStandardUser))
			{
				map[KeyRunAsStandardUser] = action.RunAsStandardUser ? "true" : "false";
			}

			return map;
		},
	};
}

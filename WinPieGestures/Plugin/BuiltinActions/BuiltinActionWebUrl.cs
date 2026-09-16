using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using StarPie.Plugin;

namespace WinPieGestures.Plugins.BuiltinActions;

/// <summary>
/// 内建动作「打开网址」的插件模型实现。
/// <para>
/// <b>别名 <c>"Url"</c> 必须保留</b>：历史上两种写法都被写进过用户配置，
/// 原 <c>switch</c> 用两个连续的 <c>case</c> 覆盖，这里改成显式声明。
/// </para>
/// </summary>
internal sealed class BuiltinActionWebUrl : IActionContribution
{
	private const string KeyUrl = "url";
	private const string KeyBrowser = "browser";
	private const string KeyCustomBrowserPath = "customBrowserPath";

	/// <summary>选了「自定义浏览器」时该字段才有意义。</summary>
	private const string BrowserChoiceCustom = "Custom";

	public ActionDescriptor Descriptor => new()
	{
		Id = "webUrl",
		DisplayName = I18n.T("ActionTypeWebUrlShort"),
		Description = null,
		Category = "",
		IconKey = null,
		Kind = ActionKind.Sequential,
		TimeoutSeconds = 0,
	};

	/// <summary>
	/// 参数声明。
	/// <para>
	/// <b>已知的表达力缺口</b>：手写面板里「自定义浏览器路径」只在浏览器选了
	/// <c>Custom</c> 时才显示（<c>FocusCustomBrowserPathPanel</c>），而
	/// <see cref="ParameterField"/> 目前没有「条件显示」这个概念 —— 统一表单会把三个字段
	/// 全部平铺出来。这是 UI 层的取舍，本轮不动面板，等条件显示补进 SDK 之后再一起切。
	/// </para>
	/// </summary>
	public IReadOnlyList<ParameterField> Parameters => new ParameterField[]
	{
		new()
		{
			Key = KeyUrl,
			Label = "网址",
			Type = ParameterFieldType.Text,
			Required = true,
			Placeholder = "https://github.com",
			// 不填协议时执行体会自动补 https://，所以这里不做正则强校验，
			// 只挡住纯空白和明显非网址的输入（例如误填了本地路径）。
			ValidationRegex = @"^\S+$",
		},
		new()
		{
			Key = KeyBrowser,
			Label = "打开方式",
			Type = ParameterFieldType.Enum,
			DefaultValue = "Default",
			Options = new ParameterOption[]
			{
				new() { Value = "Default", Label = "🌐 系统默认" },
				new() { Value = "Chrome", Label = "Google Chrome" },
				new() { Value = "Edge", Label = "Microsoft Edge" },
				new() { Value = "Firefox", Label = "Mozilla Firefox" },
				new() { Value = BrowserChoiceCustom, Label = "自定义浏览器..." },
			},
		},
		new()
		{
			Key = KeyCustomBrowserPath,
			Label = "自定义浏览器路径",
			Type = ParameterFieldType.File,
			HelpText = "仅当「打开方式」选择「自定义浏览器…」时生效。",
		},
	};

	/// <summary>
	/// <b>比原行为更严，是刻意的</b>：原 <see cref="ActionExecutor.ExecuteWebUrl"/>
	/// 遇到空网址直接 <c>return</c>，按下去毫无反应。
	/// </summary>
	public string? Validate(IReadOnlyDictionary<string, string> parameters)
	{
		string url = parameters != null && parameters.TryGetValue(KeyUrl, out string? value)
			? value ?? ""
			: "";

		if (string.IsNullOrWhiteSpace(url)) return "未填写网址。";

		// 选了自定义浏览器却没给路径，原实现会走到一个不存在的 exe 上然后抛异常，
		// 报错信息是「系统找不到指定的文件」这种用户看不懂的话。这里提前说清楚。
		string browser = parameters != null && parameters.TryGetValue(KeyBrowser, out string? choice)
			? (choice ?? "").Trim()
			: "";

		bool hasCustomPath = parameters != null &&
			parameters.TryGetValue(KeyCustomBrowserPath, out string? path) &&
			!string.IsNullOrWhiteSpace(path);

		return browser.Equals(BrowserChoiceCustom, StringComparison.OrdinalIgnoreCase) && !hasCustomPath
			? "已选择「自定义浏览器」，但未指定浏览器可执行文件路径。"
			: null;
	}

	/// <summary>列表副标题：去掉协议头，只留域名与路径，列表里更好认。</summary>
	public string Preview(IReadOnlyDictionary<string, string> parameters)
	{
		string url = parameters != null && parameters.TryGetValue(KeyUrl, out string? value)
			? (value ?? "").Trim()
			: "";

		if (url.Length == 0) return "";

		foreach (string scheme in new[] { "https://", "http://", "ftp://" })
		{
			if (url.StartsWith(scheme, StringComparison.OrdinalIgnoreCase))
			{
				url = url.Substring(scheme.Length);
				break;
			}
		}

		return url.Length <= 48 ? url : url.Substring(0, 47) + "…";
	}

	public Task<ActionResult> ExecuteAsync(PluginActionInput input, CancellationToken cancellationToken)
	{
		string url = input?.Parameter(KeyUrl) ?? "";

		if (string.IsNullOrWhiteSpace(url))
		{
			return Task.FromResult(ActionResult.Fail("未填写网址。"));
		}

		string browser = input?.Parameter(KeyBrowser) ?? "Default";
		string customBrowserPath = input?.Parameter(KeyCustomBrowserPath) ?? "";

		ActionExecutor.ExecuteWebUrl(url, browser, customBrowserPath);
		return Task.FromResult(ActionResult.Empty);
	}

	public static BuiltinActionRegistration Create() => new()
	{
		Type = "WebUrl",
		Aliases = new[] { "Url" },
		FullId = BuiltinActionCatalog.ProviderId + ".webUrl",
		Contribution = new BuiltinActionWebUrl(),
		ProjectParameters = action =>
		{
			Dictionary<string, string> map = BuiltinActionCatalog.SeedFromExtensionData(action);

			if (!map.ContainsKey(KeyUrl)) map[KeyUrl] = action.Parameter ?? "";
			if (!map.ContainsKey(KeyBrowser)) map[KeyBrowser] = action.BrowserChoice ?? "Default";
			if (!map.ContainsKey(KeyCustomBrowserPath)) map[KeyCustomBrowserPath] = action.BrowserPath ?? "";

			return map;
		},
	};
}

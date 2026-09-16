using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using StarPie.Plugin;

namespace WinPieGestures.Plugins.BuiltinActions;

/// <summary>
/// 内建动作「移到下一个显示器」的插件模型实现。无参数 ——
/// 目标显示器由执行体枚举报当前窗口所在显示器再取下一个决定，没有可配置项。
/// </summary>
internal sealed class BuiltinActionMoveMonitor : IActionContribution
{
	public ActionDescriptor Descriptor => new()
	{
		Id = "moveMonitor",
		DisplayName = I18n.T("ActionTypeMoveMonitorShort"),
		Description = null,
		Category = "",
		IconKey = null,
		Kind = ActionKind.Sequential,
		TimeoutSeconds = 0,
	};

	public IReadOnlyList<ParameterField> Parameters => Array.Empty<ParameterField>();

	public string? Validate(IReadOnlyDictionary<string, string> parameters) => null;

	public string Preview(IReadOnlyDictionary<string, string> parameters) => "";

	public Task<ActionResult> ExecuteAsync(PluginActionInput input, CancellationToken cancellationToken)
	{
		WindowTiler.MoveWindowToNextMonitor();
		return Task.FromResult(ActionResult.Empty);
	}

	public static BuiltinActionRegistration Create() => new()
	{
		Type = "MoveMonitor",
		Aliases = Array.Empty<string>(),
		FullId = BuiltinActionCatalog.ProviderId + ".moveMonitor",
		Contribution = new BuiltinActionMoveMonitor(),
	};
}

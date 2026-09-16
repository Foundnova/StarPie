using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using StarPie.Plugin;

namespace WinPieGestures.Plugins.BuiltinActions;

/// <summary>
/// 内建动作「运行命令」的插件模型实现。
/// <para>
/// <b>这是内建动作向插件模型收敛的第一个样本</b>，选它打头阵是因为它的参数最少（两个）、
/// 且手写面板里没有「预设芯片 / 条件显示 / 辅助按钮」这类声明式表单表达不了的东西
/// （对照：启动程序面板有三枚辅助按钮、打开网址面板有下拉联动的路径框、打开文件夹面板有五枚芯片）。
/// 整条链路——参数声明 → 参数投影 → 校验 → 执行——都能在这一个动作上验完。
/// </para>
/// <para>
/// <b>执行体没有被搬走</b>：仍然调用 <see cref="ActionExecutor.ExecuteCommand"/>。
/// 本轮统一的是「动作的<b>形状</b>」（描述、参数、校验、预览、执行入口），不是代码位置。
/// 把 2500 行的执行器按动作拆成九个文件是一次纯搬运，收益有限、回归面极大，
/// 等形状稳定之后再谈。
/// </para>
/// </summary>
internal sealed class BuiltinActionCommand : IActionContribution
{
	// 参数键。这两个词会被写进 ActionItem.ExtensionData，将来 UI 层统一渲染时，
	// 表单也是按这两个键回填 —— 所以它们一旦发布就不能改（会静默丢掉用户填过的值）。
	private const string KeyCommandLine = "commandLine";
	private const string KeyTerminal = "terminal";

	/// <summary>
	/// 自描述信息。
	/// <para>
	/// 刻意用<b>属性</b>而不是字段缓存：<see cref="I18n"/> 的当前语言可以在运行时切换，
	/// 每次访问重新取词条，界面才会跟着语言走。这也是插件侧声明 i18n 短键时的同一效果。
	/// </para>
	/// </summary>
	public ActionDescriptor Descriptor => new()
	{
		Id = "command",
		DisplayName = I18n.T("ActionTypeCommandShort"),
		Description = I18n.T("ActionTypeCommandDesc"),

		// 分类留空：分类是「插件动作」下拉分组用的，内建动作在「动作类型」下拉里
		// 有自己既定的位置，不需要再进插件的分组体系。
		Category = "",

		// 图标留空：内建动作的图标走 ActionItem（用户可自选 App 图标 / 自定义 SVG），
		// 与插件通过 IconKey 声明矢量图标是两套并行的机制，此处不该另起一套。
		IconKey = null,

		// 【必须与现状一致】原 switch 分支是在动作线程上同步执行的，
		// 标成 Background 会把它挪到线程池 —— 用户可感知的时序就变了。
		Kind = ActionKind.Sequential,

		TimeoutSeconds = 0,
	};

	/// <summary>
	/// 参数声明。
	/// <para>
	/// 与原手写面板逐项对齐：<c>FocusCommandTextBox</c>（单行、必填）+ <c>FocusCommandTerminalComboBox</c>（六个终端）。
	/// 终端选项的文案直接取宿主词条 —— 与 <c>SlotViewModel.LocalizedTerminals</c> 同源，
	/// 不会出现「两处下拉里的终端名字不一样」这种分裂。
	/// </para>
	/// </summary>
	public IReadOnlyList<ParameterField> Parameters => new ParameterField[]
	{
		new()
		{
			Key = KeyCommandLine,
			Label = I18n.T("CommandFieldLine"),
			Type = ParameterFieldType.Text,
			Required = true,
			Placeholder = "ping -t 127.0.0.1",
		},
		new()
		{
			Key = KeyTerminal,
			Label = I18n.T("CommandFieldTerminal"),
			Type = ParameterFieldType.Enum,
			DefaultValue = "cmd",
			Options = new ParameterOption[]
			{
				new() { Value = "cmd", Label = I18n.T("TerminalCmd") },
				new() { Value = "powershell", Label = I18n.T("TerminalPowerShell") },
				new() { Value = "wsl", Label = I18n.T("TerminalWsl") },
				new() { Value = "cmd_hidden", Label = I18n.T("TerminalCmdHidden") },
				new() { Value = "powershell_hidden", Label = I18n.T("TerminalPowerShellHidden") },
				new() { Value = "wsl_hidden", Label = I18n.T("TerminalWslHidden") },
			},
		},
	};

	/// <summary>
	/// 校验参数。
	/// <para>
	/// <b>这里比原行为更严了，是刻意的</b>：原 <see cref="ActionExecutor.ExecuteCommand"/>
	/// 遇到空命令是直接 <c>return</c> —— 用户按下了扇区、什么也没发生、也没有任何提示，
	/// 正是那种「能编译、界面正常、行为却悄悄退化」的静默失效。
	/// 现在改成在执行前拦下并说明原因。
	/// </para>
	/// </summary>
	public string? Validate(IReadOnlyDictionary<string, string> parameters)
	{
		string command = parameters != null && parameters.TryGetValue(KeyCommandLine, out string? value)
			? value ?? ""
			: "";

		return string.IsNullOrWhiteSpace(command)
			? "命令行内容为空，请在动作设置里填写要执行的命令。"
			: null;
	}

	/// <summary>
	/// 列表副标题。
	/// <para>
	/// <b>必须极快</b>（微秒级）：它会在设置页滚动时被高频调用，不得有 IO 与网络。
	/// 这里只做字符串截断。
	/// </para>
	/// </summary>
	public string Preview(IReadOnlyDictionary<string, string> parameters)
	{
		string command = parameters != null && parameters.TryGetValue(KeyCommandLine, out string? value)
			? (value ?? "").Trim()
			: "";

		if (command.Length == 0) return "";

		// 换行符会让列表项高度跳变，先压平再截断。
		command = command.Replace('\r', ' ').Replace('\n', ' ');

		return command.Length <= 48 ? command : command.Substring(0, 47) + "…";
	}

	/// <summary>
	/// 执行。
	/// <para>
	/// <b>线程约束</b>：调用方（<see cref="ActionExecutor"/>）在唯一的动作线程上以
	/// 同步方式等待本方法，因此实现里<b>绝不能出现真正的异步等待</b> ——
	/// 一旦 <c>await</c> 到别的上下文，就会在动作线程上死锁。
	/// 这里全程同步完成，<see cref="Task.FromResult{TResult}"/> 只是为了让签名与插件一致。
	/// </para>
	/// <para>
	/// <b>异常不在这里吞</b>：执行体抛出的异常照常向上冒泡，由
	/// <see cref="ActionExecutor.Execute"/> 的 <c>catch</c> 处理（弹 MessageBox）。
	/// 这一点与插件动作相反 —— 插件是社区代码，失败必须可忽略（走日志 + 托盘气泡）；
	/// 内建动作是用户亲手配的，失败必须立刻让他知道。两者诉求相反，不能统一成一种。
	/// </para>
	/// </summary>
	public Task<ActionResult> ExecuteAsync(PluginActionInput input, CancellationToken cancellationToken)
	{
		string command = input?.Parameter(KeyCommandLine) ?? "";
		string terminal = input?.Parameter(KeyTerminal) ?? "cmd";

		if (string.IsNullOrWhiteSpace(command))
		{
			// 正常情况下走不到这里：宿主在调用前已用 Validate 拦过。
			// 但直接调用本实现（例如自检）时不会有那层保护，所以再兜一次。
			return Task.FromResult(ActionResult.Fail("命令行内容为空。"));
		}

		ActionExecutor.ExecuteCommand(command, terminal);
		return Task.FromResult(ActionResult.Empty);
	}

	/// <summary>
	/// 构造登记项。
	/// <para>
	/// <b>参数投影是这里唯一有技术含量的部分</b>：内建动作的参数目前仍存在
	/// <see cref="ActionItem"/> 的裸字段上，而 <c>ActionItem.Parameter</c> 在全项目有近两百处引用，
	/// 整体搬到 <c>ExtensionData</c> 是一次高风险大改动。于是在这里「现读现装」：
	/// <list type="number">
	/// <item>先铺 <see cref="ActionItem.ExtensionData"/>（统一表单将来写入的参数）；</item>
	/// <item>再用裸字段补上缺的键。</item>
	/// </list>
	/// 顺序很关键 —— <b>ExtensionData 优先</b>，于是将来参数真正迁移到新模型时，
	/// 这个函数一行都不用改就能读到新值；而在迁移之前，它读到的就是用户配置里的原值。
	/// </para>
	/// </summary>
	public static BuiltinActionRegistration Create() => new()
	{
		Type = "Command",
		Aliases = Array.Empty<string>(),
		FullId = BuiltinActionCatalog.ProviderId + ".command",
		Contribution = new BuiltinActionCommand(),
		ProjectParameters = action =>
		{
			Dictionary<string, string> map = BuiltinActionCatalog.SeedFromExtensionData(action);

			if (!map.ContainsKey(KeyCommandLine)) map[KeyCommandLine] = action.Parameter ?? "";
			if (!map.ContainsKey(KeyTerminal)) map[KeyTerminal] = action.CommandTerminal ?? "cmd";

			return map;
		},
	};
}

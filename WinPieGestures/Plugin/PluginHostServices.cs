using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading;
using System.Windows.Threading;
using StarPie.Plugin;

namespace WinPieGestures.Plugins;

/// <summary>托盘气泡的注入点。由宿主 UI 层设置，插件侧永远见不到具体实现。</summary>
internal static class PluginNotificationHub
{
    /// <summary>参数为 (标题, 内容)。为 null 时通知降级为写日志。</summary>
    public static Action<string, string>? Sink;
}

/// <summary>非侵入式通知。宿主没有可用托盘时<b>静默降级为日志</b>，绝不弹 MessageBox（会阻塞动作线程）。</summary>
internal sealed class PluginNotificationService : INotificationService
{
    private readonly string _pluginId;

    public PluginNotificationService(string pluginId) => _pluginId = pluginId;

    public void Notify(string title, string message)
    {
        try
        {
            Action<string, string>? sink = PluginNotificationHub.Sink;
            if (sink != null)
            {
                sink(title ?? "", message ?? "");
                return;
            }
        }
        catch (Exception ex)
        {
            AppLogger.LogWarn($"[plugin:{_pluginId}] 通知发送失败，已降级为日志：{ex.Message}");
        }

        AppLogger.LogInfo($"[plugin:{_pluginId}] 通知（无托盘可显示）：{title} - {message}");
    }
}

/// <summary>宿主环境信息实现。</summary>
internal sealed class PluginHostInfo : IHostInfo
{
    private readonly PluginCapability _capabilities;

    public PluginHostInfo(PluginCapability capabilities) => _capabilities = capabilities;

    public string HostVersion => PluginManifestReader.HostVersion;

    public string ApiVersion => PluginApi.ApiVersion;

    public string LanguageCode => I18n.CurrentLanguageCode;

    public bool IsElevated
    {
        get
        {
            try { return ConfigManager.IsElevated(); }
            catch { return false; }
        }
    }

    public bool IsPortable => PluginPaths.IsPortable;

    public string HostExecutablePath
    {
        get
        {
            try
            {
                // 单文件发布形态下 Assembly.Location 为空，此时回退到进程主模块路径
                string? location = typeof(PluginHostInfo).Assembly.Location;
                return string.IsNullOrEmpty(location) ? Environment.ProcessPath ?? "" : location;
            }
            catch
            {
                return "";
            }
        }
    }

    /// <summary>
    /// 读的是<b>本插件自己的清单</b>，不是宿主的全局开关。所以插件可以在
    /// <c>Initialize</c> 里问一句「我有没有 Process 能力」，据此决定注册一个能用的动作、
    /// 还是注册一个点了就告诉用户「本插件需要「进程」能力」的动作 ——
    /// 后者比让那次动作在运行时抛异常友好得多。
    /// </summary>
    public bool HasCapability(PluginCapability capability) => (_capabilities & capability) == capability;
}

/// <summary>UI 线程调度。UI 不可用时降级为「直接执行」或「静默忽略」，绝不抛异常。</summary>
internal sealed class PluginDispatcherFacade : IDispatcherFacade
{
    private static Dispatcher? UiDispatcher
    {
        get
        {
            try
            {
                var app = System.Windows.Application.Current;
                return app?.Dispatcher;
            }
            catch
            {
                return null;
            }
        }
    }

    public bool IsOnUiThread
    {
        get
        {
            Dispatcher? dispatcher = UiDispatcher;
            return dispatcher == null || dispatcher.CheckAccess();
        }
    }

    public void Post(Action action)
    {
        if (action == null) return;

        Dispatcher? dispatcher = UiDispatcher;
        if (dispatcher == null)
        {
            // 宿主尚未完成启动：直接同步执行，保证插件在早期也能收到事件
            TryRun(action);
            return;
        }

        try
        {
            if (dispatcher.CheckAccess()) TryRun(action);
            else dispatcher.BeginInvoke(DispatcherPriority.Background, new Action(() => TryRun(action)));
        }
        catch
        {
        }
    }

    public Task InvokeAsync(Action action)
    {
        if (action == null) return Task.CompletedTask;

        Dispatcher? dispatcher = UiDispatcher;
        if (dispatcher == null) return Task.Run(() => TryRun(action));

        try
        {
            return dispatcher.InvokeAsync(() => TryRun(action), DispatcherPriority.Normal).Task;
        }
        catch
        {
            return Task.CompletedTask;
        }
    }

    private static void TryRun(Action action)
    {
        try
        {
            action();
        }
        catch (Exception ex)
        {
            // 插件回调的异常绝不能污染 WPF 的调度循环
            AppLogger.LogError("[plugin] 插件 UI 回调抛出异常", ex);
        }
    }
}

/// <summary>
/// 宿主已验证的动作能力实现。
/// <para>
/// 这里<b>刻意不重新实现</b>任何输入模拟逻辑，而是直接复用 <see cref="ActionExecutor"/> 里
/// 已经踩过坑的那几条路径：硬件扫描码映射、修饰键 10~15ms 时延保持、扩展键标志、
/// Unicode 字符流注入、Shell 令牌降权。插件自己写一遍不仅会踩同样的坑，
/// 还可能因为与主程序的全局钩子互相干扰而进入死循环。
/// </para>
/// </summary>
internal sealed class PluginHostActionInvoker : IHostActionInvoker
{
    private readonly string _pluginId;

    public PluginHostActionInvoker(string pluginId) => _pluginId = pluginId;

    public bool SendHotkey(string hotkey)
    {
        if (string.IsNullOrWhiteSpace(hotkey)) return false;
        return Guard(nameof(SendHotkey), () => ActionExecutor.ExecuteHotkey(hotkey));
    }

    public bool SendText(string text)
    {
        if (string.IsNullOrEmpty(text)) return false;
        return Guard(nameof(SendText), () => ActionExecutor.SendTextInput(text));
    }

    public bool Launch(string path, string arguments = "", bool runAsStandardUser = false)
    {
        if (string.IsNullOrWhiteSpace(path)) return false;
        return Guard(nameof(Launch), () => ActionExecutor.ExecuteLaunch(path, arguments ?? "", runAsStandardUser));
    }

    public bool OpenFolder(string folderPath)
    {
        if (string.IsNullOrWhiteSpace(folderPath)) return false;
        return Guard(nameof(OpenFolder), () => ActionExecutor.ExecuteFolder(folderPath));
    }

    public bool OpenUrl(string url, string browserChoice = "Default", string? customBrowserPath = null)
    {
        if (string.IsNullOrWhiteSpace(url)) return false;
        return Guard(nameof(OpenUrl), () => ActionExecutor.ExecuteWebUrl(url, browserChoice, customBrowserPath));
    }

    public bool SetClipboardText(string text)
    {
        if (text == null) return false;
        return Guard(nameof(SetClipboardText), () => ActionExecutor.SafeSetClipboardText(text));
    }

    public string? GetClipboardText()
    {
        string? result = null;
        try
        {
            // 动作线程是 MTA，WPF 剪贴板 API 要求 STA —— 与主程序既有做法一致，起临时 STA 线程取
            var worker = new Thread(() =>
            {
                try
                {
                    if (System.Windows.Clipboard.ContainsText())
                    {
                        result = System.Windows.Clipboard.GetText();
                    }
                }
                catch
                {
                }
            })
            {
                IsBackground = true,
                Name = "StarPie.PluginClipboardRead",
            };
            worker.SetApartmentState(ApartmentState.STA);
            worker.Start();
            worker.Join(500);
        }
        catch (Exception ex)
        {
            AppLogger.LogWarn($"[plugin:{_pluginId}] 读取剪贴板失败：{ex.Message}");
        }
        return result;
    }

    /// <summary>
    /// 统一包裹：插件通过宿主服务触发的任何异常都不允许冒泡到 <see cref="ActionExecutor.Execute"/>，
    /// 否则会命中它内部的 MessageBox 分支，在无人值守时卡住动作线程。
    /// </summary>
    private bool Guard(string operation, Action action)
    {
        try
        {
            action();
            return true;
        }
        catch (Exception ex)
        {
            AppLogger.LogError($"[plugin:{_pluginId}] 宿主动作服务 {operation} 执行失败", ex);
            return false;
        }
    }
}

/// <summary>
/// 带能力门禁的宿主服务骨架：能力校验 + 异常包裹，两条纪律的唯一实现。
/// <para>
/// 三个服务（命令 / Shell 动词 / 窗口控制）的这两件事逐字相同，差别只有所需的能力标志。
/// 抽出来不只是为了少写几行 —— 它们是<b>纪律的载体</b>，各写一份的话，
/// 某天只修了其中两份就会得到一个行为自相矛盾的 SDK：
/// <list type="bullet">
/// <item><b>门禁必须先落日志再抛</b>。异常可能被插件自己的 <c>catch</c> 吞掉，
/// 而日志是排查的第一现场 —— 否则现象只剩「按下去什么都没发生」。</item>
/// <item><b>异常必须吞在服务内</b>。冒泡到 <c>ActionExecutor.Execute</c> 会命中它的
/// MessageBox 分支，在无人值守时卡死唯一的动作线程。</item>
/// </list>
/// </para>
/// </summary>
internal abstract class PluginGatedService
{
    private readonly string _pluginId;
    private readonly PluginCapability _capabilities;
    private readonly PluginCapability _required;
    private readonly string _serviceName;

    protected PluginGatedService(
        string pluginId,
        PluginCapability capabilities,
        PluginCapability required,
        string serviceName)
    {
        _pluginId = pluginId;
        _capabilities = capabilities;
        _required = required;
        _serviceName = serviceName;
    }

    /// <summary>
    /// 校验清单能力。<b>判据是「包含」而不是「非零」</b>：一个服务将来若要两样能力，
    /// 写成 <c>!= 0</c> 会变成「有其中任意一样就放行」。
    /// </summary>
    protected void RequireCapability()
    {
        if ((_capabilities & _required) == _required) return;

        AppLogger.LogError(
            $"[plugin:{_pluginId}] 调用了 {_serviceName}，但清单未声明 {_required} 能力，调用被拒绝。");

        throw new PluginCapabilityDeniedException(_required, _serviceName, _pluginId);
    }

    /// <summary>统一包裹：插件经宿主服务触发的任何异常都不允许冒泡出去。</summary>
    protected bool Guard(string operation, Func<bool> action)
    {
        try
        {
            return action();
        }
        catch (Exception ex)
        {
            AppLogger.LogError($"[plugin:{_pluginId}] {_serviceName}.{operation} 执行失败", ex);
            return false;
        }
    }
}

/// <summary>
/// 命令执行服务实现。
/// <para>
/// 它是 SDK 里唯一「参数即任意命令」的攻击面，所以是本项目<b>唯一带真实门禁</b>的服务：
/// 插件清单没声明 <see cref="PluginCapability.Process"/> 时，<see cref="Run"/> 直接抛
/// <see cref="PluginCapabilityDeniedException"/>，绝不静默降级。
/// </para>
/// <para>
/// 元数据（<see cref="Terminals"/>）刻意<b>不</b>受门禁约束：插件的 <c>Parameters</c> 属性
/// 声明期就要读它，若在这里抛异常，一个「忘了声明能力」的插件会在注册阶段就崩掉，
/// 而它其实只是不能在运行时干活而已。门禁拦的是<b>产生后果</b>的调用。
/// </para>
/// </summary>
internal sealed class PluginCommandService : PluginGatedService, IHostCommandService
{
    public PluginCommandService(string pluginId, PluginCapability capabilities)
        : base(pluginId, capabilities, PluginCapability.Process, nameof(IHostCommandService))
    {
    }

    /// <summary>
    /// 终端标识与其词条键。<b>顺序即宿主动作编辑器里的下拉顺序</b>（可见项在前、隐藏变体在后）。
    /// <para>
    /// 这是终端清单的<b>唯一来源</b>：外移后的「运行命令」动作（<c>CommandAction</c>）直接
    /// 从 <see cref="Terminals"/> 取这份清单，不在插件里另抄一份 —— 所以不存在
    /// 「宿主改了下拉、插件没跟上」这种漂移，切换语言时也是两边同时变。
    /// </para>
    /// </summary>
    private static readonly (string Id, string TextKey)[] TerminalCatalog =
    {
        ("cmd", "TerminalCmd"),
        ("powershell", "TerminalPowerShell"),
        ("wsl", "TerminalWsl"),
        ("cmd_hidden", "TerminalCmdHidden"),
        ("powershell_hidden", "TerminalPowerShellHidden"),
        ("wsl_hidden", "TerminalWslHidden"),
    };

    public IReadOnlyList<CommandTerminalOption> Terminals
    {
        get
        {
            // 每次访问都重新取词条：I18n 的当前语言可以在运行时切换，缓存住的话
            // 用户切到英文之后下拉里还是中文。这里只有 6 项，重算的代价可以忽略。
            var list = new List<CommandTerminalOption>(TerminalCatalog.Length);
            foreach ((string id, string textKey) in TerminalCatalog)
            {
                list.Add(new CommandTerminalOption { Id = id, DisplayName = I18n.T(textKey) });
            }
            return list;
        }
    }

    public bool Run(string command, string terminal = "cmd")
    {
        RequireCapability();
        if (string.IsNullOrWhiteSpace(command)) return false;

        return Guard(nameof(Run), () => ActionExecutor.ExecuteCommand(command, terminal ?? "cmd"));
    }
}

/// <summary>
/// Shell 上下文动词服务实现。
/// <para>
/// 门禁与 <see cref="PluginCommandService"/> 相同（<see cref="PluginCapability.Process"/>）——
/// 这批动词里既有以 UAC 提权启动进程的 <c>Windows.RunAs</c>，也有清空回收站这类不可撤销的操作，
/// 用同一个判据拦住是合适的粗粒度做法。
/// </para>
/// </summary>
internal sealed class PluginShellService : PluginGatedService, IHostShellService
{
    public PluginShellService(string pluginId, PluginCapability capabilities)
        : base(pluginId, capabilities, PluginCapability.Process, nameof(IHostShellService))
    {
    }

    /// <summary>
    /// 把宿主挑选器的清单投影成 SDK 选项。
    /// <para>
    /// <b>取 <c>Id</c> 而不是 <c>Verb</c></b>：用户配置里存进 <c>Parameter</c> 的是短 ID
    /// （<c>copy_path</c>），而 <c>Verb</c>（<c>Windows.CopyAsPath</c>）是执行体 switch 里的规范名。
    /// 传错这一个字段，动作会静默无效 —— 因为 switch 的 default 分支是空的。
    /// </para>
    /// <para>
    /// <c>Title</c> 目前是清单里硬编码的中文（宿主挑选器自己也是这么显示的），
    /// 所以 <see cref="ShellVerbOption.DisplayName"/> 在英文环境下同样是中文。
    /// 这是<b>宿主挑选器既有的缺陷</b>，不在本轮范围内；这里刻意与它保持一致 ——
    /// SDK 与宿主界面显示同一功能的两个名字，是比中文更糟的问题。
    /// </para>
    /// </summary>
    public IReadOnlyList<ShellVerbOption> Verbs
    {
        get
        {
            var list = new List<ShellVerbOption>();
            foreach (ShellToolItem item in ShellActionPickerWindow.ShellTools)
            {
                if (string.IsNullOrWhiteSpace(item.Id)) continue;
                list.Add(new ShellVerbOption { Id = item.Id.Trim(), DisplayName = item.Title ?? "" });
            }
            return list;
        }
    }

    public bool Invoke(string verb)
    {
        RequireCapability();
        if (string.IsNullOrWhiteSpace(verb)) return false;

        // 返回 true 的语义刻意保守：只表示「宿主接受了这次调用」。
        // ExecuteShellTool 是 33 个分支的 switch，其中不少分支在上下文不适用时静默 return
        // （例如 Windows.RunAs 时没选中可执行文件），它本身不产生失败信号 ——
        // 与其在这里编一个不可靠的成功/失败判断，不如把语义如实写窄。
        return Guard(nameof(Invoke), () => { ActionExecutor.ExecuteShellTool(verb); return true; });
    }
}

/// <summary>
/// 窗口控制服务实现。
/// <para>
/// 门禁是 <see cref="PluginCapability.WindowControl"/> 而不是复用的 <c>Process</c>：
/// 这批方法的后果是「用户正在用的窗口被挪走 / 被改透明 / 被切走」，
/// 与「启动一个进程」是两类事。共用一个标签会让安装确认页对用户说一句不准确的话。
/// </para>
/// <para>
/// <b>实现一律转发，不在这里重写一行</b>。执行体里有大量已经踩平的坑：
/// DWM 失焦窗口过滤（<c>DWMWA_CLOAKED</c>、无标题与工具窗口排除）、
/// 多显示器工作区枚举、任务栏槽位的 UIA 遍历与前台激活的线程约束。
/// 插件自己写一遍不仅会踩同样的坑，还可能因为与主程序的全局钩子互相干扰而死循环。
/// </para>
/// </summary>
internal sealed class PluginWindowService : PluginGatedService, IHostWindowService
{
    public PluginWindowService(string pluginId, PluginCapability capabilities)
        : base(pluginId, capabilities, PluginCapability.WindowControl, nameof(IHostWindowService))
    {
    }

    /// <summary>
    /// 布局清单<b>取自宿主执行体的唯一一份表</b>（<see cref="WindowTiler.LayoutKeys"/> +
    /// <see cref="WindowTiler.LayoutDisplayName"/>），不在这里另抄。
    /// <para>
    /// 每次访问都重新取显示名：界面语言可在运行时切换，缓存住的话切到英文之后下拉还是中文。
    /// 17 项的重算代价可以忽略 —— 它只在声明期与渲染期被读。
    /// </para>
    /// </summary>
    public IReadOnlyList<WindowLayoutOption> Layouts
    {
        get
        {
            var list = new List<WindowLayoutOption>(WindowTiler.LayoutKeys.Count);
            foreach (string key in WindowTiler.LayoutKeys)
            {
                list.Add(new WindowLayoutOption
                {
                    Key = key,
                    DisplayName = WindowTiler.LayoutDisplayName(key),
                });
            }
            return list;
        }
    }

    public string CycleToken => WindowTiler.CycleParam;

    public string CycleBackToken => WindowTiler.CycleBackParam;

    public string RestoreToken => WindowTiler.RestoreParam;

    public double OpacityMinPercent => WindowTiler.MinOpacityPercent;

    public double OpacityMaxPercent => WindowTiler.MaxOpacityPercent;

    public bool ApplyLayout(string layoutKey)
    {
        RequireCapability();
        if (string.IsNullOrWhiteSpace(layoutKey)) return false;

        // ExecuteTile 内部已把三个标记与具体布局码分派到各自实现，
        // 这里不再分一遍 —— 分两处迟早会漏掉一个。
        return Guard(nameof(ApplyLayout), () => { WindowTiler.ExecuteTile(layoutKey); return true; });
    }

    public bool ToggleTopmost()
    {
        RequireCapability();

        // 传空串 = 切换当前置顶状态。执行体还支持 "1"/"on" 那种「强制置顶」取值，
        // 但界面上从来只产生「切换」这一种，SDK 也就不为它发明一个参数。
        return Guard(nameof(ToggleTopmost), () => { WindowTiler.ToggleWindowTopmost(""); return true; });
    }

    public bool MoveToNextMonitor()
    {
        RequireCapability();

        return Guard(nameof(MoveToNextMonitor), () => { WindowTiler.MoveWindowToNextMonitor(); return true; });
    }

    public bool SetOpacity(string percent)
    {
        RequireCapability();
        if (string.IsNullOrWhiteSpace(percent)) return false;

        // 解析与 1~100 钳制全在执行体里，这里不重复实现 —— 参数保持字符串就是为了
        // 让插件原样透传，避免同一个规则在两处解析（迟早不一致）。
        return Guard(nameof(SetOpacity), () => { WindowTiler.SetWindowOpacity(percent); return true; });
    }

    public bool ActivateTaskbarSlot(int slotIndex)
    {
        RequireCapability();
        if (slotIndex < 1) return false;

        // 执行体内部是 Task.Run + UIA 遍历，本身就异步且不产生失败信号，
        // 所以返回 true 的语义是「宿主已受理」而不是「窗口已切换」——
        // 与 PluginShellService.Invoke 同一条纪律，把语义如实写窄。
        string raw = slotIndex.ToString(CultureInfo.InvariantCulture);
        return Guard(nameof(ActivateTaskbarSlot), () => { ActionExecutor.ExecuteSwitchWindow(raw); return true; });
    }
}

/// <summary>
/// 屏幕截取服务实现。
/// <para>
/// 门禁是 <see cref="PluginCapability.ScreenCapture"/>：截屏是隐私敏感的能力，
/// 安装确认页上必须让用户看见「它会看到我的屏幕」这件事，而不是藏在一句「界面」里。
/// </para>
/// <para>
/// <b>为什么返回 <c>void</c></b>：框选要等用户操作、识别更是异步的，这个调用根本无法同步
/// 取得结论。返回 <c>bool</c> 只能表示「宿主已受理」，而它是一个很容易被误读成
/// 「识别成功了吗」的假信号 —— 所以这里宁可什么都不返回。
/// </para>
/// <para>
/// 它是<b>唯一</b>一个不返回布尔的宿主动作服务：其余十几个（<c>Run</c> / <c>Invoke</c> /
/// <c>ApplyLayout</c> / <c>SetOpacity</c> …）返回的都是「这件事做成了没有」，
/// 那句话对命令、对窗口成立，对「发起一次框选截屏」不成立。
/// 自检那边为此单独加了一个 void 专用的门禁探针（<c>ProbeVoidCapabilityGate</c>），
/// 而不是把返回值硬凑成 <c>false</c> 塞进原来那个 —— 那样打印出来的
/// 「调用被直接放行（返回 False）」会把一个捏造的布尔值混进结论里。
/// </para>
/// </summary>
internal sealed class PluginScreenCaptureService : PluginGatedService, IHostScreenCaptureService
{
    public PluginScreenCaptureService(string pluginId, PluginCapability capabilities)
        : base(pluginId, capabilities, PluginCapability.ScreenCapture, nameof(IHostScreenCaptureService))
    {
    }

    public void CaptureAndRecognize()
    {
        RequireCapability();

        // 执行体内部是 Task.Run 起来的异步流程（开头还有一个 35ms 的等待，
        // 目的是别把尚未淡出干净的轮盘截进全屏图里），所以这里只负责「发起」。
        Guard(nameof(CaptureAndRecognize), () => { OcrManager.StartCaptureAndRecognize(); return true; });
    }
}

/// <summary>
/// 宿主事件订阅实现。
/// <para>
/// <b>所有订阅都必须返回可释放 token</b>，因为订阅链是「宿主静态事件 → 插件实例」，
/// 插件只要不摘掉这条链，它的 ALC 就永远无法被回收，表现为「停用后 DLL 仍被占用、改不动文件」。
/// </para>
/// </summary>
internal sealed class PluginEventService : IPluginEvents
{
    private readonly string _pluginId;
    private readonly object _gate = new();
    private readonly List<Subscription> _subscriptions = new();

    public PluginEventService(string pluginId) => _pluginId = pluginId;

    private sealed class Subscription : IDisposable
    {
        private Action? _unsubscribe;
        private bool _disposed;

        public Subscription(Action unsubscribe) => _unsubscribe = unsubscribe;

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            Action? action = _unsubscribe;
            _unsubscribe = null;
            try { action?.Invoke(); } catch { }
        }
    }

    /// <summary>登记一条订阅，并在宿主侧留档以便停用时兜底撤销。</summary>
    private IDisposable Track(Action unsubscribe)
    {
        var subscription = new Subscription(unsubscribe);
        lock (_gate)
        {
            _subscriptions.Add(subscription);
        }
        return subscription;
    }

    public IDisposable OnLanguageChanged(Action<string> handler)
    {
        if (handler == null) return new Subscription(() => { });

        Action onChanged = () =>
        {
            try { handler(I18n.CurrentLanguageCode); }
            catch (Exception ex) { AppLogger.LogError($"[plugin:{_pluginId}] OnLanguageChanged 回调异常", ex); }
        };

        I18n.LanguageChanged += onChanged;
        return Track(() => I18n.LanguageChanged -= onChanged);
    }

    public IDisposable OnWheelOpening(Action<ActionContext> handler)
    {
        if (handler == null) return new Subscription(() => { });
        IDisposable token = PluginHost.RegisterWheelOpening(_pluginId, handler);
        return Track(token.Dispose);
    }

    public IDisposable OnWheelClosed(Action handler)
    {
        if (handler == null) return new Subscription(() => { });
        IDisposable token = PluginHost.RegisterWheelClosed(_pluginId, handler);
        return Track(token.Dispose);
    }

    /// <summary>宿主兜底撤销：即使插件忘记释放 token，也要把订阅链彻底剪断。</summary>
    public void RevokeAll()
    {
        List<Subscription> snapshot;
        lock (_gate)
        {
            snapshot = new List<Subscription>(_subscriptions);
            _subscriptions.Clear();
        }

        foreach (Subscription subscription in snapshot)
        {
            subscription.Dispose();
        }
    }
}

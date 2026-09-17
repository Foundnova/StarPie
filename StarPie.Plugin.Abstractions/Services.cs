namespace StarPie.Plugin;

/// <summary>
/// 契约违约异常。由宿主在插件调用注册 API 不合法时抛出。
/// <para>
/// 抛出后宿主会把这个插件整体标记为加载失败并卸载 —— 不做「部分注册」，
/// 避免留下一个状态半残、用户无法解释的插件。
/// </para>
/// </summary>
public sealed class PluginContractException : Exception
{
    public PluginContractException(string message) : base(message) { }
    public PluginContractException(string message, Exception inner) : base(message, inner) { }
}

/// <summary>
/// 插件调用了<b>自己在清单里没有声明</b>的宿主能力。
/// <para>
/// <b>刻意不继承 <see cref="PluginContractException"/></b>，这是本类型存在的全部理由。
/// 后者的语义是「插件违反了注册契约」，宿主会因此把插件<b>整体标记为加载失败并卸载</b>。
/// 而「清单里漏了一行能力声明」远不到那个程度 —— 若继承它，用户看到的现象会是
/// 「插件突然坏了 / 被系统禁用了」，而真实原因是清单少写了一个词，
/// 排查方向会完全跑偏。
/// </para>
/// <para>
/// 所以这里是一个普通的、可被捕获的异常：只有那一次动作执行失败，
/// 日志与异常消息里带着可操作的修复指引，宿主与插件都照常活着。
/// </para>
/// </summary>
public sealed class PluginCapabilityDeniedException : Exception
{
    public PluginCapabilityDeniedException(PluginCapability capability, string serviceName, string pluginId)
        : base($"插件 \"{pluginId}\" 调用了 {serviceName}，但它的清单未声明 \"{capability}\" 能力。"
               + $"请在 plugin.json 的 capabilities 数组里加入 \"{capability}\" 后重新安装。")
    {
        Capability = capability;
        ServiceName = serviceName;
        PluginId = pluginId;
    }

    /// <summary>缺少的能力。</summary>
    public PluginCapability Capability { get; }

    /// <summary>被调用的服务接口名。</summary>
    public string ServiceName { get; }

    /// <summary>发起调用的插件 ID。</summary>
    public string PluginId { get; }
}

/// <summary>插件专属日志。宿主会自动加上 <c>[plugin:&lt;id&gt;]</c> 前缀并落盘到独立文件，同时做限流。</summary>
public interface IPluginLogger
{
    void Debug(string message);
    void Info(string message);
    void Warn(string message);
    void Error(string message, Exception? exception = null);

    /// <summary>该插件日志文件路径（便于在加载失败时提示用户去查看）。</summary>
    string LogFilePath { get; }
}

/// <summary>
/// 插件私有配置。宿主读写 <c>plugins\&lt;id&gt;\settings.json</c>，与主 <c>config.json</c> 完全隔离。
/// <para>键值一律用字符串，避免插件自定义类型进入持久化层。修改后需调用 <see cref="Save"/>。</para>
/// </summary>
public interface IPluginSettings
{
    string? Get(string key);
    void Set(string key, string? value);

    bool GetBool(string key, bool defaultValue = false);
    int GetInt(string key, int defaultValue = 0);
    double GetDouble(string key, double defaultValue = 0);

    /// <summary>把所有未保存的修改落盘（原子写）。</summary>
    void Save();
}

/// <summary>非侵入式通知服务。宿主优先走托盘气泡，没有托盘时降级为日志。</summary>
public interface INotificationService
{
    /// <summary>提示一条信息。<paramref name="message"/> 建议不超过 80 字。</summary>
    void Notify(string title, string message);
}

/// <summary>宿主环境信息（只读）。</summary>
public interface IHostInfo
{
    /// <summary>StarPie 主程序版本，例如 <c>1.7.4</c>。</summary>
    string HostVersion { get; }

    /// <summary>SDK 契约版本，例如 <c>1.0</c>。</summary>
    string ApiVersion { get; }

    /// <summary>当前界面语言代码，例如 <c>zh-CN</c> / <c>en</c>。</summary>
    string LanguageCode { get; }

    /// <summary>StarPie 是否以管理员权限运行。</summary>
    bool IsElevated { get; }

    /// <summary>是否为便携模式（插件根目录位于程序目录下）。</summary>
    bool IsPortable { get; }

    /// <summary>主程序可执行文件路径。单文件发布形态下可能为空。</summary>
    string HostExecutablePath { get; }

    /// <summary>
    /// 本插件是否声明了某项能力（读的是自己清单里的 <c>capabilities</c>）。
    /// <para>
    /// 需要该能力才能工作的插件<b>应当先问这里再动手</b>，而不是等宿主抛
    /// <see cref="PluginCapabilityDeniedException"/> —— 前者能给用户一句
    /// 「本插件需要「进程」能力」的说明，后者只能让那次动作静默失败。
    /// </para>
    /// </summary>
    bool HasCapability(PluginCapability capability);
}

/// <summary>
/// 宿主事件订阅。<b>所有订阅都必须持有返回的 token 并在 <c>Shutdown</c> 中释放</b> ——
/// 这是可回收 ALC 能否真正卸载的决定性因素。
/// </summary>
public interface IPluginEvents
{
    /// <summary>界面语言切换。回调在 UI 线程触发，参数为新语言代码。</summary>
    IDisposable OnLanguageChanged(Action<string> handler);

    /// <summary>
    /// 轮盘即将呈现。回调<b>保证不在钩子线程</b>（宿主已切到 UI 线程），
    /// 但仍在呼出路径上，因此必须极快，禁止 IO。
    /// </summary>
    IDisposable OnWheelOpening(Action<ActionContext> handler);

    /// <summary>轮盘关闭后。回调在 UI 线程触发。</summary>
    IDisposable OnWheelClosed(Action handler);
}

/// <summary>UI 线程调度门面。插件若持有后台线程并需要触碰 UI，必须经此切回。</summary>
public interface IDispatcherFacade
{
    bool IsOnUiThread { get; }

    /// <summary>投递到 UI 线程（不等结果）。UI 线程不可用时静默忽略。</summary>
    void Post(Action action);

    /// <summary>在 UI 线程执行并等待完成。</summary>
    Task InvokeAsync(Action action);
}

/// <summary>
/// 宿主已验证的动作能力。插件做「发快捷键 / 启程序 / 开文件夹 / 操作剪贴板 / 开网址」时
/// <b>必须</b>走这里，禁止自己 P/Invoke <c>SendInput</c> 或 <c>Process.Start</c>。
/// <para>
/// 原因：主程序内部已解决硬件扫描码映射、修饰键 10~15ms 时延保持、扩展键标志、
/// Unicode 字符流注入、提权降权令牌等一堆坑。插件自己重写一遍不仅会踩坑，
/// 还可能因为与主程序的全局钩子互相干扰而形成死循环。
/// </para>
/// <para>
/// <b>线程约束</b>：只有 <see cref="ActionKind.Sequential"/> 类动作可以调用这些方法；
/// 后台类动作调用会造成输入序列与前台动作交叉，宿主不为此负责。
/// </para>
/// </summary>
public interface IHostActionInvoker
{
    /// <summary>发送组合键，写法如 <c>"Ctrl+Shift+G"</c>、<c>"Win+D"</c>、<c>"F5"</c>。返回是否成功下发。</summary>
    bool SendHotkey(string hotkey);

    /// <summary>以 Unicode 字符流逐字输入文本，规避输入法阻断。</summary>
    bool SendText(string text);

    /// <summary>启动程序或打开文档。<paramref name="runAsStandardUser"/> 为 true 时通过 Shell 令牌降权启动。</summary>
    bool Launch(string path, string arguments = "", bool runAsStandardUser = false);

    /// <summary>在资源管理器中打开文件夹（不存在则尝试创建）。</summary>
    bool OpenFolder(string folderPath);

    /// <summary>用指定浏览器打开网址。<paramref name="browserChoice"/> 取值 Default/Chrome/Edge/Firefox/Custom。</summary>
    bool OpenUrl(string url, string browserChoice = "Default", string? customBrowserPath = null);

    /// <summary>写入剪贴板（带回退重试）。</summary>
    bool SetClipboardText(string text);

    /// <summary>读取剪贴板文本；无文本内容时返回 null。</summary>
    string? GetClipboardText();
}

/// <summary>一个终端选项。</summary>
public sealed class CommandTerminalOption
{
    /// <summary>标识，如 <c>cmd</c> / <c>powershell_hidden</c>。写进配置的就是这个值。</summary>
    public string Id { get; init; } = "";

    /// <summary>显示名（宿主已按当前语言本地化）。</summary>
    public string DisplayName { get; init; } = "";
}

/// <summary>
/// 宿主已验证的命令执行能力：在指定终端里跑一段命令。
/// <para>
/// <b>这是 SDK 里唯一「参数即任意命令」的攻击面</b>，所以它比
/// <see cref="IHostActionInvoker"/> 多一道门 —— 清单必须声明
/// <see cref="PluginCapability.Process"/>，否则调用抛
/// <see cref="PluginCapabilityDeniedException"/>。
/// </para>
/// <para>
/// <b>但这不构成沙箱，也请不要把它当成沙箱</b>：进程内插件本来就是普通 .NET 代码，
/// 绕开本接口直接 <c>Process.Start</c> 任何时候都做得到，SDK 拦不住。
/// 这道门换到的是另一件事 —— <b>让安装确认页上展示的能力真的对应一个后果</b>。
/// 用户同意安装时看到的「进程」标签，从此不是一句不产生任何后果的话。
/// </para>
/// <para>
/// <b>为什么只有新服务有门禁</b>：<see cref="IHostActionInvoker"/> 的七个方法是既有契约，
/// 给它们补门禁会让已发布、且没声明该能力的插件突然失败 —— 那是破坏性变更。
/// 门禁只能加在新引入的接口上。
/// </para>
/// <para>
/// <b>线程约束</b>：应只在 <see cref="ActionKind.Sequential"/> 类动作里调用。
/// </para>
/// </summary>
public interface IHostCommandService
{
    /// <summary>
    /// 可用的终端选项，<b>顺序即宿主动作编辑器里的下拉顺序</b> —— 插件直接照用即可与宿主界面一致。
    /// <para>
    /// 每次访问都按<b>当前语言</b>重新求值。要在 <c>Parameters</c> 里用它就必须写成属性
    /// （每次渲染重新取），<b>不要缓存到字段</b>，否则切换语言后下拉文案不跟着变。
    /// 实现必须极快且不得有 IO：它会在设置页滚动时被高频调用。
    /// </para>
    /// </summary>
    IReadOnlyList<CommandTerminalOption> Terminals { get; }

    /// <summary>在指定终端里执行命令。只负责<b>发起</b>，不等待命令结束。</summary>
    /// <param name="command">命令原文。</param>
    /// <param name="terminal">
    /// 终端标识，取值见 <see cref="Terminals"/>。调用方无需自行判断合法性：
    /// 未识别的值一律按 <c>cmd</c> 处理（为兼容早于本接口的历史配置）。
    /// </param>
    /// <returns>是否成功发起。失败原因记入宿主日志，<b>不弹对话框</b>（插件侧失败必须可忽略）。</returns>
    /// <exception cref="PluginCapabilityDeniedException">清单未声明 <see cref="PluginCapability.Process"/>。</exception>
    bool Run(string command, string terminal = "cmd");
}

/// <summary>一项 Shell 上下文动词。</summary>
public sealed class ShellVerbOption
{
    /// <summary>标识，如 <c>Windows.CopyAsPath</c>。写进配置的就是这个值。</summary>
    public string Id { get; init; } = "";

    /// <summary>显示名（宿主已按当前语言本地化）。</summary>
    public string DisplayName { get; init; } = "";
}

/// <summary>
/// 宿主已验证的 Shell 上下文动词能力：对<b>当前活动的资源管理器窗口及其选中项</b>执行操作
/// （复制路径、以管理员身份运行、在此处打开终端…）。
/// <para>
/// 与 <see cref="IHostCommandService"/> 一样需要 <see cref="PluginCapability.Process"/> ——
/// 其中若干动词会以提权方式启动进程。
/// </para>
/// <para>
/// <b>宿主自己的正式入口是带搜索与分类的专用挑选器</b>，本接口不做那个 UI：
/// 插件可以拿 <see cref="Verbs"/> 做下拉，也可以声明成自由文本让用户自己填。
/// SDK 只提供能力，不替插件决定怎么呈现。
/// </para>
/// </summary>
public interface IHostShellService
{
    /// <summary>
    /// 推荐的动词标识。<b>这不是白名单</b> —— <see cref="Invoke"/> 只挡空值。
    /// <para>
    /// 原因：历史配置里沉淀了大量旧别名（同一个动作既有 <c>Windows.CopyAsPath</c>
    /// 又有 <c>copy_path</c>），若按本清单校验，本是「防静默失效」的初衷会变成把老配置整体判死 ——
    /// 那是更糟的结果。所以清单只用于<b>呈现与推荐</b>。
    /// </para>
    /// </summary>
    IReadOnlyList<ShellVerbOption> Verbs { get; }

    /// <summary>执行一个上下文动词。只负责发起，不等待完成。</summary>
    /// <returns>是否成功发起（动词为空、或宿主判断当前上下文不适用时返回 false）。</returns>
    /// <exception cref="PluginCapabilityDeniedException">清单未声明 <see cref="PluginCapability.Process"/>。</exception>
    bool Invoke(string verb);
}

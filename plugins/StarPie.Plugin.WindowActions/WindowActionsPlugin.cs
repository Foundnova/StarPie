using StarPie.Plugin;

namespace StarPie.Plugin.WindowActions;

/// <summary>
/// StarPie 随包动作包「窗口动作」。
/// <para>
/// 它认领了五个顶层动作类型：<c>Tile</c> / <c>ToggleTopmost</c> / <c>MoveMonitor</c> /
/// <c>WindowOpacity</c> / <c>SwitchWindow</c> —— 认领声明在 csproj 的
/// <c>StarPiePluginTypeClaims</c> 程序集元数据里，宿主<b>不加载本程序集</b>就能读到它。
/// </para>
/// <para>
/// <b>为什么单独成一个包，而不是并进基础动作包</b>：
/// 拆包的唯一正当理由是「用户会想单独关掉它」。这一批的后果是
/// 「用户正在用的窗口被挪走 / 被置顶 / 被改透明 / 被切走」，
/// 与「启动程序、打开网址」完全不是一类风险。合在一起的话，
/// 只想关掉「乱动我窗口」的用户只能连打开网页一起关掉。
/// </para>
/// <para>
/// <b>它为什么声明 <c>WindowControl</c> 而不是复用的 <c>Process</c> / <c>Ui</c></b>：
/// 安装确认页上展示的能力必须对应一个真实后果 —— 用户看到「进程」两个字，
/// 脑子里想的是「它要启动程序」，而实际后果是他的窗口被挪走，那是标签名不副实。
/// <c>Ui</c> 的语义则是「打开自己的窗口」，同样不符。
/// </para>
/// <para>
/// <b>它为什么不包含「快捷热键」</b>：那个动作的 Type 是 <c>ActionItem.Type</c> 的默认值，
/// 也是每一个还没配过的新扇区的占位类型，必须永远可解析。放进可停用的插件里，
/// 一旦用户停用本包，所有空扇区按下去都会报「动作所属的包已停用」——
/// 而他压根没配过那些扇区。
/// </para>
/// </summary>
public sealed class WindowActionsPlugin : IStarPiePlugin
{
    // 刻意不缓存 IPluginContext 的任何「服务实例」（Windows / Host / I18n …）：
    // Shutdown 之后任何一次残留调用都会摸到一个已被卸载的 ALC 里的对象。
    // 只留 context 本身一个引用、置空即断链，是最不容易出错的形态。
    private IPluginContext? _context;

    public void Initialize(IPluginContext context)
    {
        _context = context;

        // ① 词条先登记：后面几个动作的 Descriptor 与 Parameters 会在属性访问时查当前语言文案。
        Texts.Register(context);

        // ② 动作登记。短 ID 必须与 csproj 里认领串右侧的值逐字一致 ——
        //    对不上时宿主会建立一条指向不存在贡献点的认领，表现是配置里的动作
        //    「找到了归属、却永远执行不了」。自检 [3i] 专门守这一条。
        context.Actions.Register(new TileAction(context));
        context.Actions.Register(new ToggleTopmostAction(context));
        context.Actions.Register(new MoveMonitorAction(context));
        context.Actions.Register(new WindowOpacityAction(context));
        context.Actions.Register(new SwitchWindowAction(context));

        context.Log.Info("窗口动作包已就绪：平铺窗口 / 窗口置顶 / 移到下一屏 / 窗口透明度 / 切换窗口");
    }

    public void Shutdown() => _context = null;
}

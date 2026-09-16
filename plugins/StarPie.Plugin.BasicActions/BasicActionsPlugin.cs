using StarPie.Plugin;

namespace StarPie.Plugin.BasicActions;

/// <summary>
/// StarPie 随包动作包「基础动作」。
/// <para>
/// 它认领了 <c>Launch</c> / <c>WebUrl</c>（含别名 <c>Url</c>）/ <c>Folder</c>（含别名
/// <c>OpenFolder</c>）三个顶层动作类型 —— 认领声明在 csproj 的
/// <c>StarPiePluginTypeClaims</c> 程序集元数据里，宿主<b>不加载本程序集</b>就能读到它。
/// </para>
/// <para>
/// <b>它为什么不包含「快捷热键」</b>：那个动作的 Type 是 <c>ActionItem.Type</c> 的默认值，
/// 也是每一个还没配过的新扇区的占位类型，必须永远可解析。放进可停用的插件里，
/// 一旦用户停用本包，所有空扇区按下去都会报「动作所属的包已停用」—— 而他压根没配过那些扇区。
/// </para>
/// </summary>
public sealed class BasicActionsPlugin : IStarPiePlugin
{
    // 刻意不缓存 IPluginContext 的任何服务实例：Shutdown 之后任何一次残留调用都会摸到
    // 一个已被卸载的 ALC 里的对象。只留一个引用、置空即断链，是最不容易出错的形态。
    private IPluginContext? _context;

    public void Initialize(IPluginContext context)
    {
        _context = context;

        // ① 词条先登记：后面三个动作的 Descriptor 会在属性访问时查当前语言文案。
        Texts.Register(context);

        // ② 动作登记。短 ID 必须与 csproj 里认领串右侧的值逐字一致 ——
        //    对不上时宿主会建立一条指向不存在贡献点的认领，表现是配置里的动作
        //    「找到了归属、却永远执行不了」。自检 [3i] 专门守这一条。
        context.Actions.Register(new LaunchAction(context));
        context.Actions.Register(new WebUrlAction(context));
        context.Actions.Register(new FolderAction(context));

        context.Log.Info("基础动作包已就绪：启动程序 / 打开网址 / 打开文件夹");
    }

    public void Shutdown() => _context = null;
}

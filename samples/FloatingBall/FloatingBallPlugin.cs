using System;
using System.Collections.Generic;
using StarPie.Plugin;

namespace StarPie.Plugin.FloatingBall;

/// <summary>
/// 悬浮球插件入口。
/// <para>
/// 它是 SDK 1.5 那条接缝的<b>第一个真实用户</b>，两半分别在两边：
/// 球由插件自己画（<see cref="PluginCapability.Ui"/>），点球之后由宿主呼出
/// <b>用户配置的</b>轮盘（<see cref="PluginCapability.Wheel"/>）。
/// 插件全程不认识轮盘的外观、扇区、命中与配置 —— 这正是把它交给宿主的意义。
/// </para>
/// <para>
/// <b>球不是常驻进程，是常驻窗口</b>：本插件靠「插件管理页 → 开机预加载」在启动后台加载并
/// 把球放出来；没开预加载时球不会自己出现，需要用户先把「显示悬浮球」挂到轮盘扇区或快捷键上。
/// 这条前提写在 <see cref="Initialize"/> 的注释里，也写进 README 级别的交付说明。
/// </para>
/// </summary>
public sealed class FloatingBallPlugin : IStarPiePlugin
{
    private IPluginContext? _context;
    private BallController? _ball;

    public void Initialize(IPluginContext context)
    {
        _context = context;

        RegisterLocalization(context);

        // 先建控制器、后注册动作：两个动作的闭包都要引用它，注册期就会读 Descriptor。
        BallController ball = new BallController(context);
        _ball = ball;

        string? iconKey = RegisterIcon(context);
        context.Actions.Register(new ShowBallContribution(context, ball, iconKey));
        context.Actions.Register(new HideBallContribution(context, ball));

        WarnIfCapabilitiesMissing(context);

        // 上一轮用户留着「球是显示着的」，这一轮开机就该看得见。
        // 先读开关再投递，而不是把判断整个放进闭包：一次 Post 会在 UI 线程的队列里
        // 留下一个握着插件闭包的操作项，它没跑完之前宿主判不出「程序集已回收」，
        // 停用时就会被判成「引用有残留，需要重启」。没有活要干就别排队。
        // 另一层理由：Initialize 的契约是「只注册、不做耗时操作」，
        // 而建一个 AllowsTransparency 窗口要过一遍布局与 DWM 合成。
        if (_ball.IsMarkedVisible()) context.Dispatcher.Post(() => _ball.Restore());

        context.Log.Info("已注册 2 个动作（显示 / 隐藏悬浮球）。点球唤出轮盘由宿主 IHostWheelService 负责。");
    }

    public void Shutdown()
    {
        BallController? ball = _ball;
        _ball = null;

        if (ball != null && ball.HasWindow)
        {
            try
            {
                // 关窗必须在 UI 线程上发生，所以投递而不是就地执行 —— Shutdown 里阻塞等待
                // UI 线程是在拿「宿主正在停插件的线程」去等「正在被插件占住的线程」，没有收益只有死锁面。
                // 只在真有一颗球时才投递：一个什么也不做的操作项本身就把插件闭包
                // 握在 UI 线程的队列里，宿主的卸载探针会因此判成「引用有残留，需重启」。
                _context?.Dispatcher.Post(ball.Shutdown);
            }
            catch (Exception ex)
            {
                _context?.Log.Warn($"投递关闭悬浮球的动作失败：{ex.Message}");
            }
        }

        _context?.Log.Info("已停用。");
        _context = null;
    }

    // ------------------------------------------------------------------ 注册细节

    private static void RegisterLocalization(IPluginContext context)
    {
        II18nRegistry i18n = context.I18n;

        i18n.Register("action.show-ball.name", "显示悬浮球", "Show floating ball");
        i18n.Register("action.hide-ball.name", "隐藏悬浮球", "Hide floating ball");

        i18n.Register("field.diameter.label", "直径", "Diameter");
        i18n.Register("field.opacity.label", "不透明度", "Opacity");
        i18n.Register("field.color.label", "颜色", "Color");

        i18n.Register("preview.show-ball", "直径 {0} · 不透明 {1}%", "Diameter {0} · Opacity {1}%");
        i18n.Register("preview.hide-ball", "收掉悬浮球", "Dismiss the floating ball");

        i18n.Register("info.shown", "悬浮球已显示，点它即可呼出轮盘。", "Floating ball shown. Click it to bring up your wheel.");

        i18n.Register("notify.title", "悬浮球", "Floating ball");
        i18n.Register("notify.no-capability",
            "本插件的清单里没有「轮盘呼出」能力，点球不会有任何反应。请重新安装并在确认页勾选该能力。",
            "This plugin does not declare the 'Wheel' capability, so clicking the ball does nothing. Reinstall it and accept that capability.");
        i18n.Register("notify.wheel-refused",
            "轮盘现在呼不出来（可能正有一次鼠标手势在进行，或屏幕上没有可用的显示区域）。稍等一下再点。",
            "The wheel could not be brought up right now (a gesture may be in progress, or no display area is available). Try again in a moment.");

        i18n.Register("error.cancelled", "动作已被取消。", "The action was cancelled.");
        i18n.Register("notify.no-ui-capability",
            "清单里没声明「界面」能力，本插件画不出球窗。",
            "The manifest does not declare the 'Ui' capability, so this plugin cannot create its ball window.");
        i18n.Register("error.diameter", "直径得是个数（像素）。", "Diameter must be a number (pixels).");
        i18n.Register("error.diameter-range", "直径要在 {0} 到 {1} 之间。", "Diameter must be between {0} and {1}.");
        i18n.Register("error.opacity", "不透明度得是个数（百分比）。", "Opacity must be a number (percent).");
        i18n.Register("error.opacity-range", "不透明度要在 {0} 到 {1} 之间。", "Opacity must be between {0} and {1}.");
        i18n.Register("error.show-failed", "悬浮球没能显示出来：{0}", "The floating ball could not be shown: {0}");
        i18n.Register("error.hide-failed", "悬浮球没能收起来：{0}", "The floating ball could not be hidden: {0}");
    }

    private static string? RegisterIcon(IPluginContext context)
    {
        try
        {
            return context.Icons.RegisterSvg(
                "ball",
                "M12 3.2a8.8 8.8 0 1 0 0 17.6 8.8 8.8 0 0 0 0-17.6Zm0 2.4a6.4 6.4 0 1 1 0 12.8 6.4 6.4 0 0 1 0-12.8Z");
        }
        catch (Exception ex)
        {
            // 图标注册失败不是致命伤：动作照样能用，只是列表里没图。
            // 让它抛出去会把整个插件判成加载失败，那是用一个装饰换掉一个功能。
            context.Log.Warn($"悬浮球图标注册失败（动作仍可用，只是没有图标）：{ex.Message}");
            return null;
        }
    }

    private static void WarnIfCapabilitiesMissing(IPluginContext context)
    {
        var missing = new List<string>(2);

        if (!context.Info.HasCapability(PluginCapability.Ui)) missing.Add("Ui（画那颗球）");
        if (!context.Info.HasCapability(PluginCapability.Wheel)) missing.Add("Wheel（呼出轮盘）");

        if (missing.Count == 0) return;

        // 这里出声是必要的：两半能力缺任何一半，用户看到的现象都是同一个
        // 「球在，但点它没反应」—— 而那个现象最容易被报成宿主的 bug。
        context.Log.Warn(
            $"清单缺少必要能力：{string.Join("、", missing)}。请在 plugin.json 的 capabilities 里补齐后重新安装。");
    }
}

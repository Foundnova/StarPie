using System;
using System.Collections.Generic;
using System.Globalization;
using System.Threading;
using System.Threading.Tasks;
using StarPie.Plugin;

namespace StarPie.Plugin.FloatingBall;

/// <summary>
/// 动作一：显示悬浮球（并按参数设定它的外观）。
/// <para>
/// 直径 / 不透明度 / 颜色做成<b>动作参数</b>而不是插件自己的设置界面，是这套插件系统的前提：
/// 宿主不给插件任何设置页（参数由宿主用主程序既有的控件风格渲染，见 <see cref="ParameterField"/>）。
/// 于是「这颗球长什么样」跟着那条动作走 —— 用户可以配两个扇区，一个常规球、一个小而淡的球，
/// 这比在插件里塞一个设置窗更符合轮盘的使用方式。
/// </para>
/// </summary>
internal sealed class ShowBallContribution : IActionContribution
{
    private readonly IPluginContext _context;
    private readonly BallController _ball;
    private readonly string? _iconKey;

    public ShowBallContribution(IPluginContext context, BallController ball, string? iconKey)
    {
        _context = context;
        _ball = ball;
        _iconKey = iconKey;
    }

    public ActionDescriptor Descriptor => new()
    {
        Id = "showBall",
        DisplayName = "显示悬浮球",
        DisplayNameKey = "action.show-ball.name",
        Description = "在屏幕上放一颗常驻悬浮球，点它呼出你的轮盘。拖动可挪位置，右键收起。",
        Category = "悬浮球",
        IconKey = _iconKey,
        Kind = ActionKind.Sequential,
        TimeoutSeconds = 3,
    };

    public IReadOnlyList<ParameterField> Parameters => new List<ParameterField>
    {
        new()
        {
            Key = "diameter",
            Label = "直径",
            LabelKey = "field.diameter.label",
            Type = ParameterFieldType.Number,
            DefaultValue = Defaults.DiameterDiu.ToString("0.##", CultureInfo.InvariantCulture),
            Min = Defaults.DiameterMin,
            Max = Defaults.DiameterMax,
            HelpText = "单位是逻辑像素（跟随系统缩放）。",
        },
        new()
        {
            Key = "opacity",
            Label = "不透明度",
            LabelKey = "field.opacity.label",
            Type = ParameterFieldType.Number,
            DefaultValue = Defaults.OpacityPercent.ToString("0.##", CultureInfo.InvariantCulture),
            Min = Defaults.OpacityMin,
            Max = Defaults.OpacityMax,
        },
        new()
        {
            Key = "color",
            Label = "颜色",
            LabelKey = "field.color.label",
            Type = ParameterFieldType.Color,
            DefaultValue = Defaults.Color,
        },
    };

    public string? Validate(IReadOnlyDictionary<string, string> parameters)
    {
        if (!TryReadDouble(parameters, "diameter", out double diameter))
        {
            return _context.I18n.T("error.diameter", "直径得是个数（像素）。");
        }
        if (diameter < Defaults.DiameterMin || diameter > Defaults.DiameterMax)
        {
            return string.Format(
                CultureInfo.InvariantCulture,
                _context.I18n.T("error.diameter-range", "直径要在 {0} 到 {1} 之间。"),
                Defaults.DiameterMin, Defaults.DiameterMax);
        }

        if (!TryReadDouble(parameters, "opacity", out double opacity))
        {
            return _context.I18n.T("error.opacity", "不透明度得是个数（百分比）。");
        }
        if (opacity < Defaults.OpacityMin || opacity > Defaults.OpacityMax)
        {
            return string.Format(
                CultureInfo.InvariantCulture,
                _context.I18n.T("error.opacity-range", "不透明度要在 {0} 到 {1} 之间。"),
                Defaults.OpacityMin, Defaults.OpacityMax);
        }

        return null;
    }

    public string Preview(IReadOnlyDictionary<string, string> parameters)
    {
        // 契约要求极快：这里只做字符串拼接，不碰窗口、不读设置。
        TryReadDouble(parameters, "diameter", out double diameter);
        TryReadDouble(parameters, "opacity", out double opacity);

        if (diameter <= 0) diameter = Defaults.DiameterDiu;
        if (opacity <= 0) opacity = Defaults.OpacityPercent;

        return string.Format(
            CultureInfo.InvariantCulture,
            _context.I18n.T("preview.show-ball", "直径 {0} · 不透明 {1}%"),
            diameter.ToString("0.##", CultureInfo.InvariantCulture),
            opacity.ToString("0.##", CultureInfo.InvariantCulture));
    }

    public async Task<ActionResult> ExecuteAsync(PluginActionInput input, CancellationToken cancellationToken)
    {
        if (cancellationToken.IsCancellationRequested)
        {
            return ActionResult.Fail(_context.I18n.T("error.cancelled", "动作已被取消。"));
        }

        if (!_context.Info.HasCapability(PluginCapability.Ui))
        {
            // 装机时用户可以在确认页上不勾「界面」，这属于「环境不具备」而不是插件出错：
            // 报 Fail 会让连点五次之后一个正常插件被判隔离（宿主纪律，见 AGENTS.md 的熔断一节）。
            return ActionResult.Ok(
                _context.I18n.T("notify.no-ui-capability", "清单里没声明「界面」能力，本插件画不出球窗。"),
                silent: false);
        }

        double diameter = input.Double("diameter", Defaults.DiameterDiu);
        double opacity = input.Double("opacity", Defaults.OpacityPercent);
        string color = string.IsNullOrWhiteSpace(input.Parameter("color")) ? Defaults.Color : input.Parameter("color")!.Trim();

        try
        {
            // 球是 WPF 窗口，只能在 UI 线程上建；动作线程到这里必须切一次。
            await _context.Dispatcher.InvokeAsync(() => _ball.Show(diameter, opacity, color)).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            _context.Log.Error("显示悬浮球失败", ex);
            return ActionResult.Fail(string.Format(
                CultureInfo.InvariantCulture,
                _context.I18n.T("error.show-failed", "悬浮球没能显示出来：{0}"),
                ex.Message));
        }

        return ActionResult.Ok(_context.I18n.T("info.shown", "悬浮球已显示，点它即可呼出轮盘。"), silent: true);
    }

    private static bool TryReadDouble(IReadOnlyDictionary<string, string> parameters, string key, out double value)
    {
        value = 0;
        if (!parameters.TryGetValue(key, out string? raw)) return true;
        if (string.IsNullOrWhiteSpace(raw)) return true;

        // 与 PluginActionInput.Double 同一条口径：不变文化。宿主写盘用的就是它。
        return double.TryParse(raw, NumberStyles.Float, CultureInfo.InvariantCulture, out value);
    }
}

/// <summary>
/// 动作二：隐藏悬浮球。
/// <para>
/// 它与「右键收球」是同一条路（都走 <see cref="BallController.Hide"/>），差别只在要不要落盘
/// <c>visible=false</c> —— 落了这个标记，下次开机就不会再自动出现。
/// 没有这个动作的话，用户只能右键收球、然后再也找不回开机自启的开关。
/// </para>
/// </summary>
internal sealed class HideBallContribution : IActionContribution
{
    private readonly IPluginContext _context;
    private readonly BallController _ball;

    public HideBallContribution(IPluginContext context, BallController ball)
    {
        _context = context;
        _ball = ball;
    }

    public ActionDescriptor Descriptor => new()
    {
        Id = "hideBall",
        DisplayName = "隐藏悬浮球",
        DisplayNameKey = "action.hide-ball.name",
        Description = "收掉当前显示的悬浮球，并记住这个选择（下次启动不再自动出现）。",
        Category = "悬浮球",
        Kind = ActionKind.Sequential,
        TimeoutSeconds = 3,
    };

    public IReadOnlyList<ParameterField> Parameters => Array.Empty<ParameterField>();

    public string? Validate(IReadOnlyDictionary<string, string> parameters) => null;

    public string Preview(IReadOnlyDictionary<string, string> parameters) =>
        _context.I18n.T("preview.hide-ball", "收掉悬浮球");

    public async Task<ActionResult> ExecuteAsync(PluginActionInput input, CancellationToken cancellationToken)
    {
        if (cancellationToken.IsCancellationRequested)
        {
            return ActionResult.Fail(_context.I18n.T("error.cancelled", "动作已被取消。"));
        }

        try
        {
            await _context.Dispatcher.InvokeAsync(_ball.Hide).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            _context.Log.Error("隐藏悬浮球失败", ex);
            return ActionResult.Fail(string.Format(
                CultureInfo.InvariantCulture,
                _context.I18n.T("error.hide-failed", "悬浮球没能收起来：{0}"),
                ex.Message));
        }

        return ActionResult.Ok(Preview(input.Parameters), silent: true);
    }
}

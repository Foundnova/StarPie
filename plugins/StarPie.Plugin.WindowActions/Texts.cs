using System.Collections.Generic;

namespace StarPie.Plugin.WindowActions;

/// <summary>
/// 本包的词条表。
/// <para>
/// <b>为什么要自带一份，而不是去读宿主的中文表</b>：插件拿到的 <see cref="II18nRegistry"/>
/// 只能注册与读取<b>自己命名空间下</b>的词条（<c>plugin.&lt;id&gt;.*</c>），读不到宿主的
/// <c>ActionTypeTileShort</c> 这类键 —— 这是刻意的隔离，插件不该依赖宿主内部键名。
/// 所以动作的显示名必须在插件里再声明一次。
/// </para>
/// <para>
/// <b>多出来的一份「布局名」没有抄</b>：那 17 个布局的显示名<b>从宿主服务现取</b>
/// （<see cref="IHostWindowService.Layouts"/>），不在这里登记。
/// 布局表是宿主执行体的领域知识，抄一份的后果是「宿主加了新布局、插件下拉里没有」，
/// 或反过来「插件里能选、宿主执行体不认」—— 两种都是静默失效。
/// 这里只保留三个标记的文案（<c>Cycle</c> / <c>CycleBack</c> / <c>Restore</c>），
/// 因为它们是「对布局的操作」，不是布局本身，宿主没有对应的清单可查。
/// </para>
/// <para>
/// <b>带占位符的模板</b>（<c>{0}</c>）也在这里登记，由调用方 <c>string.Format</c> 填充。
/// 这样英文用户填错透明度时看到的也是英文提示，而不是一句中文。
/// </para>
/// <para>
/// <b>覆盖范围</b>：标题、字段标签与校验提示给了 zh-CN / zh-TW / en / ja 四套
/// （这些是用户真正会读到的）；帮助文案只给了 zh-CN / en，其余语言回落 zh-CN ——
/// 与宿主内置面板的现状一致，是如实告知而不是遗漏。
/// </para>
/// </summary>
internal static class Texts
{
    // ---------------------------------------------------------------- zh-CN（兜底语言，也是各处的 fallback）

    internal const string TileTitle = "平铺窗口";
    internal const string TileLayout = "平铺方式";
    internal const string TileCycle = "循环切换布局";
    internal const string TileCycleBack = "循环返回";
    internal const string TileRestore = "还原所有窗口（回到平铺前）";
    internal const string TileEmpty = "未选择平铺方式。";

    internal const string TopmostTitle = "窗口置顶/取消置顶";

    internal const string MonitorTitle = "窗口移到下一屏";

    internal const string OpacityTitle = "窗口透明度";
    internal const string OpacityValue = "透明度 (%)";

    // 下面两条<b>刻意带占位符</b>：范围由宿主服务给出（IHostWindowService.OpacityMinPercent /
    // OpacityMaxPercent），不在这里写死数字。写死的话宿主调整范围后，
    // 提示文案会继续把旧范围告诉用户 —— 而用户会照着它填。
    internal const string OpacityHelp = "取值 {0}~{1}，{1} 表示完全不透明。";
    internal const string OpacityEmpty = "未设置窗口透明度。";
    internal const string OpacityNotNumber = "透明度「{0}」不是有效数字，请填写范围内的数值。";
    internal const string OpacityOutOfRange = "透明度需在 {0}~{1} 之间（当前填写 {2}）。";

    internal const string SwitchTitle = "切换窗口";
    internal const string SwitchIndex = "任务栏位置";
    internal const string SwitchHelp = "任务栏上从左往右数的第几个图标，从 1 开始。";
    internal const string SwitchEmpty = "未设置任务栏位置。";
    internal const string SwitchNotNumber = "「{0}」不是有效的序号，请填写一个正整数。";
    internal const string SwitchTooSmall = "任务栏位置需从 1 开始计数。";

    /// <summary>
    /// 清单漏声明 <c>WindowControl</c> 时的兜底提示。
    /// <para>
    /// 五个动作共用一句，所以抽成常量而不是各写一遍 —— 复制五份的后果是
    /// 某天改写其中四份、第五份留着一句过时的说法。
    /// </para>
    /// <para>
    /// 正常情况下走不到这里：本包清单已声明该能力。<b>但只要它发生，就必须出声</b> ——
    /// 否则用户看到的是「按下去什么也没发生」，而不是「这个包缺一行声明」。
    /// </para>
    /// </summary>
    internal const string CapabilityMissing =
        "本插件缺少「窗口控制」能力声明，该动作已被拒绝。请重新安装本插件，或联系插件作者。";

    // ---------------------------------------------------------------- 注册表

    /// <summary>zh-CN 与 en 两套。<see cref="II18nRegistry.Register"/> 一次接收这两种语言。</summary>
    internal static readonly (string Key, string ZhCn, string En)[] Base = new[]
    {
        ("tile.title", TileTitle, "Tile Windows"),
        ("tile.layout", TileLayout, "Layout"),
        ("tile.cycle", TileCycle, "Cycle layouts"),
        ("tile.cycleBack", TileCycleBack, "Cycle previous"),
        ("tile.restore", TileRestore, "Restore all windows (pre-tile state)"),
        ("tile.empty", TileEmpty, "No layout selected. Please choose one in the action settings."),

        ("topmost.title", TopmostTitle, "Toggle Always-on-Top"),

        ("monitor.title", MonitorTitle, "Move to Next Monitor"),

        ("opacity.title", OpacityTitle, "Window Opacity"),
        ("opacity.value", OpacityValue, "Opacity (%)"),
        ("opacity.help", OpacityHelp, "Range {0}-{1}; {1} means fully opaque."),
        ("opacity.empty", OpacityEmpty, "No opacity value set. Please fill it in the action settings."),
        ("opacity.notNumber", OpacityNotNumber,
            "\"{0}\" is not a valid number. Please enter a value within the allowed range."),
        ("opacity.outOfRange", OpacityOutOfRange, "Opacity must be between {0} and {1} (currently {2})."),

        ("switch.title", SwitchTitle, "Switch Window"),
        ("switch.index", SwitchIndex, "Taskbar position"),
        ("switch.help", SwitchHelp, "The icon position on the taskbar, counted from the left starting at 1."),
        ("switch.empty", SwitchEmpty, "No taskbar position set. Please fill it in the action settings."),
        ("switch.notNumber", SwitchNotNumber, "\"{0}\" is not a valid index. Please enter a positive integer."),
        ("switch.tooSmall", SwitchTooSmall, "The taskbar position starts counting from 1."),

        ("capability.missing", CapabilityMissing,
            "This plugin does not declare the \"Window control\" capability, so the action was rejected. " +
            "Please reinstall the plugin or contact the author."),
    };

    // ---------------------------------------------------------------- 其余语言

    internal static readonly Dictionary<string, string> ZhTw = new()
    {
        ["tile.title"] = "平鋪視窗",
        ["tile.layout"] = "平鋪方式",
        ["tile.cycle"] = "循環切換佈局",
        ["tile.cycleBack"] = "循環返回",
        ["tile.restore"] = "還原所有視窗（回到平鋪前）",
        ["tile.empty"] = "未選擇平鋪方式。",
        ["topmost.title"] = "視窗置頂/取消置頂",
        ["monitor.title"] = "視窗移到下一螢幕",
        ["opacity.title"] = "視窗透明度",
        ["opacity.value"] = "透明度 (%)",
        ["opacity.empty"] = "未設定視窗透明度。",
        ["switch.title"] = "切換視窗",
        ["switch.index"] = "工作列位置",
        ["switch.empty"] = "未設定工作列位置。",
        ["capability.missing"] = "本外掛缺少「視窗控制」能力宣告，該動作已被拒絕。請重新安裝本外掛，或聯絡外掛作者。",
    };

    internal static readonly Dictionary<string, string> Ja = new()
    {
        ["tile.title"] = "ウィンドウを並べる",
        ["tile.layout"] = "並べ方",
        ["tile.cycle"] = "レイアウトを順番に切替",
        ["tile.cycleBack"] = "前のレイアウトへ",
        ["tile.restore"] = "すべてのウィンドウを元に戻す（並べる前の状態）",
        ["tile.empty"] = "並べ方が選択されていません。",
        ["topmost.title"] = "最前面表示の切替",
        ["monitor.title"] = "次のモニターへ移動",
        ["opacity.title"] = "ウィンドウの透明度",
        ["opacity.value"] = "透明度 (%)",
        ["opacity.empty"] = "透明度が設定されていません。",
        ["switch.title"] = "ウィンドウ切替",
        ["switch.index"] = "タスクバーの位置",
        ["switch.empty"] = "タスクバーの位置が設定されていません。",
        ["capability.missing"] =
            "このプラグインは「ウィンドウ制御」権限を宣言していないため、操作は拒否されました。再インストールするか作者に連絡してください。",
    };

    /// <summary>把三张表一次性登记进宿主。</summary>
    internal static void Register(IPluginContext context)
    {
        foreach ((string key, string zhCn, string en) in Base)
        {
            context.I18n.Register(key, zhCn, en);
        }

        context.I18n.RegisterTable("zh-TW", ZhTw);
        context.I18n.RegisterTable("ja", Ja);
    }
}

using System;
using System.Collections.Generic;
using StarPie.Plugin;

namespace WinPieGestures.Plugins;

/// <summary>
/// 插件级参数页的宿主侧入口（SDK 1.6）。
/// <para>
/// <b>刻意做成静态、且不依赖任何窗口</b>：自检（<c>--plugin-selftest</c>）要在无 UI 的进程里
/// 跑完整条「声明 → 回填 → 校验 → 落盘 → 插件读回」链路。若把这套逻辑写成窗口的方法，
/// 它就只能在人肉点鼠标时被验证到，而回归最容易坏的恰恰是这里的数据流。
/// </para>
/// <para>
/// 渲染时机是<b>点击「设置」的那一刻</b>：那时词条早已提交，标题与字段标签才解析得到当前语言。
/// 若在注册期就把文案算好存起来，就会踩上 <c>PluginCatalog.ResolveStagedDisplayNames</c>
/// 记录的那个「暂存区里没有词条」的时序坑。
/// </para>
/// </summary>
internal static class PluginSettingsPageService
{
    /// <summary>一次打开：声明 + 写入目标 + 已解析的标题说明。</summary>
    internal sealed class Page
    {
        public string PluginId { get; init; } = "";

        /// <summary>标题（已按当前语言解析）。</summary>
        public string Title { get; init; } = "";

        /// <summary>说明（已按当前语言解析）。可能为空。</summary>
        public string Description { get; init; } = "";

        public IReadOnlyList<ParameterField> Fields { get; init; } = Array.Empty<ParameterField>();

        /// <summary>写穿目标；<see cref="Persist"/> 负责落盘。</summary>
        public PluginSettingsParameterTarget Target { get; init; } = null!;
    }

    /// <summary>
    /// 这张卡片该不该显示「设置」按钮。
    /// <para>
    /// 声明了页但<b>一个字段都没有</b>时返回 false：点开是一张空表，
    /// 不如让按钮根本不出现 —— 「有入口却没内容」比「没有入口」更让人怀疑程序坏了。
    /// </para>
    /// </summary>
    public static bool HasPage(string pluginId)
    {
        PluginSettingsPageRegistration? page = PluginHost.Catalog.TryGetSettingsPage(pluginId);
        return page != null && page.Fields.Count > 0;
    }

    /// <summary>
    /// 解析出可渲染的页。<c>null</c> 表示没有入口（未声明、空表、或插件已不在本会话加载）。
    /// </summary>
    public static Page? Open(string pluginId)
    {
        PluginSettingsPageRegistration? page = PluginHost.Catalog.TryGetSettingsPage(pluginId);
        if (page == null || page.Fields.Count == 0) return null;

        // 值存在插件的 settings.json 里，而那份配置属于「已加载的插件实例」。
        // 实例不在（被停用 / 从未启用）时连读写目标都没有，宁可不给入口也不去造一个
        // 写盘无人读的临时实例。
        PluginInstance? instance = PluginHost.Find(pluginId);
        if (instance == null) return null;

        return new Page
        {
            PluginId = page.PluginId,
            Title = ResolveText(page.PluginId, page.TitleKey, page.Title),
            Description = ResolveText(page.PluginId, page.DescriptionKey, page.Description),
            Fields = page.Fields,
            Target = new PluginSettingsParameterTarget(instance.Settings),
        };
    }

    /// <summary>按声明校验页上当前已保存的值。</summary>
    public static List<PluginParameterIssue> Validate(Page page)
    {
        if (page == null) return new List<PluginParameterIssue>();
        return PluginParameterValidator.Validate(page.Fields, page.Target.Stored);
    }

    /// <summary>
    /// 落盘并返回当前校验问题（值为空表示干净）。
    /// <para>
    /// 与动作参数不同，这里没有「保存」按钮可点 —— 页面是写穿的，插件随时在读。
    /// 所以校验失败时不能撤回已经写进内存的值（那等于偷偷丢弃用户的输入），
    /// 只能把问题显示出来，让用户改对了再关一次。
    /// </para>
    /// </summary>
    public static List<PluginParameterIssue> Persist(Page page)
    {
        if (page == null) return new List<PluginParameterIssue>();

        page.Target.Save();
        return Validate(page);
    }

    /// <summary>
    /// 标题/说明的取词口径与动作显示名一致：短键命中就用译文，否则用插件给的字面文案。
    /// </summary>
    private static string ResolveText(string pluginId, string? key, string fallback)
    {
        string? translated = PluginI18n.Resolve(pluginId, key);
        if (translated != null) return translated;
        return fallback ?? "";
    }
}

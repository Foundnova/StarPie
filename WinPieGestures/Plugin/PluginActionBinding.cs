using System;
using System.Collections.Generic;
using StarPie.Plugin;

namespace WinPieGestures.Plugins;

/// <summary>
/// 「插件动作」在主程序 UI 与持久化模型之间的编解码器。
/// <para>
/// <b>为什么需要单独一个类型</b>：插件动作与内置动作在界面上的差别只有一处 ——
/// 内置动作靠 <see cref="ActionItem.Type"/> 一个字符串就能唯一定位
/// （<c>"Hotkey"</c> / <c>"Ocr"</c> / <c>"Tile"</c>…），而插件动作有 N 个，
/// 全都持久化成同一个 <c>Type="Plugin"</c>，真正的身份在
/// <see cref="ActionItem.PluginActionRef"/> 里。
/// </para>
/// <para>
/// 但 WPF 的下拉框是 <c>SelectedValuePath="Tag"</c> + <c>SelectedValue="{Binding …}"</c>，
/// 只能传递一个值。于是必须有一个地方把「Type + Ref」双向投影成单个可比较的字符串。
/// 这个类就是那个地方 —— <b>三处 ViewModel 共用同一份实现</b>，
/// 避免 <c>GestureMappingViewModel</c> / <c>SubSlotViewModel</c> / 焦点动作编辑器各自写一份
/// 而出现行为不一致。
/// </para>
/// </summary>
internal static class PluginActionBinding
{
    /// <summary>插件动作在 <see cref="ActionItem.Type"/> 中的固定取值。</summary>
    public const string TypeName = PluginApi.ActionTypeName;

    /// <summary>
    /// 下拉框 Tag 的命名空间前缀。
    /// <para>
    /// 之所以加前缀而不是直接放 <c>Plugin</c>，是因为要能<b>区分三种状态</b>：
    /// ① <c>Plugin</c>        —— 兜底项（尚未选择具体动作，或所选动作已失效）；
    /// ② <c>Plugin:&lt;id&gt;</c>  —— 某个具体插件动作。
    /// 没有前缀的话这两种情况在字符串上无法区分。
    /// </para>
    /// </summary>
    public const string TagPrefix = "Plugin:";

    /// <summary>把贡献点全 ID 编成下拉框 Tag。</summary>
    public static string BuildTag(string fullId) => TagPrefix + fullId;

    /// <summary>从下拉框 Tag 中拆出贡献点全 ID。不是插件 Tag 时返回 false。</summary>
    public static bool TryParseTag(string? tag, out string fullId)
    {
        fullId = "";
        if (string.IsNullOrEmpty(tag)) return false;
        if (!tag.StartsWith(TagPrefix, StringComparison.OrdinalIgnoreCase)) return false;

        string rest = tag.Substring(TagPrefix.Length);
        if (string.IsNullOrWhiteSpace(rest)) return false;

        fullId = rest;
        return true;
    }

    /// <summary>
    /// 把动作<b>投影</b>成下拉框应当选中的 Tag（读路径）。
    /// <para>
    /// 有两种情况会退回兜底项 <c>"Plugin"</c>，而不是返回一个列表里不存在的 Tag：
    /// </para>
    /// <list type="bullet">
    /// <item>动作压根没引用插件贡献点（<see cref="ActionItem.PluginActionRef"/> 为空）；</item>
    /// <item>引用还在、但贡献点已随插件停用/卸载而消失 —— 这在用户「先把图标配好、再停用插件」时很常见。</item>
    /// </list>
    /// <para>
    /// 若不这样兜底，WPF 会因为 <c>SelectedValue</c> 匹配不到任何项而把下拉框显示成<b>空白</b>，
    /// 用户会以为自己的配置丢了。
    /// </para>
    /// </summary>
    public static string ProjectTag(ActionItem? action)
    {
        PluginActionRef? reference = action?.PluginActionRef;
        if (reference == null || !reference.IsValid) return TypeName;

        return PluginHost.TryGetAction(reference.FullId, out _)
            ? BuildTag(reference.FullId)
            : TypeName;
    }

    /// <summary>
    /// 把用户选中的插件动作<b>写入</b>动作对象（写路径）。
    /// </summary>
    /// <returns>贡献点存在并写入成功返回 true；插件已停用/卸载导致贡献点不存在时返回 false。</returns>
    public static bool Apply(ActionItem? action, string fullId)
    {
        if (action == null || string.IsNullOrWhiteSpace(fullId)) return false;

        if (!PluginHost.TryGetAction(fullId, out PluginActionRegistration registration))
        {
            // 例如右键菜单里选择时插件刚好被停用。保持原配置不动，由调用方给出提示。
            return false;
        }

        action.Type = TypeName;
        action.PluginActionRef = new PluginActionRef
        {
            PluginId = registration.PluginId,
            ContributionId = registration.ShortId,
        };

        // 名称与图标只在「尚未自定义」时自动填充。
        // 若用户已经手写过名字，切换动作不应该把它冲掉。
        if (ShouldAutoFillName(action.Name))
        {
            action.Name = string.IsNullOrWhiteSpace(registration.DisplayName)
                ? registration.ShortId
                : registration.DisplayName;
        }

        // 图标：空着、或上一次填的也是插件图标（说明是自动填的）时跟随动作走。
        if (string.IsNullOrEmpty(action.IconKey) ||
            action.IconKey.StartsWith(PluginApi.IconKeyPrefix, StringComparison.OrdinalIgnoreCase))
        {
            action.IconKey = registration.IconKey ?? "";
        }

        return true;
    }

    /// <summary>清空插件引用（切回内置动作类型时调用，避免残留一个指向插件的死引用）。</summary>
    public static void Clear(ActionItem? action)
    {
        if (action == null) return;
        action.PluginActionRef = null;
        action.ExtensionData = null;
    }

    /// <summary>
    /// 生成插件动作的下拉选项：先一个兜底项，再逐个具体贡献点。
    /// </summary>
    public static List<ActionTypeItem> BuildActionTypeItems()
    {
        var items = new List<ActionTypeItem>
        {
            // 兜底项：Tag 就是裸 TypeName，与 ProjectTag 的退化路径对应。
            new()
            {
                Tag = TypeName,
                DisplayText = "🔌 " + I18n.T("ActionTypePluginShort"),
            },
        };

        try
        {
            foreach (PluginActionRegistration registration in PluginHost.GetRegisteredActions())
            {
                items.Add(new ActionTypeItem
                {
                    Tag = BuildTag(registration.FullId),
                    DisplayText = "🔌 " + registration.DisplayName,
                    PluginRef = new PluginActionRef
                    {
                        PluginId = registration.PluginId,
                        ContributionId = registration.ShortId,
                    },
                });
            }
        }
        catch
        {
            // 本方法运行在 UI 数据绑定路径上（下拉框的 ItemsSource）。
            // 插件系统的任何异常都不允许冒泡到主界面 —— 否则一个坏插件能让整个
            // 「手势与动作」页打不开。这里兜底为「只有兜底项」，与插件系统关闭时表现一致。
        }

        return items;
    }

    /// <summary>
    /// 判断某个下拉项是否指向具体插件动作，是则返回其全 ID。
    /// </summary>
    public static bool TryGetFullIdFromItem(ActionTypeItem? item, out string fullId)
    {
        fullId = "";

        // 优先信 PluginRef（下拉项由本类生成时一定带着它），
        // 退化路径才去解 Tag —— 这样即使将来 Tag 格式变了也不会静默失配。
        if (item?.PluginRef is { IsValid: true } reference)
        {
            fullId = reference.FullId;
            return true;
        }

        return TryParseTag(item?.Tag, out fullId);
    }

    /// <summary>
    /// 名字是否属于「自动填充值」而非用户自定义。
    /// </summary>
    private static bool ShouldAutoFillName(string? name)
    {
        if (string.IsNullOrWhiteSpace(name)) return true;
        if (name == "快捷动作") return true;
        if (name.StartsWith("动作", StringComparison.Ordinal)) return true;
        if (name.StartsWith("快捷动作", StringComparison.Ordinal)) return true;
        if (name.StartsWith("子动作", StringComparison.Ordinal)) return true;

        // 上一次就是插件动作自动填的名字 —— 换动作时应当同步替换。
        // 这里查一遍注册表，代价是一次字典遍历（插件动作通常只有几个）。
        foreach (PluginActionRegistration registration in PluginHost.GetRegisteredActions())
        {
            if (string.Equals(registration.DisplayName, name, StringComparison.Ordinal))
            {
                return true;
            }
        }

        return false;
    }
}

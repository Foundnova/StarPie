namespace StarPie.Plugin;

/// <summary>
/// SDK 全局常量。这些值同时被宿主与插件使用，属于「契约的一部分」，任何修改都是破坏性变更。
/// </summary>
public static class PluginApi
{
    /// <summary>SDK 契约主版本。插件 manifest 的 <c>apiVersion</c> 主版本必须与之相等才能加载。</summary>
    public const int ApiVersionMajor = 1;

    /// <summary>SDK 契约次版本。只增不改的演进在此递增。</summary>
    public const int ApiVersionMinor = 0;

    /// <summary>SDK 契约版本字符串，形如 <c>1.0</c>。</summary>
    public const string ApiVersion = "1.0";

    /// <summary>本契约程序集的程序集名。宿主的 PluginLoadContext 依赖它做「共享程序集放行」。</summary>
    public const string AbstractionsAssemblyName = "StarPie.Plugin.Abstractions";

    /// <summary>
    /// 当前 plugin.json 清单结构版本。宿主不支持新版本清单时直接拒绝，并提示升级 StarPie。
    /// </summary>
    public const int ManifestSchemaVersion = 1;

    /// <summary>
    /// 插件动作在 <c>ActionItem.Type</c> 中统一使用这个值。
    /// 选它而不是复用内置 type，是为了让旧版主程序读到后走 switch 无匹配分支 → 静默无操作，而不是崩溃。
    /// </summary>
    public const string ActionTypeName = "Plugin";

    /// <summary>
    /// 顶层动作类型认领的<b>程序集元数据键</b>。
    /// <para>
    /// 宿主需要<b>在不加载程序集的前提下</b>知道「用户配置里的 <c>Type="Command"</c> 归哪个插件」——
    /// 这决定了启动时该预加载谁，也决定了轮盘触发时能不能避免一次几百毫秒的现场加载。
    /// 裸 DLL 的识别本来就走静态元数据读取，认领声明搭同一班车即可，代价是几微秒的字节读。
    /// </para>
    /// <para>
    /// 值形如 <c>"Command=command;Hotkey=hotkey;WebUrl=webUrl"</c> —— 分号分隔的
    /// <c>顶层类型名=插件内短 ID</c> 对。写成显式配对而不是只列类型名，
    /// 是为了不去猜「第几个类型对应第几个贡献点」这种顺序假设。
    /// </para>
    /// <para>
    /// <b>只有随包分发的插件可以认领</b>（见 <see cref="ReservedIdPrefixes"/> 的同一动机）：
    /// 否则任何第三方插件都能声明自己认领 <c>"Command"</c>，把用户配好的命令行扇区整体接管过去。
    /// </para>
    /// </summary>
    public const string TypeClaimsMetadataKey = "StarPiePluginTypeClaims";

    /// <summary>插件语言词条的完整 key 前缀，最终形如 <c>plugin.&lt;pluginId&gt;.&lt;key&gt;</c>。</summary>
    public const string I18nKeyPrefix = "plugin.";

    /// <summary>插件图标 key 的完整前缀，最终形如 <c>plugin:&lt;pluginId&gt;:&lt;key&gt;</c>。</summary>
    public const string IconKeyPrefix = "plugin:";

    /// <summary>禁止社区占用的 ID 前缀（保留给官方与系统）。</summary>
    public static readonly string[] ReservedIdPrefixes =
    {
        "starpie", "winpiegestures", "windows", "microsoft", "system", "builtin"
    };

    /// <summary>单个动作参数字符串值的最大长度（字符），防止插件把巨串塞进 config.json 造成 LOH 膨胀。</summary>
    public const int MaxParameterValueLength = 8 * 1024;
}

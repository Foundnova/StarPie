using System;
using System.IO;
using System.Text.RegularExpressions;

namespace WinPieGestures.Plugins;

/// <summary>
/// 插件系统所有磁盘路径的<b>唯一来源</b>。
/// <para>
/// 一旦有第二处代码用 <c>Path.Combine</c> 拼插件路径，迟早会出现「设置页看到的目录」与
/// 「加载器实际用的目录」不一致的幽灵问题。所有模块一律从这里取路径。
/// </para>
/// </summary>
internal static class PluginPaths
{
    private static string? _rootOverride;

    /// <summary>便携模式的标志文件名，放在主程序目录下即可切换到「程序目录\plugins」。</summary>
    public const string PortableFlagFileName = "portable.flag";

    /// <summary>插件清单文件名。这是插件识别的唯一入口。</summary>
    public const string ManifestFileName = "plugin.json";

    /// <summary>插件包扩展名（本质是 zip）。</summary>
    public const string PackageExtension = ".spkg";

    /// <summary>插件根目录（默认为 <c>%LOCALAPPDATA%\StarPie\plugins</c>）。</summary>
    public static string Root => _rootOverride ?? DefaultRoot;

    /// <summary>宿主运维数据：启用状态 / 版本 / 哈希 / 能力确认。</summary>
    public static string RegistryFile => Path.Combine(Root, "registry.json");

    /// <summary>宿主运维数据：失败计数 / 安全模式标记。</summary>
    public static string HealthFile => Path.Combine(Root, "health.json");

    /// <summary>插件日志目录（与主日志同根，按插件分文件）。</summary>
    public static string LogDirectory => Path.Combine(AppLogger.GetLogFolderPath(), "plugins");

    private static string DefaultRoot =>
        Path.Combine(
            string.IsNullOrEmpty(Environment.GetEnvironmentVariable("LOCALAPPDATA"))
                ? Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData)
                : Environment.GetEnvironmentVariable("LOCALAPPDATA")!,
            "StarPie",
            "plugins");

    private static string ProgramDirectoryPluginsRoot =>
        Path.Combine(AppContext.BaseDirectory, "plugins");

    /// <summary>主程序目录下是否存在 <c>portable.flag</c>。</summary>
    public static bool PortableFlagPresent
    {
        get
        {
            try { return File.Exists(Path.Combine(AppContext.BaseDirectory, PortableFlagFileName)); }
            catch { return false; }
        }
    }

    /// <summary>
    /// 解析并锁定插件根目录。必须在插件系统初始化时调用一次。
    /// </summary>
    /// <param name="portableRequested">用户配置里是否勾选了便携模式。</param>
    public static void Configure(bool portableRequested)
    {
        bool portable = portableRequested || PortableFlagPresent;
        _rootOverride = portable ? ProgramDirectoryPluginsRoot : DefaultRoot;
    }

    /// <summary>当前生效的根目录是否位于程序目录下（即便携模式）。</summary>
    public static bool IsPortable =>
        string.Equals(
            Path.GetFullPath(Root).TrimEnd(Path.DirectorySeparatorChar),
            Path.GetFullPath(ProgramDirectoryPluginsRoot).TrimEnd(Path.DirectorySeparatorChar),
            StringComparison.OrdinalIgnoreCase);

    public static string GetPluginDirectory(string pluginId) => Path.Combine(Root, pluginId);

    public static string GetDataDirectory(string pluginId) => Path.Combine(GetPluginDirectory(pluginId), "data");

    public static string GetManifestPath(string pluginDirectory) => Path.Combine(pluginDirectory, ManifestFileName);

    public static string GetSettingsPath(string pluginId) => Path.Combine(GetPluginDirectory(pluginId), "settings.json");

    public static string GetLogFilePath(string pluginId)
    {
        string safe = SanitizeForFileName(pluginId);
        return Path.Combine(LogDirectory, $"{safe}_{DateTime.Now:yyyy-MM-dd}.log");
    }

    /// <summary>插件 ID 合法性：反向域名风格，至少两级，全小写，允许数字与连字符。</summary>
    private static readonly Regex IdPattern = new(
        @"^[a-z0-9]([a-z0-9-]*[a-z0-9])?(\.[a-z0-9]([a-z0-9-]*[a-z0-9])?)+$",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    public static bool IsValidPluginId(string? pluginId) =>
        !string.IsNullOrWhiteSpace(pluginId) && IdPattern.IsMatch(pluginId);

    /// <summary>ID 是否占用了保留前缀（官方 / 系统命名空间）。</summary>
    public static bool IsReservedPluginId(string? pluginId)
    {
        if (string.IsNullOrWhiteSpace(pluginId)) return false;
        foreach (string prefix in StarPie.Plugin.PluginApi.ReservedIdPrefixes)
        {
            if (pluginId.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            {
                // 精确等于前缀本身（如 id = "system"）也算占用
                if (pluginId.Length == prefix.Length) return true;

                // 前缀后必须紧跟 . 或 - 才算占用命名空间，避免误伤 "windowshelper" 这类合法 ID
                char next = pluginId[prefix.Length];
                if (next == '.' || next == '-') return true;
            }
        }
        return false;
    }

    /// <summary>把任意字符串净化为合法文件名片段。</summary>
    public static string SanitizeForFileName(string raw)
    {
        if (string.IsNullOrEmpty(raw)) return "plugin";
        var chars = new char[raw.Length];
        int n = 0;
        foreach (char c in raw)
        {
            chars[n++] = (char.IsLetterOrDigit(c) || c == '.' || c == '-' || c == '_') ? c : '_';
        }
        return new string(chars, 0, n);
    }

    /// <summary>确保根目录与日志目录存在。失败时返回 false 而不抛异常。</summary>
    public static bool EnsureDirectories()
    {
        bool ok = true;
        try
        {
            if (!Directory.Exists(Root)) Directory.CreateDirectory(Root);
        }
        catch { ok = false; }

        try
        {
            if (!Directory.Exists(LogDirectory)) Directory.CreateDirectory(LogDirectory);
        }
        catch { ok = false; }

        return ok;
    }
}

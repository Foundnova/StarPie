using System.Collections.Generic;

namespace StarPie.Plugin.BasicActions;

/// <summary>
/// 本包的词条表。
/// <para>
/// <b>为什么要自带一份，而不是去读宿主的中文表</b>：插件拿到的 <see cref="II18nRegistry"/>
/// 只能注册与读取<b>自己命名空间下</b>的词条（<c>plugin.&lt;id&gt;.*</c>），读不到宿主的
/// <c>ActionTypeLaunchShort</c> 这类键 —— 这是刻意的隔离，插件不该依赖宿主内部键名。
/// 所以动作的显示名必须在插件里再声明一次。
/// </para>
/// <para>
/// <b>重复是有代价的</b>：类型下拉里的标签仍来自宿主词条，这里的显示名进入插件列表与
/// 参数表单。两处一旦不一致，用户会看到同一个动作有两个名字。
/// 这条靠自检兜住：<c>--plugin-selftest</c> 会逐项断言「认领的 Type 在宿主下拉里的文案」
/// 与「插件 Descriptor.DisplayName」相等。改词条时请两边一起改。
/// </para>
/// <para>
/// <b>覆盖范围</b>：标题与字段标签给了 zh-CN / zh-TW / en / ja 四套（这些是用户真正会读到的）；
/// 帮助文案与校验提示只给了 zh-CN / en，其余语言回落 zh-CN —— 与宿主内置面板的现状一致，
/// 是如实告知而不是遗漏。
/// </para>
/// </summary>
internal static class Texts
{
    // ---------------------------------------------------------------- zh-CN（兜底语言，也是各处的 fallback）

    internal const string LaunchTitle = "启动程序";
    internal const string LaunchPath = "程序路径";
    internal const string LaunchPathHelp = "可执行文件或快捷方式的完整路径，也支持 shell:AppsFolder 形式的应用。";
    internal const string LaunchArguments = "启动参数";
    internal const string LaunchStandardUser = "以常规普通权限启动";
    internal const string LaunchStandardUserHelp =
        "当 StarPie 以管理员权限运行时，通过 Windows Shell 降权启动目标程序，恢复文件拖拽交互支持。";
    internal const string LaunchEmpty = "未设置要启动的程序，请在动作设置里选择可执行文件。";

    internal const string WebUrlTitle = "打开网址";
    internal const string WebUrlUrl = "网址";
    internal const string WebUrlBrowser = "打开方式";
    internal const string WebUrlBrowserDefault = "系统默认";
    internal const string WebUrlBrowserCustom = "自定义浏览器...";
    internal const string WebUrlCustomPath = "自定义浏览器路径";
    internal const string WebUrlCustomPathHelp = "仅当「打开方式」选择「自定义浏览器...」时生效。";
    internal const string WebUrlEmpty = "未填写网址。";
    internal const string WebUrlCustomMissing = "已选择「自定义浏览器」，但未指定浏览器可执行文件路径。";

    internal const string FolderTitle = "打开文件夹";
    internal const string FolderPath = "文件夹路径";
    internal const string FolderPathHelp = "支持普通路径、文件路径（会定位并选中该文件）以及 shell: 命名空间。";
    internal const string FolderEmpty = "未设置文件夹路径，请在动作设置里选择要打开的目录。";

    internal const string CommandTitle = "运行命令";
    internal const string CommandDesc = "在指定的终端中执行一条命令行语句。";
    internal const string CommandLine = "命令行";
    internal const string CommandTerminalLabel = "终端";
    internal const string CommandEmpty = "未填写要执行的命令，请在动作设置里输入命令行内容。";

    internal const string ShellToolTitle = "系统与右键工具";
    internal const string ShellToolVerb = "功能标识";
    internal const string ShellToolVerbHelp = "填功能标识，例如 copy_path / lock_screen / run_as_admin。";
    internal const string ShellToolEmpty = "未选择系统工具，请指定一个功能标识。";

    // ---------------------------------------------------------------- 注册表

    /// <summary>zh-CN 与 en 两套。<see cref="II18nRegistry.Register"/> 一次接收这两种语言。</summary>
    internal static readonly (string Key, string ZhCn, string En)[] Base = new[]
    {
        ("launch.title", LaunchTitle, "Run App"),
        ("launch.path", LaunchPath, "Application path"),
        ("launch.pathHelp", LaunchPathHelp, "Full path to an executable or shortcut. shell:AppsFolder apps are also supported."),
        ("launch.arguments", LaunchArguments, "Arguments"),
        ("launch.standardUser", LaunchStandardUser, "Launch as standard user"),
        ("launch.standardUserHelp", LaunchStandardUserHelp,
            "When StarPie runs elevated, launch the target through the Windows shell token so file drag-and-drop keeps working."),
        ("launch.empty", LaunchEmpty, "No application selected. Please pick an executable in the action settings."),

        ("weburl.title", WebUrlTitle, "Open URL"),
        ("weburl.url", WebUrlUrl, "URL"),
        ("weburl.browser", WebUrlBrowser, "Open with"),
        ("weburl.browser.default", WebUrlBrowserDefault, "System default"),
        ("weburl.browser.custom", WebUrlBrowserCustom, "Custom browser..."),
        ("weburl.customPath", WebUrlCustomPath, "Custom browser path"),
        ("weburl.customPathHelp", WebUrlCustomPathHelp, "Only used when \"Open with\" is set to \"Custom browser...\"."),
        ("weburl.empty", WebUrlEmpty, "No URL entered."),
        ("weburl.customMissing", WebUrlCustomMissing, "A custom browser was selected, but no executable path was given."),

        ("folder.title", FolderTitle, "Open Folder"),
        ("folder.path", FolderPath, "Folder path"),
        ("folder.pathHelp", FolderPathHelp, "Plain paths, file paths (the containing folder opens with the file selected) and shell: namespaces are supported."),
        ("folder.empty", FolderEmpty, "No folder path set. Please choose a directory in the action settings."),

        ("command.title", CommandTitle, "Run Command"),
        ("command.desc", CommandDesc, "Run a command line in the selected terminal."),
        ("command.line", CommandLine, "Command line"),
        ("command.terminal", CommandTerminalLabel, "Terminal"),
        ("command.empty", CommandEmpty, "No command entered. Please fill in the command line in the action settings."),

        ("shell.tool.title", ShellToolTitle, "Shell & System Tools"),
        ("shell.tool.verb", ShellToolVerb, "Tool ID"),
        ("shell.tool.verbHelp", ShellToolVerbHelp, "Enter a tool ID such as copy_path, lock_screen or run_as_admin."),
        ("shell.tool.empty", ShellToolEmpty, "No system tool selected. Please specify a tool ID."),
    };

    // ---------------------------------------------------------------- 其余语言

    internal static readonly Dictionary<string, string> ZhTw = new()
    {
        ["launch.title"] = "啟動程式",
        ["launch.path"] = "程式路徑",
        ["launch.arguments"] = "啟動參數",
        ["launch.standardUser"] = "以一般使用者權限啟動",
        ["weburl.title"] = "開啟網址",
        ["weburl.url"] = "網址",
        ["weburl.browser"] = "開啟方式",
        ["weburl.browser.default"] = "系統預設",
        ["weburl.browser.custom"] = "自訂瀏覽器...",
        ["weburl.customPath"] = "自訂瀏覽器路徑",
        ["folder.title"] = "開啟資料夾",
        ["folder.path"] = "資料夾路徑",
        ["command.title"] = "執行命令",
        ["command.line"] = "命令列",
        ["command.terminal"] = "終端",
        ["shell.tool.title"] = "系統與右鍵工具",
        ["shell.tool.verb"] = "功能識別碼",
    };

    internal static readonly Dictionary<string, string> Ja = new()
    {
        ["launch.title"] = "アプリ起動",
        ["launch.path"] = "アプリのパス",
        ["launch.arguments"] = "起動引数",
        ["launch.standardUser"] = "標準ユーザーとして起動",
        ["weburl.title"] = "URLを開く",
        ["weburl.url"] = "URL",
        ["weburl.browser"] = "開く方法",
        ["weburl.browser.default"] = "システム既定",
        ["weburl.browser.custom"] = "カスタム ブラウザー...",
        ["weburl.customPath"] = "カスタム ブラウザーのパス",
        ["folder.title"] = "フォルダーを開く",
        ["folder.path"] = "フォルダーのパス",
        ["command.title"] = "コマンド実行",
        ["command.line"] = "コマンド",
        ["command.terminal"] = "ターミナル",
        ["shell.tool.title"] = "シェル・右クリックツール",
        ["shell.tool.verb"] = "ツール ID",
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

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace WinPieGestures;

public class ShellToolItem
{
	public string Id { get; set; } = "";
	public string Title { get; set; } = "";
	public string Provider { get; set; } = "";
	public string Category { get; set; } = ""; // "Compress", "System", "Developer"
	public string Icon { get; set; } = "";
	public string Verb { get; set; } = "";
	public string TargetType { get; set; } = "";
	public string Requirement { get; set; } = "";
	public string Description { get; set; } = "";
	public string DefaultIconKey { get; set; } = "";

	public string Name => Title;
	public string IconKey => DefaultIconKey;

	public bool IsAvailable { get; set; } = true;
	public string StatusBadge { get; set; } = "就绪";
	public string StatusBadgeType { get; set; } = "Ready"; // "Builtin", "Ready", "Missing"
}

public partial class ShellActionPickerWindow : Window
{
	public ShellToolItem? SelectedTool { get; private set; }

	private string _activeCategory = "All";
	private ShellToolItem? _currentSelection;

	public static IReadOnlyList<ShellToolItem> ShellTools => PredefinedShellTools;

	private static readonly List<ShellToolItem> PredefinedShellTools = new()
	{
		// 1. 系统与文件常用增强 (System & Explorer)
		new ShellToolItem
		{
			Id = "copy_path",
			Title = "复制文件/文件夹完整路径",
			Provider = "Windows 原生增强",
			Category = "System",
			Icon = "📋",
			Verb = "Windows.CopyAsPath",
			TargetType = "任意文件 / 文件夹",
			Requirement = "前台选中文件或当前目录",
			Description = "将资源管理器中当前选中对象或当前打开目录的完整绝对路径复制进系统剪贴板",
			DefaultIconKey = "Copy"
		},
		new ShellToolItem
		{
			Id = "copy_filename",
			Title = "复制文件名 (不带路径)",
			Provider = "Windows 原生增强",
			Category = "System",
			Icon = "📄",
			Verb = "Windows.CopyFileName",
			TargetType = "任意文件 / 文件夹",
			Requirement = "前台选中对象 (支持多选)",
			Description = "仅提取选中文件或文件夹的名称（多选时自动换行），方便引用或重命名",
			DefaultIconKey = "Copy"
		},
		new ShellToolItem
		{
			Id = "open_with_notepad",
			Title = "用记事本打开 (Notepad)",
			Provider = "Windows 原生增强",
			Category = "System",
			Icon = "📝",
			Verb = "Windows.OpenWithNotepad",
			TargetType = "任意文本 / 代码 / 日志文件",
			Requirement = "选中文件",
			Description = "无论文件扩展名，快速以 Windows 系统记事本直接打开查看和编辑",
			DefaultIconKey = "Code"
		},
		new ShellToolItem
		{
			Id = "open_with_default",
			Title = "以系统默认应用打开 (Shell Open)",
			Provider = "Windows 原生增强",
			Category = "System",
			Icon = "📂",
			Verb = "Windows.OpenWithDefault",
			TargetType = "任意文件",
			Requirement = "选中文件",
			Description = "模拟鼠标双击行为，以系统关联的默认程序快速打开选中的文件",
			DefaultIconKey = "Folder"
		},
		new ShellToolItem
		{
			Id = "builtin_ocr",
			Title = "StarPie 屏幕 OCR 快速识字",
			Provider = "Windows 10/11 WinRT 原生引擎",
			Category = "System",
			Icon = "🔍",
			Verb = "StarPie.Builtin.ScreenOCR",
			TargetType = "全屏幕任意区域",
			Requirement = "全局可用 (无需选文件)",
			Description = "快速唤起本地离线 OCR 识别引擎，鼠标拉框截取屏幕任意文字直接存入剪贴板",
			DefaultIconKey = "Screenshot"
		},
		new ShellToolItem
		{
			Id = "run_as_admin",
			Title = "以管理员身份运行 (Run as Admin)",
			Provider = "Windows 原生增强",
			Category = "System",
			Icon = "🛡️",
			Verb = "Windows.RunAs",
			TargetType = "可执行程序 / 脚本",
			Requirement = "选中 exe / bat / cmd / ps1",
			Description = "以特权 UAC 提示唤起选中的程序或命令脚本",
			DefaultIconKey = "Command"
		},
		new ShellToolItem
		{
			Id = "task_manager",
			Title = "打开 Windows 任务管理器",
			Provider = "Windows 系统工具",
			Category = "System",
			Icon = "📊",
			Verb = "Windows.TaskManager",
			TargetType = "系统级",
			Requirement = "全局可用",
			Description = "瞬间呼出 Windows 任务管理器查看 CPU、内存占用与后台进程状态",
			DefaultIconKey = "TaskManager"
		},
		new ShellToolItem
		{
			Id = "snipping_tool",
			Title = "Windows 原生截屏 (Win+Shift+S)",
			Provider = "Windows 系统工具",
			Category = "System",
			Icon = "✂️",
			Verb = "Windows.SnippingTool",
			TargetType = "系统级",
			Requirement = "全局可用",
			Description = "唤起 Windows 原生 Snipping Tool 进行自定义区域、窗口或全屏截屏",
			DefaultIconKey = "Screenshot"
		},
		new ShellToolItem
		{
			Id = "new_folder",
			Title = "新建文件夹 (Ctrl+Shift+N)",
			Provider = "资源管理器工具",
			Category = "System",
			Icon = "📁",
			Verb = "Windows.NewFolder",
			TargetType = "目录空白处",
			Requirement = "资源管理器窗口",
			Description = "在当前活跃的资源管理器窗口中就地创建新文件夹",
			DefaultIconKey = "Folder"
		},
		new ShellToolItem
		{
			Id = "file_properties",
			Title = "查看文件/文件夹属性 (Alt+Enter)",
			Provider = "资源管理器工具",
			Category = "System",
			Icon = "ℹ️",
			Verb = "Windows.Properties",
			TargetType = "文件或文件夹",
			Requirement = "选中文件或文件夹",
			Description = "直接打开当前选中对象的 Windows 系统属性对话框",
			DefaultIconKey = "Settings"
		},
		new ShellToolItem
		{
			Id = "classic_context_menu",
			Title = "展开 Win11 完整经典右键菜单 (Shift+F10)",
			Provider = "Windows 11 增强",
			Category = "System",
			Icon = "📑",
			Verb = "Windows.ClassicContextMenu",
			TargetType = "文件 / 目录 / 桌面",
			Requirement = "资源管理器或桌面",
			Description = "跳过 Windows 11 折叠的二级菜单，直接就地呼出全量经典右键菜单",
			DefaultIconKey = "Settings"
		},
		new ShellToolItem
		{
			Id = "send_to_desktop",
			Title = "发送到桌面快捷方式",
			Provider = "Windows 原生增强",
			Category = "System",
			Icon = "🖥️",
			Verb = "Windows.SendToDesktop",
			TargetType = "任意文件 / 文件夹",
			Requirement = "选中文件或文件夹",
			Description = "一键为当前选中的文件或文件夹在桌面上快速生成快捷方式 (.lnk)",
			DefaultIconKey = "ShowDesktop"
		},
		new ShellToolItem
		{
			Id = "compute_sha256",
			Title = "计算文件 SHA-256 哈希校验值",
			Provider = "Windows 安全校验",
			Category = "System",
			Icon = "🔒",
			Verb = "Windows.ComputeSha256",
			TargetType = "文件",
			Requirement = "选中文件",
			Description = "快速计算选中文件的 SHA-256 哈希校验码，自动复制到剪贴板并提示结果",
			DefaultIconKey = "Lock"
		},
		new ShellToolItem
		{
			Id = "compute_md5",
			Title = "计算文件 MD5 哈希校验值",
			Provider = "Windows 安全校验",
			Category = "System",
			Icon = "🔑",
			Verb = "Windows.ComputeMd5",
			TargetType = "文件",
			Requirement = "选中文件",
			Description = "快速计算选中文件的 MD5 哈希校验值，自动写入剪贴板便于对比核验",
			DefaultIconKey = "Lock"
		},
		new ShellToolItem
		{
			Id = "toggle_hidden",
			Title = "切换选中项隐藏/可见属性",
			Provider = "Windows 文件系统",
			Category = "System",
			Icon = "👁️",
			Verb = "Windows.ToggleHidden",
			TargetType = "文件 / 文件夹",
			Requirement = "选中对象",
			Description = "快速切换选中对象的 Hidden 文件隐藏属性，便于管理私密文件",
			DefaultIconKey = "Settings"
		},
		new ShellToolItem
		{
			Id = "permanent_delete",
			Title = "永久删除 (Shift+Delete)",
			Provider = "Windows 文件系统",
			Category = "System",
			Icon = "💥",
			Verb = "Windows.PermanentDelete",
			TargetType = "文件 / 文件夹",
			Requirement = "选中对象",
			Description = "跳过系统回收站彻底永久删除选中项（附带系统原生确认提示）",
			DefaultIconKey = "Delete"
		},
		new ShellToolItem
		{
			Id = "empty_recycle_bin",
			Title = "清空桌面回收站",
			Provider = "Windows 原生增强",
			Category = "System",
			Icon = "🗑️",
			Verb = "Windows.EmptyRecycleBin",
			TargetType = "系统级",
			Requirement = "全局可用",
			Description = "一键彻底清空桌面回收站中所有已删除的项目，释放磁盘存储空间",
			DefaultIconKey = "Delete"
		},
		new ShellToolItem
		{
			Id = "lock_screen",
			Title = "快速锁定电脑屏幕 (Win+L)",
			Provider = "Windows 系统安全",
			Category = "System",
			Icon = "🔒",
			Verb = "Windows.Lock",
			TargetType = "系统级",
			Requirement = "全局可用",
			Description = "立即锁定当前 Windows 桌面会话，保护个人隐私",
			DefaultIconKey = "Lock"
		},

		// 2. 压缩与解压缩扩展 (Compress & Extract)
		// --- 7-Zip ---
		new ShellToolItem
		{
			Id = "7z_extract_here",
			Title = "7-Zip: 解压到当前位置 (Extract Here)",
			Provider = "7-Zip Shell Extension",
			Category = "Compress",
			Icon = "📦",
			Verb = "7-Zip.ExtractHere",
			TargetType = "压缩包 (*.zip, *.7z, *.rar, *.tar...)",
			Requirement = "选中压缩包文件",
			Description = "在当前所在目录下就地提取解压选中的压缩包文件",
			DefaultIconKey = "Folder"
		},
		new ShellToolItem
		{
			Id = "7z_extract_folder",
			Title = "7-Zip: 解压到独立同名子文件夹",
			Provider = "7-Zip Shell Extension",
			Category = "Compress",
			Icon = "🗂️",
			Verb = "7-Zip.ExtractToFolder",
			TargetType = "压缩包文件",
			Requirement = "选中压缩包文件",
			Description = "以压缩包名称自动新建同名独立子目录并解压其全部文件",
			DefaultIconKey = "Folder"
		},
		new ShellToolItem
		{
			Id = "7z_compress_zip",
			Title = "7-Zip: 压缩为同名 .zip",
			Provider = "7-Zip Shell Extension",
			Category = "Compress",
			Icon = "🗜️",
			Verb = "7-Zip.CompressZip",
			TargetType = "文件 / 文件夹 (支持多选)",
			Requirement = "选中要压缩的文件或目录",
			Description = "调用 7-Zip 将选中的文件或文件夹极速压缩为通用的同名 .zip 压缩包",
			DefaultIconKey = "Folder"
		},
		new ShellToolItem
		{
			Id = "7z_compress_7z",
			Title = "7-Zip: 压缩为同名 .7z",
			Provider = "7-Zip Shell Extension",
			Category = "Compress",
			Icon = "📦",
			Verb = "7-Zip.Compress7z",
			TargetType = "文件 / 文件夹 (支持多选)",
			Requirement = "选中要压缩的文件或目录",
			Description = "调用 7-Zip 将选中的文件或文件夹以高压缩率打包为同名 .7z 归档",
			DefaultIconKey = "Folder"
		},
		new ShellToolItem
		{
			Id = "7z_compress_gui",
			Title = "7-Zip: 添加到压缩包... (配置窗口)",
			Provider = "7-Zip Shell Extension",
			Category = "Compress",
			Icon = "⚙️",
			Verb = "7-Zip.CompressGui",
			TargetType = "文件 / 文件夹",
			Requirement = "选中要压缩的文件或目录",
			Description = "唤起 7-Zip 图形化压缩配置对话框，自定义格式、加密密码与分卷设置",
			DefaultIconKey = "Settings"
		},

		// --- Bandizip ---
		new ShellToolItem
		{
			Id = "bandizip_extract",
			Title = "Bandizip: 智能自动解压",
			Provider = "Bandizip Shell Extension",
			Category = "Compress",
			Icon = "⚡",
			Verb = "Bandizip.AutoExtract",
			TargetType = "压缩包文件",
			Requirement = "选中压缩包文件",
			Description = "智能判断结构：单文件包就地解压，多文件包自动新建同名目录归类",
			DefaultIconKey = "Folder"
		},
		new ShellToolItem
		{
			Id = "bandizip_compress_zip",
			Title = "Bandizip: 压缩为同名 .zip",
			Provider = "Bandizip Shell Extension",
			Category = "Compress",
			Icon = "🗜️",
			Verb = "Bandizip.CompressZip",
			TargetType = "文件 / 文件夹 (支持多选)",
			Requirement = "选中要压缩的文件或目录",
			Description = "调用 Bandizip 快速将选中的项目打包为同名 .zip 压缩文件",
			DefaultIconKey = "Folder"
		},
		new ShellToolItem
		{
			Id = "bandizip_compress_7z",
			Title = "Bandizip: 压缩为同名 .7z",
			Provider = "Bandizip Shell Extension",
			Category = "Compress",
			Icon = "📦",
			Verb = "Bandizip.Compress7z",
			TargetType = "文件 / 文件夹 (支持多选)",
			Requirement = "选中要压缩的文件或目录",
			Description = "调用 Bandizip 快速将选中的项目打包为同名 .7z 高压归档",
			DefaultIconKey = "Folder"
		},
		new ShellToolItem
		{
			Id = "bandizip_compress_gui",
			Title = "Bandizip: 添加到压缩包... (配置窗口)",
			Provider = "Bandizip Shell Extension",
			Category = "Compress",
			Icon = "⚙️",
			Verb = "Bandizip.CompressGui",
			TargetType = "文件 / 文件夹",
			Requirement = "选中要压缩的文件或目录",
			Description = "呼出 Bandizip 新建压缩文件图形对话框，支持设置密码、分卷和压缩级别",
			DefaultIconKey = "Settings"
		},

		// --- WinRAR ---
		new ShellToolItem
		{
			Id = "winrar_extract",
			Title = "WinRAR: 解压到当前文件夹",
			Provider = "WinRAR Shell Extension",
			Category = "Compress",
			Icon = "📚",
			Verb = "WinRAR.ExtractHere",
			TargetType = "压缩包文件",
			Requirement = "选中压缩包文件",
			Description = "调用 WinRAR 将选中的压缩文件就地解压至当前所在目录",
			DefaultIconKey = "Folder"
		},
		new ShellToolItem
		{
			Id = "winrar_compress_rar",
			Title = "WinRAR: 压缩为同名 .rar",
			Provider = "WinRAR Shell Extension",
			Category = "Compress",
			Icon = "📚",
			Verb = "WinRAR.CompressRar",
			TargetType = "文件 / 文件夹 (支持多选)",
			Requirement = "选中要压缩的文件或目录",
			Description = "调用 WinRAR 将选中的项目压缩为经典的同名 .rar 归档文件",
			DefaultIconKey = "Folder"
		},
		new ShellToolItem
		{
			Id = "winrar_compress_zip",
			Title = "WinRAR: 压缩为同名 .zip",
			Provider = "WinRAR Shell Extension",
			Category = "Compress",
			Icon = "🗜️",
			Verb = "WinRAR.CompressZip",
			TargetType = "文件 / 文件夹 (支持多选)",
			Requirement = "选中要压缩的文件或目录",
			Description = "调用 WinRAR 将选中的项目压缩为标准的同名 .zip 压缩包",
			DefaultIconKey = "Folder"
		},
		new ShellToolItem
		{
			Id = "winrar_compress_gui",
			Title = "WinRAR: 添加到压缩文件... (配置窗口)",
			Provider = "WinRAR Shell Extension",
			Category = "Compress",
			Icon = "⚙️",
			Verb = "WinRAR.CompressGui",
			TargetType = "文件 / 文件夹",
			Requirement = "选中要压缩的文件或目录",
			Description = "打开 WinRAR 压缩文件参数设置界面，可配置锁定、恢复记录及密码",
			DefaultIconKey = "Settings"
		},

		// --- Windows 原生免装 ---
		new ShellToolItem
		{
			Id = "windows_compress_zip",
			Title = "Windows 原生: 压缩为 ZIP 文件 (免装第三方)",
			Provider = "Windows 原生引擎",
			Category = "Compress",
			Icon = "📦",
			Verb = "Windows.CompressZip",
			TargetType = "文件 / 文件夹 (支持多选)",
			Requirement = "选中任意文件或目录",
			Description = "纯基于 Windows 内置压缩引擎，无需安装任何 7-Zip 或 Bandizip 即可一键打包",
			DefaultIconKey = "Folder"
		},
		new ShellToolItem
		{
			Id = "windows_extract",
			Title = "Windows 原生: 全部解压缩 (免装第三方)",
			Provider = "Windows 原生引擎",
			Category = "Compress",
			Icon = "📂",
			Verb = "Windows.ExtractHere",
			TargetType = "ZIP 压缩包",
			Requirement = "选中 ZIP 压缩包",
			Description = "纯基于 Windows 内置引擎，就地解压所选压缩包到同名子目录",
			DefaultIconKey = "Folder"
		},

		// 3. 开发者与高效办公 (Developer & Power Tools)
		new ShellToolItem
		{
			Id = "vscode_open",
			Title = "通过 Visual Studio Code 打开",
			Provider = "VS Code Shell Extension",
			Category = "Developer",
			Icon = "💻",
			Verb = "VSCode.Open",
			TargetType = "文件 / 文件夹",
			Requirement = "选中对象或在目录内",
			Description = "将当前选中文件或当前打开的文件夹直接加载进 Visual Studio Code 编辑器",
			DefaultIconKey = "Code"
		},
		new ShellToolItem
		{
			Id = "git_bash_here",
			Title = "Git Bash Here (在此处打开 Git 终端)",
			Provider = "Git for Windows",
			Category = "Developer",
			Icon = "🐙",
			Verb = "Git.BashHere",
			TargetType = "目录 / 桌面空白处",
			Requirement = "在文件夹内或桌面",
			Description = "在当前所在的路径直接唤起 Git Bash 终端命令行环境",
			DefaultIconKey = "Terminal"
		},
		new ShellToolItem
		{
			Id = "windows_terminal",
			Title = "在当前目录打开 Windows Terminal",
			Provider = "Windows 终端",
			Category = "Developer",
			Icon = "🖥️",
			Verb = "Windows.Terminal",
			TargetType = "目录 / 桌面空白处",
			Requirement = "在文件夹内或桌面",
			Description = "在当前所在路径启动 Windows Terminal 现代化多标签终端",
			DefaultIconKey = "Terminal"
		},
		new ShellToolItem
		{
			Id = "cmd_here",
			Title = "在当前目录打开命令提示符 (CMD)",
			Provider = "Windows 原生终端",
			Category = "Developer",
			Icon = "📟",
			Verb = "Windows.CmdHere",
			TargetType = "目录 / 桌面空白处",
			Requirement = "在文件夹内或桌面",
			Description = "在当前打开的路径就地唤起 cmd.exe 命令行窗口",
			DefaultIconKey = "Command"
		},
		new ShellToolItem
		{
			Id = "powershell_here",
			Title = "在当前目录打开 PowerShell",
			Provider = "PowerShell",
			Category = "Developer",
			Icon = "⚡",
			Verb = "Windows.PowerShellHere",
			TargetType = "目录 / 桌面空白处",
			Requirement = "在文件夹内或桌面",
			Description = "在当前所在路径直接唤起 PowerShell 脚本终端环境",
			DefaultIconKey = "Command"
		}
	};

	public ShellActionPickerWindow(string? currentVerb = null)
	{
		InitializeComponent();
		AppThemeManager.ApplyTheme(this, ConfigManager.CurrentConfig?.AppTheme ?? "System");

		// 动态检测系统环境与已安装工具状态
		DetectToolAvailability();

		if (!string.IsNullOrEmpty(currentVerb))
		{
			_currentSelection = PredefinedShellTools.FirstOrDefault(t =>
				string.Equals(t.Verb, currentVerb, StringComparison.OrdinalIgnoreCase) ||
				string.Equals(t.Id, currentVerb, StringComparison.OrdinalIgnoreCase));
		}

		UpdateCategoryButtonsUi();
		RefreshActionItemsList();
	}

	private static void DetectToolAvailability()
	{
		bool has7z = ActionExecutor.Find7ZipExecutable() != null;
		bool hasBandizip = ActionExecutor.FindBandizipExecutable() != null;
		bool hasWinRar = ActionExecutor.FindWinRarExecutable() != null;
		bool hasVsCode = ActionExecutor.FindExecutableInPath("code") != null ||
		                 ActionExecutor.FindExecutableInPath("code.cmd") != null;
		bool hasGit = ActionExecutor.FindExecutableInPath("git-bash.exe") != null ||
		              File.Exists(@"C:\Program Files\Git\git-bash.exe") ||
		              File.Exists(@"C:\Program Files (x86)\Git\git-bash.exe") ||
		              File.Exists(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), @"Programs\Git\git-bash.exe"));
		bool hasWt = ActionExecutor.FindExecutableInPath("wt.exe") != null ||
		             File.Exists(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), @"Microsoft\WindowsApps\wt.exe"));

		foreach (var tool in PredefinedShellTools)
		{
			if (tool.Id.StartsWith("7z_") || tool.Provider.Contains("7-Zip"))
			{
				tool.IsAvailable = has7z;
				tool.StatusBadge = has7z ? "🟢 已就绪" : "⚪ 未安装";
				tool.StatusBadgeType = has7z ? "Ready" : "Missing";
			}
			else if (tool.Id.StartsWith("bandizip_") || tool.Provider.Contains("Bandizip"))
			{
				tool.IsAvailable = hasBandizip;
				tool.StatusBadge = hasBandizip ? "🟢 已就绪" : "⚪ 未安装";
				tool.StatusBadgeType = hasBandizip ? "Ready" : "Missing";
			}
			else if (tool.Id.StartsWith("winrar_") || tool.Provider.Contains("WinRAR"))
			{
				tool.IsAvailable = hasWinRar;
				tool.StatusBadge = hasWinRar ? "🟢 已就绪" : "⚪ 未安装";
				tool.StatusBadgeType = hasWinRar ? "Ready" : "Missing";
			}
			else if (tool.Id.StartsWith("vscode_") || tool.Provider.Contains("VS Code"))
			{
				tool.IsAvailable = hasVsCode;
				tool.StatusBadge = hasVsCode ? "🟢 已就绪" : "⚪ 未安装";
				tool.StatusBadgeType = hasVsCode ? "Ready" : "Missing";
			}
			else if (tool.Id.StartsWith("git_") || tool.Provider.Contains("Git"))
			{
				tool.IsAvailable = hasGit;
				tool.StatusBadge = hasGit ? "🟢 已就绪" : "⚪ 未安装";
				tool.StatusBadgeType = hasGit ? "Ready" : "Missing";
			}
			else if (tool.Id.StartsWith("windows_terminal"))
			{
				tool.IsAvailable = hasWt;
				tool.StatusBadge = hasWt ? "🟢 已就绪" : "⚪ 未安装";
				tool.StatusBadgeType = hasWt ? "Ready" : "Missing";
			}
			else
			{
				// Windows 原生增强 / 系统内置
				tool.IsAvailable = true;
				tool.StatusBadge = "⚡ 内置";
				tool.StatusBadgeType = "Builtin";
			}
		}
	}

	private void SearchTextBox_TextChanged(object sender, TextChangedEventArgs e)
	{
		string query = SearchTextBox.Text.Trim();
		ClearSearchBtn.Visibility = string.IsNullOrEmpty(query) ? Visibility.Collapsed : Visibility.Visible;
		RefreshActionItemsList();
	}

	private void ClearSearchBtn_Click(object sender, RoutedEventArgs e)
	{
		SearchTextBox.Text = "";
		SearchTextBox.Focus();
	}

	private void CatBtn_Click(object sender, RoutedEventArgs e)
	{
		if (sender is Button btn && btn.Tag is string cat)
		{
			_activeCategory = cat;
			UpdateCategoryButtonsUi();
			RefreshActionItemsList();
		}
	}

	private void UpdateCategoryButtonsUi()
	{
		Button[] buttons = new[] { CatAllBtn, CatCompressBtn, CatSystemBtn, CatDevBtn };
		foreach (Button b in buttons)
		{
			if (b == null) continue;
			bool isActive = string.Equals(b.Tag?.ToString(), _activeCategory, StringComparison.OrdinalIgnoreCase);
			if (isActive)
			{
				b.Background = (Brush)FindResource("AccentPrimaryBrush");
				b.Foreground = (Brush)FindResource("AccentTextBrush");
				b.BorderBrush = (Brush)FindResource("AccentHoverBrush");
			}
			else
			{
				b.Background = (Brush)FindResource("SubtleCardBrush");
				b.Foreground = (Brush)FindResource("TextSecondaryBrush");
				b.BorderBrush = (Brush)FindResource("CardBorderBrush");
			}
		}
	}

	private void RefreshActionItemsList()
	{
		ActionItemsPanel.Children.Clear();
		string keyword = SearchTextBox.Text.Trim().ToLowerInvariant();

		var filtered = PredefinedShellTools.Where(item =>
		{
			bool matchCat = _activeCategory == "All" || string.Equals(item.Category, _activeCategory, StringComparison.OrdinalIgnoreCase);
			bool matchKey = string.IsNullOrEmpty(keyword) ||
							item.Title.ToLowerInvariant().Contains(keyword) ||
							item.Provider.ToLowerInvariant().Contains(keyword) ||
							item.Verb.ToLowerInvariant().Contains(keyword) ||
							item.Description.ToLowerInvariant().Contains(keyword);
			return matchCat && matchKey;
		}).ToList();

		// 动态排序：已就绪与系统内置工具优先展示，未安装的排在后面
		var sorted = filtered
			.OrderByDescending(item => item.IsAvailable)
			.ThenByDescending(item => item.StatusBadgeType == "Ready")
			.ToList();

		if (sorted.Count == 0)
		{
			TextBlock emptyLabel = new TextBlock
			{
				Text = "未找到匹配的右键或系统扩展功能，试试搜索 压缩、7-Zip、路径、OCR 或 终端",
				Foreground = (Brush)FindResource("TextSecondaryBrush"),
				FontSize = 12,
				HorizontalAlignment = HorizontalAlignment.Center,
				Margin = new Thickness(0, 40, 0, 40)
			};
			ActionItemsPanel.Children.Add(emptyLabel);
			return;
		}

		foreach (ShellToolItem tool in sorted)
		{
			ActionItemsPanel.Children.Add(CreateActionCard(tool));
		}
	}

	private FrameworkElement CreateActionCard(ShellToolItem item)
	{
		bool isSelected = _currentSelection != null && string.Equals(_currentSelection.Id, item.Id, StringComparison.OrdinalIgnoreCase);

		Border card = new Border
		{
			CornerRadius = new CornerRadius(8),
			BorderThickness = new Thickness(isSelected ? 1.5 : 1),
			BorderBrush = isSelected ? (Brush)FindResource("AccentPrimaryBrush") : (Brush)FindResource("CardBorderBrush"),
			Background = isSelected ? (Brush)FindResource("SubtleCardBrush") : Brushes.Transparent,
			Padding = new Thickness(10, 8, 10, 8),
			Margin = new Thickness(0, 0, 0, 6),
			Cursor = Cursors.Hand,
			Tag = item
		};

		Grid grid = new Grid();
		grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(42) });
		grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
		grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

		// Icon
		Border iconBorder = new Border
		{
			Width = 34,
			Height = 34,
			CornerRadius = new CornerRadius(8),
			Background = (Brush)FindResource("SubtleCardBrush"),
			BorderBrush = (Brush)FindResource("CardBorderBrush"),
			BorderThickness = new Thickness(1),
			HorizontalAlignment = HorizontalAlignment.Left,
			VerticalAlignment = VerticalAlignment.Center
		};

		string svg = !string.IsNullOrEmpty(item.DefaultIconKey) ? IconHelper.GetSvgPathByKey(item.DefaultIconKey) : "";
		if (!string.IsNullOrEmpty(svg))
		{
			try
			{
				var path = new System.Windows.Shapes.Path
				{
					Data = Geometry.Parse(svg),
					Fill = (Brush)FindResource("AccentPrimaryBrush"),
					Width = 16,
					Height = 16,
					Stretch = Stretch.Uniform,
					HorizontalAlignment = HorizontalAlignment.Center,
					VerticalAlignment = VerticalAlignment.Center
				};
				iconBorder.Child = path;
			}
			catch
			{
				svg = "";
			}
		}

		if (string.IsNullOrEmpty(svg))
		{
			TextBlock iconText = new TextBlock
			{
				Text = item.Icon,
				FontSize = 18,
				FontFamily = new FontFamily("Segoe UI Emoji, Segoe UI Symbol, Segoe UI"),
				Foreground = (Brush)FindResource("TextPrimaryBrush"),
				HorizontalAlignment = HorizontalAlignment.Center,
				VerticalAlignment = VerticalAlignment.Center
			};
			iconBorder.Child = iconText;
		}

		Grid.SetColumn(iconBorder, 0);
		grid.Children.Add(iconBorder);

		// Middle info
		StackPanel infoPanel = new StackPanel
		{
			VerticalAlignment = VerticalAlignment.Center,
			Margin = new Thickness(4, 0, 8, 0)
		};

		StackPanel titleRow = new StackPanel { Orientation = Orientation.Horizontal };
		TextBlock titleText = new TextBlock
		{
			Text = item.Title,
			FontSize = 12.5,
			FontWeight = FontWeights.SemiBold,
			Foreground = (Brush)FindResource("TextPrimaryBrush"),
			VerticalAlignment = VerticalAlignment.Center
		};
		titleRow.Children.Add(titleText);

		// Provider Badge
		Border providerBadge = new Border
		{
			CornerRadius = new CornerRadius(4),
			Background = (Brush)FindResource("SubtleCardBrush"),
			BorderBrush = (Brush)FindResource("CardBorderBrush"),
			BorderThickness = new Thickness(1),
			Padding = new Thickness(6, 1, 6, 1),
			Margin = new Thickness(8, 0, 0, 0),
			VerticalAlignment = VerticalAlignment.Center
		};
		TextBlock providerText = new TextBlock
		{
			Text = item.Provider,
			FontSize = 10,
			Foreground = (Brush)FindResource("TextSecondaryBrush")
		};
		providerBadge.Child = providerText;
		titleRow.Children.Add(providerBadge);

		// Dynamic Status Badge (已就绪 / 系统内置 / 未安装)
		Border statusBadge = new Border
		{
			CornerRadius = new CornerRadius(4),
			BorderThickness = new Thickness(1),
			Padding = new Thickness(6, 1, 6, 1),
			Margin = new Thickness(6, 0, 0, 0),
			VerticalAlignment = VerticalAlignment.Center
		};
		TextBlock statusText = new TextBlock
		{
			Text = item.StatusBadge,
			FontSize = 10,
			FontWeight = FontWeights.Medium
		};

		if (item.StatusBadgeType == "Ready")
		{
			statusBadge.Background = new SolidColorBrush(Color.FromArgb(28, 46, 204, 113));
			statusBadge.BorderBrush = new SolidColorBrush(Color.FromArgb(90, 46, 204, 113));
			statusText.Foreground = new SolidColorBrush(Color.FromArgb(255, 46, 204, 113));
		}
		else if (item.StatusBadgeType == "Builtin")
		{
			statusBadge.Background = (Brush)FindResource("SubtleCardBrush");
			statusBadge.BorderBrush = (Brush)FindResource("CardBorderBrush");
			statusText.Foreground = (Brush)FindResource("AccentPrimaryBrush");
		}
		else
		{
			statusBadge.Background = (Brush)FindResource("SubtleCardBrush");
			statusBadge.BorderBrush = (Brush)FindResource("CardBorderBrush");
			statusText.Foreground = (Brush)FindResource("TextMutedBrush");
		}
		statusBadge.Child = statusText;
		titleRow.Children.Add(statusBadge);

		infoPanel.Children.Add(titleRow);

		TextBlock descText = new TextBlock
		{
			Text = item.Description,
			FontSize = 11,
			Foreground = (Brush)FindResource("TextSecondaryBrush"),
			Margin = new Thickness(0, 3, 0, 0),
			TextTrimming = TextTrimming.CharacterEllipsis
		};
		infoPanel.Children.Add(descText);

		Grid.SetColumn(infoPanel, 1);
		grid.Children.Add(infoPanel);

		// Right requirement + select button
		StackPanel rightPanel = new StackPanel
		{
			Orientation = Orientation.Horizontal,
			VerticalAlignment = VerticalAlignment.Center
		};

		TextBlock reqText = new TextBlock
		{
			Text = item.Requirement,
			FontSize = 10.5,
			Foreground = (Brush)FindResource("TextMutedBrush"),
			VerticalAlignment = VerticalAlignment.Center,
			Margin = new Thickness(0, 0, 10, 0)
		};
		rightPanel.Children.Add(reqText);

		Button selectBtn = new Button
		{
			Content = isSelected ? "✓ 已选" : "选择",
			Style = isSelected ? (Style)FindResource("PrimaryButtonStyle") : (Style)FindResource("ModernButtonStyle"),
			Height = 26,
			Padding = new Thickness(10, 0, 10, 0),
			FontSize = 11,
			Tag = item
		};
		selectBtn.Click += (s, e) =>
		{
			e.Handled = true;
			SelectTool(item);
		};
		rightPanel.Children.Add(selectBtn);

		Grid.SetColumn(rightPanel, 2);
		grid.Children.Add(rightPanel);

		card.Child = grid;

		// Mouse interactions
		card.MouseEnter += (s, e) =>
		{
			if (_currentSelection?.Id != item.Id)
			{
				card.Background = (Brush)FindResource("ButtonHoverBgBrush");
			}
		};
		card.MouseLeave += (s, e) =>
		{
			if (_currentSelection?.Id != item.Id)
			{
				card.Background = Brushes.Transparent;
			}
		};
		card.MouseLeftButtonDown += (s, e) =>
		{
			SelectTool(item);
			if (e.ClickCount == 2)
			{
				ConfirmBtn_Click(this, new RoutedEventArgs());
			}
		};

		return card;
	}

	private void SelectTool(ShellToolItem tool)
	{
		_currentSelection = tool;
		if (!tool.IsAvailable)
		{
			SelectedItemLabel.Text = $"{tool.Icon} {tool.Title} ({tool.Provider}) - ⚠️ 本机尚未检测到该工具，请确保已安装";
		}
		else
		{
			SelectedItemLabel.Text = $"{tool.Icon} {tool.Title} ({tool.Provider}) - {tool.StatusBadge}";
		}
		ConfirmBtn.IsEnabled = true;
		RefreshActionItemsList();
	}

	private void ConfirmBtn_Click(object sender, RoutedEventArgs e)
	{
		if (_currentSelection == null) return;
		SelectedTool = _currentSelection;
		DialogResult = true;
		Close();
	}

	private void CancelBtn_Click(object sender, RoutedEventArgs e)
	{
		DialogResult = false;
		Close();
	}
}

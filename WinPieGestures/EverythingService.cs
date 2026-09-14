using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Win32;

namespace WinPieGestures;

public static class EverythingService
{
	private static class Native
	{
		[DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
		public static extern IntPtr FindWindow(string? lpClassName, string? lpWindowName);

		[DllImport("Everything64.dll", CharSet = CharSet.Unicode)]
		public static extern uint Everything_SetSearchW(string lpSearchString);

		[DllImport("Everything64.dll")]
		public static extern void Everything_SetMatchPath(bool bEnable);

		[DllImport("Everything64.dll")]
		public static extern void Everything_SetMatchCase(bool bEnable);

		[DllImport("Everything64.dll")]
		public static extern void Everything_SetMatchWholeWord(bool bEnable);

		[DllImport("Everything64.dll")]
		public static extern void Everything_SetRegex(bool bEnable);

		[DllImport("Everything64.dll")]
		public static extern void Everything_SetMax(uint dwMax);

		[DllImport("Everything64.dll")]
		public static extern void Everything_SetOffset(uint dwOffset);

		[DllImport("Everything64.dll")]
		public static extern void Everything_SetRequestFlags(uint dwRequestFlags);

		[DllImport("Everything64.dll")]
		public static extern void Everything_SetSort(uint dwSortType);

		[DllImport("Everything64.dll")]
		public static extern bool Everything_QueryW(bool bWait);

		[DllImport("Everything64.dll")]
		public static extern uint Everything_GetNumResults();

		[DllImport("Everything64.dll", CharSet = CharSet.Unicode)]
		public static extern void Everything_GetResultFullPathNameW(uint nIndex, StringBuilder lpString, uint nMaxCount);

		[DllImport("Everything64.dll")]
		public static extern bool Everything_GetResultSize(uint nIndex, out long lpFileSize);

		[DllImport("Everything64.dll")]
		public static extern bool Everything_GetResultDateModified(uint nIndex, out long lpFileTime);

		[DllImport("Everything64.dll")]
		public static extern bool Everything_IsFolderResult(uint nIndex);

		[DllImport("Everything64.dll")]
		public static extern bool Everything_IsFileResult(uint nIndex);

		[DllImport("Everything64.dll")]
		public static extern uint Everything_GetLastError();

		[DllImport("Everything64.dll")]
		public static extern void Everything_Reset();
	}

	private const uint EVERYTHING_REQUEST_FILE_NAME = 0x00000001;
	private const uint EVERYTHING_REQUEST_PATH = 0x00000002;
	private const uint EVERYTHING_REQUEST_FULL_PATH_AND_FILE_NAME = 0x00000004;
	private const uint EVERYTHING_REQUEST_EXTENSION = 0x00000008;
	private const uint EVERYTHING_REQUEST_SIZE = 0x00000010;
	private const uint EVERYTHING_REQUEST_DATE_MODIFIED = 0x00000040;

	private const uint EVERYTHING_SORT_NAME_ASCENDING = 1;

	private static readonly object _syncLock = new object();
	private static bool? _dllAvailable;

	public class SearchResultItem
	{
		public string FullPath { get; set; } = "";
		public string FileName { get; set; } = "";
		public string Extension { get; set; } = "";
		public long Size { get; set; }
		public string SizeFormatted { get; set; } = "";
		public DateTime DateModified { get; set; }
		public string DateFormatted { get; set; } = "";
		public bool IsFolder { get; set; }
		public string Category { get; set; } = "Other"; // App, CAD, Doc, Folder, System, Other
		public string CategoryDisplay { get; set; } = "文件";
		public string BadgeBg { get; set; } = "#183B82F6";
		public string BadgeFg { get; set; } = "#3B82F6";
		public string IconEmoji { get; set; } = "📄";
		public string EngineSource { get; set; } = "Native";
		public string Details => IsFolder ? "文件夹" : $"{SizeFormatted} · {DateFormatted}";
	}

	public enum SearchEngineState
	{
		/// <summary>Everything 数据库已直连，IPC 0ms 毫秒级极速响应</summary>
		EverythingConnected,
		/// <summary>Everything 正以管理员权限运行，受 Windows UIPI 安全隔离阻断，需提权同步</summary>
		EverythingPermissionBlocked,
		/// <summary>系统已安装或包含 Everything，但当前未在后台运行</summary>
		EverythingNotRunning,
		/// <summary>系统未检测到 Everything，仅使用内置原生极速并发引擎</summary>
		NativeOnly
	}

	public static SearchEngineState LastEngineState { get; private set; } = SearchEngineState.NativeOnly;
	public static double LastQueryElapsedMs { get; private set; } = 0;

	/// <summary>
	/// 检查 Everything64.dll 是否可正常调用
	/// </summary>
	public static bool IsDllAvailable()
	{
		if (_dllAvailable.HasValue) return _dllAvailable.Value;
		try
		{
			Native.Everything_GetNumResults();
			_dllAvailable = true;
		}
		catch
		{
			_dllAvailable = false;
		}
		return _dllAvailable.Value;
	}

	/// <summary>
	/// 毫秒级探测系统中是否正在运行 Everything 客户端或后台服务
	/// </summary>
	public static bool IsEverythingRunning()
	{
		try
		{
			if (Native.FindWindow("EVERYTHING_TASKBAR_NOTIFICATION", null) != IntPtr.Zero
				|| Native.FindWindow("EVERYTHING", null) != IntPtr.Zero)
			{
				return true;
			}

			var procs = Process.GetProcessesByName("Everything");
			return procs.Length > 0;
		}
		catch
		{
			return false;
		}
	}

	/// <summary>
	/// 综合检测当前检索引擎连通状态（即时响应，零开销探测）
	/// </summary>
	public static SearchEngineState DetectCurrentEngineState()
	{
		if (IsEverythingRunning())
		{
			if (IsDllAvailable())
			{
				lock (_syncLock)
				{
					try
					{
						Native.Everything_Reset();
						Native.Everything_SetSearchW("StarPiePing");
						Native.Everything_SetMax(1);
						bool ok = Native.Everything_QueryW(true);
						if (ok)
						{
							LastEngineState = SearchEngineState.EverythingConnected;
							return LastEngineState;
						}

						uint err = Native.Everything_GetLastError();
						if (err == 2 && !ConfigManager.IsElevated())
						{
							LastEngineState = SearchEngineState.EverythingPermissionBlocked;
							return LastEngineState;
						}
					}
					catch { }
				}
			}
			LastEngineState = SearchEngineState.EverythingConnected;
			return LastEngineState;
		}

		string? exePath = FindLocalEverythingPath();
		LastEngineState = string.IsNullOrEmpty(exePath)
			? SearchEngineState.NativeOnly
			: SearchEngineState.EverythingNotRunning;
		return LastEngineState;
	}

	/// <summary>
	/// 在系统常见目录、桌面、用户漫游目录与注册表中检索可用的 Everything.exe 完整路径
	/// </summary>
	public static string? FindLocalEverythingPath()
	{
		try
		{
			string userDesktop = Environment.GetFolderPath(Environment.SpecialFolder.Desktop);
			string commonDesktop = Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory);
			string userProfile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
			string appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
			string localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);

			string[] candidates = new[]
			{
				Path.Combine(userDesktop, "Everything.exe"),
				Path.Combine(commonDesktop, "Everything.exe"),
				Path.Combine(userProfile, "Desktop", "Everything.exe"),
				@"G:\Users\2 Better\Desktop\Everything.exe",
				Path.Combine(appData, @"Everything\Everything.exe"),
				Path.Combine(localAppData, @"Programs\Everything\Everything.exe"),
				Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Everything.exe"),
				@"C:\Program Files\Everything\Everything.exe",
				@"C:\Program Files (x86)\Everything\Everything.exe",
				@"C:\Program Files\Everything 1.5a\Everything.exe",
				@"C:\Program Files\Everything 1.5a\Everything64.exe"
			};

			foreach (var path in candidates)
			{
				if (File.Exists(path))
				{
					return path;
				}
			}

			// 注册表 App Paths
			using (var key = Registry.LocalMachine.OpenSubKey(@"SOFTWARE\Microsoft\Windows\CurrentVersion\App Paths\Everything.exe"))
			{
				string? regPath = key?.GetValue("")?.ToString();
				if (!string.IsNullOrEmpty(regPath) && File.Exists(regPath))
				{
					return regPath;
				}
			}

			// 开始菜单快捷方式探测
			string startMenu = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonPrograms), "Everything.lnk");
			if (File.Exists(startMenu))
			{
				if (IconHelper.ResolveShortcutTarget(startMenu, out string target, out string _, out int _) && File.Exists(target))
				{
					return target;
				}
				return startMenu;
			}
		}
		catch { }
		return null;
	}

	/// <summary>
	/// 尝试定位并启动本地安装的 Everything.exe
	/// </summary>
	public static bool TryLaunchEverything()
	{
		try
		{
			string? path = FindLocalEverythingPath();
			if (!string.IsNullOrEmpty(path) && (File.Exists(path) || path.EndsWith(".lnk", StringComparison.OrdinalIgnoreCase)))
			{
				Process.Start(new ProcessStartInfo { FileName = path, UseShellExecute = true });
				return true;
			}
		}
		catch { }
		return false;
	}

	/// <summary>
	/// 专为程序选择器定制：全盘穿透搜索免安装可执行程序 (.exe)
	/// </summary>
	public static Task<List<SearchResultItem>> SearchExecutablesAsync(string query, int maxResults = 80)
	{
		return Task.Run(() =>
		{
			var results = new List<SearchResultItem>();
			if (string.IsNullOrWhiteSpace(query)) return results;

			if (IsDllAvailable() && IsEverythingRunning())
			{
				lock (_syncLock)
				{
					try
					{
						Native.Everything_Reset();
						// 排除安装包、卸载程序、回收站以及 Windows 系统敏感补丁目录
						string everythingQuery = $"ext:exe {query} !unins !setup !install !update !patcher !vcredist !dotnet !$Recycle.Bin";
						Native.Everything_SetSearchW(everythingQuery);
						Native.Everything_SetMax((uint)maxResults);
						Native.Everything_SetRequestFlags(EVERYTHING_REQUEST_FULL_PATH_AND_FILE_NAME | EVERYTHING_REQUEST_SIZE | EVERYTHING_REQUEST_DATE_MODIFIED);
						Native.Everything_SetSort(EVERYTHING_SORT_NAME_ASCENDING);

						bool queryOk = Native.Everything_QueryW(true);
						if (!queryOk && Native.Everything_GetLastError() == 2)
						{
							Native.Everything_Reset();
							Native.Everything_SetSearchW(everythingQuery);
							Native.Everything_SetMax((uint)maxResults);
							queryOk = Native.Everything_QueryW(true);
						}

						if (queryOk)
						{
							uint count = Native.Everything_GetNumResults();
							StringBuilder sb = new StringBuilder(1024);
							for (uint i = 0; i < count; i++)
							{
								sb.Clear();
								Native.Everything_GetResultFullPathNameW(i, sb, 1024);
								string fullPath = sb.ToString();
								if (string.IsNullOrEmpty(fullPath) || !fullPath.EndsWith(".exe", StringComparison.OrdinalIgnoreCase))
								{
									continue;
								}

								Native.Everything_GetResultSize(i, out long fileSize);
								Native.Everything_GetResultDateModified(i, out long fileTime);
								DateTime dateModified = fileTime > 0 ? DateTime.FromFileTime(fileTime) : DateTime.MinValue;

								string fileName = Path.GetFileName(fullPath);
								results.Add(new SearchResultItem
								{
									FullPath = fullPath,
									FileName = fileName,
									Extension = ".exe",
									Size = fileSize,
									SizeFormatted = FormatFileSize(fileSize),
									DateModified = dateModified,
									DateFormatted = dateModified != DateTime.MinValue ? dateModified.ToString("yyyy-MM-dd") : "",
									IsFolder = false,
									Category = "App",
									CategoryDisplay = "绿色便携",
									BadgeBg = "#18F97316",
									BadgeFg = "#F97316",
									IconEmoji = "🚀",
									EngineSource = "Everything"
								});
							}
						}
					}
					catch
					{
					}
				}
			}

			if (results.Count == 0)
			{
				// 降级：使用内置引擎快速检索免安装与可执行程序
				var nativeApps = NativeSearchEngine.SearchAsync(query, "App", maxResults).GetAwaiter().GetResult();
				foreach (var a in nativeApps)
				{
					if (a.Extension.Equals(".exe", StringComparison.OrdinalIgnoreCase))
					{
						a.CategoryDisplay = "绿色便携";
						a.BadgeBg = "#18F97316";
						a.BadgeFg = "#F97316";
						a.IconEmoji = "🚀";
						results.Add(a);
					}
				}
				if (results.Count == 0)
				{
					ScanPortableDirectoriesFallback(query, results, maxResults);
				}
			}

			return results;
		});
	}

	/// <summary>
	/// 全盘文件与程序秒搜核心查询：支持内置原生引擎与 Everything 智能协同
	/// </summary>
	public static async Task<List<SearchResultItem>> SearchFilesAndFoldersAsync(string query, string category = "All", int maxResults = 120)
	{
		string trimmed = query?.Trim() ?? "";

		// 空白初始态：直接返回内置原生引擎的高频推荐与常用项目
		if (string.IsNullOrEmpty(trimmed))
		{
			DetectCurrentEngineState();
			return NativeSearchEngine.GetInitialRecommendations(category);
		}

		// 若 Everything 正在运行且 DLL 正常，优先尝试通过 IPC 极速检索
		if (IsDllAvailable() && IsEverythingRunning())
		{
			var everythingResults = await Task.Run(() =>
			{
				var results = new List<SearchResultItem>();
				lock (_syncLock)
				{
					try
					{
						Native.Everything_Reset();
						string builtQuery = BuildCategoryQuery(trimmed, category);
						Native.Everything_SetSearchW(builtQuery);
						Native.Everything_SetMax((uint)maxResults);
						Native.Everything_SetRequestFlags(EVERYTHING_REQUEST_FULL_PATH_AND_FILE_NAME | EVERYTHING_REQUEST_SIZE | EVERYTHING_REQUEST_DATE_MODIFIED);

						bool queryOk = Native.Everything_QueryW(true);
						uint err = Native.Everything_GetLastError();
						if (!queryOk && err == 2)
						{
							Native.Everything_Reset();
							Native.Everything_SetSearchW(builtQuery);
							Native.Everything_SetMax((uint)maxResults);
							queryOk = Native.Everything_QueryW(true);
							err = Native.Everything_GetLastError();
						}

						if (queryOk)
						{
							LastEngineState = SearchEngineState.EverythingConnected;
							uint count = Native.Everything_GetNumResults();
							StringBuilder sb = new StringBuilder(1024);
							for (uint i = 0; i < count; i++)
							{
								sb.Clear();
								Native.Everything_GetResultFullPathNameW(i, sb, 1024);
								string fullPath = sb.ToString();
								if (string.IsNullOrEmpty(fullPath)) continue;

								bool isFolder = Native.Everything_IsFolderResult(i);
								Native.Everything_GetResultSize(i, out long fileSize);
								Native.Everything_GetResultDateModified(i, out long fileTime);
								DateTime dateModified = fileTime > 0 ? DateTime.FromFileTime(fileTime) : DateTime.MinValue;

								string ext = isFolder ? "" : Path.GetExtension(fullPath).ToLowerInvariant();
								string fileName = Path.GetFileName(fullPath);
								if (string.IsNullOrEmpty(fileName)) fileName = fullPath;

								var (cat, catDisplay, badgeBg, badgeFg, emoji) = ClassifyFile(fullPath, isFolder, ext);

								results.Add(new SearchResultItem
								{
									FullPath = fullPath,
									FileName = fileName,
									Extension = ext,
									Size = fileSize,
									SizeFormatted = isFolder ? "" : FormatFileSize(fileSize),
									DateModified = dateModified,
									DateFormatted = dateModified != DateTime.MinValue ? dateModified.ToString("yyyy-MM-dd") : "",
									IsFolder = isFolder,
									Category = cat,
									CategoryDisplay = catDisplay,
									BadgeBg = badgeBg,
									BadgeFg = badgeFg,
									IconEmoji = emoji,
									EngineSource = "Everything"
								});
							}
						}
						else
						{
							if (err == 2)
							{
								LastEngineState = !ConfigManager.IsElevated()
									? SearchEngineState.EverythingPermissionBlocked
									: SearchEngineState.EverythingNotRunning;
							}
							else
							{
								LastEngineState = SearchEngineState.EverythingConnected;
							}
						}
					}
					catch
					{
					}
				}
				return results;
			});

			if (everythingResults.Count > 0)
			{
				return everythingResults;
			}
		}
		else
		{
			string? exePath = FindLocalEverythingPath();
			LastEngineState = string.IsNullOrEmpty(exePath)
				? SearchEngineState.NativeOnly
				: SearchEngineState.EverythingNotRunning;
		}

		// 若 Everything 未运行、IPC受阻或检索结果为空，平滑无缝回退至内置原生极速引擎
		return await NativeSearchEngine.SearchAsync(trimmed, category, maxResults);
	}

	private static string BuildCategoryQuery(string rawQuery, string category)
	{
		string q = string.IsNullOrWhiteSpace(rawQuery) ? "" : rawQuery.Trim();
		string excludeJunk = "!$Recycle.Bin";

		return category switch
		{
			"App" => string.IsNullOrEmpty(q) ? "ext:exe;bat;cmd;ps1" : $"ext:exe;bat;cmd;ps1 {q} {excludeJunk}",
			"CAD" => string.IsNullOrEmpty(q) ? "ext:sldprt;sldasm;slddrw;step;stp;iges;igs;dwg;dxf;prt;asm;catpart;x_t;x_b" : $"ext:sldprt;sldasm;slddrw;step;stp;iges;igs;dwg;dxf;prt;asm;catpart;x_t;x_b {q} {excludeJunk}",
			"Doc" => string.IsNullOrEmpty(q) ? "ext:md;txt;doc;docx;xls;xlsx;ppt;pptx;pdf;py;cs;cpp;h;json;xml;csv" : $"ext:md;txt;doc;docx;xls;xlsx;ppt;pptx;pdf;py;cs;cpp;h;json;xml;csv {q} {excludeJunk}",
			"Folder" => string.IsNullOrEmpty(q) ? "folder:" : $"folder: {q} {excludeJunk}",
			"Video" => string.IsNullOrEmpty(q) ? "ext:mp4;mkv;avi;mov;flv;wmv;rmvb;webm;ts;m4v;3gp" : $"ext:mp4;mkv;avi;mov;flv;wmv;rmvb;webm;ts;m4v;3gp {q} {excludeJunk}",
			"System" => string.IsNullOrEmpty(q) ? "ext:cpl;msc" : $"ext:cpl;msc {q} {excludeJunk}",
			_ => string.IsNullOrEmpty(q) ? "" : $"{q} {excludeJunk}"
		};
	}

	private static (string cat, string catDisplay, string badgeBg, string badgeFg, string emoji) ClassifyFile(string fullPath, bool isFolder, string ext)
	{
		if (isFolder)
		{
			return ("Folder", "文件夹", "#18EAB308", "#EAB308", "📁");
		}

		switch (ext)
		{
			// 应用程序与脚本
			case ".exe":
				return ("App", "应用程序", "#183B82F6", "#3B82F6", "💻");
			case ".bat":
			case ".cmd":
			case ".ps1":
				return ("App", "批处理脚本", "#183B82F6", "#3B82F6", "⚡");

			// 三维 CAD 与机械工程格式
			case ".sldprt":
				return ("CAD", "SolidWorks 零件", "#188B5CF6", "#8B5CF6", "🔩");
			case ".sldasm":
				return ("CAD", "SolidWorks 装配体", "#188B5CF6", "#8B5CF6", "⚙️");
			case ".slddrw":
				return ("CAD", "SolidWorks 工程图", "#188B5CF6", "#8B5CF6", "📐");
			case ".step":
			case ".stp":
				return ("CAD", "STEP 通用模型", "#1806B6D4", "#06B6D4", "📐");
			case ".iges":
			case ".igs":
				return ("CAD", "IGES 模型", "#1806B6D4", "#06B6D4", "📐");
			case ".dwg":
			case ".dxf":
				return ("CAD", "AutoCAD 图纸", "#1806B6D4", "#06B6D4", "📏");
			case ".x_t":
			case ".x_b":
				return ("CAD", "Parasolid 实体", "#188B5CF6", "#8B5CF6", "📐");

			// 文档与代码
			case ".md":
				return ("Doc", "Markdown 文档", "#183B82F6", "#3B82F6", "📝");
			case ".doc":
			case ".docx":
				return ("Doc", "Word 文档", "#182563EB", "#2563EB", "📄");
			case ".xls":
			case ".xlsx":
			case ".csv":
				return ("Doc", "Excel 表格", "#1810B981", "#10B981", "📊");
			case ".ppt":
			case ".pptx":
				return ("Doc", "PowerPoint 演示", "#18F97316", "#F97316", "📑");
			case ".pdf":
				return ("Doc", "PDF 文档", "#18EF4444", "#EF4444", "📕");
			case ".py":
				return ("Doc", "Python 源码", "#18F59E0B", "#F59E0B", "🐍");
			case ".cs":
				return ("Doc", "C# 源码", "#188B5CF6", "#8B5CF6", "💻");
			case ".cpp":
			case ".c":
			case ".h":
				return ("Doc", "C/C++ 源码", "#186366F1", "#6366F1", "⚙️");
			case ".json":
			case ".xml":
				return ("Doc", "配置文件", "#1864748B", "#64748B", "📋");

			// 视频媒体格式
			case ".mp4":
			case ".mkv":
			case ".avi":
			case ".mov":
			case ".flv":
			case ".wmv":
			case ".rmvb":
			case ".webm":
			case ".ts":
			case ".m4v":
			case ".3gp":
				return ("Video", "视频媒体", "#18EC4899", "#EC4899", "🎬");

			// 系统工具
			case ".cpl":
			case ".msc":
				return ("System", "系统管理控制", "#1864748B", "#64748B", "⚙️");

			default:
				return ("Other", ext.TrimStart('.').ToUpperInvariant() + " 文件", "#1464748B", "#64748B", "📄");
		}
	}

	private static void ScanPortableDirectoriesFallback(string query, List<SearchResultItem> results, int maxResults)
	{
		try
		{
			var scanRoots = new List<string>();
			string desktop = Environment.GetFolderPath(Environment.SpecialFolder.Desktop);
			string downloads = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Downloads");
			scanRoots.Add(desktop);
			scanRoots.Add(downloads);

			// 检查各磁盘根目录下的 Tools, Portable
			foreach (var drive in DriveInfo.GetDrives().Where(d => d.IsReady && d.DriveType == DriveType.Fixed))
			{
				string p1 = Path.Combine(drive.RootDirectory.FullName, "Portable");
				string p2 = Path.Combine(drive.RootDirectory.FullName, "Tools");
				string p3 = Path.Combine(drive.RootDirectory.FullName, "Software");
				if (Directory.Exists(p1)) scanRoots.Add(p1);
				if (Directory.Exists(p2)) scanRoots.Add(p2);
				if (Directory.Exists(p3)) scanRoots.Add(p3);
			}

			foreach (var root in scanRoots)
			{
				if (!Directory.Exists(root)) continue;
				try
				{
					var files = Directory.EnumerateFiles(root, "*.exe", SearchOption.AllDirectories);
					foreach (var file in files)
					{
						string name = Path.GetFileName(file);
						if (name.Contains(query, StringComparison.OrdinalIgnoreCase))
						{
							var fi = new FileInfo(file);
							results.Add(new SearchResultItem
							{
								FullPath = file,
								FileName = name,
								Extension = ".exe",
								Size = fi.Length,
								SizeFormatted = FormatFileSize(fi.Length),
								DateModified = fi.LastWriteTime,
								DateFormatted = fi.LastWriteTime.ToString("yyyy-MM-dd"),
								IsFolder = false,
								Category = "App",
								CategoryDisplay = "本地绿色程序",
								BadgeBg = "#18F97316",
								BadgeFg = "#F97316",
								IconEmoji = "🚀"
							});
							if (results.Count >= maxResults) return;
						}
					}
				}
				catch
				{
				}
			}
		}
		catch
		{
		}
	}

	private static void FallbackLocalSearch(string query, string category, List<SearchResultItem> results, int maxResults)
	{
		try
		{
			// 扫描 Recent Items 与 Desktop / Documents
			string recent = Environment.GetFolderPath(Environment.SpecialFolder.Recent);
			string desktop = Environment.GetFolderPath(Environment.SpecialFolder.Desktop);
			string documents = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);

			var pathsToScan = new List<string> { desktop, documents };

			if (Directory.Exists(recent))
			{
				foreach (var lnk in Directory.EnumerateFiles(recent, "*.lnk"))
				{
					if (IconHelper.ResolveShortcutTarget(lnk, out string target, out string _, out int _) && File.Exists(target))
					{
						string fileName = Path.GetFileName(target);
						if (string.IsNullOrEmpty(query) || fileName.Contains(query, StringComparison.OrdinalIgnoreCase))
						{
							string ext = Path.GetExtension(target).ToLowerInvariant();
							var fi = new FileInfo(target);
							var (cat, catDisplay, badgeBg, badgeFg, emoji) = ClassifyFile(target, false, ext);
							if (category == "All" || string.Equals(cat, category, StringComparison.OrdinalIgnoreCase))
							{
								results.Add(new SearchResultItem
								{
									FullPath = target,
									FileName = fileName,
									Extension = ext,
									Size = fi.Length,
									SizeFormatted = FormatFileSize(fi.Length),
									DateModified = fi.LastWriteTime,
									DateFormatted = fi.LastWriteTime.ToString("yyyy-MM-dd"),
									IsFolder = false,
									Category = cat,
									CategoryDisplay = catDisplay,
									BadgeBg = badgeBg,
									BadgeFg = badgeFg,
									IconEmoji = emoji
								});
								if (results.Count >= maxResults) return;
							}
						}
					}
				}
			}

			foreach (var dir in pathsToScan)
			{
				if (!Directory.Exists(dir)) continue;
				foreach (var file in Directory.EnumerateFiles(dir, "*.*", SearchOption.TopDirectoryOnly))
				{
					string fileName = Path.GetFileName(file);
					if (string.IsNullOrEmpty(query) || fileName.Contains(query, StringComparison.OrdinalIgnoreCase))
					{
						string ext = Path.GetExtension(file).ToLowerInvariant();
						var fi = new FileInfo(file);
						var (cat, catDisplay, badgeBg, badgeFg, emoji) = ClassifyFile(file, false, ext);
						if (category == "All" || string.Equals(cat, category, StringComparison.OrdinalIgnoreCase))
						{
							results.Add(new SearchResultItem
							{
								FullPath = file,
								FileName = fileName,
								Extension = ext,
								Size = fi.Length,
								SizeFormatted = FormatFileSize(fi.Length),
								DateModified = fi.LastWriteTime,
								DateFormatted = fi.LastWriteTime.ToString("yyyy-MM-dd"),
								IsFolder = false,
								Category = cat,
								CategoryDisplay = catDisplay,
								BadgeBg = badgeBg,
								BadgeFg = badgeFg,
								IconEmoji = emoji
							});
							if (results.Count >= maxResults) return;
						}
					}
				}
			}
		}
		catch
		{
		}
	}

	private static string FormatFileSize(long bytes)
	{
		if (bytes <= 0) return "0 KB";
		if (bytes < 1024) return $"{bytes} B";
		if (bytes < 1024 * 1024) return $"{bytes / 1024.0:F1} KB";
		if (bytes < 1024 * 1024 * 1024) return $"{bytes / (1024.0 * 1024):F1} MB";
		return $"{bytes / (1024.0 * 1024 * 1024):F2} GB";
	}
}

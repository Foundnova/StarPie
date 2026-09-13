using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace WinPieGestures;

/// <summary>
/// StarPie 内置自包含原生全盘极速搜索引擎
/// 无需依赖或安装任何第三方软件（如 Everything），开箱即用；
/// 具备内存预加载常用应用层、桌面与工程极速扫描层，以及多盘并发广度优先穿透能力。
/// </summary>
public static class NativeSearchEngine
{
	private static readonly object _initLock = new object();
	private static bool _isInitialized;
	private static List<EverythingService.SearchResultItem> _cachedApps = new List<EverythingService.SearchResultItem>();
	private static DateTime _lastCacheTime = DateTime.MinValue;

	private static readonly HashSet<string> s_ignoredDirs = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
	{
		"$Recycle.Bin",
		"System Volume Information",
		"node_modules",
		".git",
		".vs",
		".idea",
		".gradle",
		".nuget",
		"WinSxS",
		"$WinREAgent",
		"DumpStack.log.tmp",
		"Recovery",
		"MSOCache"
	};

	private static readonly HashSet<string> s_cadExts = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
	{
		".sldprt", ".sldasm", ".slddrw", ".step", ".stp", ".iges", ".igs",
		".dwg", ".dxf", ".prt", ".asm", ".catpart", ".x_t", ".x_b"
	};

	private static readonly HashSet<string> s_docExts = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
	{
		".md", ".txt", ".doc", ".docx", ".xls", ".xlsx", ".ppt", ".pptx",
		".pdf", ".py", ".cs", ".cpp", ".c", ".h", ".json", ".xml", ".csv"
	};

	private static readonly HashSet<string> s_appExts = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
	{
		".exe", ".bat", ".cmd", ".ps1", ".lnk"
	};

	private static readonly HashSet<string> s_videoExts = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
	{
		".mp4", ".mkv", ".avi", ".mov", ".flv", ".wmv", ".rmvb", ".webm", ".ts", ".m4v", ".3gp", ".f4v"
	};

	/// <summary>
	/// 确保常用应用、全盘固定驱动器已安装软件与注册表索引初始化
	/// </summary>
	public static void EnsureCacheInitialized()
	{
		if (_isInitialized && (DateTime.Now - _lastCacheTime).TotalMinutes < 30)
		{
			return;
		}

		lock (_initLock)
		{
			if (_isInitialized && (DateTime.Now - _lastCacheTime).TotalMinutes < 30)
			{
				return;
			}

			var apps = new List<EverythingService.SearchResultItem>();
			var seenPaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

			// 1. 系统核心控制预设
			apps.Add(new EverythingService.SearchResultItem
			{
				FullPath = @"C:\Windows\System32\Taskmgr.exe",
				FileName = "任务管理器 (Task Manager)",
				Extension = ".exe",
				Category = "System",
				CategoryDisplay = "系统工具",
				BadgeBg = "#1864748B",
				BadgeFg = "#64748B",
				IconEmoji = "📊"
			});
			apps.Add(new EverythingService.SearchResultItem
			{
				FullPath = @"control.exe",
				FileName = "控制面板 (Control Panel)",
				Extension = ".exe",
				Category = "System",
				CategoryDisplay = "系统设置",
				BadgeBg = "#1864748B",
				BadgeFg = "#64748B",
				IconEmoji = "⚙️"
			});
			apps.Add(new EverythingService.SearchResultItem
			{
				FullPath = @"calc.exe",
				FileName = "计算器 (Calculator)",
				Extension = ".exe",
				Category = "System",
				CategoryDisplay = "系统小工具",
				BadgeBg = "#183B82F6",
				BadgeFg = "#3B82F6",
				IconEmoji = "🔢"
			});
			apps.Add(new EverythingService.SearchResultItem
			{
				FullPath = @"explorer.exe",
				FileName = "文件资源管理器 (File Explorer)",
				Extension = ".exe",
				Category = "System",
				CategoryDisplay = "系统管理",
				BadgeBg = "#18EAB308",
				BadgeFg = "#EAB308",
				IconEmoji = "📁"
			});

			// 2. 收集桌面与开始菜单快捷方式
			var searchDirs = new List<string>();
			AddDirIfValid(searchDirs, Environment.GetFolderPath(Environment.SpecialFolder.Desktop));
			AddDirIfValid(searchDirs, Environment.GetFolderPath(Environment.SpecialFolder.CommonDesktopDirectory));
			AddDirIfValid(searchDirs, Environment.GetFolderPath(Environment.SpecialFolder.StartMenu));
			AddDirIfValid(searchDirs, Environment.GetFolderPath(Environment.SpecialFolder.CommonStartMenu));

			foreach (var dir in searchDirs)
			{
				try
				{
					var dirInfo = new DirectoryInfo(dir);
					foreach (var file in dirInfo.EnumerateFiles("*", SearchOption.AllDirectories))
					{
						string ext = file.Extension.ToLowerInvariant();
						if (ext == ".lnk" || ext == ".exe")
						{
							string nameWithoutExt = Path.GetFileNameWithoutExtension(file.Name);
							if (nameWithoutExt.Contains("uninstall", StringComparison.OrdinalIgnoreCase) ||
							    nameWithoutExt.Contains("卸载", StringComparison.OrdinalIgnoreCase) ||
							    nameWithoutExt.Contains("update", StringComparison.OrdinalIgnoreCase))
							{
								continue;
							}

							if (seenPaths.Add(file.FullName))
							{
								apps.Add(new EverythingService.SearchResultItem
								{
									FullPath = file.FullName,
									FileName = nameWithoutExt,
									Extension = ext,
									Size = file.Length,
									SizeFormatted = FormatFileSize(file.Length),
									DateModified = file.LastWriteTime,
									DateFormatted = file.LastWriteTime.ToString("yyyy-MM-dd"),
									IsFolder = false,
									Category = "App",
									CategoryDisplay = ext == ".lnk" ? "快捷方式" : "可执行程序",
									BadgeBg = "#183B82F6",
									BadgeFg = "#3B82F6",
									IconEmoji = "💻"
								});
							}
						}
					}
				}
				catch { }
			}

			// 3. 收集 64 位与 32 位注册表 App Paths
			(Microsoft.Win32.RegistryHive, Microsoft.Win32.RegistryView)[] hives = new[]
			{
				(Microsoft.Win32.RegistryHive.LocalMachine, Microsoft.Win32.RegistryView.Registry64),
				(Microsoft.Win32.RegistryHive.LocalMachine, Microsoft.Win32.RegistryView.Registry32),
				(Microsoft.Win32.RegistryHive.CurrentUser, Microsoft.Win32.RegistryView.Default)
			};
			foreach (var (hKey, view) in hives)
			{
				try
				{
					using var baseKey = Microsoft.Win32.RegistryKey.OpenBaseKey(hKey, view);
					using var appPathsKey = baseKey.OpenSubKey(@"SOFTWARE\Microsoft\Windows\CurrentVersion\App Paths");
					if (appPathsKey != null)
					{
						foreach (var subKeyName in appPathsKey.GetSubKeyNames())
						{
							try
							{
								using var subKey = appPathsKey.OpenSubKey(subKeyName);
								string? pathVal = subKey?.GetValue("")?.ToString();
								if (!string.IsNullOrEmpty(pathVal))
								{
									string cleanPath = Environment.ExpandEnvironmentVariables(pathVal.Trim().Trim('"'));
									if (File.Exists(cleanPath) && seenPaths.Add(cleanPath))
									{
										string name = Path.GetFileNameWithoutExtension(subKeyName);
										var fi = new FileInfo(cleanPath);
										apps.Add(new EverythingService.SearchResultItem
										{
											FullPath = cleanPath,
											FileName = name,
											Extension = ".exe",
											Size = fi.Length,
											SizeFormatted = FormatFileSize(fi.Length),
											DateModified = fi.LastWriteTime,
											DateFormatted = fi.LastWriteTime.ToString("yyyy-MM-dd"),
											IsFolder = false,
											Category = "App",
											CategoryDisplay = "已安装应用",
											BadgeBg = "#183B82F6",
											BadgeFg = "#3B82F6",
											IconEmoji = "💻"
										});
									}
								}
							}
							catch { }
						}
					}
				}
				catch { }
			}

			// 4. 深度扫描各固定驱动器根级与子级程序目录（如 H:\PS2024, K:\QQ, H:\bilibili 等便携或免安装程序）
			try
			{
				foreach (var drive in DriveInfo.GetDrives().Where(d => d.IsReady && d.DriveType == DriveType.Fixed))
				{
					try
					{
						foreach (var dir in Directory.GetDirectories(drive.RootDirectory.FullName))
						{
							string dirName = Path.GetFileName(dir);
							if (string.IsNullOrEmpty(dirName) || s_ignoredDirs.Contains(dirName) || dirName.StartsWith("$") || dirName.Equals("Windows", StringComparison.OrdinalIgnoreCase))
							{
								continue;
							}

							// 根目录下直接的 exe
							try
							{
								foreach (var exe in Directory.GetFiles(dir, "*.exe", SearchOption.TopDirectoryOnly))
								{
									string exeName = Path.GetFileNameWithoutExtension(exe);
									if (exeName.Contains("unins", StringComparison.OrdinalIgnoreCase) || exeName.Contains("setup", StringComparison.OrdinalIgnoreCase) || exeName.Contains("helper", StringComparison.OrdinalIgnoreCase))
									{
										continue;
									}

									if (seenPaths.Add(exe))
									{
										var fi = new FileInfo(exe);
										apps.Add(new EverythingService.SearchResultItem
										{
											FullPath = exe,
											FileName = $"{dirName} ({exeName})",
											Extension = ".exe",
											Size = fi.Length,
											SizeFormatted = FormatFileSize(fi.Length),
											DateModified = fi.LastWriteTime,
											DateFormatted = fi.LastWriteTime.ToString("yyyy-MM-dd"),
											IsFolder = false,
											Category = "App",
											CategoryDisplay = "本地应用",
											BadgeBg = "#18F97316",
											BadgeFg = "#F97316",
											IconEmoji = "🚀"
										});
									}
								}
							}
							catch { }

							// 1 级子目录下的 exe（如 H:\PS2024\Adobe Photoshop 2024\Photoshop.exe 或 K:\QQ\Bin\QQ.exe）
							try
							{
								foreach (var subDir in Directory.GetDirectories(dir))
								{
									string subName = Path.GetFileName(subDir);
									if (subName.StartsWith(".") || s_ignoredDirs.Contains(subName)) continue;

									foreach (var exe in Directory.GetFiles(subDir, "*.exe", SearchOption.TopDirectoryOnly))
									{
										string exeName = Path.GetFileNameWithoutExtension(exe);
										if (exeName.Contains("unins", StringComparison.OrdinalIgnoreCase) || exeName.Contains("setup", StringComparison.OrdinalIgnoreCase) || exeName.Contains("crash", StringComparison.OrdinalIgnoreCase))
										{
											continue;
										}

										if (seenPaths.Add(exe))
										{
											var fi = new FileInfo(exe);
											apps.Add(new EverythingService.SearchResultItem
											{
												FullPath = exe,
												FileName = $"{exeName} ({dirName})",
												Extension = ".exe",
												Size = fi.Length,
												SizeFormatted = FormatFileSize(fi.Length),
												DateModified = fi.LastWriteTime,
												DateFormatted = fi.LastWriteTime.ToString("yyyy-MM-dd"),
												IsFolder = false,
												Category = "App",
												CategoryDisplay = "本地应用",
												BadgeBg = "#18F97316",
												BadgeFg = "#F97316",
												IconEmoji = "🚀"
											});
										}
									}
								}
							}
							catch { }
						}
					}
					catch { }
				}
			}
			catch { }

			_cachedApps = apps;
			_lastCacheTime = DateTime.Now;
			_isInitialized = true;
		}
	}

	/// <summary>
	/// 当搜索输入为空时，展示智能推荐与常用项目（告别冷冰冰的“未发现匹配文件”）
	/// </summary>
	public static List<EverythingService.SearchResultItem> GetInitialRecommendations(string category = "All")
	{
		EnsureCacheInitialized();

		var list = new List<EverythingService.SearchResultItem>();

		// 1. 优先放入高频应用程序与系统工具
		if (category == "All" || category == "App" || category == "System")
		{
			var apps = _cachedApps.Where(a => category == "All" || a.Category.Equals(category, StringComparison.OrdinalIgnoreCase)).Take(15);
			list.AddRange(apps);
		}

		// 2. 放入桌面上的近期活跃文件与文件夹
		try
		{
			string desktop = Environment.GetFolderPath(Environment.SpecialFolder.Desktop);
			if (Directory.Exists(desktop))
			{
				var desktopInfo = new DirectoryInfo(desktop);
				var recentEntries = desktopInfo.EnumerateFileSystemInfos()
					.Where(e => !e.Attributes.HasFlag(FileAttributes.Hidden) && !e.Name.StartsWith("."))
					.OrderByDescending(e => e.LastWriteTime)
					.Take(18);

				foreach (var entry in recentEntries)
				{
					bool isFolder = entry is DirectoryInfo;
					string ext = isFolder ? "" : entry.Extension.ToLowerInvariant();
					if (ext == ".ini" || ext == ".tmp") continue;

					var (cat, catDisplay, badgeBg, badgeFg, emoji) = ClassifyEntry(entry.FullName, isFolder, ext);
					if (category == "All" || cat.Equals(category, StringComparison.OrdinalIgnoreCase))
					{
						long size = isFolder ? 0 : ((FileInfo)entry).Length;
						list.Add(new EverythingService.SearchResultItem
						{
							FullPath = entry.FullName,
							FileName = isFolder ? entry.Name : Path.GetFileName(entry.FullName),
							Extension = ext,
							Size = size,
							SizeFormatted = isFolder ? "" : FormatFileSize(size),
							DateModified = entry.LastWriteTime,
							DateFormatted = entry.LastWriteTime.ToString("yyyy-MM-dd"),
							IsFolder = isFolder,
							Category = cat,
							CategoryDisplay = catDisplay,
							BadgeBg = badgeBg,
							BadgeFg = badgeFg,
							IconEmoji = emoji
						});
					}
				}
			}
		}
		catch { }

		// 3. 放入常用根目录与项目工程目录
		if (category == "All" || category == "Folder")
		{
			string[] commonDirs = new[]
			{
				Environment.GetFolderPath(Environment.SpecialFolder.Desktop),
				Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Downloads"),
				Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
				@"G:\Users\2 Better\Desktop\design"
			};
			foreach (var d in commonDirs)
			{
				if (Directory.Exists(d) && !list.Any(x => x.FullPath.Equals(d, StringComparison.OrdinalIgnoreCase)))
				{
					list.Add(new EverythingService.SearchResultItem
					{
						FullPath = d,
						FileName = Path.GetFileName(d),
						Extension = "",
						IsFolder = true,
						Category = "Folder",
						CategoryDisplay = "常用目录",
						BadgeBg = "#18EAB308",
						BadgeFg = "#EAB308",
						IconEmoji = "📁"
					});
				}
			}
		}

		// 4. 若选定了视频分类且列表较空，尝试检索用户视频库与下载目录中的近期视频
		if (category == "Video")
		{
			try
			{
				var videoDirs = new[]
				{
					Environment.GetFolderPath(Environment.SpecialFolder.MyVideos),
					Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Videos"),
					Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Downloads"),
					Environment.GetFolderPath(Environment.SpecialFolder.Desktop)
				};
				foreach (var vd in videoDirs)
				{
					if (!Directory.Exists(vd)) continue;
					var dirInfo = new DirectoryInfo(vd);
					foreach (var file in dirInfo.EnumerateFiles("*", SearchOption.TopDirectoryOnly))
					{
						string ext = file.Extension.ToLowerInvariant();
						if (s_videoExts.Contains(ext) && !list.Any(x => x.FullPath.Equals(file.FullName, StringComparison.OrdinalIgnoreCase)))
						{
							list.Add(new EverythingService.SearchResultItem
							{
								FullPath = file.FullName,
								FileName = file.Name,
								Extension = ext,
								Size = file.Length,
								SizeFormatted = FormatFileSize(file.Length),
								DateModified = file.LastWriteTime,
								DateFormatted = file.LastWriteTime.ToString("yyyy-MM-dd"),
								IsFolder = false,
								Category = "Video",
								CategoryDisplay = "视频媒体",
								BadgeBg = "#18EC4899",
								BadgeFg = "#EC4899",
								IconEmoji = "🎬"
							});
							if (list.Count >= 20) break;
						}
					}
					if (list.Count >= 20) break;
				}
			}
			catch { }
		}

		return list;
	}

	/// <summary>
	/// 清空搜索引擎静态缓存（在搜索窗口关闭或长时间闲置后调用，归还内存）
	/// </summary>
	public static void ClearCaches()
	{
		lock (_initLock)
		{
			_cachedApps.Clear();
			_cachedApps.TrimExcess();
			_isInitialized = false;
			_lastCacheTime = DateTime.MinValue;
		}
	}

	/// <summary>
	/// 内置原生多线程广度优先全盘极速检索
	/// </summary>
	public static Task<List<EverythingService.SearchResultItem>> SearchAsync(string query, string category = "All", int maxResults = 80, CancellationToken token = default)
	{
		return Task.Run(() =>
		{
			EnsureCacheInitialized();

			var results = new List<EverythingService.SearchResultItem>();
			var seenPaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

			string q = query.Trim();
			if (string.IsNullOrEmpty(q))
			{
				return GetInitialRecommendations(category);
			}

			// 第一层：从已缓存的应用和控制面板中毫秒级匹配 (0ms)
			if (category == "All" || category == "App" || category == "System")
			{
				var matchedApps = _cachedApps.Where(a =>
				{
					if (category != "All" && !a.Category.Equals(category, StringComparison.OrdinalIgnoreCase))
					{
						return false;
					}
					return a.FileName.Contains(q, StringComparison.OrdinalIgnoreCase) ||
					       a.FullPath.Contains(q, StringComparison.OrdinalIgnoreCase);
				}).Take(40);

				foreach (var app in matchedApps)
				{
					if (seenPaths.Add(app.FullPath))
					{
						results.Add(app);
					}
				}
			}

			if (token.IsCancellationRequested || results.Count >= maxResults)
			{
				return results;
			}

			// 第二层：并发深盘广度优先穿透检索（重点是深度与流式轻量化）
			var searchRoots = new List<string>();
			AddDirIfValid(searchRoots, Environment.GetFolderPath(Environment.SpecialFolder.Desktop));
			AddDirIfValid(searchRoots, Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Downloads"));
			AddDirIfValid(searchRoots, Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments));
			AddDirIfValid(searchRoots, @"G:\Users\2 Better\Desktop\design");

			try
			{
				foreach (var drive in DriveInfo.GetDrives().Where(d => d.IsReady && d.DriveType == DriveType.Fixed))
				{
					if (!searchRoots.Contains(drive.RootDirectory.FullName, StringComparer.OrdinalIgnoreCase))
					{
						searchRoots.Add(drive.RootDirectory.FullName);
					}
				}
			}
			catch { }

			var syncLock = new object();
			const int maxDepth = 15; // 深度优先：深度提升至 15 层，彻底解决深层目录搜不到的痛点
			const int maxVisitedPerRoot = 3500; // 每个根节点最多遍历 3500 目录，多盘并发总数达数万目录

			Parallel.ForEach(searchRoots, new ParallelOptions
			{
				MaxDegreeOfParallelism = Math.Min(Environment.ProcessorCount, 6),
				CancellationToken = token
			}, root =>
			{
				if (token.IsCancellationRequested) return;

				var queue = new Queue<(string path, int depth)>();
				queue.Enqueue((root, 0));
				int visited = 0;

				while (queue.Count > 0 && visited < maxVisitedPerRoot)
				{
					if (token.IsCancellationRequested) break;
					lock (syncLock)
					{
						if (results.Count >= maxResults) break;
					}

					var (curDir, depth) = queue.Dequeue();
					visited++;

					try
					{
						// 流式字符串枚举：彻底摒弃 EnumerateFileSystemInfos 产生上百万 FileInfo/DirectoryInfo 导致堆段爆炸的缺陷
						foreach (string fullPath in Directory.EnumerateFileSystemEntries(curDir))
						{
							if (token.IsCancellationRequested) break;
							lock (syncLock)
							{
								if (results.Count >= maxResults) break;
							}

							string name = Path.GetFileName(fullPath);
							if (string.IsNullOrEmpty(name) || name.StartsWith("."))
							{
								continue;
							}

							// 快速判断目录：通过轻量级 FileAttributes，避免实例化庞大对象
							FileAttributes attr;
							try
							{
								attr = File.GetAttributes(fullPath);
							}
							catch
							{
								continue;
							}

							if (attr.HasFlag(FileAttributes.Hidden))
							{
								continue;
							}

							bool isFolder = attr.HasFlag(FileAttributes.Directory);
							string ext = isFolder ? "" : Path.GetExtension(name).ToLowerInvariant();

							// 匹配关键词
							if (name.Contains(q, StringComparison.OrdinalIgnoreCase))
							{
								var (cat, catDisplay, badgeBg, badgeFg, emoji) = ClassifyEntry(fullPath, isFolder, ext);

								// 分类过滤
								if (category == "All" || cat.Equals(category, StringComparison.OrdinalIgnoreCase))
								{
									lock (syncLock)
									{
										if (seenPaths.Add(fullPath))
										{
											long size = 0;
											DateTime dateModified = DateTime.MinValue;
											try
											{
												if (!isFolder)
												{
													var fi = new FileInfo(fullPath);
													size = fi.Length;
													dateModified = fi.LastWriteTime;
												}
												else
												{
													dateModified = Directory.GetLastWriteTime(fullPath);
												}
											}
											catch { }

											results.Add(new EverythingService.SearchResultItem
											{
												FullPath = fullPath,
												FileName = name,
												Extension = ext,
												Size = size,
												SizeFormatted = isFolder ? "" : FormatFileSize(size),
												DateModified = dateModified,
												DateFormatted = dateModified != DateTime.MinValue ? dateModified.ToString("yyyy-MM-dd") : "",
												IsFolder = isFolder,
												Category = cat,
												CategoryDisplay = catDisplay,
												BadgeBg = badgeBg,
												BadgeFg = badgeFg,
												IconEmoji = emoji,
												EngineSource = "Native"
											});

											if (results.Count >= maxResults) break;
										}
									}
								}
							}

							// 子目录入队：支持深层 15 层递归，过滤无关庞大垃圾缓存
							if (isFolder && depth < maxDepth)
							{
								if (!s_ignoredDirs.Contains(name))
								{
									queue.Enqueue((fullPath, depth + 1));
								}
							}
						}
					}
					catch { }
				}
			});

			return results;
		}, token);
	}

	private static void AddDirIfValid(List<string> list, string dir)
	{
		if (!string.IsNullOrWhiteSpace(dir) && Directory.Exists(dir) && !list.Contains(dir, StringComparer.OrdinalIgnoreCase))
		{
			list.Add(dir);
		}
	}

	private static (string cat, string catDisplay, string badgeBg, string badgeFg, string emoji) ClassifyEntry(string fullPath, bool isFolder, string ext)
	{
		if (isFolder)
		{
			return ("Folder", "文件夹", "#18EAB308", "#EAB308", "📁");
		}

		if (s_cadExts.Contains(ext))
		{
			return ext switch
			{
				".sldprt" => ("CAD", "SolidWorks 零件", "#188B5CF6", "#8B5CF6", "🔩"),
				".sldasm" => ("CAD", "SolidWorks 装配体", "#188B5CF6", "#8B5CF6", "⚙️"),
				".slddrw" => ("CAD", "SolidWorks 工程图", "#188B5CF6", "#8B5CF6", "📐"),
				".dwg" or ".dxf" => ("CAD", "AutoCAD 图纸", "#1806B6D4", "#06B6D4", "📏"),
				".step" or ".stp" or ".iges" or ".igs" => ("CAD", "3D 通用模型", "#1806B6D4", "#06B6D4", "📐"),
				_ => ("CAD", "三维CAD工程", "#188B5CF6", "#8B5CF6", "📐")
			};
		}

		if (s_appExts.Contains(ext))
		{
			return ext switch
			{
				".lnk" => ("App", "快捷方式", "#183B82F6", "#3B82F6", "💻"),
				".bat" or ".cmd" or ".ps1" => ("App", "系统脚本", "#183B82F6", "#3B82F6", "⚡"),
				_ => ("App", "应用程序", "#183B82F6", "#3B82F6", "🚀")
			};
		}

		if (s_docExts.Contains(ext))
		{
			return ext switch
			{
				".md" => ("Doc", "Markdown", "#183B82F6", "#3B82F6", "📝"),
				".doc" or ".docx" => ("Doc", "Word 文档", "#182563EB", "#2563EB", "📄"),
				".xls" or ".xlsx" or ".csv" => ("Doc", "Excel 表格", "#1810B981", "#10B981", "📊"),
				".ppt" or ".pptx" => ("Doc", "PowerPoint", "#18F97316", "#F97316", "📑"),
				".pdf" => ("Doc", "PDF 电子书", "#18EF4444", "#EF4444", "📕"),
				".py" => ("Doc", "Python 源码", "#18F59E0B", "#F59E0B", "🐍"),
				".cs" => ("Doc", "C# 源码", "#188B5CF6", "#8B5CF6", "💻"),
				_ => ("Doc", "文本/代码", "#1864748B", "#64748B", "📋")
			};
		}

		if (s_videoExts.Contains(ext))
		{
			return ("Video", "视频媒体", "#18EC4899", "#EC4899", "🎬");
		}

		if (ext == ".cpl" || ext == ".msc")
		{
			return ("System", "系统管理", "#1864748B", "#64748B", "⚙️");
		}

		return ("Other", ext.TrimStart('.').ToUpperInvariant() + " 文件", "#1464748B", "#64748B", "📄");
	}

	private static string FormatFileSize(long bytes)
	{
		if (bytes < 1024) return $"{bytes} B";
		if (bytes < 1024 * 1024) return $"{bytes / 1024.0:F1} KB";
		if (bytes < 1024 * 1024 * 1024) return $"{bytes / (1024.0 * 1024):F1} MB";
		return $"{bytes / (1024.0 * 1024 * 1024):F2} GB";
	}
}

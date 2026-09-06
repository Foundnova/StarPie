using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Security.Principal;
using System.Text.Json;
using Microsoft.Win32;

namespace WinPieGestures;

public static class ConfigManager
{
	private static readonly string AppDataFolder;

	private static readonly string ConfigPath;

	public static AppConfig CurrentConfig { get; private set; }

	// 本次启动是否因配置文件损坏而回落到了默认配置。
	// 为 true 时必须禁止任何自动写盘，否则会把默认配置覆盖掉用户尚可恢复的损坏文件。
	public static bool IsFallbackConfig { get; private set; }

	private static string GetAppDataFolder()
	{
		string path = (string.IsNullOrEmpty(Environment.GetEnvironmentVariable("LOCALAPPDATA")) ? Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData) : Environment.GetEnvironmentVariable("LOCALAPPDATA"));
		string text = Path.Combine(path, "StarPie");
		string text2 = Path.Combine(path, "WinPieGestures");
		if (!Directory.Exists(text) && Directory.Exists(text2))
		{
			try
			{
				Directory.CreateDirectory(text);
				string text3 = Path.Combine(text2, "config.json");
				string text4 = Path.Combine(text, "config.json");
				if (File.Exists(text3) && !File.Exists(text4))
				{
					File.Copy(text3, text4);
				}
			}
			catch
			{
			}
		}
		return text;
	}

	// 保留无法解析的配置文件现场，使用户有机会手工恢复，而不是被默认配置静默覆盖。
	private static void BackupCorruptConfig()
	{
		try
		{
			if (!File.Exists(ConfigPath))
			{
				return;
			}
			string destFileName = ConfigPath + ".corrupt." + DateTime.Now.ToString("yyyyMMddHHmmss");
			File.Copy(ConfigPath, destFileName, overwrite: true);
			AppLogger.LogInfo("Backed up unreadable config to '" + destFileName + "'");
		}
		catch (Exception ex)
		{
			AppLogger.LogError("Failed to back up unreadable config", ex);
		}
	}

	static ConfigManager()
	{
		AppDataFolder = GetAppDataFolder();
		ConfigPath = Path.Combine(AppDataFolder, "config.json");
		LoadConfig();
	}

	public static void LoadConfig()
	{
		try
		{
			if (!Directory.Exists(AppDataFolder))
			{
				Directory.CreateDirectory(AppDataFolder);
			}
			if (File.Exists(ConfigPath))
			{
				string json = File.ReadAllText(ConfigPath);
				JsonSerializerOptions options = new JsonSerializerOptions
				{
					PropertyNameCaseInsensitive = true,
					AllowTrailingCommas = true,
					ReadCommentHandling = JsonCommentHandling.Skip
				};
				CurrentConfig = JsonSerializer.Deserialize<AppConfig>(json, options) ?? CreateDefaultConfig();
				AppLogger.LogInfo($"Loaded configuration from '{ConfigPath}'");
			}
			else
			{
				CurrentConfig = CreateDefaultConfig();
				SaveConfig();
				AppLogger.LogInfo($"Created and saved default configuration at '{ConfigPath}'");
			}
			AppConfig currentConfig = CurrentConfig;
			if (currentConfig.BlacklistedProcesses == null)
			{
				AppConfig appConfig = currentConfig;
				List<string> obj = new List<string> { "mstsc.exe", "paint.exe" };
				List<string> list = obj;
				appConfig.BlacklistedProcesses = obj;
			}
			currentConfig = CurrentConfig;
			if (currentConfig.WhitelistedProcesses == null)
			{
				List<string> list = (currentConfig.WhitelistedProcesses = new List<string>());
			}
			if (string.IsNullOrEmpty(CurrentConfig.IsolationMode))
			{
				CurrentConfig.IsolationMode = "Blacklist";
			}
			if (CurrentConfig.Profiles != null)
			{
				foreach (WheelProfile profile in CurrentConfig.Profiles)
				{
					if (profile.Actions == null)
					{
						continue;
					}
					foreach (ActionItem action in profile.Actions)
					{
						if (action.SubActions == null)
						{
							List<ActionItem> list3 = (action.SubActions = new List<ActionItem>());
						}
					}
				}
				WheelProfile wheelProfile = CurrentConfig.Profiles.FirstOrDefault((WheelProfile p) => string.Equals(p.ProcessName, "Global", StringComparison.OrdinalIgnoreCase));
				if (wheelProfile != null && wheelProfile.Actions != null && wheelProfile.Actions.Sum((ActionItem a) => a.SubActions?.Count ?? 0) == 0)
				{
					if (wheelProfile.Actions.Count > 0 && wheelProfile.Actions[0] != null)
					{
						wheelProfile.Actions[0].SubActions = new List<ActionItem>
						{
							new ActionItem
							{
								Type = "Hotkey",
								Name = "复制",
								Parameter = "Ctrl+C",
								IconKey = "Copy"
							},
							new ActionItem
							{
								Type = "Hotkey",
								Name = "剪切",
								Parameter = "Ctrl+X",
								IconKey = "Cut"
							},
							new ActionItem
							{
								Type = "Hotkey",
								Name = "粘贴",
								Parameter = "Ctrl+V",
								IconKey = "Paste"
							},
							new ActionItem
							{
								Type = "Hotkey",
								Name = "全选",
								Parameter = "Ctrl+A",
								IconKey = "Edit"
							}
						};
					}
					if (wheelProfile.Actions.Count > 6 && wheelProfile.Actions[6] != null)
					{
						wheelProfile.Actions[6].SubActions = new List<ActionItem>
						{
							new ActionItem
							{
								Type = "Launch",
								Name = "记事本",
								Parameter = "notepad.exe",
								IconKey = "Code"
							},
							new ActionItem
							{
								Type = "System",
								Name = "计算器",
								Parameter = "Calculator",
								IconKey = "Code"
							},
							new ActionItem
							{
								Type = "System",
								Name = "任务管理器",
								Parameter = "TaskManager",
								IconKey = "Terminal"
							}
						};
					}
					// 回落默认配置时禁止自动落盘：否则会立刻用默认值覆盖掉用户尚可恢复的配置文件
					if (!IsFallbackConfig)
					{
						SaveConfig();
					}
				}
			}
			I18n.SetLanguage(CurrentConfig.Language);
			// 启动性能优化：自启同步完全移出启动关键路径，后台延迟 4 秒执行，消除开机时的阻塞
			_ = System.Threading.Tasks.Task.Run(async () =>
			{
				try
				{
					await System.Threading.Tasks.Task.Delay(4000).ConfigureAwait(false);
					EnsureAutoStartRegistryUpToDate();
				}
				catch
				{
				}
			});
		}
		catch (Exception ex)
		{
			AppLogger.LogError("Failed to load config from '" + ConfigPath + "', falling back to default configuration", ex);
			BackupCorruptConfig();
			IsFallbackConfig = true;
			CurrentConfig = CreateDefaultConfig();
			I18n.SetLanguage(CurrentConfig.Language);
		}
	}

	// 返回是否保存成功，调用方据此决定提示文案，避免无条件宣称“已保存”。
	public static bool SaveConfig()
	{
		try
		{
			if (!Directory.Exists(AppDataFolder))
			{
				Directory.CreateDirectory(AppDataFolder);
			}
			JsonSerializerOptions options = new JsonSerializerOptions
			{
				WriteIndented = true
			};
			string contents = JsonSerializer.Serialize(CurrentConfig, options);
			// 原子写：先落临时文件再替换。直接 WriteAllText 一旦中途被中断（退出/崩溃/断电）
			// 会留下截断的 config.json，下次启动即被判为损坏并回落默认配置。
			string tempPath = ConfigPath + ".tmp";
			File.WriteAllText(tempPath, contents);
			if (File.Exists(ConfigPath))
			{
				// 保留上一份完好配置，替换失败时仍可人工回退
				File.Replace(tempPath, ConfigPath, ConfigPath + ".bak");
			}
			else
			{
				File.Move(tempPath, ConfigPath, overwrite: true);
			}
			return true;
		}
		catch (Exception ex)
		{
			AppLogger.LogError("Failed to save config to file", ex);
			return false;
		}
	}

	public static WheelProfile GetProfileForProcess(string processName)
	{
		if (string.IsNullOrEmpty(processName))
		{
			return GetGlobalProfile();
		}
		string lowerProc = processName.ToLower();
		return CurrentConfig.Profiles.Find((WheelProfile p) => p.ProcessName.ToLower() == lowerProc) ?? GetGlobalProfile();
	}

	public static WheelProfile GetGlobalProfile()
	{
		WheelProfile wheelProfile = CurrentConfig.Profiles.Find((WheelProfile p) => p.ProcessName.Equals("Global", StringComparison.OrdinalIgnoreCase));
		if (wheelProfile == null)
		{
			wheelProfile = new WheelProfile
			{
				ProcessName = "Global",
				SectorCount = 8,
				Actions = new List<ActionItem>()
			};
			CurrentConfig.Profiles.Insert(0, wheelProfile);
		}
		return wheelProfile;
	}

	private static AppConfig CreateDefaultConfig()
	{
		AppConfig obj = new AppConfig
		{
			DragThreshold = 25.0
		};
		WheelProfile item = new WheelProfile
		{
			ProcessName = "Global",
			SectorCount = 8,
			Actions = new List<ActionItem>
			{
				new ActionItem
				{
					Type = "Hotkey",
					Name = "复制 (Copy)",
					Parameter = "Ctrl+C",
					IconKey = "Copy"
				},
				new ActionItem
				{
					Type = "System",
					Name = "锁定电脑 (Lock)",
					Parameter = "Lock",
					IconKey = "Lock"
				},
				new ActionItem
				{
					Type = "System",
					Name = "显示桌面 (Desktop)",
					Parameter = "ShowDesktop",
					IconKey = "ShowDesktop"
				},
				new ActionItem
				{
					Type = "System",
					Name = "屏幕截图 (Capture)",
					Parameter = "Screenshot",
					IconKey = "Screenshot"
				},
				new ActionItem
				{
					Type = "Hotkey",
					Name = "粘贴 (Paste)",
					Parameter = "Ctrl+V",
					IconKey = "Paste"
				},
				new ActionItem
				{
					Type = "System",
					Name = "音量减 (Vol Down)",
					Parameter = "VolumeDown",
					IconKey = "VolumeDown"
				},
				new ActionItem
				{
					Type = "Launch",
					Name = "系统工具 (Tools)",
					Parameter = "notepad.exe",
					IconKey = "Code",
					SubActions = new List<ActionItem>
					{
						new ActionItem
						{
							Type = "Launch",
							Name = "记事本",
							Parameter = "notepad.exe",
							IconKey = "Code"
						},
						new ActionItem
						{
							Type = "System",
							Name = "计算器",
							Parameter = "Calculator",
							IconKey = "Code"
						},
						new ActionItem
						{
							Type = "System",
							Name = "任务管理器",
							Parameter = "TaskManager",
							IconKey = "Terminal"
						}
					}
				},
				new ActionItem
				{
					Type = "System",
					Name = "音量增 (Vol Up)",
					Parameter = "VolumeUp",
					IconKey = "VolumeUp"
				}
			}
		};
		WheelProfile item2 = new WheelProfile
		{
			ProcessName = "chrome.exe",
			SectorCount = 4,
			Actions = new List<ActionItem>
			{
				new ActionItem
				{
					Type = "Hotkey",
					Name = "关闭标签 (Close Tab)",
					Parameter = "Ctrl+W",
					IconKey = "CloseTab"
				},
				new ActionItem
				{
					Type = "Hotkey",
					Name = "后退 (Back)",
					Parameter = "Alt+Left",
					IconKey = "Back"
				},
				new ActionItem
				{
					Type = "Hotkey",
					Name = "新建标签 (New Tab)",
					Parameter = "Ctrl+T",
					IconKey = "NewTab"
				},
				new ActionItem
				{
					Type = "Hotkey",
					Name = "刷新 (Refresh)",
					Parameter = "F5",
					IconKey = "Refresh"
				}
			}
		};
		WheelProfile item3 = new WheelProfile
		{
			ProcessName = "code.exe",
			SectorCount = 8,
			Actions = new List<ActionItem>
			{
				new ActionItem
				{
					Type = "Hotkey",
					Name = "定义跳转 (F12)",
					Parameter = "F12",
					IconKey = "Code"
				},
				new ActionItem
				{
					Type = "Hotkey",
					Name = "格式化 (Format)",
					Parameter = "Shift+Alt+F",
					IconKey = "Edit"
				},
				new ActionItem
				{
					Type = "Hotkey",
					Name = "控制台 (Terminal)",
					Parameter = "Ctrl+`",
					IconKey = "Terminal"
				},
				new ActionItem
				{
					Type = "Hotkey",
					Name = "查找文件 (Quick Open)",
					Parameter = "Ctrl+P",
					IconKey = "Search"
				},
				new ActionItem
				{
					Type = "Hotkey",
					Name = "保存全部 (Save All)",
					Parameter = "Ctrl+K,S",
					IconKey = "Save"
				},
				new ActionItem
				{
					Type = "Hotkey",
					Name = "全局搜索 (Find in Files)",
					Parameter = "Ctrl+Shift+F",
					IconKey = "Search"
				},
				new ActionItem
				{
					Type = "Hotkey",
					Name = "撤销 (Undo)",
					Parameter = "Ctrl+Z",
					IconKey = "Undo"
				},
				new ActionItem
				{
					Type = "Hotkey",
					Name = "重做 (Redo)",
					Parameter = "Ctrl+Y",
					IconKey = "Redo"
				}
			}
		};
		obj.Profiles.Add(item);
		obj.Profiles.Add(item2);
		obj.Profiles.Add(item3);
		return obj;
	}

	public static bool ExportConfig(string targetFilePath)
	{
		try
		{
			JsonSerializerOptions options = new JsonSerializerOptions
			{
				WriteIndented = true
			};
			string contents = JsonSerializer.Serialize(CurrentConfig, options);
			File.WriteAllText(targetFilePath, contents);
			return true;
		}
		catch (Exception)
		{
			return false;
		}
	}

	public static bool ImportConfig(string sourceFilePath)
	{
		try
		{
			if (!File.Exists(sourceFilePath))
			{
				return false;
			}
			AppConfig appConfig = JsonSerializer.Deserialize<AppConfig>(File.ReadAllText(sourceFilePath));
			if (appConfig != null)
			{
				CurrentConfig = appConfig;
				SaveConfig();
				return true;
			}
		}
		catch (Exception)
		{
		}
		return false;
	}

	public static bool IsElevated()
	{
		try
		{
			using WindowsIdentity identity = WindowsIdentity.GetCurrent();
			WindowsPrincipal principal = new WindowsPrincipal(identity);
			return principal.IsInRole(WindowsBuiltInRole.Administrator);
		}
		catch
		{
			return false;
		}
	}

	public static bool IsAutoStartEnabled()
	{
		if (IsRegistryAutoStartEnabled())
		{
			return true;
		}
		if (CurrentConfig != null && CurrentConfig.AutoStartAsAdmin)
		{
			return IsAdminTaskAutoStartEnabled();
		}
		return false;
	}

	public static bool IsRegistryAutoStartEnabled()
	{
		try
		{
			using RegistryKey registryKey = Registry.CurrentUser.OpenSubKey("Software\\Microsoft\\Windows\\CurrentVersion\\Run", writable: false);
			return (registryKey != null && registryKey.GetValue("StarPie") != null) || registryKey?.GetValue("WinPieGestures") != null;
		}
		catch
		{
			return false;
		}
	}

	public static bool IsAdminTaskAutoStartEnabled()
	{
		try
		{
			using Process process = Process.Start(new ProcessStartInfo
			{
				FileName = "schtasks.exe",
				Arguments = "/query /tn \"StarPie_AdminAutoStart\"",
				UseShellExecute = false,
				CreateNoWindow = true,
				RedirectStandardOutput = true,
				RedirectStandardError = true
			});
			if (process == null)
			{
				return false;
			}
			process.WaitForExit(1500);
			return process.ExitCode == 0;
		}
		catch
		{
			return false;
		}
	}

	public static void SetAutoStart(bool enable, bool asAdmin = false)
	{
		try
		{
			string exePath = Environment.ProcessPath ?? Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "StarPie.exe");
			if (enable)
			{
				// Always write registry auto-start as the foundational reliable guarantee
				SetRegistryAutoStart(exePath);

				if (asAdmin)
				{
					CreateOrUpdateAdminTask(exePath);
				}
				else
				{
					RemoveAdminTask();
				}
			}
			else
			{
				RemoveRegistryAutoStart();
				RemoveAdminTask();
			}
			if (CurrentConfig != null)
			{
				CurrentConfig.AutoStartAsAdmin = asAdmin;
				SaveConfig();
			}
		}
		catch (Exception)
		{
		}
	}

	private static void SetRegistryAutoStart(string exePath)
	{
		try
		{
			using RegistryKey registryKey = Registry.CurrentUser.OpenSubKey("Software\\Microsoft\\Windows\\CurrentVersion\\Run", writable: true);
			if (registryKey == null)
			{
				return;
			}
			registryKey.SetValue("StarPie", "\"" + exePath + "\" --autostart --minimized");
			try
			{
				registryKey.DeleteValue("WinPieGestures", throwOnMissingValue: false);
			}
			catch
			{
			}
		}
		catch
		{
		}
	}

	private static void RemoveRegistryAutoStart()
	{
		try
		{
			using RegistryKey registryKey = Registry.CurrentUser.OpenSubKey("Software\\Microsoft\\Windows\\CurrentVersion\\Run", writable: true);
			if (registryKey == null)
			{
				return;
			}
			try
			{
				registryKey.DeleteValue("StarPie", throwOnMissingValue: false);
			}
			catch
			{
			}
			try
			{
				registryKey.DeleteValue("WinPieGestures", throwOnMissingValue: false);
			}
			catch
			{
			}
		}
		catch
		{
		}
	}

	private static void CreateOrUpdateAdminTask(string exePath)
	{
		try
		{
			// /delay 0000:00 确保计划任务在用户登录后以零延迟（0秒）立即启动，消除 Windows 任务计划程序默认的数秒登录延迟
			string arguments = $"/create /tn \"StarPie_AdminAutoStart\" /tr \"\\\"{exePath}\\\" --autostart --minimized\" /sc onlogon /delay 0000:00 /rl highest /f";
			bool isElevated = IsElevated();
			ProcessStartInfo psi = new ProcessStartInfo
			{
				FileName = "schtasks.exe",
				Arguments = arguments,
				UseShellExecute = !isElevated,
				Verb = isElevated ? "" : "runas",
				CreateNoWindow = isElevated,
				WindowStyle = ProcessWindowStyle.Hidden
			};
			using Process process = Process.Start(psi);
			process?.WaitForExit(2000);
		}
		catch (Exception)
		{
		}
	}

	private static void RemoveAdminTask()
	{
		try
		{
			bool isElevated = IsElevated();
			ProcessStartInfo psi = new ProcessStartInfo
			{
				FileName = "schtasks.exe",
				Arguments = "/delete /tn \"StarPie_AdminAutoStart\" /f",
				UseShellExecute = !isElevated,
				Verb = isElevated ? "" : "runas",
				CreateNoWindow = isElevated,
				WindowStyle = ProcessWindowStyle.Hidden
			};
			using Process process = Process.Start(psi);
			process?.WaitForExit(2000);
		}
		catch
		{
		}
	}

	public static void EnsureAutoStartRegistryUpToDate()
	{
		try
		{
			// 若当前进程本身就是通过自启动参数呼起，说明任务与注册表均已正确就绪，直接跳过耗时的外置进程核验
			if (Environment.GetCommandLineArgs().Any(a => string.Equals(a, "--autostart", StringComparison.OrdinalIgnoreCase)))
			{
				return;
			}

			if (CurrentConfig != null && CurrentConfig.AutoStartAsAdmin)
			{
				if (IsAdminTaskAutoStartEnabled())
				{
					CreateOrUpdateAdminTask(Environment.ProcessPath ?? Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "StarPie.exe"));
				}
				return;
			}
			using RegistryKey registryKey = Registry.CurrentUser.OpenSubKey("Software\\Microsoft\\Windows\\CurrentVersion\\Run", writable: true);
			if (registryKey == null)
			{
				return;
			}
			string text = (registryKey.GetValue("StarPie") as string) ?? (registryKey.GetValue("WinPieGestures") as string);
			if (string.IsNullOrEmpty(text))
			{
				return;
			}
			string text2 = Environment.ProcessPath ?? Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "StarPie.exe");
			string text3 = "\"" + text2 + "\" --autostart --minimized";
			if (!(text != text3))
			{
				return;
			}
			registryKey.SetValue("StarPie", text3);
			try
			{
				registryKey.DeleteValue("WinPieGestures", throwOnMissingValue: false);
			}
			catch
			{
			}
		}
		catch
		{
		}
	}
}

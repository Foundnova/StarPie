using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.Json;
using Microsoft.Win32;

namespace WinPieGestures;

public static class ConfigManager
{
	private static readonly string AppDataFolder;

	private static readonly string ConfigPath;

	public static AppConfig CurrentConfig { get; private set; }

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

	static ConfigManager()
	{
		AppDataFolder = GetAppDataFolder();
		ConfigPath = Path.Combine(AppDataFolder, "config.json");
		LoadConfig();
	}

<<<<<<< HEAD
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
			}
			else
			{
				CurrentConfig = CreateDefaultConfig();
				SaveConfig();
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
					SaveConfig();
				}
			}
			I18n.SetLanguage(CurrentConfig.Language);
			EnsureAutoStartRegistryUpToDate();
		}
		catch (Exception)
		{
			CurrentConfig = CreateDefaultConfig();
			I18n.SetLanguage(CurrentConfig.Language);
		}
	}
=======
    public class AppConfig
    {
        public string Language { get; set; } = "Auto";
        public string TriggerButton { get; set; } = "RightButton";
        public TriggerConfig Trigger { get; set; } = new TriggerConfig(); // "RightButton", "MiddleButton", "XButton1", "XButton2" // "Auto", "zh-CN", "zh-TW", "en", "ja"
        public double DragThreshold { get; set; } = 25.0;
        public string AnimationSpeed { get; set; } = "Balanced"; // "Elegant" (130ms), "Balanced" (80ms), "Fast" (35ms)
        public bool EnableOuterEscapeCancel { get; set; } = false;
        public double OuterEscapeDistance { get; set; } = 186.0; // Distance in pixels to trigger radial menu
        public string AppTheme { get; set; } = "System"; // "System", "Light", "Dark", "MidnightNavy", "RoyalViolet", "TitaniumGray"
        public string Theme { get; set; } = "System"; // Radial Wheel Color Theme: "System", "Dark", "Light", "MatchaForest", "GlacialIce", "MorandiMuted", "Custom"
        public string UiStyle { get; set; } = "ClassicRing"; // "ClassicRing", "CleanSectors", "Glassmorphism", "CatPaw"
        
        // Multi-Tier Sub-Wheel (多级轮盘与级联子菜单)
        public bool EnableMultiTier { get; set; } = true; // Multi-Tier feature toggle
        public double SubWheelRadiusRatio { get; set; } = 1.55; // Outer ring radius multiplier
        public string SubmenuStyle { get; set; } = "Wheel"; // "Wheel" (outer sub-ring), "Fan" (honeycomb fan)
        
        public bool ShowText { get; set; } = true;
        public double WheelRadius { get; set; } = 138.0;
        public double InnerRadius { get; set; } = 52.0;
        public double CoreRadius { get; set; } = 50.0;
        public string Shape { get; set; } = "Original"; // "Original", "Circle", "RoundedRect", "FloatingCapsules", "HexagonHive"
        public double SectorGap { get; set; } = 2.0; // Optical Gap between sectors: 0 ~ 12px
        public double SectorCornerRadius { get; set; } = 4.0; // Smooth Corner/Fillet: 0 ~ 16px
        public string IconLayoutMode { get; set; } = "IconAndText"; // "IconAndText", "IconOnly", "TextOnly"
        public double SectorIconSize { get; set; } = 20.0; // 14.0 ~ 36.0 px
        public double SectorFontSize { get; set; } = 10.5; // 8.0 ~ 18.0 px
>>>>>>> 3ff691fae314fa72f6cc0244386f8e08f9efbc00

	public static void SaveConfig()
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
			File.WriteAllText(ConfigPath, contents);
		}
		catch (Exception)
		{
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

	public static bool IsAutoStartEnabled()
	{
		if (!IsRegistryAutoStartEnabled())
		{
			return IsAdminTaskAutoStartEnabled();
		}
		return true;
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
			string text = Environment.ProcessPath ?? Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "StarPie.exe");
			if (enable)
			{
				if (asAdmin)
				{
					CreateOrUpdateAdminTask(text);
					RemoveRegistryAutoStart();
				}
				else
				{
					SetRegistryAutoStart(text);
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
			string arguments = "/create /tn \"StarPie_AdminAutoStart\" /tr \"\\\"" + exePath + "\\\" --autostart --minimized\" /sc onlogon /rl highest /f";
			using Process process = Process.Start(new ProcessStartInfo
			{
				FileName = "schtasks.exe",
				Arguments = arguments,
				UseShellExecute = false,
				CreateNoWindow = true,
				RedirectStandardOutput = true,
				RedirectStandardError = true
			});
			process?.WaitForExit(2500);
		}
		catch (Exception)
		{
		}
	}

	private static void RemoveAdminTask()
	{
		try
		{
			using Process process = Process.Start(new ProcessStartInfo
			{
				FileName = "schtasks.exe",
				Arguments = "/delete /tn \"StarPie_AdminAutoStart\" /f",
				UseShellExecute = false,
				CreateNoWindow = true,
				RedirectStandardOutput = true,
				RedirectStandardError = true
			});
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

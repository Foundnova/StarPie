using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;

namespace WinPieGestures
{
    public class ActionItem
    {
        public string Type { get; set; } // "Launch", "Hotkey", "System"
        public string Name { get; set; } // Name to show on the wheel sector
        public string Parameter { get; set; } // Executable path, hotkey string, or system preset
        public string Arguments { get; set; } // Optional arguments for launching
    }

    public class WheelProfile
    {
        public string ProcessName { get; set; } // e.g. "chrome.exe" or "Global"
        public int SectorCount { get; set; } // 4, 8, or 12
        public List<ActionItem> Actions { get; set; }
    }

    public class AppConfig
    {
        public double DragThreshold { get; set; } = 25.0; // Distance in pixels to trigger radial menu
        public List<WheelProfile> Profiles { get; set; } = new List<WheelProfile>();
    }

    public static class ConfigManager
    {
        private static readonly string AppDataFolder = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "WinPieGestures"
        );
        private static readonly string ConfigPath = Path.Combine(AppDataFolder, "config.json");

        public static AppConfig CurrentConfig { get; private set; }

        static ConfigManager()
        {
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
                    CurrentConfig = JsonSerializer.Deserialize<AppConfig>(json) ?? CreateDefaultConfig();
                }
                else
                {
                    CurrentConfig = CreateDefaultConfig();
                    SaveConfig();
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Failed to load config: {ex.Message}");
                CurrentConfig = CreateDefaultConfig();
            }
        }

        public static void SaveConfig()
        {
            try
            {
                if (!Directory.Exists(AppDataFolder))
                {
                    Directory.CreateDirectory(AppDataFolder);
                }

                var options = new JsonSerializerOptions { WriteIndented = true };
                string json = JsonSerializer.Serialize(CurrentConfig, options);
                File.WriteAllText(ConfigPath, json);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Failed to save config: {ex.Message}");
            }
        }

        private static AppConfig CreateDefaultConfig()
        {
            var config = new AppConfig { DragThreshold = 25.0 };

            // Create Global default profile with 8 keys
            var globalProfile = new WheelProfile
            {
                ProcessName = "Global",
                SectorCount = 8,
                Actions = new List<ActionItem>
                {
                    new ActionItem { Type = "Hotkey", Name = "复制 (Copy)", Parameter = "Ctrl+C" },           // Index 0: Right (E)
                    new ActionItem { Type = "System", Name = "锁定电脑 (Lock)", Parameter = "Lock" },        // Index 1: Down-Right (SE)
                    new ActionItem { Type = "System", Name = "显示桌面 (Desktop)", Parameter = "ShowDesktop" }, // Index 2: Down (S)
                    new ActionItem { Type = "System", Name = "屏幕截图 (Capture)", Parameter = "Screenshot" }, // Index 3: Down-Left (SW)
                    new ActionItem { Type = "Hotkey", Name = "粘贴 (Paste)", Parameter = "Ctrl+V" },          // Index 4: Left (W)
                    new ActionItem { Type = "System", Name = "音量减 (Vol Down)", Parameter = "VolumeDown" },  // Index 5: Up-Left (NW)
                    new ActionItem { Type = "Launch", Name = "记事本 (Notepad)", Parameter = "notepad.exe" },   // Index 6: Up (N)
                    new ActionItem { Type = "System", Name = "音量增 (Vol Up)", Parameter = "VolumeUp" }       // Index 7: Up-Right (NE)
                }
            };

            // Create Chrome specific profile with 4 keys for demo
            var chromeProfile = new WheelProfile
            {
                ProcessName = "chrome.exe",
                SectorCount = 4,
                Actions = new List<ActionItem>
                {
                    new ActionItem { Type = "Hotkey", Name = "关闭标签 (Close Tab)", Parameter = "Ctrl+W" },  // Index 0: Right (E)
                    new ActionItem { Type = "Hotkey", Name = "刷新页面 (Refresh)", Parameter = "Ctrl+R" },    // Index 1: Down (S)
                    new ActionItem { Type = "Hotkey", Name = "后退 (Back)", Parameter = "Alt+Left" },         // Index 2: Left (W)
                    new ActionItem { Type = "Hotkey", Name = "新建标签 (New Tab)", Parameter = "Ctrl+T" }     // Index 3: Up (N)
                }
            };

            config.Profiles.Add(globalProfile);
            config.Profiles.Add(chromeProfile);

            return config;
        }

        public static WheelProfile GetProfileForProcess(string processName)
        {
            if (string.IsNullOrEmpty(processName))
                return GetGlobalProfile();

            string normalizedProcess = processName.Trim().ToLower();
            foreach (var profile in CurrentConfig.Profiles)
            {
                if (profile.ProcessName.Trim().ToLower() == normalizedProcess)
                {
                    return profile;
                }
            }

            return GetGlobalProfile();
        }

        private static WheelProfile GetGlobalProfile()
        {
            foreach (var profile in CurrentConfig.Profiles)
            {
                if (profile.ProcessName == "Global")
                {
                    return profile;
                }
            }

            // Fallback if not found
            var config = CreateDefaultConfig();
            return config.Profiles[0];
        }
    }
}

using System;
using System.Windows;
using System.Windows.Media;

namespace WinPieGestures
{
    using Color = System.Windows.Media.Color;
    using ColorConverter = System.Windows.Media.ColorConverter;

    public static class AppThemeManager
    {
        public static string CurrentEffectiveTheme { get; private set; } = "Light";

        public static void ApplyTheme(FrameworkElement rootElement, string themeName)
        {
            if (rootElement == null) return;

            string effectiveTheme = themeName;
            if (string.Equals(themeName, "System", StringComparison.OrdinalIgnoreCase) || string.IsNullOrEmpty(themeName))
            {
                effectiveTheme = IsWindowsInDarkTheme() ? "Dark" : "Light";
            }

            CurrentEffectiveTheme = effectiveTheme;

            switch (effectiveTheme.ToLowerInvariant())
            {
                case "dark":
                case "obsidiandark":
                    SetThemeBrushes(rootElement,
                        windowBg: "#0F172A",
                        sidebarBg: "#182234",
                        cardBg: "#1E293B",
                        cardBorder: "#334155",
                        textPrimary: "#F8FAFC",
                        textSecondary: "#94A3B8",
                        textMuted: "#64748B",
                        inputBg: "#0F172A",
                        inputBorder: "#475569",
                        itemHover: "#334155",
                        subtleCard: "#131D2E",
                        accentPrimary: "#3B82F6",
                        accentHover: "#60A5FA");
                    break;

                case "midnightnavy":
                    SetThemeBrushes(rootElement,
                        windowBg: "#0B132B",
                        sidebarBg: "#141C36",
                        cardBg: "#1C2541",
                        cardBorder: "#3A506B",
                        textPrimary: "#F1F5F9",
                        textSecondary: "#94A3B8",
                        textMuted: "#5C6B73",
                        inputBg: "#0E1838",
                        inputBorder: "#3A506B",
                        itemHover: "#2B3A67",
                        subtleCard: "#111C3A",
                        accentPrimary: "#38BDF8",
                        accentHover: "#0284C7");
                    break;

                case "royalviolet":
                    SetThemeBrushes(rootElement,
                        windowBg: "#130826",
                        sidebarBg: "#1F0E3D",
                        cardBg: "#271448",
                        cardBorder: "#492675",
                        textPrimary: "#FAF5FF",
                        textSecondary: "#C084FC",
                        textMuted: "#8B5CF6",
                        inputBg: "#180B30",
                        inputBorder: "#5B21B6",
                        itemHover: "#3B1C63",
                        subtleCard: "#1C0D37",
                        accentPrimary: "#A855F7",
                        accentHover: "#C084FC");
                    break;

                case "titaniumgray":
                    SetThemeBrushes(rootElement,
                        windowBg: "#181818",
                        sidebarBg: "#222222",
                        cardBg: "#282828",
                        cardBorder: "#3C3C3C",
                        textPrimary: "#F0F0F0",
                        textSecondary: "#AAAAAA",
                        textMuted: "#777777",
                        inputBg: "#1E1E1E",
                        inputBorder: "#4A4A4A",
                        itemHover: "#383838",
                        subtleCard: "#1F1F1F",
                        accentPrimary: "#3B82F6",
                        accentHover: "#60A5FA");
                    break;

                case "light":
                default:
                    SetThemeBrushes(rootElement,
                        windowBg: "#F8FAFC",
                        sidebarBg: "#FFFFFF",
                        cardBg: "#FFFFFF",
                        cardBorder: "#E2E8F0",
                        textPrimary: "#0F172A",
                        textSecondary: "#64748B",
                        textMuted: "#94A3B8",
                        inputBg: "#FFFFFF",
                        inputBorder: "#CBD5E1",
                        itemHover: "#F1F5F9",
                        subtleCard: "#F8FAFC",
                        accentPrimary: "#2563EB",
                        accentHover: "#1D4ED8");
                    break;
            }
        }

        private static void SetThemeBrushes(FrameworkElement root,
            string windowBg, string sidebarBg, string cardBg, string cardBorder,
            string textPrimary, string textSecondary, string textMuted,
            string inputBg, string inputBorder, string itemHover, string subtleCard,
            string accentPrimary, string accentHover)
        {
            root.Resources["WindowBackgroundBrush"] = CreateSolidBrush(windowBg);
            root.Resources["SidebarBackgroundBrush"] = CreateSolidBrush(sidebarBg);
            root.Resources["CardBackgroundBrush"] = CreateSolidBrush(cardBg);
            root.Resources["CardBorderBrush"] = CreateSolidBrush(cardBorder);
            root.Resources["TextPrimaryBrush"] = CreateSolidBrush(textPrimary);
            root.Resources["TextSecondaryBrush"] = CreateSolidBrush(textSecondary);
            root.Resources["TextMutedBrush"] = CreateSolidBrush(textMuted);
            root.Resources["InputBackgroundBrush"] = CreateSolidBrush(inputBg);
            root.Resources["InputBorderBrush"] = CreateSolidBrush(inputBorder);
            root.Resources["ItemHoverBrush"] = CreateSolidBrush(itemHover);
            root.Resources["SubtleCardBrush"] = CreateSolidBrush(subtleCard);
            root.Resources["AccentPrimaryBrush"] = CreateSolidBrush(accentPrimary);
            root.Resources["AccentHoverBrush"] = CreateSolidBrush(accentHover);
        }

        private static SolidColorBrush CreateSolidBrush(string hex)
        {
            var color = (Color)ColorConverter.ConvertFromString(hex);
            var brush = new SolidColorBrush(color);
            brush.Freeze();
            return brush;
        }

        public static bool IsWindowsInDarkTheme()
        {
            try
            {
                using var key = Microsoft.Win32.Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize");
                if (key != null)
                {
                    object? val = key.GetValue("AppsUseLightTheme");
                    if (val is int intVal)
                    {
                        return intVal == 0;
                    }
                }
            }
            catch { }
            return false;
        }
    }
}

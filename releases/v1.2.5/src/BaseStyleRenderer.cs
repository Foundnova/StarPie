using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;
using Brush = System.Windows.Media.Brush;
using Color = System.Windows.Media.Color;
using ColorConverter = System.Windows.Media.ColorConverter;

namespace WinPieGestures
{
    public abstract class BaseStyleRenderer : IRadialStyleRenderer
    {
        public Brush DefaultSectorBrush { get; protected set; }
        public Brush HighlightSectorBrush { get; protected set; }
        public Brush SectorBorderBrush { get; protected set; }
        public Brush HighlightBorderBrush { get; protected set; }
        public Brush TextColorBrush { get; protected set; }
        public Brush CoreBgBrush { get; protected set; }
        public Brush CoreBorderBrush { get; protected set; }

        public double BorderThickness { get; protected set; } = 1.0;
        public double HighlightBorderThickness { get; protected set; } = 1.5;

        public virtual void Initialize(string theme, AppConfig config)
        {
            BorderThickness = 1.0;
            HighlightBorderThickness = 1.5;

            string sectorBgHex, sectorBorderHex, highlightBgHex, highlightBorderHex, textHex;
            GetDefaultColors(theme, out sectorBgHex, out sectorBorderHex, out highlightBgHex, out highlightBorderHex, out textHex);

            string coreBgHex = sectorBgHex;
            string coreBorderHex = sectorBorderHex;

            if (theme == "System")
            {
                int appsUseLightTheme = 0;
                try
                {
                    appsUseLightTheme = (int)(Microsoft.Win32.Registry.GetValue(
                        @"HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\Themes\Personalize",
                        "AppsUseLightTheme", 1) ?? 1);
                }
                catch { }

                theme = appsUseLightTheme == 0 ? "Dark" : "Light";
            }

            if (theme == "Light" && UseStandardLightThemeFallback())
            {
                sectorBgHex = "#F0F8FAFC";
                sectorBorderHex = "#3064748B";
                highlightBgHex = "#FF2563EB";
                highlightBorderHex = "#FF60A5FA";
                textHex = "#FF0F172A";
                coreBgHex = "#FFF8FAFC";
                coreBorderHex = "#3064748B";
            }
            else if (theme == "CyberNeon")
            {
                sectorBgHex = "#E60F172A";
                sectorBorderHex = "#6038BDF8";
                highlightBgHex = "#FF06B6D4";
                highlightBorderHex = "#FFF43F5E";
                textHex = "#FFF8FAFC";
                coreBgHex = "#F00F172A";
                coreBorderHex = "#6038BDF8";
            }
            else if (theme == "SunsetAurora")
            {
                sectorBgHex = "#E61E1B4B";
                sectorBorderHex = "#50F43F5E";
                highlightBgHex = "#FFF97316";
                highlightBorderHex = "#FFFDE047";
                textHex = "#FFFFF7ED";
                coreBgHex = "#F01E1B4B";
                coreBorderHex = "#50F43F5E";
            }
            else if (theme == "MatchaForest")
            {
                sectorBgHex = "#E6142E1F";
                sectorBorderHex = "#4034D399";
                highlightBgHex = "#FF10B981";
                highlightBorderHex = "#FF6EE7B7";
                textHex = "#FFF0FDF4";
                coreBgHex = "#F0142E1F";
                coreBorderHex = "#4034D399";
            }
            else if (theme == "VolcanoEmber")
            {
                sectorBgHex = "#E6181111";
                sectorBorderHex = "#50EF4444";
                highlightBgHex = "#FFDC2626";
                highlightBorderHex = "#FFFBBF24";
                textHex = "#FFFEE2E2";
                coreBgHex = "#F0181111";
                coreBorderHex = "#50EF4444";
            }
            else if (theme == "RoyalViolet")
            {
                sectorBgHex = "#E62E1065";
                sectorBorderHex = "#50C084FC";
                highlightBgHex = "#FF9333EA";
                highlightBorderHex = "#FFE879F9";
                textHex = "#FFFAF5FF";
                coreBgHex = "#F02E1065";
                coreBorderHex = "#50C084FC";
            }
            else if (theme == "GlacialIce")
            {
                sectorBgHex = "#E0E0F2FE";
                sectorBorderHex = "#6038BDF8";
                highlightBgHex = "#FF0284C7";
                highlightBorderHex = "#FFBAE6FD";
                textHex = "#FF0C4A6E";
                coreBgHex = "#F0E0F2FE";
                coreBorderHex = "#6038BDF8";
            }
            else if (theme == "MorandiMuted")
            {
                sectorBgHex = "#E62C302E";
                sectorBorderHex = "#409CA3AF";
                highlightBgHex = "#FF78716C";
                highlightBorderHex = "#FFD6D3D1";
                textHex = "#FFF5F5F4";
                coreBgHex = "#F02C302E";
                coreBorderHex = "#409CA3AF";
            }
            else if (theme == "Custom")
            {
                sectorBgHex = config.CustomSectorBg ?? sectorBgHex;
                sectorBorderHex = config.CustomSectorBorder ?? sectorBorderHex;
                highlightBgHex = config.CustomHighlightBg ?? highlightBgHex;
                highlightBorderHex = config.CustomHighlightBorder ?? highlightBorderHex;
                textHex = config.CustomText ?? textHex;
                coreBgHex = sectorBgHex;
                coreBorderHex = sectorBorderHex;
            }

            coreBgHex = sectorBgHex;
            coreBorderHex = sectorBorderHex;

            try
            {
                DefaultSectorBrush = CreateSolidBrush(sectorBgHex);
                HighlightSectorBrush = CreateSolidBrush(highlightBgHex);
                SectorBorderBrush = CreateSolidBrush(sectorBorderHex);
                HighlightBorderBrush = CreateSolidBrush(highlightBorderHex);
                TextColorBrush = CreateSolidBrush(textHex);
                CoreBgBrush = CreateSolidBrush(coreBgHex);
                CoreBorderBrush = CreateSolidBrush(coreBorderHex);
            }
            catch
            {
                DefaultSectorBrush = CreateSolidBrush("#E618181B");
                HighlightSectorBrush = CreateSolidBrush("#FF3B82F6");
                SectorBorderBrush = CreateSolidBrush("#35FFFFFF");
                HighlightBorderBrush = CreateSolidBrush("#A0FFFFFF");
                TextColorBrush = CreateSolidBrush("#F8FAFC");
                CoreBgBrush = CreateSolidBrush("#F018181B");
                CoreBorderBrush = CreateSolidBrush("#30FFFFFF");
            }

            PostInitialize();
        }

        protected SolidColorBrush CreateSolidBrush(string hex)
        {
            return new SolidColorBrush((Color)ColorConverter.ConvertFromString(hex));
        }

        protected virtual void GetDefaultColors(string theme, out string sectorBgHex, out string sectorBorderHex, out string highlightBgHex, out string highlightBorderHex, out string textHex)
        {
            // Modern Dark Neutral Base with Electric Blue Accent
            sectorBgHex = "#EB18181B";     // Dark slate-zinc
            sectorBorderHex = "#30FFFFFF"; // Subtle hairline
            highlightBgHex = "#FF2563EB";  // Pure vivid Cobalt/Blue
            highlightBorderHex = "#FF60A5FA";
            textHex = "#FFF8FAFC";
        }

        protected virtual bool UseStandardLightThemeFallback()
        {
            return true;
        }

        protected virtual void PostInitialize()
        {
        }

        public abstract void RenderDecorations(Canvas canvas, Grid coreGrid, double cx, double cy, double wheelRadius, double coreRadius, int insertIndex);

        public virtual void ApplySectorHighlight(Path path, bool isHighlighted)
        {
        }

        public virtual void ApplyExitHighlight(Path exitIcon, bool isHighlighted)
        {
        }
    }
}

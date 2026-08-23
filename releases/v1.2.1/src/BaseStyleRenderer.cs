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

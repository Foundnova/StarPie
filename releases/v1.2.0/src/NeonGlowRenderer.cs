using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Effects;
using System.Windows.Shapes;
using HorizontalAlignment = System.Windows.HorizontalAlignment;
using Panel = System.Windows.Controls.Panel;
using Color = System.Windows.Media.Color;

namespace WinPieGestures
{
    /// <summary>
    /// Neon Glow / Cyber Tech Style: High-tech HUD telemetry, dashed telemetry rings, laser cyan glow.
    /// </summary>
    public class NeonGlowRenderer : BaseStyleRenderer
    {
        protected override void GetDefaultColors(string theme, out string sectorBgHex, out string sectorBorderHex, out string highlightBgHex, out string highlightBorderHex, out string textHex)
        {
            sectorBgHex = "#F00B101B";     // Cyber midnight
            sectorBorderHex = "#4006B6D4"; // Neon cyan border line
            highlightBgHex = "#FF0891B2";  // Laser cyan
            highlightBorderHex = "#FF22D3EE";
            textHex = "#FFECFEFF";         // Bright cyan white
        }

        protected override bool UseStandardLightThemeFallback()
        {
            return false;
        }

        public override void RenderDecorations(Canvas canvas, Grid coreGrid, double cx, double cy, double wheelRadius, double coreRadius, int insertIndex)
        {
            // 1. Concentric dashed telemetry ring
            var dashRing = new Ellipse
            {
                Width = wheelRadius * 2.0 + 12.0,
                Height = wheelRadius * 2.0 + 12.0,
                Stroke = new SolidColorBrush(Color.FromArgb(80, 6, 182, 212)),
                StrokeThickness = 1.0,
                StrokeDashArray = new DoubleCollection { 6, 4 },
                Tag = "Deco_TechDashRing"
            };
            Canvas.SetLeft(dashRing, cx - (wheelRadius + 6.0));
            Canvas.SetTop(dashRing, cy - (wheelRadius + 6.0));
            Panel.SetZIndex(dashRing, 1);
            canvas.Children.Add(dashRing);

            // 2. Cardinal tick marks (0, 90, 180, 270 deg)
            for (int i = 0; i < 4; i++)
            {
                double angle = i * 90 * (Math.PI / 180.0);
                double tickX = cx + (wheelRadius + 14.0) * Math.Cos(angle);
                double tickY = cy + (wheelRadius + 14.0) * Math.Sin(angle);

                var tick = new Ellipse
                {
                    Width = 3,
                    Height = 3,
                    Fill = HighlightBorderBrush,
                    Tag = "Deco_TechTick"
                };
                Canvas.SetLeft(tick, tickX - 1.5);
                Canvas.SetTop(tick, tickY - 1.5);
                Panel.SetZIndex(tick, 2);
                canvas.Children.Add(tick);
            }

            // 3. Tech radar crosshairs inside the core grid
            var radarGrid = new Grid
            {
                Name = "DynamicTechGrid",
                Width = coreRadius * 2.0,
                Height = coreRadius * 2.0
            };
            var hLine = new Line { X1 = 0, Y1 = coreRadius, X2 = coreRadius * 2, Y2 = coreRadius, Stroke = HighlightBorderBrush, StrokeThickness = 0.6, Opacity = 0.5 };
            var vLine = new Line { X1 = coreRadius, Y1 = 0, X2 = coreRadius, Y2 = coreRadius * 2, Stroke = HighlightBorderBrush, StrokeThickness = 0.6, Opacity = 0.5 };
            var tCircle = new Ellipse { Width = coreRadius * 1.35, Height = coreRadius * 1.35, Stroke = HighlightBorderBrush, StrokeThickness = 0.6, Opacity = 0.4, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = System.Windows.VerticalAlignment.Center };

            radarGrid.Children.Add(hLine);
            radarGrid.Children.Add(vLine);
            radarGrid.Children.Add(tCircle);
            coreGrid.Children.Insert(insertIndex, radarGrid);
        }

        public override void ApplySectorHighlight(Path path, bool isHighlighted)
        {
            if (isHighlighted && HighlightSectorBrush is SolidColorBrush solidHighlight)
            {
                path.Effect = new DropShadowEffect
                {
                    Color = Color.FromRgb(6, 182, 212),
                    BlurRadius = 24,
                    ShadowDepth = 0,
                    Opacity = 0.85
                };
            }
            else
            {
                path.Effect = null;
            }
        }

        public override void ApplyExitHighlight(Path exitIcon, bool isHighlighted)
        {
            if (isHighlighted)
            {
                exitIcon.Effect = new DropShadowEffect
                {
                    Color = Color.FromRgb(244, 63, 94), // Rose cancel laser
                    BlurRadius = 15,
                    ShadowDepth = 0,
                    Opacity = 0.9
                };
            }
            else
            {
                exitIcon.Effect = null;
            }
        }
    }
}

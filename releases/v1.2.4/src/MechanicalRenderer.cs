using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;
using Point = System.Windows.Point;
using Panel = System.Windows.Controls.Panel;
using Color = System.Windows.Media.Color;

namespace WinPieGestures
{
    /// <summary>
    /// Mechanical / Industrial Steampunk Style: Titanium gunmetal steel, 16 hex rivets, precision center cog.
    /// </summary>
    public class MechanicalRenderer : BaseStyleRenderer
    {
        protected override void GetDefaultColors(string theme, out string sectorBgHex, out string sectorBorderHex, out string highlightBgHex, out string highlightBorderHex, out string textHex)
        {
            if (theme != "Custom")
            {
                sectorBgHex = "#EB22252A";     // Deep gunmetal steel
                sectorBorderHex = "#6064748B"; // Steel alloy border
                highlightBgHex = "#FFD97706";  // Amber brass / Industrial gold
                highlightBorderHex = "#FFFBBF24";
                textHex = "#FFF1F5F9";
            }
            else
            {
                base.GetDefaultColors(theme, out sectorBgHex, out sectorBorderHex, out highlightBgHex, out highlightBorderHex, out textHex);
            }
        }

        protected override bool UseStandardLightThemeFallback()
        {
            return false;
        }

        public override void RenderDecorations(Canvas canvas, Grid coreGrid, double cx, double cy, double wheelRadius, double coreRadius, int insertIndex)
        {
            // Hide the default circular CoreEllipse
            foreach (UIElement child in coreGrid.Children)
            {
                if (child is Ellipse ellipse && ellipse.Name == "CoreEllipse")
                {
                    ellipse.Visibility = Visibility.Collapsed;
                    break;
                }
            }

            // Create Precision Cog Gear Geometry in the center
            var gearGeometry = CreateGearGeometry(coreRadius, coreRadius, coreRadius * 0.84, coreRadius, 14);
            var gearPath = new Path
            {
                Name = "DynamicGearPath",
                Data = gearGeometry,
                Fill = CoreBgBrush,
                Stroke = CoreBorderBrush,
                StrokeThickness = 1.5
            };
            coreGrid.Children.Insert(insertIndex, gearPath);

            // Draw 16 hex rivets around the outer rim
            int rivetCount = 16;
            for (int i = 0; i < rivetCount; i++)
            {
                double angle = (360.0 / rivetCount) * i * (Math.PI / 180.0);
                double rx = cx + Math.Cos(angle) * (wheelRadius + 4.0);
                double ry = cy + Math.Sin(angle) * (wheelRadius + 4.0);

                var rivet = new Ellipse
                {
                    Width = 4.5,
                    Height = 4.5,
                    Fill = new SolidColorBrush(Color.FromArgb(200, 148, 163, 184)),
                    Stroke = new SolidColorBrush(Color.FromArgb(255, 30, 41, 59)),
                    StrokeThickness = 0.8,
                    Tag = "Deco_Rivet"
                };
                Canvas.SetLeft(rivet, rx - 2.25);
                Canvas.SetTop(rivet, ry - 2.25);
                Panel.SetZIndex(rivet, 0);
                canvas.Children.Add(rivet);
            }
        }

        private Geometry CreateGearGeometry(double cx, double cy, double innerR, double outerR, int teethCount)
        {
            var geometry = new StreamGeometry();
            using (StreamGeometryContext ctx = geometry.Open())
            {
                double angleStep = 360.0 / (teethCount * 2);
                bool isOuter = true;
                double r = outerR;
                double firstX = cx + r;
                double firstY = cy;
                ctx.BeginFigure(new Point(firstX, firstY), isFilled: true, isClosed: true);

                for (int i = 1; i <= teethCount * 2; i++)
                {
                    double angleRad = i * angleStep * (Math.PI / 180.0);
                    r = isOuter ? innerR : outerR;
                    ctx.LineTo(new Point(cx + Math.Cos(angleRad) * r, cy + Math.Sin(angleRad) * r), isStroked: true, isSmoothJoin: false);
                    isOuter = !isOuter;
                }
            }
            geometry.Freeze();
            return geometry;
        }
    }
}

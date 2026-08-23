using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;
using Panel = System.Windows.Controls.Panel;

namespace WinPieGestures
{
    /// <summary>
    /// Classic Ring Style: Crisp concentric rings, polished dark slate palette, high-contrast azure accent.
    /// </summary>
    public class ClassicRingRenderer : BaseStyleRenderer
    {
        protected override void GetDefaultColors(string theme, out string sectorBgHex, out string sectorBorderHex, out string highlightBgHex, out string highlightBorderHex, out string textHex)
        {
            sectorBgHex = "#EE18181B";     // Deep obsidian zinc
            sectorBorderHex = "#35FFFFFF"; // Fine crisp ring border
            highlightBgHex = "#FF2563EB";  // Pure vivid Cobalt Blue
            highlightBorderHex = "#FF93C5FD";
            textHex = "#FFF8FAFC";
        }

        public override void RenderDecorations(Canvas canvas, Grid coreGrid, double cx, double cy, double wheelRadius, double coreRadius, int insertIndex)
        {
            // Subtle concentric accent ring around core
            var innerRing = new Ellipse
            {
                Width = coreRadius * 2.0 + 8.0,
                Height = coreRadius * 2.0 + 8.0,
                Stroke = new SolidColorBrush(System.Windows.Media.Color.FromArgb(40, 255, 255, 255)),
                StrokeThickness = 1.0,
                Tag = "Deco_ClassicInnerRing"
            };
            Canvas.SetLeft(innerRing, cx - (coreRadius + 4.0));
            Canvas.SetTop(innerRing, cy - (coreRadius + 4.0));
            Panel.SetZIndex(innerRing, 0);
            canvas.Children.Add(innerRing);
        }
    }
}

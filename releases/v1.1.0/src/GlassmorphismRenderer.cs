using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Effects;
using System.Windows.Shapes;
using Color = System.Windows.Media.Color;
using Panel = System.Windows.Controls.Panel;

namespace WinPieGestures
{
    /// <summary>
    /// Glassmorphism Style: True frosted acrylic glass with inner specular refraction and soft glow.
    /// </summary>
    public class GlassmorphismRenderer : BaseStyleRenderer
    {
        protected override void GetDefaultColors(string theme, out string sectorBgHex, out string sectorBorderHex, out string highlightBgHex, out string highlightBorderHex, out string textHex)
        {
            if (theme == "Light")
            {
                sectorBgHex = "#40FFFFFF";
                sectorBorderHex = "#80FFFFFF";
                highlightBgHex = "#D03B82F6";
                highlightBorderHex = "#E093C5FD";
                textHex = "#FF0F172A";
            }
            else
            {
                sectorBgHex = "#45182030";     // Translucent dark acrylic
                sectorBorderHex = "#60FFFFFF"; // Glass specular rim
                highlightBgHex = "#D82563EB";  // Frosted royal blue
                highlightBorderHex = "#B093C5FD";
                textHex = "#FFF8FAFC";
            }
        }

        protected override void PostInitialize()
        {
            BorderThickness = 1.5;
            HighlightBorderThickness = 2.2;
        }

        public override void RenderDecorations(Canvas canvas, Grid coreGrid, double cx, double cy, double wheelRadius, double coreRadius, int insertIndex)
        {
            // Frosted outer halo ring
            var glassHalo = new Ellipse
            {
                Width = wheelRadius * 2.0 + 4.0,
                Height = wheelRadius * 2.0 + 4.0,
                Stroke = new SolidColorBrush(Color.FromArgb(30, 255, 255, 255)),
                StrokeThickness = 1.0,
                Tag = "Deco_GlassHalo"
            };
            Canvas.SetLeft(glassHalo, cx - (wheelRadius + 2.0));
            Canvas.SetTop(glassHalo, cy - (wheelRadius + 2.0));
            Panel.SetZIndex(glassHalo, 0);
            canvas.Children.Add(glassHalo);
        }

        public override void ApplySectorHighlight(Path path, bool isHighlighted)
        {
            if (isHighlighted)
            {
                path.Effect = new DropShadowEffect
                {
                    Color = Color.FromRgb(59, 130, 246),
                    BlurRadius = 18,
                    ShadowDepth = 0,
                    Opacity = 0.6
                };
            }
            else
            {
                path.Effect = null;
            }
        }
    }
}

using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Effects;
using System.Windows.Shapes;
using Color = System.Windows.Media.Color;
using Panel = System.Windows.Controls.Panel;
using HorizontalAlignment = System.Windows.HorizontalAlignment;

namespace WinPieGestures
{
    /// <summary>
    /// Cat Paw Style: Adorable pastel sakura aesthetics, smooth 3D gradient cat ears and soft toe cushions.
    /// </summary>
    public class CatPawRenderer : BaseStyleRenderer
    {
        protected override void GetDefaultColors(string theme, out string sectorBgHex, out string sectorBorderHex, out string highlightBgHex, out string highlightBorderHex, out string textHex)
        {
            if (theme != "Custom")
            {
                sectorBgHex = "#F8FFF7F7";     // Warm creamy peach
                sectorBorderHex = "#45F472B6"; // Soft pink border
                highlightBgHex = "#FFF472B6";  // Sweet Sakura Pink
                highlightBorderHex = "#FFFBCFE8";
                textHex = "#FF4C1D24";         // Warm cocoa text
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
            // Cat ears geometry (Top-Left and Top-Right)
            double earSize = coreRadius * 0.72;
            
            // Left Ear
            double leftCenterRad = 238 * Math.PI / 180.0;
            double leftBase1Rad = 224 * Math.PI / 180.0;
            double leftBase2Rad = 252 * Math.PI / 180.0;

            double lx1 = cx + wheelRadius * Math.Cos(leftBase1Rad);
            double ly1 = cy + wheelRadius * Math.Sin(leftBase1Rad);
            double lx2 = cx + (wheelRadius + earSize) * Math.Cos(leftCenterRad);
            double ly2 = cy + (wheelRadius + earSize) * Math.Sin(leftCenterRad);
            double lx3 = cx + wheelRadius * Math.Cos(leftBase2Rad);
            double ly3 = cy + wheelRadius * Math.Sin(leftBase2Rad);

            var leftEar = new Path
            {
                Data = Geometry.Parse($"M {lx1},{ly1} L {lx2},{ly2} L {lx3},{ly3} Z"),
                Fill = DefaultSectorBrush,
                Stroke = CoreBorderBrush,
                StrokeThickness = 1.5,
                Tag = "Deco_LeftEar"
            };
            Panel.SetZIndex(leftEar, 2);
            canvas.Children.Add(leftEar);

            // Right Ear
            double rightCenterRad = 302 * Math.PI / 180.0;
            double rightBase1Rad = 288 * Math.PI / 180.0;
            double rightBase2Rad = 316 * Math.PI / 180.0;

            double rx1 = cx + wheelRadius * Math.Cos(rightBase1Rad);
            double ry1 = cy + wheelRadius * Math.Sin(rightBase1Rad);
            double rx2 = cx + (wheelRadius + earSize) * Math.Cos(rightCenterRad);
            double ry2 = cy + (wheelRadius + earSize) * Math.Sin(rightCenterRad);
            double rx3 = cx + wheelRadius * Math.Cos(rightBase2Rad);
            double ry3 = cy + wheelRadius * Math.Sin(rightBase2Rad);

            var rightEar = new Path
            {
                Data = Geometry.Parse($"M {rx1},{ry1} L {rx2},{ry2} L {rx3},{ry3} Z"),
                Fill = DefaultSectorBrush,
                Stroke = CoreBorderBrush,
                StrokeThickness = 1.5,
                Tag = "Deco_RightEar"
            };
            Panel.SetZIndex(rightEar, 2);
            canvas.Children.Add(rightEar);

            // Inner Ear Pink Highlights
            var leftInner = new Path
            {
                Data = Geometry.Parse($"M {lx1*0.7+lx2*0.3},{ly1*0.7+ly2*0.3} L {lx2*0.9+lx1*0.05+lx3*0.05},{ly2*0.9+ly1*0.05+ly3*0.05} L {lx3*0.7+lx2*0.3},{ly3*0.7+ly2*0.3} Z"),
                Fill = new SolidColorBrush(Color.FromArgb(140, 244, 114, 182)),
                Tag = "Deco_LeftInnerEar"
            };
            Panel.SetZIndex(leftInner, 3);
            canvas.Children.Add(leftInner);

            var rightInner = new Path
            {
                Data = Geometry.Parse($"M {rx1*0.7+rx2*0.3},{ry1*0.7+ry2*0.3} L {rx2*0.9+rx1*0.05+rx3*0.05},{ry2*0.9+ry1*0.05+ry3*0.05} L {rx3*0.7+rx2*0.3},{ry3*0.7+ry2*0.3} Z"),
                Fill = new SolidColorBrush(Color.FromArgb(140, 244, 114, 182)),
                Tag = "Deco_RightInnerEar"
            };
            Panel.SetZIndex(rightInner, 3);
            canvas.Children.Add(rightInner);

            // 3D-styled cute paw print in center core
            var pawPrint = new Grid
            {
                Name = "DynamicPawGrid",
                Width = coreRadius * 2.0,
                Height = coreRadius * 2.0,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            };

            var pad = new Ellipse
            {
                Width = coreRadius * 0.72,
                Height = coreRadius * 0.52,
                Fill = new SolidColorBrush(Color.FromArgb(200, 244, 114, 182)), // Soft pink main pad
                Margin = new Thickness(0, coreRadius * 0.48, 0, 0),
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Top
            };
            pawPrint.Children.Add(pad);

            double toeSize = coreRadius * 0.22;
            double toeY = coreRadius * 0.23;
            var toe1 = new Ellipse { Width = toeSize, Height = toeSize, Fill = pad.Fill, Margin = new Thickness(coreRadius * 0.32, toeY + 2, 0, 0), HorizontalAlignment = HorizontalAlignment.Left, VerticalAlignment = VerticalAlignment.Top };
            var toe2 = new Ellipse { Width = toeSize, Height = toeSize, Fill = pad.Fill, Margin = new Thickness(coreRadius * 0.66, toeY - 4, 0, 0), HorizontalAlignment = HorizontalAlignment.Left, VerticalAlignment = VerticalAlignment.Top };
            var toe3 = new Ellipse { Width = toeSize, Height = toeSize, Fill = pad.Fill, Margin = new Thickness(coreRadius * 1.04, toeY - 4, 0, 0), HorizontalAlignment = HorizontalAlignment.Left, VerticalAlignment = VerticalAlignment.Top };
            var toe4 = new Ellipse { Width = toeSize, Height = toeSize, Fill = pad.Fill, Margin = new Thickness(coreRadius * 1.38, toeY + 2, 0, 0), HorizontalAlignment = HorizontalAlignment.Left, VerticalAlignment = VerticalAlignment.Top };

            pawPrint.Children.Add(toe1);
            pawPrint.Children.Add(toe2);
            pawPrint.Children.Add(toe3);
            pawPrint.Children.Add(toe4);

            coreGrid.Children.Insert(insertIndex, pawPrint);
        }

        public override void ApplySectorHighlight(Path path, bool isHighlighted)
        {
            if (isHighlighted)
            {
                path.Effect = new DropShadowEffect
                {
                    Color = Color.FromRgb(244, 114, 182),
                    BlurRadius = 14,
                    ShadowDepth = 0,
                    Opacity = 0.5
                };
            }
            else
            {
                path.Effect = null;
            }
        }
    }
}

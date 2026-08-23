using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Shapes;
using Point = System.Windows.Point;
using Brush = System.Windows.Media.Brush;
using Color = System.Windows.Media.Color;
using ColorConverter = System.Windows.Media.ColorConverter;
using Size = System.Windows.Size;
using Brushes = System.Windows.Media.Brushes;

namespace WinPieGestures
{
    public partial class RadialWindow : Window
    {
        private readonly Point _centerPoint;
        private readonly WheelProfile _profile;
        private readonly List<Path> _sectorPaths = new List<Path>();
        private readonly List<TextBlock> _labels = new List<TextBlock>();

        // Styling brushes
        private static readonly Brush DefaultSectorBrush = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#9016161A"));
        private static readonly Brush HighlightSectorBrush = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#E06C4DFF")); // Premium purple
        private static readonly Brush SectorBorderBrush = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#35FFFFFF"));
        private static readonly Brush HighlightBorderBrush = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#A0FFFFFF"));

        public RadialWindow(Point centerPoint, WheelProfile profile)
        {
            InitializeComponent();

            _centerPoint = centerPoint;
            _profile = profile;

            // Load event to position the window and render sectors
            Loaded += RadialWindow_Loaded;

            CoreTitle.Text = profile.ProcessName == "Global" ? "全局动作" : profile.ProcessName;
            CoreSubtitle.Text = $"{profile.SectorCount} 键笔势";
        }

        private void RadialWindow_Loaded(object sender, RoutedEventArgs e)
        {
            // Position the window centered on the mouse click coordinates, accounting for DPI scaling
            double scaleX = 1.0;
            double scaleY = 1.0;

            var source = PresentationSource.FromVisual(this);
            if (source?.CompositionTarget != null)
            {
                scaleX = source.CompositionTarget.TransformToDevice.M11;
                scaleY = source.CompositionTarget.TransformToDevice.M22;
            }

            // Set window position in WPF units (device-independent pixels)
            this.Left = (_centerPoint.X / scaleX) - (this.Width / 2);
            this.Top = (_centerPoint.Y / scaleY) - (this.Height / 2);

            RenderSectors();

            // Run open scale-in and fade-in animation
            var sb = new Storyboard();
            
            var scaleXAnim = new DoubleAnimation(0.4, 1.0, new Duration(TimeSpan.FromMilliseconds(90)))
            {
                DecelerationRatio = 0.8
            };
            Storyboard.SetTarget(scaleXAnim, MainGrid);
            Storyboard.SetTargetProperty(scaleXAnim, new PropertyPath("RenderTransform.Children[0].ScaleX"));

            var scaleYAnim = new DoubleAnimation(0.4, 1.0, new Duration(TimeSpan.FromMilliseconds(90)))
            {
                DecelerationRatio = 0.8
            };
            Storyboard.SetTarget(scaleYAnim, MainGrid);
            Storyboard.SetTargetProperty(scaleYAnim, new PropertyPath("RenderTransform.Children[0].ScaleY"));

            var opacityAnim = new DoubleAnimation(0.0, 1.0, new Duration(TimeSpan.FromMilliseconds(80)));
            Storyboard.SetTarget(opacityAnim, MainGrid);
            Storyboard.SetTargetProperty(opacityAnim, new PropertyPath(Window.OpacityProperty));

            sb.Children.Add(scaleXAnim);
            sb.Children.Add(scaleYAnim);
            sb.Children.Add(opacityAnim);
            sb.Begin();
        }

        private void RenderSectors()
        {
            int n = _profile.SectorCount;
            double sectorSize = 360.0 / n;

            // Clear previous elements if any
            _sectorPaths.Clear();
            _labels.Clear();

            for (int i = 0; i < n; i++)
            {
                // Each sector occupies start to end degrees
                // Shift half a sector back to center sector 0 at 0 degrees (Right)
                double midAngle = i * sectorSize;
                double startAngle = midAngle - (sectorSize / 2);
                double endAngle = midAngle + (sectorSize / 2);

                // Create sector path shape (inner radius 52, outer radius 138)
                var geometry = CreateSectorGeometry(startAngle, endAngle, 52, 138);

                var path = new Path
                {
                    Data = geometry,
                    Fill = DefaultSectorBrush,
                    Stroke = SectorBorderBrush,
                    StrokeThickness = 1,
                    Tag = i
                };

                // Add to canvas under the center core (which is at index 0 in Canvas or we add path at index 0)
                WheelCanvas.Children.Insert(0, path);
                _sectorPaths.Add(path);

                // Add Label
                double midRad = midAngle * (Math.PI / 180.0);
                double labelRadius = 92.0; // Place label in the center of the ring slice
                double lx = 150.0 + Math.Cos(midRad) * labelRadius;
                double ly = 150.0 + Math.Sin(midRad) * labelRadius;

                // Load action text
                string actionText = "未设置";
                if (i < _profile.Actions.Count && _profile.Actions[i] != null)
                {
                    actionText = _profile.Actions[i].Name;
                }

                var textBlock = new TextBlock
                {
                    Text = actionText,
                    Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#E0FFFFFF")),
                    FontSize = 10,
                    FontWeight = FontWeights.Medium,
                    TextAlignment = TextAlignment.Center,
                    TextWrapping = TextWrapping.Wrap,
                    Width = 75,
                    Height = 30,
                    Effect = (System.Windows.Media.Effects.Effect)Resources["TextShadow"]
                };

                // Position centered on (lx, ly)
                Canvas.SetLeft(textBlock, lx - textBlock.Width / 2);
                Canvas.SetTop(textBlock, ly - textBlock.Height / 2);

                WheelCanvas.Children.Add(textBlock);
                _labels.Add(textBlock);
            }
        }

        private Geometry CreateSectorGeometry(double startAngleDegrees, double endAngleDegrees, double innerRadius, double outerRadius)
        {
            double startRad = startAngleDegrees * (Math.PI / 180.0);
            double endRad = endAngleDegrees * (Math.PI / 180.0);

            double cx = 150.0;
            double cy = 150.0;

            Point p1 = new Point(cx + Math.Cos(startRad) * outerRadius, cy + Math.Sin(startRad) * outerRadius);
            Point p2 = new Point(cx + Math.Cos(endRad) * outerRadius, cy + Math.Sin(endRad) * outerRadius);
            Point p3 = new Point(cx + Math.Cos(endRad) * innerRadius, cy + Math.Sin(endRad) * innerRadius);
            Point p4 = new Point(cx + Math.Cos(startRad) * innerRadius, cy + Math.Sin(startRad) * innerRadius);

            bool isLargeArc = Math.Abs(endAngleDegrees - startAngleDegrees) > 180.0;

            var geometry = new StreamGeometry();
            using (StreamGeometryContext ctx = geometry.Open())
            {
                ctx.BeginFigure(p1, isFilled: true, isClosed: true);
                ctx.ArcTo(p2, new Size(outerRadius, outerRadius), rotationAngle: 0, isLargeArc: isLargeArc, sweepDirection: SweepDirection.Clockwise, isStroked: true, isSmoothJoin: false);
                ctx.LineTo(p3, isStroked: true, isSmoothJoin: false);
                ctx.ArcTo(p4, new Size(innerRadius, innerRadius), rotationAngle: 0, isLargeArc: isLargeArc, sweepDirection: SweepDirection.Counterclockwise, isStroked: true, isSmoothJoin: false);
            }
            geometry.Freeze();
            return geometry;
        }

        public void HighlightSector(int index)
        {
            for (int i = 0; i < _sectorPaths.Count; i++)
            {
                var path = _sectorPaths[i];
                var label = _labels[i];

                if (i == index)
                {
                    path.Fill = HighlightSectorBrush;
                    path.Stroke = HighlightBorderBrush;
                    path.StrokeThickness = 1.5;
                    label.Foreground = Brushes.White;
                    label.FontWeight = FontWeights.Bold;
                }
                else
                {
                    path.Fill = DefaultSectorBrush;
                    path.Stroke = SectorBorderBrush;
                    path.StrokeThickness = 1;
                    label.Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#C0FFFFFF"));
                    label.FontWeight = FontWeights.Medium;
                }
            }
        }
    }
}

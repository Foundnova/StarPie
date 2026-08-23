using System;
using System.Collections.Generic;
using System.Linq;
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
        private readonly List<StackPanel> _contentPanels = new List<StackPanel>();
        private readonly List<TranslateTransform> _sectorTransforms = new List<TranslateTransform>();
        private readonly List<TranslateTransform> _containerTransforms = new List<TranslateTransform>();
        private readonly List<double> _sectorAngles = new List<double>();
        private IRadialStyleRenderer _styleRenderer;

        // Styling brushes and dimensions (instantiated dynamically)
        private Brush _defaultSectorBrush;
        private Brush _highlightSectorBrush;
        private Brush _sectorBorderBrush;
        private Brush _highlightBorderBrush;
        private Brush _textColorBrush;
        private Brush _coreBgBrush;
        private Brush _coreBorderBrush;

        private double _innerRadius = 52;
        private double _outerRadius = 138;
        private double _borderThickness = 1.0;
        private double _highlightBorderThickness = 1.5;

        public RadialWindow(Point centerPoint, WheelProfile profile)
        {
            InitializeComponent();

            _centerPoint = centerPoint;
            _profile = profile;

            InitializeThemeAndStyle();
            CoreTextPanel.Visibility = Visibility.Collapsed;

            // Load event to position the window and render sectors
            Loaded += RadialWindow_Loaded;

            CoreTitle.Text = profile.ProcessName == "Global" ? "全局动作" : profile.ProcessName;
            CoreSubtitle.Text = $"{profile.SectorCount} 键动作";
        }

        private void InitializeThemeAndStyle()
        {
            string theme = ConfigManager.CurrentConfig.Theme ?? "System";
            string style = ConfigManager.CurrentConfig.UiStyle ?? "ClassicRing";

            _innerRadius = ConfigManager.CurrentConfig.InnerRadius;
            _outerRadius = ConfigManager.CurrentConfig.WheelRadius;

            // Enforce basic safety boundary
            if (_innerRadius >= _outerRadius)
            {
                _innerRadius = Math.Max(0, _outerRadius - 20);
            }

            // Instantiate corresponding style renderer using the factory
            _styleRenderer = StyleRendererFactory.CreateRenderer(style);
            _styleRenderer.Initialize(theme, ConfigManager.CurrentConfig);

            // Fetch brushes and dimensions from style renderer
            _defaultSectorBrush = _styleRenderer.DefaultSectorBrush;
            _highlightSectorBrush = _styleRenderer.HighlightSectorBrush;
            _sectorBorderBrush = _styleRenderer.SectorBorderBrush;
            _highlightBorderBrush = _styleRenderer.HighlightBorderBrush;
            _textColorBrush = _styleRenderer.TextColorBrush;
            _coreBgBrush = _styleRenderer.CoreBgBrush;
            _coreBorderBrush = _styleRenderer.CoreBorderBrush;
            _borderThickness = _styleRenderer.BorderThickness;
            _highlightBorderThickness = _styleRenderer.HighlightBorderThickness;
        }

        private void RadialWindow_Loaded(object sender, RoutedEventArgs e)
        {
            double wheelRadius = ConfigManager.CurrentConfig.WheelRadius;
            double coreRadius = ConfigManager.CurrentConfig.CoreRadius;

            // Adjust window size dynamically based on outer radius
            double winSize = wheelRadius * 2.0 + 40.0; // Margin for shadow
            this.Width = winSize;
            this.Height = winSize;

            WheelCanvas.Width = winSize;
            WheelCanvas.Height = winSize;

            // Center core position dynamically
            double coreLeft = (winSize / 2.0) - coreRadius;
            double coreTop = (winSize / 2.0) - coreRadius;
            Canvas.SetLeft(CoreGrid, coreLeft);
            Canvas.SetTop(CoreGrid, coreTop);
            CoreGrid.Width = coreRadius * 2.0;
            CoreGrid.Height = coreRadius * 2.0;
            System.Windows.Controls.Panel.SetZIndex(CoreGrid, 5);

            // Outer Ellipse size
            OuterEllipse.Width = wheelRadius * 2.0 + 8.0;
            OuterEllipse.Height = wheelRadius * 2.0 + 8.0;

            // Position the window centered on the mouse click coordinates, accounting for DPI scaling
            double scaleX = 1.0;
            double scaleY = 1.0;

            var source = PresentationSource.FromVisual(this);
            if (source?.CompositionTarget != null)
            {
                scaleX = source.CompositionTarget.TransformToDevice.M11;
                scaleY = source.CompositionTarget.TransformToDevice.M22;
            }

            // Set window position in WPF units
            this.Left = (_centerPoint.X / scaleX) - (this.Width / 2);
            this.Top = (_centerPoint.Y / scaleY) - (this.Height / 2);

            // Apply core brushes
            CoreEllipse.Fill = _coreBgBrush;
            CoreEllipse.Stroke = _coreBorderBrush;
            CoreTitle.Foreground = _textColorBrush;
            CoreExitIcon.Fill = _textColorBrush;
            CoreExitIcon.Width = coreRadius * 0.45;
            CoreExitIcon.Height = coreRadius * 0.45;

            CoreTitle.FontSize = Math.Max(8.0, coreRadius / 5.0);
            CoreSubtitle.FontSize = Math.Max(6.0, coreRadius / 7.0);

            // Render style decorations first
            RenderStyleDecorations();

            RenderSectors();

            // Run open spring scale-in and fade-in animation
            var sb = new Storyboard();
            var backEase = new BackEase { EasingMode = EasingMode.EaseOut, Amplitude = 0.35 };
            
            var scaleXAnim = new DoubleAnimation(0.65, 1.0, new Duration(TimeSpan.FromMilliseconds(110)))
            {
                EasingFunction = backEase
            };
            Storyboard.SetTarget(scaleXAnim, MainGrid);
            Storyboard.SetTargetProperty(scaleXAnim, new PropertyPath("RenderTransform.Children[0].ScaleX"));

            var scaleYAnim = new DoubleAnimation(0.65, 1.0, new Duration(TimeSpan.FromMilliseconds(110)))
            {
                EasingFunction = backEase
            };
            Storyboard.SetTarget(scaleYAnim, MainGrid);
            Storyboard.SetTargetProperty(scaleYAnim, new PropertyPath("RenderTransform.Children[0].ScaleY"));

            var opacityAnim = new DoubleAnimation(0.0, 1.0, new Duration(TimeSpan.FromMilliseconds(90)));
            Storyboard.SetTarget(opacityAnim, MainGrid);
            Storyboard.SetTargetProperty(opacityAnim, new PropertyPath(Window.OpacityProperty));

            sb.Children.Add(scaleXAnim);
            sb.Children.Add(scaleYAnim);
            sb.Children.Add(opacityAnim);
            sb.Begin();
        }

        private void RenderStyleDecorations()
        {
            string style = ConfigManager.CurrentConfig.UiStyle ?? "ClassicRing";
            double winSize = this.Width;
            double cx = winSize / 2.0;
            double cy = winSize / 2.0;
            double wheelRadius = ConfigManager.CurrentConfig.WheelRadius;
            double coreRadius = ConfigManager.CurrentConfig.CoreRadius;

            // Clear previous style decoration paths
            var toRemove = new List<UIElement>();
            foreach (UIElement child in WheelCanvas.Children)
            {
                if (child is FrameworkElement fe && fe.Tag != null && fe.Tag.ToString().StartsWith("Deco_"))
                {
                    toRemove.Add(child);
                }
            }
            foreach (var elem in toRemove)
            {
                WheelCanvas.Children.Remove(elem);
            }

            // Reset core visuals
            CoreEllipse.Visibility = Visibility.Visible;
            OuterEllipse.Visibility = Visibility.Collapsed;

            // Remove any dynamically added grids or paths inside CoreGrid
            var gear = CoreGrid.Children.OfType<Path>().FirstOrDefault(p => p.Name == "DynamicGearPath");
            if (gear != null) CoreGrid.Children.Remove(gear);

            var paw = CoreGrid.Children.OfType<Grid>().FirstOrDefault(g => g.Name == "DynamicPawGrid");
            if (paw != null) CoreGrid.Children.Remove(paw);

            var tech = CoreGrid.Children.OfType<Grid>().FirstOrDefault(g => g.Name == "DynamicTechGrid");
            if (tech != null) CoreGrid.Children.Remove(tech);

            // Determine insert position behind text panel
            int insertIndex = CoreGrid.Children.IndexOf(CoreTextPanel);
            if (insertIndex < 0) insertIndex = 0;

            // Render style decorations via the style renderer
            if (_styleRenderer != null)
            {
                if (style == "NeonGlow" || style == "Tech")
                {
                    OuterEllipse.Visibility = Visibility.Visible;
                    OuterEllipse.Stroke = _highlightBorderBrush;
                    OuterEllipse.StrokeThickness = 0.8;
                    OuterEllipse.StrokeDashArray = new DoubleCollection { 4, 3 };
                }

                _styleRenderer.RenderDecorations(WheelCanvas, CoreGrid, cx, cy, wheelRadius, coreRadius, insertIndex);
            }
        }

        private void RenderSectors()
        {
            int n = _profile.SectorCount;
            double sectorSize = 360.0 / n;
            double winSize = this.Width;
            double cx = winSize / 2.0;
            double cy = winSize / 2.0;

            string shape = ConfigManager.CurrentConfig.Shape ?? "Original";
            bool showText = ConfigManager.CurrentConfig.ShowText;

            _sectorPaths.Clear();
            _contentPanels.Clear();
            _sectorTransforms.Clear();
            _containerTransforms.Clear();
            _sectorAngles.Clear();

            // Clear previous sector drawings from Canvas
            var toRemove = new List<UIElement>();
            foreach (UIElement child in WheelCanvas.Children)
            {
                if (child != CoreGrid && child != OuterEllipse && !(child is FrameworkElement fe && fe.Tag != null && fe.Tag.ToString().StartsWith("Deco_")))
                {
                    toRemove.Add(child);
                }
            }
            foreach (var elem in toRemove)
            {
                WheelCanvas.Children.Remove(elem);
            }

            for (int i = 0; i < n; i++)
            {
                double midAngle = i * sectorSize;
                double startAngle = midAngle - (sectorSize / 2);
                double endAngle = midAngle + (sectorSize / 2);

                double midAngleRad = midAngle * (Math.PI / 180.0);
                double layoutRadius = (_innerRadius + _outerRadius) / 2.0;
                double lx = cx + Math.Cos(midAngleRad) * layoutRadius;
                double ly = cy + Math.Sin(midAngleRad) * layoutRadius;

                Geometry geometry;
                if (shape == "Circle")
                {
                    double size = (_outerRadius - _innerRadius) * 0.85;
                    geometry = new EllipseGeometry(new Point(lx, ly), size / 2.0, size / 2.0);
                }
                else if (shape == "RoundedRect")
                {
                    double w = (_outerRadius - _innerRadius) * 0.9;
                    double arcLength = layoutRadius * sectorSize * (Math.PI / 180.0);
                    double h = Math.Min(w * 0.85, arcLength * 0.85);

                    var rectGeom = new RectangleGeometry(new Rect(lx - w / 2.0, ly - h / 2.0, w, h), 6, 6);
                    rectGeom.Transform = new RotateTransform(midAngle, lx, ly);
                    geometry = rectGeom;
                }
                else // "Original"
                {
                    geometry = CreateSectorGeometry(startAngle, endAngle, _innerRadius, _outerRadius);
                }

                var pathTransform = new TranslateTransform(0, 0);
                var path = new Path
                {
                    Data = geometry,
                    Fill = _defaultSectorBrush,
                    Stroke = _sectorBorderBrush,
                    StrokeThickness = _borderThickness,
                    RenderTransform = pathTransform,
                    Tag = i
                };
                System.Windows.Controls.Panel.SetZIndex(path, 1);

                WheelCanvas.Children.Insert(0, path);
                _sectorPaths.Add(path);
                _sectorTransforms.Add(pathTransform);
                _sectorAngles.Add(midAngleRad);

                // Grid Container to ensure absolute centering of StackPanel (especially when text is hidden)
                var containerTransform = new TranslateTransform(0, 0);
                var container = new Grid
                {
                    Width = 80,
                    Height = 60,
                    RenderTransform = containerTransform
                };

                var stackPanel = new StackPanel
                {
                    Orientation = System.Windows.Controls.Orientation.Vertical,
                    HorizontalAlignment = System.Windows.HorizontalAlignment.Center,
                    VerticalAlignment = System.Windows.VerticalAlignment.Center
                };
                container.Children.Add(stackPanel);

                string actionText = "未设置";
                string actionType = "Hotkey";
                string parameter = "";
                if (i < _profile.Actions.Count && _profile.Actions[i] != null)
                {
                    actionText = _profile.Actions[i].Name;
                    actionType = _profile.Actions[i].Type;
                    parameter = _profile.Actions[i].Parameter;
                }

                FrameworkElement iconElement = null;
                if (actionType == "Launch")
                {
                    System.Windows.Media.Imaging.BitmapSource iconSrc = null;
                    if (!string.IsNullOrEmpty(parameter))
                    {
                        iconSrc = IconHelper.GetIcon(parameter);
                    }

                    if (iconSrc != null)
                    {
                        iconElement = new System.Windows.Controls.Image
                        {
                            Source = iconSrc,
                            Width = 24,
                            Height = 24,
                            Stretch = Stretch.Uniform,
                            Margin = new Thickness(0, 0, 0, 2),
                            HorizontalAlignment = System.Windows.HorizontalAlignment.Center
                        };
                    }
                }

                if (iconElement == null)
                {
                    string pathData = GetVectorIconPath(actionType, parameter);
                    if (!string.IsNullOrEmpty(pathData))
                    {
                        iconElement = new Path
                        {
                            Data = Geometry.Parse(pathData),
                            Fill = _textColorBrush,
                            Stretch = Stretch.Uniform,
                            Width = 16,
                            Height = 16,
                            Margin = new Thickness(0, 0, 0, 3),
                            HorizontalAlignment = System.Windows.HorizontalAlignment.Center
                        };
                    }
                }

                if (iconElement != null)
                {
                    stackPanel.Children.Add(iconElement);
                }

                if (showText)
                {
                    var textBlock = new TextBlock
                    {
                        Text = actionText,
                        Foreground = _textColorBrush,
                        FontSize = 9.5,
                        FontWeight = FontWeights.Medium,
                        TextAlignment = TextAlignment.Center,
                        TextWrapping = TextWrapping.Wrap,
                        Width = 75,
                        Height = 26,
                        Margin = new Thickness(0, 1, 0, 0),
                        Effect = (System.Windows.Media.Effects.Effect)Resources["TextShadow"]
                    };
                    stackPanel.Children.Add(textBlock);
                }

                // Center Grid Container on (lx, ly)
                Canvas.SetLeft(container, lx - container.Width / 2.0);
                Canvas.SetTop(container, ly - container.Height / 2.0);

                System.Windows.Controls.Panel.SetZIndex(container, 10);
                WheelCanvas.Children.Add(container);
                _contentPanels.Add(stackPanel);
                _containerTransforms.Add(containerTransform);
            }
        }

        private string GetVectorIconPath(string type, string parameter)
        {
            if (type == "Hotkey")
            {
                // Keyboard icon
                return "M19,15H5V5H19M19,3H5C3.89,3 3,3.89 3,5V15C3,16.1 3.89,17 5,17H19C20.1,17 21,16.1 21,15V5C21,3.89 20.1,3 19,3M2,18H22V20H2V18Z";
            }

            if (type == "System" && !string.IsNullOrEmpty(parameter))
            {
                switch (parameter.Trim().ToLower())
                {
                    case "lock":
                        // Lock padlock
                        return "M18,8H17V6A5,5,0,0,0,7,6V8H6A2,2,0,0,0,4,10V20A2,2,0,0,0,6,22H18A2,2,0,0,0,20,20V10A2,2,0,0,0,18,8ZM9,6A3,3,0,0,1,15,6V8H9ZM18,20H6V10H18Z";
                    case "volumeup":
                        // Speaker volume up
                        return "M3,9V15H7L12,20V4L7,9H3ZM14,3.23V5.29C16.89,6.15 19,8.83 19,12C19,15.17 16.89,17.85 14,18.71V20.77C18.01,19.86 21,16.28 21,12C21,7.72 18.01,4.14 14,3.23ZM14,8.83V15.17C15.14,14.6 16,13.4 16,12C16,10.6 15.14,9.4 14,8.83Z";
                    case "volumedown":
                        // Speaker volume down
                        return "M3,9V15H7L12,20V4L7,9H3ZM14,8.83V15.17C15.14,14.6 16,13.4 16,12C16,10.6 15.14,9.4 14,8.83ZM14,3.23V5.29C16.89,6.15 19,8.83 19,12C19,15.17 16.89,17.85 14,18.71V20.77C18.01,19.86 21,16.28 21,12Z";
                    case "volumemute":
                        // Speaker mute
                        return "M3,9V15H7L12,20V4L7,9H3ZM16.5,12L14,9.5L15.5,8L18,10.5L20.5,8L22,9.5L19.5,12L22,14.5L20.5,16L18,13.5L15.5,16L14,14.5L16.5,12Z";
                    case "showdesktop":
                        // Screen monitor
                        return "M4,2A2,2,0,0,0,2,4V16A2,2,0,0,0,4,18H10V20H8V22H16V20H14V18H20A2,2,0,0,0,22,16V4A2,2,0,0,0,20,2H4ZM4,4H20V16H4V4Z";
                    case "screenshot":
                        // Camera
                        return "M4,4H7L9,2H15L17,4H20A2,2,0,0,1,22,6V18A2,2,0,0,1,20,20H4A2,2,0,0,1,2,18V6A2,2,0,0,1,4,4ZM12,7A5,5,0,1,0,17,12A5,5,0,0,0,12,7ZM12,9A3,3,0,1,1,9,12A3,3,0,0,1,12,9Z";
                }
            }

            return null;
        }

        private Geometry CreateSectorGeometry(double startAngleDegrees, double endAngleDegrees, double innerRadius, double outerRadius)
        {
            double startRad = startAngleDegrees * (Math.PI / 180.0);
            double endRad = endAngleDegrees * (Math.PI / 180.0);

            double cx = this.Width / 2.0;
            double cy = this.Height / 2.0;

            Point p1 = new Point(cx + Math.Cos(startRad) * outerRadius, cy + Math.Sin(startRad) * outerRadius);
            Point p2 = new Point(cx + Math.Cos(endRad) * outerRadius, cy + Math.Sin(endRad) * outerRadius);
            Point p3 = new Point(cx + Math.Cos(endRad) * innerRadius, cy + Math.Sin(endRad) * innerRadius);
            Point p4 = new Point(cx + Math.Cos(startRad) * innerRadius, cy + Math.Sin(startRad) * innerRadius);

            bool isLargeArc = Math.Abs(endAngleDegrees - startAngleDegrees) > 180.0;

            var geometry = new StreamGeometry();
            using (StreamGeometryContext ctx = geometry.Open())
            {
                ctx.BeginFigure(p1, isFilled: true, isClosed: true);
                ctx.ArcTo(p2, new Size(outerRadius, outerRadius), 0, isLargeArc, SweepDirection.Clockwise, isStroked: true, isSmoothJoin: true);
                ctx.LineTo(p3, isStroked: true, isSmoothJoin: false);
                ctx.ArcTo(p4, new Size(innerRadius, innerRadius), 0, isLargeArc, SweepDirection.Counterclockwise, isStroked: true, isSmoothJoin: true);
                ctx.LineTo(p1, isStroked: true, isSmoothJoin: false);
            }
            geometry.Freeze();
            return geometry;
        }

        public void HighlightSector(int index)
        {
            // Center Exit Hover Feedback
            if (index == -1)
            {
                CoreExitIcon.Fill = new SolidColorBrush(Color.FromRgb(244, 63, 94)); // Warm rose cancel
                if (_styleRenderer != null)
                {
                    _styleRenderer.ApplyExitHighlight(CoreExitIcon, true);
                }

                // Animate CoreScale up
                var scaleAnim = new DoubleAnimation(1.12, new Duration(TimeSpan.FromMilliseconds(90)))
                {
                    EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
                };
                CoreScale.BeginAnimation(ScaleTransform.ScaleXProperty, scaleAnim);
                CoreScale.BeginAnimation(ScaleTransform.ScaleYProperty, scaleAnim);
            }
            else
            {
                CoreExitIcon.Fill = _textColorBrush;
                if (_styleRenderer != null)
                {
                    _styleRenderer.ApplyExitHighlight(CoreExitIcon, false);
                }

                // Animate CoreScale back to normal
                var scaleAnim = new DoubleAnimation(1.0, new Duration(TimeSpan.FromMilliseconds(90)))
                {
                    EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
                };
                CoreScale.BeginAnimation(ScaleTransform.ScaleXProperty, scaleAnim);
                CoreScale.BeginAnimation(ScaleTransform.ScaleYProperty, scaleAnim);
            }

            var ease = new CubicEase { EasingMode = EasingMode.EaseOut };
            var animDuration = new Duration(TimeSpan.FromMilliseconds(80));

            for (int i = 0; i < _sectorPaths.Count; i++)
            {
                var path = _sectorPaths[i];
                var panel = i < _contentPanels.Count ? _contentPanels[i] : null;
                var pTransform = i < _sectorTransforms.Count ? _sectorTransforms[i] : null;
                var cTransform = i < _containerTransforms.Count ? _containerTransforms[i] : null;
                double angleRad = i < _sectorAngles.Count ? _sectorAngles[i] : 0;

                TextBlock textBlock = panel?.Children.OfType<TextBlock>().FirstOrDefault();
                Path vectorIcon = panel?.Children.OfType<Path>().FirstOrDefault();

                if (i == index)
                {
                    path.Fill = _highlightSectorBrush;
                    path.Stroke = _highlightBorderBrush;
                    path.StrokeThickness = _highlightBorderThickness;
                    System.Windows.Controls.Panel.SetZIndex(path, 5);

                    // Magnetic pop-out: Translate outward by 5.5px along the radial vector
                    double targetX = Math.Cos(angleRad) * 5.5;
                    double targetY = Math.Sin(angleRad) * 5.5;

                    if (pTransform != null)
                    {
                        pTransform.BeginAnimation(TranslateTransform.XProperty, new DoubleAnimation(targetX, animDuration) { EasingFunction = ease });
                        pTransform.BeginAnimation(TranslateTransform.YProperty, new DoubleAnimation(targetY, animDuration) { EasingFunction = ease });
                    }
                    if (cTransform != null)
                    {
                        cTransform.BeginAnimation(TranslateTransform.XProperty, new DoubleAnimation(targetX, animDuration) { EasingFunction = ease });
                        cTransform.BeginAnimation(TranslateTransform.YProperty, new DoubleAnimation(targetY, animDuration) { EasingFunction = ease });
                    }

                    if (textBlock != null)
                    {
                        textBlock.Foreground = Brushes.White;
                        textBlock.FontWeight = FontWeights.Bold;
                    }
                    if (vectorIcon != null)
                    {
                        vectorIcon.Fill = Brushes.White;
                    }

                    if (_styleRenderer != null)
                    {
                        _styleRenderer.ApplySectorHighlight(path, true);
                    }
                }
                else
                {
                    path.Fill = _defaultSectorBrush;
                    path.Stroke = _sectorBorderBrush;
                    path.StrokeThickness = _borderThickness;
                    System.Windows.Controls.Panel.SetZIndex(path, 1);

                    // Spring back to 0,0
                    if (pTransform != null)
                    {
                        pTransform.BeginAnimation(TranslateTransform.XProperty, new DoubleAnimation(0.0, animDuration) { EasingFunction = ease });
                        pTransform.BeginAnimation(TranslateTransform.YProperty, new DoubleAnimation(0.0, animDuration) { EasingFunction = ease });
                    }
                    if (cTransform != null)
                    {
                        cTransform.BeginAnimation(TranslateTransform.XProperty, new DoubleAnimation(0.0, animDuration) { EasingFunction = ease });
                        cTransform.BeginAnimation(TranslateTransform.YProperty, new DoubleAnimation(0.0, animDuration) { EasingFunction = ease });
                    }
                    
                    if (_styleRenderer != null)
                    {
                        _styleRenderer.ApplySectorHighlight(path, false);
                    }

                    if (_textColorBrush is SolidColorBrush sc)
                    {
                        var dimColor = new SolidColorBrush(Color.FromArgb(170, sc.Color.R, sc.Color.G, sc.Color.B));
                        if (textBlock != null)
                        {
                            textBlock.Foreground = dimColor;
                            textBlock.FontWeight = FontWeights.Medium;
                        }
                        if (vectorIcon != null)
                        {
                            vectorIcon.Fill = dimColor;
                        }
                    }
                    else
                    {
                        if (textBlock != null)
                        {
                            textBlock.Foreground = _textColorBrush;
                            textBlock.FontWeight = FontWeights.Medium;
                        }
                        if (vectorIcon != null)
                        {
                            vectorIcon.Fill = _textColorBrush;
                        }
                    }
                }
            }
        }
    }
}

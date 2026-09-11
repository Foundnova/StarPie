using System;
using System.Collections.Generic;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Effects;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;

namespace Issue17Demo;

public enum DisplayMode
{
    CursorFollow,   // 模式 A: 伴随光标浮动
    OuterAnchor,    // 模式 B: 扇区外沿径向锚定
    CenterCore,     // 模式 C: 轮盘中心显示 (PR #37 现状)
    InlineSector    // 模式 D: 原版扇区内嵌文字 (现状溢出)
}

public enum WheelLayoutMode
{
    IconOnly,       // 仅显示图标 (推荐搭配浮动)
    IconAndText,    // 图标 + 文字
    TextOnly        // 仅文字
}

public class MockAction
{
    public string Name { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty;
    public string Hotkey { get; set; } = string.Empty;
    public string IconEmoji { get; set; } = "⚡";
    public string Description { get; set; } = string.Empty;
}

public partial class MainWindow : Window
{
    // State
    private DisplayMode _currentMode = DisplayMode.CursorFollow;
    private WheelLayoutMode _currentLayout = WheelLayoutMode.IconOnly;
    private int _sectorCount = 8;
    private int _hoveredSectorIndex = -1;
    private Point _lastMousePos = new Point(0, 0);

    // Dimensions
    private const double WheelOuterRadius = 160.0;
    private const double WheelInnerRadius = 46.0;
    private const double CenterDeadzoneRadius = 38.0;

    // Drawing cache
    private readonly List<Path> _sectorPaths = new();
    private readonly List<MockAction> _activeActions = new();
    private readonly System.Windows.Threading.DispatcherTimer _lerpTimer;
    private Point _targetPillPos = new Point(0, 0);
    private Point _currentPillPos = new Point(0, 0);
    private bool _isInitializing = true;

    public MainWindow()
    {
        _isInitializing = true;
        InitializeComponent();
        UpdateActionData();

        // Setup smooth lerp timer (60 FPS)
        _lerpTimer = new System.Windows.Threading.DispatcherTimer
        {
            Interval = TimeSpan.FromMilliseconds(16)
        };
        _lerpTimer.Tick += LerpTimer_Tick;
        _lerpTimer.Start();

        _isInitializing = false;

        Loaded += MainWindow_Loaded;
        SizeChanged += MainWindow_SizeChanged;
    }

    private void MainWindow_Loaded(object sender, RoutedEventArgs e)
    {
        UpdateActionData();
        RedrawWheel();
        UpdateModeBanner();
    }

    private void MainWindow_SizeChanged(object sender, SizeChangedEventArgs e)
    {
        RedrawWheel();
    }

    #region Mock Data Initialization
    private void UpdateActionData()
    {
        _activeActions.Clear();
        if (_sectorCount == 4)
        {
            _activeActions.Add(new MockAction { Name = "复制选中", Category = "剪贴板", Hotkey = "Ctrl + C", IconEmoji = "📋", Description = "复制当前高亮选中的文本或文件" });
            _activeActions.Add(new MockAction { Name = "显示桌面", Category = "窗口管理", Hotkey = "Win + D", IconEmoji = "🖥️", Description = "最小化所有窗口，极速回到桌面" });
            _activeActions.Add(new MockAction { Name = "粘贴内容", Category = "剪贴板", Hotkey = "Ctrl + V", IconEmoji = "📥", Description = "将剪贴板最近的内容粘贴至焦点处" });
            _activeActions.Add(new MockAction { Name = "任务视图", Category = "窗口管理", Hotkey = "Win + Tab", IconEmoji = "🗂️", Description = "打开全局多任务与多桌面切换视图" });
        }
        else if (_sectorCount == 8)
        {
            // 真实还原 Issue #17 截图中的配置
            _activeActions.Add(new MockAction { Name = "复制选中 (Copy)", Category = "剪贴板", Hotkey = "Ctrl + C", IconEmoji = "📋", Description = "快速复制选中内容到系统剪贴板" });
            _activeActions.Add(new MockAction { Name = "锁定电脑 (Lock)", Category = "系统安全", Hotkey = "Win + L", IconEmoji = "🔒", Description = "离开工位时立即锁定当前 Windows 计算机" });
            _activeActions.Add(new MockAction { Name = "显示桌面 (Desktop)", Category = "窗口管理", Hotkey = "Win + D", IconEmoji = "🖥️", Description = "一键透视/最小化所有窗口返回纯净桌面" });
            _activeActions.Add(new MockAction { Name = "屏幕截图 (Capture)", Category = "实用工具", Hotkey = "Win + Shift + S", IconEmoji = "📸", Description = "唤起 Windows 原生高分辨率区域截屏" });
            _activeActions.Add(new MockAction { Name = "粘贴内容 (Paste)", Category = "剪贴板", Hotkey = "Ctrl + V", IconEmoji = "📥", Description = "极速下发粘贴指令并保留原始格式" });
            _activeActions.Add(new MockAction { Name = "音量减小 (Vol Down)", Category = "多媒体", Hotkey = "Media Vol-", IconEmoji = "🔉", Description = "按步进降低当前主音频输出音量" });
            _activeActions.Add(new MockAction { Name = "系统管理工具 (System Tools & Diag)", Category = "系统管理", Hotkey = "Win + X", IconEmoji = "🛠️", Description = "呼出高级系统管理、设备管理与终端菜单" });
            _activeActions.Add(new MockAction { Name = "音量增加 (Vol Up)", Category = "多媒体", Hotkey = "Media Vol+", IconEmoji = "🔊", Description = "按步进提高当前主音频输出音量" });
        }
        else
        {
            // 12 扇区密集档位
            _activeActions.Add(new MockAction { Name = "复制选中", Category = "剪贴板", Hotkey = "Ctrl + C", IconEmoji = "📋", Description = "复制文本或对象" });
            _activeActions.Add(new MockAction { Name = "快速保存", Category = "文件", Hotkey = "Ctrl + S", IconEmoji = "💾", Description = "保存当前活动文档" });
            _activeActions.Add(new MockAction { Name = "锁定电脑", Category = "系统", Hotkey = "Win + L", IconEmoji = "🔒", Description = "安全锁定屏幕" });
            _activeActions.Add(new MockAction { Name = "显示桌面", Category = "窗口", Hotkey = "Win + D", IconEmoji = "🖥️", Description = "显示当前桌面" });
            _activeActions.Add(new MockAction { Name = "任务管理器", Category = "系统", Hotkey = "Ctrl + Shift + Esc", IconEmoji = "📊", Description = "监控进程与资源占用" });
            _activeActions.Add(new MockAction { Name = "区域截图", Category = "工具", Hotkey = "Win + Shift + S", IconEmoji = "📸", Description = "框选区域截图" });
            _activeActions.Add(new MockAction { Name = "粘贴内容", Category = "剪贴板", Hotkey = "Ctrl + V", IconEmoji = "📥", Description = "粘贴剪贴板内容" });
            _activeActions.Add(new MockAction { Name = "撤销操作", Category = "编辑", Hotkey = "Ctrl + Z", IconEmoji = "↩️", Description = "撤销上一步操作" });
            _activeActions.Add(new MockAction { Name = "音量减小", Category = "多媒体", Hotkey = "Vol-", IconEmoji = "🔉", Description = "降低系统音量" });
            _activeActions.Add(new MockAction { Name = "系统管理工具", Category = "系统", Hotkey = "Win + X", IconEmoji = "🛠️", Description = "呼出系统高级快捷菜单" });
            _activeActions.Add(new MockAction { Name = "音量增加", Category = "多媒体", Hotkey = "Vol+", IconEmoji = "🔊", Description = "增加系统音量" });
            _activeActions.Add(new MockAction { Name = "重做操作", Category = "编辑", Hotkey = "Ctrl + Y", IconEmoji = "↪️", Description = "重做先前撤销的步骤" });
        }
    }
    #endregion

    #region Wheel Rendering
    private void RedrawWheel()
    {
        if (_isInitializing || WheelCanvas == null || SectorsCanvas == null || _activeActions.Count == 0 || WheelCanvas.ActualWidth < 10 || WheelCanvas.ActualHeight < 10)
        {
            return;
        }

        double cx = WheelCanvas.ActualWidth / 2.0;
        double cy = WheelCanvas.ActualHeight / 2.0;

        // Position rulers and axes
        Canvas.SetLeft(RulerOuterEllipse, cx - RulerOuterEllipse.Width / 2.0);
        Canvas.SetTop(RulerOuterEllipse, cy - RulerOuterEllipse.Height / 2.0);

        Canvas.SetLeft(RulerInnerEllipse, cx - RulerInnerEllipse.Width / 2.0);
        Canvas.SetTop(RulerInnerEllipse, cy - RulerInnerEllipse.Height / 2.0);

        RulerAxisX.X1 = cx - 220; RulerAxisX.Y1 = cy; RulerAxisX.X2 = cx + 220; RulerAxisX.Y2 = cy;
        RulerAxisY.X1 = cx; RulerAxisY.Y1 = cy - 220; RulerAxisY.X2 = cx; RulerAxisY.Y2 = cy + 220;

        // Position Center Core
        Canvas.SetLeft(CenterCoreGrid, cx - CenterCoreGrid.Width / 2.0);
        Canvas.SetTop(CenterCoreGrid, cy - CenterCoreGrid.Height / 2.0);

        // Clear and rebuild sectors
        SectorsCanvas.Children.Clear();
        _sectorPaths.Clear();

        double stepAngle = 360.0 / _sectorCount;
        double halfStep = stepAngle / 2.0;

        for (int i = 0; i < _sectorCount; i++)
        {
            double centerAngleDeg = i * stepAngle;
            double startAngleDeg = centerAngleDeg - halfStep;
            double endAngleDeg = centerAngleDeg + halfStep;

            Path sectorPath = CreateSectorPath(cx, cy, WheelInnerRadius + 2, WheelOuterRadius, startAngleDeg, endAngleDeg);
            sectorPath.Fill = new SolidColorBrush(Color.FromArgb(160, 24, 34, 53));
            sectorPath.Stroke = new SolidColorBrush(Color.FromArgb(120, 51, 65, 85));
            sectorPath.StrokeThickness = 1.2;
            sectorPath.Tag = i;

            SectorsCanvas.Children.Add(sectorPath);
            _sectorPaths.Add(sectorPath);

            // Add Sector Content (Icon / Text)
            double midAngleRad = centerAngleDeg * Math.PI / 180.0;
            double textRadius = (WheelInnerRadius + WheelOuterRadius) / 2.0;
            double itemX = cx + textRadius * Math.Cos(midAngleRad);
            double itemY = cy + textRadius * Math.Sin(midAngleRad);

            MockAction action = _activeActions[i % _activeActions.Count];

            StackPanel itemPanel = new StackPanel
            {
                Orientation = Orientation.Vertical,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
                IsHitTestVisible = false
            };

            // Icon
            if (_currentLayout == WheelLayoutMode.IconOnly || _currentLayout == WheelLayoutMode.IconAndText)
            {
                TextBlock iconBlock = new TextBlock
                {
                    Text = action.IconEmoji,
                    FontSize = (_currentLayout == WheelLayoutMode.IconOnly) ? 26 : 18,
                    HorizontalAlignment = HorizontalAlignment.Center,
                    Foreground = Brushes.White
                };
                itemPanel.Children.Add(iconBlock);
            }

            // Text
            if (_currentLayout == WheelLayoutMode.IconAndText || _currentLayout == WheelLayoutMode.TextOnly || _currentMode == DisplayMode.InlineSector)
            {
                TextBlock nameBlock = new TextBlock
                {
                    Text = action.Name,
                    FontSize = (_currentLayout == WheelLayoutMode.TextOnly) ? 12 : 9.5,
                    FontWeight = FontWeights.Medium,
                    HorizontalAlignment = HorizontalAlignment.Center,
                    TextAlignment = TextAlignment.Center,
                    MaxWidth = (_sectorCount == 12) ? 60 : 78,
                    TextTrimming = TextTrimming.CharacterEllipsis,
                    Foreground = new SolidColorBrush(Color.FromRgb(226, 232, 240)),
                    Margin = new Thickness(0, 2, 0, 0)
                };

                // In Mode D (Inline Sector Text), intentionally don't clip heavily to expose the overflow flaw of Issue #17
                if (_currentMode == DisplayMode.InlineSector)
                {
                    nameBlock.TextTrimming = TextTrimming.None;
                    nameBlock.MaxWidth = 110; // Exposes real overlap!
                    nameBlock.Foreground = (_sectorCount >= 8) ? new SolidColorBrush(Color.FromRgb(251, 146, 60)) : Brushes.White;
                }

                itemPanel.Children.Add(nameBlock);
            }

            itemPanel.Measure(new Size(200, 200));
            double pw = itemPanel.DesiredSize.Width;
            double ph = itemPanel.DesiredSize.Height;

            Canvas.SetLeft(itemPanel, itemX - pw / 2.0);
            Canvas.SetTop(itemPanel, itemY - ph / 2.0);
            SectorsCanvas.Children.Add(itemPanel);
        }

        HighlightHoveredSector();
    }

    private static Path CreateSectorPath(double cx, double cy, double innerR, double outerR, double startDeg, double endDeg)
    {
        double startRad = startDeg * Math.PI / 180.0;
        double endRad = endDeg * Math.PI / 180.0;

        Point pOuterStart = new Point(cx + outerR * Math.Cos(startRad), cy + outerR * Math.Sin(startRad));
        Point pOuterEnd = new Point(cx + outerR * Math.Cos(endRad), cy + outerR * Math.Sin(endRad));
        Point pInnerEnd = new Point(cx + innerR * Math.Cos(endRad), cy + innerR * Math.Sin(endRad));
        Point pInnerStart = new Point(cx + innerR * Math.Cos(startRad), cy + innerR * Math.Sin(startRad));

        bool isLargeArc = (endDeg - startDeg) > 180.0;

        PathGeometry geom = new PathGeometry();
        PathFigure fig = new PathFigure
        {
            StartPoint = pOuterStart,
            IsClosed = true,
            IsFilled = true
        };

        fig.Segments.Add(new ArcSegment(pOuterEnd, new Size(outerR, outerR), 0, isLargeArc, SweepDirection.Clockwise, true));
        fig.Segments.Add(new LineSegment(pInnerEnd, true));
        fig.Segments.Add(new ArcSegment(pInnerStart, new Size(innerR, innerR), 0, isLargeArc, SweepDirection.Counterclockwise, true));

        geom.Figures.Add(fig);

        return new Path { Data = geom };
    }
    #endregion

    #region Mouse & Hit Testing
    private void WheelCanvas_MouseMove(object sender, MouseEventArgs e)
    {
        Point pos = e.GetPosition(WheelCanvas);
        _lastMousePos = pos;

        double cx = WheelCanvas.ActualWidth / 2.0;
        double cy = WheelCanvas.ActualHeight / 2.0;
        double dx = pos.X - cx;
        double dy = pos.Y - cy;
        double dist = Math.Sqrt(dx * dx + dy * dy);

        double angleDeg = (Math.Atan2(dy, dx) * (180.0 / Math.PI) + 360.0) % 360.0;

        StatusAngleText.Text = $"{angleDeg:F1}°";
        StatusDistText.Text = $"{dist:F1} px";

        int newSector = -1;
        if (dist >= WheelInnerRadius)
        {
            double step = 360.0 / _sectorCount;
            newSector = (int)Math.Floor((angleDeg + step / 2.0) / step) % _sectorCount;
        }

        if (newSector != _hoveredSectorIndex)
        {
            _hoveredSectorIndex = newSector;
            HighlightHoveredSector();
            UpdateHudContent();
        }

        UpdateHudPosition(pos, cx, cy);
    }

    private void WheelCanvas_MouseLeave(object sender, MouseEventArgs e)
    {
        _hoveredSectorIndex = -1;
        HighlightHoveredSector();
        UpdateHudContent();
        HideFloatingHud();

        StatusSectorText.Text = "已离开画布";
    }

    private void HighlightHoveredSector()
    {
        for (int i = 0; i < _sectorPaths.Count; i++)
        {
            if (i == _hoveredSectorIndex)
            {
                _sectorPaths[i].Fill = new SolidColorBrush(Color.FromArgb(230, 2, 132, 199)); // Glowing accent
                _sectorPaths[i].Stroke = new SolidColorBrush(Color.FromRgb(56, 189, 248));
                _sectorPaths[i].StrokeThickness = 2.4;
                Panel.SetZIndex(_sectorPaths[i], 10);
            }
            else
            {
                _sectorPaths[i].Fill = new SolidColorBrush(Color.FromArgb(160, 24, 34, 53));
                _sectorPaths[i].Stroke = new SolidColorBrush(Color.FromArgb(120, 51, 65, 85));
                _sectorPaths[i].StrokeThickness = 1.2;
                Panel.SetZIndex(_sectorPaths[i], 1);
            }
        }

        if (_hoveredSectorIndex >= 0 && _hoveredSectorIndex < _activeActions.Count)
        {
            MockAction action = _activeActions[_hoveredSectorIndex];
            StatusSectorText.Text = $"扇区 #{_hoveredSectorIndex} - {action.Name}";
        }
        else
        {
            StatusSectorText.Text = "死区 / 未激活";
        }
    }
    #endregion

    #region Floating HUD & Center Text Updates
    private void UpdateHudContent()
    {
        if (_hoveredSectorIndex < 0 || _hoveredSectorIndex >= _activeActions.Count)
        {
            CenterSelectionGrid.Visibility = Visibility.Collapsed;
            CenterNormalPanel.Visibility = Visibility.Visible;
            HideFloatingHud();
            ConnectingLine.Opacity = 0;
            return;
        }

        MockAction action = _activeActions[_hoveredSectorIndex];

        // Mode C: Center Core Display (PR #37 现状)
        if (_currentMode == DisplayMode.CenterCore)
        {
            CenterNormalPanel.Visibility = Visibility.Collapsed;
            CenterSelectionGrid.Visibility = Visibility.Visible;
            CenterActionIcon.Text = action.IconEmoji;
            CenterActionTitle.Text = action.Name;
            HideFloatingHud();
            ConnectingLine.Opacity = 0;
            return;
        }
        else
        {
            CenterSelectionGrid.Visibility = Visibility.Collapsed;
            CenterNormalPanel.Visibility = Visibility.Visible;
        }

        // Mode D: Inline Sector Text (现状对比)
        if (_currentMode == DisplayMode.InlineSector)
        {
            HideFloatingHud();
            ConnectingLine.Opacity = 0;
            return;
        }

        // Mode A & B: Floating Tooltip
        HudIconText.Text = action.IconEmoji;
        HudActionTitle.Text = action.Name;
        HudActionDesc.Text = action.Description;
        HudHotkeyText.Text = action.Hotkey;
        HudCategoryText.Text = action.Category;

        HudHotkeyChip.Visibility = (ShowHotkeyChipCheckBox.IsChecked == true) ? Visibility.Visible : Visibility.Collapsed;
        HudCategoryBorder.Visibility = (ShowCategoryTagCheckBox.IsChecked == true) ? Visibility.Visible : Visibility.Collapsed;

        ShowFloatingHud();
    }

    private void UpdateHudPosition(Point mousePos, double cx, double cy)
    {
        if (_hoveredSectorIndex < 0 || _hoveredSectorIndex >= _activeActions.Count)
        {
            return;
        }

        FloatingHudPill.Measure(new Size(600, 200));
        double pillW = FloatingHudPill.DesiredSize.Width;
        double pillH = FloatingHudPill.DesiredSize.Height;

        if (_currentMode == DisplayMode.CursorFollow)
        {
            ConnectingLine.Opacity = 0;

            // Offset from cursor
            double targetX = mousePos.X + 18;
            double targetY = mousePos.Y + 18;

            // Smart Screen/Canvas Boundary Clamping
            if (targetX + pillW > WheelCanvas.ActualWidth - 10)
            {
                targetX = mousePos.X - pillW - 14;
            }
            if (targetY + pillH > WheelCanvas.ActualHeight - 10)
            {
                targetY = mousePos.Y - pillH - 14;
            }
            if (targetX < 10) targetX = 10;
            if (targetY < 10) targetY = 10;

            _targetPillPos = new Point(targetX, targetY);

            if (EnableSmoothLerpCheckBox.IsChecked != true)
            {
                _currentPillPos = _targetPillPos;
                Canvas.SetLeft(FloatingHudPill, _currentPillPos.X);
                Canvas.SetTop(FloatingHudPill, _currentPillPos.Y);
            }
        }
        else if (_currentMode == DisplayMode.OuterAnchor)
        {
            // Calculate anchor point on the outer ray of hovered sector
            double step = 360.0 / _sectorCount;
            double centerAngleDeg = _hoveredSectorIndex * step;
            double midAngleRad = centerAngleDeg * Math.PI / 180.0;

            double anchorRadius = WheelOuterRadius + 24.0;
            double cos = Math.Cos(midAngleRad);
            double sin = Math.Sin(midAngleRad);

            double anchorX = cx + anchorRadius * cos;
            double anchorY = cy + anchorRadius * sin;

            double targetX, targetY;

            // Quadrant smart positioning:
            if (Math.Abs(sin) > 0.707) // Mostly Top or Bottom
            {
                targetX = anchorX - pillW / 2.0;
                targetY = (sin < 0) ? (anchorY - pillH - 4) : (anchorY + 4);
            }
            else // Mostly Left or Right
            {
                targetX = (cos > 0) ? (anchorX + 8) : (anchorX - pillW - 8);
                targetY = anchorY - pillH / 2.0;
            }

            // Boundary clamping
            if (targetX + pillW > WheelCanvas.ActualWidth - 8) targetX = WheelCanvas.ActualWidth - pillW - 8;
            if (targetX < 8) targetX = 8;
            if (targetY + pillH > WheelCanvas.ActualHeight - 8) targetY = WheelCanvas.ActualHeight - pillH - 8;
            if (targetY < 8) targetY = 8;

            _targetPillPos = new Point(targetX, targetY);
            _currentPillPos = _targetPillPos;

            Canvas.SetLeft(FloatingHudPill, targetX);
            Canvas.SetTop(FloatingHudPill, targetY);

            // Draw connecting line from sector outer arc to pill
            if (ShowConnectingLineCheckBox.IsChecked == true)
            {
                double sectorEdgeX = cx + WheelOuterRadius * cos;
                double sectorEdgeY = cy + WheelOuterRadius * sin;

                ConnectingLine.X1 = sectorEdgeX;
                ConnectingLine.Y1 = sectorEdgeY;
                ConnectingLine.X2 = targetX + pillW / 2.0;
                ConnectingLine.Y2 = targetY + pillH / 2.0;
                ConnectingLine.Opacity = 0.85;
            }
            else
            {
                ConnectingLine.Opacity = 0;
            }
        }
    }

    private void LerpTimer_Tick(object? sender, EventArgs e)
    {
        if (_currentMode != DisplayMode.CursorFollow || EnableSmoothLerpCheckBox.IsChecked != true)
        {
            return;
        }

        // Smooth Lerp factor (0.35 gives responsive yet silky glide)
        double dx = _targetPillPos.X - _currentPillPos.X;
        double dy = _targetPillPos.Y - _currentPillPos.Y;

        if (Math.Abs(dx) > 0.5 || Math.Abs(dy) > 0.5)
        {
            _currentPillPos.X += dx * 0.40;
            _currentPillPos.Y += dy * 0.40;
            Canvas.SetLeft(FloatingHudPill, _currentPillPos.X);
            Canvas.SetTop(FloatingHudPill, _currentPillPos.Y);
        }
    }

    private void ShowFloatingHud()
    {
        if (FloatingHudPill.Opacity < 0.9)
        {
            DoubleAnimation anim = new DoubleAnimation(1.0, TimeSpan.FromMilliseconds(100));
            FloatingHudPill.BeginAnimation(UIElement.OpacityProperty, anim);
        }
    }

    private void HideFloatingHud()
    {
        if (FloatingHudPill.Opacity > 0.05)
        {
            DoubleAnimation anim = new DoubleAnimation(0.0, TimeSpan.FromMilliseconds(80));
            FloatingHudPill.BeginAnimation(UIElement.OpacityProperty, anim);
        }
    }
    #endregion

    #region Simulated Click & Ripple Feedback
    private void WheelCanvas_MouseDown(object sender, MouseButtonEventArgs e)
    {
        if (_hoveredSectorIndex >= 0 && _hoveredSectorIndex < _activeActions.Count)
        {
            MockAction action = _activeActions[_hoveredSectorIndex];
            StatusExecutionLog.Text = $"⚡ [触发动作] #{_hoveredSectorIndex} - {action.Name} ({action.Hotkey})";
            StatusExecutionLog.Foreground = new SolidColorBrush(Color.FromRgb(56, 189, 248));

            // Trigger ripple animation at mouse pos
            Point clickPos = e.GetPosition(WheelCanvas);
            Canvas.SetLeft(ClickRipple, clickPos.X - 5);
            Canvas.SetTop(ClickRipple, clickPos.Y - 5);
            ClickRipple.Opacity = 0.9;
            ClickRipple.Width = 10;
            ClickRipple.Height = 10;

            DoubleAnimation sizeAnim = new DoubleAnimation(10, 80, TimeSpan.FromMilliseconds(260));
            DoubleAnimation fadeAnim = new DoubleAnimation(0.9, 0, TimeSpan.FromMilliseconds(260));

            sizeAnim.Completed += (s, ev) =>
            {
                ClickRipple.Opacity = 0;
            };

            ClickRipple.BeginAnimation(FrameworkElement.WidthProperty, sizeAnim);
            ClickRipple.BeginAnimation(FrameworkElement.HeightProperty, sizeAnim);
            ClickRipple.BeginAnimation(UIElement.OpacityProperty, fadeAnim);
        }
    }
    #endregion

    #region UI Event Handlers
    private void ModeRadio_Checked(object sender, RoutedEventArgs e)
    {
        if (_isInitializing) return;

        if (ModeCursorFollowRadio?.IsChecked == true) _currentMode = DisplayMode.CursorFollow;
        else if (ModeOuterAnchorRadio?.IsChecked == true) _currentMode = DisplayMode.OuterAnchor;
        else if (ModeCenterCoreRadio?.IsChecked == true) _currentMode = DisplayMode.CenterCore;
        else if (ModeInlineSectorRadio?.IsChecked == true) _currentMode = DisplayMode.InlineSector;

        UpdateModeBanner();
        RedrawWheel();
        UpdateHudContent();
    }

    private void SectorsRadio_Checked(object sender, RoutedEventArgs e)
    {
        if (_isInitializing) return;

        if (Sectors4Radio?.IsChecked == true) _sectorCount = 4;
        else if (Sectors8Radio?.IsChecked == true) _sectorCount = 8;
        else if (Sectors12Radio?.IsChecked == true) _sectorCount = 12;

        UpdateActionData();
        RedrawWheel();
        UpdateHudContent();
    }

    private void LayoutRadio_Checked(object sender, RoutedEventArgs e)
    {
        if (_isInitializing) return;

        if (LayoutIconOnlyRadio?.IsChecked == true) _currentLayout = WheelLayoutMode.IconOnly;
        else if (LayoutIconAndTextRadio?.IsChecked == true) _currentLayout = WheelLayoutMode.IconAndText;
        else if (LayoutTextOnlyRadio?.IsChecked == true) _currentLayout = WheelLayoutMode.TextOnly;

        RedrawWheel();
    }

    private void Option_Changed(object sender, RoutedEventArgs e)
    {
        if (_isInitializing) return;
        UpdateHudContent();
    }

    private void FontSizeSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (FontSizeLabel != null && HudActionTitle != null)
        {
            FontSizeLabel.Text = $"{e.NewValue:F1} px";
            HudActionTitle.FontSize = e.NewValue;
        }
    }

    private void UpdateModeBanner()
    {
        if (CurrentModeBanner == null) return;

        switch (_currentMode)
        {
            case DisplayMode.CursorFollow:
                CurrentModeBanner.Text = "当前模式: 🚀 模式 A - 光标伴随浮动标签 (视线零转移)";
                CurrentModeBanner.Foreground = new SolidColorBrush(Color.FromRgb(56, 189, 248));
                break;
            case DisplayMode.OuterAnchor:
                CurrentModeBanner.Text = "当前模式: 🛰️ 模式 B - 扇区外沿径向锚定 (雷达HUD锁定)";
                CurrentModeBanner.Foreground = new SolidColorBrush(Color.FromRgb(14, 165, 233));
                break;
            case DisplayMode.CenterCore:
                CurrentModeBanner.Text = "当前模式: 🎯 模式 C - 轮盘中心显示 (PR #37 现状对比)";
                CurrentModeBanner.Foreground = new SolidColorBrush(Color.FromRgb(168, 85, 247));
                break;
            case DisplayMode.InlineSector:
                CurrentModeBanner.Text = "当前模式: ⚠️ 模式 D - 原版扇区内嵌文字 (Issue #17 现状溢出)";
                CurrentModeBanner.Foreground = new SolidColorBrush(Color.FromRgb(251, 146, 60));
                break;
        }
    }
    #endregion

    #region Public Simulation & Snapshot Methods
    public void SimulateHover(int sectorIndex, DisplayMode mode, WheelLayoutMode layout)
    {
        _currentMode = mode;
        _currentLayout = layout;

        ModeCursorFollowRadio.IsChecked = (mode == DisplayMode.CursorFollow);
        ModeOuterAnchorRadio.IsChecked = (mode == DisplayMode.OuterAnchor);
        ModeCenterCoreRadio.IsChecked = (mode == DisplayMode.CenterCore);
        ModeInlineSectorRadio.IsChecked = (mode == DisplayMode.InlineSector);

        LayoutIconOnlyRadio.IsChecked = (layout == WheelLayoutMode.IconOnly);
        LayoutIconAndTextRadio.IsChecked = (layout == WheelLayoutMode.IconAndText);
        LayoutTextOnlyRadio.IsChecked = (layout == WheelLayoutMode.TextOnly);

        UpdateActionData();
        RedrawWheel();
        UpdateModeBanner();

        double cx = WheelCanvas.ActualWidth / 2.0;
        double cy = WheelCanvas.ActualHeight / 2.0;
        if (cx <= 0) cx = 380;
        if (cy <= 0) cy = 340;

        double step = 360.0 / _sectorCount;
        double centerAngleDeg = sectorIndex * step;
        double midAngleRad = centerAngleDeg * Math.PI / 180.0;
        double hoverRadius = (WheelInnerRadius + WheelOuterRadius) / 2.0;
        Point simulatedMousePos = new Point(cx + hoverRadius * Math.Cos(midAngleRad), cy + hoverRadius * Math.Sin(midAngleRad));

        _hoveredSectorIndex = sectorIndex;
        HighlightHoveredSector();
        UpdateHudContent();

        if (mode == DisplayMode.CursorFollow)
        {
            FloatingHudPill.Opacity = 1.0;
            UpdateHudPosition(simulatedMousePos, cx, cy);
            _currentPillPos = _targetPillPos;
            Canvas.SetLeft(FloatingHudPill, _currentPillPos.X);
            Canvas.SetTop(FloatingHudPill, _currentPillPos.Y);
        }
        else if (mode == DisplayMode.OuterAnchor)
        {
            FloatingHudPill.Opacity = 1.0;
            UpdateHudPosition(simulatedMousePos, cx, cy);
        }
        else
        {
            FloatingHudPill.Opacity = 0.0;
            ConnectingLine.Opacity = 0.0;
        }

        UpdateLayout();
    }

    public void SaveSnapshot(string filePath)
    {
        UpdateLayout();
        int width = (int)ActualWidth;
        int height = (int)ActualHeight;
        if (width <= 0) width = 1160;
        if (height <= 0) height = 780;

        RenderTargetBitmap rtb = new RenderTargetBitmap(width, height, 96, 96, PixelFormats.Pbgra32);
        rtb.Render(this);

        PngBitmapEncoder encoder = new PngBitmapEncoder();
        encoder.Frames.Add(BitmapFrame.Create(rtb));
        using var fs = System.IO.File.Create(filePath);
        encoder.Save(fs);
    }
    #endregion
}


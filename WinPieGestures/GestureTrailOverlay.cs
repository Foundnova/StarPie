using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;

namespace WinPieGestures;

/// <summary>手势轨迹浮层：透明置顶小窗，绘制手势拖动轨迹与起点（仅供盲操可视化，不拦截鼠标）。</summary>
public class GestureTrailOverlay : Window
{
	private readonly Canvas _canvas;
	private readonly Polyline _line;
	private readonly Ellipse _startDot;
	private double _originX;
	private double _originY;

	public GestureTrailOverlay()
	{
		WindowStyle = WindowStyle.None;
		AllowsTransparency = true;
		Background = Brushes.Transparent;
		Topmost = true;
		ShowInTaskbar = false;
		ShowActivated = false;
		Focusable = false;
		ResizeMode = ResizeMode.NoResize;
		IsHitTestVisible = false;
		UseLayoutRounding = true;
		SnapsToDevicePixels = true;

		_canvas = new Canvas { IsHitTestVisible = false };
		_line = new Polyline
		{
			Stroke = new SolidColorBrush(Color.FromArgb(255, 108, 99, 255)),
			StrokeThickness = 3.0,
			StrokeStartLineCap = PenLineCap.Round,
			StrokeEndLineCap = PenLineCap.Round,
			StrokeLineJoin = PenLineJoin.Round
		};
		_startDot = new Ellipse { Width = 6.0, Height = 6.0, Fill = new SolidColorBrush(Color.FromRgb(244, 63, 94)) };
		Panel.SetZIndex(_line, 0);
		Panel.SetZIndex(_startDot, 1);
		_canvas.Children.Add(_line);
		_canvas.Children.Add(_startDot);
		Content = _canvas;
	}

	/// <summary>在屏幕物理坐标（按所在屏 DIP 缩放换算）定位并开始轨迹。</summary>
	public void BeginAt(double screenX, double screenY, double scaleX, double scaleY)
	{
		double sx = scaleX > 0.0 ? scaleX : 1.0;
		double sy = scaleY > 0.0 ? scaleY : 1.0;
		_originX = screenX / sx - 120.0;
		_originY = screenY / sy - 120.0;
		Left = _originX;
		Top = _originY;
		Width = 240.0;
		Height = 240.0;
		_line.Points.Clear();
		Point start = new Point(screenX / sx - _originX, screenY / sy - _originY);
		_line.Points.Add(start);
		Canvas.SetLeft(_startDot, start.X - 3.0);
		Canvas.SetTop(_startDot, start.Y - 3.0);
	}

	public void AddPoint(double screenX, double screenY, double scaleX, double scaleY)
	{
		double sx = scaleX > 0.0 ? scaleX : 1.0;
		double sy = scaleY > 0.0 ? scaleY : 1.0;
		_line.Points.Add(new Point(screenX / sx - _originX, screenY / sy - _originY));
	}

	public void ClearTrail()
	{
		_line.Points.Clear();
	}
}
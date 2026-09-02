using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;

namespace WinPieGestures;

/// <summary>
/// 手势轨迹浮层：覆盖整个虚拟屏幕的透明置顶窗（固定不动，无缩放/移动），
/// 轨迹点按手势起点所在屏的 DIP 缩放换算后直接绘制——任意方向/任意长度都不会裁剪或变形。
/// </summary>
public class GestureTrailOverlay : Window
{
	private readonly Canvas _canvas;
	private readonly Polyline _line;
	private readonly Ellipse _startDot;
	private double _scaleX = 1.0;
	private double _scaleY = 1.0;
	private double _leftDIP;
	private double _topDIP;

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

	/// <summary>覆盖整个虚拟屏幕并显示（只定位一次，之后不再移动/缩放）。</summary>
	public void ShowCoveringScreen(double scaleX, double scaleY)
	{
		_scaleX = scaleX > 0.0 ? scaleX : 1.0;
		_scaleY = scaleY > 0.0 ? scaleY : 1.0;
		_leftDIP = SystemParameters.VirtualScreenLeft;
		_topDIP = SystemParameters.VirtualScreenTop;
		Left = _leftDIP;
		Top = _topDIP;
		Width = SystemParameters.VirtualScreenWidth;
		Height = SystemParameters.VirtualScreenHeight;
		Show();
	}

	/// <summary>开始轨迹：屏幕物理坐标 → 覆盖层局部坐标，画起点。</summary>
	public void BeginAt(double screenX, double screenY)
	{
		double lx = screenX / _scaleX - _leftDIP;
		double ly = screenY / _scaleY - _topDIP;
		_line.Points.Clear();
		_line.Points.Add(new Point(lx, ly));
		Canvas.SetLeft(_startDot, lx - 3.0);
		Canvas.SetTop(_startDot, ly - 3.0);
	}

	public void AddPoint(double screenX, double screenY)
	{
		_line.Points.Add(new Point(screenX / _scaleX - _leftDIP, screenY / _scaleY - _topDIP));
	}

	public void ClearTrail()
	{
		_line.Points.Clear();
	}
}
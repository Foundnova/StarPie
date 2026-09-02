using System;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;

namespace WinPieGestures;

/// <summary>
/// 手势轨迹浮层：覆盖"手势起点所在显示器"的透明置顶窗（定位一次，之后不再移动/缩放），
/// 轨迹点按该屏 DIP 缩放换算绘制——单屏内精确，无跨屏错位，任意方向/长度不裁剪不变形。
/// </summary>
public class GestureTrailOverlay : Window
{
	private readonly Canvas _canvas;
	private readonly Polyline _line;
	private readonly Ellipse _startDot;
	private double _leftDIP;
	private double _topDIP;

	[DllImport("user32.dll")]
	private static extern nint MonitorFromPoint(POINT pt, uint dwFlags);

	[DllImport("user32.dll")]
	private static extern bool GetMonitorInfo(nint hMonitor, ref MONITORINFO lpmi);

	[StructLayout(LayoutKind.Sequential)]
	private struct POINT
	{
		public int x;
		public int y;
	}

	[StructLayout(LayoutKind.Sequential)]
	private struct RECT
	{
		public int Left;
		public int Top;
		public int Right;
		public int Bottom;
	}

	[StructLayout(LayoutKind.Sequential)]
	private struct MONITORINFO
	{
		public uint cbSize;
		public RECT rcMonitor;
		public RECT rcWork;
		public uint dwFlags;
	}

	private const uint MONITOR_DEFAULTTONEAREST = 2;

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

	/// <summary>覆盖手势起点所在显示器（物理矩形 ÷ 该屏缩放 → DIP），只定位一次。</summary>
	public void ShowCoveringScreen(double screenX, double screenY, double scaleX, double scaleY)
	{
		double sx = scaleX > 0.0 ? scaleX : 1.0;
		double sy = scaleY > 0.0 ? scaleY : 1.0;
		RECT rc;
		try
		{
			nint mon = MonitorFromPoint(new POINT { x = (int)Math.Round(screenX), y = (int)Math.Round(screenY) }, MONITOR_DEFAULTTONEAREST);
			MONITORINFO mi = default;
			mi.cbSize = (uint)Marshal.SizeOf<MONITORINFO>();
			if (mon == IntPtr.Zero || !GetMonitorInfo(mon, ref mi))
			{
				throw new InvalidOperationException("monitor");
			}
			rc = mi.rcMonitor;
		}
		catch
		{
			// 兜底：虚拟屏幕（主屏度量，近似）
			rc = new RECT
			{
				Left = (int)Math.Round(SystemParameters.VirtualScreenLeft * sx),
				Top = (int)Math.Round(SystemParameters.VirtualScreenTop * sy),
				Right = (int)Math.Round((SystemParameters.VirtualScreenLeft + SystemParameters.VirtualScreenWidth) * sx),
				Bottom = (int)Math.Round((SystemParameters.VirtualScreenTop + SystemParameters.VirtualScreenHeight) * sy)
			};
		}
		_leftDIP = rc.Left / sx;
		_topDIP = rc.Top / sy;
		Left = _leftDIP;
		Top = _topDIP;
		Width = (rc.Right - rc.Left) / sx;
		Height = (rc.Bottom - rc.Top) / sy;
		Show();
	}

	/// <summary>开始轨迹：屏幕物理坐标 → 覆盖层局部坐标，画起点。</summary>
	public void BeginAt(double screenX, double screenY, double scaleX, double scaleY)
	{
		double sx = scaleX > 0.0 ? scaleX : 1.0;
		double sy = scaleY > 0.0 ? scaleY : 1.0;
		double lx = screenX / sx - _leftDIP;
		double ly = screenY / sy - _topDIP;
		_line.Points.Clear();
		_line.Points.Add(new Point(lx, ly));
		Canvas.SetLeft(_startDot, lx - 3.0);
		Canvas.SetTop(_startDot, ly - 3.0);
	}

	public void AddPoint(double screenX, double screenY, double scaleX, double scaleY)
	{
		double sx = scaleX > 0.0 ? scaleX : 1.0;
		double sy = scaleY > 0.0 ? scaleY : 1.0;
		_line.Points.Add(new Point(screenX / sx - _leftDIP, screenY / sy - _topDIP));
	}

	public void ClearTrail()
	{
		_line.Points.Clear();
	}
}
using System;
using System.Drawing;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using MouseEventArgs = System.Windows.Input.MouseEventArgs;
using MouseButtonEventArgs = System.Windows.Input.MouseButtonEventArgs;

namespace WinPieGestures;

public partial class ScreenSnipWindow : Window
{
	private readonly Bitmap? _fullScreenBitmap;
	private readonly int _virtualLeft;
	private readonly int _virtualTop;
	private readonly Action<Bitmap?> _onCaptured;
	private System.Windows.Point _startPoint;
	private System.Drawing.Point _startPhysical;
	private bool _isSelecting;

	/// <param name="fullScreenBitmap">显示截屏窗前预抓取的全虚拟屏幕物理位图（无叠层污染）；为 null 时松开鼠标实时抓屏</param>
	/// <param name="virtualLeft">虚拟屏幕物理原点 X，用于把全局物理光标坐标换算为位图本地坐标</param>
	/// <param name="virtualTop">虚拟屏幕物理原点 Y</param>
	public ScreenSnipWindow(Bitmap? fullScreenBitmap, int virtualLeft, int virtualTop, Action<Bitmap?> onCaptured)
	{
		InitializeComponent();
		_fullScreenBitmap = fullScreenBitmap;
		_virtualLeft = virtualLeft;
		_virtualTop = virtualTop;
		_onCaptured = onCaptured;

		Left = SystemParameters.VirtualScreenLeft;
		Top = SystemParameters.VirtualScreenTop;
		Width = SystemParameters.VirtualScreenWidth;
		Height = SystemParameters.VirtualScreenHeight;
	}

	private void Window_Loaded(object sender, RoutedEventArgs e)
	{
		Focus();
		CaptureMouse();
	}

	private void Window_KeyDown(object sender, KeyEventArgs e)
	{
		if (e.Key == Key.Escape)
		{
			ReleaseMouseCapture();
			_onCaptured?.Invoke(null);
			Close();
		}
	}

	private void Window_MouseLeftButtonDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
	{
		_startPoint = e.GetPosition(this);
		// 物理像素坐标（PerMonitorV2 进程中 GetCursorPos 返回物理值），裁剪直接用它，不经过 DPI 换算
		_startPhysical = ToDrawingPoint(ScreenHelper.GetCursorPhysicalPosition());
		_isSelecting = true;

		SelectionRect.Visibility = Visibility.Visible;
		InfoBadge.Visibility = Visibility.Visible;

		Canvas.SetLeft(SelectionRect, _startPoint.X);
		Canvas.SetTop(SelectionRect, _startPoint.Y);
		SelectionRect.Width = 0;
		SelectionRect.Height = 0;
	}

	private void Window_MouseMove(object sender, System.Windows.Input.MouseEventArgs e)
	{
		if (!_isSelecting)
		{
			return;
		}

		System.Windows.Point currentPoint = e.GetPosition(this);
		double x = Math.Min(_startPoint.X, currentPoint.X);
		double y = Math.Min(_startPoint.Y, currentPoint.Y);
		double w = Math.Abs(currentPoint.X - _startPoint.X);
		double h = Math.Abs(currentPoint.Y - _startPoint.Y);

		Canvas.SetLeft(SelectionRect, x);
		Canvas.SetTop(SelectionRect, y);
		SelectionRect.Width = w;
		SelectionRect.Height = h;

		System.Drawing.Point currentPhysical = ToDrawingPoint(ScreenHelper.GetCursorPhysicalPosition());
		SizeTextBlock.Text = $"{Math.Abs(currentPhysical.X - _startPhysical.X)} × {Math.Abs(currentPhysical.Y - _startPhysical.Y)}";
		Canvas.SetLeft(InfoBadge, Math.Max(10, x));
		Canvas.SetTop(InfoBadge, Math.Max(10, y - 32));
	}

	private void Window_MouseLeftButtonUp(object sender, System.Windows.Input.MouseButtonEventArgs e)
	{
		if (!_isSelecting)
		{
			return;
		}
		_isSelecting = false;
		ReleaseMouseCapture();

		System.Drawing.Point endPhysical = ToDrawingPoint(ScreenHelper.GetCursorPhysicalPosition());
		int physLeft = Math.Min(_startPhysical.X, endPhysical.X);
		int physTop = Math.Min(_startPhysical.Y, endPhysical.Y);
		int physWidth = Math.Abs(endPhysical.X - _startPhysical.X);
		int physHeight = Math.Abs(endPhysical.Y - _startPhysical.Y);

		Close();

		if (physWidth > 5 && physHeight > 5)
		{
			try
			{
				Bitmap? bmp = CaptureSelection(physLeft, physTop, physWidth, physHeight);
				if (bmp != null)
				{
					AppLogger.LogInfo($"OCR capture rect: ({physLeft},{physTop}) {physWidth}x{physHeight}px source={(_fullScreenBitmap != null ? "pre-captured" : "live")}");
					_onCaptured?.Invoke(bmp);
					return;
				}
			}
			catch (Exception ex)
			{
				AppLogger.LogError("Failed to capture snippet rectangle", ex);
			}
		}

		_onCaptured?.Invoke(null);
	}

	/// <summary>物理光标坐标取整为 Drawing.Point（ScreenHelper 返回 WPF Point）</summary>
	private static System.Drawing.Point ToDrawingPoint(System.Windows.Point p)
	{
		return new System.Drawing.Point((int)Math.Round(p.X), (int)Math.Round(p.Y));
	}

	/// <summary>按物理像素矩形截取识别图。优先从预抓位图裁剪，位图缺失时实时抓屏兜底。</summary>
	private Bitmap? CaptureSelection(int physLeft, int physTop, int physWidth, int physHeight)
	{
		if (_fullScreenBitmap != null)
		{
			Rectangle want = new Rectangle(physLeft - _virtualLeft, physTop - _virtualTop, physWidth, physHeight);
			Rectangle bounds = new Rectangle(0, 0, _fullScreenBitmap.Width, _fullScreenBitmap.Height);
			Rectangle src = Rectangle.Intersect(want, bounds);
			if (src.Width <= 0 || src.Height <= 0)
			{
				return null;
			}

			Bitmap crop = new Bitmap(src.Width, src.Height);
			using (Graphics g = Graphics.FromImage(crop))
			{
				g.DrawImage(_fullScreenBitmap, new Rectangle(0, 0, src.Width, src.Height), src, GraphicsUnit.Pixel);
			}
			return crop;
		}

		Bitmap live = new Bitmap(physWidth, physHeight);
		using (Graphics g = Graphics.FromImage(live))
		{
			g.CopyFromScreen(physLeft, physTop, 0, 0, new System.Drawing.Size(physWidth, physHeight), CopyPixelOperation.SourceCopy);
		}
		return live;
	}
}

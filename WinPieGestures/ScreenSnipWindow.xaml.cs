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
	private System.Windows.Point _startPoint;
	private bool _isSelecting;
	private readonly Action<Bitmap?> _onCaptured;

	public ScreenSnipWindow(Action<Bitmap?> onCaptured)
	{
		InitializeComponent();
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

		SizeTextBlock.Text = $"{(int)w} × {(int)h}";
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

		System.Windows.Point endPoint = e.GetPosition(this);
		double x = Math.Min(_startPoint.X, endPoint.X);
		double y = Math.Min(_startPoint.Y, endPoint.Y);
		double w = Math.Abs(endPoint.X - _startPoint.X);
		double h = Math.Abs(endPoint.Y - _startPoint.Y);

		Close();

		if (w > 5 && h > 5)
		{
			try
			{
				// 换算绝对屏幕坐标与 DPI
				PresentationSource source = PresentationSource.FromVisual(this);
				double dpiX = source?.CompositionTarget?.TransformToDevice.M11 ?? 1.0;
				double dpiY = source?.CompositionTarget?.TransformToDevice.M22 ?? 1.0;

				int physLeft = (int)Math.Round((Left + x) * dpiX);
				int physTop = (int)Math.Round((Top + y) * dpiY);
				int physWidth = (int)Math.Round(w * dpiX);
				int physHeight = (int)Math.Round(h * dpiY);

				Bitmap bmp = new Bitmap(physWidth, physHeight);
				using (Graphics g = Graphics.FromImage(bmp))
				{
					g.CopyFromScreen(physLeft, physTop, 0, 0, new System.Drawing.Size(physWidth, physHeight), CopyPixelOperation.SourceCopy);
				}
				_onCaptured?.Invoke(bmp);
				return;
			}
			catch (Exception ex)
			{
				AppLogger.LogError("Failed to capture snippet rectangle", ex);
			}
		}

		_onCaptured?.Invoke(null);
	}
}

using System;
using System.CodeDom.Compiler;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using System.Windows.Markup;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Effects;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;

namespace WinPieGestures;

public partial class RadialWindow : Window
{
	[StructLayout(LayoutKind.Sequential)]
	public struct POINT
	{
		public int x;
		public int y;
	}

	private const uint MONITOR_DEFAULTTONEAREST = 2;
	private const uint SWP_NOACTIVATE = 0x0010;
	private const uint SWP_NOZORDER = 0x0004;

	[DllImport("user32.dll")]
	public static extern nint MonitorFromPoint(POINT pt, uint dwFlags);

	[DllImport("SHCore.dll", SetLastError = true)]
	public static extern int GetDpiForMonitor(nint hMonitor, int dpiType, out uint dpiX, out uint dpiY);

	[DllImport("user32.dll", SetLastError = true)]
	public static extern bool SetWindowPos(nint hWnd, nint hWndInsertAfter, int X, int Y, int cx, int cy, uint uFlags);

	/// <summary>
	/// 获取指定物理像素坐标所在显示器的 DPI 缩放系数(多显示器混合 DPI 环境下逐屏获取)。
	/// </summary>
	public static (double scaleX, double scaleY) GetMonitorDpiScale(Point physicalPoint)
	{
		try
		{
			POINT pt = new POINT { x = (int)Math.Round(physicalPoint.X), y = (int)Math.Round(physicalPoint.Y) };
			nint hMonitor = MonitorFromPoint(pt, MONITOR_DEFAULTTONEAREST);
			if (hMonitor != IntPtr.Zero)
			{
				if (GetDpiForMonitor(hMonitor, 0, out uint dpiX, out uint dpiY) == 0 && dpiX > 0 && dpiY > 0)
				{
					return (dpiX / 96.0, dpiY / 96.0);
				}
			}
		}
		catch
		{
		}
		return (1.0, 1.0);
	}

	/// <summary>
	/// 以物理像素将窗口居中到 _centerPoint(鼠标钩子返回的物理坐标)所在的显示器上。
	/// 在 PerMonitorV2 DPI 感知下,SetWindowPos 直接使用物理像素,可正确定位到副屏。
	/// </summary>
	/// <summary>
		/// SwitchWindow 动作：显示任务栏第 N 窗口的当前图标（参数缺失/非法默认第 1 个）；获取失败返回 null（调用方回退默认程序图标）。
		/// </summary>
		private static FrameworkElement? BuildSwitchWindowIcon(string parameter, double size, bool showText)
		{
			int n = 1;
			if (int.TryParse(parameter?.Trim(), out int parsed) && parsed > 0)
			{
				n = parsed;
			}
			BitmapSource? windowIcon = WindowTaskbarHelper.GetNthWindowIcon(n);
			if (windowIcon == null)
			{
				return null;
			}
			return new Image
			{
				Source = windowIcon,
				Width = size,
				Height = size,
				Stretch = Stretch.Uniform,
				Margin = new Thickness(0.0, 0.0, 0.0, showText ? 2 : 0),
				HorizontalAlignment = HorizontalAlignment.Center
			};
		}

		/// <summary>以物理像素将窗口居中到 _centerPoint 所在显示器（PerMonitorV2 混合 DPI 副屏定位）。</summary>
		private void PositionWindowOnTargetMonitor()
	{
		nint handle = new WindowInteropHelper(this).Handle;
		if (handle == IntPtr.Zero)
		{
			return;
		}
		var (scaleX, scaleY) = GetMonitorDpiScale(_centerPoint);
		int physicalWidth = (int)Math.Round(base.Width * scaleX);
		int physicalHeight = (int)Math.Round(base.Height * scaleY);
		int physicalLeft = (int)Math.Round(_centerPoint.X - physicalWidth / 2.0);
		int physicalTop = (int)Math.Round(_centerPoint.Y - physicalHeight / 2.0);
		SetWindowPos(handle, IntPtr.Zero, physicalLeft, physicalTop, physicalWidth, physicalHeight, SWP_NOACTIVATE | SWP_NOZORDER);
	}

	protected override void OnSourceInitialized(EventArgs e)
	{
		base.OnSourceInitialized(e);
		PositionWindowOnTargetMonitor();
	}

	private sealed class SubTierVisuals
	{
		public List<System.Windows.Shapes.Path> Paths { get; }

		public List<Grid> Containers { get; }

		public List<TranslateTransform> PathTransforms { get; }

		public List<TranslateTransform> ContainerTransforms { get; }

		public List<double> Angles { get; }

		public SubTierVisuals(
			List<System.Windows.Shapes.Path> paths,
			List<Grid> containers,
			List<TranslateTransform> pathTransforms,
			List<TranslateTransform> containerTransforms,
			List<double> angles)
		{
			Paths = paths;
			Containers = containers;
			PathTransforms = pathTransforms;
			ContainerTransforms = containerTransforms;
			Angles = angles;
		}
	}

	private int _currentHighlightedSector;

	private int _currentHighlightedSubSector;

	private int _activeSubTierParentSector;

	private readonly Point _centerPoint;

	private readonly WheelProfile _profile;

	private readonly List<System.Windows.Shapes.Path> _sectorPaths;

	private readonly List<StackPanel> _contentPanels;

	private readonly List<TranslateTransform> _sectorTransforms;

	private readonly List<TranslateTransform> _containerTransforms;

	private readonly List<double> _sectorAngles;

	private readonly List<System.Windows.Shapes.Path> _subSectorPaths;

	private readonly List<Grid> _subContentContainers;

	private readonly List<TranslateTransform> _subSectorTransforms;

	private readonly List<TranslateTransform> _subContainerTransforms;

	private readonly List<double> _subSectorAngles;

	private readonly Dictionary<int, SubTierVisuals> _subTierCache = new Dictionary<int, SubTierVisuals>();

	private IRadialStyleRenderer _styleRenderer;

	private IRadialStyleRenderer? _subStyleRenderer;

	private Brush _defaultSectorBrush;

	private Brush _highlightSectorBrush;

	private Brush _sectorBorderBrush;

	private Brush _highlightBorderBrush;

	private Brush _textColorBrush;

	private Brush _coreBgBrush;

	private Brush _coreBorderBrush;

	private Brush _subDefaultSectorBrush;

	private Brush _subHighlightSectorBrush;

	private Brush _subSectorBorderBrush;

	private Brush _subHighlightBorderBrush;

	private Brush _subTextColorBrush;

	private double _innerRadius;

	private double _outerRadius;

	private double _borderThickness;

	private double _highlightBorderThickness;

	private bool _isOuterEscaped;

	public RadialWindow(Point centerPoint, WheelProfile profile)
	{
		//IL_00d0: Unknown result type (might be due to invalid IL or missing references)
		//IL_00d1: Unknown result type (might be due to invalid IL or missing references)
		_currentHighlightedSector = -999;
		_currentHighlightedSubSector = -1;
		_activeSubTierParentSector = -1;
		_sectorPaths = new List<System.Windows.Shapes.Path>();
		_contentPanels = new List<StackPanel>();
		_sectorTransforms = new List<TranslateTransform>();
		_containerTransforms = new List<TranslateTransform>();
		_sectorAngles = new List<double>();
		_subSectorPaths = new List<System.Windows.Shapes.Path>();
		_subContentContainers = new List<Grid>();
		_subSectorTransforms = new List<TranslateTransform>();
		_subContainerTransforms = new List<TranslateTransform>();
		_subSectorAngles = new List<double>();
		_innerRadius = 52.0;
		_outerRadius = 138.0;
		_borderThickness = 1.0;
		_highlightBorderThickness = 1.5;
		InitializeComponent();
		_centerPoint = centerPoint;
		_profile = profile;
		InitializeThemeAndStyle();
		CoreTextPanel.Visibility = Visibility.Collapsed;
		base.Loaded += RadialWindow_Loaded;
		CoreTitle.Text = ((profile.ProcessName == "Global") ? "全局动作" : profile.ProcessName);
		CoreSubtitle.Text = $"{profile.SectorCount} 键动作";
	}

	private void InitializeThemeAndStyle()
	{
		string text = ConfigManager.CurrentConfig.Theme ?? "System";
		string text2 = ConfigManager.CurrentConfig.UiStyle ?? "ClassicRing";
		_innerRadius = ConfigManager.CurrentConfig.InnerRadius;
		_outerRadius = ConfigManager.CurrentConfig.WheelRadius;
		if (_innerRadius >= _outerRadius)
		{
			_innerRadius = Math.Max(0.0, _outerRadius - 20.0);
		}
		_styleRenderer = StyleRendererFactory.CreateRenderer(text2);
		_styleRenderer.Initialize(text, ConfigManager.CurrentConfig);
		_defaultSectorBrush = _styleRenderer.DefaultSectorBrush;
		_highlightSectorBrush = _styleRenderer.HighlightSectorBrush;
		_sectorBorderBrush = _styleRenderer.SectorBorderBrush;
		_highlightBorderBrush = _styleRenderer.HighlightBorderBrush;
		_textColorBrush = _styleRenderer.TextColorBrush;
		_coreBgBrush = _styleRenderer.CoreBgBrush;
		_coreBorderBrush = _styleRenderer.CoreBorderBrush;
		_borderThickness = _styleRenderer.BorderThickness;
		_highlightBorderThickness = _styleRenderer.HighlightBorderThickness;
		string text3 = ConfigManager.CurrentConfig.SubWheelUiStyle ?? text2;
		string text4 = ConfigManager.CurrentConfig.SubWheelTheme ?? text;
		if (string.IsNullOrEmpty(text4) || text4 == "FollowPrimary")
		{
			text4 = text;
		}
		if (string.IsNullOrEmpty(text3) || text3 == "FollowPrimary")
		{
			text3 = text2;
		}
		if (ConfigManager.CurrentConfig.UseIndependentSubWheelTheme || text3 != text2 || text4 != text)
		{
			try
			{
				_subStyleRenderer = StyleRendererFactory.CreateRenderer(text3);
				_subStyleRenderer.Initialize(text4, ConfigManager.CurrentConfig);
				_subDefaultSectorBrush = _subStyleRenderer.DefaultSectorBrush;
				_subHighlightSectorBrush = _subStyleRenderer.HighlightSectorBrush;
				_subSectorBorderBrush = _subStyleRenderer.SectorBorderBrush;
				_subHighlightBorderBrush = _subStyleRenderer.HighlightBorderBrush;
				_subTextColorBrush = _subStyleRenderer.TextColorBrush;
				if (text4 == "Custom")
				{
					if (!string.IsNullOrWhiteSpace(ConfigManager.CurrentConfig.SubWheelCustomSectorBg))
					{
						_subDefaultSectorBrush = new SolidColorBrush((Color)ColorConverter.ConvertFromString(ConfigManager.CurrentConfig.SubWheelCustomSectorBg));
					}
					if (!string.IsNullOrWhiteSpace(ConfigManager.CurrentConfig.SubWheelCustomSectorBorder))
					{
						_subSectorBorderBrush = new SolidColorBrush((Color)ColorConverter.ConvertFromString(ConfigManager.CurrentConfig.SubWheelCustomSectorBorder));
					}
					if (!string.IsNullOrWhiteSpace(ConfigManager.CurrentConfig.SubWheelCustomHighlightBg))
					{
						_subHighlightSectorBrush = new SolidColorBrush((Color)ColorConverter.ConvertFromString(ConfigManager.CurrentConfig.SubWheelCustomHighlightBg));
					}
					if (!string.IsNullOrWhiteSpace(ConfigManager.CurrentConfig.SubWheelCustomHighlightBorder))
					{
						_subHighlightBorderBrush = new SolidColorBrush((Color)ColorConverter.ConvertFromString(ConfigManager.CurrentConfig.SubWheelCustomHighlightBorder));
					}
					if (!string.IsNullOrWhiteSpace(ConfigManager.CurrentConfig.SubWheelCustomText))
					{
						_subTextColorBrush = new SolidColorBrush((Color)ColorConverter.ConvertFromString(ConfigManager.CurrentConfig.SubWheelCustomText));
					}
				}
				return;
			}
			catch
			{
				_subStyleRenderer = _styleRenderer;
				_subDefaultSectorBrush = _defaultSectorBrush;
				_subHighlightSectorBrush = _highlightSectorBrush;
				_subSectorBorderBrush = _sectorBorderBrush;
				_subHighlightBorderBrush = _highlightBorderBrush;
				_subTextColorBrush = _textColorBrush;
				return;
			}
		}
		_subStyleRenderer = _styleRenderer;
		_subDefaultSectorBrush = _defaultSectorBrush;
		_subHighlightSectorBrush = _highlightSectorBrush;
		_subSectorBorderBrush = _sectorBorderBrush;
		_subHighlightBorderBrush = _highlightBorderBrush;
		_subTextColorBrush = _textColorBrush;
	}

	private void RadialWindow_Loaded(object sender, RoutedEventArgs e)
	{
		//IL_01a6: Unknown result type (might be due to invalid IL or missing references)
		//IL_01ab: Unknown result type (might be due to invalid IL or missing references)
		//IL_01cf: Unknown result type (might be due to invalid IL or missing references)
		//IL_01d4: Unknown result type (might be due to invalid IL or missing references)
		//IL_017d: Unknown result type (might be due to invalid IL or missing references)
		//IL_0182: Unknown result type (might be due to invalid IL or missing references)
		//IL_0194: Unknown result type (might be due to invalid IL or missing references)
		//IL_0199: Unknown result type (might be due to invalid IL or missing references)
		double wheelRadius = ConfigManager.CurrentConfig.WheelRadius;
		double coreRadius = ConfigManager.CurrentConfig.CoreRadius;
		bool enableMultiTier = ConfigManager.CurrentConfig.EnableMultiTier;
		double num = ((ConfigManager.CurrentConfig.SubWheelRadiusRatio > 1.1) ? ConfigManager.CurrentConfig.SubWheelRadiusRatio : 1.55);
		double subMaxR = (ConfigManager.CurrentConfig.SubWheelOuterRadius > 0.0)
			? ConfigManager.CurrentConfig.SubWheelOuterRadius
			: (wheelRadius * num);
		double maxEffectiveR = enableMultiTier ? Math.Max(wheelRadius, subMaxR + 25.0) : wheelRadius;
		double num2 = (base.Height = (base.Width = maxEffectiveR * 2.0 + 40.0));
		WheelCanvas.Width = num2;
		WheelCanvas.Height = num2;
		double length = num2 / 2.0 - coreRadius;
		double length2 = num2 / 2.0 - coreRadius;
		Canvas.SetLeft(CoreGrid, length);
		Canvas.SetTop(CoreGrid, length2);
		CoreGrid.Width = coreRadius * 2.0;
		CoreGrid.Height = coreRadius * 2.0;
		Panel.SetZIndex(CoreGrid, 5);
		OuterEllipse.Width = wheelRadius * 2.0 + 8.0;
		OuterEllipse.Height = wheelRadius * 2.0 + 8.0;
		PositionWindowOnTargetMonitor();
		CoreEllipse.Fill = _coreBgBrush;
		CoreEllipse.Stroke = _coreBorderBrush;
		string text = ConfigManager.CurrentConfig.CoreBgImagePath ?? "";
		if (!string.IsNullOrEmpty(text) && File.Exists(text))
		{
			try
			{
				BitmapImage image = new BitmapImage(new Uri(text, UriKind.Absolute));
				CoreEllipse.Fill = new ImageBrush(image)
				{
					Stretch = ParseStretch(ConfigManager.CurrentConfig.CoreBgStretch),
					Opacity = ConfigManager.CurrentConfig.CoreBgOpacity
				};
			}
			catch
			{
			}
		}
		CoreTitle.Foreground = _textColorBrush;
		CoreExitIcon.Fill = _textColorBrush;
		CoreExitIcon.Width = coreRadius * 0.42;
		CoreExitIcon.Height = coreRadius * 0.42;
		CoreTitle.FontSize = Math.Max(8.0, coreRadius / 5.0);
		CoreSubtitle.FontSize = Math.Max(6.0, coreRadius / 7.0);
		bool showCoreIcon = ConfigManager.CurrentConfig.ShowCoreIcon;
		string text2 = ConfigManager.CurrentConfig.CoreIconType ?? "Exit";
		CoreTitle.Visibility = Visibility.Collapsed;
		CoreSubtitle.Visibility = Visibility.Collapsed;
		double num7 = ((ConfigManager.CurrentConfig.CoreIconScale > 0.0) ? ConfigManager.CurrentConfig.CoreIconScale : 1.0);
		double coreImageOffsetX = ConfigManager.CurrentConfig.CoreImageOffsetX;
		double coreImageOffsetY = ConfigManager.CurrentConfig.CoreImageOffsetY;
		TranslateTransform renderTransform = ((coreImageOffsetX != 0.0 || coreImageOffsetY != 0.0) ? new TranslateTransform(coreImageOffsetX, coreImageOffsetY) : null);
		if (showCoreIcon)
		{
			bool num8 = text2 == "Custom";
			IconHelper.CustomIconItem customIconItem = null;
			if (num8 && !string.IsNullOrEmpty(ConfigManager.CurrentConfig.CoreCustomIconKey))
			{
				customIconItem = IconHelper.GetCustomIcons().FirstOrDefault((IconHelper.CustomIconItem c) => string.Equals(c.Key, ConfigManager.CurrentConfig.CoreCustomIconKey, StringComparison.OrdinalIgnoreCase));
			}
			bool flag = customIconItem != null && !customIconItem.IsSvg && File.Exists(customIconItem.FilePath);
			bool flag2 = !string.IsNullOrEmpty(ConfigManager.CurrentConfig.CoreCustomImagePath) && File.Exists(ConfigManager.CurrentConfig.CoreCustomImagePath);
			bool num9 = ((text2 == "Image") | flag) || (flag2 && text2 != "Custom" && text2 != "Exit");
			string text3 = (flag ? customIconItem.FilePath : (flag2 ? ConfigManager.CurrentConfig.CoreCustomImagePath : null));
			if (num9 && !string.IsNullOrEmpty(text3) && File.Exists(text3))
			{
				try
				{
					BitmapImage bitmapImage = new BitmapImage();
					bitmapImage.BeginInit();
					bitmapImage.UriSource = new Uri(text3, UriKind.Absolute);
					bitmapImage.CacheOption = BitmapCacheOption.OnLoad;
					bitmapImage.EndInit();
					((Freezable)bitmapImage).Freeze();
					double num10 = coreRadius * 1.85;
					CoreCustomImageEllipse.Width = num10;
					CoreCustomImageEllipse.Height = num10;
					CoreCustomImageEllipse.RenderTransform = null;
					ImageBrush imageBrush = new ImageBrush(bitmapImage)
					{
						Stretch = Stretch.UniformToFill,
						AlignmentX = AlignmentX.Center,
						AlignmentY = AlignmentY.Center
					};
					TransformGroup transformGroup = new TransformGroup();
					if (Math.Abs(num7 - 1.0) > 0.001)
					{
						transformGroup.Children.Add(new ScaleTransform(num7, num7, num10 / 2.0, num10 / 2.0));
					}
					if (Math.Abs(coreImageOffsetX) > 0.001 || Math.Abs(coreImageOffsetY) > 0.001)
					{
						transformGroup.Children.Add(new TranslateTransform(coreImageOffsetX, coreImageOffsetY));
					}
					if (transformGroup.Children.Count > 0)
					{
						imageBrush.Transform = transformGroup;
					}
					RenderOptions.SetBitmapScalingMode((DependencyObject)(object)imageBrush, BitmapScalingMode.HighQuality);
					RenderOptions.SetEdgeMode((DependencyObject)(object)CoreCustomImageEllipse, EdgeMode.Unspecified);
					CoreCustomImageEllipse.Fill = imageBrush;
					CoreCustomImageEllipse.Visibility = Visibility.Visible;
					CoreExitIcon.Visibility = Visibility.Collapsed;
				}
				catch
				{
					CoreCustomImageEllipse.Visibility = Visibility.Collapsed;
					CoreExitIcon.Visibility = Visibility.Collapsed;
				}
			}
			else
			{
				CoreCustomImageEllipse.Visibility = Visibility.Collapsed;
				Geometry coreIconGeometry = IconHelper.GetCoreIconGeometry(text2, ConfigManager.CurrentConfig.CoreCustomIconKey, ConfigManager.CurrentConfig.CoreCustomIconSvg);
				if (coreIconGeometry != null)
				{
					CoreExitIcon.Data = coreIconGeometry;
				}
				CoreExitIcon.Width = coreRadius * 0.42 * num7;
				CoreExitIcon.Height = coreRadius * 0.42 * num7;
				CoreExitIcon.RenderTransform = renderTransform;
				CoreExitIcon.Visibility = Visibility.Visible;
			}
		}
		else
		{
			CoreCustomImageEllipse.Visibility = Visibility.Collapsed;
			CoreExitIcon.Visibility = Visibility.Collapsed;
		}
		RenderStyleDecorations();
		RenderSectors();
		Storyboard storyboard = new Storyboard();
		BackEase easingFunction = new BackEase
		{
			EasingMode = EasingMode.EaseOut,
			Amplitude = 0.35
		};
		DoubleAnimation doubleAnimation = new DoubleAnimation(0.65, 1.0, new Duration(TimeSpan.FromMilliseconds(110.0)))
		{
			EasingFunction = easingFunction
		};
		Storyboard.SetTarget((DependencyObject)(object)doubleAnimation, (DependencyObject)(object)MainGrid);
		Storyboard.SetTargetProperty((DependencyObject)(object)doubleAnimation, new PropertyPath("RenderTransform.Children[0].ScaleX"));
		DoubleAnimation doubleAnimation2 = new DoubleAnimation(0.65, 1.0, new Duration(TimeSpan.FromMilliseconds(110.0)))
		{
			EasingFunction = easingFunction
		};
		Storyboard.SetTarget((DependencyObject)(object)doubleAnimation2, (DependencyObject)(object)MainGrid);
		Storyboard.SetTargetProperty((DependencyObject)(object)doubleAnimation2, new PropertyPath("RenderTransform.Children[0].ScaleY"));
		DoubleAnimation doubleAnimation3 = new DoubleAnimation(0.0, 1.0, new Duration(TimeSpan.FromMilliseconds(90.0)));
		Storyboard.SetTarget((DependencyObject)(object)doubleAnimation3, (DependencyObject)(object)MainGrid);
		Storyboard.SetTargetProperty((DependencyObject)(object)doubleAnimation3, new PropertyPath(UIElement.OpacityProperty));
		storyboard.Children.Add(doubleAnimation);
		storyboard.Children.Add(doubleAnimation2);
		storyboard.Children.Add(doubleAnimation3);
		storyboard.Begin();
	}

	private void RenderStyleDecorations()
	{
		_ = ConfigManager.CurrentConfig.UiStyle;
		double width = base.Width;
		double cx = width / 2.0;
		double cy = width / 2.0;
		double wheelRadius = ConfigManager.CurrentConfig.WheelRadius;
		double coreRadius = ConfigManager.CurrentConfig.CoreRadius;
		List<UIElement> list = new List<UIElement>();
		foreach (UIElement child in WheelCanvas.Children)
		{
			if (child is FrameworkElement { Tag: not null } frameworkElement && frameworkElement.Tag.ToString().StartsWith("Deco_"))
			{
				list.Add(child);
			}
		}
		foreach (UIElement item in list)
		{
			WheelCanvas.Children.Remove(item);
		}
		CoreEllipse.Visibility = Visibility.Visible;
		OuterEllipse.Visibility = Visibility.Collapsed;
		System.Windows.Shapes.Path path = CoreGrid.Children.OfType<System.Windows.Shapes.Path>().FirstOrDefault((System.Windows.Shapes.Path p) => p.Name == "DynamicGearPath");
		if (path != null)
		{
			CoreGrid.Children.Remove(path);
		}
		Grid grid = CoreGrid.Children.OfType<Grid>().FirstOrDefault((Grid g) => g.Name == "DynamicPawGrid");
		if (grid != null)
		{
			CoreGrid.Children.Remove(grid);
		}
		Grid grid2 = CoreGrid.Children.OfType<Grid>().FirstOrDefault((Grid g) => g.Name == "DynamicTechGrid");
		if (grid2 != null)
		{
			CoreGrid.Children.Remove(grid2);
		}
		int num = CoreGrid.Children.IndexOf(CoreTextPanel);
		if (num < 0)
		{
			num = 0;
		}
		if (_styleRenderer != null)
		{
			_styleRenderer.RenderDecorations(WheelCanvas, CoreGrid, cx, cy, wheelRadius, coreRadius, num);
		}
	}

	private void RenderSectors()
	{
		int sectorCount = _profile.SectorCount;
		double num = 360.0 / (double)sectorCount;
		double width = base.Width;
		double num2 = width / 2.0;
		double num3 = width / 2.0;
		string shape = ConfigManager.CurrentConfig.Shape ?? "Original";
		double gap = Math.Max(0.0, ConfigManager.CurrentConfig.SectorGap);
		double cornerRadius = Math.Max(0.0, ConfigManager.CurrentConfig.SectorCornerRadius);
		string text = ConfigManager.CurrentConfig.IconLayoutMode ?? "IconAndText";
		bool flag = ConfigManager.CurrentConfig.ShowText && text != "IconOnly";
		_sectorPaths.Clear();
		_contentPanels.Clear();
		_sectorTransforms.Clear();
		_containerTransforms.Clear();
		_sectorAngles.Clear();
		List<UIElement> list = new List<UIElement>();
		foreach (UIElement child in WheelCanvas.Children)
		{
			if (child != CoreGrid && child != OuterEllipse && (!(child is FrameworkElement { Tag: not null } frameworkElement) || !frameworkElement.Tag.ToString().StartsWith("Deco_")))
			{
				list.Add(child);
			}
		}
		foreach (UIElement item in list)
		{
			WheelCanvas.Children.Remove(item);
		}
		for (int i = 0; i < sectorCount; i++)
		{
			double num4 = (double)i * num;
			double startAngle = num4 - num / 2.0;
			double endAngle = num4 + num / 2.0;
			double num5 = num4 * (Math.PI / 180.0);
			double num6 = (_innerRadius + _outerRadius) / 2.0;
			double num7 = num2 + Math.Cos(num5) * num6;
			double num8 = num3 + Math.Sin(num5) * num6;
			Geometry data = IconHelper.CreateAdvancedSectorGeometry(num2, num3, startAngle, endAngle, _innerRadius, _outerRadius, shape, gap, cornerRadius);
			TranslateTransform translateTransform = new TranslateTransform(0.0, 0.0);
			System.Windows.Shapes.Path path = new System.Windows.Shapes.Path
			{
				Data = data,
				Fill = _defaultSectorBrush,
				Stroke = _sectorBorderBrush,
				StrokeThickness = _borderThickness,
				RenderTransform = translateTransform,
				Tag = i
			};
			Panel.SetZIndex(path, 1);
			WheelCanvas.Children.Insert(0, path);
			_styleRenderer?.ApplySectorHighlight(path, isHighlighted: false);
			_sectorPaths.Add(path);
			_sectorTransforms.Add(translateTransform);
			_sectorAngles.Add(num5);
			double width2 = sectorCount switch
			{
				4 => 124.0, 
				12 => 66.0, 
				_ => 100.0, 
			};
			double height = sectorCount switch
			{
				4 => 76.0, 
				12 => 52.0, 
				_ => 68.0, 
			};
			TranslateTransform translateTransform2 = new TranslateTransform(0.0, 0.0);
			Grid grid = new Grid
			{
				Width = width2,
				Height = height,
				RenderTransform = translateTransform2
			};
			StackPanel stackPanel = new StackPanel
			{
				Orientation = Orientation.Vertical,
				HorizontalAlignment = HorizontalAlignment.Center,
				VerticalAlignment = VerticalAlignment.Center
			};
			grid.Children.Add(stackPanel);
			string text2 = "未设置";
			string text3 = "Hotkey";
			string text4 = "";
			string iconKey = "";
			string text5 = "";
			if (i < _profile.Actions.Count && _profile.Actions[i] != null)
			{
				text2 = _profile.Actions[i].Name ?? "";
				text3 = _profile.Actions[i].Type ?? "Hotkey";
				text4 = _profile.Actions[i].Parameter ?? "";
				iconKey = _profile.Actions[i].IconKey ?? "";
				text5 = _profile.Actions[i].CustomIconSvg ?? "";
			}
			FrameworkElement frameworkElement2 = null;
			if (text != "TextOnly")
			{
				double num9 = ((ConfigManager.CurrentConfig.SectorIconSize > 0.0) ? ConfigManager.CurrentConfig.SectorIconSize : 20.0);
				double num10 = sectorCount switch
				{
					4 => 1.2, 
					12 => 0.82, 
					_ => 1.0, 
				};
				double num11 = ((text == "IconOnly") ? (num9 * 1.35) : num9) * num10;
				if (!string.IsNullOrEmpty(text5))
				{
					try
					{
						frameworkElement2 = new System.Windows.Shapes.Path
						{
							Data = Geometry.Parse(text5),
							Fill = _textColorBrush,
							Stretch = Stretch.Uniform,
							Width = num11,
							Height = num11,
							Margin = new Thickness(0.0, 0.0, 0.0, flag ? 2 : 0),
							HorizontalAlignment = HorizontalAlignment.Center
						};
					}
					catch
					{
					}
				}
				if (frameworkElement2 == null && !string.IsNullOrEmpty(iconKey))
				{
					if (iconKey.StartsWith("custom:", StringComparison.OrdinalIgnoreCase))
					{
						IconHelper.CustomIconItem customIconItem = IconHelper.GetCustomIcons().FirstOrDefault((IconHelper.CustomIconItem c) => c.Key == iconKey);
						if (customIconItem != null)
						{
							frameworkElement2 = ((!customIconItem.IsSvg) ? ((FrameworkElement)new Image
							{
								Width = num11,
								Height = num11,
								Stretch = Stretch.Uniform,
								Margin = new Thickness(0.0, 0.0, 0.0, flag ? 2 : 0),
								HorizontalAlignment = HorizontalAlignment.Center,
								Source = IconHelper.GetCustomImageSource(customIconItem.FilePath)
							}) : ((FrameworkElement)new System.Windows.Shapes.Path
							{
								Data = Geometry.Parse(customIconItem.SvgData),
								Fill = _textColorBrush,
								Stretch = Stretch.Uniform,
								Width = num11,
								Height = num11,
								Margin = new Thickness(0.0, 0.0, 0.0, flag ? 2 : 0),
								HorizontalAlignment = HorizontalAlignment.Center
							}));
						}
					}
					else
					{
						string svgPathByKey = IconHelper.GetSvgPathByKey(iconKey);
						if (!string.IsNullOrEmpty(svgPathByKey))
						{
							frameworkElement2 = new System.Windows.Shapes.Path
							{
								Data = Geometry.Parse(svgPathByKey),
								Fill = _textColorBrush,
								Stretch = Stretch.Uniform,
								Width = num11,
								Height = num11,
								Margin = new Thickness(0.0, 0.0, 0.0, flag ? 2 : 0),
								HorizontalAlignment = HorizontalAlignment.Center
							};
						}
					}
				}
				if (frameworkElement2 == null && (text3 == "Launch" || text3 == "App") && !string.IsNullOrEmpty(text4))
				{
					BitmapSource icon = IconHelper.GetIcon(text4);
					if (icon != null)
					{
						frameworkElement2 = new Image
						{
							Source = icon,
							Width = num11 + 4.0,
							Height = num11 + 4.0,
							Stretch = Stretch.Uniform,
							Margin = new Thickness(0.0, 0.0, 0.0, flag ? 2 : 0),
							HorizontalAlignment = HorizontalAlignment.Center
						};
					}
				}
				if (frameworkElement2 == null && text3 == "SwitchWindow")
				{
					frameworkElement2 = BuildSwitchWindowIcon(text4, num11, flag);
				}
				if (frameworkElement2 == null)
				{
					string vectorIconPath = GetVectorIconPath(text3, text4);
					if (!string.IsNullOrEmpty(vectorIconPath))
					{
						frameworkElement2 = new System.Windows.Shapes.Path
						{
							Data = Geometry.Parse(vectorIconPath),
							Fill = _textColorBrush,
							Stretch = Stretch.Uniform,
							Width = num11,
							Height = num11,
							Margin = new Thickness(0.0, 0.0, 0.0, flag ? 2 : 0),
							HorizontalAlignment = HorizontalAlignment.Center
						};
					}
				}
				if (frameworkElement2 != null)
				{
					stackPanel.Children.Add(frameworkElement2);
				}
			}
			if (flag && !string.IsNullOrEmpty(text2))
			{
				double num12 = ((ConfigManager.CurrentConfig.SectorFontSize > 0.0) ? ConfigManager.CurrentConfig.SectorFontSize : 11.0);
				double num13 = ((text == "TextOnly") ? (num12 + 1.0) : num12);
				switch (sectorCount)
				{
				case 12:
					num13 = Math.Min(num13, 10.0);
					break;
				case 4:
					num13 = Math.Max(num13, 12.0);
					break;
				}
				double maxWidth = sectorCount switch
				{
					4 => 120.0, 
					12 => 62.0, 
					_ => 96.0, 
				};
				TextBlock element = new TextBlock
				{
					Text = text2,
					Foreground = _textColorBrush,
					FontSize = num13,
					FontFamily = new FontFamily(ConfigManager.CurrentConfig.WheelFontFamily ?? "Microsoft YaHei UI, Segoe UI"),
					FontWeight = FontWeights.Medium,
					TextAlignment = TextAlignment.Center,
					TextWrapping = TextWrapping.Wrap,
					TextTrimming = TextTrimming.CharacterEllipsis,
					MaxWidth = maxWidth,
					MaxHeight = 34.0,
					Margin = new Thickness(0.0, 1.0, 0.0, 0.0),
					Effect = (Effect)base.Resources["TextShadow"]
				};
				TextOptions.SetTextFormattingMode((DependencyObject)(object)element, (TextFormattingMode)1);
				TextOptions.SetTextRenderingMode((DependencyObject)(object)element, TextRenderingMode.ClearType);
				stackPanel.Children.Add(element);
			}
			Canvas.SetLeft(grid, num7 - grid.Width / 2.0);
			Canvas.SetTop(grid, num8 - grid.Height / 2.0);
			Panel.SetZIndex(grid, 10);
			WheelCanvas.Children.Add(grid);
			_contentPanels.Add(stackPanel);
			_containerTransforms.Add(translateTransform2);
		}
	}

	private string? GetVectorIconPath(string type, string parameter)
	{
		switch (type)
		{
		case "Folder":
		case "OpenFolder":
			return IconHelper.GetSvgPathByKey("Folder");
		case "Hotkey":
			return "M19,15H5V5H19M19,3H5C3.89,3 3,3.89 3,5V15C3,16.1 3.89,17 5,17H19C20.1,17 21,16.1 21,15V5C21,3.89 20.1,3 19,3M2,18H22V20H2V18Z";
		case "Command":
			return IconHelper.GetSvgPathByKey("Command");
		case "SwitchWindow":
			// 默认程序图标（窗口切换）：四格程序窗格
			return "M4,4H11V11H4V4M13,4H20V11H13V4M4,13H11V20H4V13M13,13H20V20H13V13Z";
		case "System":
			if (!string.IsNullOrEmpty(parameter))
			{
				switch (parameter.Trim().ToLower())
				{
				case "lock":
					return IconHelper.GetSvgPathByKey("Lock");
				case "volumeup":
					return IconHelper.GetSvgPathByKey("VolumeUp");
				case "volumedown":
					return IconHelper.GetSvgPathByKey("VolumeDown");
				case "volumemute":
					return IconHelper.GetSvgPathByKey("VolumeMute");
				case "showdesktop":
					return IconHelper.GetSvgPathByKey("ShowDesktop");
				case "screenshot":
					return IconHelper.GetSvgPathByKey("Screenshot");
				}
			}
			break;
		}
		return null;
	}

	private Geometry CreateSectorGeometry(double startAngleDegrees, double endAngleDegrees, double innerRadius, double outerRadius)
	{
		//IL_00d1: Unknown result type (might be due to invalid IL or missing references)
		//IL_00dc: Unknown result type (might be due to invalid IL or missing references)
		//IL_00e2: Unknown result type (might be due to invalid IL or missing references)
		//IL_00fc: Unknown result type (might be due to invalid IL or missing references)
		//IL_0107: Unknown result type (might be due to invalid IL or missing references)
		//IL_010b: Unknown result type (might be due to invalid IL or missing references)
		//IL_0125: Unknown result type (might be due to invalid IL or missing references)
		double num = startAngleDegrees * (Math.PI / 180.0);
		double num2 = endAngleDegrees * (Math.PI / 180.0);
		double num3 = base.Width / 2.0;
		double num4 = base.Height / 2.0;
		Point val = default(Point);
		val = new Point(num3 + Math.Cos(num) * outerRadius, num4 + Math.Sin(num) * outerRadius);
		Point point = default(Point);
		point = new Point(num3 + Math.Cos(num2) * outerRadius, num4 + Math.Sin(num2) * outerRadius);
		Point point2 = default(Point);
		point2 = new Point(num3 + Math.Cos(num2) * innerRadius, num4 + Math.Sin(num2) * innerRadius);
		Point point3 = default(Point);
		point3 = new Point(num3 + Math.Cos(num) * innerRadius, num4 + Math.Sin(num) * innerRadius);
		bool isLargeArc = Math.Abs(endAngleDegrees - startAngleDegrees) > 180.0;
		StreamGeometry streamGeometry = new StreamGeometry();
		using (StreamGeometryContext streamGeometryContext = streamGeometry.Open())
		{
			streamGeometryContext.BeginFigure(val, isFilled: true, isClosed: true);
			streamGeometryContext.ArcTo(point, new Size(outerRadius, outerRadius), 0.0, isLargeArc, SweepDirection.Clockwise, isStroked: true, isSmoothJoin: true);
			streamGeometryContext.LineTo(point2, isStroked: true, isSmoothJoin: false);
			streamGeometryContext.ArcTo(point3, new Size(innerRadius, innerRadius), 0.0, isLargeArc, SweepDirection.Counterclockwise, isStroked: true, isSmoothJoin: true);
			streamGeometryContext.LineTo(val, isStroked: true, isSmoothJoin: false);
		}
		((Freezable)streamGeometry).Freeze();
		return streamGeometry;
	}

	public void SetOuterEscapeState(bool isEscaped)
	{
		if (_isOuterEscaped != isEscaped)
		{
			_isOuterEscaped = isEscaped;
			DoubleAnimation animation = new DoubleAnimation
			{
				From = Opacity,
				To = (isEscaped ? 0.38 : 1.0),
				Duration = TimeSpan.FromMilliseconds(120.0),
				EasingFunction = new QuadraticEase
				{
					EasingMode = EasingMode.EaseOut
				}
			};
			BeginAnimation(UIElement.OpacityProperty, animation);
		}
	}

	private void ClearSubTier()
	{
		for (int i = 0; i < _subSectorPaths.Count; i++)
		{
			System.Windows.Shapes.Path subSectorPath = _subSectorPaths[i];
			subSectorPath.BeginAnimation(UIElement.OpacityProperty, null);
			subSectorPath.Opacity = 0.0;
			subSectorPath.Visibility = Visibility.Collapsed;
			if (i < _subSectorTransforms.Count)
			{
				TranslateTransform transform = _subSectorTransforms[i];
				transform.BeginAnimation(TranslateTransform.XProperty, null);
				transform.BeginAnimation(TranslateTransform.YProperty, null);
				transform.X = 0.0;
				transform.Y = 0.0;
			}
		}
		for (int i = 0; i < _subContentContainers.Count; i++)
		{
			Grid subContentContainer = _subContentContainers[i];
			subContentContainer.BeginAnimation(UIElement.OpacityProperty, null);
			subContentContainer.Opacity = 0.0;
			subContentContainer.Visibility = Visibility.Collapsed;
			if (i < _subContainerTransforms.Count)
			{
				TranslateTransform transform = _subContainerTransforms[i];
				transform.BeginAnimation(TranslateTransform.XProperty, null);
				transform.BeginAnimation(TranslateTransform.YProperty, null);
				transform.X = 0.0;
				transform.Y = 0.0;
			}
		}
		_subSectorPaths.Clear();
		_subContentContainers.Clear();
		_subSectorTransforms.Clear();
		_subContainerTransforms.Clear();
		_subSectorAngles.Clear();
		_activeSubTierParentSector = -1;
		_currentHighlightedSubSector = -1;
	}

	private void ActivateCachedSubTier(int parentIndex, SubTierVisuals visuals)
	{
		_activeSubTierParentSector = parentIndex;
		_subSectorPaths.AddRange(visuals.Paths);
		_subContentContainers.AddRange(visuals.Containers);
		_subSectorTransforms.AddRange(visuals.PathTransforms);
		_subContainerTransforms.AddRange(visuals.ContainerTransforms);
		_subSectorAngles.AddRange(visuals.Angles);

		Duration duration = new Duration(TimeSpan.FromMilliseconds(110.0));
		DoubleAnimation fadeIn = new DoubleAnimation(0.0, 1.0, duration)
		{
			EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseOut }
		};
		for (int i = 0; i < _subSectorPaths.Count; i++)
		{
			System.Windows.Shapes.Path path = _subSectorPaths[i];
			path.Visibility = Visibility.Visible;
			path.Opacity = 0.0;
			path.BeginAnimation(UIElement.OpacityProperty, fadeIn.Clone());
			if (i < _subSectorTransforms.Count)
			{
				_subSectorTransforms[i].BeginAnimation(TranslateTransform.XProperty, null);
				_subSectorTransforms[i].BeginAnimation(TranslateTransform.YProperty, null);
				_subSectorTransforms[i].X = 0.0;
				_subSectorTransforms[i].Y = 0.0;
			}
		}
		for (int i = 0; i < _subContentContainers.Count; i++)
		{
			Grid container = _subContentContainers[i];
			container.Visibility = Visibility.Visible;
			container.Opacity = 0.0;
			container.BeginAnimation(UIElement.OpacityProperty, fadeIn.Clone());
			if (i < _subContainerTransforms.Count)
			{
				_subContainerTransforms[i].BeginAnimation(TranslateTransform.XProperty, null);
				_subContainerTransforms[i].BeginAnimation(TranslateTransform.YProperty, null);
				_subContainerTransforms[i].X = 0.0;
				_subContainerTransforms[i].Y = 0.0;
			}
		}
	}

	private void ShowSubTier(int parentIndex)
	{
		if (ConfigManager.CurrentConfig.SubmenuStyle == "Fan") { RenderFanSubtier(parentIndex); return; }
		ClearSubTier();
		if (!ConfigManager.CurrentConfig.EnableMultiTier || parentIndex < 0 || parentIndex >= _profile.Actions.Count)
		{
			return;
		}
		ActionItem actionItem = _profile.Actions[parentIndex];
		if (actionItem == null || actionItem.SubActions == null || actionItem.SubActions.Count == 0)
		{
			return;
		}
		if (_subTierCache.TryGetValue(parentIndex, out SubTierVisuals? cachedVisuals))
		{
			ActivateCachedSubTier(parentIndex, cachedVisuals);
			return;
		}
		int sectorCount = _profile.SectorCount;
		double num = 360.0 / (double)sectorCount;
		double width = base.Width;
		double num2 = width / 2.0;
		double num3 = width / 2.0;
		string shape = ConfigManager.CurrentConfig.Shape ?? "Original";
		string text = ConfigManager.CurrentConfig.IconLayoutMode ?? "IconAndText";
		bool flag = ConfigManager.CurrentConfig.ShowText && text != "IconOnly";
		double num4 = ((ConfigManager.CurrentConfig.SubWheelOuterRadius > 0.0) ? ConfigManager.CurrentConfig.SubWheelOuterRadius : (_outerRadius * 1.55));
		double num5 = ((ConfigManager.CurrentConfig.SubWheelInnerGap >= 0.0) ? ConfigManager.CurrentConfig.SubWheelInnerGap : Math.Max(0.0, ConfigManager.CurrentConfig.SectorGap));
		double num6 = _outerRadius + num5 + 2.0;
		double cornerRadius = ((ConfigManager.CurrentConfig.SubWheelCornerRadius >= 0.0) ? ConfigManager.CurrentConfig.SubWheelCornerRadius : Math.Max(0.0, ConfigManager.CurrentConfig.SectorCornerRadius));
		double num7 = ((ConfigManager.CurrentConfig.SubWheelIconSize > 0.0) ? ConfigManager.CurrentConfig.SubWheelIconSize : ((text == "IconOnly") ? 22.0 : 17.0));
		double fontSize = ((ConfigManager.CurrentConfig.SubWheelFontSize > 0.0) ? ConfigManager.CurrentConfig.SubWheelFontSize : Math.Max(8.5, ConfigManager.CurrentConfig.SectorFontSize - 1.0));
		double num8 = (double)parentIndex * num - num / 2.0;
		List<ActionItem> subActions = actionItem.SubActions;
		int count = subActions.Count;
		double num9 = num / (double)count;
		_activeSubTierParentSector = parentIndex;
		for (int i = 0; i < count; i++)
		{
			double num10 = num8 + (double)i * num9;
			double num11 = num10 + num9;
			double num12 = (num10 + num11) / 2.0 * (Math.PI / 180.0);
			double num13 = (num6 + num4) / 2.0;
			double num14 = num2 + Math.Cos(num12) * num13;
			double num15 = num3 + Math.Sin(num12) * num13;
			Geometry data = IconHelper.CreateAdvancedSectorGeometry(num2, num3, num10, num11, num6, num4, shape, num5, cornerRadius);
			ScaleTransform scaleTransform = new ScaleTransform(0.75, 0.75, num2, num3);
			TranslateTransform translateTransform = new TranslateTransform(0.0, 0.0);
			TransformGroup transformGroup = new TransformGroup();
			transformGroup.Children.Add(scaleTransform);
			transformGroup.Children.Add(translateTransform);
			System.Windows.Shapes.Path path = new System.Windows.Shapes.Path
			{
				Data = data,
				Fill = _subDefaultSectorBrush,
				Stroke = _subSectorBorderBrush,
				StrokeThickness = _borderThickness,
				Tag = $"sub_{parentIndex}_{i}",
				Opacity = 0.0,
				RenderTransform = transformGroup
			};
			Panel.SetZIndex(path, 15);
			WheelCanvas.Children.Add(path);
			_subSectorPaths.Add(path);
			_subSectorTransforms.Add(translateTransform);
			_subSectorAngles.Add(num12);
			double num16 = ((count >= 4) ? 76.0 : 92.0);
			double num17 = ((count >= 4) ? 54.0 : 64.0);
			ScaleTransform scaleTransform2 = new ScaleTransform(0.75, 0.75, num16 / 2.0, num17 / 2.0);
			TranslateTransform translateTransform2 = new TranslateTransform(0.0, 0.0);
			TransformGroup transformGroup2 = new TransformGroup();
			transformGroup2.Children.Add(scaleTransform2);
			transformGroup2.Children.Add(translateTransform2);
			Grid grid = new Grid
			{
				Width = num16,
				Height = num17,
				Opacity = 0.0,
				RenderTransform = transformGroup2
			};
			StackPanel stackPanel = new StackPanel
			{
				Orientation = Orientation.Vertical,
				HorizontalAlignment = HorizontalAlignment.Center,
				VerticalAlignment = VerticalAlignment.Center
			};
			grid.Children.Add(stackPanel);
			ActionItem actionItem2 = subActions[i];
			string text2 = actionItem2?.Name ?? "子动作";
			string text3 = actionItem2?.Type ?? "Hotkey";
			string text4 = actionItem2?.Parameter ?? "";
			string iconKey = actionItem2?.IconKey ?? "";
			string text5 = actionItem2?.CustomIconSvg ?? "";
			FrameworkElement frameworkElement = null;
			if (text != "TextOnly")
			{
				if (!string.IsNullOrEmpty(text5))
				{
					try
					{
						frameworkElement = new System.Windows.Shapes.Path
						{
							Data = Geometry.Parse(text5),
							Fill = _subTextColorBrush,
							Stretch = Stretch.Uniform,
							Width = num7,
							Height = num7,
							Margin = new Thickness(0.0, 0.0, 0.0, flag ? 2 : 0),
							HorizontalAlignment = HorizontalAlignment.Center
						};
					}
					catch
					{
					}
				}
				if (frameworkElement == null && !string.IsNullOrEmpty(iconKey))
				{
					if (iconKey.StartsWith("custom:", StringComparison.OrdinalIgnoreCase))
					{
						IconHelper.CustomIconItem customIconItem = IconHelper.GetCustomIcons().FirstOrDefault((IconHelper.CustomIconItem c) => c.Key == iconKey);
						if (customIconItem != null)
						{
							frameworkElement = ((!customIconItem.IsSvg) ? ((FrameworkElement)new Image
							{
								Width = num7,
								Height = num7,
								Stretch = Stretch.Uniform,
								Margin = new Thickness(0.0, 0.0, 0.0, flag ? 2 : 0),
								HorizontalAlignment = HorizontalAlignment.Center,
								Source = IconHelper.GetCustomImageSource(customIconItem.FilePath)
							}) : ((FrameworkElement)new System.Windows.Shapes.Path
							{
								Data = Geometry.Parse(customIconItem.SvgData),
								Fill = _subTextColorBrush,
								Stretch = Stretch.Uniform,
								Width = num7,
								Height = num7,
								Margin = new Thickness(0.0, 0.0, 0.0, flag ? 2 : 0),
								HorizontalAlignment = HorizontalAlignment.Center
							}));
						}
					}
					else
					{
						string svgPathByKey = IconHelper.GetSvgPathByKey(iconKey);
						if (!string.IsNullOrEmpty(svgPathByKey))
						{
							frameworkElement = new System.Windows.Shapes.Path
							{
								Data = Geometry.Parse(svgPathByKey),
								Fill = _subTextColorBrush,
								Stretch = Stretch.Uniform,
								Width = num7,
								Height = num7,
								Margin = new Thickness(0.0, 0.0, 0.0, flag ? 2 : 0),
								HorizontalAlignment = HorizontalAlignment.Center
							};
						}
					}
				}
				if (frameworkElement == null && (text3 == "Launch" || text3 == "App") && !string.IsNullOrEmpty(text4))
				{
					BitmapSource icon = IconHelper.GetIcon(text4);
					if (icon != null)
					{
						frameworkElement = new Image
						{
							Source = icon,
							Width = num7 + 2.0,
							Height = num7 + 2.0,
							Stretch = Stretch.Uniform,
							Margin = new Thickness(0.0, 0.0, 0.0, flag ? 2 : 0),
							HorizontalAlignment = HorizontalAlignment.Center
						};
					}
				}
				if (frameworkElement == null && text3 == "SwitchWindow")
					{
						frameworkElement = BuildSwitchWindowIcon(text4, num7, flag);
					}
					if (frameworkElement == null)
					{
						string vectorIconPath = GetVectorIconPath(text3, text4);
						if (!string.IsNullOrEmpty(vectorIconPath))
						{
							frameworkElement = new System.Windows.Shapes.Path
							{
								Data = Geometry.Parse(vectorIconPath),
								Fill = _subTextColorBrush,
								Stretch = Stretch.Uniform,
								Width = num7,
								Height = num7,
								Margin = new Thickness(0.0, 0.0, 0.0, flag ? 2 : 0),
								HorizontalAlignment = HorizontalAlignment.Center
							};
						}
					}
				if (frameworkElement != null)
				{
					stackPanel.Children.Add(frameworkElement);
				}
			}
			if (flag && !string.IsNullOrEmpty(text2))
			{
				TextBlock element = new TextBlock
				{
					Text = text2,
					Foreground = _subTextColorBrush,
					FontSize = fontSize,
					FontFamily = new FontFamily(ConfigManager.CurrentConfig.WheelFontFamily ?? "Microsoft YaHei UI, Segoe UI"),
					FontWeight = FontWeights.Medium,
					TextAlignment = TextAlignment.Center,
					TextWrapping = TextWrapping.Wrap,
					TextTrimming = TextTrimming.CharacterEllipsis,
					MaxWidth = num16 - 4.0,
					MaxHeight = 28.0,
					Margin = new Thickness(0.0, 1.0, 0.0, 0.0),
					Effect = (Effect)base.Resources["TextShadow"]
				};
				TextOptions.SetTextFormattingMode((DependencyObject)(object)element, (TextFormattingMode)1);
				TextOptions.SetTextRenderingMode((DependencyObject)(object)element, TextRenderingMode.ClearType);
				stackPanel.Children.Add(element);
			}
			Canvas.SetLeft(grid, num14 - grid.Width / 2.0);
			Canvas.SetTop(grid, num15 - grid.Height / 2.0);
			Panel.SetZIndex(grid, 30);
			WheelCanvas.Children.Add(grid);
			_subContentContainers.Add(grid);
			_subContainerTransforms.Add(translateTransform2);
			BackEase easingFunction = new BackEase
			{
				Amplitude = 0.35,
				EasingMode = EasingMode.EaseOut
			};
			Duration duration = new Duration(TimeSpan.FromMilliseconds(130.0));
			DoubleAnimation animation = new DoubleAnimation(0.75, 1.0, duration)
			{
				EasingFunction = easingFunction
			};
			DoubleAnimation animation2 = new DoubleAnimation(0.0, 1.0, duration)
			{
				EasingFunction = new CircleEase
				{
					EasingMode = EasingMode.EaseOut
				}
			};
			scaleTransform.BeginAnimation(ScaleTransform.ScaleXProperty, animation);
			scaleTransform.BeginAnimation(ScaleTransform.ScaleYProperty, animation);
			path.BeginAnimation(UIElement.OpacityProperty, animation2);
			scaleTransform2.BeginAnimation(ScaleTransform.ScaleXProperty, animation);
			scaleTransform2.BeginAnimation(ScaleTransform.ScaleYProperty, animation);
			grid.BeginAnimation(UIElement.OpacityProperty, animation2);
		}
		_subTierCache[parentIndex] = new SubTierVisuals(
			new List<System.Windows.Shapes.Path>(_subSectorPaths),
			new List<Grid>(_subContentContainers),
			new List<TranslateTransform>(_subSectorTransforms),
			new List<TranslateTransform>(_subContainerTransforms),
			new List<double>(_subSectorAngles));
	}

	public void HighlightSector(int index)
	{
		HighlightSector(index, -1, showSubTier: true);
	}

	public void HighlightSector(int mainIndex, int subIndex)
	{
		HighlightSector(mainIndex, subIndex, showSubTier: true);
	}

	public void HighlightSector(int mainIndex, int subIndex, bool showSubTier)
	{
		_ = _currentHighlightedSector;
		int currentHighlightedSector = _currentHighlightedSector;
		_currentHighlightedSector = mainIndex;
		_ = _currentHighlightedSubSector;
		_ = _currentHighlightedSubSector;
		_currentHighlightedSubSector = subIndex;
		AppConfig currentConfig = ConfigManager.CurrentConfig;
		double num = ((currentConfig == null || !(currentConfig.CustomAnimationDurationMs > 0.0)) ? ((double)(ConfigManager.CurrentConfig?.AnimationSpeed switch
		{
			"Elegant" => 130, 
			"Fast" => 35, 
			"Custom" => (int)(ConfigManager.CurrentConfig?.CustomAnimationDurationMs ?? 80.0), 
			_ => 80, 
		})) : ConfigManager.CurrentConfig.CustomAnimationDurationMs);
		int num2 = (int)num;
		CubicEase easingFunction = new CubicEase
		{
			EasingMode = EasingMode.EaseOut
		};
		Duration duration = new Duration(TimeSpan.FromMilliseconds(num2));
		int num3 = Math.Max(30, (int)((double)num2 * 1.12));
		Duration duration2 = new Duration(TimeSpan.FromMilliseconds(num3));
		if (CoreScale != null)
		{
			double toValue = ((mainIndex == -1) ? 1.1 : 1.0);
			CoreScale.BeginAnimation(ScaleTransform.ScaleXProperty, new DoubleAnimation(CoreScale.ScaleX, toValue, duration2)
			{
				EasingFunction = easingFunction
			});
			CoreScale.BeginAnimation(ScaleTransform.ScaleYProperty, new DoubleAnimation(CoreScale.ScaleY, toValue, duration2)
			{
				EasingFunction = easingFunction
			});
		}
		if (mainIndex == -1)
		{
			CoreExitIcon.Fill = new SolidColorBrush(Color.FromRgb(244, 63, 94));
			if (_styleRenderer != null)
			{
				_styleRenderer.ApplyExitHighlight(CoreExitIcon, isHighlighted: true);
			}
		}
		else
		{
			CoreExitIcon.Fill = _textColorBrush;
			if (_styleRenderer != null)
			{
				_styleRenderer.ApplyExitHighlight(CoreExitIcon, isHighlighted: false);
			}
		}
		if (currentHighlightedSector >= 0 && currentHighlightedSector < _sectorPaths.Count && currentHighlightedSector != mainIndex)
		{
			System.Windows.Shapes.Path path = _sectorPaths[currentHighlightedSector];
			StackPanel obj = ((currentHighlightedSector < _contentPanels.Count) ? _contentPanels[currentHighlightedSector] : null);
			TranslateTransform translateTransform = ((currentHighlightedSector < _sectorTransforms.Count) ? _sectorTransforms[currentHighlightedSector] : null);
			TranslateTransform translateTransform2 = ((currentHighlightedSector < _containerTransforms.Count) ? _containerTransforms[currentHighlightedSector] : null);
			path.Fill = _defaultSectorBrush;
			path.Stroke = _sectorBorderBrush;
			path.StrokeThickness = _borderThickness;
			Panel.SetZIndex(path, 0);
			if (translateTransform != null)
			{
				translateTransform.BeginAnimation(TranslateTransform.XProperty, new DoubleAnimation(translateTransform.X, 0.0, duration)
				{
					EasingFunction = easingFunction
				});
				translateTransform.BeginAnimation(TranslateTransform.YProperty, new DoubleAnimation(translateTransform.Y, 0.0, duration)
				{
					EasingFunction = easingFunction
				});
			}
			if (translateTransform2 != null)
			{
				translateTransform2.BeginAnimation(TranslateTransform.XProperty, new DoubleAnimation(translateTransform2.X, 0.0, duration)
				{
					EasingFunction = easingFunction
				});
				translateTransform2.BeginAnimation(TranslateTransform.YProperty, new DoubleAnimation(translateTransform2.Y, 0.0, duration)
				{
					EasingFunction = easingFunction
				});
			}
			TextBlock textBlock = obj?.Children.OfType<TextBlock>().FirstOrDefault();
			System.Windows.Shapes.Path path2 = obj?.Children.OfType<System.Windows.Shapes.Path>().FirstOrDefault();
			if (textBlock != null)
			{
				textBlock.Foreground = _textColorBrush;
				textBlock.FontWeight = FontWeights.Medium;
			}
			if (path2 != null)
			{
				path2.Fill = _textColorBrush;
			}
			if (_styleRenderer != null)
			{
				_styleRenderer.ApplySectorHighlight(path, isHighlighted: false);
			}
		}
		if (mainIndex >= 0 && mainIndex < _sectorPaths.Count)
		{
			System.Windows.Shapes.Path path3 = _sectorPaths[mainIndex];
			StackPanel obj2 = ((mainIndex < _contentPanels.Count) ? _contentPanels[mainIndex] : null);
			TranslateTransform translateTransform3 = ((mainIndex < _sectorTransforms.Count) ? _sectorTransforms[mainIndex] : null);
			TranslateTransform translateTransform4 = ((mainIndex < _containerTransforms.Count) ? _containerTransforms[mainIndex] : null);
			double num4 = ((mainIndex < _sectorAngles.Count) ? _sectorAngles[mainIndex] : 0.0);
			path3.Fill = _highlightSectorBrush;
			path3.Stroke = _highlightBorderBrush;
			path3.StrokeThickness = _highlightBorderThickness;
			Panel.SetZIndex(path3, 5);
			double toValue2 = Math.Cos(num4) * 5.5;
			double toValue3 = Math.Sin(num4) * 5.5;
			if (translateTransform3 != null)
			{
				translateTransform3.BeginAnimation(TranslateTransform.XProperty, new DoubleAnimation(translateTransform3.X, toValue2, duration)
				{
					EasingFunction = easingFunction
				});
				translateTransform3.BeginAnimation(TranslateTransform.YProperty, new DoubleAnimation(translateTransform3.Y, toValue3, duration)
				{
					EasingFunction = easingFunction
				});
			}
			if (translateTransform4 != null)
			{
				translateTransform4.BeginAnimation(TranslateTransform.XProperty, new DoubleAnimation(translateTransform4.X, toValue2, duration)
				{
					EasingFunction = easingFunction
				});
				translateTransform4.BeginAnimation(TranslateTransform.YProperty, new DoubleAnimation(translateTransform4.Y, toValue3, duration)
				{
					EasingFunction = easingFunction
				});
			}
			TextBlock textBlock2 = obj2?.Children.OfType<TextBlock>().FirstOrDefault();
			System.Windows.Shapes.Path path4 = obj2?.Children.OfType<System.Windows.Shapes.Path>().FirstOrDefault();
			if (textBlock2 != null)
			{
				textBlock2.Foreground = Brushes.White;
				textBlock2.FontWeight = FontWeights.Bold;
			}
			if (path4 != null)
			{
				path4.Fill = Brushes.White;
			}
			if (_styleRenderer != null)
			{
				_styleRenderer.ApplySectorHighlight(path3, isHighlighted: true);
			}
		}
		if (showSubTier && mainIndex >= 0 && mainIndex < _profile.Actions.Count)
		{
			if (_activeSubTierParentSector != mainIndex)
			{
				ShowSubTier(mainIndex);
			}
		}
		else if (_activeSubTierParentSector != -1)
		{
			ClearSubTier();
		}
		if (_subSectorPaths.Count <= 0)
		{
			return;
		}
		for (int i = 0; i < _subSectorPaths.Count; i++)
		{
			System.Windows.Shapes.Path path5 = _subSectorPaths[i];
			TranslateTransform translateTransform5 = ((i < _subSectorTransforms.Count) ? _subSectorTransforms[i] : null);
			Grid obj3 = ((i < _subContentContainers.Count) ? _subContentContainers[i] : null);
			TranslateTransform translateTransform6 = ((i < _subContainerTransforms.Count) ? _subContainerTransforms[i] : null);
			double num5 = ((i < _subSectorAngles.Count) ? _subSectorAngles[i] : 0.0);
			TextBlock textBlock3 = obj3?.Children.OfType<StackPanel>().FirstOrDefault()?.Children.OfType<TextBlock>().FirstOrDefault();
			System.Windows.Shapes.Path path6 = obj3?.Children.OfType<StackPanel>().FirstOrDefault()?.Children.OfType<System.Windows.Shapes.Path>().FirstOrDefault();
			if (i == subIndex)
			{
				path5.Fill = _subHighlightSectorBrush;
				path5.Stroke = _subHighlightBorderBrush;
				path5.StrokeThickness = _highlightBorderThickness;
				Panel.SetZIndex(path5, 18);
				ApplySubSectorGlow(path5, isHighlighted: true);
				if (textBlock3 != null)
				{
					textBlock3.Foreground = Brushes.White;
					textBlock3.FontWeight = FontWeights.Bold;
				}
				if (path6 != null)
				{
					path6.Fill = Brushes.White;
				}
				double toValue4 = Math.Cos(num5) * 4.0;
				double toValue5 = Math.Sin(num5) * 4.0;
				if (translateTransform5 != null)
				{
					translateTransform5.BeginAnimation(TranslateTransform.XProperty, new DoubleAnimation(translateTransform5.X, toValue4, duration)
					{
						EasingFunction = easingFunction
					});
					translateTransform5.BeginAnimation(TranslateTransform.YProperty, new DoubleAnimation(translateTransform5.Y, toValue5, duration)
					{
						EasingFunction = easingFunction
					});
				}
				if (translateTransform6 != null)
				{
					translateTransform6.BeginAnimation(TranslateTransform.XProperty, new DoubleAnimation(translateTransform6.X, toValue4, duration)
					{
						EasingFunction = easingFunction
					});
					translateTransform6.BeginAnimation(TranslateTransform.YProperty, new DoubleAnimation(translateTransform6.Y, toValue5, duration)
					{
						EasingFunction = easingFunction
					});
				}
			}
			else
			{
				path5.Fill = _subDefaultSectorBrush;
				path5.Stroke = _subSectorBorderBrush;
				path5.StrokeThickness = _borderThickness;
				Panel.SetZIndex(path5, 15);
				ApplySubSectorGlow(path5, isHighlighted: false);
				if (textBlock3 != null)
				{
					textBlock3.Foreground = _subTextColorBrush;
					textBlock3.FontWeight = FontWeights.Medium;
				}
				if (path6 != null)
				{
					path6.Fill = _subTextColorBrush;
				}
				if (translateTransform5 != null && (translateTransform5.X != 0.0 || translateTransform5.Y != 0.0))
				{
					translateTransform5.BeginAnimation(TranslateTransform.XProperty, new DoubleAnimation(translateTransform5.X, 0.0, duration)
					{
						EasingFunction = easingFunction
					});
					translateTransform5.BeginAnimation(TranslateTransform.YProperty, new DoubleAnimation(translateTransform5.Y, 0.0, duration)
					{
						EasingFunction = easingFunction
					});
				}
				if (translateTransform6 != null && (translateTransform6.X != 0.0 || translateTransform6.Y != 0.0))
				{
					translateTransform6.BeginAnimation(TranslateTransform.XProperty, new DoubleAnimation(translateTransform6.X, 0.0, duration)
					{
						EasingFunction = easingFunction
					});
					translateTransform6.BeginAnimation(TranslateTransform.YProperty, new DoubleAnimation(translateTransform6.Y, 0.0, duration)
					{
						EasingFunction = easingFunction
					});
				}
			}
		}
	}

	private void ApplySubSectorGlow(System.Windows.Shapes.Path path, bool isHighlighted)
	{
		if (!isHighlighted)
		{
			path.Effect = null;
			return;
		}
		string text = ConfigManager.CurrentConfig?.SubWheelHighlightGlowPreset ?? "FollowPrimary";
		if (text == "FollowPrimary")
		{
			text = ConfigManager.CurrentConfig?.HighlightGlowPreset ?? "Auto";
		}
		if (text == "None")
		{
			path.Effect = null;
			return;
		}
		Color color;
		if (!(text == "Custom") || string.IsNullOrEmpty(ConfigManager.CurrentConfig?.SubWheelHighlightGlowColor))
		{
			color = text switch
			{
				"Lilac" => Color.FromRgb(168, 85, 247), 
				"Blue" => Color.FromRgb(59, 130, 246), 
				"Emerald" => Color.FromRgb(16, 185, 129), 
				"Rose" => Color.FromRgb(236, 72, 153), 
				"Amber" => Color.FromRgb(245, 158, 11), 
				"Red" => Color.FromRgb(239, 68, 68), 
				"White" => Color.FromRgb(byte.MaxValue, byte.MaxValue, byte.MaxValue), 
				_ => (_subHighlightBorderBrush is SolidColorBrush { Color: { A: >0 } } solidColorBrush) ? solidColorBrush.Color : ((_subHighlightSectorBrush is SolidColorBrush { Color: { A: >0 } } solidColorBrush2) ? solidColorBrush2.Color : ((!(_highlightBorderBrush is SolidColorBrush { Color: { A: >0 } } solidColorBrush3)) ? Color.FromRgb(59, 130, 246) : solidColorBrush3.Color)), 
			};
		}
		else
		{
			try
			{
				color = (Color)ColorConverter.ConvertFromString(ConfigManager.CurrentConfig.SubWheelHighlightGlowColor);
			}
			catch
			{
				color = Color.FromRgb(168, 85, 247);
			}
		}
		double num;
		if (!(ConfigManager.CurrentConfig?.SubWheelHighlightGlowPreset == "FollowPrimary"))
		{
			AppConfig currentConfig = ConfigManager.CurrentConfig;
			num = ((currentConfig != null && currentConfig.SubWheelHighlightGlowRadius > 0.0) ? ConfigManager.CurrentConfig.SubWheelHighlightGlowRadius : 24.0);
		}
		else
		{
			AppConfig currentConfig2 = ConfigManager.CurrentConfig;
			num = ((currentConfig2 != null && currentConfig2.HighlightGlowRadius > 0.0) ? ConfigManager.CurrentConfig.HighlightGlowRadius : 24.0);
		}
		double blurRadius = num;
		double num2;
		if (!(ConfigManager.CurrentConfig?.SubWheelHighlightGlowPreset == "FollowPrimary"))
		{
			AppConfig currentConfig3 = ConfigManager.CurrentConfig;
			num2 = ((currentConfig3 != null && currentConfig3.SubWheelHighlightGlowOpacity >= 0.0) ? ConfigManager.CurrentConfig.SubWheelHighlightGlowOpacity : 0.85);
		}
		else
		{
			AppConfig currentConfig4 = ConfigManager.CurrentConfig;
			num2 = ((currentConfig4 != null && currentConfig4.HighlightGlowOpacity >= 0.0) ? ConfigManager.CurrentConfig.HighlightGlowOpacity : 0.85);
		}
		double opacity = num2;
		path.Effect = new DropShadowEffect
		{
			Color = color,
			BlurRadius = blurRadius,
			ShadowDepth = 0.0,
			Opacity = opacity
		};
	}

	private static Stretch ParseStretch(string? str)
	{
		if (string.Equals(str, "Uniform", StringComparison.OrdinalIgnoreCase))
		{
			return Stretch.Uniform;
		}
		if (string.Equals(str, "Fill", StringComparison.OrdinalIgnoreCase))
		{
			return Stretch.Fill;
		}
		if (string.Equals(str, "None", StringComparison.OrdinalIgnoreCase))
		{
			return Stretch.None;
		}
		return Stretch.UniformToFill;
	}

	public const int FanSubmenuSlotCount = 3;

	public static (double du, double dv) GetFanSubOffset(int index)
	{
		double ringR = Math.Sqrt(2.0 - Math.Sqrt(2.0)); // 0.7653668647301795
		return index switch
		{
			0 => (1.0 + ringR * 0.5, ringR * 0.8660254038),   // upper wing
			1 => (1.0 + ringR, 0.0),                           // tip / center
			_ => (1.0 + ringR * 0.5, -ringR * 0.8660254038)   // lower wing
		};
	}

	public static int GetFanSlotIndex(int subIndex, int totalCount)
	{
		if (totalCount <= 1) return 1; // Center tip
		if (totalCount == 2) return (subIndex == 0) ? 0 : 2; // Symmetric upper/lower wings
		return Math.Clamp(subIndex, 0, 2); // 0 = upper, 1 = tip, 2 = lower
	}

	public static double GetFanExtentRadius(double outer, double inner)
	{
		double R = (outer + inner) / 2.0;
		return GetFanSubOffset(1).du * R + (outer - inner) * 0.40;
	}

	public static Geometry CreateSubMenuGeometry(string shape, double cx, double cy, double radius, double parentAngleRad, double wheelCx, double wheelCy, double cornerRadius = -1)
	{
		string s = string.IsNullOrEmpty(shape) ? "Original" : shape;
		double itemAngleRad = Math.Atan2(cy - wheelCy, cx - wheelCx);
		double itemAngleDeg = itemAngleRad * (180.0 / Math.PI);

		// 1. HexagonHive: 6-vertex regular hexagon with smooth rounded corners
		if (s.Equals("HexagonHive", StringComparison.OrdinalIgnoreCase))
		{
			double maxFillet = (Math.Sqrt(3.0) / 2.0) * radius * 0.95;
			double rawCr = (cornerRadius >= 0.0)
				? cornerRadius
				: ((ConfigManager.CurrentConfig?.SubWheelCornerRadius >= 0.0) ? ConfigManager.CurrentConfig.SubWheelCornerRadius : 4.0);
			double effectiveCr = Math.Max(0.0, Math.Min(rawCr, maxFillet));

			Point[] vertices = new Point[6];
			for (int i = 0; i < 6; i++)
			{
				double ang = itemAngleRad + (double)i * (Math.PI / 3.0);
				vertices[i] = new Point(cx + Math.Cos(ang) * radius, cy + Math.Sin(ang) * radius);
			}

			StreamGeometry hex = new StreamGeometry();
			using (StreamGeometryContext ctx = hex.Open())
			{
				if (effectiveCr < 0.5)
				{
					ctx.BeginFigure(vertices[0], isFilled: true, isClosed: true);
					for (int i = 1; i < 6; i++)
					{
						ctx.LineTo(vertices[i], isStroked: true, isSmoothJoin: false);
					}
				}
				else
				{
					double tangentDist = effectiveCr / Math.Sqrt(3.0);
					Point[] pEntry = new Point[6];
					Point[] pExit = new Point[6];

					for (int k = 0; k < 6; k++)
					{
						Point prev = vertices[(k + 5) % 6];
						Point curr = vertices[k];
						Point next = vertices[(k + 1) % 6];

						Vector vIn = curr - prev;
						vIn.Normalize();
						pEntry[k] = curr - vIn * tangentDist;

						Vector vOut = next - curr;
						vOut.Normalize();
						pExit[k] = curr + vOut * tangentDist;
					}

					ctx.BeginFigure(pEntry[0], isFilled: true, isClosed: true);
					Size arcSize = new Size(effectiveCr, effectiveCr);

					for (int i = 0; i < 6; i++)
					{
						ctx.ArcTo(pExit[i], arcSize, 0.0, isLargeArc: false, SweepDirection.Clockwise, isStroked: true, isSmoothJoin: true);
						int nextIdx = (i + 1) % 6;
						ctx.LineTo(pEntry[nextIdx], isStroked: true, isSmoothJoin: false);
					}
				}
			}
			hex.Freeze();
			return hex;
		}

		// 2. Circle: Clean round circular card bubble
		if (s.Equals("Circle", StringComparison.OrdinalIgnoreCase))
		{
			EllipseGeometry ellipse = new EllipseGeometry(new Point(cx, cy), radius, radius);
			ellipse.Freeze();
			return ellipse;
		}

		// 3. RoundedCapsule / FloatingCapsules / Capsule: Stadium / Pill shape
		if (s.Equals("RoundedCapsule", StringComparison.OrdinalIgnoreCase) ||
		    s.Equals("FloatingCapsules", StringComparison.OrdinalIgnoreCase) ||
		    s.Equals("Capsule", StringComparison.OrdinalIgnoreCase))
		{
			double w = 1.9 * radius, h = 1.35 * radius;
			double r = (cornerRadius >= 0.0) ? Math.Min(h / 2.0, cornerRadius) : (h / 2.0);
			RectangleGeometry rect = new RectangleGeometry(new Rect(-w / 2.0, -h / 2.0, w, h), r, r);
			TransformGroup tf = new TransformGroup();
			tf.Children.Add(new RotateTransform(itemAngleDeg));
			tf.Children.Add(new TranslateTransform(cx, cy));
			rect.Transform = tf;
			rect.Freeze();
			return rect;
		}

		// 4. CleanSectors / RoundedRect: Modern smooth rounded rectangle
		if (s.Equals("CleanSectors", StringComparison.OrdinalIgnoreCase) ||
		    s.Equals("RoundedRect", StringComparison.OrdinalIgnoreCase))
		{
			double w = 1.85 * radius, h = 1.4 * radius;
			double r = (cornerRadius >= 0.0) ? cornerRadius : (radius * 0.35);
			RectangleGeometry rect = new RectangleGeometry(new Rect(-w / 2.0, -h / 2.0, w, h), r, r);
			TransformGroup tf = new TransformGroup();
			tf.Children.Add(new RotateTransform(itemAngleDeg));
			tf.Children.Add(new TranslateTransform(cx, cy));
			rect.Transform = tf;
			rect.Freeze();
			return rect;
		}

		// 5. Original / ClassicRing: Smooth radiating curved arc petal
		{
			double halfSpan = 14.0;
			double startDeg = itemAngleDeg - halfSpan;
			double endDeg = itemAngleDeg + halfSpan;
			double distFromCenter = Math.Sqrt((cx - wheelCx) * (cx - wheelCx) + (cy - wheelCy) * (cy - wheelCy));
			double rIn = Math.Max(10.0, distFromCenter - radius * 0.88);
			double rOut = distFromCenter + radius * 0.88;
			double cr = (cornerRadius >= 0.0) ? cornerRadius : 6.0;

			return IconHelper.CreateAdvancedSectorGeometry(wheelCx, wheelCy, startDeg, endDeg, rIn, rOut, "Original", 0.0, cr);
		}
	}

	private void RenderFanSubtier(int parentIndex)
	{
		ClearSubTier();
		if (!ConfigManager.CurrentConfig.EnableMultiTier || parentIndex < 0 || parentIndex >= _profile.Actions.Count)
		{
			return;
		}
		ActionItem actionItem = _profile.Actions[parentIndex];
		if (actionItem == null || actionItem.SubActions == null || actionItem.SubActions.Count == 0)
		{
			return;
		}
		if (_subTierCache.TryGetValue(parentIndex, out SubTierVisuals? cachedVisuals))
		{
			ActivateCachedSubTier(parentIndex, cachedVisuals);
			return;
		}
		int sectorCount = _profile.SectorCount;
		double sectorSize = 360.0 / (double)sectorCount;
		double width = base.Width;
		double cx = width / 2.0;
		double cy = width / 2.0;
		string shape = ConfigManager.CurrentConfig.Shape ?? "Original";
		string layoutMode = ConfigManager.CurrentConfig.IconLayoutMode ?? "IconAndText";
		bool showText = ConfigManager.CurrentConfig.ShowText && layoutMode != "IconOnly";

		double midRad = (double)parentIndex * sectorSize * (Math.PI / 180.0);
		double ux = Math.Cos(midRad), uy = Math.Sin(midRad);
		double vx = -Math.Sin(midRad), vy = Math.Cos(midRad);

		double userSubRadius = ConfigManager.CurrentConfig.SubWheelOuterRadius;
		double userSubGap = ConfigManager.CurrentConfig.SubWheelInnerGap;
		double userCornerRadius = ConfigManager.CurrentConfig.SubWheelCornerRadius;

		double ratio = (userSubRadius > 0.0 && _outerRadius > 0.0) ? (userSubRadius / (_outerRadius * 1.55)) : 1.0;
		double itemR = (_outerRadius - _innerRadius) * 0.40 * Math.Max(0.5, Math.Min(2.5, ratio));
		double gapOffset = (userSubGap >= 0.0) ? userSubGap : 4.0;
		double R = ((_innerRadius + _outerRadius) / 2.0 * ratio) + gapOffset;

		double num7 = ((ConfigManager.CurrentConfig.SubWheelIconSize > 0.0) ? ConfigManager.CurrentConfig.SubWheelIconSize : ((layoutMode == "IconOnly") ? 22.0 : 17.0));
		double fontSize = ((ConfigManager.CurrentConfig.SubWheelFontSize > 0.0) ? ConfigManager.CurrentConfig.SubWheelFontSize : Math.Max(8.5, ConfigManager.CurrentConfig.SectorFontSize - 1.0));

		List<ActionItem> subActions = actionItem.SubActions;
		int subCount = subActions.Count;
		int activeCount = Math.Min(FanSubmenuSlotCount, subCount);
		_activeSubTierParentSector = parentIndex;

		for (int j = 0; j < activeCount; j++)
		{
			int slot = GetFanSlotIndex(j, activeCount);
			var (du, dv) = GetFanSubOffset(slot);

			double px = cx + ux * (du * R) + vx * (dv * R);
			double py = cy + uy * (du * R) + vy * (dv * R);

			Geometry data = CreateSubMenuGeometry(shape, px, py, itemR, midRad, cx, cy, userCornerRadius);

			ScaleTransform scaleTransform = new ScaleTransform(0.75, 0.75, px, py);
			TranslateTransform translateTransform = new TranslateTransform(0.0, 0.0);
			TransformGroup transformGroup = new TransformGroup();
			transformGroup.Children.Add(scaleTransform);
			transformGroup.Children.Add(translateTransform);

			System.Windows.Shapes.Path path = new System.Windows.Shapes.Path
			{
				Data = data,
				Fill = _subDefaultSectorBrush,
				Stroke = _subSectorBorderBrush,
				StrokeThickness = _borderThickness,
				Tag = $"sub_{parentIndex}_{j}",
				Opacity = 0.0,
				RenderTransform = transformGroup
			};
			Panel.SetZIndex(path, 15);
			WheelCanvas.Children.Add(path);
			_subSectorPaths.Add(path);
			_subSectorTransforms.Add(translateTransform);
			_subSectorAngles.Add(midRad);

			double containerW = itemR * 2.2;
			double containerH = itemR * 2.0;
			ScaleTransform scaleTransform2 = new ScaleTransform(0.75, 0.75, containerW / 2.0, containerH / 2.0);
			TranslateTransform translateTransform2 = new TranslateTransform(0.0, 0.0);
			TransformGroup transformGroup2 = new TransformGroup();
			transformGroup2.Children.Add(scaleTransform2);
			transformGroup2.Children.Add(translateTransform2);

			Grid grid = new Grid
			{
				Width = containerW,
				Height = containerH,
				Opacity = 0.0,
				RenderTransform = transformGroup2
			};
			StackPanel stackPanel = new StackPanel
			{
				Orientation = Orientation.Vertical,
				HorizontalAlignment = HorizontalAlignment.Center,
				VerticalAlignment = VerticalAlignment.Center
			};
			grid.Children.Add(stackPanel);

			ActionItem actionItem2 = subActions[j];
			string text2 = actionItem2?.Name ?? "子动作";
			string text3 = actionItem2?.Type ?? "Hotkey";
			string text4 = actionItem2?.Parameter ?? "";
			string iconKey = actionItem2?.IconKey ?? "";
			string text5 = actionItem2?.CustomIconSvg ?? "";
			FrameworkElement frameworkElement = null;

			if (layoutMode != "TextOnly")
			{
				if (!string.IsNullOrEmpty(text5))
				{
					try
					{
						frameworkElement = new System.Windows.Shapes.Path
						{
							Data = Geometry.Parse(text5),
							Fill = _subTextColorBrush,
							Stretch = Stretch.Uniform,
							Width = num7,
							Height = num7,
							Margin = new Thickness(0.0, 0.0, 0.0, showText ? 2 : 0),
							HorizontalAlignment = HorizontalAlignment.Center
						};
					}
					catch { }
				}
				if (frameworkElement == null && !string.IsNullOrEmpty(iconKey))
				{
					if (iconKey.StartsWith("custom:", StringComparison.OrdinalIgnoreCase))
					{
						IconHelper.CustomIconItem customIconItem = IconHelper.GetCustomIcons().FirstOrDefault((IconHelper.CustomIconItem c) => c.Key == iconKey);
						if (customIconItem != null)
						{
							frameworkElement = ((!customIconItem.IsSvg) ? ((FrameworkElement)new Image
							{
								Width = num7,
								Height = num7,
								Stretch = Stretch.Uniform,
								Margin = new Thickness(0.0, 0.0, 0.0, showText ? 2 : 0),
								HorizontalAlignment = HorizontalAlignment.Center,
								Source = IconHelper.GetCustomImageSource(customIconItem.FilePath)
							}) : ((FrameworkElement)new System.Windows.Shapes.Path
							{
								Data = Geometry.Parse(customIconItem.SvgData),
								Fill = _subTextColorBrush,
								Stretch = Stretch.Uniform,
								Width = num7,
								Height = num7,
								Margin = new Thickness(0.0, 0.0, 0.0, showText ? 2 : 0),
								HorizontalAlignment = HorizontalAlignment.Center
							}));
						}
					}
					else
					{
						string svgPathByKey = IconHelper.GetSvgPathByKey(iconKey);
						if (!string.IsNullOrEmpty(svgPathByKey))
						{
							frameworkElement = new System.Windows.Shapes.Path
							{
								Data = Geometry.Parse(svgPathByKey),
								Fill = _subTextColorBrush,
								Stretch = Stretch.Uniform,
								Width = num7,
								Height = num7,
								Margin = new Thickness(0.0, 0.0, 0.0, showText ? 2 : 0),
								HorizontalAlignment = HorizontalAlignment.Center
							};
						}
					}
				}
				if (frameworkElement == null && (text3 == "Launch" || text3 == "App") && !string.IsNullOrEmpty(text4))
				{
					BitmapSource icon = IconHelper.GetIcon(text4);
					if (icon != null)
					{
						frameworkElement = new Image
						{
							Source = icon,
							Width = num7 + 2.0,
							Height = num7 + 2.0,
							Stretch = Stretch.Uniform,
							Margin = new Thickness(0.0, 0.0, 0.0, showText ? 2 : 0),
							HorizontalAlignment = HorizontalAlignment.Center
						};
					}
				}
				if (frameworkElement == null && text3 == "SwitchWindow")
				{
					frameworkElement = BuildSwitchWindowIcon(text4, num7, showText);
				}
				if (frameworkElement == null)
				{
					string vectorIconPath = GetVectorIconPath(text3, text4);
					if (!string.IsNullOrEmpty(vectorIconPath))
					{
						frameworkElement = new System.Windows.Shapes.Path
						{
							Data = Geometry.Parse(vectorIconPath),
							Fill = _subTextColorBrush,
							Stretch = Stretch.Uniform,
							Width = num7,
							Height = num7,
							Margin = new Thickness(0.0, 0.0, 0.0, showText ? 2 : 0),
							HorizontalAlignment = HorizontalAlignment.Center
						};
					}
				}
				if (frameworkElement != null)
				{
					stackPanel.Children.Add(frameworkElement);
				}
			}

			if (showText && !string.IsNullOrEmpty(text2))
			{
				TextBlock textBlock = new TextBlock
				{
					Text = text2,
					Foreground = _subTextColorBrush,
					FontSize = fontSize,
					FontWeight = FontWeights.Medium,
					TextAlignment = TextAlignment.Center,
					TextWrapping = TextWrapping.Wrap,
					TextTrimming = TextTrimming.CharacterEllipsis,
					MaxWidth = containerW - 4.0,
					MaxHeight = 26.0,
					Margin = new Thickness(0.0, 1.0, 0.0, 0.0),
					FontFamily = new FontFamily(ConfigManager.CurrentConfig.WheelFontFamily ?? "Microsoft YaHei UI, Segoe UI")
				};
				if (base.Resources.Contains("TextShadow"))
				{
					textBlock.Effect = (Effect)base.Resources["TextShadow"];
				}
				stackPanel.Children.Add(textBlock);
			}

			Canvas.SetLeft(grid, px - grid.Width / 2.0);
			Canvas.SetTop(grid, py - grid.Height / 2.0);
			Panel.SetZIndex(grid, 35);
			WheelCanvas.Children.Add(grid);
			_subContentContainers.Add(grid);
			_subContainerTransforms.Add(translateTransform2);

			int durationMs = (ConfigManager.CurrentConfig?.AnimationSpeed == "Custom" && ConfigManager.CurrentConfig.CustomAnimationDurationMs > 0) 
				? (int)ConfigManager.CurrentConfig.CustomAnimationDurationMs 
				: (ConfigManager.CurrentConfig?.AnimationSpeed switch
				{
					"Elegant" => 130,
					"Fast" => 35,
					_ => 80
				});
			Duration duration = new Duration(TimeSpan.FromMilliseconds(durationMs * 1.3));
			DoubleAnimation doubleAnimation = new DoubleAnimation(0.0, 1.0, duration)
			{
				EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
			};
			DoubleAnimation doubleAnimation2 = new DoubleAnimation(0.75, 1.0, duration)
			{
				EasingFunction = new BackEase { EasingMode = EasingMode.EaseOut, Amplitude = 0.3 }
			};
			path.BeginAnimation(UIElement.OpacityProperty, doubleAnimation);
			grid.BeginAnimation(UIElement.OpacityProperty, doubleAnimation);
			scaleTransform.BeginAnimation(ScaleTransform.ScaleXProperty, doubleAnimation2);
			scaleTransform.BeginAnimation(ScaleTransform.ScaleYProperty, doubleAnimation2);
			scaleTransform2.BeginAnimation(ScaleTransform.ScaleXProperty, doubleAnimation2);
			scaleTransform2.BeginAnimation(ScaleTransform.ScaleYProperty, doubleAnimation2);
		}
		_subTierCache[parentIndex] = new SubTierVisuals(
			new List<System.Windows.Shapes.Path>(_subSectorPaths),
			new List<Grid>(_subContentContainers),
			new List<TranslateTransform>(_subSectorTransforms),
			new List<TranslateTransform>(_subContainerTransforms),
			new List<double>(_subSectorAngles));
	}

}

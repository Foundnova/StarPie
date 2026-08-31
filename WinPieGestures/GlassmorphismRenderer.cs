using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Effects;
using System.Windows.Shapes;

namespace WinPieGestures;

public class GlassmorphismRenderer : BaseStyleRenderer
{
	protected override void GetDefaultColors(string theme, out string sectorBgHex, out string sectorBorderHex, out string highlightBgHex, out string highlightBorderHex, out string textHex)
	{
		if (theme == "Light")
		{
			sectorBgHex = "#45FFFFFF";
			sectorBorderHex = "#85FFFFFF";
			highlightBgHex = "#D86366F1";
			highlightBorderHex = "#FFFFFFFF";
			textHex = "#FF0F172A";
		}
		else
		{
			sectorBgHex = "#40181E32";
			sectorBorderHex = "#50E2E8F0";
			highlightBgHex = "#D07C3AED";
			highlightBorderHex = "#FFF5F3FF";
			textHex = "#FFF8FAFC";
		}
	}

	protected override void PostInitialize()
	{
		base.BorderThickness = 0.9;
		base.HighlightBorderThickness = 1.8;
		base.CoreBgBrush = new SolidColorBrush(Color.FromArgb(60, 20, 24, 40));
		base.CoreBorderBrush = new SolidColorBrush(Color.FromArgb(70, byte.MaxValue, byte.MaxValue, byte.MaxValue));
	}

	public override void RenderDecorations(Canvas canvas, Grid coreGrid, double cx, double cy, double wheelRadius, double coreRadius, int insertIndex)
	{
		Color color = (base.IsLightTheme ? Color.FromArgb(40, 100, 116, 139) : Color.FromArgb(35, byte.MaxValue, byte.MaxValue, byte.MaxValue));
		Ellipse element = new Ellipse
		{
			Width = coreRadius * 2.0 + 4.0,
			Height = coreRadius * 2.0 + 4.0,
			Stroke = new SolidColorBrush(color),
			StrokeThickness = 0.8,
			Tag = "Deco_InnerGlassRing",
			IsHitTestVisible = false
		};
		Canvas.SetLeft(element, cx - (coreRadius + 2.0));
		Canvas.SetTop(element, cy - (coreRadius + 2.0));
		Panel.SetZIndex(element, 0);
		canvas.Children.Add(element);
	}

	public override void ApplySectorHighlight(Path path, bool isHighlighted)
	{
		if (isHighlighted)
		{
			Color effectiveGlowColor = GetEffectiveGlowColor();
			double effectiveGlowRadius = GetEffectiveGlowRadius(26.0);
			double effectiveGlowOpacity = GetEffectiveGlowOpacity(0.95);
			path.Effect = new DropShadowEffect
			{
				Color = effectiveGlowColor,
				BlurRadius = effectiveGlowRadius,
				ShadowDepth = 0.0,
				Opacity = effectiveGlowOpacity
			};
		}
		else
		{
			path.Effect = new DropShadowEffect
			{
				Color = Color.FromRgb(0, 0, 0),
				BlurRadius = 14.0,
				ShadowDepth = 2.0,
				Opacity = 0.4,
				Direction = 270.0
			};
		}
	}

	public override void ApplyExitHighlight(Path exitIcon, bool isHighlighted)
	{
		if (isHighlighted)
		{
			exitIcon.Effect = new DropShadowEffect
			{
				Color = Color.FromRgb(244, 63, 94),
				BlurRadius = 16.0,
				ShadowDepth = 0.0,
				Opacity = 0.9
			};
		}
		else
		{
			exitIcon.Effect = null;
		}
	}
}

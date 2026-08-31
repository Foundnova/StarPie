using System;
using System.Runtime.CompilerServices;
using System.Windows;

namespace WinPieGestures;

public class MouseEventArgs : EventArgs
{
	[CompilerGenerated]
	private readonly Point _003CPosition_003Ek__BackingField;

	public Point Position { get; set; }

	public bool Handled { get; set; }

	public MouseEventArgs(double x, double y)
	{
		//IL_0009: Unknown result type (might be due to invalid IL or missing references)
		//IL_000e: Unknown result type (might be due to invalid IL or missing references)
		Position = new Point(x, y);
		Handled = false;
	}
}

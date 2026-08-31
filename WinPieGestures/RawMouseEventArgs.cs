using System;
using System.Runtime.CompilerServices;
using System.Windows;

namespace WinPieGestures;

public class RawMouseEventArgs : EventArgs
{
	[CompilerGenerated]
	private readonly Point _003CPosition_003Ek__BackingField;

	public int Message { get; }

	public string MouseButton { get; }

	public uint MouseData { get; }

	public bool IsButtonDown { get; }

	public Point Position { get; set; }

	public bool Handled { get; set; }

	public RawMouseEventArgs(int message, string mouseButton, uint mouseData, bool isButtonDown, double x, double y)
	{
		//IL_0028: Unknown result type (might be due to invalid IL or missing references)
		//IL_002d: Unknown result type (might be due to invalid IL or missing references)
		Message = message;
		MouseButton = mouseButton;
		MouseData = mouseData;
		IsButtonDown = isButtonDown;
		Position = new Point(x, y);
		Handled = false;
	}
}

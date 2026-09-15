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

	/// <summary>复用实例：重置坐标与 Handled 标记。仅限钩子回调线程在派发前调用，订阅方不得跨线程或异步持有该实例。</summary>
	internal void Update(double x, double y)
	{
		Position = new Point(x, y);
		Handled = false;
	}
}

public class MouseWheelHookEventArgs : EventArgs
{
	public short Delta { get; private set; }

	public Point Position { get; private set; }

	public bool Handled { get; set; }

	public MouseWheelHookEventArgs(short delta, double x, double y)
	{
		Delta = delta;
		Position = new Point(x, y);
		Handled = false;
	}

	/// <summary>复用实例：重置增量、坐标与 Handled 标记。仅限钩子回调线程在派发前调用，订阅方不得跨线程或异步持有该实例。</summary>
	internal void Update(short delta, double x, double y)
	{
		Delta = delta;
		Position = new Point(x, y);
		Handled = false;
	}
}

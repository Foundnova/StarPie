using System;
using System.Runtime.CompilerServices;
using System.Windows.Input;

namespace WinPieGestures;

public class GlobalKeyEventArgs : EventArgs
{
	[CompilerGenerated]
	private readonly Key _003CKey_003Ek__BackingField;

	[CompilerGenerated]
	private readonly ModifierKeys _003CModifiers_003Ek__BackingField;

	public uint VkCode { get; }

	public Key Key { get; set; }

	public ModifierKeys Modifiers { get; set; }

	public bool Handled { get; set; }

	public GlobalKeyEventArgs(uint vkCode, ModifierKeys modifiers)
	{
		//IL_000f: Unknown result type (might be due to invalid IL or missing references)
		//IL_0014: Unknown result type (might be due to invalid IL or missing references)
		//IL_001a: Unknown result type (might be due to invalid IL or missing references)
		//IL_001b: Unknown result type (might be due to invalid IL or missing references)
		VkCode = vkCode;
		Key = KeyInterop.KeyFromVirtualKey((int)vkCode);
		Modifiers = modifiers;
		Handled = false;
	}
}

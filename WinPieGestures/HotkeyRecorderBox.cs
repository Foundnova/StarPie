using System.Collections.Generic;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace WinPieGestures;

public class HotkeyRecorderBox : Control
{
	public static readonly DependencyProperty HotkeyTextProperty;

	public static readonly DependencyProperty IsRecordingProperty;

	public static readonly DependencyProperty PlaceholderProperty;

	private TextBlock? _displayTextBlock;

	private Button? _clearButton;

	private Border? _mainBorder;

	public string HotkeyText
	{
		get
		{
			return (string)((DependencyObject)this).GetValue(HotkeyTextProperty);
		}
		set
		{
			((DependencyObject)this).SetValue(HotkeyTextProperty, (object)value);
		}
	}

	public bool IsRecording
	{
		get
		{
			return (bool)((DependencyObject)this).GetValue(IsRecordingProperty);
		}
		set
		{
			((DependencyObject)this).SetValue(IsRecordingProperty, (object)value);
		}
	}

	public string Placeholder
	{
		get
		{
			return (string)((DependencyObject)this).GetValue(PlaceholderProperty);
		}
		set
		{
			((DependencyObject)this).SetValue(PlaceholderProperty, (object)value);
		}
	}

	static HotkeyRecorderBox()
	{
		//IL_002a: Unknown result type (might be due to invalid IL or missing references)
		//IL_0034: Expected O, but got Unknown
		//IL_0064: Unknown result type (might be due to invalid IL or missing references)
		//IL_006e: Expected O, but got Unknown
		//IL_0069: Unknown result type (might be due to invalid IL or missing references)
		//IL_0073: Expected O, but got Unknown
		//IL_0096: Unknown result type (might be due to invalid IL or missing references)
		//IL_00a0: Expected O, but got Unknown
		HotkeyTextProperty = DependencyProperty.Register("HotkeyText", typeof(string), typeof(HotkeyRecorderBox), (PropertyMetadata)(object)new FrameworkPropertyMetadata(string.Empty, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault, new PropertyChangedCallback(OnHotkeyTextChanged)));
		IsRecordingProperty = DependencyProperty.Register("IsRecording", typeof(bool), typeof(HotkeyRecorderBox), new PropertyMetadata((object)false, new PropertyChangedCallback(OnIsRecordingChanged)));
		PlaceholderProperty = DependencyProperty.Register("Placeholder", typeof(string), typeof(HotkeyRecorderBox), new PropertyMetadata((object)"点击录制快捷键..."));
		FrameworkElement.DefaultStyleKeyProperty.OverrideMetadata(typeof(HotkeyRecorderBox), (PropertyMetadata)(object)new FrameworkPropertyMetadata((object)typeof(HotkeyRecorderBox)));
		UIElement.FocusableProperty.OverrideMetadata(typeof(HotkeyRecorderBox), (PropertyMetadata)(object)new FrameworkPropertyMetadata((object)true));
	}

	public HotkeyRecorderBox()
	{
		base.FocusVisualStyle = null;
		base.Cursor = Cursors.Hand;
	}

	public override void OnApplyTemplate()
	{
		base.OnApplyTemplate();
		_displayTextBlock = GetTemplateChild("PART_DisplayText") as TextBlock;
		_clearButton = GetTemplateChild("PART_ClearButton") as Button;
		_mainBorder = GetTemplateChild("PART_Border") as Border;
		if (_clearButton != null)
		{
			_clearButton.Click += delegate(object s, RoutedEventArgs e)
			{
				HotkeyText = string.Empty;
				IsRecording = false;
				e.Handled = true;
			};
		}
		UpdateVisualDisplay();
	}

	protected override void OnMouseDown(MouseButtonEventArgs e)
	{
		base.OnMouseDown(e);
		if (e.ChangedButton == MouseButton.Left)
		{
			Focus();
			IsRecording = true;
			e.Handled = true;
		}
	}

	protected override void OnGotKeyboardFocus(KeyboardFocusChangedEventArgs e)
	{
		base.OnGotKeyboardFocus(e);
		IsRecording = true;
		UpdateVisualDisplay();
	}

	protected override void OnLostKeyboardFocus(KeyboardFocusChangedEventArgs e)
	{
		base.OnLostKeyboardFocus(e);
		IsRecording = false;
		UpdateVisualDisplay();
	}

	protected override void OnPreviewKeyDown(KeyEventArgs e)
	{
		//IL_0018: Unknown result type (might be due to invalid IL or missing references)
		//IL_0022: Invalid comparison between Unknown and I4
		//IL_002d: Unknown result type (might be due to invalid IL or missing references)
		//IL_0025: Unknown result type (might be due to invalid IL or missing references)
		//IL_0032: Unknown result type (might be due to invalid IL or missing references)
		//IL_0033: Unknown result type (might be due to invalid IL or missing references)
		//IL_0036: Invalid comparison between Unknown and I4
		//IL_004b: Unknown result type (might be due to invalid IL or missing references)
		//IL_004d: Invalid comparison between Unknown and I4
		//IL_004f: Unknown result type (might be due to invalid IL or missing references)
		//IL_0052: Invalid comparison between Unknown and I4
		//IL_0072: Unknown result type (might be due to invalid IL or missing references)
		//IL_0081: Unknown result type (might be due to invalid IL or missing references)
		if (!IsRecording)
		{
			base.OnPreviewKeyDown(e);
			return;
		}
		e.Handled = true;
		Key val = (((int)e.Key == 156) ? e.SystemKey : e.Key);
		if ((int)val == 13)
		{
			IsRecording = false;
			Keyboard.ClearFocus();
			UpdateVisualDisplay();
			return;
		}
		if ((int)val == 2 || (int)val == 32)
		{
			HotkeyText = string.Empty;
			IsRecording = false;
			Keyboard.ClearFocus();
			UpdateVisualDisplay();
			return;
		}
		if (IsModifierKey(val))
		{
			UpdateModifierOnlyDisplay();
			return;
		}
		string text = BuildHotkeyString(val);
		if (!string.IsNullOrEmpty(text))
		{
			HotkeyText = text;
			IsRecording = false;
			Keyboard.ClearFocus();
			UpdateVisualDisplay();
		}
	}

	protected override void OnPreviewKeyUp(KeyEventArgs e)
	{
		if (IsRecording)
		{
			e.Handled = true;
			UpdateModifierOnlyDisplay();
		}
		base.OnPreviewKeyUp(e);
	}

	private static bool IsModifierKey(Key key)
	{
		//IL_0000: Unknown result type (might be due to invalid IL or missing references)
		//IL_0003: Invalid comparison between Unknown and I4
		//IL_0005: Unknown result type (might be due to invalid IL or missing references)
		//IL_0008: Invalid comparison between Unknown and I4
		//IL_000a: Unknown result type (might be due to invalid IL or missing references)
		//IL_000d: Invalid comparison between Unknown and I4
		//IL_000f: Unknown result type (might be due to invalid IL or missing references)
		//IL_0012: Invalid comparison between Unknown and I4
		//IL_0014: Unknown result type (might be due to invalid IL or missing references)
		//IL_0017: Invalid comparison between Unknown and I4
		//IL_0019: Unknown result type (might be due to invalid IL or missing references)
		//IL_001c: Invalid comparison between Unknown and I4
		//IL_001e: Unknown result type (might be due to invalid IL or missing references)
		//IL_0021: Invalid comparison between Unknown and I4
		//IL_0023: Unknown result type (might be due to invalid IL or missing references)
		//IL_0026: Invalid comparison between Unknown and I4
		if ((int)key != 118 && (int)key != 119 && (int)key != 120 && (int)key != 121 && (int)key != 116 && (int)key != 117 && (int)key != 70)
		{
			return (int)key == 71;
		}
		return true;
	}

	private void UpdateModifierOnlyDisplay()
	{
		//IL_000f: Unknown result type (might be due to invalid IL or missing references)
		//IL_0015: Unknown result type (might be due to invalid IL or missing references)
		//IL_0024: Unknown result type (might be due to invalid IL or missing references)
		//IL_002a: Unknown result type (might be due to invalid IL or missing references)
		//IL_0039: Unknown result type (might be due to invalid IL or missing references)
		//IL_003f: Unknown result type (might be due to invalid IL or missing references)
		//IL_004e: Unknown result type (might be due to invalid IL or missing references)
		//IL_0054: Unknown result type (might be due to invalid IL or missing references)
		if (!IsRecording)
		{
			return;
		}
		StringBuilder stringBuilder = new StringBuilder();
		if (((((int)Keyboard.Modifiers & 2))) != 0)
		{
			stringBuilder.Append("Ctrl + ");
		}
		if (((((int)Keyboard.Modifiers & 4))) != 0)
		{
			stringBuilder.Append("Shift + ");
		}
		if (((((int)Keyboard.Modifiers & 1))) != 0)
		{
			stringBuilder.Append("Alt + ");
		}
		if (((((int)Keyboard.Modifiers & 8))) != 0 || Keyboard.IsKeyDown((Key)70) || Keyboard.IsKeyDown((Key)71))
		{
			stringBuilder.Append("Win + ");
		}
		if (stringBuilder.Length > 0)
		{
			if (_displayTextBlock != null)
			{
				_displayTextBlock.Text = stringBuilder.ToString() + "...";
				_displayTextBlock.Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#2563EB"));
			}
		}
		else if (_displayTextBlock != null)
		{
			_displayTextBlock.Text = "\ud83d\udd34 请按下快捷键组合...";
			_displayTextBlock.Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#E11D48"));
		}
	}

	private static string BuildHotkeyString(Key mainKey)
	{
		//IL_0006: Unknown result type (might be due to invalid IL or missing references)
		//IL_000c: Unknown result type (might be due to invalid IL or missing references)
		//IL_001a: Unknown result type (might be due to invalid IL or missing references)
		//IL_0020: Unknown result type (might be due to invalid IL or missing references)
		//IL_002e: Unknown result type (might be due to invalid IL or missing references)
		//IL_0034: Unknown result type (might be due to invalid IL or missing references)
		//IL_0042: Unknown result type (might be due to invalid IL or missing references)
		//IL_0048: Unknown result type (might be due to invalid IL or missing references)
		//IL_0068: Unknown result type (might be due to invalid IL or missing references)
		List<string> list = new List<string>();
		if (((((int)Keyboard.Modifiers & 2))) != 0)
		{
			list.Add("Ctrl");
		}
		if (((((int)Keyboard.Modifiers & 4))) != 0)
		{
			list.Add("Shift");
		}
		if (((((int)Keyboard.Modifiers & 1))) != 0)
		{
			list.Add("Alt");
		}
		if (((((int)Keyboard.Modifiers & 8))) != 0 || Keyboard.IsKeyDown((Key)70) || Keyboard.IsKeyDown((Key)71))
		{
			list.Add("Win");
		}
		string text = FormatKeyName(mainKey);
		if (!string.IsNullOrEmpty(text))
		{
			list.Add(text);
		}
		return string.Join(" + ", list);
	}

	
	private static string FormatKeyName(System.Windows.Input.Key key)
	{
		return key switch
		{
			System.Windows.Input.Key.Return => "Enter",
			System.Windows.Input.Key.Space => "Space",
			System.Windows.Input.Key.Tab => "Tab",
			System.Windows.Input.Key.Back => "Backspace",
			System.Windows.Input.Key.Delete => "Delete",
			System.Windows.Input.Key.Insert => "Insert",
			System.Windows.Input.Key.Home => "Home",
			System.Windows.Input.Key.End => "End",
			System.Windows.Input.Key.PageUp => "PageUp",
			System.Windows.Input.Key.PageDown => "PageDown",
			System.Windows.Input.Key.Left => "Left",
			System.Windows.Input.Key.Up => "Up",
			System.Windows.Input.Key.Right => "Right",
			System.Windows.Input.Key.Down => "Down",
			System.Windows.Input.Key.PrintScreen => "PrintScreen",
			System.Windows.Input.Key.Pause => "Pause",
			System.Windows.Input.Key.Capital => "CapsLock",
			System.Windows.Input.Key.Multiply => "NumMultiply",
			System.Windows.Input.Key.Divide => "NumDivide",
			System.Windows.Input.Key.Decimal => "NumDecimal",
			_ => key.ToString()
		};
	}

	private void UpdateVisualDisplay()
	{
		if (_displayTextBlock == null)
		{
			return;
		}
		if (IsRecording)
		{
			_displayTextBlock.Text = "\ud83d\udd34 请按下快捷键组合...";
			_displayTextBlock.Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#E11D48"));
			if (_mainBorder != null)
			{
				_mainBorder.BorderBrush = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#2563EB"));
				_mainBorder.Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#EFF6FF"));
			}
		}
		else
		{
			if (string.IsNullOrEmpty(HotkeyText))
			{
				_displayTextBlock.Text = Placeholder;
				_displayTextBlock.Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#94A3B8"));
			}
			else
			{
				_displayTextBlock.Text = HotkeyText;
				_displayTextBlock.Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#0F172A"));
			}
			if (_mainBorder != null)
			{
				_mainBorder.BorderBrush = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#CBD5E1"));
				_mainBorder.Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#FFFFFF"));
			}
		}
		if (_clearButton != null)
		{
			_clearButton.Visibility = ((string.IsNullOrEmpty(HotkeyText) || IsRecording) ? Visibility.Collapsed : Visibility.Visible);
		}
	}

	private static void OnHotkeyTextChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
	{
		if (d is HotkeyRecorderBox hotkeyRecorderBox)
		{
			hotkeyRecorderBox.UpdateVisualDisplay();
		}
	}

	private static void OnIsRecordingChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
	{
		if (d is HotkeyRecorderBox hotkeyRecorderBox)
		{
			hotkeyRecorderBox.UpdateVisualDisplay();
		}
	}
}

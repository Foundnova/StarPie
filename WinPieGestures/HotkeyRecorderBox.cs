using System;
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

	public event EventHandler<string>? HotkeyChanged;
	private TextBlock? _displayTextBlock;
	private Button? _clearButton;
	private Border? _mainBorder;
	private Key _currentPressedKey = Key.None;
	private readonly HashSet<string> _sessionModifiers = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

	public string HotkeyText
	{
		get => (string)GetValue(HotkeyTextProperty);
		set => SetValue(HotkeyTextProperty, value);
	}

	public bool IsRecording
	{
		get => (bool)GetValue(IsRecordingProperty);
		set => SetValue(IsRecordingProperty, value);
	}

	public string Placeholder
	{
		get => (string)GetValue(PlaceholderProperty);
		set => SetValue(PlaceholderProperty, value);
	}

	static HotkeyRecorderBox()
	{
		HotkeyTextProperty = DependencyProperty.Register("HotkeyText", typeof(string), typeof(HotkeyRecorderBox), new FrameworkPropertyMetadata(string.Empty, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault, OnHotkeyTextChanged));
		IsRecordingProperty = DependencyProperty.Register("IsRecording", typeof(bool), typeof(HotkeyRecorderBox), new PropertyMetadata(false, OnIsRecordingChanged));
		PlaceholderProperty = DependencyProperty.Register("Placeholder", typeof(string), typeof(HotkeyRecorderBox), new PropertyMetadata("点击录制/按Esc取消..."));
		DefaultStyleKeyProperty.OverrideMetadata(typeof(HotkeyRecorderBox), new FrameworkPropertyMetadata(typeof(HotkeyRecorderBox)));
		FocusableProperty.OverrideMetadata(typeof(HotkeyRecorderBox), new FrameworkPropertyMetadata(true));
	}

	public HotkeyRecorderBox()
	{
		FocusVisualStyle = null;
		Cursor = Cursors.Hand;
		KeyboardNavigation.SetTabNavigation(this, KeyboardNavigationMode.None);
		KeyboardNavigation.SetDirectionalNavigation(this, KeyboardNavigationMode.None);
		KeyboardNavigation.SetControlTabNavigation(this, KeyboardNavigationMode.None);
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
				_sessionModifiers.Clear();
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
			if (!IsRecording)
			{
				Focus();
				_sessionModifiers.Clear();
				IsRecording = true;
				_currentPressedKey = Key.None;
			}
			else
			{
				CommitModifierIfAny();
				IsRecording = false;
				Keyboard.ClearFocus();
			}
			e.Handled = true;
		}
	}

	protected override void OnGotKeyboardFocus(KeyboardFocusChangedEventArgs e)
	{
		base.OnGotKeyboardFocus(e);
		_sessionModifiers.Clear();
		IsRecording = true;
		_currentPressedKey = Key.None;
		UpdateVisualDisplay();
	}

	protected override void OnLostKeyboardFocus(KeyboardFocusChangedEventArgs e)
	{
		base.OnLostKeyboardFocus(e);
		if (IsRecording)
		{
			CommitModifierIfAny();
			IsRecording = false;
			UpdateVisualDisplay();
		}
	}

	protected override void OnPreviewKeyDown(KeyEventArgs e)
	{
		if (!IsRecording)
		{
			base.OnPreviewKeyDown(e);
			return;
		}

		e.Handled = true;
		Key val = (e.Key == Key.System) ? e.SystemKey : e.Key;
		if (val == Key.ImeProcessed) val = e.ImeProcessedKey;

		if (val == Key.Escape)
		{
			_sessionModifiers.Clear();
			IsRecording = false;
			Keyboard.ClearFocus();
			UpdateVisualDisplay();
			return;
		}

		if (val == Key.Return)
		{
			CommitModifierIfAny();
			IsRecording = false;
			Keyboard.ClearFocus();
			UpdateVisualDisplay();
			return;
		}

		if (val == Key.LeftCtrl || val == Key.RightCtrl) _sessionModifiers.Add("Ctrl");
		else if (val == Key.LeftShift || val == Key.RightShift) _sessionModifiers.Add("Shift");
		else if (val == Key.LeftAlt || val == Key.RightAlt) _sessionModifiers.Add("Alt");
		else if (val == Key.LWin || val == Key.RWin) _sessionModifiers.Add("Win");

		if (IsModifierKey(val))
		{
			_currentPressedKey = Key.None;
			UpdateModifierOnlyDisplay();
			return;
		}

		_currentPressedKey = val;
		string text = BuildHotkeyString(val);
		if (!string.IsNullOrEmpty(text))
		{
			HotkeyText = text;
			_sessionModifiers.Clear();
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
			Key val = (e.Key == Key.System) ? e.SystemKey : e.Key;

			if (IsModifierKey(val) && _currentPressedKey == Key.None)
			{
				bool anyModDown = Keyboard.IsKeyDown(Key.LeftCtrl) || Keyboard.IsKeyDown(Key.RightCtrl) ||
				                  Keyboard.IsKeyDown(Key.LeftShift) || Keyboard.IsKeyDown(Key.RightShift) ||
				                  Keyboard.IsKeyDown(Key.LeftAlt) || Keyboard.IsKeyDown(Key.RightAlt) ||
				                  Keyboard.IsKeyDown(Key.LWin) || Keyboard.IsKeyDown(Key.RWin);
				if (!anyModDown && _sessionModifiers.Count > 0)
				{
					CommitModifierIfAny();
					IsRecording = false;
					Keyboard.ClearFocus();
					UpdateVisualDisplay();
					return;
				}
			}
			UpdateModifierOnlyDisplay();
		}
		base.OnPreviewKeyUp(e);
	}

	private void CommitModifierIfAny()
	{
		List<string> list = new List<string>();
		if (_sessionModifiers.Contains("Ctrl") || Keyboard.IsKeyDown(Key.LeftCtrl) || Keyboard.IsKeyDown(Key.RightCtrl)) list.Add("Ctrl");
		if (_sessionModifiers.Contains("Shift") || Keyboard.IsKeyDown(Key.LeftShift) || Keyboard.IsKeyDown(Key.RightShift)) list.Add("Shift");
		if (_sessionModifiers.Contains("Alt") || Keyboard.IsKeyDown(Key.LeftAlt) || Keyboard.IsKeyDown(Key.RightAlt)) list.Add("Alt");
		if (_sessionModifiers.Contains("Win") || Keyboard.IsKeyDown(Key.LWin) || Keyboard.IsKeyDown(Key.RWin)) list.Add("Win");

		if (list.Count > 0)
		{
			HotkeyText = string.Join(" + ", list);
		}
		_sessionModifiers.Clear();
	}

	private static bool IsModifierKey(Key key)
	{
		return key == Key.LeftCtrl || key == Key.RightCtrl ||
		       key == Key.LeftAlt || key == Key.RightAlt ||
		       key == Key.LeftShift || key == Key.RightShift ||
		       key == Key.LWin || key == Key.RWin;
	}

	private void UpdateModifierOnlyDisplay()
	{
		if (!IsRecording) return;
		List<string> list = new List<string>();
		if (_sessionModifiers.Contains("Ctrl") || Keyboard.IsKeyDown(Key.LeftCtrl) || Keyboard.IsKeyDown(Key.RightCtrl)) list.Add("Ctrl");
		if (_sessionModifiers.Contains("Shift") || Keyboard.IsKeyDown(Key.LeftShift) || Keyboard.IsKeyDown(Key.RightShift)) list.Add("Shift");
		if (_sessionModifiers.Contains("Alt") || Keyboard.IsKeyDown(Key.LeftAlt) || Keyboard.IsKeyDown(Key.RightAlt)) list.Add("Alt");
		if (_sessionModifiers.Contains("Win") || Keyboard.IsKeyDown(Key.LWin) || Keyboard.IsKeyDown(Key.RWin)) list.Add("Win");
		string modStr = string.Join(" + ", list);
		if (_displayTextBlock != null)
		{
			if (!string.IsNullOrEmpty(modStr))
			{
				_displayTextBlock.Text = modStr + " + ...";
				_displayTextBlock.Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#2563EB"));
			}
			else
			{
				_displayTextBlock.Text = "🔴 请按下快捷键组合...";
				_displayTextBlock.Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#E11D48"));
			}
		}
	}

	private string BuildHotkeyString(Key mainKey)
	{
		List<string> list = new List<string>();
		if (_sessionModifiers.Contains("Ctrl") || ((int)Keyboard.Modifiers & 2) != 0 || Keyboard.IsKeyDown(Key.LeftCtrl) || Keyboard.IsKeyDown(Key.RightCtrl))
		{
			list.Add("Ctrl");
		}
		if (_sessionModifiers.Contains("Shift") || ((int)Keyboard.Modifiers & 4) != 0 || Keyboard.IsKeyDown(Key.LeftShift) || Keyboard.IsKeyDown(Key.RightShift))
		{
			list.Add("Shift");
		}
		if (_sessionModifiers.Contains("Alt") || ((int)Keyboard.Modifiers & 1) != 0 || Keyboard.IsKeyDown(Key.LeftAlt) || Keyboard.IsKeyDown(Key.RightAlt))
		{
			list.Add("Alt");
		}
		if (_sessionModifiers.Contains("Win") || ((int)Keyboard.Modifiers & 8) != 0 || Keyboard.IsKeyDown(Key.LWin) || Keyboard.IsKeyDown(Key.RWin))
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

	public static string BuildHotkeyString(Key mainKey, ModifierKeys modifiers)
	{
		List<string> list = new List<string>();
		if (modifiers.HasFlag(ModifierKeys.Control)) list.Add("Ctrl");
		if (modifiers.HasFlag(ModifierKeys.Shift)) list.Add("Shift");
		if (modifiers.HasFlag(ModifierKeys.Alt)) list.Add("Alt");
		if (modifiers.HasFlag(ModifierKeys.Windows)) list.Add("Win");
		string text = FormatKeyName(mainKey);
		if (!string.IsNullOrEmpty(text))
		{
			list.Add(text);
		}
		return string.Join(" + ", list);
	}

	public static string FormatKeyName(Key key)
	{
		return key switch
		{
			Key.Return => "Enter",
			Key.Space => "Space",
			Key.Tab => "Tab",
			Key.Back => "Backspace",
			Key.Delete => "Delete",
			Key.Insert => "Insert",
			Key.Home => "Home",
			Key.End => "End",
			Key.PageUp => "PageUp",
			Key.PageDown => "PageDown",
			Key.Left => "Left",
			Key.Up => "Up",
			Key.Right => "Right",
			Key.Down => "Down",
			Key.PrintScreen => "PrintScreen",
			Key.Pause => "Pause",
			Key.Capital => "CapsLock",
			Key.Escape => "Esc",
			Key.Multiply => "NumMultiply",
			Key.Divide => "NumDivide",
			Key.Add => "NumAdd",
			Key.Subtract => "NumSubtract",
			Key.Decimal => "NumDecimal",
			Key.NumPad0 => "Num0",
			Key.NumPad1 => "Num1",
			Key.NumPad2 => "Num2",
			Key.NumPad3 => "Num3",
			Key.NumPad4 => "Num4",
			Key.NumPad5 => "Num5",
			Key.NumPad6 => "Num6",
			Key.NumPad7 => "Num7",
			Key.NumPad8 => "Num8",
			Key.NumPad9 => "Num9",
			Key.D0 => "0",
			Key.D1 => "1",
			Key.D2 => "2",
			Key.D3 => "3",
			Key.D4 => "4",
			Key.D5 => "5",
			Key.D6 => "6",
			Key.D7 => "7",
			Key.D8 => "8",
			Key.D9 => "9",
			_ => key.ToString()
		};
	}

	public void SetRecordedHotkey(string hotkeyText)
	{
		_sessionModifiers.Clear();
		_currentPressedKey = Key.None;
		HotkeyText = hotkeyText;
		IsRecording = false;
		Keyboard.ClearFocus();
		UpdateVisualDisplay();
	}

	public void ShowExclusiveRecordingState(string text)
	{
		IsRecording = true;
		if (_displayTextBlock != null)
		{
			_displayTextBlock.Text = text;
			_displayTextBlock.Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#E11D48"));
		}
	}

	private void UpdateVisualDisplay()
	{
		if (_displayTextBlock == null) return;

		if (IsRecording)
		{
			_displayTextBlock.Text = "🔴 录制中... 点击或按Esc完成";
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
				_displayTextBlock.Foreground = (Brush)TryFindResource("TextMutedBrush") ?? new SolidColorBrush((Color)ColorConverter.ConvertFromString("#94A3B8"));
			}
			else
			{
				_displayTextBlock.Text = HotkeyText;
				_displayTextBlock.Foreground = (Brush)TryFindResource("TextPrimaryBrush") ?? new SolidColorBrush((Color)ColorConverter.ConvertFromString("#0F172A"));
			}
			if (_mainBorder != null)
			{
				_mainBorder.BorderBrush = (Brush)TryFindResource("InputBorderBrush") ?? new SolidColorBrush((Color)ColorConverter.ConvertFromString("#CBD5E1"));
				_mainBorder.Background = (Brush)TryFindResource("InputBackgroundBrush") ?? new SolidColorBrush((Color)ColorConverter.ConvertFromString("#FFFFFF"));
			}
		}
		if (_clearButton != null)
		{
			_clearButton.Visibility = (string.IsNullOrEmpty(HotkeyText) || IsRecording) ? Visibility.Collapsed : Visibility.Visible;
		}
	}

	private static void OnHotkeyTextChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
	{
		if (d is HotkeyRecorderBox hotkeyRecorderBox)
		{
			hotkeyRecorderBox.UpdateVisualDisplay();
			hotkeyRecorderBox.HotkeyChanged?.Invoke(hotkeyRecorderBox, (string)e.NewValue);
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

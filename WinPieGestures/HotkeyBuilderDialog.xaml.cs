using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;

namespace WinPieGestures;

public partial class HotkeyBuilderDialog : Window
{
	public string ResultHotkey { get; private set; } = string.Empty;
	private bool _isInternalUpdating = false;

	public HotkeyBuilderDialog(string initialHotkey)
	{
		InitializeComponent();
		AppThemeManager.ApplyTheme(this, ConfigManager.CurrentConfig?.Theme ?? "System");
		InitializeFromHotkey(initialHotkey);
	}

	private void InitializeFromHotkey(string hotkey)
	{
		if (string.IsNullOrWhiteSpace(hotkey)) return;

		_isInternalUpdating = true;
		ResultHotkey = hotkey.Trim();
		PreviewResultText.Text = ResultHotkey;
		CustomInputTextBox.Text = ResultHotkey;

		string[] parts = hotkey.Split(new char[] { '+', ' ' }, StringSplitOptions.RemoveEmptyEntries);
		string mainKey = "";
		foreach (string p in parts)
		{
			string t = p.Trim();
			if (t.Equals("Ctrl", StringComparison.OrdinalIgnoreCase) || t.Equals("Control", StringComparison.OrdinalIgnoreCase)) CtrlCheckBox.IsChecked = true;
			else if (t.Equals("Shift", StringComparison.OrdinalIgnoreCase)) ShiftCheckBox.IsChecked = true;
			else if (t.Equals("Alt", StringComparison.OrdinalIgnoreCase) || t.Equals("Menu", StringComparison.OrdinalIgnoreCase)) AltCheckBox.IsChecked = true;
			else if (t.Equals("Win", StringComparison.OrdinalIgnoreCase) || t.Equals("Windows", StringComparison.OrdinalIgnoreCase)) WinCheckBox.IsChecked = true;
			else mainKey = t;
		}

		if (!string.IsNullOrEmpty(mainKey))
		{
			foreach (ComboBoxItem item in MainKeyComboBox.Items)
			{
				if (string.Equals(item.Tag?.ToString(), mainKey, StringComparison.OrdinalIgnoreCase))
				{
					MainKeyComboBox.SelectedItem = item;
					break;
				}
			}
		}
		_isInternalUpdating = false;
	}

	private void OnComboChanged(object sender, RoutedEventArgs e)
	{
		if (_isInternalUpdating) return;
		BuildFromControls();
	}

	private void BuildFromControls()
	{
		List<string> list = new List<string>();
		if (CtrlCheckBox.IsChecked == true) list.Add("Ctrl");
		if (ShiftCheckBox.IsChecked == true) list.Add("Shift");
		if (AltCheckBox.IsChecked == true) list.Add("Alt");
		if (WinCheckBox.IsChecked == true) list.Add("Win");

		string tag = (MainKeyComboBox.SelectedItem as ComboBoxItem)?.Tag?.ToString() ?? "";
		if (!string.IsNullOrEmpty(tag))
		{
			list.Add(tag);
		}

		ResultHotkey = string.Join(" + ", list);
		_isInternalUpdating = true;
		CustomInputTextBox.Text = ResultHotkey;
		PreviewResultText.Text = string.IsNullOrEmpty(ResultHotkey) ? "(空)" : ResultHotkey;
		_isInternalUpdating = false;
	}

	private void CustomInputTextBox_TextChanged(object sender, TextChangedEventArgs e)
	{
		if (_isInternalUpdating) return;
		ResultHotkey = CustomInputTextBox.Text.Trim();
		PreviewResultText.Text = string.IsNullOrEmpty(ResultHotkey) ? "(空)" : ResultHotkey;
	}

	private void OkButton_Click(object sender, RoutedEventArgs e)
	{
		DialogResult = true;
		Close();
	}

	private void CancelButton_Click(object sender, RoutedEventArgs e)
	{
		DialogResult = false;
		Close();
	}
}

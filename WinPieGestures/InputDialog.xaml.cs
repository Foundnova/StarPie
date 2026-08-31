using System;
using System.CodeDom.Compiler;
using System.ComponentModel;
using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Markup;

namespace WinPieGestures;

public partial class InputDialog : Window
{
	private readonly Func<string, (bool IsValid, string ErrorMessage)>? _validator;

	public string InputText { get; private set; } = "";

	public InputDialog(string title, string prompt, string defaultText = "", Func<string, (bool IsValid, string ErrorMessage)>? validator = null)
	{
		InitializeComponent();
		AppThemeManager.ApplyTheme(this, AppThemeManager.CurrentEffectiveTheme);
		base.Title = title;
		TitleTextBlock.Text = title;
		PromptTextBlock.Text = prompt;
		InputTextBox.Text = defaultText;
		_validator = validator;
		if (OkButton != null)
		{
			OkButton.Content = I18n.T("BtnConfirm");
		}
		if (CancelButton != null)
		{
			CancelButton.Content = I18n.T("BtnCancel");
		}
		base.Loaded += delegate
		{
			InputTextBox.Focus();
			InputTextBox.SelectAll();
		};
	}

	private void OkButton_Click(object sender, RoutedEventArgs e)
	{
		string text = InputTextBox.Text.Trim();
		if (string.IsNullOrEmpty(text))
		{
			MessageBox.Show(I18n.T("InputDialogEmpty"), I18n.T("Notice"), MessageBoxButton.OK, MessageBoxImage.Exclamation);
			InputTextBox.Focus();
			return;
		}
		if (_validator != null)
		{
			var (flag, messageBoxText) = _validator(text);
			if (!flag)
			{
				MessageBox.Show(messageBoxText, I18n.T("Notice"), MessageBoxButton.OK, MessageBoxImage.Exclamation);
				InputTextBox.Focus();
				InputTextBox.SelectAll();
				return;
			}
		}
		InputText = text;
		base.DialogResult = true;
		Close();
	}

	private void CancelButton_Click(object sender, RoutedEventArgs e)
	{
		base.DialogResult = false;
		Close();
	}

	private void InputTextBox_KeyDown(object sender, KeyEventArgs e)
	{
		//IL_0001: Unknown result type (might be due to invalid IL or missing references)
		//IL_0007: Invalid comparison between Unknown and I4
		//IL_001a: Unknown result type (might be due to invalid IL or missing references)
		//IL_0021: Invalid comparison between Unknown and I4
		if ((int)e.Key == 6)
		{
			OkButton_Click(sender, e);
			e.Handled = true;
		}
		else if ((int)e.Key == 13)
		{
			CancelButton_Click(sender, e);
			e.Handled = true;
		}
	}
}
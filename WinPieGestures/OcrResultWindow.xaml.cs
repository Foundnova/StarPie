using System;
using System.Diagnostics;
using System.Windows;
using System.Windows.Input;

namespace WinPieGestures;

public partial class OcrResultWindow : Window
{
	public OcrResultWindow(string text, string engineName, string latency)
	{
		InitializeComponent();

		ResultTextBox.Text = text ?? string.Empty;
		CharCountText.Text = $"提取文本 (共 {ResultTextBox.Text.Length} 字符):";
		EngineText.Text = $"{engineName} · {latency}";

		ResultTextBox.SelectAll();
		ResultTextBox.Focus();
	}

	private void Window_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
	{
		if (e.ButtonState == MouseButtonState.Pressed)
		{
			DragMove();
		}
	}

	private void Window_KeyDown(object sender, KeyEventArgs e)
	{
		if (e.Key == Key.Escape)
		{
			Close();
		}
	}

	private void CloseButton_Click(object sender, RoutedEventArgs e)
	{
		Close();
	}

	private void CopyButton_Click(object sender, RoutedEventArgs e)
	{
		try
		{
			System.Windows.Clipboard.SetText(ResultTextBox.Text);
			ClipboardStatusText.Text = "✓ 已重新复制到剪贴板";
			ClipboardStatusText.Foreground = System.Windows.Media.Brushes.LightGreen;
		}
		catch
		{
		}
	}

	private void SearchButton_Click(object sender, RoutedEventArgs e)
	{
		string query = ResultTextBox.Text.Trim();
		if (query.Length > 80)
		{
			query = query.Substring(0, 80);
		}
		if (!string.IsNullOrEmpty(query))
		{
			try
			{
				string url = "https://www.bing.com/search?q=" + Uri.EscapeDataString(query);
				Process.Start(new ProcessStartInfo { FileName = url, UseShellExecute = true });
			}
			catch
			{
			}
		}
	}

	private void SettingsButton_Click(object sender, RoutedEventArgs e)
	{
		OcrManager.ShowSettingsDialog();
	}
}

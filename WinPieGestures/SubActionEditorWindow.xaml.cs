using System;
using System.CodeDom.Compiler;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Forms;
using System.Windows.Markup;

namespace WinPieGestures;

public partial class SubActionEditorWindow : Window
{

	public ObservableCollection<SubSlotViewModel> SubSlots { get; } = new ObservableCollection<SubSlotViewModel>();

	public List<ActionItem> ResultSubActions { get; private set; } = new List<ActionItem>();

	public SubActionEditorWindow(string directionLabel, string sectorName, List<ActionItem>? existingSubActions)
	{
		InitializeComponent();
		AppThemeManager.ApplyTheme(this, ConfigManager.CurrentConfig?.AppTheme ?? "System");
		SectorInfoTitle.Text = directionLabel + " 方位 - [" + sectorName + "] 级联子菜单";
		if (existingSubActions != null && existingSubActions.Count > 0)
		{
			int num = 1;
			foreach (ActionItem existingSubAction in existingSubActions)
			{
				SubSlots.Add(new SubSlotViewModel
				{
					IndexNumber = num++,
					Action = new ActionItem
					{
						Name = existingSubAction.Name,
						Type = existingSubAction.Type,
						Parameter = existingSubAction.Parameter,
						Arguments = existingSubAction.Arguments,
						IconKey = existingSubAction.IconKey,
						CustomIconSvg = existingSubAction.CustomIconSvg,
						InheritAppIconPath = existingSubAction.InheritAppIconPath,
						CommandTerminal = existingSubAction.CommandTerminal
					}
				});
			}
		}
		SubActionsItemsControl.ItemsSource = SubSlots;
		UpdateEmptyState();
	}

	private void UpdateEmptyState()
	{
		EmptyStateBorder.Visibility = ((SubSlots.Count != 0) ? Visibility.Collapsed : Visibility.Visible);
		bool isFan = string.Equals(ConfigManager.CurrentConfig?.SubmenuStyle, "Fan", StringComparison.OrdinalIgnoreCase);
		int maxAllowed = isFan ? 3 : 4;
		AddSubActionButton.IsEnabled = SubSlots.Count < maxAllowed;
		AddSubActionButton.ToolTip = SubSlots.Count >= maxAllowed
			? (isFan ? "当前蜂窝扇模式下最多支持配置 3 个二级级联子动作" : "当前外圈子环模式下最多支持配置 4 个二级级联子动作")
			: null;
	}

	private void AddSubActionButton_Click(object sender, RoutedEventArgs e)
	{
		bool isFan = string.Equals(ConfigManager.CurrentConfig?.SubmenuStyle, "Fan", StringComparison.OrdinalIgnoreCase);
		int maxAllowed = isFan ? 3 : 4;
		if (SubSlots.Count < maxAllowed)
		{
			int num = SubSlots.Count + 1;
			SubSlots.Add(new SubSlotViewModel
			{
				IndexNumber = num,
				Action = new ActionItem
				{
					Name = $"子动作 {num}",
					Type = "Hotkey",
					Parameter = "",
					IconKey = ""
				}
			});
			UpdateEmptyState();
		}
		else
		{
			string styleName = isFan ? "蜂窝扇 (Honeycomb Fan)" : "外圈子环 (Sub-Ring)";
			System.Windows.MessageBox.Show(this, $"当前二级菜单样式为【{styleName}】，每个主扇区最多支持配置 {maxAllowed} 个二级级联子动作。", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
		}
	}

	private void SubTest_Click(object sender, RoutedEventArgs e)
	{
		if (sender is FrameworkElement { DataContext: SubSlotViewModel dataContext })
		{
			ActionExecutor.Execute(dataContext.Action);
		}
	}

	private void DeleteSubAction_Click(object sender, RoutedEventArgs e)
	{
		if (!(sender is FrameworkElement { DataContext: SubSlotViewModel dataContext }))
		{
			return;
		}
		SubSlots.Remove(dataContext);
		int num = 1;
		foreach (SubSlotViewModel subSlot in SubSlots)
		{
			subSlot.IndexNumber = num++;
		}
		UpdateEmptyState();
	}

	private void SubPickIcon_Click(object sender, RoutedEventArgs e)
	{
		if (sender is FrameworkElement { DataContext: SubSlotViewModel dataContext })
		{
			IconPickerWindow iconPickerWindow = new IconPickerWindow(dataContext.IconKey);
			iconPickerWindow.Owner = this;
			if (iconPickerWindow.ShowDialog() == true)
			{
				dataContext.IconKey = iconPickerWindow.SelectedIconKey ?? "";
				dataContext.InheritAppIconPath = "";
			}
		}
	}

	private void SubBrowseApp_Click(object sender, RoutedEventArgs e)
	{
		if (!(sender is FrameworkElement { DataContext: SubSlotViewModel dataContext }))
		{
			return;
		}
		ProgramPickerWindow programPickerWindow = new ProgramPickerWindow();
		programPickerWindow.Owner = this;
		if (programPickerWindow.ShowDialog() == true && !string.IsNullOrEmpty(programPickerWindow.SelectedPath))
		{
			dataContext.Parameter = programPickerWindow.SelectedPath;
			dataContext.InheritAppIconPath = programPickerWindow.SelectedPath;
			dataContext.IconKey = "";
			dataContext.CustomIconSvg = "";
			if (string.IsNullOrEmpty(dataContext.Name) || dataContext.Name.StartsWith("子动作"))
			{
				dataContext.Name = ((!string.IsNullOrEmpty(programPickerWindow.SelectedName)) ? programPickerWindow.SelectedName : Path.GetFileNameWithoutExtension(programPickerWindow.SelectedPath));
			}
		}
	}

	private void SubBrowseFolder_Click(object sender, RoutedEventArgs e)
	{
		if (!(sender is FrameworkElement { DataContext: SubSlotViewModel dataContext }))
		{
			return;
		}
		FolderBrowserDialog folderBrowserDialog = new FolderBrowserDialog
		{
			Description = "选择要打开的本地文件夹",
			UseDescriptionForTitle = true,
			ShowNewFolderButton = true
		};
		if (folderBrowserDialog.ShowDialog() == System.Windows.Forms.DialogResult.OK)
		{
			dataContext.Parameter = folderBrowserDialog.SelectedPath;
			if (string.IsNullOrEmpty(dataContext.Name) || dataContext.Name.StartsWith("子动作"))
			{
				dataContext.Name = Path.GetFileName(folderBrowserDialog.SelectedPath);
			}
			if (string.IsNullOrEmpty(dataContext.IconKey))
			{
				dataContext.IconKey = "Folder";
			}
		}
	}

	private void SaveButton_Click(object sender, RoutedEventArgs e)
	{
		ResultSubActions = SubSlots.Select((SubSlotViewModel s) => s.Action).ToList();
		base.DialogResult = true;
		Close();
	}

	private void CancelButton_Click(object sender, RoutedEventArgs e)
	{
		base.DialogResult = false;
		Close();
	}

		private void SubHotkeyBuilder_Click(object sender, RoutedEventArgs e)
	{
		try
		{
			if (sender is FrameworkElement { DataContext: SubSlotViewModel dataContext })
			{
				HotkeyBuilderDialog dlg = new HotkeyBuilderDialog(dataContext.Parameter ?? "")
				{
					Owner = this
				};
				if (dlg.ShowDialog() == true)
				{
					dataContext.Parameter = dlg.ResultHotkey;
				}
			}
		}
		catch (Exception ex)
		{
			System.Windows.MessageBox.Show(this, "打开按键拼装器失败: " + ex.Message, "错误", MessageBoxButton.OK, MessageBoxImage.Error);
		}
	}

}

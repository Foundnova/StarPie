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
						CustomIconSvg = existingSubAction.CustomIconSvg
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
		AddSubActionButton.IsEnabled = SubSlots.Count < 4;
	}

	private void AddSubActionButton_Click(object sender, RoutedEventArgs e)
	{
		if (SubSlots.Count < 4)
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
}
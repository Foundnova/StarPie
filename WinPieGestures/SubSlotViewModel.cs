using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Windows.Media;

namespace WinPieGestures;

public class SubSlotViewModel : INotifyPropertyChanged
{
	public int IndexNumber { get; set; }

	public ActionItem Action { get; set; }

	public string Name
	{
		get
		{
			return Action.Name ?? "";
		}
		set
		{
			if (Action.Name != value)
			{
				Action.Name = value;
				OnPropertyChanged("Name");
			}
		}
	}

	public string Type
	{
		get
		{
			if (!string.IsNullOrEmpty(Action.Type))
			{
				return Action.Type;
			}
			return "Hotkey";
		}
		set
		{
			if (!(Action.Type != value) || string.IsNullOrEmpty(value))
			{
				return;
			}
			Action.Type = value;
			if ((value == "Folder" || value == "OpenFolder") && string.IsNullOrEmpty(IconKey))
			{
				IconKey = "Folder";
				if (string.IsNullOrEmpty(Name) || Name.StartsWith("子动作"))
				{
					Name = "打开文件夹";
				}
			}
			if (value == "SwitchWindow" && (string.IsNullOrEmpty(Name) || Name.StartsWith("子动作")))
			{
				Name = "切换窗口";
			}
			OnPropertyChanged("Type");
			OnPropertyChanged("IsHotkeyType");
			OnPropertyChanged("IsLaunchType");
			OnPropertyChanged("IsFolderType");
			OnPropertyChanged("IsSystemType");
			OnPropertyChanged("IsCommandType");
			OnPropertyChanged("IsSwitchWindowType");
		}
	}

	public string Parameter
	{
		get
		{
			return Action.Parameter ?? "";
		}
		set
		{
			if (Action.Parameter != value)
			{
				Action.Parameter = value;
				OnPropertyChanged("Parameter");
			}
		}
	}

	public string Arguments
	{
		get
		{
			return Action.Arguments ?? "";
		}
		set
		{
			if (Action.Arguments != value)
			{
				Action.Arguments = value;
				OnPropertyChanged("Arguments");
			}
		}
	}

	public string IconKey
	{
		get
		{
			return Action.IconKey ?? "";
		}
		set
		{
			if (Action.IconKey != value)
			{
				Action.IconKey = value;
				OnPropertyChanged("IconKey");
				OnPropertyChanged("IconDisplayText");
				OnPropertyChanged("HasVectorIcon");
				OnPropertyChanged("VectorIconData");
			}
		}
	}

	public string CustomIconSvg
	{
		get
		{
			return Action.CustomIconSvg ?? "";
		}
		set
		{
			if (Action.CustomIconSvg != value)
			{
				Action.CustomIconSvg = value;
				OnPropertyChanged("CustomIconSvg");
				OnPropertyChanged("IconDisplayText");
				OnPropertyChanged("HasVectorIcon");
				OnPropertyChanged("VectorIconData");
			}
		}
	}

	public string IconDisplayText
	{
		get
		{
			if (!string.IsNullOrEmpty(IconKey))
			{
				return IconKey;
			}
			if (!string.IsNullOrEmpty(CustomIconSvg))
			{
				return "自定义SVG";
			}
			return "图标...";
		}
	}

	public bool HasVectorIcon => VectorIconData != null;

	public Geometry? VectorIconData
	{
		get
		{
			string text = null;
			if (!string.IsNullOrEmpty(CustomIconSvg))
			{
				text = CustomIconSvg;
			}
			else if (!string.IsNullOrEmpty(IconKey))
			{
				if (IconKey.StartsWith("custom:", StringComparison.OrdinalIgnoreCase))
				{
					IconHelper.CustomIconItem customIconItem = IconHelper.GetCustomIcons().FirstOrDefault((IconHelper.CustomIconItem c) => c.Key == IconKey);
					if (customIconItem != null && customIconItem.IsSvg)
					{
						text = customIconItem.SvgData;
					}
				}
				else
				{
					text = IconHelper.GetSvgPathByKey(IconKey);
				}
			}
			if (!string.IsNullOrEmpty(text))
			{
				try
				{
					return Geometry.Parse(text);
				}
				catch
				{
				}
			}
			return null;
		}
	}

	public string SelectedSystemPreset
	{
		get
		{
			return Parameter;
		}
		set
		{
			if (!(Parameter != value) || string.IsNullOrEmpty(value))
			{
				return;
			}
			Parameter = value;
			SystemPresetItem systemPresetItem = SlotViewModel.SystemPresetList.FirstOrDefault((SystemPresetItem p) => p.Key == value);
			if (systemPresetItem != null)
			{
				if (string.IsNullOrEmpty(Name) || Name.StartsWith("子动作"))
				{
					Name = systemPresetItem.DefaultName;
				}
				if (string.IsNullOrEmpty(IconKey))
				{
					IconKey = systemPresetItem.DefaultIconKey;
				}
			}
			OnPropertyChanged("SelectedSystemPreset");
			OnPropertyChanged("Parameter");
		}
	}

	public bool IsHotkeyType => Type == "Hotkey";

	public bool IsLaunchType
	{
		get
		{
			if (!(Type == "Launch"))
			{
				return Type == "App";
			}
			return true;
		}
	}

	public bool IsFolderType
	{
		get
		{
			if (!(Type == "Folder"))
			{
				return Type == "OpenFolder";
			}
			return true;
		}
	}

	public bool IsSystemType => Type == "System";

	public bool IsCommandType => Type == "Command";

	public bool IsSwitchWindowType => Type == "SwitchWindow";

	/// <summary>任务栏第 N 个窗口的序号（1~20，仅数字）。</summary>
	public string NthWindowIndex
	{
		get
		{
			return Action.Parameter ?? "";
		}
		set
		{
			string digits = string.IsNullOrEmpty(value) ? "" : new string(value.Where(char.IsDigit).ToArray());
			if (int.TryParse(digits, out int n))
			{
				n = Math.Max(1, Math.Min(20, n));
				digits = n.ToString();
			}
			if (Action.Parameter != digits)
			{
				Action.Parameter = digits;
				OnPropertyChanged("NthWindowIndex");
			}
		}
	}

	/// <summary>Localized terminal options for Command actions.</summary>
	public List<ActionTypeItem> Terminals => SlotViewModel.LocalizedTerminals;

	public string CommandTerminal
	{
		get
		{
			return Action.CommandTerminal ?? "cmd";
		}
		set
		{
			if (Action.CommandTerminal != value && !string.IsNullOrEmpty(value))
			{
				Action.CommandTerminal = value;
				OnPropertyChanged("CommandTerminal");
			}
		}
	}

	public List<ActionTypeItem> ActionTypes => SlotViewModel.LocalizedActionTypes;

	public event PropertyChangedEventHandler? PropertyChanged;

	protected void OnPropertyChanged(string propertyName)
	{
		PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
	}
}

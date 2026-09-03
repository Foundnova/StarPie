using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;

namespace WinPieGestures;

/// <summary>手势映射编辑行视图模型。</summary>
public class GestureMappingViewModel : INotifyPropertyChanged
{
	public GestureMapping Mapping { get; }

	public GestureMappingViewModel(GestureMapping mapping)
	{
		Mapping = mapping;
	}

	/// <summary>可选图样清单（单段 8 + 常用双段/三段，多段以 "-" 分隔避免歧义）。</summary>
	public List<ActionTypeOption> PatternChoices => PatternOptions;

	public static List<ActionTypeOption> PatternOptions { get; } = new List<ActionTypeOption>
	{
		new ActionTypeOption { Tag = "U", DisplayText = "↑ 上" },
		new ActionTypeOption { Tag = "D", DisplayText = "↓ 下" },
		new ActionTypeOption { Tag = "L", DisplayText = "← 左" },
		new ActionTypeOption { Tag = "R", DisplayText = "→ 右" },
		new ActionTypeOption { Tag = "UL", DisplayText = "↖ 左上" },
		new ActionTypeOption { Tag = "UR", DisplayText = "↗ 右上" },
		new ActionTypeOption { Tag = "DL", DisplayText = "↙ 左下" },
		new ActionTypeOption { Tag = "DR", DisplayText = "↘ 右下" },
		new ActionTypeOption { Tag = "D-R", DisplayText = "下→右 (L)" },
		new ActionTypeOption { Tag = "R-D", DisplayText = "右→下 (Γ)" },
		new ActionTypeOption { Tag = "D-L", DisplayText = "下→左 (反L)" },
		new ActionTypeOption { Tag = "L-D", DisplayText = "左→下" },
		new ActionTypeOption { Tag = "U-R", DisplayText = "上→右" },
		new ActionTypeOption { Tag = "R-U", DisplayText = "右→上 (┘)" },
		new ActionTypeOption { Tag = "U-L", DisplayText = "上→左" },
		new ActionTypeOption { Tag = "L-U", DisplayText = "左→上 (└)" },
		new ActionTypeOption { Tag = "U-D", DisplayText = "上→下" },
		new ActionTypeOption { Tag = "D-U", DisplayText = "下→上" },
		new ActionTypeOption { Tag = "L-R", DisplayText = "左→右" },
		new ActionTypeOption { Tag = "R-L", DisplayText = "右→左" },
		new ActionTypeOption { Tag = "UL-R", DisplayText = "左上→右" },
		new ActionTypeOption { Tag = "UR-L", DisplayText = "右上→左" },
		new ActionTypeOption { Tag = "DL-R", DisplayText = "左下→右" },
		new ActionTypeOption { Tag = "DR-L", DisplayText = "右下→左" },
		new ActionTypeOption { Tag = "D-R-D", DisplayText = "下→右→下 (Z)" },
		new ActionTypeOption { Tag = "R-D-R", DisplayText = "右→下→右" },
		new ActionTypeOption { Tag = "D-L-D", DisplayText = "下→左→下 (反Z)" },
		new ActionTypeOption { Tag = "U-R-U", DisplayText = "上→右→上" },
		new ActionTypeOption { Tag = "U-L-U", DisplayText = "上→左→上" },
		new ActionTypeOption { Tag = "D-R-U", DisplayText = "下→右→上 (S)" },
		new ActionTypeOption { Tag = "D-L-U", DisplayText = "下→左→上 (反S)" },
		new ActionTypeOption { Tag = "L-D-L", DisplayText = "左→下→左" }
	};

	public List<ActionTypeItem> ActionTypes => SlotViewModel.LocalizedActionTypes;

	public string Pattern
	{
		get => Mapping.Pattern ?? "D";
		set
		{
			if (Mapping.Pattern != value && !string.IsNullOrEmpty(value))
			{
				Mapping.Pattern = value;
				OnPropertyChanged(nameof(Pattern));
			}
		}
	}

	public string Type
	{
		get => Mapping.Action.Type ?? "Hotkey";
		set
		{
			if (!string.IsNullOrEmpty(value) && Mapping.Action.Type != value)
			{
				Mapping.Action.Type = value;
				OnPropertyChanged(nameof(Type));
				OnPropertyChanged(nameof(IsHotkeyType));
				OnPropertyChanged(nameof(IsLaunchType));
				OnPropertyChanged(nameof(IsFolderType));
				OnPropertyChanged(nameof(IsSystemType));
				OnPropertyChanged(nameof(IsCommandType));
				OnPropertyChanged(nameof(IsSwitchWindowType));
				OnPropertyChanged(nameof(IsTileType));
			}
		}
	}

	public bool IsHotkeyType => Type == "Hotkey";

	public bool IsLaunchType => Type == "Launch" || Type == "App";

	public bool IsFolderType => Type == "Folder" || Type == "OpenFolder";

	public bool IsSystemType => Type == "System";

	public bool IsCommandType => Type == "Command";

	public bool IsSwitchWindowType => Type == "SwitchWindow";

	public bool IsTileType => Type == "Tile";

	/// <summary>平铺布局下拉（key → 显示名）。</summary>
	public List<ActionTypeOption> TileLayoutOptions
	{
		get
		{
			List<ActionTypeOption> list = new List<ActionTypeOption>();
			foreach (string key in WindowTiler.LayoutKeys)
			{
				list.Add(new ActionTypeOption { Tag = key, DisplayText = WindowTiler.LayoutDisplayName(key) });
			}
			return list;
		}
	}

	/// <summary>平铺布局（写入 Parameter）。</summary>
	public string TileLayout
	{
		get
		{
			return Mapping.Action.Parameter ?? "";
		}
		set
		{
			if (Mapping.Action.Parameter != value)
			{
				Mapping.Action.Parameter = value;
				OnPropertyChanged(nameof(TileLayout));
			}
		}
	}

	/// <summary>命令动作的终端选项。</summary>
	public List<ActionTypeItem> Terminals => SlotViewModel.LocalizedTerminals;

	public string CommandTerminal
	{
		get => Mapping.Action.CommandTerminal ?? "cmd";
		set
		{
			if (Mapping.Action.CommandTerminal != value && !string.IsNullOrEmpty(value))
			{
				Mapping.Action.CommandTerminal = value;
				OnPropertyChanged(nameof(CommandTerminal));
			}
		}
	}

	/// <summary>系统控制预设（Key ↔ Parameter）。</summary>
	public string SelectedSystemPreset
	{
		get => Parameter;
		set
		{
			if (Parameter != value && !string.IsNullOrEmpty(value))
			{
				Parameter = value;
				OnPropertyChanged(nameof(SelectedSystemPreset));
			}
		}
	}

	/// <summary>切换窗口的序号（仅数字 1~20）。</summary>
	public string NthWindowIndex
	{
		get => Parameter;
		set
		{
			string digits = string.IsNullOrEmpty(value) ? "" : new string(value.Where(char.IsDigit).ToArray());
			if (int.TryParse(digits, out int n))
			{
				n = Math.Max(1, Math.Min(20, n));
				digits = n.ToString();
			}
			if (Parameter != digits)
			{
				Parameter = digits;
				OnPropertyChanged(nameof(NthWindowIndex));
			}
		}
	}

	public string Parameter
	{
		get => Mapping.Action.Parameter ?? "";
		set
		{
			if (Mapping.Action.Parameter != value)
			{
				Mapping.Action.Parameter = value ?? "";
				OnPropertyChanged(nameof(Parameter));
			}
		}
	}

	public string Name
	{
		get => Mapping.Action.Name ?? "";
		set
		{
			if (Mapping.Action.Name != value)
			{
				Mapping.Action.Name = value ?? "";
				OnPropertyChanged(nameof(Name));
			}
		}
	}

	public event PropertyChangedEventHandler? PropertyChanged;

	protected void OnPropertyChanged(string propertyName)
	{
		PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
	}
}
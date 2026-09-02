using System.Collections.Generic;
using System.ComponentModel;

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
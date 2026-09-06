using System.Collections.Generic;
using System.ComponentModel;

namespace WinPieGestures;

/// <summary>
/// 独立轮盘层模型（支持无限多层轮盘，每层独立拥有扇区数量、槽位动作列表与中心核圆）
/// </summary>
public class WheelLayer : INotifyPropertyChanged
{
	private string _name = "第 1 层";

	public string Name
	{
		get => _name;
		set
		{
			if (_name != value)
			{
				_name = value;
				OnPropertyChanged(nameof(Name));
			}
		}
	}

	public int SectorCount { get; set; } = 8;

	public List<ActionItem> Actions { get; set; } = new List<ActionItem>();

	/// <summary>本层专属中心核心圆动作</summary>
	public ActionItem? CenterAction { get; set; }

	/// <summary>是否启用本层专属中心核心圆动作</summary>
	public bool EnableCenterAction { get; set; } = false;

	public event PropertyChangedEventHandler? PropertyChanged;

	protected void OnPropertyChanged(string propertyName)
	{
		PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
	}

	public WheelLayer Clone()
	{
		WheelLayer clone = new WheelLayer
		{
			Name = this.Name,
			SectorCount = this.SectorCount,
			EnableCenterAction = this.EnableCenterAction,
			CenterAction = this.CenterAction?.Clone(),
			Actions = new List<ActionItem>()
		};
		if (this.Actions != null)
		{
			foreach (var action in this.Actions)
			{
				clone.Actions.Add(action.Clone());
			}
		}
		return clone;
	}

	public override string ToString()
	{
		return Name;
	}
}

using System.Collections.Generic;
using System.ComponentModel;

namespace WinPieGestures;

public class WheelProfile : INotifyPropertyChanged
{
	private string _processName = "Global";

	public string ProcessName
	{
		get => _processName;
		set
		{
			if (_processName != value)
			{
				_processName = value;
				OnPropertyChanged(nameof(ProcessName));
			}
		}
	}

	public int SectorCount { get; set; } = 8;

	public List<ActionItem> Actions { get; set; } = new List<ActionItem>();

	/// <summary>中心核心圆死区动作（在外甩脱离取消开启时，松开光标于中心死区触发）</summary>
	public ActionItem? CenterAction { get; set; }

	/// <summary>是否启用中心核心圆动作</summary>
	public bool EnableCenterAction { get; set; } = false;

	public event PropertyChangedEventHandler? PropertyChanged;

	protected void OnPropertyChanged(string propertyName)
	{
		PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
	}

	public WheelProfile Clone(string newProcessName)
	{
		WheelProfile clone = new WheelProfile
		{
			ProcessName = newProcessName,
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
		return ProcessName;
	}
}

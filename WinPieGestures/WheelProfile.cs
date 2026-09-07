using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;

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

	/// <summary>多层轮盘层级列表（支持无限多层独立配置）</summary>
	public List<WheelLayer> Layers { get; set; } = new List<WheelLayer>();

	/// <summary>当前活跃的轮盘层级索引（默认 0 = 第 1 层）</summary>
	public int ActiveLayerIndex { get; set; } = 0;

	public event PropertyChangedEventHandler? PropertyChanged;

	protected void OnPropertyChanged(string propertyName)
	{
		PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
	}

	/// <summary>
	/// 确保 Layers 至少包含一个有效层；若旧配置未包含 Layers，自动无损迁移根属性为第 1 层；
	/// 校验并自愈各层动作完整性，防止因反序列化空列表导致层级覆盖与动作丢失。
	/// </summary>
	public void EnsureLayers()
	{
		if (Layers == null)
		{
			Layers = new List<WheelLayer>();
		}
		if (Layers.Count == 0)
		{
			WheelLayer layer0 = new WheelLayer
			{
				Name = "第 1 层",
				SectorCount = this.SectorCount > 0 ? this.SectorCount : 8,
				CenterAction = this.CenterAction?.Clone(),
				EnableCenterAction = this.EnableCenterAction,
				Actions = new List<ActionItem>()
			};
			if (this.Actions != null && this.Actions.Count > 0)
			{
				foreach (var action in this.Actions)
				{
					layer0.Actions.Add(action?.Clone() ?? new ActionItem { Type = "Hotkey", Name = "未命名动作", Parameter = "" });
				}
			}
			else
			{
				for (int i = 0; i < layer0.SectorCount; i++)
				{
					layer0.Actions.Add(new ActionItem { Type = "Hotkey", Name = $"动作 {i + 1}", Parameter = "" });
				}
			}
			Layers.Add(layer0);
		}
		else if (Layers.Count == 1 && (Layers[0].Actions == null || Layers[0].Actions.Count == 0 || Layers[0].Actions.All(a => string.IsNullOrEmpty(a.Type) || (a.Type == "Hotkey" && string.IsNullOrEmpty(a.Parameter) && string.IsNullOrEmpty(a.InheritAppIconPath)))))
		{
			// 自愈保护：若仅有第 1 层且层内动作全空，但根属性 Actions 中存在真实自定义配置，则无损同步给第 1 层
			if (this.Actions != null && this.Actions.Count > 0 && this.Actions.Any(a => !string.IsNullOrEmpty(a.Parameter) || !string.IsNullOrEmpty(a.InheritAppIconPath) || (a.SubActions != null && a.SubActions.Count > 0)))
			{
				Layers[0].Actions.Clear();
				foreach (var action in this.Actions)
				{
					Layers[0].Actions.Add(action?.Clone() ?? new ActionItem { Type = "Hotkey", Name = "未命名动作", Parameter = "" });
				}
			}
		}

		// 确保每一层均具备合法结构、有效扇区数与非空动作列表
		for (int l = 0; l < Layers.Count; l++)
		{
			var layer = Layers[l];
			if (string.IsNullOrWhiteSpace(layer.Name))
			{
				layer.Name = $"第 {l + 1} 层";
			}
			if (layer.SectorCount != 4 && layer.SectorCount != 8 && layer.SectorCount != 12)
			{
				layer.SectorCount = (this.SectorCount is 4 or 8 or 12) ? this.SectorCount : 8;
			}
			if (layer.Actions == null)
			{
				layer.Actions = new List<ActionItem>();
			}
			while (layer.Actions.Count < layer.SectorCount)
			{
				layer.Actions.Add(new ActionItem
				{
					Type = "Hotkey",
					Name = $"动作 {layer.Actions.Count + 1}",
					Parameter = ""
				});
			}
			for (int a = 0; a < layer.Actions.Count; a++)
			{
				if (layer.Actions[a] == null)
				{
					layer.Actions[a] = new ActionItem { Type = "Hotkey", Name = $"动作 {a + 1}", Parameter = "" };
				}
				else
				{
					if (string.IsNullOrEmpty(layer.Actions[a].Type))
					{
						layer.Actions[a].Type = "Hotkey";
						if (string.IsNullOrEmpty(layer.Actions[a].Name))
						{
							layer.Actions[a].Name = $"动作 {a + 1}";
						}
					}
					layer.Actions[a].SubActions ??= new List<ActionItem>();
				}
			}
		}

		if (ActiveLayerIndex < 0 || ActiveLayerIndex >= Layers.Count)
		{
			ActiveLayerIndex = 0;
		}
		SyncRootPropertiesFromActiveLayer();
	}

	/// <summary>将当前活跃层的数据同步到根属性（保证旧有读取逻辑 100% 兼容）</summary>
	public void SyncRootPropertiesFromActiveLayer()
	{
		if (Layers != null && ActiveLayerIndex >= 0 && ActiveLayerIndex < Layers.Count)
		{
			WheelLayer current = Layers[ActiveLayerIndex];
			this.SectorCount = current.SectorCount;
			this.Actions = current.Actions;
			this.CenterAction = current.CenterAction;
			this.EnableCenterAction = current.EnableCenterAction;
		}
	}

	/// <summary>将根属性的变更同步写回当前活跃层</summary>
	public void SyncActiveLayerFromRootProperties()
	{
		if (Layers != null && ActiveLayerIndex >= 0 && ActiveLayerIndex < Layers.Count)
		{
			WheelLayer current = Layers[ActiveLayerIndex];
			current.SectorCount = this.SectorCount;
			current.Actions = this.Actions;
			current.CenterAction = this.CenterAction;
			current.EnableCenterAction = this.EnableCenterAction;
		}
	}

	public WheelLayer GetActiveLayer()
	{
		EnsureLayers();
		return Layers[ActiveLayerIndex];
	}

	public WheelProfile Clone(string newProcessName)
	{
		EnsureLayers();
		WheelProfile clone = new WheelProfile
		{
			ProcessName = newProcessName,
			SectorCount = this.SectorCount,
			EnableCenterAction = this.EnableCenterAction,
			CenterAction = this.CenterAction?.Clone(),
			ActiveLayerIndex = this.ActiveLayerIndex,
			Actions = new List<ActionItem>(),
			Layers = new List<WheelLayer>()
		};
		foreach (var layer in this.Layers)
		{
			clone.Layers.Add(layer.Clone());
		}
		clone.SyncRootPropertiesFromActiveLayer();
		return clone;
	}

	public override string ToString()
	{
		return ProcessName;
	}
}

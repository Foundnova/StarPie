using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Microsoft.Win32;

namespace WinPieGestures
{
    public class SubSlotViewModel : INotifyPropertyChanged
    {
        public int IndexNumber { get; set; }
        public ActionItem Action { get; set; }

        public string Name
        {
            get => Action.Name ?? "";
            set
            {
                if (Action.Name != value)
                {
                    Action.Name = value;
                    OnPropertyChanged(nameof(Name));
                }
            }
        }

        public string Type
        {
            get => string.IsNullOrEmpty(Action.Type) ? "Hotkey" : Action.Type;
            set
            {
                if (Action.Type != value && !string.IsNullOrEmpty(value))
                {
                    Action.Type = value;
                    if ((value == "Folder" || value == "OpenFolder") && string.IsNullOrEmpty(IconKey))
                    {
                        IconKey = "Folder";
                        if (string.IsNullOrEmpty(Name) || Name.StartsWith("子动作"))
                        {
                            Name = "打开文件夹";
                        }
                    }
                    OnPropertyChanged(nameof(Type));
                    OnPropertyChanged(nameof(IsHotkeyType));
                    OnPropertyChanged(nameof(IsLaunchType));
                    OnPropertyChanged(nameof(IsFolderType));
                    OnPropertyChanged(nameof(IsSystemType));
                }
            }
        }

        public string Parameter
        {
            get => Action.Parameter ?? "";
            set
            {
                if (Action.Parameter != value)
                {
                    Action.Parameter = value;
                    OnPropertyChanged(nameof(Parameter));
                }
            }
        }

        public string IconKey
        {
            get => Action.IconKey ?? "";
            set
            {
                if (Action.IconKey != value)
                {
                    Action.IconKey = value;
                    OnPropertyChanged(nameof(IconKey));
                    OnPropertyChanged(nameof(IconDisplayText));
                    OnPropertyChanged(nameof(HasVectorIcon));
                    OnPropertyChanged(nameof(VectorIconData));
                }
            }
        }

        public string CustomIconSvg
        {
            get => Action.CustomIconSvg ?? "";
            set
            {
                if (Action.CustomIconSvg != value)
                {
                    Action.CustomIconSvg = value;
                    OnPropertyChanged(nameof(CustomIconSvg));
                    OnPropertyChanged(nameof(IconDisplayText));
                    OnPropertyChanged(nameof(HasVectorIcon));
                    OnPropertyChanged(nameof(VectorIconData));
                }
            }
        }

        public string IconDisplayText
        {
            get
            {
                if (!string.IsNullOrEmpty(IconKey)) return IconKey;
                if (!string.IsNullOrEmpty(CustomIconSvg)) return "自定义SVG";
                return "图标...";
            }
        }

        public bool HasVectorIcon => VectorIconData != null;

        public Geometry? VectorIconData
        {
            get
            {
                string? data = null;
                if (!string.IsNullOrEmpty(CustomIconSvg)) data = CustomIconSvg;
                else if (!string.IsNullOrEmpty(IconKey))
                {
                    if (IconKey.StartsWith("custom:", StringComparison.OrdinalIgnoreCase))
                    {
                        var custom = IconHelper.GetCustomIcons().FirstOrDefault(c => c.Key == IconKey);
                        if (custom != null && custom.IsSvg) data = custom.SvgData;
                    }
                    else
                    {
                        data = IconHelper.GetSvgPathByKey(IconKey);
                    }
                }
                
                if (!string.IsNullOrEmpty(data))
                {
                    try
                    {
                        return Geometry.Parse(data);
                    }
                    catch { }
                }
                return null;
            }
        }

        public string SelectedSystemPreset
        {
            get => Parameter;
            set
            {
                if (Parameter != value && !string.IsNullOrEmpty(value))
                {
                    Parameter = value;
                    var preset = SlotViewModel.SystemPresetList.FirstOrDefault(p => p.Key == value);
                    if (preset != null)
                    {
                        if (string.IsNullOrEmpty(Name) || Name.StartsWith("子动作"))
                        {
                            Name = preset.DefaultName;
                        }
                        if (string.IsNullOrEmpty(IconKey))
                        {
                            IconKey = preset.DefaultIconKey;
                        }
                    }
                    OnPropertyChanged(nameof(SelectedSystemPreset));
                    OnPropertyChanged(nameof(Parameter));
                }
            }
        }

        public bool IsHotkeyType => Type == "Hotkey";
        public bool IsLaunchType => Type == "Launch" || Type == "App";
        public bool IsFolderType => Type == "Folder" || Type == "OpenFolder";
        public bool IsSystemType => Type == "System";

        public List<ActionTypeItem> ActionTypes => SlotViewModel.LocalizedActionTypes;

        public event PropertyChangedEventHandler? PropertyChanged;
        protected void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }

    public partial class SubActionEditorWindow : Window
    {
        public ObservableCollection<SubSlotViewModel> SubSlots { get; } = new ObservableCollection<SubSlotViewModel>();
        public List<ActionItem> ResultSubActions { get; private set; } = new List<ActionItem>();

        public SubActionEditorWindow(string directionLabel, string sectorName, List<ActionItem>? existingSubActions)
        {
            InitializeComponent();

            SectorInfoTitle.Text = $"{directionLabel} 方位 - [{sectorName}] 级联子菜单";

            if (existingSubActions != null && existingSubActions.Count > 0)
            {
                int idx = 1;
                foreach (var item in existingSubActions)
                {
                    SubSlots.Add(new SubSlotViewModel
                    {
                        IndexNumber = idx++,
                        Action = new ActionItem
                        {
                            Name = item.Name,
                            Type = item.Type,
                            Parameter = item.Parameter,
                            Arguments = item.Arguments,
                            IconKey = item.IconKey,
                            CustomIconSvg = item.CustomIconSvg
                        }
                    });
                }
            }

            SubActionsItemsControl.ItemsSource = SubSlots;
            UpdateEmptyState();
        }

        private void UpdateEmptyState()
        {
            EmptyStateBorder.Visibility = (SubSlots.Count == 0) ? Visibility.Visible : Visibility.Collapsed;
            AddSubActionButton.IsEnabled = (SubSlots.Count < 4);
        }

        private void AddSubActionButton_Click(object sender, RoutedEventArgs e)
        {
            if (SubSlots.Count >= 4) return;

            int newIdx = SubSlots.Count + 1;
            SubSlots.Add(new SubSlotViewModel
            {
                IndexNumber = newIdx,
                Action = new ActionItem
                {
                    Name = $"子动作 {newIdx}",
                    Type = "Hotkey",
                    Parameter = "",
                    IconKey = ""
                }
            });

            UpdateEmptyState();
        }

        private void DeleteSubAction_Click(object sender, RoutedEventArgs e)
        {
            if (sender is FrameworkElement elem && elem.DataContext is SubSlotViewModel vm)
            {
                SubSlots.Remove(vm);
                int idx = 1;
                foreach (var s in SubSlots)
                {
                    s.IndexNumber = idx++;
                }
                UpdateEmptyState();
            }
        }

        private void SubPickIcon_Click(object sender, RoutedEventArgs e)
        {
            if (sender is FrameworkElement elem && elem.DataContext is SubSlotViewModel vm)
            {
                var picker = new IconPickerWindow(vm.IconKey);
                picker.Owner = this;
                if (picker.ShowDialog() == true)
                {
                    vm.IconKey = picker.SelectedIconKey ?? "";
                }
            }
        }

        private void SubBrowseApp_Click(object sender, RoutedEventArgs e)
        {
            if (sender is FrameworkElement elem && elem.DataContext is SubSlotViewModel vm)
            {
                var dialog = new Microsoft.Win32.OpenFileDialog
                {
                    Title = "选择要启动的应用程序",
                    Filter = "可执行程序 (*.exe;*.lnk;*.bat)|*.exe;*.lnk;*.bat|所有文件 (*.*)|*.*"
                };

                if (dialog.ShowDialog() == true)
                {
                    vm.Parameter = dialog.FileName;
                    if (string.IsNullOrEmpty(vm.Name) || vm.Name.StartsWith("子动作"))
                    {
                        vm.Name = Path.GetFileNameWithoutExtension(dialog.FileName);
                    }
                }
            }
        }

        private void SubBrowseFolder_Click(object sender, RoutedEventArgs e)
        {
            if (sender is FrameworkElement elem && elem.DataContext is SubSlotViewModel vm)
            {
                var dialog = new System.Windows.Forms.FolderBrowserDialog
                {
                    Description = "选择要打开的本地文件夹",
                    UseDescriptionForTitle = true,
                    ShowNewFolderButton = true
                };

                if (dialog.ShowDialog() == System.Windows.Forms.DialogResult.OK)
                {
                    vm.Parameter = dialog.SelectedPath;
                    if (string.IsNullOrEmpty(vm.Name) || vm.Name.StartsWith("子动作"))
                    {
                        vm.Name = Path.GetFileName(dialog.SelectedPath);
                    }
                    if (string.IsNullOrEmpty(vm.IconKey))
                    {
                        vm.IconKey = "Folder";
                    }
                }
            }
        }

        private void SaveButton_Click(object sender, RoutedEventArgs e)
        {
            ResultSubActions = SubSlots.Select(s => s.Action).ToList();
            DialogResult = true;
            Close();
        }

        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }
    }
}

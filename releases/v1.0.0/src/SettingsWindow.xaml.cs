using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Forms;
using Application = System.Windows.Application;
using MessageBox = System.Windows.MessageBox;

namespace WinPieGestures
{
    public partial class SettingsWindow : Window
    {
        private NotifyIcon _notifyIcon;
        private bool _isClosingFromTray = false;
        private WheelProfile _selectedProfile;
        private readonly ObservableCollection<SlotViewModel> _slotViewModels = new ObservableCollection<SlotViewModel>();

        // Direction Labels
        private static readonly string[] Directions4 = { "右 (E)", "下 (S)", "左 (W)", "上 (N)" };
        private static readonly string[] Directions8 = { "右 (E)", "右下 (SE)", "下 (S)", "左下 (SW)", "左 (W)", "左上 (NW)", "上 (N)", "右上 (NE)" };
        private static readonly string[] Directions12 = { 
            "右 (E)", "右下 30°", "右下 60°", "下 (S)", "左下 60°", "左下 30°", 
            "左 (W)", "左上 30°", "左上 60°", "上 (N)", "右上 60°", "右上 30°" 
        };

        public SettingsWindow()
        {
            InitializeComponent();
            InitializeTrayIcon();

            // Load profiles to listbox
            ProfilesListBox.ItemsSource = ConfigManager.CurrentConfig.Profiles;
            ThresholdSlider.Value = ConfigManager.CurrentConfig.DragThreshold;
            ThresholdValueLabel.Text = ConfigManager.CurrentConfig.DragThreshold.ToString("0");

            SlotsItemsControl.ItemsSource = _slotViewModels;
        }

        private void InitializeTrayIcon()
        {
            _notifyIcon = new NotifyIcon
            {
                Icon = System.Drawing.SystemIcons.Application, // Fallback icon
                Visible = true,
                Text = "WinPieGestures - 鼠标轮盘笔势"
            };

            // Double click opens settings
            _notifyIcon.DoubleClick += (s, e) => ShowSettings();

            // Context Menu
            var contextMenu = new ContextMenuStrip();
            contextMenu.Items.Add("设置 (Settings)", null, (s, e) => ShowSettings());
            contextMenu.Items.Add("-");
            contextMenu.Items.Add("退出 (Exit)", null, (s, e) => ExitApplication());

            _notifyIcon.ContextMenuStrip = contextMenu;
        }

        private void ShowSettings()
        {
            this.Show();
            this.WindowState = WindowState.Normal;
            this.Activate();
        }

        private void ExitApplication()
        {
            _isClosingFromTray = true;
            _notifyIcon.Visible = false;
            _notifyIcon.Dispose();
            Application.Current.Shutdown();
        }

        private void Window_Closing(object sender, CancelEventArgs e)
        {
            if (!_isClosingFromTray)
            {
                e.Cancel = true;
                this.Hide();
                _notifyIcon.ShowBalloonTip(
                    2000, 
                    "WinPieGestures", 
                    "应用已最小化至系统托盘，将在后台继续运行鼠标笔势监视。", 
                    ToolTipIcon.Info
                );
            }
        }

        private void ProfilesListBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            _selectedProfile = ProfilesListBox.SelectedItem as WheelProfile;

            if (_selectedProfile != null)
            {
                SelectProfilePlaceholder.Visibility = Visibility.Collapsed;
                DetailsGrid.Visibility = Visibility.Visible;

                ProcessNameTextBox.Text = _selectedProfile.ProcessName;
                ProcessNameTextBox.IsEnabled = _selectedProfile.ProcessName != "Global";

                // Setup layout index
                int layoutIndex = 1; // Default to 8
                if (_selectedProfile.SectorCount == 4) layoutIndex = 0;
                else if (_selectedProfile.SectorCount == 12) layoutIndex = 2;
                SectorCountComboBox.SelectedIndex = layoutIndex;

                LoadSlotsForSelectedProfile();
            }
            else
            {
                SelectProfilePlaceholder.Visibility = Visibility.Visible;
                DetailsGrid.Visibility = Visibility.Collapsed;
            }
        }

        private void LoadSlotsForSelectedProfile()
        {
            if (_selectedProfile == null) return;

            _slotViewModels.Clear();
            int n = _selectedProfile.SectorCount;
            string[] directions = n == 4 ? Directions4 : (n == 12 ? Directions12 : Directions8);

            // Ensure actions list matches sector count
            while (_selectedProfile.Actions.Count < n)
            {
                _selectedProfile.Actions.Add(new ActionItem { Type = "Hotkey", Name = "未命名", Parameter = "" });
            }
            while (_selectedProfile.Actions.Count > n)
            {
                _selectedProfile.Actions.RemoveAt(_selectedProfile.Actions.Count - 1);
            }

            for (int i = 0; i < n; i++)
            {
                var action = _selectedProfile.Actions[i];
                var vm = new SlotViewModel
                {
                    DirectionName = directions[i],
                    Action = action
                };
                _slotViewModels.Add(vm);
            }
        }

        private void SectorCountComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_selectedProfile == null || SectorCountComboBox.SelectedValue == null) return;

            var selectedItem = SectorCountComboBox.SelectedItem as ComboBoxItem;
            if (selectedItem != null && int.TryParse(selectedItem.Tag.ToString(), out int n))
            {
                if (_selectedProfile.SectorCount != n)
                {
                    _selectedProfile.SectorCount = n;
                    LoadSlotsForSelectedProfile();
                }
            }
        }

        private void ProcessNameTextBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (_selectedProfile != null && _selectedProfile.ProcessName != "Global")
            {
                _selectedProfile.ProcessName = ProcessNameTextBox.Text;
                ProfilesListBox.Items.Refresh();
            }
        }

        private void AddProfile_Click(object sender, RoutedEventArgs e)
        {
            var newProfile = new WheelProfile
            {
                ProcessName = "newapp.exe",
                SectorCount = 8,
                Actions = new List<ActionItem>()
            };

            for (int i = 0; i < 8; i++)
            {
                newProfile.Actions.Add(new ActionItem { Type = "Hotkey", Name = "快捷动作", Parameter = "" });
            }

            ConfigManager.CurrentConfig.Profiles.Add(newProfile);
            ProfilesListBox.Items.Refresh();
            ProfilesListBox.SelectedItem = newProfile;
        }

        private void DeleteProfile_Click(object sender, RoutedEventArgs e)
        {
            if (_selectedProfile == null) return;

            if (_selectedProfile.ProcessName == "Global")
            {
                MessageBox.Show("全局默认配置文件不能被删除。", "警告", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            ConfigManager.CurrentConfig.Profiles.Remove(_selectedProfile);
            ProfilesListBox.Items.Refresh();
            ProfilesListBox.SelectedIndex = 0;
        }

        private void ThresholdSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (ThresholdValueLabel != null)
            {
                ThresholdValueLabel.Text = e.NewValue.ToString("0");
                ConfigManager.CurrentConfig.DragThreshold = e.NewValue;
            }
        }

        private void SaveButton_Click(object sender, RoutedEventArgs e)
        {
            ConfigManager.SaveConfig();
            MessageBox.Show("配置保存成功！并在内存中即时生效。", "保存成功", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        private void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            this.Hide();
        }

        private void Test_Click(object sender, RoutedEventArgs e)
        {
            var button = sender as System.Windows.Controls.Button;
            var vm = button?.DataContext as SlotViewModel;
            if (vm != null && vm.Action != null)
            {
                Debug.WriteLine($"Testing action: {vm.Action.Name}");
                ActionExecutor.Execute(vm.Action);
            }
        }
    }

    public class SlotViewModel : INotifyPropertyChanged
    {
        private string _directionName;
        private ActionItem _action;

        public string DirectionName
        {
            get => _directionName;
            set
            {
                _directionName = value;
                OnPropertyChanged(nameof(DirectionName));
            }
        }

        public ActionItem Action
        {
            get => _action;
            set
            {
                _action = value;
                OnPropertyChanged(nameof(Action));
                if (_action != null)
                {
                    // Track type changes
                    OnPropertyChanged(nameof(IsLaunchType));
                }
            }
        }

        public bool IsLaunchType => Action?.Type == "Launch";

        public event PropertyChangedEventHandler PropertyChanged;

        protected void OnPropertyChanged(string name)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
        }
    }
}

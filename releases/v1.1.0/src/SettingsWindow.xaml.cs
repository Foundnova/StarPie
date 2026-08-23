using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Forms;
using System.Windows.Media.Animation;
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

            // Load theme & style settings
            SetComboBoxSelectedValue(ThemeComboBox, ConfigManager.CurrentConfig.Theme);
            SetComboBoxSelectedValue(UiStyleComboBox, ConfigManager.CurrentConfig.UiStyle);

            CustomSectorBgTextBox.Text = ConfigManager.CurrentConfig.CustomSectorBg;
            CustomSectorBorderTextBox.Text = ConfigManager.CurrentConfig.CustomSectorBorder;
            CustomHighlightBgTextBox.Text = ConfigManager.CurrentConfig.CustomHighlightBg;
            CustomHighlightBorderTextBox.Text = ConfigManager.CurrentConfig.CustomHighlightBorder;
            CustomTextTextBox.Text = ConfigManager.CurrentConfig.CustomText;

            CustomColorsPanel.Visibility = ConfigManager.CurrentConfig.Theme == "Custom" ? Visibility.Visible : Visibility.Collapsed;

            // Load sliders & shape settings
            WheelRadiusSlider.Value = ConfigManager.CurrentConfig.WheelRadius;
            WheelRadiusLabel.Text = ConfigManager.CurrentConfig.WheelRadius.ToString("0");
            InnerRadiusSlider.Value = ConfigManager.CurrentConfig.InnerRadius;
            InnerRadiusLabel.Text = ConfigManager.CurrentConfig.InnerRadius.ToString("0");
            CoreRadiusSlider.Value = ConfigManager.CurrentConfig.CoreRadius;
            CoreRadiusLabel.Text = ConfigManager.CurrentConfig.CoreRadius.ToString("0");

            SetComboBoxSelectedValue(ShapeComboBox, ConfigManager.CurrentConfig.Shape);
            ShowTextCheckBox.IsChecked = ConfigManager.CurrentConfig.ShowText;

            // Load Scene Isolation settings
            DisableOnFullScreenCheckBox.IsChecked = ConfigManager.CurrentConfig.DisableOnFullScreen;
            CtrlModifierCheckBox.IsChecked = ConfigManager.CurrentConfig.DisableOnCtrl;
            ShiftModifierCheckBox.IsChecked = ConfigManager.CurrentConfig.DisableOnShift;
            AltModifierCheckBox.IsChecked = ConfigManager.CurrentConfig.DisableOnAlt;

            if (ConfigManager.CurrentConfig.BlacklistedProcesses != null)
            {
                foreach (var proc in ConfigManager.CurrentConfig.BlacklistedProcesses)
                {
                    BlacklistListBox.Items.Add(proc);
                }
            }

            // Initialize color preview borders
            UpdateColorPreviews();

            SlotsItemsControl.ItemsSource = _slotViewModels;

            // Check UAC privileges and show warning if not elevated
            bool isAdmin = IsRunningAsAdmin();
            UacWarningCard.Visibility = isAdmin ? Visibility.Collapsed : Visibility.Visible;
        }

        private ToolStripMenuItem? _pauseResumeMenuItem;

        private void InitializeTrayIcon()
        {
            System.Drawing.Icon trayIcon = System.Drawing.SystemIcons.Application;
            try
            {
                string iconPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "app_icon.ico");
                if (File.Exists(iconPath))
                {
                    trayIcon = new System.Drawing.Icon(iconPath);
                }
            }
            catch { }

            _notifyIcon = new NotifyIcon
            {
                Icon = trayIcon,
                Visible = true,
                Text = "WinPieGestures v1.1.0 - 鼠标轮盘笔势"
            };

            // Double click opens settings
            _notifyIcon.DoubleClick += (s, e) => ShowSettings(0);

            // Modern Context Menu
            var contextMenu = new ContextMenuStrip();
            
            var titleItem = new ToolStripMenuItem("WinPieGestures v1.1.0")
            {
                Enabled = false,
                Font = new System.Drawing.Font(System.Drawing.SystemFonts.DefaultFont, System.Drawing.FontStyle.Bold)
            };
            contextMenu.Items.Add(titleItem);
            contextMenu.Items.Add(new ToolStripSeparator());

            _pauseResumeMenuItem = new ToolStripMenuItem("⏸️ 暂停手势", null, (s, e) => TogglePauseGestures());
            contextMenu.Items.Add(_pauseResumeMenuItem);

            contextMenu.Items.Add("⚙️ 偏好设置 (Settings)", null, (s, e) => ShowSettings(0));
            contextMenu.Items.Add("📋 更新日志与关于 (About)", null, (s, e) => ShowSettings(3));
            contextMenu.Items.Add("🛡️ 以管理员身份重启", null, (s, e) => ElevatePrivileges_Click(s, new RoutedEventArgs()));
            
            contextMenu.Items.Add(new ToolStripSeparator());
            contextMenu.Items.Add("❌ 退出 (Exit)", null, (s, e) => ExitApplication());

            _notifyIcon.ContextMenuStrip = contextMenu;
        }

        private void TogglePauseGestures()
        {
            if (App.MainMouseHook == null) return;
            App.MainMouseHook.IsPaused = !App.MainMouseHook.IsPaused;

            if (App.MainMouseHook.IsPaused)
            {
                if (_pauseResumeMenuItem != null) _pauseResumeMenuItem.Text = "▶️ 恢复手势";
                _notifyIcon.Text = "WinPieGestures (已暂停)";
                _notifyIcon.ShowBalloonTip(1500, "WinPieGestures", "已暂停鼠标笔势监控，原生右键直接生效。", ToolTipIcon.Warning);
            }
            else
            {
                if (_pauseResumeMenuItem != null) _pauseResumeMenuItem.Text = "⏸️ 暂停手势";
                _notifyIcon.Text = "WinPieGestures v1.1.0 - 鼠标轮盘笔势";
                _notifyIcon.ShowBalloonTip(1500, "WinPieGestures", "鼠标手势监控已恢复生效。", ToolTipIcon.Info);
            }
        }

        private void ShowSettings(int tabIndex = 0)
        {
            if (SidebarMenuListBox != null && tabIndex >= 0 && tabIndex < SidebarMenuListBox.Items.Count)
            {
                SidebarMenuListBox.SelectedIndex = tabIndex;
            }

            this.Opacity = 0.0;
            this.Show();
            this.WindowState = WindowState.Normal;
            this.Activate();

            // Smooth fade-in animation
            var anim = new DoubleAnimation(0.0, 1.0, new Duration(TimeSpan.FromMilliseconds(160)))
            {
                EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
            };
            this.BeginAnimation(Window.OpacityProperty, anim);
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
                
                // Fade out before hiding
                var anim = new DoubleAnimation(1.0, 0.0, new Duration(TimeSpan.FromMilliseconds(120)));
                anim.Completed += (s, ev) =>
                {
                    this.Hide();
                    this.Opacity = 1.0;
                };
                this.BeginAnimation(Window.OpacityProperty, anim);

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
            var selectedTheme = (ThemeComboBox.SelectedItem as ComboBoxItem)?.Tag?.ToString() ?? "System";
            var selectedStyle = (UiStyleComboBox.SelectedItem as ComboBoxItem)?.Tag?.ToString() ?? "ClassicRing";

            if (selectedTheme == "Custom")
            {
                try
                {
                    System.Windows.Media.ColorConverter.ConvertFromString(CustomSectorBgTextBox.Text);
                    System.Windows.Media.ColorConverter.ConvertFromString(CustomSectorBorderTextBox.Text);
                    System.Windows.Media.ColorConverter.ConvertFromString(CustomHighlightBgTextBox.Text);
                    System.Windows.Media.ColorConverter.ConvertFromString(CustomHighlightBorderTextBox.Text);
                    System.Windows.Media.ColorConverter.ConvertFromString(CustomTextTextBox.Text);
                }
                catch
                {
                    MessageBox.Show("自定义颜色值必须是有效的十六进制颜色代码 (例如: #9016161A)。", "格式错误", MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }
            }

            ConfigManager.CurrentConfig.Theme = selectedTheme;
            ConfigManager.CurrentConfig.UiStyle = selectedStyle;
            ConfigManager.CurrentConfig.CustomSectorBg = CustomSectorBgTextBox.Text;
            ConfigManager.CurrentConfig.CustomSectorBorder = CustomSectorBorderTextBox.Text;
            ConfigManager.CurrentConfig.CustomHighlightBg = CustomHighlightBgTextBox.Text;
            ConfigManager.CurrentConfig.CustomHighlightBorder = CustomHighlightBorderTextBox.Text;
            ConfigManager.CurrentConfig.CustomText = CustomTextTextBox.Text;

            // Save Scene Isolation settings
            ConfigManager.CurrentConfig.DisableOnFullScreen = DisableOnFullScreenCheckBox.IsChecked == true;
            ConfigManager.CurrentConfig.DisableOnCtrl = CtrlModifierCheckBox.IsChecked == true;
            ConfigManager.CurrentConfig.DisableOnShift = ShiftModifierCheckBox.IsChecked == true;
            ConfigManager.CurrentConfig.DisableOnAlt = AltModifierCheckBox.IsChecked == true;

            ConfigManager.CurrentConfig.BlacklistedProcesses = new List<string>();
            foreach (var item in BlacklistListBox.Items)
            {
                ConfigManager.CurrentConfig.BlacklistedProcesses.Add(item.ToString());
            }

            ConfigManager.SaveConfig();
            MessageBox.Show("配置保存成功！并在内存中即时生效。", "保存成功", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        private void DisableOnFullScreenCheckBox_Changed(object sender, RoutedEventArgs e)
        {
            if (DisableOnFullScreenCheckBox == null || ConfigManager.CurrentConfig == null) return;
            ConfigManager.CurrentConfig.DisableOnFullScreen = DisableOnFullScreenCheckBox.IsChecked == true;
        }

        private void ModifierCheckBox_Changed(object sender, RoutedEventArgs e)
        {
            if (ConfigManager.CurrentConfig == null) return;
            if (CtrlModifierCheckBox != null) ConfigManager.CurrentConfig.DisableOnCtrl = CtrlModifierCheckBox.IsChecked == true;
            if (ShiftModifierCheckBox != null) ConfigManager.CurrentConfig.DisableOnShift = ShiftModifierCheckBox.IsChecked == true;
            if (AltModifierCheckBox != null) ConfigManager.CurrentConfig.DisableOnAlt = AltModifierCheckBox.IsChecked == true;
        }

        private void AddBlacklistButton_Click(object sender, RoutedEventArgs e)
        {
            string proc = NewBlacklistProcessTextBox.Text.Trim().ToLower();
            if (string.IsNullOrEmpty(proc)) return;
            if (!proc.EndsWith(".exe") && !proc.Contains("."))
            {
                proc += ".exe";
            }
            if (BlacklistListBox.Items.Contains(proc))
            {
                MessageBox.Show("该进程已在排除名单中。", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }
            BlacklistListBox.Items.Add(proc);
            NewBlacklistProcessTextBox.Clear();
        }

        private void DeleteBlacklistButton_Click(object sender, RoutedEventArgs e)
        {
            if (BlacklistListBox.SelectedItem != null)
            {
                BlacklistListBox.Items.Remove(BlacklistListBox.SelectedItem);
            }
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
                Debug.WriteLine($"Testing action: {vm.Name}");
                ActionExecutor.Execute(vm.Action);
            }
        }

        private void SidebarMenuListBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (SidebarMenuListBox == null || GeneralSettingsGrid == null || GeometrySettingsGrid == null || MappingsSettingsGrid == null || AboutSettingsGrid == null) return;
            int index = SidebarMenuListBox.SelectedIndex;
            GeneralSettingsGrid.Visibility = index == 0 ? Visibility.Visible : Visibility.Collapsed;
            GeometrySettingsGrid.Visibility = index == 1 ? Visibility.Visible : Visibility.Collapsed;
            MappingsSettingsGrid.Visibility = index == 2 ? Visibility.Visible : Visibility.Collapsed;
            AboutSettingsGrid.Visibility = index == 3 ? Visibility.Visible : Visibility.Collapsed;
        }

        private void OpenChangelogButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                string changelogPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "..", "..", "..", "..", "CHANGELOG.md");
                if (!File.Exists(changelogPath))
                {
                    changelogPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "CHANGELOG.md");
                }
                if (File.Exists(changelogPath))
                {
                    Process.Start(new ProcessStartInfo(changelogPath) { UseShellExecute = true });
                }
                else
                {
                    MessageBox.Show("未找到本地 CHANGELOG.md 文件，请在项目根目录查阅。", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"打开更新日志失败: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void OpenReleasesFolderButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                string releasesPath = Path.GetFullPath(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "..", "..", "..", "..", "releases"));
                if (Directory.Exists(releasesPath))
                {
                    Process.Start(new ProcessStartInfo("explorer.exe", releasesPath));
                }
                else
                {
                    MessageBox.Show("发布归档目录位于项目根目录下的 releases/ 文件夹中。", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"打开归档目录失败: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void OpenAppFolderButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                Process.Start(new ProcessStartInfo("explorer.exe", AppDomain.CurrentDomain.BaseDirectory));
            }
            catch (Exception ex)
            {
                MessageBox.Show($"打开应用目录失败: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void ThemeComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (ThemeComboBox == null || CustomColorsPanel == null) return;
            var selectedItem = ThemeComboBox.SelectedItem as ComboBoxItem;
            if (selectedItem != null)
            {
                string theme = selectedItem.Tag?.ToString() ?? "System";
                ConfigManager.CurrentConfig.Theme = theme;
                CustomColorsPanel.Visibility = theme == "Custom" ? Visibility.Visible : Visibility.Collapsed;
            }
        }

        private void UiStyleComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (UiStyleComboBox == null) return;
            var selectedItem = UiStyleComboBox.SelectedItem as ComboBoxItem;
            if (selectedItem != null)
            {
                ConfigManager.CurrentConfig.UiStyle = selectedItem.Tag?.ToString() ?? "ClassicRing";
            }
        }

        private void CustomColorTextBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (ConfigManager.CurrentConfig == null) return;
            if (CustomSectorBgTextBox != null) ConfigManager.CurrentConfig.CustomSectorBg = CustomSectorBgTextBox.Text;
            if (CustomSectorBorderTextBox != null) ConfigManager.CurrentConfig.CustomSectorBorder = CustomSectorBorderTextBox.Text;
            if (CustomHighlightBgTextBox != null) ConfigManager.CurrentConfig.CustomHighlightBg = CustomHighlightBgTextBox.Text;
            if (CustomHighlightBorderTextBox != null) ConfigManager.CurrentConfig.CustomHighlightBorder = CustomHighlightBorderTextBox.Text;
            if (CustomTextTextBox != null) ConfigManager.CurrentConfig.CustomText = CustomTextTextBox.Text;
            UpdateColorPreviews();
        }

        private void UpdateColorPreviews()
        {
            UpdatePreviewBrush(CustomSectorBgTextBox?.Text, CustomSectorBgPreview);
            UpdatePreviewBrush(CustomSectorBorderTextBox?.Text, CustomSectorBorderPreview);
            UpdatePreviewBrush(CustomHighlightBgTextBox?.Text, CustomHighlightBgPreview);
            UpdatePreviewBrush(CustomHighlightBorderTextBox?.Text, CustomHighlightBorderPreview);
            UpdatePreviewBrush(CustomTextTextBox?.Text, CustomTextPreview);
        }

        private void UpdatePreviewBrush(string hex, Border previewBorder)
        {
            if (previewBorder == null) return;
            try
            {
                if (!string.IsNullOrEmpty(hex))
                {
                    var brush = new System.Windows.Media.SolidColorBrush((System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString(hex));
                    previewBorder.Background = brush;
                }
            }
            catch
            {
                // Keep existing color or do nothing
            }
        }

        private void ColorPicker_Click(object sender, RoutedEventArgs e)
        {
            var button = sender as System.Windows.Controls.Button;
            if (button == null) return;

            // Determine which textbox corresponds to this button
            System.Windows.Controls.TextBox targetTextBox = null;
            if (button == CustomSectorBgPicker) targetTextBox = CustomSectorBgTextBox;
            else if (button == CustomSectorBorderPicker) targetTextBox = CustomSectorBorderTextBox;
            else if (button == CustomHighlightBgPicker) targetTextBox = CustomHighlightBgTextBox;
            else if (button == CustomHighlightBorderPicker) targetTextBox = CustomHighlightBorderTextBox;
            else if (button == CustomTextPicker) targetTextBox = CustomTextTextBox;

            if (targetTextBox == null) return;

            using (var dialog = new System.Windows.Forms.ColorDialog())
            {
                try
                {
                    if (!string.IsNullOrEmpty(targetTextBox.Text))
                    {
                        var wpfColor = (System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString(targetTextBox.Text);
                        dialog.Color = System.Drawing.Color.FromArgb(wpfColor.A, wpfColor.R, wpfColor.G, wpfColor.B);
                    }
                }
                catch { }

                if (dialog.ShowDialog() == System.Windows.Forms.DialogResult.OK)
                {
                    targetTextBox.Text = $"#FF{dialog.Color.R:X2}{dialog.Color.G:X2}{dialog.Color.B:X2}";
                    UpdateColorPreviews();
                }
            }
        }

        private void WheelRadiusSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (WheelRadiusLabel != null)
            {
                WheelRadiusLabel.Text = e.NewValue.ToString("0");
                if (ConfigManager.CurrentConfig != null)
                {
                    ConfigManager.CurrentConfig.WheelRadius = e.NewValue;
                }
            }
        }

        private void InnerRadiusSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (InnerRadiusLabel != null)
            {
                InnerRadiusLabel.Text = e.NewValue.ToString("0");
                if (ConfigManager.CurrentConfig != null)
                {
                    ConfigManager.CurrentConfig.InnerRadius = e.NewValue;
                }
            }
        }

        private void CoreRadiusSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (CoreRadiusLabel != null)
            {
                CoreRadiusLabel.Text = e.NewValue.ToString("0");
                if (ConfigManager.CurrentConfig != null)
                {
                    ConfigManager.CurrentConfig.CoreRadius = e.NewValue;
                }
            }
        }

        private void ShapeComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (ShapeComboBox == null || ConfigManager.CurrentConfig == null) return;
            var selectedItem = ShapeComboBox.SelectedItem as ComboBoxItem;
            if (selectedItem != null)
            {
                ConfigManager.CurrentConfig.Shape = selectedItem.Tag?.ToString() ?? "Original";
            }
        }

        private void ShowTextCheckBox_Changed(object sender, RoutedEventArgs e)
        {
            if (ShowTextCheckBox == null || ConfigManager.CurrentConfig == null) return;
            ConfigManager.CurrentConfig.ShowText = ShowTextCheckBox.IsChecked == true;
        }

        private void Browse_Click(object sender, RoutedEventArgs e)
        {
            var button = sender as System.Windows.Controls.Button;
            var vm = button?.DataContext as SlotViewModel;
            if (vm == null) return;

            var picker = new ProgramPickerWindow();
            picker.Owner = this;
            if (picker.ShowDialog() == true)
            {
                vm.Parameter = picker.SelectedPath;
                
                // Automatically set Name if it is empty/default
                if (string.IsNullOrEmpty(vm.Name) || vm.Name == "未命名" || vm.Name == "快捷动作")
                {
                    vm.Name = picker.SelectedName;
                }
            }
        }

        private void SetComboBoxSelectedValue(System.Windows.Controls.ComboBox comboBox, string tagValue)
        {
            if (comboBox == null || string.IsNullOrEmpty(tagValue)) return;
            foreach (ComboBoxItem item in comboBox.Items)
            {
                if (item.Tag?.ToString() == tagValue)
                {
                    comboBox.SelectedItem = item;
                    break;
                }
            }
        }

        private bool IsRunningAsAdmin()
        {
            using (var identity = System.Security.Principal.WindowsIdentity.GetCurrent())
            {
                var principal = new System.Security.Principal.WindowsPrincipal(identity);
                return principal.IsInRole(System.Security.Principal.WindowsBuiltInRole.Administrator);
            }
        }

        private void ElevatePrivileges_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var processInfo = new ProcessStartInfo
                {
                    FileName = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "WinPieGestures.exe"),
                    UseShellExecute = true,
                    Verb = "runas"
                };
                Process.Start(processInfo);
                Application.Current.Shutdown();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"提升权限失败:\n{ex.Message}", "权限错误", MessageBoxButton.OK, MessageBoxImage.Error);
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
                NotifyAllActionProperties();
            }
        }

        public string Type
        {
            get => Action?.Type;
            set
            {
                if (Action != null && Action.Type != value)
                {
                    Action.Type = value;
                    OnPropertyChanged(nameof(Type));
                    OnPropertyChanged(nameof(IsLaunchType));
                    OnPropertyChanged(nameof(IsSystemType));
                    OnPropertyChanged(nameof(IsHotkeyType));
                    
                    if (value == "System" && string.IsNullOrEmpty(Action.Parameter))
                    {
                        Action.Parameter = "Lock"; // Default system command
                        OnPropertyChanged(nameof(Parameter));
                    }
                }
            }
        }

        public string Name
        {
            get => Action?.Name;
            set
            {
                if (Action != null && Action.Name != value)
                {
                    Action.Name = value;
                    OnPropertyChanged(nameof(Name));
                }
            }
        }

        public string Parameter
        {
            get => Action?.Parameter;
            set
            {
                if (Action != null && Action.Parameter != value)
                {
                    Action.Parameter = value;
                    OnPropertyChanged(nameof(Parameter));
                    if (IsSystemType)
                    {
                        OnPropertyChanged(nameof(SelectedSystemPreset));
                    }
                }
            }
        }

        public string Arguments
        {
            get => Action?.Arguments;
            set
            {
                if (Action != null && Action.Arguments != value)
                {
                    Action.Arguments = value;
                    OnPropertyChanged(nameof(Arguments));
                }
            }
        }

        public bool IsLaunchType => Type == "Launch";
        public bool IsSystemType => Type == "System";
        public bool IsHotkeyType => Type == "Hotkey";

        public static Dictionary<string, string> SystemPresets { get; } = new Dictionary<string, string>
        {
            { "Lock", "锁定电脑 (Lock)" },
            { "VolumeUp", "音量加 (Vol Up)" },
            { "VolumeDown", "音量减 (Vol Down)" },
            { "VolumeMute", "静音 (Mute)" },
            { "ShowDesktop", "显示桌面 (Desktop)" },
            { "Screenshot", "屏幕截图 (Capture)" }
        };

        public string SelectedSystemPreset
        {
            get => Parameter;
            set
            {
                Parameter = value;
            }
        }

        public void NotifyAllActionProperties()
        {
            OnPropertyChanged(nameof(Type));
            OnPropertyChanged(nameof(Name));
            OnPropertyChanged(nameof(Parameter));
            OnPropertyChanged(nameof(Arguments));
            OnPropertyChanged(nameof(IsLaunchType));
            OnPropertyChanged(nameof(IsSystemType));
            OnPropertyChanged(nameof(IsHotkeyType));
            OnPropertyChanged(nameof(SelectedSystemPreset));
        }

        public event PropertyChangedEventHandler PropertyChanged;

        protected void OnPropertyChanged(string name)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
        }
    }
}

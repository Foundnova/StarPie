using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media.Imaging;

namespace WinPieGestures
{
    public partial class ProgramPickerWindow : Window
    {
        public class ProgramItem
        {
            public string Name { get; set; }
            public string Path { get; set; }
            public string FriendlyPath { get; set; }
            public BitmapSource IconSource { get; set; }
        }

        private readonly List<ProgramItem> _allPrograms = new List<ProgramItem>();
        private readonly ObservableCollection<ProgramItem> _displayedPrograms = new ObservableCollection<ProgramItem>();

        public string SelectedPath { get; private set; }
        public string SelectedName { get; private set; }

        public ProgramPickerWindow()
        {
            InitializeComponent();
            AppThemeManager.ApplyTheme(this, AppThemeManager.CurrentEffectiveTheme);
            ProgramsListView.ItemsSource = _displayedPrograms;
        }

        private async void Window_Loaded(object sender, RoutedEventArgs e)
        {
            StatusTextBlock.Visibility = Visibility.Visible;
            StatusTextBlock.Text = "正在扫描系统中的软件，请稍候...";

            try
            {
                var programs = await Task.Run(() => ScanInstalledPrograms());
                _allPrograms.Clear();
                _allPrograms.AddRange(programs);

                // Update UI on UI thread
                UpdateDisplayedList("");
                StatusTextBlock.Visibility = Visibility.Collapsed;
            }
            catch (Exception ex)
            {
                StatusTextBlock.Text = $"扫描失败: {ex.Message}";
                StatusTextBlock.Foreground = System.Windows.Media.Brushes.Red;
            }
        }

        private List<ProgramItem> ScanInstalledPrograms()
        {
            var list = new List<ProgramItem>();
            var visited = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            string commonStartMenu = Environment.GetFolderPath(Environment.SpecialFolder.CommonStartMenu);
            string userStartMenu = Environment.GetFolderPath(Environment.SpecialFolder.StartMenu);

            ScanLnkFiles(Path.Combine(commonStartMenu, "Programs"), list, visited);
            ScanLnkFiles(Path.Combine(userStartMenu, "Programs"), list, visited);

            // Sort by name
            list.Sort((a, b) => string.Compare(a.Name, b.Name, StringComparison.CurrentCultureIgnoreCase));
            return list;
        }

        private void ScanLnkFiles(string dir, List<ProgramItem> list, HashSet<string> visited)
        {
            if (!Directory.Exists(dir)) return;

            try
            {
                // Scan lnk files recursively
                var files = Directory.GetFiles(dir, "*.lnk", SearchOption.AllDirectories);
                foreach (string file in files)
                {
                    string name = Path.GetFileNameWithoutExtension(file);
                    
                    // Filter out duplicate shortcuts or uninstallers
                    if (visited.Contains(name) || name.ToLower().Contains("uninstall") || name.ToLower().Contains("卸载"))
                        continue;

                    BitmapSource icon = null;
                    try
                    {
                        icon = IconHelper.GetIcon(file);
                    }
                    catch { }

                    list.Add(new ProgramItem
                    {
                        Name = name,
                        Path = file,
                        FriendlyPath = file, // Using shortcut path as the launch target is extremely stable
                        IconSource = icon
                    });
                    visited.Add(name);
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Failed to scan lnk files in {dir}: {ex.Message}");
            }
        }

        private void UpdateDisplayedList(string filter)
        {
            _displayedPrograms.Clear();
            var query = _allPrograms.AsEnumerable();

            if (!string.IsNullOrEmpty(filter))
            {
                string lowerFilter = filter.ToLower();
                query = query.Where(p => p.Name.ToLower().Contains(lowerFilter));
            }

            foreach (var item in query)
            {
                _displayedPrograms.Add(item);
            }
        }

        private void SearchTextBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            UpdateDisplayedList(SearchTextBox.Text);
        }

        private void ProgramsListView_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            SelectAndClose();
        }

        private void Ok_Click(object sender, RoutedEventArgs e)
        {
            SelectAndClose();
        }

        private void Cancel_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }

        private void ManualBrowse_Click(object sender, RoutedEventArgs e)
        {
            var openFileDialog = new Microsoft.Win32.OpenFileDialog
            {
                Filter = "可执行程序 (*.exe)|*.exe|快捷方式 (*.lnk)|*.lnk|所有文件 (*.*)|*.*",
                Title = "手动浏览程序文件"
            };

            if (openFileDialog.ShowDialog(this) == true)
            {
                SelectedPath = openFileDialog.FileName;
                SelectedName = Path.GetFileNameWithoutExtension(openFileDialog.FileName);
                DialogResult = true;
                Close();
            }
        }

        private void SelectAndClose()
        {
            var selected = ProgramsListView.SelectedItem as ProgramItem;
            if (selected != null)
            {
                SelectedPath = selected.Path;
                SelectedName = selected.Name;
                DialogResult = true;
                Close();
            }
            else
            {
                System.Windows.MessageBox.Show("请选择一个程序，或者点击“手动浏览文件...”", "未选择", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }
    }
}

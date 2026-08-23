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
            var visitedPaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var visitedNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            // 1. Built-in Windows System Tools
            AddSystemApp(list, visitedPaths, visitedNames, "文件资源管理器 (Explorer)", Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Windows), "explorer.exe"));
            AddSystemApp(list, visitedPaths, visitedNames, "记事本 (Notepad)", Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.System), "notepad.exe"));
            AddSystemApp(list, visitedPaths, visitedNames, "任务管理器 (Taskmgr)", Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.System), "taskmgr.exe"));
            AddSystemApp(list, visitedPaths, visitedNames, "计算器 (Calculator)", Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.System), "calc.exe"));
            AddSystemApp(list, visitedPaths, visitedNames, "截图工具 (SnippingTool)", Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.System), "SnippingTool.exe"));
            AddSystemApp(list, visitedPaths, visitedNames, "命令提示符 (cmd.exe)", Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.System), "cmd.exe"));
            AddSystemApp(list, visitedPaths, visitedNames, "Windows PowerShell", Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.System), "WindowsPowerShell\\v1.0\\powershell.exe"));
            AddSystemApp(list, visitedPaths, visitedNames, "画图 (MSPaint)", Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.System), "mspaint.exe"));
            AddSystemApp(list, visitedPaths, visitedNames, "注册表编辑器 (Regedit)", Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Windows), "regedit.exe"));

            // 2. Start Menu Programs (Common & User)
            string commonStartMenu = Environment.GetFolderPath(Environment.SpecialFolder.CommonStartMenu);
            string userStartMenu = Environment.GetFolderPath(Environment.SpecialFolder.StartMenu);
            ScanLnkFiles(Path.Combine(commonStartMenu, "Programs"), list, visitedPaths, visitedNames);
            ScanLnkFiles(Path.Combine(userStartMenu, "Programs"), list, visitedPaths, visitedNames);

            // 3. Desktop Shortcuts (Common & User)
            string commonDesktop = Environment.GetFolderPath(Environment.SpecialFolder.CommonDesktopDirectory);
            string userDesktop = Environment.GetFolderPath(Environment.SpecialFolder.Desktop);
            ScanLnkFiles(commonDesktop, list, visitedPaths, visitedNames);
            ScanLnkFiles(userDesktop, list, visitedPaths, visitedNames);

            // 4. Registry Registered App Paths
            ScanRegistryAppPaths(Microsoft.Win32.Registry.LocalMachine, list, visitedPaths, visitedNames);
            ScanRegistryAppPaths(Microsoft.Win32.Registry.CurrentUser, list, visitedPaths, visitedNames);

            // 5. Registry Uninstall Display Icons
            ScanRegistryUninstall(Microsoft.Win32.Registry.LocalMachine, @"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall", list, visitedPaths, visitedNames);
            ScanRegistryUninstall(Microsoft.Win32.Registry.LocalMachine, @"SOFTWARE\WOW6432Node\Microsoft\Windows\CurrentVersion\Uninstall", list, visitedPaths, visitedNames);
            ScanRegistryUninstall(Microsoft.Win32.Registry.CurrentUser, @"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall", list, visitedPaths, visitedNames);

            // Sort cleanly by name
            list.Sort((a, b) => string.Compare(a.Name, b.Name, StringComparison.CurrentCultureIgnoreCase));
            return list;
        }

        private void AddSystemApp(List<ProgramItem> list, HashSet<string> visitedPaths, HashSet<string> visitedNames, string displayName, string exePath)
        {
            if (File.Exists(exePath) && !visitedPaths.Contains(exePath))
            {
                var icon = IconHelper.GetIcon(exePath);
                list.Add(new ProgramItem
                {
                    Name = displayName,
                    Path = exePath,
                    FriendlyPath = exePath,
                    IconSource = icon!
                });
                visitedPaths.Add(exePath);
                visitedNames.Add(displayName);
            }
        }

        private void ScanLnkFiles(string dir, List<ProgramItem> list, HashSet<string> visitedPaths, HashSet<string> visitedNames)
        {
            if (!Directory.Exists(dir)) return;

            try
            {
                var files = Directory.GetFiles(dir, "*.lnk", SearchOption.AllDirectories);
                foreach (string file in files)
                {
                    string name = Path.GetFileNameWithoutExtension(file);

                    // Filter out uninstaller/helper shortcuts
                    string lowerName = name.ToLower();
                    if (lowerName.Contains("uninstall") || lowerName.Contains("卸载") ||
                        lowerName.Contains("help") || lowerName.Contains("readme") ||
                        lowerName.Contains("manual") || lowerName.Contains("使用说明") ||
                        lowerName.Contains("website") || lowerName.Contains("修复") ||
                        lowerName.Contains("crash") || lowerName.Contains("update") ||
                        lowerName.Contains("license") || lowerName.Contains("feedback") || lowerName.Contains("意见反馈"))
                        continue;

                    string targetPath = file;
                    if (IconHelper.ResolveShortcutTarget(file, out string resolvedTarget, out _, out _))
                    {
                        if (!string.IsNullOrEmpty(resolvedTarget) && File.Exists(resolvedTarget))
                        {
                            targetPath = resolvedTarget;
                        }
                    }

                    if (visitedPaths.Contains(targetPath) || visitedNames.Contains(name))
                        continue;

                    BitmapSource? icon = IconHelper.GetIcon(file);
                    if (icon == null && File.Exists(targetPath))
                    {
                        icon = IconHelper.GetIcon(targetPath);
                    }

                    list.Add(new ProgramItem
                    {
                        Name = name,
                        Path = targetPath,
                        FriendlyPath = targetPath,
                        IconSource = icon!
                    });

                    visitedPaths.Add(targetPath);
                    visitedNames.Add(name);
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Failed to scan lnk files in {dir}: {ex.Message}");
            }
        }

        private void ScanRegistryAppPaths(Microsoft.Win32.RegistryKey rootKey, List<ProgramItem> list, HashSet<string> visitedPaths, HashSet<string> visitedNames)
        {
            try
            {
                using var appPaths = rootKey.OpenSubKey(@"SOFTWARE\Microsoft\Windows\CurrentVersion\App Paths");
                if (appPaths == null) return;

                foreach (string subKeyName in appPaths.GetSubKeyNames())
                {
                    try
                    {
                        using var key = appPaths.OpenSubKey(subKeyName);
                        string? defaultVal = key?.GetValue("")?.ToString();
                        if (string.IsNullOrEmpty(defaultVal)) continue;

                        string exePath = Environment.ExpandEnvironmentVariables(defaultVal.Trim().Trim('"'));
                        if (!File.Exists(exePath)) continue;

                        string name = Path.GetFileNameWithoutExtension(exePath);
                        if (visitedPaths.Contains(exePath) || visitedNames.Contains(name))
                            continue;

                        string lowerName = name.ToLower();
                        if (lowerName.Contains("uninstall") || lowerName.Contains("unins000") || lowerName.Contains("setup") || lowerName.Contains("helper"))
                            continue;

                        var icon = IconHelper.GetIcon(exePath);
                        list.Add(new ProgramItem
                        {
                            Name = name,
                            Path = exePath,
                            FriendlyPath = exePath,
                            IconSource = icon!
                        });

                        visitedPaths.Add(exePath);
                        visitedNames.Add(name);
                    }
                    catch { }
                }
            }
            catch { }
        }

        private void ScanRegistryUninstall(Microsoft.Win32.RegistryKey rootKey, string uninstallPath, List<ProgramItem> list, HashSet<string> visitedPaths, HashSet<string> visitedNames)
        {
            try
            {
                using var uninstall = rootKey.OpenSubKey(uninstallPath);
                if (uninstall == null) return;

                foreach (string subKeyName in uninstall.GetSubKeyNames())
                {
                    try
                    {
                        using var key = uninstall.OpenSubKey(subKeyName);
                        if (key == null) continue;

                        string? displayName = key.GetValue("DisplayName")?.ToString();
                        if (string.IsNullOrEmpty(displayName)) continue;

                        string? displayIcon = key.GetValue("DisplayIcon")?.ToString();
                        string? installLocation = key.GetValue("InstallLocation")?.ToString();

                        string exePath = "";
                        if (!string.IsNullOrEmpty(displayIcon))
                        {
                            string raw = displayIcon.Split(',')[0].Trim().Trim('"');
                            string expanded = Environment.ExpandEnvironmentVariables(raw);
                            if (File.Exists(expanded) && expanded.EndsWith(".exe", StringComparison.OrdinalIgnoreCase))
                            {
                                exePath = expanded;
                            }
                        }

                        if (string.IsNullOrEmpty(exePath) && !string.IsNullOrEmpty(installLocation) && Directory.Exists(installLocation))
                        {
                            var exes = Directory.GetFiles(installLocation, "*.exe", SearchOption.TopDirectoryOnly);
                            var mainExe = exes.FirstOrDefault(e => !Path.GetFileName(e).ToLower().Contains("unins") && !Path.GetFileName(e).ToLower().Contains("setup"));
                            if (mainExe != null) exePath = mainExe;
                        }

                        if (string.IsNullOrEmpty(exePath) || !File.Exists(exePath))
                            continue;

                        if (visitedPaths.Contains(exePath) || visitedNames.Contains(displayName))
                            continue;

                        var icon = IconHelper.GetIcon(exePath);
                        list.Add(new ProgramItem
                        {
                            Name = displayName,
                            Path = exePath,
                            FriendlyPath = exePath,
                            IconSource = icon!
                        });

                        visitedPaths.Add(exePath);
                        visitedNames.Add(displayName);
                    }
                    catch { }
                }
            }
            catch { }
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

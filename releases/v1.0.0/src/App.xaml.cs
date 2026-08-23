using System;
using System.Windows;

namespace WinPieGestures
{
    public partial class App : System.Windows.Application
    {
        private MouseHook _mouseHook;
        private GestureController _gestureController;

        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);

            try
            {
                // Initialize configuration
                ConfigManager.LoadConfig();

                // Initialize mouse hook
                _mouseHook = new MouseHook();
                _mouseHook.Start();

                // Initialize gesture controller
                _gestureController = new GestureController(_mouseHook);
            }
            catch (Exception ex)
            {
                System.Windows.MessageBox.Show($"初始化 WinPieGestures 失败:\n{ex.Message}", "启动错误", MessageBoxButton.OK, MessageBoxImage.Error);
                Shutdown();
            }
        }

        protected override void OnExit(ExitEventArgs e)
        {
            // Unregister mouse hook on exit
            _mouseHook?.Stop();
            base.OnExit(e);
        }
    }
}

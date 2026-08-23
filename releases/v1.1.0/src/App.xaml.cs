using System;
using System.Windows;

namespace WinPieGestures
{
    public partial class App : System.Windows.Application
    {
        public static MouseHook? MainMouseHook { get; private set; }
        private GestureController? _gestureController;

        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);

            try
            {
                // Initialize configuration
                ConfigManager.LoadConfig();

                // Initialize mouse hook
                MainMouseHook = new MouseHook();
                MainMouseHook.Start();

                // Initialize gesture controller
                _gestureController = new GestureController(MainMouseHook);
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
            MainMouseHook?.Stop();
            base.OnExit(e);
        }
    }
}

using System;
using System.Diagnostics;
using System.Windows;
using System.Windows.Threading;

namespace WinPieGestures
{
    public partial class App : System.Windows.Application
    {
        public static MouseHook? MainMouseHook { get; private set; }
        private GestureController? _gestureController;

        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);

            // Register global unhandled exception handlers to prevent unexpected process crashes
            this.DispatcherUnhandledException += App_DispatcherUnhandledException;
            AppDomain.CurrentDomain.UnhandledException += CurrentDomain_UnhandledException;

            try
            {
                // Initialize configuration
                ConfigManager.LoadConfig();

                // Initialize mouse hook
                MainMouseHook = new MouseHook();
                MainMouseHook.Start();

                // Initialize gesture controller
                _gestureController = new GestureController(MainMouseHook);

                // Initial memory optimization after startup
                MemoryOptimizer.TrimMemory(true);
            }
            catch (Exception ex)
            {
                System.Windows.MessageBox.Show($"初始化 StarPie 失败:\n{ex.Message}", "启动错误", MessageBoxButton.OK, MessageBoxImage.Error);
                Shutdown();
            }
        }

        private void App_DispatcherUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
        {
            Debug.WriteLine($"[App Dispatcher Exception]: {e.Exception}");
            e.Handled = true; // Mark as handled to prevent app crash
        }

        private void CurrentDomain_UnhandledException(object sender, UnhandledExceptionEventArgs e)
        {
            Debug.WriteLine($"[App Domain Exception]: {e.ExceptionObject}");
        }

        protected override void OnExit(ExitEventArgs e)
        {
            // Auto-persist latest configuration on application exit
            try
            {
                ConfigManager.SaveConfig();
            }
            catch { }

            // Unregister mouse hook on exit
            MainMouseHook?.Stop();
            base.OnExit(e);
        }
    }
}

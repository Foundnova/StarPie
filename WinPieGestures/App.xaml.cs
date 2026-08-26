using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Threading;
using System.Windows;
using System.Windows.Threading;

namespace WinPieGestures
{
    public partial class App : System.Windows.Application
    {
        private static Mutex? _singleInstanceMutex;
        private static bool _isDuplicateInstance = false;
        private const string MutexName = @"Global\StarPie_SingleInstance_Mutex_9B8A7C";
        public const string WmShowStarPieMessageName = "WM_SHOW_STARPIE_SETTINGS_MESSAGE_UUID_4A2B";

        [DllImport("user32.dll", SetLastError = true, CharSet = CharSet.Auto)]
        public static extern uint RegisterWindowMessage(string lpString);

        [DllImport("user32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool PostMessage(IntPtr hWnd, uint Msg, IntPtr wParam, IntPtr lParam);

        public static readonly IntPtr HWND_BROADCAST = new IntPtr(0xffff);

        public static MouseHook? MainMouseHook { get; private set; }
        private GestureController? _gestureController;

        protected override void OnStartup(StartupEventArgs e)
        {
            // Load configuration from disk immediately BEFORE any windows or UI components instantiate
            ConfigManager.LoadConfig();

            // Allow bypassing mutex for automated test runners if explicitly specified
            string cmdLine = Environment.CommandLine;
            bool isTestMode = cmdLine.Contains("--allow-multiple", StringComparison.OrdinalIgnoreCase) ||
                              cmdLine.Contains("--test-instance", StringComparison.OrdinalIgnoreCase);

            if (!isTestMode)
            {
                bool isNewInstance;
                try
                {
                    _singleInstanceMutex = new Mutex(true, MutexName, out isNewInstance);
                }
                catch
                {
                    isNewInstance = true;
                }

                if (!isNewInstance)
                {
                    // Existing instance is running, signal it via Windows Message to cleanly restore SettingsWindow
                    try
                    {
                        uint msg = RegisterWindowMessage(WmShowStarPieMessageName);
                        if (msg != 0)
                        {
                            PostMessage(HWND_BROADCAST, msg, IntPtr.Zero, IntPtr.Zero);
                        }
                    }
                    catch { }

                    // Terminate current process immediately without initializing hooks or saving uninitialized config
                    _isDuplicateInstance = true;
                    Shutdown(0);
                    return;
                }
            }

            base.OnStartup(e);

            // Register global unhandled exception handlers to prevent unexpected process crashes
            this.DispatcherUnhandledException += App_DispatcherUnhandledException;
            AppDomain.CurrentDomain.UnhandledException += CurrentDomain_UnhandledException;

            try
            {
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
            Console.Error.WriteLine($"[App Dispatcher Exception]: {e.Exception}");
            Debug.WriteLine($"[App Dispatcher Exception]: {e.Exception}");
            e.Handled = true; // Mark as handled to prevent app crash
        }

        private void CurrentDomain_UnhandledException(object sender, UnhandledExceptionEventArgs e)
        {
            Console.Error.WriteLine($"[App Domain Exception]: {e.ExceptionObject}");
            Debug.WriteLine($"[App Domain Exception]: {e.ExceptionObject}");
        }

        protected override void OnExit(ExitEventArgs e)
        {
            if (_isDuplicateInstance)
            {
                // Never overwrite config.json from a terminated duplicate instance
                base.OnExit(e);
                return;
            }

            // Auto-persist latest configuration on application exit
            try
            {
                ConfigManager.SaveConfig();
            }
            catch { }

            // Unregister mouse hook on exit
            MainMouseHook?.Stop();

            if (_singleInstanceMutex != null)
            {
                try
                {
                    _singleInstanceMutex.ReleaseMutex();
                }
                catch { }
                _singleInstanceMutex.Dispose();
                _singleInstanceMutex = null;
            }

            base.OnExit(e);
        }
    }
}

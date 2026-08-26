using System;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Threading;

namespace WinPieGestures
{
    public partial class App : System.Windows.Application
    {
        private static Mutex? _singleInstanceMutex;
        private static EventWaitHandle? _instanceWakeEvent;
        private static RegisteredWaitHandle? _waitHandleRegistration;
        private static bool _isDuplicateInstance = false;
        
        private const string MutexName = @"Global\StarPie_SingleInstance_Mutex_9B8A7C";
        private const string WakeEventName = @"Global\StarPie_Wakeup_Event_9B8A7C";
        private const string AppId = "SoftBlack42.StarPie.App";

        [DllImport("shell32.dll", SetLastError = true)]
        private static extern void SetCurrentProcessExplicitAppUserModelID([MarshalAs(UnmanagedType.LPWStr)] string AppID);

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool SetForegroundWindow(IntPtr hWnd);

        public static MouseHook? MainMouseHook { get; private set; }
        private GestureController? _gestureController;
        public static SettingsWindow? MainSettingsWindow { get; private set; }

        protected override void OnStartup(StartupEventArgs e)
        {
            try
            {
                SetCurrentProcessExplicitAppUserModelID(AppId);
            }
            catch { }

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
                    // Existing instance is running: signal it to restore the settings window
                    try
                    {
                        using var wakeEvent = EventWaitHandle.OpenExisting(WakeEventName);
                        wakeEvent.Set();
                    }
                    catch { }

                    _isDuplicateInstance = true;
                    Shutdown(0);
                    return;
                }

                // Primary instance: create wake event
                try
                {
                    _instanceWakeEvent = new EventWaitHandle(false, EventResetMode.AutoReset, WakeEventName);
                    _waitHandleRegistration = ThreadPool.RegisterWaitForSingleObject(_instanceWakeEvent, (state, timedOut) =>
                    {
                        System.Windows.Application.Current.Dispatcher.BeginInvoke(new Action(() =>
                        {
                            WakeUpSettingsWindow();
                        }));
                    }, null, -1, false);
                }
                catch { }
            }

            base.OnStartup(e);

            // Register global unhandled exception handlers to prevent unexpected process crashes
            this.DispatcherUnhandledException += App_DispatcherUnhandledException;
            AppDomain.CurrentDomain.UnhandledException += CurrentDomain_UnhandledException;

            try
            {
                // 1. Load configuration
                ConfigManager.LoadConfig();

                // 2. Initialize mouse hook
                MainMouseHook = new MouseHook();
                MainMouseHook.Start();

                // 3. Initialize gesture controller
                _gestureController = new GestureController(MainMouseHook);

                // 4. Create and show settings window
                MainSettingsWindow = new SettingsWindow();
                this.MainWindow = MainSettingsWindow;

                bool startMinimized = cmdLine.Contains("--minimized", StringComparison.OrdinalIgnoreCase) ||
                                      cmdLine.Contains("--autostart", StringComparison.OrdinalIgnoreCase);

                if (!startMinimized)
                {
                    MainSettingsWindow.Show();
                }

                // Initial memory optimization after startup
                MemoryOptimizer.TrimMemory(true);
            }
            catch (Exception ex)
            {
                System.Windows.MessageBox.Show($"初始化 StarPie 失败:\n{ex.Message}", "启动错误", MessageBoxButton.OK, MessageBoxImage.Error);
                Shutdown();
            }
        }

        public static void WakeUpSettingsWindow()
        {
            if (MainSettingsWindow != null)
            {
                MainSettingsWindow.ShowSettings(0);
                try
                {
                    IntPtr hwnd = new WindowInteropHelper(MainSettingsWindow).Handle;
                    if (hwnd != IntPtr.Zero)
                    {
                        SetForegroundWindow(hwnd);
                    }
                }
                catch { }
            }
        }

        private void App_DispatcherUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
        {
            try
            {
                System.Diagnostics.Debug.WriteLine($"[DispatcherUnhandledException]: {e.Exception.Message}");
                // Mark handled so app doesn't crash from non-fatal UI dispatch exceptions
                e.Handled = true;
            }
            catch { }
        }

        private void CurrentDomain_UnhandledException(object sender, UnhandledExceptionEventArgs e)
        {
            try
            {
                if (e.ExceptionObject is Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"[AppDomain UnhandledException]: {ex.Message}");
                }
            }
            catch { }
        }

        protected override void OnExit(ExitEventArgs e)
        {
            if (_isDuplicateInstance)
            {
                base.OnExit(e);
                return;
            }

            try
            {
                _waitHandleRegistration?.Unregister(null);
                _instanceWakeEvent?.Dispose();
                _singleInstanceMutex?.ReleaseMutex();
                _singleInstanceMutex?.Dispose();
            }
            catch { }

            // Auto-persist latest configuration on application exit
            try
            {
                ConfigManager.SaveConfig();
            }
            catch { }

            try
            {
                MainMouseHook?.Stop();
                
            }
            catch { }

            base.OnExit(e);
        }
    }
}

using System;
using System.CodeDom.Compiler;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Threading;

namespace WinPieGestures;

public partial class App : Application
{
	private static Mutex? _singleInstanceMutex;
	private static EventWaitHandle? _instanceWakeEvent;
	private static RegisteredWaitHandle? _waitHandleRegistration;
	private static bool _isDuplicateInstance;

	private const string MutexName = "Global\\StarPie_SingleInstance_Mutex_9B8A7C";
	private const string WakeEventName = "Global\\StarPie_Wakeup_Event_9B8A7C";
	private const string AppId = "SoftBlack42.StarPie.App";

	public static GestureController? MainGestureController { get; private set; }
	public static MouseHook? MainMouseHook { get; private set; }
	public static KeyboardHook? MainKeyboardHook { get; private set; }
	public static SettingsWindow? MainSettingsWindow { get; private set; }

	[DllImport("shell32.dll", SetLastError = true)]
	private static extern void SetCurrentProcessExplicitAppUserModelID([MarshalAs(UnmanagedType.LPWStr)] string AppID);

	[DllImport("user32.dll")]
	[return: MarshalAs(UnmanagedType.Bool)]
	private static extern bool SetForegroundWindow(nint hWnd);

	protected override void OnStartup(StartupEventArgs e)
	{
		try
		{
			SetCurrentProcessExplicitAppUserModelID(AppId);
		}
		catch
		{
		}
		string commandLine = Environment.CommandLine;
		if (!commandLine.Contains("--allow-multiple", StringComparison.OrdinalIgnoreCase) && !commandLine.Contains("--test-instance", StringComparison.OrdinalIgnoreCase))
		{
			bool createdNew;
			try
			{
				_singleInstanceMutex = new Mutex(initiallyOwned: true, MutexName, out createdNew);
			}
			catch
			{
				createdNew = true;
			}
			if (!createdNew)
			{
				try
				{
					using EventWaitHandle eventWaitHandle = EventWaitHandle.OpenExisting(WakeEventName);
					eventWaitHandle.Set();
				}
				catch
				{
				}
				_isDuplicateInstance = true;
				Shutdown(0);
				return;
			}
			try
			{
				_instanceWakeEvent = new EventWaitHandle(initialState: false, EventResetMode.AutoReset, WakeEventName);
				_waitHandleRegistration = ThreadPool.RegisterWaitForSingleObject(_instanceWakeEvent, delegate
				{
					((DispatcherObject)Application.Current).Dispatcher.BeginInvoke((Delegate)(Action)delegate
					{
						WakeUpSettingsWindow();
					}, Array.Empty<object>());
				}, null, -1, executeOnlyOnce: false);
			}
			catch
			{
			}
		}
		base.OnStartup(e);
		AppLogger.LogInfo($"=== StarPie v1.5.8 Starting (OS: {Environment.OSVersion}, .NET: {Environment.Version}, 64bit: {Environment.Is64BitProcess}, Elevated: {ConfigManager.IsElevated()}) ===");
		base.DispatcherUnhandledException += new DispatcherUnhandledExceptionEventHandler(App_DispatcherUnhandledException);
		AppDomain.CurrentDomain.UnhandledException += CurrentDomain_UnhandledException;
		try
		{
			ConfigManager.LoadConfig();
			AppLogger.LogInfo("ConfigManager.LoadConfig completed");
			MainMouseHook = new MouseHook();
			MainMouseHook.Start();
			AppLogger.LogInfo("MainMouseHook started");
			MainKeyboardHook = new KeyboardHook();
			MainKeyboardHook.Start();
			AppLogger.LogInfo("MainKeyboardHook started");
			MainGestureController = new GestureController(MainMouseHook, MainKeyboardHook);
			MainSettingsWindow = new SettingsWindow();
			base.MainWindow = MainSettingsWindow;
			if (!Environment.GetCommandLineArgs().Any((string a) => string.Equals(a, "--minimized", StringComparison.OrdinalIgnoreCase) || string.Equals(a, "--autostart", StringComparison.OrdinalIgnoreCase) || string.Equals(a, "--silent", StringComparison.OrdinalIgnoreCase) || string.Equals(a, "-s", StringComparison.OrdinalIgnoreCase) || string.Equals(a, "-m", StringComparison.OrdinalIgnoreCase) || string.Equals(a, "/minimized", StringComparison.OrdinalIgnoreCase) || string.Equals(a, "/autostart", StringComparison.OrdinalIgnoreCase) || string.Equals(a, "/silent", StringComparison.OrdinalIgnoreCase)))
			{
				MainSettingsWindow.Show();
			}
			MemoryOptimizer.TrimMemory(force: true);
		}
		catch (Exception ex)
		{
			AppLogger.LogError("StarPie initialization failed", ex);
			MessageBox.Show("初始化 StarPie 失败:\n" + ex.Message, "启动错误", MessageBoxButton.OK, MessageBoxImage.Hand);
			Shutdown();
		}
	}

	public static void WakeUpSettingsWindow()
	{
		if (MainSettingsWindow == null)
		{
			return;
		}
		MainSettingsWindow.ShowSettings();
		try
		{
			nint handle = new WindowInteropHelper(MainSettingsWindow).Handle;
			if (handle != IntPtr.Zero)
			{
				SetForegroundWindow(handle);
			}
		}
		catch
		{
		}
	}

	private void App_DispatcherUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
	{
		try
		{
			AppLogger.LogError("WPF Dispatcher Unhandled Exception", e.Exception);
			e.Handled = true;
		}
		catch
		{
		}
	}

	private void CurrentDomain_UnhandledException(object sender, UnhandledExceptionEventArgs e)
	{
		try
		{
			if (e.ExceptionObject is Exception ex)
			{
				AppLogger.LogError("AppDomain Unhandled Exception", ex);
			}
			else
			{
				AppLogger.LogError($"AppDomain Unhandled Exception Object: {e.ExceptionObject}");
			}
		}
		catch
		{
		}
	}

	protected override void OnExit(ExitEventArgs e)
	{
		if (_isDuplicateInstance)
		{
			base.OnExit(e);
			return;
		}
		AppLogger.LogInfo("=== StarPie Exiting ===");
		try
		{
			_waitHandleRegistration?.Unregister(null);
			_instanceWakeEvent?.Dispose();
			_singleInstanceMutex?.ReleaseMutex();
			_singleInstanceMutex?.Dispose();
		}
		catch
		{
		}
		try
		{
			ConfigManager.SaveConfig();
		}
		catch
		{
		}
		try
		{
			MainMouseHook?.Stop();
			MainKeyboardHook?.Stop();
		}
		catch
		{
		}
		AppLogger.Shutdown();
		base.OnExit(e);
	}
}

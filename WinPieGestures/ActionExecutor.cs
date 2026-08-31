using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows;

namespace WinPieGestures;

public static class ActionExecutor
{
	private delegate bool EnumWindowsProc(nint hWnd, nint lParam);

	private struct MOUSEINPUT
	{
		public int dx;

		public int dy;

		public uint mouseData;

		public uint dwFlags;

		public uint time;

		public nint dwExtraInfo;
	}

	private struct KEYBDINPUT
	{
		public ushort wVk;

		public ushort wScan;

		public uint dwFlags;

		public uint time;

		public nint dwExtraInfo;
	}

	private struct HARDWAREINPUT
	{
		public uint uMsg;

		public ushort wParamL;

		public ushort wParamH;
	}

	[StructLayout(LayoutKind.Explicit)]
	private struct InputUnion
	{
		[FieldOffset(0)]
		public MOUSEINPUT mi;

		[FieldOffset(0)]
		public KEYBDINPUT ki;

		[FieldOffset(0)]
		public HARDWAREINPUT hi;
	}

	private struct INPUT
	{
		public uint type;

		public InputUnion U;
	}

	private class HotkeyDetails
	{
		public List<ushort> Modifiers { get; } = new List<ushort>();

		public ushort MainKey { get; set; }
	}

	private const int SW_HIDE = 0;

	private const int SW_SHOWNORMAL = 1;

	private const int SW_SHOWMINIMIZED = 2;

	private const int SW_SHOWMAXIMIZED = 3;

	private const int SW_SHOW = 5;

	private const int SW_MINIMIZE = 6;

	private const int SW_RESTORE = 9;

	private const uint INPUT_KEYBOARD = 1u;

	private const uint KEYEVENTF_KEYUP = 2u;

	private const uint KEYEVENTF_EXTENDEDKEY = 1u;

	private const ushort VK_LCONTROL = 162;

	private const ushort VK_LSHIFT = 160;

	private const ushort VK_LMENU = 164;

	private const ushort VK_LWIN = 91;

	private const ushort VK_VOLUME_MUTE = 173;

	private const ushort VK_VOLUME_DOWN = 174;

	private const ushort VK_VOLUME_UP = 175;

	private const ushort VK_LEFT = 37;

	private const ushort VK_UP = 38;

	private const ushort VK_RIGHT = 39;

	private const ushort VK_DOWN = 40;

	private const ushort VK_ESCAPE = 27;

	private const ushort VK_RETURN = 13;

	private const ushort VK_TAB = 9;

	private const ushort VK_SPACE = 32;

	[DllImport("user32.dll")]
	private static extern bool LockWorkStation();

	[DllImport("user32.dll")]
	private static extern nint GetForegroundWindow();

	[DllImport("user32.dll")]
	private static extern bool SetForegroundWindow(nint hWnd);

	[DllImport("user32.dll")]
	private static extern bool ShowWindow(nint hWnd, int nCmdShow);

	[DllImport("user32.dll")]
	private static extern bool IsIconic(nint hWnd);

	[DllImport("user32.dll")]
	private static extern bool IsWindowVisible(nint hWnd);

	[DllImport("user32.dll")]
	private static extern bool BringWindowToTop(nint hWnd);

	[DllImport("user32.dll")]
	private static extern bool EnumWindows(EnumWindowsProc enumProc, nint lParam);

	[DllImport("user32.dll", SetLastError = true)]
	private static extern uint GetWindowThreadProcessId(nint hWnd, out uint lpdwProcessId);

	[DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
	private static extern int GetWindowText(nint hWnd, StringBuilder lpString, int nMaxCount);

	[DllImport("user32.dll", SetLastError = true)]
	private static extern uint SendInput(uint nInputs, INPUT[] pInputs, int cbSize);

	public static void Execute(ActionItem action)
	{
		if (action == null)
		{
			return;
		}
		try
		{
			switch (action.Type.Trim())
			{
			case "Launch":
				ExecuteLaunch(action.Parameter, action.Arguments);
				break;
			case "Folder":
			case "OpenFolder":
				ExecuteFolder(action.Parameter);
				break;
			case "Hotkey":
				ExecuteHotkey(action.Parameter);
				break;
			case "System":
				ExecuteSystem(action.Parameter);
				break;
			}
		}
		catch (Exception ex)
		{
			MessageBox.Show("Failed to execute action '" + action.Name + "': " + ex.Message, "Error", MessageBoxButton.OK, MessageBoxImage.Hand);
		}
	}

	public static bool TryToggleProcessWindow(string processOrExePath)
	{
		if (string.IsNullOrWhiteSpace(processOrExePath))
		{
			return false;
		}
		string text = Path.GetFileNameWithoutExtension(processOrExePath).ToLower();
		Process[] processesByName = Process.GetProcessesByName(text);
		if ((processesByName == null || processesByName.Length == 0) && text.EndsWith("64"))
		{
			processesByName = Process.GetProcessesByName(text.Substring(0, text.Length - 2));
		}
		if (processesByName == null || processesByName.Length == 0)
		{
			return false;
		}
		nint foregroundWindow = GetForegroundWindow();
		List<nint> windowHandles = new List<nint>();
		Process[] array = processesByName;
		foreach (Process process in array)
		{
			try
			{
				if (process.MainWindowHandle != IntPtr.Zero && IsWindowVisible(process.MainWindowHandle))
				{
					windowHandles.Add(process.MainWindowHandle);
					continue;
				}
				int pid = process.Id;
				EnumWindows(delegate(nint hWnd, nint lParam)
				{
					GetWindowThreadProcessId(hWnd, out var lpdwProcessId);
					if (lpdwProcessId == pid && IsWindowVisible(hWnd))
					{
						StringBuilder stringBuilder = new StringBuilder(256);
						GetWindowText(hWnd, stringBuilder, 256);
						if (stringBuilder.Length > 0)
						{
							windowHandles.Add(hWnd);
						}
					}
					return true;
				}, IntPtr.Zero);
			}
			catch
			{
			}
		}
		if (windowHandles.Count == 0)
		{
			return false;
		}
		foreach (nint item in windowHandles)
		{
			if (item == foregroundWindow && !IsIconic(item))
			{
				ShowWindow(item, 6);
				return true;
			}
		}
		nint num = windowHandles[0];
		if (IsIconic(num))
		{
			ShowWindow(num, 9);
		}
		else
		{
			ShowWindow(num, 5);
		}
		SetForegroundWindow(num);
		BringWindowToTop(num);
		return true;
	}

	public static bool TryToggleFolderWindow(string folderPath)
	{
		if (string.IsNullOrWhiteSpace(folderPath))
		{
			return false;
		}
		string b = folderPath.Trim().Trim('"').TrimEnd('\\', '/');
		try
		{
			Type typeFromProgID = Type.GetTypeFromProgID("Shell.Application");
			if (typeFromProgID != null)
			{
				dynamic val = Activator.CreateInstance(typeFromProgID);
				if (val != null)
				{
					dynamic val2 = val.Windows();
					int num = val2.Count;
					nint foregroundWindow = GetForegroundWindow();
					for (int i = 0; i < num; i++)
					{
						try
						{
							dynamic val3 = val2.Item(i);
							if (!((val3 != null) ? true : false))
							{
								continue;
							}
							string text = val3.LocationURL?.ToString() ?? "";
							if (string.IsNullOrEmpty(text) || !text.StartsWith("file:///", StringComparison.OrdinalIgnoreCase) || !string.Equals(Uri.UnescapeDataString(new Uri(text).LocalPath).TrimEnd('\\', '/'), b, StringComparison.OrdinalIgnoreCase))
							{
								continue;
							}
							nint num2 = (nint)val3.HWND;
							if (num2 == IntPtr.Zero)
							{
								continue;
							}
							if (num2 == foregroundWindow && !IsIconic(num2))
							{
								ShowWindow(num2, 6);
							}
							else
							{
								if (IsIconic(num2))
								{
									ShowWindow(num2, 9);
								}
								else
								{
									ShowWindow(num2, 5);
								}
								SetForegroundWindow(num2);
								BringWindowToTop(num2);
							}
							return true;
						}
						catch
						{
						}
					}
				}
			}
		}
		catch
		{
		}
		return false;
	}

	private static void ExecuteFolder(string folderPath)
	{
		if (string.IsNullOrWhiteSpace(folderPath))
		{
			return;
		}
		try
		{
			string text = Environment.ExpandEnvironmentVariables(folderPath.Trim().Trim('"'));
			if (!TryToggleFolderWindow(text))
			{
				if (Directory.Exists(text))
				{
					Process.Start(new ProcessStartInfo
					{
						FileName = "explorer.exe",
						Arguments = "\"" + text + "\"",
						UseShellExecute = true
					});
				}
				else if (File.Exists(text))
				{
					Process.Start(new ProcessStartInfo
					{
						FileName = "explorer.exe",
						Arguments = "/select,\"" + text + "\"",
						UseShellExecute = true
					});
				}
				else
				{
					Process.Start(new ProcessStartInfo
					{
						FileName = text,
						UseShellExecute = true
					});
				}
			}
		}
		catch (Exception ex)
		{
			MessageBox.Show("无法打开文件夹 '" + folderPath + "':\n" + ex.Message, "StarPie", MessageBoxButton.OK, MessageBoxImage.Exclamation);
		}
	}

	private static void ExecuteLaunch(string path, string arguments)
	{
		if (string.IsNullOrWhiteSpace(path))
		{
			return;
		}
		string text = Environment.ExpandEnvironmentVariables(path.Trim().Trim('"'));
		if (text.StartsWith("shell:AppsFolder", StringComparison.OrdinalIgnoreCase) || (text.Contains("!") && !text.Contains(":\\") && !text.EndsWith(".exe", StringComparison.OrdinalIgnoreCase)))
		{
			string arguments2 = (text.StartsWith("shell:AppsFolder", StringComparison.OrdinalIgnoreCase) ? text : ("shell:AppsFolder\\" + text));
			Process.Start(new ProcessStartInfo
			{
				FileName = "explorer.exe",
				Arguments = arguments2,
				UseShellExecute = true
			});
		}
		else
		{
			if (string.IsNullOrWhiteSpace(arguments) && TryToggleProcessWindow(text))
			{
				return;
			}
			ProcessStartInfo processStartInfo = new ProcessStartInfo
			{
				FileName = text,
				Arguments = (arguments ?? string.Empty),
				UseShellExecute = true
			};
			try
			{
				if (File.Exists(text))
				{
					string directoryName = Path.GetDirectoryName(text);
					if (!string.IsNullOrEmpty(directoryName) && Directory.Exists(directoryName))
					{
						processStartInfo.WorkingDirectory = directoryName;
					}
				}
				else if (Directory.Exists(text))
				{
					processStartInfo.WorkingDirectory = text;
				}
			}
			catch
			{
			}
			Process.Start(processStartInfo);
		}
	}

	private static void ExecuteHotkey(string hotkeyString)
	{
		if (string.IsNullOrEmpty(hotkeyString))
		{
			return;
		}
		HotkeyDetails hotkeyDetails = ParseHotkey(hotkeyString);
		if (hotkeyDetails.Modifiers.Count == 0 && hotkeyDetails.MainKey == 0)
		{
			return;
		}
		List<INPUT> list = new List<INPUT>();
		foreach (ushort modifier in hotkeyDetails.Modifiers)
		{
			list.Add(CreateKeyInput(modifier, down: true));
		}
		if (hotkeyDetails.MainKey != 0)
		{
			list.Add(CreateKeyInput(hotkeyDetails.MainKey, down: true));
		}
		if (hotkeyDetails.MainKey != 0)
		{
			list.Add(CreateKeyInput(hotkeyDetails.MainKey, down: false));
		}
		for (int num = hotkeyDetails.Modifiers.Count - 1; num >= 0; num--)
		{
			list.Add(CreateKeyInput(hotkeyDetails.Modifiers[num], down: false));
		}
		SendInput((uint)list.Count, list.ToArray(), Marshal.SizeOf(typeof(INPUT)));
	}

	private static void ExecuteSystem(string presetName)
	{
		if (string.IsNullOrEmpty(presetName))
		{
			return;
		}
		string text = presetName.Trim().ToLower();
		if (text == null)
		{
			return;
		}
		switch (text.Length)
		{
		case 11:
			switch (text[0])
			{
			case 'c':
				if (text == "closewindow")
				{
					ExecuteHotkey("Alt+F4");
				}
				break;
			case 'p':
				if (text == "prevdesktop")
				{
					ExecuteHotkey("Win+Ctrl+Left");
				}
				break;
			case 'n':
				if (text == "nextdesktop")
				{
					ExecuteHotkey("Win+Ctrl+Right");
				}
				break;
			case 's':
				if (text == "showdesktop")
				{
					ExecuteHotkey("Win+D");
				}
				break;
			case 't':
				if (!(text == "taskmanager") || TryToggleProcessWindow("taskmgr"))
				{
					break;
				}
				try
				{
					Process.Start(new ProcessStartInfo
					{
						FileName = "taskmgr.exe",
						UseShellExecute = true
					});
					break;
				}
				catch
				{
					ExecuteHotkey("Ctrl+Shift+Esc");
					break;
				}
			case 'h':
				if (text == "hardrefresh")
				{
					ExecuteHotkey("Ctrl+F5");
				}
				break;
			}
			break;
		case 8:
			switch (text[2])
			{
			case 'n':
				if (text == "minimize")
				{
					ExecuteHotkey("Win+Down");
				}
				break;
			case 'x':
				if (text == "maximize")
				{
					ExecuteHotkey("Win+Up");
				}
				break;
			case 'a':
				if (text == "snapleft")
				{
					ExecuteHotkey("Win+Left");
				}
				break;
			case 's':
				if (text == "taskview")
				{
					ExecuteHotkey("Win+Tab");
				}
				break;
			case 'p':
				if (!(text == "explorer"))
				{
					break;
				}
				try
				{
					Process.Start(new ProcessStartInfo
					{
						FileName = "explorer.exe",
						UseShellExecute = true
					});
					break;
				}
				catch
				{
					ExecuteHotkey("Win+E");
					break;
				}
			case 't':
				if (!(text == "settings") || TryToggleProcessWindow("SystemSettings"))
				{
					break;
				}
				try
				{
					Process.Start(new ProcessStartInfo
					{
						FileName = "ms-settings:",
						UseShellExecute = true
					});
					break;
				}
				catch
				{
					ExecuteHotkey("Win+I");
					break;
				}
			case 'l':
				if (text == "volumeup")
				{
					SimulateSingleKey(175);
				}
				break;
			case 'o':
				if (text == "closetab")
				{
					ExecuteHotkey("Ctrl+W");
				}
				break;
			case 'u':
				if (!(text == "shutdown"))
				{
					break;
				}
				try
				{
					Process.Start(new ProcessStartInfo
					{
						FileName = "shutdown.exe",
						Arguments = "/s /t 0",
						UseShellExecute = true
					});
					break;
				}
				catch
				{
					break;
				}
			}
			break;
		case 9:
			switch (text[1])
			{
			case 'n':
				if (text == "snapright")
				{
					ExecuteHotkey("Win+Right");
				}
				break;
			case 'u':
				if (text == "rundialog")
				{
					ExecuteHotkey("Win+R");
				}
				break;
			case 'l':
				if (text == "playpause")
				{
					SimulateSingleKey(179);
				}
				break;
			case 'e':
				if (!(text == "nexttrack"))
				{
					if (text == "reopentab")
					{
						ExecuteHotkey("Ctrl+Shift+T");
					}
				}
				else
				{
					SimulateSingleKey(176);
				}
				break;
			case 'r':
				if (text == "prevtrack")
				{
					SimulateSingleKey(177);
				}
				break;
			case 't':
				if (text == "stopmedia")
				{
					SimulateSingleKey(178);
				}
				break;
			case 'o':
				if (text == "zoomreset")
				{
					ExecuteHotkey("Ctrl+0");
				}
				break;
			}
			break;
		case 10:
			switch (text[6])
			{
			case 'r':
				if (text == "fullscreen")
				{
					ExecuteHotkey("F11");
				}
				break;
			case 's':
				if (text == "screenshot")
				{
					ExecuteHotkey("Win+Shift+S");
				}
				break;
			case 'a':
				if (!(text == "calculator") || TryToggleProcessWindow("calc") || TryToggleProcessWindow("CalculatorApp") || TryToggleProcessWindow("Calculator"))
				{
					break;
				}
				try
				{
					Process.Start(new ProcessStartInfo
					{
						FileName = "calc.exe",
						UseShellExecute = true
					});
					break;
				}
				catch
				{
					ExecuteHotkey("Win+R");
					break;
				}
			case 'd':
				if (text == "volumedown")
				{
					SimulateSingleKey(174);
				}
				break;
			case 'm':
				if (text == "volumemute")
				{
					SimulateSingleKey(173);
				}
				break;
			}
			break;
		case 6:
			switch (text[0])
			{
			case 'n':
				if (text == "newtab")
				{
					ExecuteHotkey("Ctrl+T");
				}
				break;
			case 'z':
				if (text == "zoomin")
				{
					ExecuteHotkey("Ctrl+Plus");
				}
				break;
			}
			break;
		case 7:
			switch (text[2])
			{
			case 'f':
				if (text == "refresh")
				{
					ExecuteHotkey("F5");
				}
				break;
			case 'o':
				if (text == "zoomout")
				{
					ExecuteHotkey("Ctrl+Minus");
				}
				break;
			case 's':
				if (!(text == "restart"))
				{
					break;
				}
				try
				{
					Process.Start(new ProcessStartInfo
					{
						FileName = "shutdown.exe",
						Arguments = "/r /t 0",
						UseShellExecute = true
					});
					break;
				}
				catch
				{
					break;
				}
			}
			break;
		case 13:
			if (text == "windowssearch")
			{
				ExecuteHotkey("Win+S");
			}
			break;
		case 16:
			if (text == "clipboardhistory")
			{
				ExecuteHotkey("Win+V");
			}
			break;
		case 4:
			if (text == "lock")
			{
				LockWorkStation();
			}
			break;
		case 5:
			if (!(text == "sleep"))
			{
				break;
			}
			try
			{
				Process.Start(new ProcessStartInfo
				{
					FileName = "rundll32.exe",
					Arguments = "powrprof.dll,SetSuspendState 0,1,0",
					UseShellExecute = true
				});
				break;
			}
			catch
			{
				break;
			}
		case 12:
		case 14:
		case 15:
			break;
		}
	}

	private static void SimulateSingleKey(ushort vk)
	{
		INPUT[] pInputs = new INPUT[2]
		{
			CreateKeyInput(vk, down: true),
			CreateKeyInput(vk, down: false)
		};
		SendInput(2u, pInputs, Marshal.SizeOf(typeof(INPUT)));
	}

	private static INPUT CreateKeyInput(ushort vk, bool down)
	{
		INPUT result = new INPUT
		{
			type = 1u
		};
		result.U.ki = new KEYBDINPUT
		{
			wVk = vk,
			wScan = 0,
			dwFlags = ((!down) ? 2u : 0u),
			time = 0u,
			dwExtraInfo = IntPtr.Zero
		};
		if ((vk >= 33 && vk <= 47) || (vk >= 91 && vk <= 92) || (vk >= 173 && vk <= 179) || (vk >= 166 && vk <= 172))
		{
			result.U.ki.dwFlags |= 1u;
		}
		return result;
	}

	private static HotkeyDetails ParseHotkey(string hotkeyString)
	{
		HotkeyDetails hotkeyDetails = new HotkeyDetails();
		string[] array = hotkeyString.Split(new char[2] { '+', ' ' }, StringSplitOptions.RemoveEmptyEntries);
		for (int i = 0; i < array.Length; i++)
		{
			string text = array[i].Trim().ToLower();
			switch (text)
			{
			case "ctrl":
			case "control":
			case "lctrl":
			case "rctrl":
				if (!hotkeyDetails.Modifiers.Contains(162))
				{
					hotkeyDetails.Modifiers.Add(162);
				}
				continue;
			case "shift":
			case "lshift":
			case "rshift":
				if (!hotkeyDetails.Modifiers.Contains(160))
				{
					hotkeyDetails.Modifiers.Add(160);
				}
				continue;
			case "alt":
			case "menu":
			case "lalt":
			case "ralt":
				if (!hotkeyDetails.Modifiers.Contains(164))
				{
					hotkeyDetails.Modifiers.Add(164);
				}
				continue;
			case "win":
			case "lwin":
			case "rwin":
			case "windows":
				if (!hotkeyDetails.Modifiers.Contains(91))
				{
					hotkeyDetails.Modifiers.Add(91);
				}
				continue;
			}
			ushort num = MapKeyStringToVk(text);
			if (num != 0)
			{
				hotkeyDetails.MainKey = num;
			}
		}
		return hotkeyDetails;
	}

	private static ushort MapKeyStringToVk(string keyToken)
	{
		if (string.IsNullOrEmpty(keyToken))
		{
			return 0;
		}
		string text = keyToken.ToLower().Trim();
		if (text.Length == 1)
		{
			char c = text[0];
			if (c >= 'a' && c <= 'z')
			{
				return (ushort)(65 + (c - 97));
			}
			if (c >= '0' && c <= '9')
			{
				return c;
			}
			switch (c)
			{
			case ';':
				return 186;
			case '+':
			case '=':
				return 187;
			case ',':
				return 188;
			case '-':
				return 189;
			case '.':
				return 190;
			case '/':
				return 191;
			case '`':
				return 192;
			case '[':
				return 219;
			case '\\':
				return 220;
			case ']':
				return 221;
			case '\'':
				return 222;
			}
		}
		if (text.StartsWith("f") && int.TryParse(text.Substring(1), out var result) && result >= 1 && result <= 24)
		{
			return (ushort)(112 + (result - 1));
		}
		if (text.StartsWith("num") || text.StartsWith("numpad"))
		{
			string text2 = text.Replace("numpad", "").Replace("num", "");
			if (int.TryParse(text2, out var result2) && result2 >= 0 && result2 <= 9)
			{
				return (ushort)(96 + result2);
			}
			switch (text2)
			{
			case "add":
			case "plus":
			case "+":
				return 107;
			case "subtract":
			case "minus":
			case "-":
				return 109;
			case "multiply":
			case "star":
			case "*":
				return 106;
			case "divide":
			case "slash":
			case "/":
				return 111;
			case "decimal":
			case "dot":
			case ".":
				return 110;
			}
		}
		switch (text)
		{
		case "left":
			return 37;
		case "up":
			return 38;
		case "right":
			return 39;
		case "down":
			return 40;
		case "home":
			return 36;
		case "end":
			return 35;
		case "pgup":
		case "prior":
		case "pageup":
			return 33;
		case "pgdn":
		case "next":
		case "pagedown":
			return 34;
		case "ins":
		case "insert":
			return 45;
		case "del":
		case "delete":
			return 46;
		case "back":
		case "backspace":
			return 8;
		case "tab":
			return 9;
		case "enter":
		case "return":
			return 13;
		case "esc":
		case "escape":
			return 27;
		case "space":
		case "spacebar":
			return 32;
		case "prtscn":
		case "snapshot":
		case "printscreen":
			return 44;
		case "pause":
			return 19;
		case "capslock":
			return 20;
		case "scrolllock":
			return 145;
		case "numlock":
			return 144;
		case "plus":
			return 187;
		case "minus":
			return 189;
		case "comma":
			return 188;
		case "dot":
		case "period":
			return 190;
		case "slash":
			return 191;
		case "backslash":
			return 220;
		case "semicolon":
			return 186;
		case "quote":
			return 222;
		case "bracketleft":
		case "openbracket":
			return 219;
		case "bracketright":
		case "closebracket":
			return 221;
		case "tilde":
		case "backquote":
			return 192;
		case "volumeup":
			return 175;
		case "volumedown":
			return 174;
		case "mute":
		case "volumemute":
			return 173;
		case "playpause":
		case "mediaplaypause":
			return 179;
		case "nexttrack":
		case "medianext":
			return 176;
		case "mediaprev":
		case "prevtrack":
			return 177;
		case "stopmedia":
		case "mediastop":
			return 178;
		case "browserback":
			return 166;
		case "browserforward":
			return 167;
		case "browserrefresh":
			return 168;
		case "browserstop":
			return 169;
		case "browsersearch":
			return 170;
		case "browserfavorites":
			return 171;
		case "browserhome":
			return 172;
		default:
			return 0;
		}
	}
}

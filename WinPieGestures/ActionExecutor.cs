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

	
	[DllImport("user32.dll")]
	private static extern uint MapVirtualKey(uint uCode, uint uMapType);

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
			case "Command":
				ExecuteCommand(action.Parameter, action.CommandTerminal);
				break;
			case "Text":
			case "String":
				SendTextInput(action.Parameter);
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

	/// <summary>Runs a command in the selected terminal (cmd / PowerShell / WSL), with or without a window.</summary>
	private static void ExecuteCommand(string command, string? terminal)
	{
		if (string.IsNullOrWhiteSpace(command))
		{
			return;
		}
		string term = string.IsNullOrEmpty(terminal) ? "cmd" : terminal.Trim().ToLowerInvariant();
		bool hidden = term.EndsWith("_hidden", StringComparison.OrdinalIgnoreCase);
		string shell = hidden ? term.Substring(0, term.Length - "_hidden".Length) : term;
		// Escape embedded quotes for the cmd/PowerShell wrappers ("" is the escape inside Windows quoting)
		string quoted = command.Replace("\"", "\"\"");
		try
		{
			switch (shell)
			{
			case "powershell":
				// Visible: keep the window open (-NoExit). Hidden: run to completion.
				Process.Start(new ProcessStartInfo("powershell.exe", (hidden ? "-NoProfile -Command \"" : "-NoProfile -NoExit -Command \"") + quoted + "\"")
				{
					UseShellExecute = false,
					CreateNoWindow = hidden
				});
				break;
			case "wsl":
				// WSL receives the raw command after "--"; no extra quoting needed
				Process.Start(new ProcessStartInfo("wsl.exe", "-- " + command)
				{
					UseShellExecute = false,
					CreateNoWindow = hidden
				});
				break;
			default:
				// Visible: keep the window open (/k). Hidden: /c so no lingering process.
				Process.Start(new ProcessStartInfo("cmd.exe", (hidden ? "/c \"" : "/k \"") + quoted + "\"")
				{
					UseShellExecute = false,
					CreateNoWindow = hidden
				});
				break;
			}
		}
		catch (Exception ex)
		{
			MessageBox.Show("Failed to run command: " + ex.Message, "StarPie", MessageBoxButton.OK, MessageBoxImage.Hand);
		}
	}

	private static bool IsStandardKeyToken(string token)
	{
		string t = token.Trim().ToLowerInvariant();
		return t == "ctrl" || t == "shift" || t == "alt" || t == "win" ||
		       t == "tab" || t == "enter" || t == "esc" || t == "space" ||
		       t == "backspace" || t == "delete" || t == "insert" ||
		       (t.StartsWith("f") && int.TryParse(t.Substring(1), out _)) ||
		       (t.StartsWith("num"));
	}

	private static void ExecuteHotkey(string hotkeyString)
	{
		if (string.IsNullOrWhiteSpace(hotkeyString))
		{
			return;
		}

		if (!hotkeyString.Contains('+') && hotkeyString.Length > 1 && !IsStandardKeyToken(hotkeyString))
		{
			SendTextInput(hotkeyString);
			return;
		}

		HotkeyDetails hotkeyDetails = ParseHotkey(hotkeyString);
		if (hotkeyDetails.Modifiers.Count == 0 && hotkeyDetails.MainKey == 0)
		{
			SendTextInput(hotkeyString);
			return;
		}

		// 1. If pure modifier combo (e.g. Shift + Alt, Ctrl + Shift)
		if (hotkeyDetails.MainKey == 0 && hotkeyDetails.Modifiers.Count > 0)
		{
			List<INPUT> modDowns = new List<INPUT>();
			foreach (ushort mod in hotkeyDetails.Modifiers)
			{
				modDowns.Add(CreateKeyInput(mod, down: true));
			}
			SendInput((uint)modDowns.Count, modDowns.ToArray(), Marshal.SizeOf(typeof(INPUT)));
			System.Threading.Thread.Sleep(20);
			List<INPUT> modUps = new List<INPUT>();
			for (int i = hotkeyDetails.Modifiers.Count - 1; i >= 0; i--)
			{
				modUps.Add(CreateKeyInput(hotkeyDetails.Modifiers[i], down: false));
			}
			SendInput((uint)modUps.Count, modUps.ToArray(), Marshal.SizeOf(typeof(INPUT)));
			return;
		}

		// 2. Standard Modifier + Main Key combo
		List<INPUT> downInputs = new List<INPUT>();
		foreach (ushort modifier in hotkeyDetails.Modifiers)
		{
			downInputs.Add(CreateKeyInput(modifier, down: true));
		}
		if (downInputs.Count > 0)
		{
			SendInput((uint)downInputs.Count, downInputs.ToArray(), Marshal.SizeOf(typeof(INPUT)));
			System.Threading.Thread.Sleep(15);
		}

		if (hotkeyDetails.MainKey != 0)
		{
			INPUT[] keySeq = new INPUT[2]
			{
				CreateKeyInput(hotkeyDetails.MainKey, down: true),
				CreateKeyInput(hotkeyDetails.MainKey, down: false)
			};
			SendInput(1u, new INPUT[] { keySeq[0] }, Marshal.SizeOf(typeof(INPUT)));
			System.Threading.Thread.Sleep(20);
			SendInput(1u, new INPUT[] { keySeq[1] }, Marshal.SizeOf(typeof(INPUT)));
			System.Threading.Thread.Sleep(15);
		}

		List<INPUT> upInputs = new List<INPUT>();
		for (int num = hotkeyDetails.Modifiers.Count - 1; num >= 0; num--)
		{
			upInputs.Add(CreateKeyInput(hotkeyDetails.Modifiers[num], down: false));
		}
		if (upInputs.Count > 0)
		{
			SendInput((uint)upInputs.Count, upInputs.ToArray(), Marshal.SizeOf(typeof(INPUT)));
		}
	}

	private static void ExecuteSystem(string presetName)
	{
		if (string.IsNullOrEmpty(presetName))
		{
			return;
		}
		string text = presetName.Trim().ToLowerInvariant();

		switch (text)
		{
		case "windowswitcher":
		case "taskswitcher":
		case "alttabsticky":
			ExecuteHotkey("Ctrl+Alt+Tab");
			break;
		case "alttab":
		case "switchwindow":
			ExecuteHotkey("Alt+Tab");
			break;
		case "closewindow":
			ExecuteHotkey("Alt+F4");
			break;
		case "minimize":
			ExecuteHotkey("Win+Down");
			break;
		case "maximize":
			ExecuteHotkey("Win+Up");
			break;
		case "snapleft":
			ExecuteHotkey("Win+Left");
			break;
		case "snapright":
			ExecuteHotkey("Win+Right");
			break;
		case "taskview":
			ExecuteHotkey("Win+Tab");
			break;
		case "prevdesktop":
			ExecuteHotkey("Win+Ctrl+Left");
			break;
		case "nextdesktop":
			ExecuteHotkey("Win+Ctrl+Right");
			break;
		case "showdesktop":
			ExecuteHotkey("Win+D");
			break;
		case "fullscreen":
			ExecuteHotkey("F11");
			break;
		case "screenshot":
			ExecuteHotkey("Win+Shift+S");
			break;
		case "taskmanager":
			if (!TryToggleProcessWindow("taskmgr"))
			{
				try
				{
					Process.Start(new ProcessStartInfo
					{
						FileName = "taskmgr.exe",
						UseShellExecute = true
					});
				}
				catch
				{
					ExecuteHotkey("Ctrl+Shift+Esc");
				}
			}
			break;
		case "explorer":
			try
			{
				Process.Start(new ProcessStartInfo
				{
					FileName = "explorer.exe",
					UseShellExecute = true
				});
			}
			catch
			{
				ExecuteHotkey("Win+E");
			}
			break;
		case "settings":
			if (!TryToggleProcessWindow("SystemSettings"))
			{
				try
				{
					Process.Start(new ProcessStartInfo
					{
						FileName = "ms-settings:",
						UseShellExecute = true
					});
				}
				catch
				{
					ExecuteHotkey("Win+I");
				}
			}
			break;
		case "calculator":
			if (!TryToggleProcessWindow("calc") && !TryToggleProcessWindow("CalculatorApp") && !TryToggleProcessWindow("Calculator"))
			{
				try
				{
					Process.Start(new ProcessStartInfo
					{
						FileName = "calc.exe",
						UseShellExecute = true
					});
				}
				catch
				{
					ExecuteHotkey("Win+R");
				}
			}
			break;
		case "rundialog":
			ExecuteHotkey("Win+R");
			break;
		case "windowssearch":
			ExecuteHotkey("Win+S");
			break;
		case "clipboardhistory":
			ExecuteHotkey("Win+V");
			break;
		case "lock":
			LockWorkStation();
			break;
		case "volumeup":
			SimulateSingleKey(175);
			break;
		case "volumedown":
			SimulateSingleKey(174);
			break;
		case "volumemute":
			SimulateSingleKey(173);
			break;
		case "playpause":
			SimulateSingleKey(179);
			break;
		case "nexttrack":
			SimulateSingleKey(176);
			break;
		case "prevtrack":
			SimulateSingleKey(177);
			break;
		case "stopmedia":
			SimulateSingleKey(178);
			break;
		case "newtab":
			ExecuteHotkey("Ctrl+T");
			break;
		case "closetab":
			ExecuteHotkey("Ctrl+W");
			break;
		case "reopentab":
			ExecuteHotkey("Ctrl+Shift+T");
			break;
		case "refresh":
			ExecuteHotkey("F5");
			break;
		case "hardrefresh":
			ExecuteHotkey("Ctrl+F5");
			break;
		case "zoomin":
			ExecuteHotkey("Ctrl+Plus");
			break;
		case "zoomout":
			ExecuteHotkey("Ctrl+Minus");
			break;
		case "zoomreset":
			ExecuteHotkey("Ctrl+0");
			break;
		case "sleep":
			try
			{
				Process.Start(new ProcessStartInfo
				{
					FileName = "rundll32.exe",
					Arguments = "powrprof.dll,SetSuspendState 0,1,0",
					UseShellExecute = true
				});
			}
			catch { }
			break;
		case "restart":
			try
			{
				Process.Start(new ProcessStartInfo
				{
					FileName = "shutdown.exe",
					Arguments = "/r /t 0",
					UseShellExecute = true
				});
			}
			catch { }
			break;
		case "shutdown":
			try
			{
				Process.Start(new ProcessStartInfo
				{
					FileName = "shutdown.exe",
					Arguments = "/s /t 0",
					UseShellExecute = true
				});
			}
			catch { }
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

	public static void SendTextInput(string text)
	{
		if (string.IsNullOrEmpty(text)) return;
		List<INPUT> inputs = new List<INPUT>();
		foreach (char c in text)
		{
			INPUT down = new INPUT { type = 1u };
			down.U.ki = new KEYBDINPUT
			{
				wVk = 0,
				wScan = (ushort)c,
				dwFlags = 4u, // KEYEVENTF_UNICODE
				time = 0u,
				dwExtraInfo = IntPtr.Zero
			};
			INPUT up = new INPUT { type = 1u };
			up.U.ki = new KEYBDINPUT
			{
				wVk = 0,
				wScan = (ushort)c,
				dwFlags = 4u | 2u, // KEYEVENTF_UNICODE | KEYEVENTF_KEYUP
				time = 0u,
				dwExtraInfo = IntPtr.Zero
			};
			inputs.Add(down);
			inputs.Add(up);
		}
		SendInput((uint)inputs.Count, inputs.ToArray(), Marshal.SizeOf(typeof(INPUT)));
	}

	private static INPUT CreateKeyInput(ushort vk, bool down)
	{
		INPUT result = new INPUT
		{
			type = 1u
		};
		ushort scan = (ushort)MapVirtualKey((uint)vk, 0u);
		result.U.ki = new KEYBDINPUT
		{
			wVk = vk,
			wScan = scan,
			dwFlags = ((!down) ? 2u : 0u),
			time = 0u,
			dwExtraInfo = IntPtr.Zero
		};
		if (vk == 33 || vk == 34 || vk == 35 || vk == 36 ||
		    vk == 37 || vk == 38 || vk == 39 || vk == 40 ||
		    vk == 45 || vk == 46 ||
		    vk == 91 || vk == 92 ||
		    vk == 111 ||
		    (vk >= 166 && vk <= 179))
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
		if (text.StartsWith("d") && text.Length == 2 && char.IsDigit(text[1]))
		{
			return (ushort)text[1];
		}
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

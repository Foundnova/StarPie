using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Windows;

namespace WinPieGestures
{
    public static class ActionExecutor
    {
        // P/Invoke for executing applications/commands
        [DllImport("user32.dll")]
        private static extern bool LockWorkStation();

        // P/Invoke for key simulation
        [StructLayout(LayoutKind.Sequential)]
        private struct MOUSEINPUT
        {
            public int dx;
            public int dy;
            public uint mouseData;
            public uint dwFlags;
            public uint time;
            public IntPtr dwExtraInfo;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct KEYBDINPUT
        {
            public ushort wVk;
            public ushort wScan;
            public uint dwFlags;
            public uint time;
            public IntPtr dwExtraInfo;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct HARDWAREINPUT
        {
            public uint uMsg;
            public ushort wParamL;
            public ushort wParamH;
        }

        [StructLayout(LayoutKind.Explicit)]
        private struct InputUnion
        {
            [FieldOffset(0)] public MOUSEINPUT mi;
            [FieldOffset(0)] public KEYBDINPUT ki;
            [FieldOffset(0)] public HARDWAREINPUT hi;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct INPUT
        {
            public uint type;
            public InputUnion U;
        }

        private const uint INPUT_KEYBOARD = 1;
        private const uint KEYEVENTF_KEYUP = 0x0002;
        private const uint KEYEVENTF_EXTENDEDKEY = 0x0001;

        [DllImport("user32.dll", SetLastError = true)]
        private static extern uint SendInput(uint nInputs, INPUT[] pInputs, int cbSize);

        // Virtual Key Codes
        private const ushort VK_LCONTROL = 0xA2;
        private const ushort VK_LSHIFT = 0xA0;
        private const ushort VK_LMENU = 0xA4; // Alt
        private const ushort VK_LWIN = 0x5B;

        private const ushort VK_VOLUME_MUTE = 0xAD;
        private const ushort VK_VOLUME_DOWN = 0xAE;
        private const ushort VK_VOLUME_UP = 0xAF;

        private const ushort VK_LEFT = 0x25;
        private const ushort VK_UP = 0x26;
        private const ushort VK_RIGHT = 0x27;
        private const ushort VK_DOWN = 0x28;
        private const ushort VK_ESCAPE = 0x1B;
        private const ushort VK_RETURN = 0x0D;
        private const ushort VK_TAB = 0x09;
        private const ushort VK_SPACE = 0x20;

        public static void Execute(ActionItem action)
        {
            if (action == null) return;

            try
            {
                switch (action.Type.Trim())
                {
                    case "Launch":
                        ExecuteLaunch(action.Parameter, action.Arguments);
                        break;
                    case "Hotkey":
                        ExecuteHotkey(action.Parameter);
                        break;
                    case "System":
                        ExecuteSystem(action.Parameter);
                        break;
                    default:
                        Debug.WriteLine($"Unknown action type: {action.Type}");
                        break;
                }
            }
            catch (Exception ex)
            {
                System.Windows.MessageBox.Show($"Failed to execute action '{action.Name}': {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private static void ExecuteLaunch(string path, string arguments)
        {
            if (string.IsNullOrEmpty(path)) return;

            var startInfo = new ProcessStartInfo
            {
                FileName = path,
                Arguments = arguments ?? string.Empty,
                UseShellExecute = true
            };
            Process.Start(startInfo);
        }

        private static void ExecuteHotkey(string hotkeyString)
        {
            if (string.IsNullOrEmpty(hotkeyString)) return;

            var keys = ParseHotkey(hotkeyString);
            if (keys.Modifiers.Count == 0 && keys.MainKey == 0) return;

            // Generate inputs: modifiers down, key down, key up, modifiers up
            var inputs = new List<INPUT>();

            // 1. Modifiers down
            foreach (var vk in keys.Modifiers)
            {
                inputs.Add(CreateKeyInput(vk, down: true));
            }

            // 2. Main key down
            if (keys.MainKey != 0)
            {
                inputs.Add(CreateKeyInput(keys.MainKey, down: true));
            }

            // 3. Main key up
            if (keys.MainKey != 0)
            {
                inputs.Add(CreateKeyInput(keys.MainKey, down: false));
            }

            // 4. Modifiers up (in reverse order)
            for (int i = keys.Modifiers.Count - 1; i >= 0; i--)
            {
                inputs.Add(CreateKeyInput(keys.Modifiers[i], down: false));
            }

            SendInput((uint)inputs.Count, inputs.ToArray(), Marshal.SizeOf(typeof(INPUT)));
        }

        private static void ExecuteSystem(string presetName)
        {
            if (string.IsNullOrEmpty(presetName)) return;

            switch (presetName.Trim().ToLower())
            {
                case "lock":
                    LockWorkStation();
                    break;
                case "volumeup":
                    SimulateSingleKey(VK_VOLUME_UP);
                    break;
                case "volumedown":
                    SimulateSingleKey(VK_VOLUME_DOWN);
                    break;
                case "volumemute":
                    SimulateSingleKey(VK_VOLUME_MUTE);
                    break;
                case "showdesktop":
                    // Simulate Win+D
                    ExecuteHotkey("Win+D");
                    break;
                case "screenshot":
                    // Simulate Win+Shift+S
                    ExecuteHotkey("Win+Shift+S");
                    break;
                default:
                    Debug.WriteLine($"Unknown system preset: {presetName}");
                    break;
            }
        }

        private static void SimulateSingleKey(ushort vk)
        {
            var inputs = new INPUT[]
            {
                CreateKeyInput(vk, down: true),
                CreateKeyInput(vk, down: false)
            };
            SendInput(2, inputs, Marshal.SizeOf(typeof(INPUT)));
        }

        private static INPUT CreateKeyInput(ushort vk, bool down)
        {
            var input = new INPUT { type = INPUT_KEYBOARD };
            input.U.ki = new KEYBDINPUT
            {
                wVk = vk,
                wScan = 0,
                dwFlags = (uint)(down ? 0 : KEYEVENTF_KEYUP),
                time = 0,
                dwExtraInfo = IntPtr.Zero
            };

            // Set extended key flag for media keys and arrow keys
            if (vk >= 0x21 && vk <= 0x2F || vk >= 0x5B && vk <= 0x5C || vk >= 0xAD && vk <= 0xB3)
            {
                input.U.ki.dwFlags |= KEYEVENTF_EXTENDEDKEY;
            }

            return input;
        }

        private class HotkeyDetails
        {
            public List<ushort> Modifiers { get; } = new List<ushort>();
            public ushort MainKey { get; set; } = 0;
        }

        private static HotkeyDetails ParseHotkey(string hotkeyString)
        {
            var details = new HotkeyDetails();
            var parts = hotkeyString.Split('+');

            foreach (var part in parts)
            {
                string token = part.Trim().ToLower();
                if (token == "ctrl" || token == "control")
                {
                    details.Modifiers.Add(VK_LCONTROL);
                }
                else if (token == "shift")
                {
                    details.Modifiers.Add(VK_LSHIFT);
                }
                else if (token == "alt" || token == "menu")
                {
                    details.Modifiers.Add(VK_LMENU);
                }
                else if (token == "win" || token == "lwin")
                {
                    details.Modifiers.Add(VK_LWIN);
                }
                else
                {
                    details.MainKey = MapKeyStringToVk(token);
                }
            }

            return details;
        }

        private static ushort MapKeyStringToVk(string keyToken)
        {
            if (keyToken.Length == 1)
            {
                char c = keyToken[0];
                if (c >= 'a' && c <= 'z')
                {
                    return (ushort)('A' + (c - 'a'));
                }
                if (c >= '0' && c <= '9')
                {
                    return (ushort)c;
                }
            }

            switch (keyToken)
            {
                case "left": return VK_LEFT;
                case "up": return VK_UP;
                case "right": return VK_RIGHT;
                case "down": return VK_DOWN;
                case "esc":
                case "escape": return VK_ESCAPE;
                case "enter":
                case "return": return VK_RETURN;
                case "tab": return VK_TAB;
                case "space": return VK_SPACE;
                case "f1": return 0x70;
                case "f2": return 0x71;
                case "f3": return 0x72;
                case "f4": return 0x73;
                case "f5": return 0x74;
                case "f6": return 0x75;
                case "f7": return 0x76;
                case "f8": return 0x77;
                case "f9": return 0x78;
                case "f10": return 0x79;
                case "f11": return 0x7A;
                case "f12": return 0x7B;
                default:
                    Debug.WriteLine($"Unrecognized key: {keyToken}");
                    return 0;
            }
        }
    }
}

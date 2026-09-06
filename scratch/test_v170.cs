using System;
using System.Windows;
using System.Windows.Media;
using WinPieGestures;

public class Program
{
    [STAThread]
    public static void Main()
    {
        Console.WriteLine("=== Testing StarPie v1.7.0 Features ===");
        
        // 1. Test ConfigManager & ThemeManager initialization
        ConfigManager.LoadConfig();
        
        // 2. Test RadialWindow with CenterAction
        var profile = new WheelProfile
        {
            ProcessName = "TestApp",
            SectorCount = 8,
            EnableCenterAction = true,
            CenterAction = new ActionItem
            {
                Name = "StarPie控制台",
                Type = "Launch",
                IconKey = "Paste"
            }
        };
        
        RadialWindow rw = new RadialWindow(new Point(500, 500), profile);
        
        var coreExitIcon = rw.FindName("CoreExitIcon") as System.Windows.Shapes.Path;
        if (coreExitIcon == null)
        {
            Console.WriteLine("FAIL: CoreExitIcon not found!");
            return;
        }
        
        Console.WriteLine($"CoreExitIcon Visibility: {coreExitIcon.Visibility}");
        Console.WriteLine($"CoreExitIcon Data present: {coreExitIcon.Data != null}");
        
        if (coreExitIcon.Visibility != Visibility.Visible || coreExitIcon.Data == null)
        {
            Console.WriteLine("FAIL: CenterAction icon was not rendered!");
            return;
        }
        else
        {
            Console.WriteLine("SUCCESS: CenterAction Paste icon successfully rendered in RadialWindow!");
        }

        // Test hover highlight (deadzone hover)
        rw.HighlightSector(-1, -1);
        var brush = coreExitIcon.Fill as SolidColorBrush;
        Console.WriteLine($"CoreExitIcon Fill color on center hover: {brush?.Color}");
        if (brush != null && brush.Color.R == 245 && brush.Color.G == 158 && brush.Color.B == 11)
        {
            Console.WriteLine("SUCCESS: Center hover highlighted with amber (245, 158, 11)!");
        }
        else
        {
            Console.WriteLine("FAIL: Center hover color mismatch!");
        }
        
        // 3. Test SettingsWindow ConfigMode switching (Simple vs Pro)
        SettingsWindow sw = new SettingsWindow();
        
        var mouseGesturesCard = sw.FindName("Tab0_MouseGesturesCardBorder") as FrameworkElement;
        var cancelActionOuter = sw.FindName("Tab0_CancelActionOuterBorder") as FrameworkElement;
        var screenEdgeCard = sw.FindName("Tab0_ScreenEdgeCardBorder") as FrameworkElement;
        var mappingsViewMode = sw.FindName("Tab2_MappingsViewModeBorder") as FrameworkElement;
        var multiLayerHeader = sw.FindName("Tab2_MultiLayerHeaderPanel") as FrameworkElement;
        var canvasRadio = sw.FindName("MappingsViewModeCanvasRadio") as System.Windows.Controls.RadioButton;

        // Test Simple Mode
        sw.ApplyConfigMode("Simple", false);
        Console.WriteLine($"[Simple Mode] MouseGesturesCard: {mouseGesturesCard?.Visibility}");
        Console.WriteLine($"[Simple Mode] CancelActionOuter: {cancelActionOuter?.Visibility}");
        Console.WriteLine($"[Simple Mode] ScreenEdgeCard: {screenEdgeCard?.Visibility}");
        Console.WriteLine($"[Simple Mode] MappingsViewMode: {mappingsViewMode?.Visibility}");
        Console.WriteLine($"[Simple Mode] MultiLayerHeader: {multiLayerHeader?.Visibility}");
        Console.WriteLine($"[Simple Mode] CanvasRadio checked: {canvasRadio?.IsChecked}");
        
        bool simplePass = mouseGesturesCard?.Visibility == Visibility.Collapsed &&
                          cancelActionOuter?.Visibility == Visibility.Collapsed &&
                          screenEdgeCard?.Visibility == Visibility.Collapsed &&
                          mappingsViewMode?.Visibility == Visibility.Collapsed &&
                          multiLayerHeader?.Visibility == Visibility.Collapsed &&
                          canvasRadio?.IsChecked == true;

        // Test Pro Mode
        sw.ApplyConfigMode("Pro", false);
        Console.WriteLine($"[Pro Mode] MouseGesturesCard: {mouseGesturesCard?.Visibility}");
        Console.WriteLine($"[Pro Mode] CancelActionOuter: {cancelActionOuter?.Visibility}");
        Console.WriteLine($"[Pro Mode] ScreenEdgeCard: {screenEdgeCard?.Visibility}");
        Console.WriteLine($"[Pro Mode] MappingsViewMode: {mappingsViewMode?.Visibility}");
        Console.WriteLine($"[Pro Mode] MultiLayerHeader: {multiLayerHeader?.Visibility}");
        
        bool proPass = mouseGesturesCard?.Visibility == Visibility.Visible &&
                       cancelActionOuter?.Visibility == Visibility.Visible &&
                       screenEdgeCard?.Visibility == Visibility.Visible &&
                       mappingsViewMode?.Visibility == Visibility.Visible &&
                       multiLayerHeader?.Visibility == Visibility.Visible;
                       
        if (simplePass && proPass)
        {
            Console.WriteLine("SUCCESS: Simple and Pro mode UI switching verified 100%!");
        }
        else
        {
            Console.WriteLine("FAIL: Mode visibility assertion failed!");
        }

        // 4. Test SafeSetClipboardText from MTA background thread
        bool mtaSuccess = false;
        Exception mtaEx = null;
        var safeClipboardMethod = typeof(ActionExecutor).GetMethod("SafeSetClipboardText", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);
        var thread = new System.Threading.Thread(() =>
        {
            try
            {
                safeClipboardMethod.Invoke(null, new object[] { "StarPie-Test-MTA" });
                mtaSuccess = true;
            }
            catch (Exception ex)
            {
                mtaEx = ex;
            }
        });
        thread.SetApartmentState(System.Threading.ApartmentState.MTA);
        thread.Start();
        thread.Join();

        if (mtaSuccess && mtaEx == null)
        {
            Console.WriteLine("SUCCESS: SafeSetClipboardText succeeded from MTA background thread without ThreadStateException!");
        }
        else
        {
            Console.WriteLine($"FAIL: SafeSetClipboardText threw: {mtaEx?.Message}");
        }

        // 5. Test ParseHotkey for sequences (U+U, U,U, Win+X)
        var parseHotkeyMethod = typeof(ActionExecutor).GetMethod("ParseHotkey", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);
        if (parseHotkeyMethod != null)
        {
            var uuHotkey = parseHotkeyMethod.Invoke(null, new object[] { "U+U" });
            var uuSeq = uuHotkey?.GetType().GetProperty("SequenceKeys")?.GetValue(uuHotkey) as System.Collections.IList;
            Console.WriteLine($"'U+U' parsed SequenceKeys count: {uuSeq?.Count}");

            var commaHotkey = parseHotkeyMethod.Invoke(null, new object[] { "U,U" });
            var commaSeq = commaHotkey?.GetType().GetProperty("SequenceKeys")?.GetValue(commaHotkey) as System.Collections.IList;
            Console.WriteLine($"'U,U' parsed SequenceKeys count: {commaSeq?.Count}");

            var winXHotkey = parseHotkeyMethod.Invoke(null, new object[] { "Win+X" });
            var mods = winXHotkey?.GetType().GetProperty("Modifiers")?.GetValue(winXHotkey) as System.Collections.Generic.List<ushort>;
            var mainKey = (ushort)(winXHotkey?.GetType().GetProperty("MainKey")?.GetValue(winXHotkey) ?? (ushort)0);
            bool hasWin = mods != null && (mods.Contains(91) || mods.Contains(92));
            Console.WriteLine($"'Win+X' parsed Win Modifier: {hasWin}, MainKey: {mainKey}");

            if (uuSeq?.Count == 2 && commaSeq?.Count == 2 && hasWin && mainKey == 88)
            {
                Console.WriteLine("SUCCESS: ParseHotkey successfully parsed multi-key sequences and Win combos!");
            }
            else
            {
                Console.WriteLine("FAIL: ParseHotkey output mismatch!");
            }
        }

        // 6. Test EnsureTriggerHealth collision resolution
        var ensureHealthMethod = typeof(ConfigManager).GetMethod("EnsureTriggerHealth", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);
        if (ensureHealthMethod != null)
        {
            var testConf = new AppConfig
            {
                Trigger = new TriggerConfig { MouseButton = "RightButton" },
                GestureEnabled = true,
                GestureTriggerButton = "RightButton"
            };
            ensureHealthMethod.Invoke(null, new object[] { testConf });
            Console.WriteLine($"Colliding RightButton adjusted to: {testConf.GestureTriggerButton}");
            if (testConf.GestureTriggerButton == "MiddleButton")
            {
                Console.WriteLine("SUCCESS: EnsureTriggerHealth successfully resolved button collision!");
            }
            else
            {
                Console.WriteLine("FAIL: EnsureTriggerHealth did not resolve button collision!");
            }
        }
    }
}

using System;
using System.Windows;
using System.Windows.Controls;
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

        // 4. Test Layer Indicator Appearance Customization and Toggle
        Console.WriteLine("\n--- Testing Layer Indicator Customization ---");
        var layerIndicatorCard = sw.FindName("Tab1_LayerIndicatorCardBorder") as FrameworkElement;
        var showLayerIndicatorCheckBox = sw.FindName("ShowLayerIndicatorCheckBox") as CheckBox;
        var previewLayerBadge = sw.FindName("PreviewLayerIndicatorBadge") as FrameworkElement;
        var previewLayerText = sw.FindName("PreviewLayerIndicatorText") as TextBlock;
        var previewLayerIcon = sw.FindName("PreviewLayerIndicatorIcon") as TextBlock;

        if (layerIndicatorCard == null || showLayerIndicatorCheckBox == null || previewLayerBadge == null || previewLayerText == null || previewLayerIcon == null)
        {
            Console.WriteLine($"FAIL: Layer indicator UI elements missing! card={layerIndicatorCard!=null}, cb={showLayerIndicatorCheckBox!=null}, badge={previewLayerBadge!=null}, text={previewLayerText!=null}, icon={previewLayerIcon!=null}");
        }
        else
        {
            // Simple mode hides Tab1_LayerIndicatorCardBorder
            sw.ApplyConfigMode("Simple", false);
            Console.WriteLine($"[Simple Mode] LayerIndicatorCard Visibility: {layerIndicatorCard.Visibility}");
            if (layerIndicatorCard.Visibility == Visibility.Collapsed)
            {
                Console.WriteLine("SUCCESS: Tab1_LayerIndicatorCardBorder collapsed in Simple Mode!");
            }
            else
            {
                Console.WriteLine("FAIL: Tab1_LayerIndicatorCardBorder not collapsed in Simple Mode!");
            }

            // Pro mode shows Tab1_LayerIndicatorCardBorder
            sw.ApplyConfigMode("Pro", false);
            Console.WriteLine($"[Pro Mode] LayerIndicatorCard Visibility: {layerIndicatorCard.Visibility}");
            if (layerIndicatorCard.Visibility == Visibility.Visible)
            {
                Console.WriteLine("SUCCESS: Tab1_LayerIndicatorCardBorder visible in Pro Mode!");
            }
            else
            {
                Console.WriteLine("FAIL: Tab1_LayerIndicatorCardBorder not visible in Pro Mode!");
            }

            // Test AppConfig defaults and serialization roundtrip
            var cfg = new AppConfig
            {
                ShowLayerIndicator = false,
                LayerIndicatorStyle = "Purple",
                LayerIndicatorBg = "#E62E1065",
                LayerIndicatorBorder = "#C084FC",
                LayerIndicatorTextColor = "#FAF5FF",
                LayerIndicatorIcon = "❄️",
                LayerIndicatorFontSize = 13.0,
                LayerIndicatorCornerRadius = 16.0,
                LayerIndicatorOffsetY = 15.0,
                LayerIndicatorDurationMs = 1500.0
            };

            string json = System.Text.Json.JsonSerializer.Serialize(cfg);
            var deserialized = System.Text.Json.JsonSerializer.Deserialize<AppConfig>(json);

            if (deserialized != null &&
                deserialized.ShowLayerIndicator == false &&
                deserialized.LayerIndicatorStyle == "Purple" &&
                deserialized.LayerIndicatorBg == "#E62E1065" &&
                deserialized.LayerIndicatorBorder == "#C084FC" &&
                deserialized.LayerIndicatorTextColor == "#FAF5FF" &&
                deserialized.LayerIndicatorIcon == "❄️" &&
                deserialized.LayerIndicatorFontSize == 13.0 &&
                deserialized.LayerIndicatorCornerRadius == 16.0 &&
                deserialized.LayerIndicatorOffsetY == 15.0 &&
                deserialized.LayerIndicatorDurationMs == 1500.0)
            {
                Console.WriteLine("SUCCESS: AppConfig Layer Indicator properties serialized and deserialized flawlessly!");
            }
            else
            {
                Console.WriteLine("FAIL: Serialization roundtrip mismatch for Layer Indicator properties!");
            }

            // Test RadialWindow Layer Indicator Style Application
            ConfigManager.CurrentConfig.ShowLayerIndicator = true;
            ConfigManager.CurrentConfig.LayerIndicatorStyle = "Aurora";
            ConfigManager.CurrentConfig.LayerIndicatorBg = "#E6082F49";
            ConfigManager.CurrentConfig.LayerIndicatorBorder = "#22D3EE";
            ConfigManager.CurrentConfig.LayerIndicatorTextColor = "#F0FDFA";
            ConfigManager.CurrentConfig.LayerIndicatorIcon = "🌀";
            ConfigManager.CurrentConfig.LayerIndicatorCornerRadius = 14.0;
            ConfigManager.CurrentConfig.LayerIndicatorFontSize = 12.0;

            var multiLayerProfile = new WheelProfile
            {
                ProcessName = "MultiLayerTest",
                Layers = new System.Collections.Generic.List<WheelLayer>
                {
                    new WheelLayer { Name = "第一层" },
                    new WheelLayer { Name = "第二层" }
                }
            };
            RadialWindow rw2 = new RadialWindow(new Point(600, 600), multiLayerProfile);
            rw2.SwitchToLayer(1);

            var rwBadge = rw2.FindName("LayerIndicatorBadge") as Border;
            var rwText = rw2.FindName("LayerIndicatorText") as TextBlock;
            var rwIcon = rw2.FindName("LayerIndicatorIcon") as TextBlock;

            if (rwBadge != null && rwText != null && rwIcon != null)
            {
                Console.WriteLine($"RadialWindow Badge Visibility: {rwBadge.Visibility}");
                Console.WriteLine($"RadialWindow Badge Text: {rwText.Text}");
                Console.WriteLine($"RadialWindow Badge Icon: {rwIcon.Text}");
                Console.WriteLine($"RadialWindow CornerRadius: {rwBadge.CornerRadius.TopLeft}");

                if (rwBadge.Visibility == Visibility.Visible &&
                    rwText.Text.Contains("第二层") &&
                    rwIcon.Text == "🌀" &&
                    rwBadge.CornerRadius.TopLeft == 14.0)
                {
                    Console.WriteLine("SUCCESS: RadialWindow multi-layer indicator badge correctly rendered with custom style!");
                }
                else
                {
                    Console.WriteLine("FAIL: RadialWindow layer indicator badge did not match expected values!");
                }

                // Switch off indicator
                ConfigManager.CurrentConfig.ShowLayerIndicator = false;
                rw2.SwitchToLayer(0);
                if (rwBadge.Visibility == Visibility.Collapsed)
                {
                    Console.WriteLine("SUCCESS: RadialWindow layer indicator correctly hidden when ShowLayerIndicator is false!");
                }
                else
                {
                    Console.WriteLine("FAIL: RadialWindow layer indicator remained visible when ShowLayerIndicator is false!");
                }
            }
            else
            {
                Console.WriteLine("FAIL: RadialWindow LayerIndicator elements not found!");
            }
        }
    }
}

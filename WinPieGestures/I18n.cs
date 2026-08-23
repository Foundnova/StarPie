using System;
using System.Collections.Generic;
using System.Globalization;

namespace WinPieGestures
{
    public enum LanguageCode
    {
        ZhCn, // 简体中文
        ZhTw, // 繁體中文
        En,   // English
        Ja    // 日本語
    }

    public static class I18n
    {
        private static LanguageCode _currentLanguage = LanguageCode.ZhCn;
        public static event Action? LanguageChanged;

        public static LanguageCode CurrentLanguage
        {
            get => _currentLanguage;
            set
            {
                if (_currentLanguage != value)
                {
                    _currentLanguage = value;
                    LanguageChanged?.Invoke();
                }
            }
        }

        public static string CurrentLanguageCode => _currentLanguage switch
        {
            LanguageCode.ZhTw => "zh-TW",
            LanguageCode.En => "en",
            LanguageCode.Ja => "ja",
            _ => "zh-CN"
        };

        public static void SetLanguage(string code)
        {
            if (string.Equals(code, "Auto", StringComparison.OrdinalIgnoreCase))
            {
                var culture = CultureInfo.CurrentUICulture.Name;
                if (culture.StartsWith("zh-TW", StringComparison.OrdinalIgnoreCase) ||
                    culture.StartsWith("zh-HK", StringComparison.OrdinalIgnoreCase) ||
                    culture.StartsWith("zh-MO", StringComparison.OrdinalIgnoreCase) ||
                    culture.StartsWith("zh-Hant", StringComparison.OrdinalIgnoreCase))
                {
                    CurrentLanguage = LanguageCode.ZhTw;
                }
                else if (culture.StartsWith("zh", StringComparison.OrdinalIgnoreCase))
                {
                    CurrentLanguage = LanguageCode.ZhCn;
                }
                else if (culture.StartsWith("ja", StringComparison.OrdinalIgnoreCase))
                {
                    CurrentLanguage = LanguageCode.Ja;
                }
                else
                {
                    CurrentLanguage = LanguageCode.En;
                }
                return;
            }

            CurrentLanguage = code switch
            {
                "zh-TW" or "zh-HK" or "zh-Hant" => LanguageCode.ZhTw,
                "en" or "en-US" or "en-GB" => LanguageCode.En,
                "ja" or "ja-JP" => LanguageCode.Ja,
                _ => LanguageCode.ZhCn
            };
        }

        public static string T(string key) => GetString(key);

        public static string GetString(string key)
        {
            if (Translations.TryGetValue(key, out var dict))
            {
                if (dict.TryGetValue(_currentLanguage, out var val))
                    return val;
                if (dict.TryGetValue(LanguageCode.ZhCn, out var fallback))
                    return fallback;
            }
            return key;
        }

        private static readonly Dictionary<string, Dictionary<LanguageCode, string>> Translations = new()
        {
            // App Brand & Headers
            ["AppName"] = new()
            {
                [LanguageCode.ZhCn] = "StarPie",
                [LanguageCode.ZhTw] = "StarPie",
                [LanguageCode.En] = "StarPie",
                [LanguageCode.Ja] = "StarPie"
            },
            ["AppSubtitle"] = new()
            {
                [LanguageCode.ZhCn] = "现代鼠标轮盘笔势系统",
                [LanguageCode.ZhTw] = "現代滑鼠輪盤手勢系統",
                [LanguageCode.En] = "Modern Mouse Radial Gestures",
                [LanguageCode.Ja] = "次世代マウスラジアルジェスチャー"
            },
            ["WindowTitle"] = new()
            {
                [LanguageCode.ZhCn] = "StarPie 设置控制台 (Preferences)",
                [LanguageCode.ZhTw] = "StarPie 設定控制台 (Preferences)",
                [LanguageCode.En] = "StarPie Preferences Console",
                [LanguageCode.Ja] = "StarPie 環境設定コンソール"
            },

            // Sidebar Tabs
            ["TabTrigger"] = new()
            {
                [LanguageCode.ZhCn] = "🎯 触发与场景",
                [LanguageCode.ZhTw] = "🎯 觸發與場景",
                [LanguageCode.En] = "🎯 Trigger & Scenes",
                [LanguageCode.Ja] = "🎯 トリガーとシーン"
            },
            ["TabAppearance"] = new()
            {
                [LanguageCode.ZhCn] = "🎨 外观与形态",
                [LanguageCode.ZhTw] = "🎨 外觀與形態",
                [LanguageCode.En] = "🎨 Appearance & Shapes",
                [LanguageCode.Ja] = "🎨 外観と形状"
            },
            ["TabGestures"] = new()
            {
                [LanguageCode.ZhCn] = "⚡ 手势与动作",
                [LanguageCode.ZhTw] = "⚡ 手勢與動作",
                [LanguageCode.En] = "⚡ Gestures & Actions",
                [LanguageCode.Ja] = "⚡ ジェスチャーと動作"
            },
            ["TabAdvanced"] = new()
            {
                [LanguageCode.ZhCn] = "⚙️ 高级与系统",
                [LanguageCode.ZhTw] = "⚙️ 進階與系統",
                [LanguageCode.En] = "⚙️ Advanced & System",
                [LanguageCode.Ja] = "⚙️ 高度な設定とシステム"
            },
            ["TabAbout"] = new()
            {
                [LanguageCode.ZhCn] = "📋 关于与更新",
                [LanguageCode.ZhTw] = "📋 關於與更新",
                [LanguageCode.En] = "📋 About & Updates",
                [LanguageCode.Ja] = "📋 情報と更新"
            },

            // Bottom Bar
            ["BottomStatusNote"] = new()
            {
                [LanguageCode.ZhCn] = "注: 所有修改均在内存中即时生效，点击【保存更改】持久化保存至硬盘。",
                [LanguageCode.ZhTw] = "註: 所有修改均在記憶體中即時生效，點擊【儲存變更】持久化儲存至硬碟。",
                [LanguageCode.En] = "Note: All changes take effect in memory immediately. Click [Save Changes] to persist to disk.",
                [LanguageCode.Ja] = "注: 変更はメモリ上で即座に有効になります。[変更を保存] で設定ファイルに永続化されます。"
            },
            ["BtnSave"] = new()
            {
                [LanguageCode.ZhCn] = "保存更改",
                [LanguageCode.ZhTw] = "儲存變更",
                [LanguageCode.En] = "Save Changes",
                [LanguageCode.Ja] = "変更を保存"
            },
            ["BtnClose"] = new()
            {
                [LanguageCode.ZhCn] = "关闭并隐藏",
                [LanguageCode.ZhTw] = "關閉並隱藏",
                [LanguageCode.En] = "Close & Hide",
                [LanguageCode.Ja] = "閉じて隠す"
            },

            // Tab 0: Trigger & Scenes
            ["TriggerHeader"] = new()
            {
                [LanguageCode.ZhCn] = "触发与场景隔离设置",
                [LanguageCode.ZhTw] = "觸發與場景隔離設定",
                [LanguageCode.En] = "Trigger & Scene Isolation",
                [LanguageCode.Ja] = "トリガーとシーンの分離設定"
            },
            ["TriggerSubheader"] = new()
            {
                [LanguageCode.ZhCn] = "在此配置全局鼠标手势的触发灵敏度、全屏游戏自动拦截与排除程序黑名单。",
                [LanguageCode.ZhTw] = "在此配置全域滑鼠手勢的觸發靈敏度、全螢幕遊戲自動攔截與排除程式黑名單。",
                [LanguageCode.En] = "Configure mouse gesture sensitivity, full-screen gaming bypass, and exclusion blacklist.",
                [LanguageCode.Ja] = "マウスジェスチャーの感度、フルスクリーンゲームでの自動回避、除外プロセスを設定します。"
            },
            ["SensitivityTitle"] = new()
            {
                [LanguageCode.ZhCn] = "手势触发灵敏度",
                [LanguageCode.ZhTw] = "手勢觸發靈敏度",
                [LanguageCode.En] = "Trigger Sensitivity",
                [LanguageCode.Ja] = "ジェスチャー起動感度"
            },
            ["SensitivityDesc"] = new()
            {
                [LanguageCode.ZhCn] = "按住鼠标右键移动超过指定像素距离后呼出手势轮盘。距离越小越灵敏，过小可能造成右键微抖动误触。",
                [LanguageCode.ZhTw] = "按住滑鼠右鍵移動超過指定像素距離後呼出手勢輪盤。距離越小越靈敏，過小可能造成右鍵微抖動誤觸。",
                [LanguageCode.En] = "Hold right-click and move beyond this pixel distance to trigger radial menu. Lower values are more sensitive.",
                [LanguageCode.Ja] = "右クリックを押しながら指定ピクセル以上移動するとホイールを呼び出します。値が小さいほど高感度です。"
            },
            ["SceneIsolationTitle"] = new()
            {
                [LanguageCode.ZhCn] = "场景隔离与防误触",
                [LanguageCode.ZhTw] = "場景隔離與防誤觸",
                [LanguageCode.En] = "Scene Isolation & Guard",
                [LanguageCode.Ja] = "シーン分離と誤操作防止"
            },
            ["SceneIsolationDesc"] = new()
            {
                [LanguageCode.ZhCn] = "当处于特定场景或配合修饰键操作时，自动绕过轮盘拦截，放行原生右键事件。",
                [LanguageCode.ZhTw] = "當處於特定場景或配合修飾鍵操作時，自動繞過輪盤攔截，放行原生右鍵事件。",
                [LanguageCode.En] = "Automatically bypass radial menu and pass-through native right-click in specific scenarios.",
                [LanguageCode.Ja] = "特定の環境や修飾キー操作時にホイールを無効化し、通常の右クリックを通過させます。"
            },
            ["FullScreenOption"] = new()
            {
                [LanguageCode.ZhCn] = "全屏游戏/独占应用自动禁用手势",
                [LanguageCode.ZhTw] = "全螢幕遊戲/獨佔應用自動禁用手勢",
                [LanguageCode.En] = "Auto-disable in Full-screen games / Exclusive apps",
                [LanguageCode.Ja] = "全画面ゲーム/専用アプリでジェスチャーを自動無効化"
            },
            ["FullScreenOptionDesc"] = new()
            {
                [LanguageCode.ZhCn] = "自动检测当前前台窗口是否处于全屏独占状态，避免游戏瞄准等右键操作被拦截。",
                [LanguageCode.ZhTw] = "自動檢測當前前台視窗是否處於全螢幕獨佔狀態，避免遊戲瞄準等右鍵操作被攔截。",
                [LanguageCode.En] = "Detects whether active window is running in full-screen to avoid intercepting gaming right-clicks.",
                [LanguageCode.Ja] = "アクティブなウィンドウが全画面かどうかを検知し、ゲームの照準等の右クリック操作を邪魔しません。"
            },
            ["ModifierPassTitle"] = new()
            {
                [LanguageCode.ZhCn] = "快捷键旁路穿透 (按住以下修饰键拖拽时不触发手势):",
                [LanguageCode.ZhTw] = "快速鍵旁路穿透 (按住以下修飾鍵拖曳時不觸發手勢):",
                [LanguageCode.En] = "Modifier Pass-Through (hold to bypass gestures):",
                [LanguageCode.Ja] = "修飾キーバイパス (押下中はジェスチャーを無効化):"
            },
            ["ModifierCtrl"] = new()
            {
                [LanguageCode.ZhCn] = "按住 Ctrl 键时旁路",
                [LanguageCode.ZhTw] = "按住 Ctrl 鍵時旁路",
                [LanguageCode.En] = "Bypass on Ctrl",
                [LanguageCode.Ja] = "Ctrl 押下時にバイパス"
            },
            ["ModifierShift"] = new()
            {
                [LanguageCode.ZhCn] = "按住 Shift 键时旁路",
                [LanguageCode.ZhTw] = "按住 Shift 鍵時旁路",
                [LanguageCode.En] = "Bypass on Shift",
                [LanguageCode.Ja] = "Shift 押下時にバイパス"
            },
            ["ModifierAlt"] = new()
            {
                [LanguageCode.ZhCn] = "按住 Alt 键时旁路",
                [LanguageCode.ZhTw] = "按住 Alt 鍵時旁路",
                [LanguageCode.En] = "Bypass on Alt",
                [LanguageCode.Ja] = "Alt 押下時にバイパス"
            },
            ["BlacklistTitle"] = new()
            {
                [LanguageCode.ZhCn] = "进程排除黑名单",
                [LanguageCode.ZhTw] = "行程排除黑名單",
                [LanguageCode.En] = "Process Exclusion Blacklist",
                [LanguageCode.Ja] = "除外プロセスブラックリスト"
            },
            ["BlacklistDesc"] = new()
            {
                [LanguageCode.ZhCn] = "在排除名单中的应用程序（如远程桌面、画图、3D建模软件）中，完全放行鼠标右键。",
                [LanguageCode.ZhTw] = "在排除名單中的應用程式（如遠端桌面、小畫家、3D建模軟體）中，完全放行滑鼠右鍵。",
                [LanguageCode.En] = "Native right-click is fully allowed within blacklisted applications (e.g. Remote Desktop, Paint, CAD).",
                [LanguageCode.Ja] = "登録されたアプリ（リモートデスクトップ、ペイント、3Dモデリング等）では右クリックを直接通します。"
            },
            ["BtnAddProcess"] = new()
            {
                [LanguageCode.ZhCn] = "添加进程",
                [LanguageCode.ZhTw] = "新增行程",
                [LanguageCode.En] = "Add Process",
                [LanguageCode.Ja] = "プロセス追加"
            },
            ["BtnDeleteProcess"] = new()
            {
                [LanguageCode.ZhCn] = "删除选中",
                [LanguageCode.ZhTw] = "刪除選中",
                [LanguageCode.En] = "Delete Selected",
                [LanguageCode.Ja] = "選択項目を削除"
            },
            ["BlacklistPlaceholder"] = new()
            {
                [LanguageCode.ZhCn] = "输入进程名称 (如 mstsc.exe)",
                [LanguageCode.ZhTw] = "輸入行程名稱 (如 mstsc.exe)",
                [LanguageCode.En] = "Enter process name (e.g. mstsc.exe)",
                [LanguageCode.Ja] = "プロセス名を入力 (例: mstsc.exe)"
            },

            // Tab 1: Appearance & Shapes
            ["AppearanceHeader"] = new()
            {
                [LanguageCode.ZhCn] = "轮盘外观与形态定制",
                [LanguageCode.ZhTw] = "輪盤外觀與形態自訂",
                [LanguageCode.En] = "Appearance & Shapes Customization",
                [LanguageCode.Ja] = "外観と形状のカスタマイズ"
            },
            ["AppearanceSubheader"] = new()
            {
                [LanguageCode.ZhCn] = "自由配置轮盘视觉风格、配色方案、高亮边缘光晕、几何切削、图标排版与中心核圆贴图。",
                [LanguageCode.ZhTw] = "自由配置輪盤視覺風格、配色方案、高亮邊緣光暈、幾何切削、圖示排版與中心核圓貼圖。",
                [LanguageCode.En] = "Customize visual styles, color palettes, highlight glow, geometry shapes, typography, and core image.",
                [LanguageCode.Ja] = "ビジュアルスタイル、配色テーマ、グロー発光、幾何学形状、アイコン配置、コアバッジをカスタマイズします。"
            },
            ["StyleTitle"] = new()
            {
                [LanguageCode.ZhCn] = "轮盘渲染风格 (Visual Renderer)",
                [LanguageCode.ZhTw] = "輪盤渲染風格 (Visual Renderer)",
                [LanguageCode.En] = "Visual Renderer Style",
                [LanguageCode.Ja] = "ビジュアルレンダラー"
            },
            ["StyleGlass"] = new()
            {
                [LanguageCode.ZhCn] = "液态毛玻璃 (Glassmorphism)",
                [LanguageCode.ZhTw] = "液態毛玻璃 (Glassmorphism)",
                [LanguageCode.En] = "Liquid Glassmorphism",
                [LanguageCode.Ja] = "リキッドグラスモーフィズム"
            },
            ["StyleClassic"] = new()
            {
                [LanguageCode.ZhCn] = "经典圆环 (Classic Ring)",
                [LanguageCode.ZhTw] = "經典圓環 (Classic Ring)",
                [LanguageCode.En] = "Classic Ring",
                [LanguageCode.Ja] = "クラシックリング"
            },
            ["StyleClean"] = new()
            {
                [LanguageCode.ZhCn] = "极简扇区 (Clean Sectors)",
                [LanguageCode.ZhTw] = "極簡扇區 (Clean Sectors)",
                [LanguageCode.En] = "Clean Sectors",
                [LanguageCode.Ja] = "クリーンセクター"
            },
            ["StyleCatPaw"] = new()
            {
                [LanguageCode.ZhCn] = "萌宠猫爪 (Cute Cat Paw)",
                [LanguageCode.ZhTw] = "萌寵貓爪 (Cute Cat Paw)",
                [LanguageCode.En] = "Cute Cat Paw",
                [LanguageCode.Ja] = "キュートキャットポー (肉球)"
            },
            ["ThemeTitle"] = new()
            {
                [LanguageCode.ZhCn] = "轮盘配色方案 (Color Palette)",
                [LanguageCode.ZhTw] = "輪盤配色方案 (Color Palette)",
                [LanguageCode.En] = "Wheel Color Palette",
                [LanguageCode.Ja] = "ホイール配色パレット"
            },
            ["BtnDeletePreset"] = new()
            {
                [LanguageCode.ZhCn] = "🗑️ 删除预设",
                [LanguageCode.ZhTw] = "🗑️ 刪除預設",
                [LanguageCode.En] = "🗑️ Delete Preset",
                [LanguageCode.Ja] = "🗑️ プリセット削除"
            },
            ["GlowTitle"] = new()
            {
                [LanguageCode.ZhCn] = "高亮边缘光晕 (Highlight Edge Glow)",
                [LanguageCode.ZhTw] = "高亮邊緣光暈 (Highlight Edge Glow)",
                [LanguageCode.En] = "Highlight Edge Glow",
                [LanguageCode.Ja] = "ハイライトエッジグロー発光"
            },
            ["GlowFollowTheme"] = new()
            {
                [LanguageCode.ZhCn] = "跟随主题高亮色 (Auto)",
                [LanguageCode.ZhTw] = "跟隨主題高亮色 (Auto)",
                [LanguageCode.En] = "Follow Theme (Auto)",
                [LanguageCode.Ja] = "テーマ連動 (自動)"
            },
            ["GlowRadius"] = new()
            {
                [LanguageCode.ZhCn] = "光晕弥散半径 (Glow Radius)",
                [LanguageCode.ZhTw] = "光暈彌散半徑 (Glow Radius)",
                [LanguageCode.En] = "Glow Radius",
                [LanguageCode.Ja] = "グロー拡散半径"
            },
            ["GlowOpacity"] = new()
            {
                [LanguageCode.ZhCn] = "光晕不透明度 (Glow Opacity)",
                [LanguageCode.ZhTw] = "光暈不透明度 (Glow Opacity)",
                [LanguageCode.En] = "Glow Opacity",
                [LanguageCode.Ja] = "グロー不透明度"
            },
            ["GeometryTitle"] = new()
            {
                [LanguageCode.ZhCn] = "几何形态与尺寸 (Geometry & Dimensions)",
                [LanguageCode.ZhTw] = "幾何形態與尺寸 (Geometry & Dimensions)",
                [LanguageCode.En] = "Geometry & Dimensions",
                [LanguageCode.Ja] = "幾何学形状とサイズ"
            },
            ["ShapeOriginal"] = new()
            {
                [LanguageCode.ZhCn] = "原生扇区 (Original Sector)",
                [LanguageCode.ZhTw] = "原生扇區 (Original Sector)",
                [LanguageCode.En] = "Original Sector",
                [LanguageCode.Ja] = "オリジナルセクター"
            },
            ["ShapeCircle"] = new()
            {
                [LanguageCode.ZhCn] = "极简圆形 (Floating Circle)",
                [LanguageCode.ZhTw] = "極簡圓形 (Floating Circle)",
                [LanguageCode.En] = "Floating Circle",
                [LanguageCode.Ja] = "フローティングサークル"
            },
            ["ShapeRounded"] = new()
            {
                [LanguageCode.ZhCn] = "平滑圆角 (Rounded Fillet)",
                [LanguageCode.ZhTw] = "平滑圓角 (Rounded Fillet)",
                [LanguageCode.En] = "Rounded Fillet",
                [LanguageCode.Ja] = "角丸フィレット"
            },
            ["ShapeCapsule"] = new()
            {
                [LanguageCode.ZhCn] = "圆润胶囊 (Pill Capsules)",
                [LanguageCode.ZhTw] = "圓潤膠囊 (Pill Capsules)",
                [LanguageCode.En] = "Pill Capsules",
                [LanguageCode.Ja] = "ピルカプセル"
            },
            ["ShapeHexagon"] = new()
            {
                [LanguageCode.ZhCn] = "未来蜂巢 (Hexagon Hive)",
                [LanguageCode.ZhTw] = "未來蜂巢 (Hexagon Hive)",
                [LanguageCode.En] = "Hexagon Hive",
                [LanguageCode.Ja] = "ヘキサゴンハニカム"
            },
            ["RadiusOuter"] = new()
            {
                [LanguageCode.ZhCn] = "轮盘外半径 (Outer Radius)",
                [LanguageCode.ZhTw] = "輪盤外半徑 (Outer Radius)",
                [LanguageCode.En] = "Outer Radius",
                [LanguageCode.Ja] = "外側半径"
            },
            ["RadiusInner"] = new()
            {
                [LanguageCode.ZhCn] = "内环半径 (Inner Radius)",
                [LanguageCode.ZhTw] = "內環半徑 (Inner Radius)",
                [LanguageCode.En] = "Inner Radius",
                [LanguageCode.Ja] = "内側半径"
            },
            ["RadiusCore"] = new()
            {
                [LanguageCode.ZhCn] = "核圆半径 (Core Radius)",
                [LanguageCode.ZhTw] = "核圓半徑 (Core Radius)",
                [LanguageCode.En] = "Core Radius",
                [LanguageCode.Ja] = "コア半径"
            },
            ["SectorGap"] = new()
            {
                [LanguageCode.ZhCn] = "扇区间隙 (Sector Gap)",
                [LanguageCode.ZhTw] = "扇區間隙 (Sector Gap)",
                [LanguageCode.En] = "Sector Gap",
                [LanguageCode.Ja] = "セクター間隔"
            },
            ["SectorCornerRadius"] = new()
            {
                [LanguageCode.ZhCn] = "扇区倒角 (Corner Radius)",
                [LanguageCode.ZhTw] = "扇區倒角 (Corner Radius)",
                [LanguageCode.En] = "Corner Radius",
                [LanguageCode.Ja] = "角丸半径"
            },
            ["BtnResetGeometry"] = new()
            {
                [LanguageCode.ZhCn] = "重置形态默认值",
                [LanguageCode.ZhTw] = "重設形態預設值",
                [LanguageCode.En] = "Reset Geometry Defaults",
                [LanguageCode.Ja] = "形状初期値に戻す"
            },
            ["IconLayoutTitle"] = new()
            {
                [LanguageCode.ZhCn] = "图标与文字排版 (Layout & Typography)",
                [LanguageCode.ZhTw] = "圖示與文字排版 (Layout & Typography)",
                [LanguageCode.En] = "Layout & Typography",
                [LanguageCode.Ja] = "レイアウトと文字"
            },
            ["LayoutIconText"] = new()
            {
                [LanguageCode.ZhCn] = "图文并茂 (Icon + Text)",
                [LanguageCode.ZhTw] = "圖文並茂 (Icon + Text)",
                [LanguageCode.En] = "Icon & Text",
                [LanguageCode.Ja] = "アイコン＋文字"
            },
            ["LayoutIconOnly"] = new()
            {
                [LanguageCode.ZhCn] = "仅显示图标 (Icon Only)",
                [LanguageCode.ZhTw] = "僅顯示圖示 (Icon Only)",
                [LanguageCode.En] = "Icon Only",
                [LanguageCode.Ja] = "アイコンのみ"
            },
            ["LayoutTextOnly"] = new()
            {
                [LanguageCode.ZhCn] = "仅显示文字 (Text Only)",
                [LanguageCode.ZhTw] = "僅顯示文字 (Text Only)",
                [LanguageCode.En] = "Text Only",
                [LanguageCode.Ja] = "文字のみ"
            },
            ["SectorIconSize"] = new()
            {
                [LanguageCode.ZhCn] = "图标大小 (Icon Size)",
                [LanguageCode.ZhTw] = "圖示大小 (Icon Size)",
                [LanguageCode.En] = "Icon Size",
                [LanguageCode.Ja] = "アイコンサイズ"
            },
            ["SectorFontSize"] = new()
            {
                [LanguageCode.ZhCn] = "文字字号 (Font Size)",
                [LanguageCode.ZhTw] = "文字字級 (Font Size)",
                [LanguageCode.En] = "Font Size",
                [LanguageCode.Ja] = "文字サイズ"
            },
            ["CoreTitle"] = new()
            {
                [LanguageCode.ZhCn] = "中心核圆图案定制 (Center Core Customization)",
                [LanguageCode.ZhTw] = "中心核圓圖案自訂 (Center Core Customization)",
                [LanguageCode.En] = "Center Core Customization",
                [LanguageCode.Ja] = "中央コアのカスタマイズ"
            },
            ["CoreShowIcon"] = new()
            {
                [LanguageCode.ZhCn] = "显示中心图案 / 贴图",
                [LanguageCode.ZhTw] = "顯示中心圖案 / 貼圖",
                [LanguageCode.En] = "Show Core Icon / Image",
                [LanguageCode.Ja] = "中央アイコン/画像を表示"
            },
            ["CoreIconType"] = new()
            {
                [LanguageCode.ZhCn] = "核圆图案模式",
                [LanguageCode.ZhTw] = "核圓圖案模式",
                [LanguageCode.En] = "Core Pattern Mode",
                [LanguageCode.Ja] = "コアパターンモード"
            },
            ["CorePatternExit"] = new()
            {
                [LanguageCode.ZhCn] = "取消叉号 (Cancel Cross)",
                [LanguageCode.ZhTw] = "取消叉號 (Cancel Cross)",
                [LanguageCode.En] = "Cancel Cross",
                [LanguageCode.Ja] = "キャンセルバツ"
            },
            ["CorePatternCrosshair"] = new()
            {
                [LanguageCode.ZhCn] = "精准准心 (Crosshair)",
                [LanguageCode.ZhTw] = "精準準心 (Crosshair)",
                [LanguageCode.En] = "Crosshair",
                [LanguageCode.Ja] = "照準レティクル"
            },
            ["CorePatternWindows"] = new()
            {
                [LanguageCode.ZhCn] = "Windows 微标",
                [LanguageCode.ZhTw] = "Windows 微標",
                [LanguageCode.En] = "Windows Emblem",
                [LanguageCode.Ja] = "Windows ロゴ"
            },
            ["CorePatternDot"] = new()
            {
                [LanguageCode.ZhCn] = "极简圆点 (Minimal Dot)",
                [LanguageCode.ZhTw] = "極簡圓點 (Minimal Dot)",
                [LanguageCode.En] = "Minimal Dot",
                [LanguageCode.Ja] = "ミニマルドット"
            },
            ["CorePatternHome"] = new()
            {
                [LanguageCode.ZhCn] = "主页图标 (Home)",
                [LanguageCode.ZhTw] = "首頁圖示 (Home)",
                [LanguageCode.En] = "Home",
                [LanguageCode.Ja] = "ホーム"
            },
            ["CorePatternPower"] = new()
            {
                [LanguageCode.ZhCn] = "电源图标 (Power)",
                [LanguageCode.ZhTw] = "電源圖示 (Power)",
                [LanguageCode.En] = "Power",
                [LanguageCode.Ja] = "電源"
            },
            ["CorePatternCompass"] = new()
            {
                [LanguageCode.ZhCn] = "星空罗盘 (Compass)",
                [LanguageCode.ZhTw] = "星空羅盤 (Compass)",
                [LanguageCode.En] = "Compass",
                [LanguageCode.Ja] = "コンパス"
            },
            ["CorePatternCatPaw"] = new()
            {
                [LanguageCode.ZhCn] = "萌宠猫爪 (Cat Paw)",
                [LanguageCode.ZhTw] = "萌寵貓爪 (Cat Paw)",
                [LanguageCode.En] = "Cat Paw",
                [LanguageCode.Ja] = "肉球"
            },
            ["CorePatternImage"] = new()
            {
                [LanguageCode.ZhCn] = "🖼️ 自定义本地图片贴图...",
                [LanguageCode.ZhTw] = "🖼️ 自訂本機圖片貼圖...",
                [LanguageCode.En] = "🖼️ Custom Local Image...",
                [LanguageCode.Ja] = "🖼️ カスタム画像ファイル..."
            },
            ["BtnBrowseImage"] = new()
            {
                [LanguageCode.ZhCn] = "浏览选择图片",
                [LanguageCode.ZhTw] = "瀏覽選擇圖片",
                [LanguageCode.En] = "Browse Image",
                [LanguageCode.Ja] = "画像を選択"
            },
            ["ConsoleThemeTitle"] = new()
            {
                [LanguageCode.ZhCn] = "软件控制台主题 (Console Theme)",
                [LanguageCode.ZhTw] = "軟體控制台主題 (Console Theme)",
                [LanguageCode.En] = "Console Theme",
                [LanguageCode.Ja] = "コントロールパネルテーマ"
            },
            ["ThemeSystem"] = new()
            {
                [LanguageCode.ZhCn] = "🖥️ 跟随 Windows 系统 (Auto)",
                [LanguageCode.ZhTw] = "🖥️ 跟隨 Windows 系統 (Auto)",
                [LanguageCode.En] = "🖥️ Follow Windows System",
                [LanguageCode.Ja] = "🖥️ Windows システムに従う"
            },
            ["ThemeLight"] = new()
            {
                [LanguageCode.ZhCn] = "☀️ 极简纯白 (Pure Light)",
                [LanguageCode.ZhTw] = "☀️ 極簡純白 (Pure Light)",
                [LanguageCode.En] = "☀️ Pure Light",
                [LanguageCode.Ja] = "☀️ ピュアライト"
            },
            ["ThemeDark"] = new()
            {
                [LanguageCode.ZhCn] = "🌙 极夜曜黑 (Oled Dark)",
                [LanguageCode.ZhTw] = "🌙 極夜曜黑 (Oled Dark)",
                [LanguageCode.En] = "🌙 OLED Dark",
                [LanguageCode.Ja] = "🌙 OLEDダーク"
            },
            ["ThemeNavy"] = new()
            {
                [LanguageCode.ZhCn] = "🌌 午夜深蓝 (Midnight Navy)",
                [LanguageCode.ZhTw] = "🌌 午夜深藍 (Midnight Navy)",
                [LanguageCode.En] = "🌌 Midnight Navy",
                [LanguageCode.Ja] = "🌌 ミッドナイトネイビー"
            },
            ["ThemeViolet"] = new()
            {
                [LanguageCode.ZhCn] = "🔮 暗夜紫罗兰 (Royal Violet)",
                [LanguageCode.ZhTw] = "🔮 暗夜紫羅蘭 (Royal Violet)",
                [LanguageCode.En] = "🔮 Royal Violet",
                [LanguageCode.Ja] = "🔮 ロイヤルバイオレット"
            },
            ["ThemeGray"] = new()
            {
                [LanguageCode.ZhCn] = "⚙️ 钛金深灰 (Titanium Gray)",
                [LanguageCode.ZhTw] = "⚙️ 鈦金深灰 (Titanium Gray)",
                [LanguageCode.En] = "⚙️ Titanium Gray",
                [LanguageCode.Ja] = "⚙️ チタングレー"
            },

            // Tab 2: Gestures & Actions
            ["GesturesHeader"] = new()
            {
                [LanguageCode.ZhCn] = "手势轮盘分位与动作配置",
                [LanguageCode.ZhTw] = "手勢輪盤分位與動作配置",
                [LanguageCode.En] = "Gesture Sectors & Action Mappings",
                [LanguageCode.Ja] = "セクター配置とアクション設定"
            },
            ["SectorCountTitle"] = new()
            {
                [LanguageCode.ZhCn] = "轮盘方位按键数 (Sector Count)",
                [LanguageCode.ZhTw] = "輪盤方位按鍵數 (Sector Count)",
                [LanguageCode.En] = "Sector Count",
                [LanguageCode.Ja] = "セクター数（キー数）"
            },
            ["SectorCount4"] = new()
            {
                [LanguageCode.ZhCn] = "4 键 (十字方位 / Cross 4-Way)",
                [LanguageCode.ZhTw] = "4 鍵 (十字方位 / Cross 4-Way)",
                [LanguageCode.En] = "4 Sectors (Cross 4-Way)",
                [LanguageCode.Ja] = "4キー (十字方向)"
            },
            ["SectorCount8"] = new()
            {
                [LanguageCode.ZhCn] = "8 键 (八卦全向 / Standard 8-Way)",
                [LanguageCode.ZhTw] = "8 鍵 (八卦全向 / Standard 8-Way)",
                [LanguageCode.En] = "8 Sectors (Standard 8-Way)",
                [LanguageCode.Ja] = "8キー (全方向8方位)"
            },
            ["SectorCount12"] = new()
            {
                [LanguageCode.ZhCn] = "12 键 (钟表表盘 / Clock Dial 12-Way)",
                [LanguageCode.ZhTw] = "12 鍵 (鐘錶錶盤 / Clock Dial 12-Way)",
                [LanguageCode.En] = "12 Sectors (Clock Dial 12-Way)",
                [LanguageCode.Ja] = "12キー (時計盤12方位)"
            },
            ["ActionTypeHotkey"] = new()
            {
                [LanguageCode.ZhCn] = "⌨️ 键盘快捷键",
                [LanguageCode.ZhTw] = "⌨️ 鍵盤快速鍵",
                [LanguageCode.En] = "⌨️ Keyboard Hotkey",
                [LanguageCode.Ja] = "⌨️ キーボードショートカット"
            },
            ["ActionTypeLaunch"] = new()
            {
                [LanguageCode.ZhCn] = "🚀 启动程序/打开网页",
                [LanguageCode.ZhTw] = "🚀 啟動程式/開啟網頁",
                [LanguageCode.En] = "🚀 Launch App / Open URL",
                [LanguageCode.Ja] = "🚀 アプリ起動 / Webを開く"
            },
            ["ActionTypeSystem"] = new()
            {
                [LanguageCode.ZhCn] = "⚙️ 系统控制指令",
                [LanguageCode.ZhTw] = "⚙️ 系統控制指令",
                [LanguageCode.En] = "⚙️ System Action",
                [LanguageCode.Ja] = "⚙️ システム制御コマンド"
            },
            ["BtnRecordHotkey"] = new()
            {
                [LanguageCode.ZhCn] = "点击录制热键",
                [LanguageCode.ZhTw] = "點擊錄製快速鍵",
                [LanguageCode.En] = "Click to Record Hotkey",
                [LanguageCode.Ja] = "クリックしてショートカット録画"
            },
            ["BtnBrowseApp"] = new()
            {
                [LanguageCode.ZhCn] = "🔍 选择应用程序...",
                [LanguageCode.ZhTw] = "🔍 選擇應用程式...",
                [LanguageCode.En] = "🔍 Select Application...",
                [LanguageCode.Ja] = "🔍 アプリケーションを選択..."
            },

            // Tab 3: Advanced & System
            ["AdvancedHeader"] = new()
            {
                [LanguageCode.ZhCn] = "系统集成与高级偏好设置",
                [LanguageCode.ZhTw] = "系統整合與進階偏好設定",
                [LanguageCode.En] = "System Integration & Preferences",
                [LanguageCode.Ja] = "システム統合と高度な設定"
            },
            ["LanguageTitle"] = new()
            {
                [LanguageCode.ZhCn] = "界面语言 (Display Language)",
                [LanguageCode.ZhTw] = "介面語言 (Display Language)",
                [LanguageCode.En] = "Display Language",
                [LanguageCode.Ja] = "表示言語 (Display Language)"
            },
            ["LanguageDesc"] = new()
            {
                [LanguageCode.ZhCn] = "选择软件控制台与轮盘的显示语言，支持即时热切换并自动保存。",
                [LanguageCode.ZhTw] = "選擇軟體控制台與輪盤的顯示語言，支援即時熱切換並自動儲存。",
                [LanguageCode.En] = "Select language for StarPie. Applies immediately without restarting.",
                [LanguageCode.Ja] = "StarPieの表示言語を選択します。再起動不要で即時に切り替わります。"
            },

            // Program Picker Dialog
            ["ProgramPickerTitle"] = new()
            {
                [LanguageCode.ZhCn] = "选择程序",
                [LanguageCode.ZhTw] = "選擇程式",
                [LanguageCode.En] = "Select Program",
                [LanguageCode.Ja] = "プログラムを選択"
            },
            ["ProgramPickerHeader"] = new()
            {
                [LanguageCode.ZhCn] = "从已安装的软件和开始菜单中选择",
                [LanguageCode.ZhTw] = "從已安裝的軟體與開始功能表中選擇",
                [LanguageCode.En] = "Select from Installed Apps & Start Menu",
                [LanguageCode.Ja] = "インストール済みアプリやスタートメニューから選択"
            },
            ["ProgramPickerPlaceholder"] = new()
            {
                [LanguageCode.ZhCn] = "搜索软件名称、可执行文件或路径...",
                [LanguageCode.ZhTw] = "搜尋軟體名稱、執行檔或路徑...",
                [LanguageCode.En] = "Search app name, executable, or path...",
                [LanguageCode.Ja] = "アプリ名、実行可能ファイル、またはパスを検索..."
            },
            ["ProgramPickerScanning"] = new()
            {
                [LanguageCode.ZhCn] = "正在智能检索系统中已安装的软件，请稍候...",
                [LanguageCode.ZhTw] = "正在智慧檢索系統中已安裝的軟體，請稍候...",
                [LanguageCode.En] = "Scanning installed programs, please wait...",
                [LanguageCode.Ja] = "インストール済みアプリをスキャンしています..."
            },
            ["BtnManualBrowse"] = new()
            {
                [LanguageCode.ZhCn] = "手动浏览文件...",
                [LanguageCode.ZhTw] = "手動瀏覽檔案...",
                [LanguageCode.En] = "Browse File...",
                [LanguageCode.Ja] = "手動で参照..."
            },
            ["LangZhCn"] = new()
            {
                [LanguageCode.ZhCn] = "🇨🇳 简体中文 (Simplified Chinese)",
                [LanguageCode.ZhTw] = "🇨🇳 簡體中文 (Simplified Chinese)",
                [LanguageCode.En] = "🇨🇳 简体中文 (Simplified Chinese)",
                [LanguageCode.Ja] = "🇨🇳 簡体字中国語 (Simplified Chinese)"
            },
            ["LangZhTw"] = new()
            {
                [LanguageCode.ZhCn] = "🇭🇰/🇹🇼 繁體中文 (Traditional Chinese)",
                [LanguageCode.ZhTw] = "🇭🇰/🇹🇼 繁體中文 (Traditional Chinese)",
                [LanguageCode.En] = "🇭🇰/🇹🇼 繁體中文 (Traditional Chinese)",
                [LanguageCode.Ja] = "🇭🇰/🇹🇼 繁体字中国語 (Traditional Chinese)"
            },
            ["LangEn"] = new()
            {
                [LanguageCode.ZhCn] = "🇺🇸 English (US/UK)",
                [LanguageCode.ZhTw] = "🇺🇸 English (US/UK)",
                [LanguageCode.En] = "🇺🇸 English (US/UK)",
                [LanguageCode.Ja] = "🇺🇸 英語 (English)"
            },
            ["LangJa"] = new()
            {
                [LanguageCode.ZhCn] = "🇯🇵 日本語 (Japanese)",
                [LanguageCode.ZhTw] = "🇯🇵 日本語 (Japanese)",
                [LanguageCode.En] = "🇯🇵 日本語 (Japanese)",
                [LanguageCode.Ja] = "🇯🇵 日本語 (Japanese)"
            },
            ["LangAuto"] = new()
            {
                [LanguageCode.ZhCn] = "🖥️ 跟随系统 (System Default)",
                [LanguageCode.ZhTw] = "🖥️ 跟隨系統 (System Default)",
                [LanguageCode.En] = "🖥️ System Default",
                [LanguageCode.Ja] = "🖥️ システム既定 (System Default)"
            },

            ["StartupTitle"] = new()
            {
                [LanguageCode.ZhCn] = "开机自启动",
                [LanguageCode.ZhTw] = "開機自啟動",
                [LanguageCode.En] = "Run on Windows Startup",
                [LanguageCode.Ja] = "Windows起動時に自動起動"
            },
            ["StartupDesc"] = new()
            {
                [LanguageCode.ZhCn] = "在 Windows 开机登录时静默自启动并在后台托盘驻留。",
                [LanguageCode.ZhTw] = "在 Windows 開機登入時靜默自啟動並在後台托盤駐留。",
                [LanguageCode.En] = "Automatically start StarPie silently minimized to tray on login.",
                [LanguageCode.Ja] = "Windows起動時に自動でタスクトレイに常駐します。"
            },
            ["MemoryTitle"] = new()
            {
                [LanguageCode.ZhCn] = "极简内存优化 (Working Set Trim)",
                [LanguageCode.ZhTw] = "極簡記憶體最佳化 (Working Set Trim)",
                [LanguageCode.En] = "Memory Optimization",
                [LanguageCode.Ja] = "メモリ最適化 (ワーキングセット圧縮)"
            },
            ["MemoryDesc"] = new()
            {
                [LanguageCode.ZhCn] = "启用 Windows 进程工作集深度修剪，后台常驻内存低至 15~25MB。",
                [LanguageCode.ZhTw] = "啟用 Windows 行程工作集深度修剪，後台常駐記憶體低至 15~25MB。",
                [LanguageCode.En] = "Deep trims working set, keeping background RAM usage under 20MB.",
                [LanguageCode.Ja] = "メモリを自動トリムし、バックグラウンド使用量を15〜25MBに維持します。"
            },
            ["BtnTrimMemory"] = new()
            {
                [LanguageCode.ZhCn] = "立即压缩物理内存",
                [LanguageCode.ZhTw] = "立即壓縮實體記憶體",
                [LanguageCode.En] = "Trim RAM Now",
                [LanguageCode.Ja] = "今すぐメモリ圧縮"
            },
            ["ElevateTitle"] = new()
            {
                [LanguageCode.ZhCn] = "管理员权限提升 (Run as Admin)",
                [LanguageCode.ZhTw] = "系統管理員權限提升 (Run as Admin)",
                [LanguageCode.En] = "Run as Administrator",
                [LanguageCode.Ja] = "管理者権限で実行"
            },
            ["ElevateDesc"] = new()
            {
                [LanguageCode.ZhCn] = "以管理员身份重启，可在任务管理器、系统设置等高权限窗口中正常唤起手势。",
                [LanguageCode.ZhTw] = "以系統管理員身分重啟，可在工作管理員、系統設定等高權限視窗中正常呼出手勢。",
                [LanguageCode.En] = "Relaunch with administrator privileges to interact with elevated windows.",
                [LanguageCode.Ja] = "管理者権限で再起動し、タスクマネージャー等の高権限画面でも動作可能にします。"
            },
            ["BtnElevate"] = new()
            {
                [LanguageCode.ZhCn] = "🛡️ 以管理员身份重启",
                [LanguageCode.ZhTw] = "🛡️ 以系統管理員身分重啟",
                [LanguageCode.En] = "🛡️ Restart as Administrator",
                [LanguageCode.Ja] = "🛡️ 管理者として再起動"
            },
            ["BackupTitle"] = new()
            {
                [LanguageCode.ZhCn] = "配置备份与恢复 (Backup & Reset)",
                [LanguageCode.ZhTw] = "配置備份與恢復 (Backup & Reset)",
                [LanguageCode.En] = "Backup & Reset",
                [LanguageCode.Ja] = "バックアップとリセット"
            },
            ["BtnExportConfig"] = new()
            {
                [LanguageCode.ZhCn] = "导出配置备份",
                [LanguageCode.ZhTw] = "匯出配置備份",
                [LanguageCode.En] = "Export Backup",
                [LanguageCode.Ja] = "設定をエクスポート"
            },
            ["BtnImportConfig"] = new()
            {
                [LanguageCode.ZhCn] = "导入配置文件",
                [LanguageCode.ZhTw] = "匯入設定檔",
                [LanguageCode.En] = "Import Config",
                [LanguageCode.Ja] = "設定をインポート"
            },
            ["BtnResetConfig"] = new()
            {
                [LanguageCode.ZhCn] = "恢复出厂设置",
                [LanguageCode.ZhTw] = "恢復原廠設定",
                [LanguageCode.En] = "Restore Factory Defaults",
                [LanguageCode.Ja] = "初期設定にリセット"
            },

            // Tab 4: About & Updates
            ["AboutHeader"] = new()
            {
                [LanguageCode.ZhCn] = "关于 StarPie & 版本记录",
                [LanguageCode.ZhTw] = "關於 StarPie & 版本記錄",
                [LanguageCode.En] = "About StarPie & Changelog",
                [LanguageCode.Ja] = "StarPie について & 更新履歴"
            },
            ["AboutDesc"] = new()
            {
                [LanguageCode.ZhCn] = "高质感、极速现代 Windows 鼠标轮盘笔势工具",
                [LanguageCode.ZhTw] = "高質感、極速現代 Windows 滑鼠輪盤手勢工具",
                [LanguageCode.En] = "High-aesthetic, ultra-fast modern Windows mouse radial gestures tool.",
                [LanguageCode.Ja] = "洗練されたデザインと高速な応答性を誇る次世代マウスジェスチャーツール"
            },
            ["BtnOpenChangelog"] = new()
            {
                [LanguageCode.ZhCn] = "查看完整 CHANGELOG",
                [LanguageCode.ZhTw] = "檢視完整 CHANGELOG",
                [LanguageCode.En] = "View Full CHANGELOG",
                [LanguageCode.Ja] = "完全な更新履歴を表示"
            },
            ["MilestonesTitle"] = new()
            {
                [LanguageCode.ZhCn] = "版本演进里程碑 (Milestones)",
                [LanguageCode.ZhTw] = "版本演進里程碑 (Milestones)",
                [LanguageCode.En] = "Version Milestones",
                [LanguageCode.Ja] = "バージョン履歴"
            },

            // Dialogs & System Tray
            ["MsgSaveSuccess"] = new()
            {
                [LanguageCode.ZhCn] = "设置已成功保存至硬盘！",
                [LanguageCode.ZhTw] = "設定已成功儲存至硬碟！",
                [LanguageCode.En] = "Settings successfully saved to disk!",
                [LanguageCode.Ja] = "設定が正常に保存されました！"
            },
            ["MsgConfirmDeletePreset"] = new()
            {
                [LanguageCode.ZhCn] = "确定要永久删除此自定义配色方案吗？\n删除后不可恢复。",
                [LanguageCode.ZhTw] = "確定要永久刪除此自訂配色方案嗎？\n刪除後不可恢復。",
                [LanguageCode.En] = "Are you sure you want to delete this custom color preset?\nThis cannot be undone.",
                [LanguageCode.Ja] = "このカスタム配色プリセットを削除してもよろしいですか？\n削除後は復元できません。"
            },
            ["MsgConfirmReset"] = new()
            {
                [LanguageCode.ZhCn] = "确定要恢复出厂默认设置吗？所有自定义手势与样式将被重置。",
                [LanguageCode.ZhTw] = "確定要恢復原廠預設設定嗎？所有自訂手勢與樣式將被重設。",
                [LanguageCode.En] = "Are you sure you want to restore factory defaults? All customizations will be reset.",
                [LanguageCode.Ja] = "工場出荷時の初期設定に戻してもよろしいですか？すべてのカスタム設定がリセットされます。"
            },
            ["TrayPause"] = new()
            {
                [LanguageCode.ZhCn] = "⏸️ 暂停手势",
                [LanguageCode.ZhTw] = "⏸️ 暫停手勢",
                [LanguageCode.En] = "⏸️ Pause Gestures",
                [LanguageCode.Ja] = "⏸️ ジェスチャーを一時停止"
            },
            ["TrayResume"] = new()
            {
                [LanguageCode.ZhCn] = "▶️ 恢复手势",
                [LanguageCode.ZhTw] = "▶️ 恢復手勢",
                [LanguageCode.En] = "▶️ Resume Gestures",
                [LanguageCode.Ja] = "▶️ ジェスチャーを再開"
            },
            ["TrayPreferences"] = new()
            {
                [LanguageCode.ZhCn] = "⚙️ 偏好设置 (Settings)",
                [LanguageCode.ZhTw] = "⚙️ 偏好設定 (Settings)",
                [LanguageCode.En] = "⚙️ Preferences (Settings)",
                [LanguageCode.Ja] = "⚙️ 環境設定 (Settings)"
            },
            ["TrayAppearance"] = new()
            {
                [LanguageCode.ZhCn] = "🎨 外观与形态 (Appearance)",
                [LanguageCode.ZhTw] = "🎨 外觀與形態 (Appearance)",
                [LanguageCode.En] = "🎨 Appearance & Shapes",
                [LanguageCode.Ja] = "🎨 外観と形状"
            },
            ["TrayGestures"] = new()
            {
                [LanguageCode.ZhCn] = "⚡ 手势与动作 (Mappings)",
                [LanguageCode.ZhTw] = "⚡ 手勢與動作 (Mappings)",
                [LanguageCode.En] = "⚡ Gestures & Actions",
                [LanguageCode.Ja] = "⚡ ジェスチャーと動作"
            },
            ["TrayAbout"] = new()
            {
                [LanguageCode.ZhCn] = "📋 更新日志与关于 (About)",
                [LanguageCode.ZhTw] = "📋 更新日誌與關於 (About)",
                [LanguageCode.En] = "📋 About & Changelog",
                [LanguageCode.Ja] = "📋 情報と更新履歴"
            },
            ["TrayElevate"] = new()
            {
                [LanguageCode.ZhCn] = "🛡️ 以管理员身份重启",
                [LanguageCode.ZhTw] = "🛡️ 以系統管理員身分重啟",
                [LanguageCode.En] = "🛡️ Restart as Administrator",
                [LanguageCode.Ja] = "🛡️ 管理者として再起動"
            },
            ["TrayExit"] = new()
            {
                [LanguageCode.ZhCn] = "❌ 退出 StarPie",
                [LanguageCode.ZhTw] = "❌ 退出 StarPie",
                [LanguageCode.En] = "❌ Exit StarPie",
                [LanguageCode.Ja] = "❌ StarPie を終了"
            },
            ["TrayTooltip"] = new()
            {
                [LanguageCode.ZhCn] = "StarPie v1.3.8 - 现代化鼠标轮盘笔势",
                [LanguageCode.ZhTw] = "StarPie v1.3.8 - 現代化滑鼠輪盤手勢",
                [LanguageCode.En] = "StarPie v1.3.8 - Modern Mouse Radial Gestures",
                [LanguageCode.Ja] = "StarPie v1.3.8 - 次世代マウスラジアルジェスチャー"
            }
        };
    }
}

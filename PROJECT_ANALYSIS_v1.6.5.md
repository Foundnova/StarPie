# StarPie 项目现状分析报告

> **调研日期**：2026-09-04
> **代码基线**：`main` @ `d639c5d`
> **当前版本**：**v1.6.5**（2026-09-03 发布）
> **调研方式**：三路并行只读探索（源码架构 / 版本与文档 / 技术债），未修改任何源码或现有文档
> **性能红线口径**：沿用 `AGENTS.md §1.2` 原文

---

## 目录

1. [项目画像](#1-项目画像)
2. [核心链路剖析](#2-核心链路剖析)
3. [风险清单](#3-风险清单)
4. [工程外围状态](#4-工程外围状态)
5. [未完成功能对账（PRD）](#5-未完成功能对账-prd)
6. [建议路线](#6-建议路线)
7. [附录：关键文件索引](#7-附录关键文件索引)

---

## 1. 项目画像

### 1.1 一句话结论

> **核心引擎质量高，工程外围在塌方。**
> 手势主链路的线程模型、背压合并、代数守卫都达到了相当水准；但配置层无锁、更新链无校验、测试停在 v1.4.3、文档落后 13 个版本。继续只堆功能而不止血，风险会加速累积。

### 1.2 规模总览

| 维度 | 数值 |
|---|---|
| 技术栈 | .NET 8 WPF（`net8.0-windows`），**零 NuGet 依赖**，纯 BCL + P/Invoke |
| 程序集名 | `StarPie`（目录名仍为 `WinPieGestures`） |
| 源文件 | 72 个（62 `.cs` + 10 `.xaml`） |
| 代码行数 | **36,094 行** |
| 最大文件 | `SettingsWindow.xaml.cs` — **10,524 行**（342 个方法） |
| 语言版本 | C# 12（由 TFM 推导，未显式设置 `LangVersion`） |
| 配置位置 | `%LOCALAPPDATA%\StarPie\config.json`（JSON，97 个顶层键） |
| 日志位置 | `%LOCALAPPDATA%\StarPie\logs\starpie_yyyy-MM-dd.log`（7 天轮转） |
| 测试 | Python + pywinauto GUI E2E，19 例，**最新只覆盖到 v1.4.3** |
| CI | `.github/workflows/build-and-test.yml` — 名为 "Build and Test"，实际只做 build + publish Standalone，**不跑测试** |

### 1.3 代码体量分布

呈**极端长尾分布**：11 个超过 800 行的文件占总量的 **95.6%**，而中位数文件仅 108 行。

| 行数 | 文件 | 性质 |
|---:|---|---|
| **10,524** | `SettingsWindow.xaml.cs` | God Object，342 个方法 |
| **3,204** | `SettingsWindow.xaml` | 巨型 XAML，与代码隐藏强耦合 |
| **2,367** | `I18n.cs` | 4 语言 × 312 key 字典 |
| **2,334** | `RadialWindow.xaml.cs` | 渲染 + 动画 + 二级轮盘三合一 |
| **1,487** | `ActionExecutor.cs` | 动作分派 + SendInput 执行引擎 |
| **1,431** | `GestureController.cs` | 手势状态机 + 极坐标命中 |
| **1,221** | `IconHelper.cs` | 47 个内置矢量图标 + Shell 图标提取 |
| **1,067** | `WindowTiler.cs` | 窗口平铺布局计算 |
| **1,027** | `SlotViewModel.cs` | 扇区槽位 VM |
| **987** | `WindowTaskbarHelper.cs` | 任务栏 TBBUTTON 逆向 + 虚拟桌面 COM |
| **862** | `ProgramPickerWindow.xaml.cs` | 软件检索器（模糊 + 拼音 + MRU） |

> 前 2 个文件（`SettingsWindow` 的 cs + xaml，13,728 行）独占 **38.0%** 的代码量。

---

## 2. 核心链路剖析

### 2.1 四阶段主链路

```
[钩子线程 StarPie.MouseHook]
  MouseHook.HookCallback                      MouseHook.cs:351
    ↓
  GestureController.Hook_OnTriggerButtonDown   GestureController.cs:346
    ├─ 隔离检查 CheckIsIsolated                 GestureController.cs:278
    ├─ DPI 采样 GetMonitorDpiScale              RadialWindow.xaml.cs:75
    └─ e.Handled = true（吞掉原生右键按下）
    ↓
  Hook_OnMouseMove                             GestureController.cs:1073
    ├─ 越阈判定（dist ≥ DragThreshold，默认 25）
    ├─ GetProfileForProcess                     ConfigManager.cs:208
    └─ Dispatcher.BeginInvoke(Normal)
         ──► [UI 线程] ShowRadialUI             GestureController.cs:1253
                  new RadialWindow              RadialWindow.xaml.cs:292
                  RenderSectors                 RadialWindow.xaml.cs:667
    ↓
  ProcessMove（高频）                           GestureController.cs:1160
    ├─ 极坐标命中：angle = Atan2(dy, dx)
    └─ QueueHighlightUpdate（背压合并）         GestureController.cs:184
         ──► [UI 线程] RadialWindow.HighlightSector  RadialWindow.xaml.cs:1426
    ↓
  Hook_OnTriggerButtonUp                       GestureController.cs:818
    ├─ EndActiveGesture()（原子快照 + 版本自增）
    └─ ActionExecutor.EnqueueAction             GestureController.cs:907
         ──► [ActionExecutor 线程] Execute      ActionExecutor.cs:235
                  SendInput（P/Invoke）          ActionExecutor.cs:169
```

### 2.2 线程模型

| 线程 | 名称 / 优先级 | 职责 |
|---|---|---|
| UI 线程 | 主 Dispatcher | 轮盘渲染、高亮动画、设置控制台 |
| 鼠标钩子 | `StarPie.MouseHook` / AboveNormal | `WH_MOUSE_LL` 采集 + 独立消息泵（`MouseHook.cs:277-338`） |
| 键盘钩子 | `StarPie.KeyboardHook` / AboveNormal | `WH_KEYBOARD_LL` 采集 + 独立消息泵（`KeyboardHook.cs:252-311`） |
| 动作执行 | `StarPie.ActionExecutor` / AboveNormal | `Channel` 无界队列消费 + SendInput（`ActionExecutor.cs:177-224`） |
| 日志写入 | `StarPie.Logger` / Lowest | 队列批量刷盘，2 秒超时（`AppLogger.cs:46-52`） |

### 2.3 设计亮点（重构时务必保留）

以下六处是本项目最值得肯定的工程实践，**任何重构都不应破坏**：

| # | 亮点 | 位置 | 说明 |
|---|---|---|---|
| 1 | **三线程分离 + 独立消息泵** | `MouseHook.cs:277`、`KeyboardHook.cs:252`、`ActionExecutor.cs:177` | 钩子线程先 `PeekMessage(PM_NOREMOVE)` 强制建队列再 `SetWindowsHookEx`，回调内零耗时 IO，符合 AGENTS.md §1.2 红线 |
| 2 | **自注入事件快速放行** | `MouseHook.cs:86, 360-364` | `StarPieExtraInfo = 0x53544152` 签名，凡 `dwExtraInfo` 匹配者直接 `CallNextHookEx`，杜绝自捕获死循环 |
| 3 | **高亮更新背压合并** | `GestureController.cs:184-233` | 只保留**一份** pending 状态并覆盖，`shouldSchedule = !_highlightUpdateScheduled` 确保每帧最多一次 UI 投递。1000Hz/8000Hz 鼠标也不会打爆 Dispatcher |
| 4 | **双代数守卫** | `GestureController.cs:125-174`、`745-772` | `_gestureVersion` 防跨手势串扰；长按定时器另有独立的 `_longPressGeneration` 防旧回调 |
| 5 | **蜂窝扇最近邻夹角命中** | `GestureController.cs:1366-1422` | 严格用极坐标夹角绝对距离最近邻，而非欧氏圆心距离，杜绝左右颠倒 |
| 6 | **硬件扫描码注入** | `ActionExecutor.cs:1199-1226`、`:875-904` | `MapVirtualKey(MAPVK_VK_TO_VSC)` 转换 + `KEYEVENTF_EXTENDEDKEY` 标志 + 修饰键 10~15ms 保持时延，解决 Photoshop / Illustrator / CAD 丢修饰键问题 |

另有两处细节同样值得保留：
- **轨迹浮层绘制顺序**：`GestureController.cs:536-541` 先 `Clear()` → 画起点 → 再 `Show()`，规避 `Show()` 触发嵌套 `WM_PAINT` 导致旧内容闪现。
- **长按 vs 拖动优先级**：`GestureController.cs:1122` 一旦拖动越阈立即 `CancelLongPressTimer()`，拖动优先于长按，符合直觉。

### 2.4 动作执行分派表

`ActionExecutor.Execute()`（`:235-279`）支持 15 种动作类型：

| 类型 | 处理方法 | 行号 |
|---|---|---:|
| `Launch` | `ExecuteLaunch` | `:646` |
| `Folder` / `OpenFolder` | `ExecuteFolder` | `:602` |
| `Hotkey` | `ExecuteHotkey` | `:847` |
| `Command` | `ExecuteCommand`（cmd / powershell / wsl / 无终端） | `:745` |
| `SwitchWindow` | `ExecuteSwitchWindow` | `:795` |
| `Tile` / `TileRestore` / `MoveMonitor` / `ToggleTopmost` / `WindowOpacity` | `WindowTiler.*` | `WindowTiler.cs:221 / 324 / 599 / 698 / 725` |
| `Text` / `String` | `SendTextInput` | `:1169` |
| `WebUrl` / `Url` | `ExecuteWebUrl`（默认 / Chrome / Edge / Firefox / 自定义） | `:506` |
| `System` | `ExecuteSystem`（50+ 预设） | `:922` |

---

## 3. 风险清单

> 每条均给出**位置（文件:行号）+ 问题描述 + 影响 + 建议**。

### 3.1 🔴 高危（4 项）

#### H1 · 配置全局裸读写，跨线程无同步

| 项 | 内容 |
|---|---|
| **位置** | `ConfigManager.cs:18`（`public static AppConfig CurrentConfig { get; private set; }`）、`MouseHook.cs:419`、`GestureController.cs:1118/1171/1186/1207` |
| **问题** | `CurrentConfig` 是**无锁、非 volatile、非不可变**的静态属性。UI 线程执行 `Profiles.Insert`（`ConfigManager.cs:229`）、整体换引用（`:463`）；**钩子线程**每次按键读 `Trigger.MouseButton`（`MouseHook.cs:419`），并在 `ProcessMove` 中高频读 `DragThreshold`/`CoreRadius`/`WheelRadius`/`SubWheelTriggerDistance` |
| **影响** | `Profiles` 是 `List<WheelProfile>`。`List.Insert` 与 `List.Find` 并发会抛 `InvalidOperationException`；`JsonSerializer.Serialize` 遍历中集合被改会直接抛异常并**中断保存**。叠加 `SaveConfig()` 的裸 `File.WriteAllText`（无原子写、无备份），写一半崩溃会留下截断的 `config.json`，而 `LoadConfig` 的异常兜底会**静默回落默认配置** —— 用户配置全部丢失且无任何提示 |
| **建议** | 改为 `volatile AppConfig` + 不可变快照（写入时构造新对象再原子替换）；或引入 `ReaderWriterLockSlim` 包裹所有读写与序列化。最小改动：给 `SaveConfig`/`LoadConfig`/`Profiles` 变更加统一锁，并将 `SaveConfig` 改为「临时文件 + `File.Replace`」原子写 |

#### H2 · 自动更新无完整性校验

| 项 | 内容 |
|---|---|
| **位置** | `UpdateManager.cs:112-113`（第三方代理）、`:286-296`（下载落盘）、`:347-372`（拼接 `.cmd` 脚本）、`:379-389`（执行） |
| **问题** | 支持 `ghproxy.net` / `github.moeyy.xyz` 第三方镜像；下载 zip 后**无 SHA256、无签名、无发布资产摘要校验**；直接 `tar -xf` 覆盖安装目录，再 `Process.Start(cmd.exe /c script)` 并以 `UseShellExecute = true` 重启 |
| **影响** | 典型供应链攻击面。镜像被劫持或 Release 资产被替换时，恶意 zip 会被解压到安装目录并以当前用户权限执行 |
| **建议** | ① 下载后校验 SHA256（可在 Release 附件放 `.sha256` 并对齐 `tag_name`）；② 优先官方源，代理仅作降级；③ 解压到临时目录校验通过后再覆盖；④ 避免 shell 脚本拼接，改用参数化 `Process.Start` |

#### H3 · 全局异常处理器无条件吞掉所有异常

| 项 | 内容 |
|---|---|
| **位置** | `App.xaml.cs:182-192` |
| **问题** | `App_DispatcherUnhandledException` 中记录日志后**无条件** `e.Handled = true`，外层再套一个 `catch { }` |
| **影响** | 任何未处理异常后应用继续运行，状态可能已损坏（如 `_radialWindow` 未清理、`_isGestureActive` 卡死导致**轮盘再也呼不出来**）。与 L1 的 24 处空 catch 叠加，真实缺陷几乎不可能暴露 |
| **建议** | 仅对白名单异常（已知的 COM/Dispatcher 关闭）设 `Handled = true`；其余记录后弹出错误提示，并强制重置手势状态机或优雅退出 |

#### H4 · 钩子事件用匿名委托订阅，永久无法注销

| 项 | 内容 |
|---|---|
| **位置** | `SettingsWindow.xaml.cs:4465`、`SettingsWindow.xaml.cs:4480` |
| **问题** | `App.MainMouseHook.OnRawMouseButtonEvent += delegate(object? s, RawMouseEventArgs e) {...}`。全项目搜索对应事件的 `-=` **结果为 0**，匿名委托无法退订 |
| **影响** | 钩子是随 `App` 存活的全局对象，强引用 `SettingsWindow` → 窗口无法 GC；回调体内的 `Dispatcher.BeginInvoke` 在窗口/Dispatcher 关闭后会抛异常。虽因 `SettingsWindow` 当前是单例且关闭时只 `Hide()`（`:1810-1829`）使泄漏有界，但这是结构性地雷 |
| **建议** | 将委托存为字段，在 `Window_Closing` / `OnExit` 中 `-=`；或改用 `WeakEventManager` |

---

### 3.2 🟡 中危（6 项）

#### M1 · 轮盘高亮每帧重建动画对象，且无索引短路

| 项 | 内容 |
|---|---|
| **位置** | `RadialWindow.xaml.cs:1426`（`HighlightSector(int, int, bool)` 主实现） |
| **问题** | 方法入口**没有** `mainIndex == currentHighlightedSector && subIndex == currentHighlightedSubSector` 的短路判断。每帧无条件：新建 `CubicEase`（`:1443`）+ 2 个 `Duration`（`:1446`、`:1449`）+ 多个 `DoubleAnimation`；对 `CoreScale` 无条件 `BeginAnimation`（`:1451-1462`）；`mainIndex == -1` 时每帧 `new SolidColorBrush`（`:1465`）。此外每帧对 touched panel 执行 `OfType<TextBlock>().FirstOrDefault()` 视觉树 LINQ 遍历（`:1509-1510`、`:1543-1544`）。渲染器每次还 `new DropShadowEffect`（`ClassicRingRenderer.cs:95`、`CleanSectorsRenderer.cs:47`、`GlassmorphismRenderer.cs:63/73`） |
| **影响** | `CoreScale` 动画被反复重启、**永不收敛**，产生可感知的视觉抖动；持续的对象分配增加 GC 压力。因 `QueueHighlightUpdate` 的背压合并已将调用限制在每帧一次（约 60 次/秒），不是灾难，但属明确的质量缺陷 |
| **建议** | ① 在 `HighlightSector` 入口加索引相等短路（`mainIndex`/`subIndex`/`showSubTier` 三者均未变则直接 return）；② `CubicEase`/`Duration`/`Brush`/`DropShadowEffect` 提为字段缓存并 `Freeze()`；③ `CoreScale` 动画仅在 `mainIndex` 变化时启动 |

#### M2 · 画刷几乎全部未 Freeze（**违反 AGENTS.md §1.2 明文红线**）

| 项 | 内容 |
|---|---|
| **位置** | 全项目 `new SolidColorBrush` 共 **108 处**；`.Freeze()` 调用共 25 处，其中**仅 1 处**（`AppThemeManager.cs:123`）作用于 `SolidColorBrush`，其余 24 处是 `BitmapSource` / `StreamGeometry` / `Pen` 等 |
| **关键位置** | `BaseStyleRenderer.cs:133-136` 的 `CreateSolidBrush(string hex)` **从不 Freeze**，却被调用 8 次初始化渲染器字段（`:111-117`）；`RadialWindow.xaml.cs:397/401/405/409/413`（子轮盘 5 个画刷）、`:577/761/1232/2147`（文本色画刷）；`GestureTrailOverlay.cs:74/80/91/92`（高频轨迹绘制） |
| **分布** | `SettingsWindow.xaml.cs` 61 处、`RadialWindow.xaml.cs` 12 处、`HotkeyRecorderBox.cs` 9 处、`ScreenEyedropperOverlay.cs` 5 处、`GestureTrailOverlay.cs` 4 处、`CatPawRenderer.cs` 4 处、`GlassmorphismRenderer.cs` 3 处、`ColorPickerWindow.xaml.cs` 3 处、`ClassicRingRenderer.cs` 3 处、`WheelPositionIndicator.cs` 2 处、`BaseStyleRenderer.cs` 1 处、`AppThemeManager.cs` 1 处 |
| **影响** | 未冻结的 `Freezable` 保留依赖属性变更通知链，增加渲染线程负担与内存抖动。`AGENTS.md:30` 明确要求「绘图画刷、笔刷必须显式调用 `Freezable.Freeze()` 消除内存泄漏与 GC 抖动」—— 这是本项目自定的工程红线，当前大面积违反 |
| **建议** | `CreateSolidBrush` 内统一 `brush.Freeze(); return brush;`（渲染器画刷语义上只读，1 行改动覆盖大部分调用点）；所有构造后不再修改的画刷/几何体统一 Freeze；`RadialWindow.xaml.cs:1465` 的每帧创建改为静态只读冻结画刷 |

#### M3 · 国际化硬编码覆盖缺口

| 项 | 内容 |
|---|---|
| **位置** | `SettingsWindow.xaml`（656 行含中文）、`HotkeyBuilderDialog.xaml`（39 行）、`WindowPickerWindow.xaml`（22 行）、`SubActionEditorWindow.xaml`（20 行）等 |
| **问题** | **字典本身是完整的**：`I18n.cs` 共 312 key，四语言（zh-CN / zh-TW / en / ja）全部齐全，`GetString` 有 zh-CN 兜底（`:162-176`）。但 UI 侧存在大量硬编码：`SettingsWindow.xaml` 中 **504 行中文无 `x:Name`，无法本地化**；另有 **26 个有名元素**在 `.cs` 中从未赋值（`AdvancedPageSubheader`、`AutoStartAsAdminTitleText`、`OuterEscapeCheckboxDescText`、`AddProfileBtn2` 等）；C# 中硬编码中文字面量（不含 I18n.cs）在 `SettingsWindow.xaml.cs` 有 251 行、`SlotViewModel.cs` 132 行、`IconHelper.cs` 94 行 |
| **机制缺陷** | `I18n.LanguageChanged` 事件全项目**只有 1 个订阅者**（`SlotViewModel.cs:942`，并在 `:1019` 正确 `-=`）。`SettingsWindow` 未订阅，靠手动调 `ApplyLocalization()`（`:953`）；其他窗口（ColorPicker / HotkeyBuilder / WindowPicker / IconPicker / InputDialog）**完全无刷新路径** |
| **建议** | ① 为 504 行无名中文补 `x:Name` 并接入 `I18n.T`；② 为 26 个有名元素补赋值；③ 各窗口改为订阅 `I18n.LanguageChanged` 而非手动调用；④ `MessageBox.Show` 硬编码中文替换为 `I18n.T(...)` |

#### M4 · 单实例 Mutex 创建失败时「失败即放行」

| 项 | 内容 |
|---|---|
| **位置** | `App.xaml.cs:97-102` |
| **问题** | `try { new Mutex(initiallyOwned: true, MutexName, out createdNew); } catch { createdNew = true; }` —— fail-open：Mutex 创建失败（权限不足、会话隔离）时视为「我是首实例」 |
| **影响** | 两个实例同时安装全局低级鼠标钩子，并并发写同一份 `config.json`，可致配置损坏与手势行为错乱。与 H1 叠加风险放大 |
| **建议** | 失败时提示用户「无法确认单实例状态」并退出，或降级为不启用手势仅打开设置；至少记录 `AppLogger.LogError` |

#### M5 · 死代码：CatPawRenderer 完全未使用

| 项 | 内容 |
|---|---|
| **位置** | `CatPawRenderer.cs`（314 行，13.7 KB） |
| **问题** | `StyleRendererFactory.cs:5-17` 的 switch 只注册了 `Glassmorphism` / `CleanSectors` / 默认 `ClassicRingRenderer`，不含 `CatPawRenderer`；全项目对该类的引用仅其自身定义处 |
| **影响** | 无功能影响，但该文件内的 4 处 `new SolidColorBrush` 与 4 处 `new DropShadowEffect` 计入 M2 统计，且是维护噪音（v1.4.0 已移除「萌宠猫爪」主题） |
| **建议** | 可安全删除 |

#### M6 · P/Invoke 结构体大量重复定义

| 项 | 内容 |
|---|---|
| **位置** | `POINT` ×8（`MouseHook.cs:12`、`KeyboardHook.cs:25`、`GestureController.cs:11`、`GestureTrailOverlay.cs:31`、`ScreenEyedropperOverlay.cs:13`、`RadialWindow.xaml.cs:24`、`WindowPickerWindow.xaml.cs:79`、`WindowTiler.cs:34`）；`RECT` ×4；`MONITORINFO` ×4；`MSG` ×2；`INPUT`/`KEYBDINPUT`/`InputUnion` 各 ×2 |
| **问题** | 均为**嵌套** `private struct`（仅两处为嵌套 `public struct POINT`），不构成编译冲突，因此**不是编译错误** |
| **影响** | 纯维护债务：新增 P/Invoke 时容易复制粘贴出错 |
| **建议** | 抽取 `internal static class NativeStructs`（或 `NativeMethods`）统一定义 |

---

### 3.3 🟢 低危（7 项）

| # | 问题 | 位置 | 说明 / 建议 |
|---|---|---|---|
| L1 | **24 处空 catch 吞异常** | `ActionExecutor.cs:1130/1142/1154`、`RadialWindow.xaml.cs:2176`、`SettingsWindow.xaml.cs:2756/3227/3652/3676/3801/3822/3843/3865/3964`、`AppLogger.cs:43/93/158/162/218`、`UpdateManager.cs:283`、`WindowPickerWindow.xaml.cs:106/112/217/236/276` | 全项目 `catch` 共 245 处，空 catch 24 处。**关机（`shutdown.exe /s /t 0`，`:1154`）与重启（`:1142`）失败完全静默**，用户无任何反馈；`Geometry.Parse` 解析 SVG path 失败（`:2176`）导致图标不显示且难以排查。建议至少这几处补 `AppLogger.LogWarn` 并保留异常对象 |
| L2 | **`MouseHook.Stop()` 超时则钩子永久无法重启** | `MouseHook.cs:259-274` | 若 500ms `Join` 内线程未退出（如卡在 `HookCallback`），`_hookReady` 不 Dispose、`_hookThread` 不置空、`_stopRequested` 保持 true，后续 `Start()` 因 `_hookThread.IsAlive` 直接返回（`:203-206`）。建议超时后仍 Dispose 事件并置空线程引用 |
| L3 | **`Process` 对象未 Dispose** | `ActionExecutor.cs:707`（在 `Task.Run` 中轮询 2 秒，全程未 Dispose）；另有约 23 处 `Process.Start(...)` 直接丢弃返回值 | `Process` 持有内核句柄，依赖 GC 终结器回收。频繁启动动作时句柄累积。建议 `using var p = Process.Start(...)` 或启动后显式 `Dispose()`。对照：`ConfigManager.cs:510` 已正确使用 `using` |
| L4 | **动作队列 `while(true)` + `.Result` + 空 catch** | `ActionExecutor.cs:196-211` | `reader.WaitToReadAsync().AsTask().Result` 同步阻塞（在专用后台线程，**不阻塞 UI**），但 `.Result` 会把异常包装成 `AggregateException`；`:209-211` 的空 catch 使循环永不退出且异常完全被吞。建议改 `GetAwaiter().GetResult()` + 记日志 + `CancellationToken` 支持优雅退出 |
| L5 | **更新脚本路径未转义** | `UpdateManager.cs:347-372` | `currentExe` / `targetDir` / `downloadedZipPath` 未转义直接拼入 `.cmd`，路径含 `"`、`&`、`\|` 时脚本被破坏或被注入。建议做引号/元字符校验，或改用参数化 `Process.Start("tar", ...)` |
| L6 | **版本号硬编码 `v1.6.0`** | `App.xaml.cs:133` | `AppLogger.LogInfo($"=== StarPie v1.6.0 Starting ...")`，实际已是 v1.6.5。建议改从 `Assembly.GetExecutingAssembly().GetName().Version` 或 `AssemblyInformationalVersion` 读取 |
| L7 | **零 .NET 单元测试** | — | 无 `.csproj` 测试项目。`GestureController`（状态机 + 命中测试）、`ConfigManager`（序列化 + 迁移）、`ActionExecutor`、`WindowTiler`、`WindowTaskbarHelper` 全部零覆盖。建议补 xUnit 项目，优先覆盖纯逻辑（命中测试、布局计算、配置迁移） |

### 3.4 ✅ 已排查通过（无需担心）

以下常见 WPF 陷阱**均未发现问题**，说明基础工程质量扎实：

| 检查项 | 结果 |
|---|---|
| 主线程阻塞 / CPU 空转 | 未发现。所有 `Thread.Sleep` 均在后台线程或线程池；最长阻塞 2 秒（等待进程主窗口，`:716-726`）不占用 UI |
| 跨线程直接访问 WPF 对象 | 未发现。轨迹绘制经 `GestureController.cs:709` 的 `DispatchUi` 正确封送 |
| GDI 设备上下文泄漏 | 未发现。`ScreenEyedropperOverlay.cs:205-207` 的 `GetDC`/`ReleaseDC` 严格配对 |
| 流 / 文件资源未释放 | 未发现。`UpdateManager.cs:292-293` 正确使用 `await using` |
| 未 Dispose 的 Timer | 未发现。仅 2 个定时器：长按定时器（`GestureController.cs:745`，在 `:757-761` 正确 Dispose 并置 null，含 generation 校验）；自动保存定时器（`SettingsWindow.xaml.cs:1600`，随窗口存活） |
| 未卸载的全局钩子 | 未发现。`MouseHook.Stop()` / `KeyboardHook.Stop()` 均有明确卸载路径，`finally` 中 `UnhookWindowsHookEx`，`App.xaml.cs:247-248` 在 `OnExit` 中调用。仅存在 L2 的超时分支缺陷 |
| 托盘图标泄漏 | 未发现。`SettingsWindow.xaml.cs:1805-1806` 正确 `Visible = false` + `Dispose()` |
| ViewModel 事件注销 | 未发现。`SlotViewModel.cs:942` 订阅、`:1019` 注销，配对正确 |
| 会导致编译失败的重复类定义 | 未发现。重复项均为嵌套 `private struct` |
| `TODO` / `FIXME` / `HACK` / `NotImplementedException` | 未发现（0 处，双重验证） |

---

## 4. 工程外围状态

### 4.1 文档版本偏差

| 文档 | 声明版本 | 实际 | 偏差 |
|---|---|---|---|
| `WinPieGestures.csproj` | v1.6.5 | v1.6.5 | ✅ 同步 |
| `CHANGELOG.md` | v1.6.5 | v1.6.5 | ✅ 同步 |
| `AGENTS.md` | v1.6.5（表格） | v1.6.5 | ⚠️ 版本对，但见 4.2 |
| `README.md` | **v1.4.2** + "18 项测试" | v1.6.5 / 19 项 | ❌ 落后 **13 个版本** |
| `releases/README.md` | **v1.5.3**（标"最新发布版"） | v1.6.5 | ❌ 落后 12 个版本 |
| `StarPie_v160_Interactive_Demo.html` | v1.6.0 | v1.6.5 | ⚠️ 落后 3 个版本 |
| `PRD.md` | v1.0 时代 | — | ⚠️ 配置路径仍写 `WinPieGestures` |
| `RELEASE_NOTES_v1.4.4.md` | v1.4.4 | — | 历史遗留，已被 CHANGELOG 覆盖 |

### 4.2 `AGENTS.md` 三重内部不一致

作为项目的「唯一权威架构规范」，本文件自身存在三处不一致：

1. **同一章出现 3 个版本号**：目录锚点（`:14`）写 `v1.0.0 ~ v1.5.5`，章节标题（`:199`）写 `v1.0.0 ~ v1.6.0`，表格末行（`:214`）实际到 `v1.6.5`。
2. **路径基准全错**：全文使用 `g:\Users\2 Better\Desktop\design\`，实际为 `D:\IO\dotnet\StarPie`。
3. **架构图缺 18+ 个文件**：§2.1 目录树缺 `AppLogger.cs`、`UpdateManager.cs`、`WindowTiler.cs`、`WindowTaskbarHelper.cs`、`WindowPickerWindow.xaml(.cs)`、`FuzzyMatcher.cs`、`PinyinHelper.cs`、`GestureMapping.cs`、`GestureMappingViewModel.cs`、`SlotViewModel.cs`、`SubSlotViewModel.cs`、`ScreenEyedropperOverlay.cs`、`TriggerConfig.cs` 等；且描述的 `scratch/Decompiler/` 与 `Renderers/` 子目录在当前工作区**均不存在**（渲染器文件平铺在主目录）。

**摘要遗漏**：v1.6.0 摘要遗漏了「底层钩子线程化与按键防粘滞重构」（该版本最核心的架构变更）；v1.6.2 摘要遗漏了「GitHub Releases 在线更新系统」。

### 4.3 事实核查

| 项 | 结论 |
|---|---|
| `m.md` | **不是需求备忘或待办清单**。仅 482 字节 / 26 行，标题 `M♭`，内容为日语歌词及中文翻译，带 Obsidian frontmatter。无任何功能规划价值 |
| `_m44/` | **完全空目录**。无隐藏文件、无子目录、无 git 历史（`git log -- _m44` 无输出）。未被 `.gitignore` 覆盖。推测（**未经证实**）为某次操作的空占位目录。建议清理 |
| `releases/` | **无任何可用二进制**。`.gitignore` 忽略 `*.exe`/`*.dll`/`*.pdb`/`*.zip` 及 releases 产物目录，`v1.6.5/Lightweight/` 只剩 3 个残骸（`StarPie.deps.json`、`StarPie.runtimeconfig.json`、`app_icon.ico`）。仓库内无法获取可运行产物，必须本地 `dotnet publish` |
| 版本号缺口 | CHANGELOG 中 **v1.3.7、v1.6.3、v1.6.4 不存在**（v1.6.2 直接跳到 v1.6.5） |
| 反编译残留 | 10 个文件含 **177 处** `//IL_xxxx: Unknown result type` 注释：`SettingsWindow.xaml.cs` 93 处、`GestureController.cs` 33 处、`IconHelper.cs` 28 处、`RadialWindow.xaml.cs` 9 处等 |
| 手工补丁痕迹 | `GestureController.cs:1212` 的 `if (ConfigManager.CurrentConfig.SubmenuStyle == "Fan")` 缺少缩进，与周围 4 空格制表符风格不一致，疑为后期手工插入的分支 |
| 冗余配置字段 | `AppConfig.AppTheme`（`:57`）与 `AppConfig.Theme`（`:59`）默认值相同；`MouseHook.cs:419` 同时读 `Trigger.MouseButton` 与旧的 `TriggerButton` |

### 4.4 性能红线口径（沿用 AGENTS.md §1.2 原文）

按用户确认，本报告一律以 `AGENTS.md §1.2` 为准：

| 红线 | AGENTS.md 原文 | 出处 |
|---|---|---|
| 空闲内存占用 | **3MB ~ 8MB** | `AGENTS.md:28` |
| 按下到轮盘呈现延迟 | **< 16ms** | `AGENTS.md:32` |
| 盲操触发命中率 | **100%** | `AGENTS.md:37` |
| 画刷必须 Freeze | 「绘图画刷、笔刷必须显式调用 `Freezable.Freeze()`」 | `AGENTS.md:30` |

> ⚠️ **文档待校准（照实记录，本轮不做修改）**
>
> 内存指标在三处文档中互斥，需实测校准后才能统一：
> - `AGENTS.md:28` → 3MB ~ 8MB
> - `CHANGELOG.md`（v1.4.3 条目）→ 15 ~ 25MB
> - `PRD.md:9` → < 50MB
>
> 三者不可能同时成立。任何据此做出的优化决策都应先实测当前真实占用。

**M2 的定性**：以 AGENTS.md 明文要求为准，画刷未 Freeze 属**违反已确立的工程红线**，而非一般风格问题。

---

## 5. 未完成功能对账（PRD）

`PRD.md` 为 v1.0 时代文档（文中项目名仍为 WinPieGestures），共 118 行。

### 5.1 明确声明的 Non-Goals

| # | 未实现项 | PRD 出处 | 当前实际状态 | 判定 |
|---:|---|---|---|---|
| N1 | 全图形化拖拽式轮盘编辑器（规划 v1.1） | `:60-63` | **已实现**：v1.6.1 画布直接拖拽对调扇区/中心核圆 + v1.6.5 二级子动作拖拽对调 | ✅ 已完成 |
| N2 | 多指触控板手势 / 手写笔压感输入 | `:62` | 源码全文检索无任何触控相关实现 | ❌ 未实现（主动放弃项） |
| N3 | 基于云服务的配置跨设备同步 | `:63` | 源码无 `Sync`/`Cloud` 相关实现（所有 `Sync` 命中均为 `_uiUpdateSync` 锁对象） | ❌ 未实现 |

### 5.2 路线图「阶段 3（v1.1 体验提升版）」

| # | 功能项 | PRD 出处 | 当前状态 | 判定 |
|---:|---|---|---|---|
| R1 | 可视化轮盘拖拽配置界面 | `:109-111` | v1.6.1 / v1.6.5 实现 | ✅ 已完成 |
| R2 | **复杂的多按键宏录制** | `:109-111` | 仅有单次组合键拼装（`HotkeyBuilderDialog`）+ `SendTextInput` 文本字符流注入；**无多步动作序列的录制与回放引擎** | ⚠️ 部分实现，仍有缺口 |

### 5.3 量化验收标准（PRD `:7-11`）

| 指标 | PRD 要求 | AGENTS.md 红线 | 验证手段 | 冲突 |
|---|---|---|---|---|
| 轮盘弹出延迟 | < 50ms | < 16ms | 无 | 需实测确认以哪个为准 |
| CPU 占用 | < 0.5% | — | 无 | — |
| 内存占用 | < 50MB | 3~8MB | 无 | ⚠️ **自相矛盾**，见 4.4 |
| 方向识别准确率 | > 99.9% | 100% 盲操命中 | 无 | — |
| 原生右键穿透延迟 | < 10ms | — | 无 | — |

**结论：5 项硬性验收指标全部无自动化验证手段。** 建议建立性能基准（可用 `Stopwatch` 埋点 + `AppLogger` 采集），或将关键指标纳入 CI。

---

## 6. 建议路线

> 本轮**仅交付分析，不执行以下任何一项**。此处供后续开发轮次排期参考。

**核心原则：先止血，再重构，最后谈新功能。**

| 优先级 | 事项 | 涉及条目 | 预估工作量 |
|---|---|---|---|
| **P0 止血** | 配置加锁 + 原子写 / 更新包 SHA256 校验 / 异常处理器收窄 / 钩子事件可注销 | H1、H2、H3、H4 | 小 ~ 中 |
| **P1 性能与规范** | 高亮索引短路 / `CreateSolidBrush` 统一 Freeze / Mutex 显式失败 / 关机重启补日志 | M1、M2、M4、L1 | 极小 ~ 小（M1、M2 收益极高） |
| **P2 工程外围** | 重写 AGENTS.md 架构图与路径 / 同步 README 与 releases README / 清理 `_m44` 与 `CatPawRenderer` / 补 v1.5.x–v1.6.5 测试 | 第 4 章、M5 | 中 |
| **P3 架构重构** | `SettingsWindow.xaml.cs`（10,524 行）拆分为多个 `partial class` 或 UserControl / i18n 补齐 504 行无名中文 / 抽取 `NativeStructs` | M3、M6 | 大 |
| **P4 新功能** | 多按键宏录制引擎（唯一有实际价值的 PRD 缺口） | R2 | 大 |

**关于 P3 的补充建议**：`SettingsWindow.xaml.cs` 的拆分优先级其实应高于 P4。当前 342 个方法挤在一个类里，新增任何功能都在向一个已经 10,524 行的文件里继续堆砌，边际成本持续上升。

**关于 N3（云同步）的特别提示**：该功能与 PRD「纯本地运行、不收集不上传」的**隐私承诺直接冲突**。若确需实现，建议改为「导出到用户自选云盘目录 / WebDAV」，而非自建云服务。

---

## 7. 附录：关键文件索引

```
D:\IO\dotnet\StarPie\
├── WinPieGestures\                            主工程（72 源文件，36,094 行）
│   ├── WinPieGestures.csproj                  <Version>1.6.5</Version>，零 NuGet 依赖
│   ├── App.xaml.cs                            256 行｜单实例 Mutex、Hook 装配（H3、M4、L6）
│   ├── MouseHook.cs                           473 行｜WH_MOUSE_LL + 独立消息泵（H1、L2）
│   ├── KeyboardHook.cs                        449 行｜WH_KEYBOARD_LL + 独立消息泵
│   ├── GestureController.cs                  1,431 行｜状态机 + 极坐标命中 + 背压合并
│   ├── RadialWindow.xaml.cs                  2,334 行｜轮盘渲染 + 高亮动画（M1、M2）
│   ├── ActionExecutor.cs                     1,487 行｜动作分派 + SendInput（L1、L3、L4）
│   ├── ConfigManager.cs                        705 行｜JSON 持久化 + 自启管理（H1）
│   ├── UpdateManager.cs                        405 行｜GitHub Releases 更新（H2、L5）
│   ├── WindowTiler.cs                        1,067 行｜窗口平铺
│   ├── WindowTaskbarHelper.cs                  987 行｜任务栏 TBBUTTON 逆向
│   ├── IconHelper.cs                         1,221 行｜47 矢量图标 + Shell 提取
│   ├── I18n.cs                               2,367 行｜4 语言 × 312 key
│   ├── SettingsWindow.xaml.cs               10,524 行｜God Object（H4、M2、M3）
│   ├── AppConfig.cs                            224 行｜97 个持久化字段
│   └── Renderers（平铺）：IRadialStyleRenderer / BaseStyleRenderer / StyleRendererFactory
│                          ClassicRingRenderer / CleanSectorsRenderer / GlassmorphismRenderer
│                          CatPawRenderer（死代码，M5）
├── CHANGELOG.md                              1,044 行｜38 个版本，最新 v1.6.5
├── AGENTS.md                                   240 行｜架构规范（三重不一致，见 4.2）
├── PRD.md                                      118 行｜v1.0 时代需求文档
├── README.md                                   277 行｜版本声明过期（v1.4.2）
├── m.md                                         26 行｜日语歌词笔记（非需求清单）
├── _m44\                                        【空目录】
├── releases\                                   33 个版本目录，无可用二进制
├── 主题尺寸配置文件\                            3 个 config.json 导出快照（v1 / v3 / v4）
├── tests\
│   ├── conftest.py                             80 行｜pytest fixture + pywinauto 启动
│   └── test_settings.py                      ~900 行｜19 个 E2E 用例，最新覆盖到 v1.4.3
└── StarPie_v160_Interactive_Demo.html      50,402 B｜v1.6.0 交互原型（落后 3 个版本）
```

### 主题尺寸配置文件说明

三份 JSON 均为 `config.json` 的**完整导出快照**，用于一键导入复现某套外观方案：

| 文件 | 大小 | 字段数 | 特征 |
|---|---:|---:|---|
| `轮盘尺寸配置文件（...）.json` | 14,591 B | 79 | 最早：小轮盘 127px、灵敏度 16px、经典扇区 |
| `轮盘尺寸配置文件v3（...）.json` | 15,927 B | 79 | 大轮盘 145px、蜂巢六边形、灵敏度 30px |
| `v4.json` | 15,970 B | **80** | 中轮盘 122px、毛玻璃渲染、二级改 Wheel；**新增 `ShowSelectedActionText`** |

> 精确 diff：v1 与 v3 字段集**完全一致**（仅取值不同，非架构升级）；真正的架构演进只有 v4 新增的 `ShowSelectedActionText`。

---

*本报告基于 2026-09-04 的代码基线快照。所有结论均可追溯至 `文件:行号`。未核实的推测已明确标注。*

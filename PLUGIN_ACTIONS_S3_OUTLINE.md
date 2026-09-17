# StarPie 动作插件化 · S3 大纲（拆包 + 剩余动作外移）

> **本文属于规划，不是现状描述。** 所有「现状」段落都标注了核实点（文件:行），
> 可直接复查；所有「建议」段落都是待拍板的选项，不是已定稿的决定。
> 上级文档：`PLUGIN_FIRST_ROADMAP.md`（边界判据与分期）、`AGENTS.md`（唯一权威工程规范）。

---

## 0. 结论先行

### 0.1 待办的真实规模：7 个动作，不是 8 个

13 个顶层动作类型里已有 5 个外移（`Launch` / `WebUrl`+`Url` / `Folder`+`OpenFolder` /
`Command` / `ShellTool`）。剩下的 8 个中 **`Hotkey` 按设计永久留在宿主**（它是
`ActionItem.Type` 的默认值，也是未配置扇区的占位类型 —— 占位类型必须永远可解析，
理由已写在 `BuiltinActionCatalog.Build()` 的注释里）。

⇒ **实际要外移的是 7 个**：`Tile` / `ToggleTopmost` / `MoveMonitor` / `WindowOpacity` /
`SwitchWindow` / `System` / `Ocr`。

### 0.2 有一个必须先修的地基问题，否则拆包会当场炸

随包插件的**认领声明是「安装那一刻的快照」**，插件升级或文件被替换后
**永远不会刷新**（核实：`PluginHost.cs:425 EnsureBundledPlugin` —— 已登记条目只做
「文件补回」，不更新任何元数据字段）。

拆包的必经动作是「把 `Command`/`ShellTool` 从 A 包挪到 B 包」。而老用户的登记表里
A 包的 `ClaimedTypes` 仍是旧快照、含这两项，B 包也认领同样两项 ⇒
`RebuildClaimTable` 走「多包抢同一类型 → **整对拒绝**」⇒ **两个包的全部动作一起失效**，
且用户侧唯一线索是日志里一条 Error。

**这不是拆包引入的新问题，是既有 bug 被拆包放大成灾难**（同一机制也让「随包插件升级后
新增认领类型」永远不生效）。修复成本很低：`ScanCandidateFile` 每次启动都已经在读 PE
元数据了，只是拿到了没用。

### 0.3 UI 要不要一起改，是必须现在决定的范围问题

已被外移的 5 个动作，**UI 一行未动** —— 它们仍走手写的 `FocusXxxPanel`
（核实：`SettingsWindow.xaml.cs:5094-5102`；统一表单 `PluginParameterForm` 只服务
`Type="Plugin"` 那条路）。

剩余 7 个动作的**手写面板比已外移的那批复杂得多**，装了大量声明式表单表达不了的东西
（二级子模式选择器、Slider、快捷芯片按钮、预设二级轮盘、自定义动作按钮）。
`PLUGIN_FIRST_ROADMAP.md` §6 已经点过这件事：「先得决定 `ParameterField` 要不要补
**Presets / 条件显示 / 自定义动作按钮** 这三样」。

⇒ 本轮建议**继续不动 UI**（与 S1/S2 一致），把「面板统一化」独立排期。
理由写在 §2.2，反方意见也写在 §2.2，不藏。

---

## 1. 现状事实（已核实）

### 1.1 动作清单与宿主依赖

| 动作 | `ActionItem.Type` | 状态 | 宿主依赖（唯一实现入口） |
|---|---|---|---|
| 快捷热键 | `Hotkey` | **永久内建** | `ActionExecutor.ExecuteHotkey` |
| 平铺窗口 | `Tile` | 内建 | `WindowTiler.ExecuteTile` |
| 窗口置顶 | `ToggleTopmost` | 内建 | `WindowTiler.ToggleWindowTopmost` |
| 移到下一屏 | `MoveMonitor` | 内建 | `WindowTiler.MoveWindowToNextMonitor` |
| 窗口透明度 | `WindowOpacity` | 内建 | `WindowTiler.SetWindowOpacity` |
| 切换应用 | `SwitchWindow` | 内建 | `ActionExecutor.ExecuteSwitchWindow` |
| 系统控制 | `System` | 内建 | `ActionExecutor.ExecuteSystem` |
| 屏幕文字 | `Ocr`（别名 `ScreenOcr`） | 内建 | `OcrManager.StartCaptureAndRecognize` |
| （遗留） | `TileRestore` | switch 特例 | `WindowTiler.RestoreLastLayout` |
| （未收敛） | `Text` / `String` | switch 特例 | `ActionExecutor.SendTextInput` |

### 1.2 调用方分析 —— 哪些实现体可以自由改签名

这是外移可行性的硬前提：**只有「唯一调用者就是待外移的那个动作」的实现体，
才可以改签名 / 改失败语义**（S2 处理 `ExecuteCommand` 时用的就是这条判据）。

| 实现体 | 宿主侧其它调用者 | 结论 |
|---|---|---|
| `WindowTiler.ExecuteTile` | 无 | ✅ 可改 |
| `WindowTiler.MoveWindowToNextMonitor` | 无 | ✅ 可改 |
| `WindowTiler.ToggleWindowTopmost` | 无 | ✅ 可改 |
| `WindowTiler.SetWindowOpacity` | 无 | ✅ 可改 |
| `ActionExecutor.ExecuteSwitchWindow` | 无 | ✅ 可改 |
| `ActionExecutor.ExecuteSystem` | 无 | ✅ 可改（但内含 WPF 调用，见 1.4） |
| `WindowTiler.RestoreLastLayout` | `App.xaml.cs:614`（退出还原）、`ActionExecutor.cs:368`（`TileRestore` 特例） | ⚠️ **必须保留原行为** |
| `OcrManager.StartCaptureAndRecognize` | `OcrSettingsDialog` / `SettingsWindow`×2 / `SubActionEditorWindow` 共 4 处 | ⚠️ **必须保留原行为** |
| `ActionExecutor.ExecuteHotkey` | `ActionExecutor` 内部 3 处 + `ExecuteSystem` 内部 20+ 处 | ⚠️ **留在宿主** |
| `WindowTaskbarHelper` | `RadialWindow`（轮盘缩略图）/ `GestureController.Prefetch` / `WindowTiler` | ⚠️ **保留** |

### 1.3 已具备的服务面（无需新增即可复用的部分）

- `IHostActionInvoker`（**既有契约，无门禁**）：`SendHotkey` / `SendText` / `Launch` /
  `OpenFolder` / `OpenUrl` / `SetClipboardText` / `GetClipboardText`
- `IHostCommandService.Run` / `IHostShellService.Invoke`（有门禁，S2 新增）
- `IHostInfo.HasCapability` / `IDispatcherFacade`（UI 线程调度）
- `IPluginContext`：`Me` / `PluginDirectory` / `DataDirectory` / `Log` / `Settings` /
  `Actions` / `I18n` / `Icons` / `Host` / `Commands` / `Shell` / `Info` / `Notify` /
  `Events` / `Dispatcher`

### 1.4 规模数据（决定工作量）

| 项 | 数量 | 位置 |
|---|---|---|
| 布局码 | **17** + 3 个特殊标记（`Cycle` / `CycleBack` / `Restore`） | `WindowTiler.cs:159-171` |
| 系统预设（UI 可选） | **41** | `SlotViewModel.cs:12-344` |
| `ExecuteSystem` 的 `case` 标签 | **63**（其中 **22 个是不在预设表里的历史别名**） | `ActionExecutor.cs:2003-2256` |
| `ExecuteSystem` 的实际分支 | 41 | 同上 |
| `ExecuteSystem` 内的 WPF 调用 | `Application.Current.Dispatcher.BeginInvoke` → `App.ShowSettingsWindow` / `QuickSearchWindow.ShowOrActivate` | `ActionExecutor.cs:2091` / `:2153` |
| Shell 工具（已外移，供对照） | 18 | `ShellActionPickerWindow.xaml.cs` |
| 自检文件 | 2057 行 / 12 段 | `PluginSelfTest.cs` |

> **预设表 ↔ switch 覆盖关系已逐项核对（41/41 全部命中）**，当前不存在
> 「选了没反应」的预设。那 22 个历史别名反而是外移时最容易漏掉的东西 ——
> 它们不在任何 UI 清单里，只活在 `switch` 的 `case` 标签上。

### 1.5 UI 侧现状

- 动作类型下拉有**两份硬编码清单**（`SlotViewModel.cs:804 AggregatedActionTypes` 9 项、
  `:821 LocalizedActionTypes` 13 项）。**Tag 是 `Type` 字符串本身，外移不改 Type ⇒ 不用动。**
- 简洁模式下 5 个窗口动作被聚合成一个 `WindowManager` 项 —— 那是**纯 UI 聚合，不是真实 Type**
  （核实：`GestureMappingViewModel.cs:72/89`，写入配置的始终是具体 Type）。
- 动作参数面板按 `Type` 切换 `FocusXxxPanel`，全部是手写 XAML。

---

## 2. 三个关键发现

### 2.1 【P0 · 拆包前置】随包插件的认领是「安装时快照」

**证据链**

1. `PluginHost.cs:315`（`CommitInstall` 内）是 `ClaimedTypes` 的**唯一写入点**：
   `ClaimedTypes = ClaimWire(manifest, options.Bundled)`
2. `PluginHost.cs:425 EnsureBundledPlugin`：已登记条目（`existing != null`）**直接返回**
   `RestoreBundledPayload`，只判断「宿主区文件还在不在」，不碰任何元数据
3. `PluginHost.cs:548 RebuildClaimTable`：只读 `entry.ClaimedTypes`（登记表快照）
4. `PluginRegistryEntry.ClaimedTypes` 的注释说明这是**刻意设计**（为了启动最早期不读程序集），
   但设计时漏了「文件变了怎么办」

**拆包时的爆炸路径**

```
老用户登记表： basicactions.ClaimedTypes = [Launch=launch, …, Command=command, ShellTool=shellTool]
新版本分发：   basicactions 只声明打开类  +  新增 terminalactions 声明 Command/ShellTool
RebuildClaimTable：两个包抢 Command/ShellTool → "已全部拒绝 —— 这属于打包错误"
结果：两个包的所有动作全部失效（其中 basicactions 的打开类动作也会一起陪葬）
```

**修复方向（建议）**

在 `EnsureBundledPlugin` 里，对「已登记的随包插件」增加一次**元数据刷新**：

| 字段 | 是否刷新 | 理由 |
|---|---|---|
| `ClaimedTypes` | ✅ 用当前文件的 `ClaimWire(manifest, true)` 覆盖 | 认领的事实来源是**程序目录里的那枚 dll**，不是历史快照 |
| `Version` / `Name` / `Description` | ✅ | 顺带修掉「插件页显示的版本号永远停在首次安装那版」 |
| `CapabilitiesAck` | ✅ 同步为当前清单 | 随包插件的能力由发行方决定，用户只能整体停用、无法逐项拒绝 |
| `Enabled` / `Preload` | ❌ **绝不触碰** | 用户的选择 |
| `InstallPath` / `Bundled` / `AckedAt` | ❌ | 身份与安装信息 |
| 磁盘文件 | 仅缺失时补回（现状逻辑保留） | |

只在**值真的变了**时落盘（先比较再写），避免每次启动都重写 `registry.json`。

**不破坏 R1**：`EnsureBundledPlugin` 调用的 `ScanCandidateFile` → `PluginScanner.ScanSelectedDll`
本就是「纯静态读 PE 元数据、不执行任何插件代码」，刷新字段不引入程序集加载。

**备选方案**：把认领来源从「登记表快照」整体改为「实例的 `Scan`（`SyncFromDisk` 已扫过）」。
更彻底（认领永远是当前事实），但要改 `PluginInstance.Scan` 的可见性与 `RebuildClaimTable`
的读取路径，改动面更大。**建议先走上面的局部修复**。

**必须配的自检断言**：模拟「先装旧认领清单 → 再用新清单重扫」，断言
①认领表与新清单一致 ②没有多包冲突 ③`Enabled` 未被改动。

### 2.2 【范围】手写面板与声明式表单是两套真相

**已被外移的 5 个动作，UI 一行未动**，所以本轮外移也不必动 —— 但代价必须说清楚：

剩余 7 个动作的手写面板里，**声明式表单当前表达不了**的东西（逐项核实过 XAML）：

| 面板 | `ParameterField` 表达不了的部分 |
|---|---|
| `FocusWindowManagerPanel` | ①**二级子模式选择器**（8 项，选的是「动作变体」不是参数）②平铺布局下拉 ③「✨ 预设 8 布局二级轮盘」按钮（会写二级级联菜单）④5 枚常用布局芯片 ⑤`Slider`（`ParameterFieldType` 只有 `Number`，没有滑杆）⑥4 枚透明度预设芯片 ⑦4 枚任务栏槽位芯片 |
| `FocusSystemPanel` | 单一下拉（**这个能用 `Enum` 表达**），但选中后要联动改扇区名与图标（`DefaultName` / `DefaultIconKey`，见 `SettingsWindow.xaml.cs:7679-7686`）—— 属「Presets」类需求 |
| `FocusOcrPanel` | 状态文案 + **两枚自定义动作按钮**（✂️ 立即测试截屏 / ⚙️ 接口配置），操作的是全局 OCR 设置而非动作参数 |

**两个选项**

- **甲（建议）：本轮 UI 完全不动。** 插件的 `Parameters` 只用于「校验 + 预览」，界面继续由
  手写面板渲染。与 S1/S2 一致，改动可控，且**外移与面板统一化本来就是两件可独立验证的事**。
  代价：两套真相继续并存；`FocusWindowOpacitySlider` 的 30~100 与插件声明的 1~100 不一致
  （现在就不一致，因为手写面板不走声明式校验）。
- **乙：本轮顺带把面板切到统一表单。** 需要先给 SDK 补 `Presets`（芯片/快捷按钮）/
  `CustomActions`（自定义动作按钮）/ `VisibleWhen`（条件显示）/ `Slider` 四样，再重写三个面板。
  收益是一套真相 + 社区插件也能用这些控件；代价是 SDK 变更面大、二级子模式这种
  「动作变体」概念不好建模，且会把本轮变成「不可单独验证的大改」。

**反方意见（不藏）**：甲的问题是真的 —— 面板里的布局清单、透明度范围、槽位上限都是手写的，
与插件声明各说各话；未来任何一侧改动都可能只改一半。若采纳甲，**至少要为「面板与声明一致性」
补一条自检断言**（例如断言面板用的布局清单 == `WindowTiler.LayoutKeys`）。

### 2.3 【缺口】能力枚举覆盖不到窗口 / 截屏 / 模拟输入

现有 9 项（`PluginMetadata.cs:12`）：`None` / `Process` / `FileSystem` / `Network` /
`Clipboard` / `Registry` / `GlobalHook` / `Ui` / `Admin`。

按「安装确认页展示的能力必须对应一个后果」（S2 立下的原则）衡量：

| 新能力 | 覆盖的动作 | 对应后果 | 是否建议新增 |
|---|---|---|---|
| `WindowControl` | 5 个窗口动作 | 移动 / 缩放 / 置顶 / 改透明度**其它程序的**窗口 | ✅ 建议（`Ui` 的语义是「打开自己的窗口」，不符） |
| `ScreenCapture` | `Ocr` | **截取屏幕内容**（隐私敏感） | ✅ 建议 |
| `InputSimulation` | `System`（模拟音量键 / 媒体键 / 全局组合键） | 模拟全局输入 | ✅ 建议 |
| `Power` | `System`（关机 / 重启 / 睡眠 / 锁屏） | 直接改变机器电源状态 | 🤔 可选 —— 也可归入 `InputSimulation` + `Process` |

**必须同时写清楚的诚实说明**（沿用 S2 的写法）：
- 门禁**只能加在新接口上**。`IHostActionInvoker.SendHotkey` 是既有契约，给它补门禁会让
  已发布插件突然失败 ⇒ 「模拟输入」实际上有一条无门禁的旁路（插件可以直接 `SendHotkey`）。
- 因此新增能力项换到的**不是安全边界**，仍是「能力标签对应一个真实后果」。
  进程内插件任何时候都能自己 `Process.Start` / P/Invoke，SDK 拦不住。
- 新增枚举值对已发布插件是安全变更（只是多几个取值）。

---

## 3. 目标包划分（两套方案，待拍板）

前提：**`BasicActions` 的插件 ID 与文件名保持不变**。理由见 §5.1 —— 改名会留下
「宿主区有副本、来源区已无对应文件」的幽灵包，且宿主当前没有「来源文件消失就清理登记」的逻辑。

### 方案甲（4 包，零迁移）

| 包（ID） | 认领类型 | 动作数 | 能力 |
|---|---|---|---|
| `starpie.builtin.basicactions`（**不动**） | `Launch` / `WebUrl` / `Url` / `Folder` / `OpenFolder` / `Command` / `ShellTool` | 5 | `Process` |
| `starpie.builtin.windowactions`（新） | `Tile` / `ToggleTopmost` / `MoveMonitor` / `WindowOpacity` / `SwitchWindow` | 5 | `WindowControl` |
| `starpie.builtin.systemactions`（新） | `System` | 1 | `Process` / `InputSimulation` |
| `starpie.builtin.screenocr`（新） | `Ocr` / `ScreenOcr` | 1 | `ScreenCapture` |

### 方案乙（5 包，按风险切分 —— 回应「进一步拆分」）

| 包（ID） | 认领类型 | 拆分理由 |
|---|---|---|
| `starpie.builtin.basicactions`（**ID 与文件名保留，只留打开类**） | `Launch` / `WebUrl` / `Url` / `Folder` / `OpenFolder` | 「打开东西」是零风险动作，用户极少想关 |
| `starpie.builtin.terminalactions`（新） | `Command` / `ShellTool` | **执行任意命令**，是用户最可能想单独关掉的一类（安全动机） |
| `starpie.builtin.windowactions`（新） | 5 个窗口动作 | 会动用户的窗口，独立领域 |
| `starpie.builtin.systemactions`（新） | `System` | 关机 / 锁屏，误触代价高 |
| `starpie.builtin.screenocr`（新） | `Ocr` / `ScreenOcr` | **截屏**，隐私敏感 |

**每个包都对应一个「用户为什么会想单独关掉它」的动机**，这是拆包的唯一正当理由。
不是为了「好看」。

**再细就不值得了**：把 `Launch` / `WebUrl` / `Folder` 各拆一个包，用户看到的是三行
配置相同的插件，无人会分开停用；而包数每 +1 都要付出 §5.2 的代价。

---

## 4. 需要改动清单

### 4.1 SDK 层（`StarPie.Plugin.Abstractions/`）

| 文件 | 改动 |
|---|---|
| `Services.cs` | 新增 `IHostWindowService` / `IHostSystemService` / `IHostScreenCaptureService` + 各自的选项类型 |
| `PluginMetadata.cs` | `PluginCapability` 增加 `WindowControl` / `ScreenCapture` / `InputSimulation`（+ 可选 `Power`） |
| `IPluginContext.cs` | 新增 `Windows` / `System` / `ScreenCapture` 三个属性（加性变更，不破坏既有插件） |
| `PluginApi.cs` | `ApiVersionMinor` 1 → 2，`ApiVersion` 同步为手写 `"1.2"`（常量插值对 `int` 不成立，见 S2 踩坑记录） |

**接口草案**（`IHostWindowService`）

```csharp
public interface IHostWindowService
{
    // —— 元数据面：Parameters 期要用，故不受门禁约束（与 Terminals/Verbs 同理）
    IReadOnlyList<WindowLayoutOption> Layouts { get; }  // 17 个布局码 + 本地化显示名
    string CycleToken { get; }        // "Cycle"
    string CycleBackToken { get; }    // "CycleBack"
    string RestoreToken { get; }      // "Restore" —— 它同时是 TileRestore 的语义入口

    // —— 执行面：需要 PluginCapability.WindowControl
    bool ApplyLayout(string layoutKey);   // 内部已处理 Cycle / CycleBack / Restore 三个标记
    bool ToggleTopmost();
    bool MoveToNextMonitor();
    bool SetOpacity(string percent);      // 保持 string，钳制规则留在宿主（不复制第二份）
    bool ActivateTaskbarSlot(int slotIndex);
}
```

**为什么 `Layouts` / `CycleToken` 这类元数据不受门禁**：插件的 `Parameters` 是**属性**，
注册期就会被读取；在那里抛异常会让「忘了声明能力」的插件在注册阶段整个崩掉，
而不是在执行时给出一句人话。这与 S2 的 `Terminals` / `Verbs` 同一条理由。

**`IHostSystemService` 两档草案**

- **档 1（薄外移，建议先做）**：`IReadOnlyList<SystemPresetOption> Presets { get; }` +
  `bool RunPreset(string presetKey)`。宿主保留那 41 个分支。
  - 反驳「这不算外移」：**这正是 `IHostCommandService.Run` 的形态** ——
    真正的 `Process.Start` 也在宿主，但没人说「运行命令」没外移。
    动作的**形状、参数、校验、预览、执行入口**全在插件里，宿主只留一个执行器工厂。
  - 收益立刻兑现：可停用 + 预设表只有一份（UI 与插件都从服务取）。
- **档 2（厚外移）**：把 41 个分支的行为搬进插件，宿主只提供原子能力
  （`LockWorkstation` / `ShowHostWindow(kind)` / `ToggleProcessWindow(name)` /
  `SendMediaKey(key)`；启动进程复用既有的 `IHostActionInvoker.Launch`）。
  - 收益：真外移，第三方系统动作包因此可行。
  - 代价：**22 个历史别名**必须逐个搬到插件的映射表里，漏一个就是静默失效
    （`switch` 的 `default` 是空的）。**必须配「旧 `case` 标签全集对拍」自检。**

**`IHostScreenCaptureService`**：`bool CaptureAndRecognize()`，一行转发到
`OcrManager.StartCaptureAndRecognize`，门禁 `ScreenCapture`。

### 4.2 宿主层（`WinPieGestures/`）

| 文件 | 改动 |
|---|---|
| `Plugin/PluginHost.cs` | **§2.1 的元数据刷新修复**（P0 前置）；`RebuildClaimTable` 不动 |
| `Plugin/PluginHostServices.cs` | 新增三个服务实现类（沿用 S2 的 `RequireCapability` + `Guard` 双件套） |
| `Plugin/PluginContext.cs` | 装配三个新服务 |
| `Plugin/BuiltinActionCatalog.cs` | 移除 7 条登记，只剩 `Hotkey`；注释改成「唯一内建动作」并说明为何 |
| `Plugin/BuiltinActions/*.cs` | 删除 7 个文件（`BuiltinActionTile` / `ToggleTopmost` / `MoveMonitor` / `WindowOpacity` / `SwitchWindow` / `System` / `Ocr`） |
| `ActionExecutor.cs` | `ExecuteSwitchWindow` / `ExecuteSystem` 改由服务调用（或整体搬走）；`Execute = switch(ClassifyAction(...))` 不变 |
| `WindowTiler.cs` / `OcrManager.cs` | **实体不动**（它们是服务实现，不是动作实现） |

### 4.3 插件层（`plugins/`）

新增 3~4 个工程，每个都遵守既有三条硬约束（`TargetFramework` 不高于宿主 /
`ProjectReference` 必须 `Private=false` / **绝不引用 `StarPie.dll`**）：

```
plugins/StarPie.Plugin.WindowActions/     WindowActionsPlugin.cs + 5 个 Action + Texts.cs + csproj
plugins/StarPie.Plugin.SystemActions/     SystemActionsPlugin.cs + 1 个 Action + Texts.cs + csproj
plugins/StarPie.Plugin.ScreenOcr/         ScreenOcrPlugin.cs + 1 个 Action + Texts.cs + csproj
plugins/StarPie.Plugin.TerminalActions/   （仅方案乙）TerminalActionsPlugin.cs + 2 个 Action + csproj
```

每个 `csproj` 需要：插件元数据（`StarPiePluginId` / `Name` / `Capabilities` / `License` /
`Homepage`）+ `StarPiePluginTypeClaims`。**注意 `Id` 与 `Descriptor.Id` 必须逐字一致**。

**词条**：`Texts.cs` 模式照抄 `BasicActions`（zh-CN 必填作兜底，en / zh-TW / ja 补齐）。
动作显示名继续抄宿主 `I18n.cs` 的既有词条值，保证界面文案不变。

**随包插件的三条规则对每个新包都成立**（首启自动装并启用 / 有登记就一律不动 /
文件被删会补回并保留启用状态）—— 由既有机制提供，无需新写。

### 4.4 构建层

`WinPieGestures/WinPieGestures.csproj` 的 `CopyBundledPlugins` 目前**硬编码单枚 dll**
（`BundledPluginPayload` 只有一项），必须改成列举全部随包产物：

```xml
<ItemGroup>
  <ProjectReference Include="..\plugins\StarPie.Plugin.BasicActions\..." ReferenceOutputAssembly="false" Private="false" />
  <ProjectReference Include="..\plugins\StarPie.Plugin.WindowActions\..." ReferenceOutputAssembly="false" Private="false" />
  <!-- … -->
</ItemGroup>
<ItemGroup>
  <BundledPluginPayload Include="$(BundledPluginOutputRoot)StarPie.Plugin.BasicActions.dll" />
  <BundledPluginPayload Include="$(BundledPluginOutputRoot)StarPie.Plugin.WindowActions.dll" />
  <!-- … -->
</ItemGroup>
```

注意现有注释里那条纪律要**保留并加强**：**只拷列出的 dll，绝不做「目录一扫全拷」** ——
否则会把 `StarPie.Plugin.Abstractions.dll` 塞进来源区，造成类型身份分裂。
包多了以后这条更容易被违反，建议在 `CopyBundledPlugins` 里加一句「拷进去的文件数 == 列出的项数」
的校验（多一个就报错）。

### 4.5 UI 层

**按 §2.2 选项甲：本轮零改动。** 需要留意的三处（都不改，但要确认不会被破坏）：

1. `SettingsWindow.xaml.cs:5070-5102` 的 `isWindowManager` 分支与 `Type == "System"` /
   `Type == "Ocr"` 判断 —— 依赖的都是 `Type` 字符串，外移后不变 ✅
2. `GestureMappingViewModel.cs:155-200` 的 `WindowManagerSubMode` 映射 ——
   引用 `WindowTiler.CycleParam` / `RestoreParam` 常量，宿主侧仍在 ✅
3. `SlotViewModel.AggregatedActionTypes` / `LocalizedActionTypes` —— Tag 是 Type 名，不变 ✅

### 4.6 自检（`PluginSelfTest.cs`）

| 段 | 改动 |
|---|---|
| `[3f]` | `migratedTypes` 写死断言表要补 7 个类型；`builtinCases` 最终只剩 `Hotkey` |
| `[3i]` | 认领断言改为覆盖**全部 12 个**已认领类型（5 旧 + 7 新），并断言「每个认领指向的贡献点真的存在」 |
| `[3j]` | 新增三个服务的门禁断言（未声明 → 抛 `PluginCapabilityDeniedException`；声明后 → 放行；元数据不受门禁） |
| **新增 `[3k]`** | **随包插件元数据刷新**：装旧清单 → 用新清单重扫 → 认领表更新且无冲突，`Enabled` 不变 |
| **新增 `[3l]`** | **别名覆盖对拍**（若走 System 档 2）：把旧 `switch` 的 63 个 `case` 标签写成断言表，逐项断言插件侧映射表能认 |
| `[7]` | 环境还原性检查要覆盖新包的宿主区目录 |

> 自检数据驱动的既有原则继续适用：**能从快照推导的用循环，属于设计意图的写死**。

### 4.7 文档层

- `AGENTS.md` §2.1 目录树（新增 4 个插件工程）+ §3.7 插件系统（服务面清单、能力表、拆包纪律）
- `CHANGELOG.md` 新增 S3 小节
- `PLUGIN_FIRST_ROADMAP.md` §6 第 0 期的「仍未做」段落需要更新（列出的三样里，
  本轮明确选择「暂不补」）
- 本文（`PLUGIN_ACTIONS_S3_OUTLINE.md`）在实施后转为「现状描述」或归档

---

## 5. 风险与红线

### 5.1 风险表

| 级别 | 风险 | 触发条件 | 对策 |
|---|---|---|---|
| **P0** | 认领快照不刷新 ⇒ 拆包后两个包全部动作失效 | 任何一次更改已登记随包插件的认领清单 | §2.1 的修复必须先合入，并配 `[3k]` 断言 |
| **P0** | 改了包 ID 或文件名 ⇒ 幽灵包（宿主区有副本、来源区已无对应文件） | 拆包时顺手改名 | **不改 ID、不改文件名**；确需改名则同时提供迁移 |
| **P1** | 来源区出现第二份 `StarPie.Plugin.Abstractions.dll` | `CopyBundledPlugins` 写成「目录一扫全拷」 | 保持列举式 + 加「拷贝数 == 列举数」校验 |
| **P1** | System 走档 2 时漏掉历史别名 ⇒ 静默失效 | 手工搬运 63 个标签 | `[3l]` 对拍断言（写死旧标签全集） |
| **P1** | 元数据面缺口 ⇒ 插件抄一份布局表 / 预设表 ⇒ 两处漂移 | 只暴露执行面、不暴露枚举 | 服务同时暴露 `Layouts` / `Presets`，并断言与宿主清单同源 |
| **P2** | 包数增加 ⇒ 启动期 PE 读取次数线性上升 | 来源区 dll 数从 1 变 5 | 实测启动耗时增量；当前无扫描缓存，必要时加「按文件 mtime + size 的元数据缓存」 |
| **P2** | 面板与声明不一致（§2.2 选项甲的代价） | 任一侧单独修改 | 补一致性断言 |
| **P2** | 新增能力项后，老用户的 `CapabilitiesAck` 不含新能力 | 拆包 + 新增能力同时发生 | §2.1 的刷新覆盖 `CapabilitiesAck` |

### 5.2 拆包的真实代价（必须正视）

每个包都要付出：一次 PE 元数据扫描（`AutoInstallBundledPlugins` + `SyncFromDisk` 各一轮）、
一条登记条目、一个 `PluginInstance`、一个按需创建的 ALC、一份独立的词条/图标/参数注册表。
**包数不是免费的**，所以 §3 才坚持「一个包必须对应一个停用动机」。

### 5.3 三条红线的复核

| 红线 | 本轮是否触碰 | 依据 |
|---|---|---|
| R1 不装插件零开销 | ❌ 不触碰 | 剩余动作外移后，`BuiltinActionCatalog` 只剩 `Hotkey`，仍是编译期静态注册；随包插件走既有的「登记表 + 惰性加载」路径，认领表在启动期只读登记表、不加载程序集 |
| R2 轮盘零延迟丝滑 | ❌ 不触碰 | 热路径（按下 → 呈现）不经过插件代码；认领表是启动期建好的字典命中 |
| R3 肌肉记忆确定性 | ⚠️ 需留意 | 外移动作后，**停用某个包会让对应扇区失效**。这正是任务 #30「停用语义」要处理的事（停用时列出受影响扇区数并确认、停用后槽位编辑器标红、触发时给可读提示）。**本轮不解决它，但会让它从「理论问题」变成「用户真会遇到的问题」** |

---

## 6. 阶段划分与顺序

| 阶段 | 内容 | 前置 | 状态 |
|---|---|---|---|
| **S3-0** | 修 §2.1 的认领快照刷新 + `[3k]` 断言 | — | ✅ `77788b3` |
| **S3a** | 新建 `WindowActions` 包 + `IHostWindowService` + `WindowControl` 能力；移除 5 条内建登记 | S3-0 | ✅ `7e95b02` |
| **S3e** | `CopyBundledPlugins` 改为多包列举 + 构建校验 | 任一新包存在 | ✅ 随 S3a 完成（缺失即构建失败） |
| **S3c** | 新建 `ScreenOcr` 包 + `IHostScreenCaptureService` + `ScreenCapture` 能力 | S3-0 | ⬜ 下一阶段 |
| **S3d** | 新建 `SystemActions` 包 + `IHostSystemService`（档 1 起步）+ `InputSimulation` 能力 | S3-0 | ⬜ |
| **S3b** | 新建 `TerminalActions` 包 + 从 `BasicActions` 移除 `Command`/`ShellTool` 两项认领 | S3-0（**强依赖，无此则冲突**） | ⬜ 放最后（有迁移风险） |
| **S3f** | 文档同步 + 自检全段回归 + 提交 | 全部 | ⬜ |

> **S3a 实际落地时与原估的三处偏差**（记下来，免得后续阶段重复踩）：
> 1. `[3g]` 里写死 `Type="Tile"` 的校验探针，在 Tile 外移的同一刻变成**误报** ——
>    它报出的「掉进 default」其实是正确行为（那时 `[3h]` 还没装包）。现在改为从
>    `SnapshotAll()` 取第一个带必填项的动作。**凡是拿具体动作当探针的地方都要随外移一起复查。**
> 2. 三个带门禁的服务共用一个基类之后，「required 传错」**没有编译期保护** ——
>    补了 `[3j]` 跨能力交叉断言（只声明 A 的插件调 B 的服务）。
> 3. 门禁探针一律传空参数：万一门禁写错、调用被放行，也只会撞上空值短路，
>    **不会当场改掉自检者自己的窗口**。

> 顺序建议：**S3-0 → S3a → S3c → S3d → S3b**（把风险最高的 `System` 放在窗口/截屏都验证过之后，
> 把有迁移风险的 `BasicActions` 拆分放最后）。S3e 可以在第一个新包落地时就顺手做。

**每阶段的合入标准**（沿用 `PLUGIN_FIRST_ROADMAP.md` §6）：
新增贡献点必须同时提交**该点的失败域定义 + 自检断言 + 资源开销实测**。

---

## 7. 决策点（**已定稿 · 2026-09-17**）

| # | 决策 | 结论 |
|---|---|---|
| 1 | 拆包粒度 | **方案乙 · 5 包** —— `BasicActions` 保留 ID 与文件名、只留打开类；新增 `TerminalActions`（`Command`/`ShellTool`）、`WindowActions`、`SystemActions`、`ScreenOcr` |
| 2 | `System` 外移深度 | **档 1 · 薄转发** `IHostSystemService.RunPreset(key)`；宿主保留 41 个分支。档 2 单独立项（需 `[3l]` 对拍自检配套） |
| 3 | `Ocr` 是否本轮外移 | **是**，只做薄转发 `CaptureAndRecognize()`；同时是路线图第 1 期「OCR 引擎可替换」的前置 |
| 4 | 能力枚举 | **新增 3 项**：`WindowControl` / `ScreenCapture` / `InputSimulation`（`Power` 不单列） |
| 5 | UI 是否本轮统一 | **甲 · 完全不动**（保持与 S1/S2 一致）。面板统一化独立排期 |
| 6 | 包 ID 与文件名 | **保持不变**，避免幽灵包 |

**由第 1 项带来的强顺序约束**：`TerminalActions` 的落地**强依赖 S3-0** ——
`BasicActions` 要移除 `Command`/`ShellTool` 两项认领，而老用户的登记表快照仍是旧值。
不先修 §2.1，这一步会触发「多包抢同一类型 → 整对拒绝」。

**由第 2 项带来的简化**：`System` 不走档 2 ⇒ `[3l]`（63 个 `case` 标签对拍）本轮不做，
但大纲保留它 —— 档 2 真要立项时它是必做项。

**由第 5 项带来的欠账**（必须记账，不能假装没有）：
面板与声明两套真相继续并存。已知的不一致点：
- `FocusWindowOpacitySlider` 范围 `30~100` vs 插件声明 `1~100`
- 面板布局清单（XAML 内联 + `FocusTileLayoutComboBox`）vs `WindowTiler.LayoutKeys`（17 项）
- 系统预设下拉 vs `SystemPresetList`（41 项）

⇒ 实施时**至少补一条一致性断言**，把「改了 A 没改 B」变成构建期可见。

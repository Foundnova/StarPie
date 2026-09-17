# StarPie 动作插件化 · S4 大纲（单动作拆包）

> **本文属于规划，不是现状描述。** 「现状」段落都标了核实点；「建议」段落是待拍板项。
> 上级文档：`PLUGIN_ACTIONS_S3_OUTLINE.md`（服务面与能力门禁的设计）、`AGENTS.md`（唯一权威工程规范）。
> 本文**取代** S3 大纲 §3「目标包划分」与 §6 阶段表的包数部分，其余部分仍然有效。

---

## 0. 结论先行

### 0.1 粒度从「5 功能域包」改为「12 单动作包」（2026-09-17 用户拍板）

S3 定的是 5 个包、一个包认领一组类型。用户看过落地结果后要求**每个功能一个插件**，
粒度提到**一个包认领一个顶层类型**。

| | S3 方案 | **S4 方案** |
|---|---|---|
| 包数 | 5 | **12** |
| 一个包认领 | 1~7 个类型 | **1 个类型**（别名随主类型同包） |
| 用户能单独停用 | 一组动作 | **单个动作** |
| `BasicActions` | 保留，含 5 个打开类动作 | **消失**，拆成 5 个包 |

### 0.2 拆包之前必须先补一块地基，否则会当场炸

「拆包」= 旧包名从来源区消失。而宿主**没有**「来源区文件没了就清理对应登记条目」的逻辑
（核实：`PluginHost.cs` 全文无任何按来源区反查登记条目的代码；`AutoInstallBundledPlugins`
只遍历**存在的**文件，遍历不到的就什么都不做）。

于是拆包后的现场是：

```
来源区：  5 个新包，各认领 Archive 之外的一个类型
宿主区：  BasicActions 的旧副本还在（没人删它）
登记表：  starpie.builtin.basicactions 条目还在，ClaimedTypes 仍是旧的 7 项快照
认领表：  老条目认领 Launch/WebUrl/… ，新包也认领 Launch/WebUrl/…
          ⇒ RebuildClaimTable 判「多包抢同一类型 → 整对拒绝」
          ⇒ 老包与新包的动作**一起失效**
```

S3-0 修的 `RefreshBundledMetadata` **管不到这个场景** —— 它的前置是「来源区里还能找到这枚
dll」，而拆包恰恰让这枚 dll 消失了。两者是互补的两半：

| 场景 | 由谁负责 |
|---|---|
| 来源区里**还有**这枚 dll，但它变了 | `RefreshBundledMetadata`（S3-0 已做） |
| 来源区里**已经没有**这枚 dll 了 | **本阶段要补的清理**（S4-0） |

### 0.3 12 个包的代价（必须正视，不能只说收益）

| 代价 | 量级 | 缓解 |
|---|---|---|
| 启动期 PE 元数据读取 | 每枚 dll 读 2 轮（`AutoInstallBundledPlugins` + `ScanCandidates`）⇒ 12 枚 = 24 次 | 后续可加「按 路径+mtime+size 的元数据缓存」，本轮不做 |
| 插件页行数 | 12 行 | 需要合并展示时再做，本轮不动 UI |
| 每包固定开销 | 一条登记条目、一个 `PluginInstance`、一个按需 ALC、一份词条/图标表 | 惰性加载让 ALC 与词条只在真正用到时创建 |
| 打包纪律 | `CopyBundledPlugins` 的逐枚列举从 2 项变 12 项 | 已有「缺失即构建失败」校验（S3e） |

**收益**：用户能把「乱动我窗口」精确到只关「窗口透明度」；也能在出问题时只回退一个动作的提供方。

---

## 1. 目标包划分（12 个）

包名一律 `StarPie.Plugin.<Type>`，插件 ID 一律 `starpie.builtin.<type 全小写>`。
**认领串的右侧短 ID 必须与 `Descriptor.Id` 逐字一致。**

### 1.1 打开类（3 包，能力 `Process`）

| 包 | 插件 ID | 认领 | 来源 |
|---|---|---|---|
| `StarPie.Plugin.Launch` | `starpie.builtin.launch` | `Launch=launch` | 拆自 `BasicActions` |
| `StarPie.Plugin.WebUrl` | `starpie.builtin.weburl` | `WebUrl=webUrl;Url=webUrl` | 拆自 `BasicActions` |
| `StarPie.Plugin.Folder` | `starpie.builtin.folder` | `Folder=folder;OpenFolder=folder` | 拆自 `BasicActions` |

> **别名必须与主类型同包**：`Url` 与 `WebUrl` 是同一个动作的两种写法，分到两个包就会
> 出现「同一个动作在两种写法下由不同插件执行」，且停用其中一个只失效一半配置。

### 1.2 命令类（2 包，能力 `Process`）

| 包 | 插件 ID | 认领 | 来源 |
|---|---|---|---|
| `StarPie.Plugin.Command` | `starpie.builtin.command` | `Command=command` | 拆自 `BasicActions` |
| `StarPie.Plugin.ShellTool` | `starpie.builtin.shelltool` | `ShellTool=shellTool` | 拆自 `BasicActions` |

### 1.3 窗口类（5 包，能力 `WindowControl`）

| 包 | 插件 ID | 认领 | 来源 |
|---|---|---|---|
| `StarPie.Plugin.Tile` | `starpie.builtin.tile` | `Tile=tile` | 拆自 `WindowActions` |
| `StarPie.Plugin.ToggleTopmost` | `starpie.builtin.toggletopmost` | `ToggleTopmost=toggleTopmost` | 拆自 `WindowActions` |
| `StarPie.Plugin.MoveMonitor` | `starpie.builtin.movemonitor` | `MoveMonitor=moveMonitor` | 拆自 `WindowActions` |
| `StarPie.Plugin.WindowOpacity` | `starpie.builtin.windowopacity` | `WindowOpacity=windowOpacity` | 拆自 `WindowActions` |
| `StarPie.Plugin.SwitchWindow` | `starpie.builtin.switchwindow` | `SwitchWindow=switchWindow` | 拆自 `WindowActions` |

### 1.4 系统与截屏类（2 包，新建）

| 包 | 插件 ID | 认领 | 能力 | 来源 |
|---|---|---|---|---|
| `StarPie.Plugin.System` | `starpie.builtin.system` | `System=system` | `InputSimulation` | 新建（原 S3d） |
| `StarPie.Plugin.Ocr` | `starpie.builtin.ocr` | `Ocr=ocr;ScreenOcr=ocr` | `ScreenCapture` | 新建（原 S3c） |

### 1.5 不外移

| 动作 | 理由 |
|---|---|
| `Hotkey` | `ActionItem.Type` 的默认值 + 未配置扇区占位类型，**必须永远可解析**。放进可停用的包，用户一停用，所有空扇区按下去都会报「包已停用」 |

### 1.6 认领表终态

15 个类型 → 12 个包（`WebUrl`/`Url` 同包、`Folder`/`OpenFolder` 同包、`Ocr`/`ScreenOcr` 同包）。
`Hotkey` 仍由内建表提供，`Text`/`String`/`TileRestore` 仍走 `switch` 特例。

---

## 2. 地基（S4-0）：来源区不再分发即清理

### 2.1 判据

`AutoInstallBundledPlugins` 本轮已经把来源区所有 dll 扫过一遍、拿到了各自的 `manifest.Id`。
这天然给出了清理所需的唯一判据：

```
seenIds = 本轮成功识别的所有随包插件 Id
对每个 Bundled=true 的登记条目：Id ∉ seenIds ⇒ 该包已不再随程序分发
```

**不需要**在登记条目里新增「来源文件名」字段 —— 判据用 ID 就够，且对老数据（没有该字段的
既有条目）同样成立。

### 2.2 保守守卫（三条，缺一不可）

| 条件 | 行为 | 理由 |
|---|---|---|
| `!PluginPaths.ScanRootExists` | 不清理 | 来源区整体不可用（例如程序目录只读/被隔离），无从判断 |
| 来源区里一枚 dll 都没有 | 不清理 | 「空目录」几乎不可能是用户的明确意志，更像是杀软/同步工具的中间态；清掉就不可恢复 |
| 有任一文件识别失败 | 不清理 | 数据不完整。宁可留一个幽灵包（日志里有警告），也不要误删用户的包 |

### 2.3 清理做什么

1. `PluginRegistryStore.RemoveEntry(id)` —— 去掉登记条目（含认领快照，这是关键）
2. 删除宿主区安装目录 `PluginPaths.GetPluginDirectory(id)`；被占用时沿用既有的
   `.pending-delete-<guid>` 挂起机制
3. **保留插件私有数据** —— 「不再分发」不等于「用户数据该丢」
4. **出声**：`LogInfo`，写清「哪个包、因为来源区已无对应文件被清理」

### 2.4 为什么不能复用 `UninstallCore`

它在最开头 `Find(pluginId)`，而清理发生在 `Initialize` 里 `SyncFromDisk` **之前** ——
那时 `Instances` 还是空的，`Find` 必然返回 null ⇒ 必然失败。需要写一个直接操作
登记表与磁盘的专用函数。

### 2.5 时序

清理必须落在 `AutoInstallBundledPlugins` 之后、`SyncFromDisk` 之前 ——
这样 `SyncFromDisk` 按登记表建实例时，待清理的包已经不在了；
`RebuildClaimTable` 随后建表，读到的就是干净的登记表。

```
Initialize:
  AutoInstallBundledPlugins()   ← 装来源区里有的
  PruneUndistributedBundled()   ← 清来源区里没有的（新增）
  SyncFromDisk()                ← 按登记表建实例
  RebuildClaimTable()           ← 内建表 + 认领表
```

### 2.6 自检断言（`[3m]`）

- 造一个「来源区有 A、登记表里 A 与 B 都在」的沙箱 ⇒ 断言 B 的登记条目被移除、A 保留
- 断言 B 的宿主区目录被删除、但 B 的 `data\` 保留
- 断言**保守守卫**：来源区一枚 dll 都没有时，B **不**被清理
- 断言清理后 `RebuildClaimTable` 里没有 B 的认领（不产生「多提供方」冲突）
- 同段还要断言**没有幽灵包**：清理后 `Instances`/登记表里都不含 B

---

## 3. 改动清单

### 3.1 地基（S4-0）

| 文件 | 改动 |
|---|---|
| `PluginHost.cs` | 新增 `PruneUndistributedBundledPlugins(HashSet<string> seenIds, bool trustworthy)`；`AutoInstallBundledPlugins` 收集并按引用返回 seenIds 与「是否有识别失败」 |
| `PluginSelfTest.cs` | 新增 `[3m]` 段 + `RunBundledPruneChecks`（`NoInlining`，见纪律 5） |

### 3.2 新建包（S4a / S4b / S4c / S4d）

每个包 4 个源文件：`<X>Plugin.cs` / `<X>Action.cs` / `Texts.cs` / `<X>.csproj`。
词条只带自己那一组（现有 `Texts.cs` 已按 `launch.` / `weburl.` / `folder.` / `command.` /
`shell.tool.` 前缀分组，切起来是机械操作）。

`csproj` 三处必改：`AssemblyName` / `RootNamespace` / `StarPiePluginId`，加上 `StarPiePluginTypeClaims`。
**三条硬约束照旧**：`TargetFramework` 不高于宿主、`ProjectReference` 必须 `Private=false`、
**绝不引用 `StarPie.dll`**。

### 3.3 宿主侧

| 文件 | 改动 |
|---|---|
| `WinPieGestures.csproj` | `BundledPluginPayload` 列举 12 项（已有「缺失即 `<Error>`」校验，漏一项构建就会失败） |
| `Plugin/BuiltinActionCatalog.cs` | 最终只剩 `Hotkey` 一项 |
| `Plugin/BuiltinActions/` | `BuiltinAction{System,Ocr}.cs` 删除（其余已删） |
| `Plugin/PluginSelfTest.cs` | `[3f]` 的 `migratedTypes` 扩到全部 15 个类型；`[3i]` 认领断言覆盖 12 个包 |

### 3.4 文档

`AGENTS.md` §2.1 目录树（12 个插件工程）+ §3.7；`CHANGELOG.md`；`MEMORY.md`（**必须 < 12 KB**，
加内容前先删等量）；技能 `dotnet-inprocess-plugin-system`。

---

## 4. 风险

| 级别 | 风险 | 对策 |
|---|---|---|
| **P0** | 不补地基就拆包 ⇒ 幽灵包 + 认领冲突 ⇒ 两包动作全失效 | S4-0 必须先合入，且 `[3m]` 覆盖 |
| **P0** | 认领串右侧短 ID 与 `Descriptor.Id` 不一致 ⇒ 动作「找得到归属、永远执行不了」 | `[3i]` 已有断言，新包逐个纳入 |
| **P1** | 别名漏在同一包里（`Url` / `OpenFolder` / `ScreenOcr`） | `[3f]` 的 `migratedTypes` 写死全部 15 个类型 |
| **P1** | 「加认领」与「删内建登记」只做一半 ⇒ 同一类型挂两条路、内建优先 | 交割不变量，一个提交里同时发生 |
| **P2** | 24 次 PE 读取抬高启动耗时 | 实测；必要时加元数据缓存（本轮不做） |
| **P2** | 12 行插件页 | 本轮不动 UI |

---

## 5. 阶段划分

| 阶段 | 内容 | 前置 | 状态 |
|---|---|---|---|
| **S4-0** | 来源区不再分发即清理 + `[3m]` | — | ✅ `5106629` |
| **S4c** | 拆 `WindowActions` → 5 个单动作包 | S4-0 | ✅ `25b1e15` |
| **S4d** | 拆 `BasicActions` → 5 个单动作包 | S4-0 | ✅ `54250f5` |
| **S4a** | 新建 `Ocr` 包 + `IHostScreenCaptureService` + `ScreenCapture` 能力 | S4-0 | ✅ |
| **S4b** | 新建 `System` 包 + `IHostSystemService`（档 1 薄转发）+ `InputSimulation` 能力 | S4-0 | ✅ |
| **S4e** | 文档同步 + 12 个包逐个自检 + 提交 | 全部 | ⬜ |

> **原定顺序是「先纯新增、后拆分」，实际执行反了过来**（S4c → S4d → S4a）。
> 理由是：两个纯新增包里，`Ocr` 必须先扩 SDK 契约（新接口 + 新能力位 + 版本号），
> 而扩契约会牵动**全部**包的重新构建 —— 先把不依赖新契约的十个包拆完，
> 契约只在最后动一次，中途任何一次构建失败都能确定不是契约引起的。
> `System`（S4b）同理会引入 `InputSimulation`，所以同样排在末尾。
>
> **为什么不用 S3c/S3d 的原编号**：那两阶段的产出是「`ScreenOcr` 包」与「`SystemActions` 包」，
> 在单动作粒度下要改名成 `StarPie.Plugin.Ocr` 与 `StarPie.Plugin.System`，产物与阶段名对不上会误导。
> 任务 #42 / #43 承接原 #37 / #38 的内容。

---

## 6. 决策记录

| # | 决策 | 结论 | 日期 |
|---|---|---|---|
| 1 | 拆包粒度 | **每动作一包（12 个）**，取代 S3 的「5 功能域包」 | 2026-09-17 |
| 2 | 别名归属 | 与主类型**同包**（`Url`→WebUrl 包、`OpenFolder`→Folder 包、`ScreenOcr`→Ocr 包） | 2026-09-17 |
| 3 | 清理判据 | 用 **ID 级**（`Id ∉ seenIds`），不新增「来源文件名」字段 | 2026-09-17 |
| 4 | 清理的激进度 | **保守**：来源区不存在 / 空目录 / 有识别失败 → 一律不清理 | 2026-09-17 |
| 5 | 插件私有数据 | 「不再分发」时**保留** `data\` | 2026-09-17 |
| 6 | `Hotkey` | 不动，永久内建 | 沿用 S3 |
| 7 | UI | 本轮完全不动 | 沿用 S3 |

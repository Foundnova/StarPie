# PR: 进程内插件系统

> **标题（PR Title）**：
> `feat: 进程内插件系统（SDK 契约/宿主加载卸载/管理页与参数表单）+ 只读来源区与可写宿主区目录模型 + CLI 全链路自检`
>
> **目标分支**：上游 `SoftBlack42/StarPie` 的 `main`
> **来源**：fork `Sunse666/StarPie` 的 `main`（或从其切出的特性分支）
> **基线**：`c47c6f8`（ci(build): 精简 PR 编译验证工作流 #118）
> **范围**：9 个提交，68 个文件，+17362/−93

---

## 概述

给 StarPie 加上**进程内插件系统**：第三方 DLL 可以注册自定义动作（带声明式参数表单、多语言词条、矢量图标），出现在轮盘的「🔌 插件动作」类型下，随轮盘/手势/子动作统一调用；宿主提供管理页负责扫描、安装、启停与卸载，并提供一条 CLI 通道做全链路自检。

设计上守住三条既有工程红线：

1. **轻量低内存** —— 不装任何插件时，插件系统只做一次目录扫描（零 Load、零反射实例化），不额外占内存；
2. **零延迟丝滑** —— 插件动作与内置动作走同一条调用链，不引入额外等待；插件异常被隔离，不会拖慢或卡死主界面；
3. **肌肉记忆确定性** —— 动作类型下拉里插件**只占一项**，不随插件数量增长而打乱既有顺序，用户配置不会因为装/卸插件而漂移。

---

## 设计决策

### 1. 三层程序集边界，杜绝类型身份分裂

```
StarPie.Plugin.Abstractions   ← 独立 SDK 程序集（唯一契约层）
WinPieGestures（宿主）        ← 引用 SDK（CopyLocal，随主程序分发）
插件                          ← 只引用 SDK，ProjectReference 设 Private=false
```

插件**绝对不引用宿主主程序**——否则加载后出现两份 `IStarPiePlugin` 类型身份，强转全部静默返回 null。宿主侧 `Load()` 按四步判定（SDK 已加载 → 宿主自有 → Default ALC 可提供 → 插件目录），保证每个程序集身份唯一。

### 2. 识别不执行：MetadataReader 纯静态扫描

扫描目录里的每个 `.dll` 只读 PE 元数据（`System.Reflection.Metadata`）判断「是否 .NET 程序集、TFM 是否兼容、有没有实现 `IStarPiePlugin`」——**不 Load、不反射取类型、不实例化**。丢一枚改名的文本文件进去，结论是「无法识别」而不是崩溃；TFM 高于宿主的插件给出明确文案而非加载后失败。

### 3. 目录模型：只读来源区 + 可写宿主区（本次的核心方案）

| 目录 | 角色 | 宿主行为 |
|---|---|---|
| `<程序目录>\plugin\` | **只读来源区**：随发行包分发的待安装候选 | **不创建、不写入、不删除** |
| `%LOCALAPPDATA%\StarPie\plugin-data\` | **可写宿主区**：安装副本、`registry.json`、`health.json`、插件私有 `data\` | 只在这里写 |

- 来源区放的是**候选**：不登记、不加载、不进插件列表，安装必须由用户点按钮触发；
- 复制策略按清单来源分叉：有 `plugin.json` 整目录复制，裸 DLL 只复制那一枚并**回填生成的 `plugin.json`**（否则「装得上却永远启用不了」，且报错文不对题）；
- 便携模式（`portable.flag`）只改可写宿主区的落点，来源区永远固定在程序目录——保证绿色包在只读位置（如 Program Files）也能正常工作；
- 历史目录名 `plugins\` 由一次性迁移逻辑自动搬迁，不丢已装插件。

> 这样拆之后，「卸载插件」永远只删宿主区副本，来源区候选完好，重新安装零成本；
> 而宿主对程序目录保持只读，符合发行包的完整性预期。

### 4. 动作收敛：插件只占一个类型位

动作类型下拉里插件**只出现一项**「🔌 插件动作」，具体动作放在按插件分组的子下拉里（组名取插件显示名，与详情面板同源）。此前若把每个插件动作平铺进类型下拉，装 N 个插件就多 N 项，且组名会拖着插件 ID 的尾巴——都会打乱用户既有配置的顺序。

### 5. 安全边界

- 插件参与 UI 聚合的路径全部包 `try/catch`：**插件异常不会让主界面/设置窗打不开**；
- 无界面模式（自检/诊断）不参与「启动健康」记账——否则连跑两次自检会把用户正常使用的插件误禁用、误触发安全模式；
- 注册 API 返回 `IDisposable`，宿主侧留档并有 `RevokeAll` 兜底；
- ALC 卸载验证：同步探测失败不下结论，走 250/750/2000ms 延迟三轮判定；卸载后插件目录**能被即时删除**作为文件锁释放的硬证据。

---

## 变更明细（按提交）

| 提交 | 内容 |
|---|---|
| `12c7e64` | **feat**: 新增 `StarPie.Plugin.Abstractions` SDK（动作注册、参数 Schema、I18n、图标、宿主服务）；宿主侧 `PluginHost` / `PluginLoadContext` / 扫描器 / 注册表存储；设置窗新增插件管理页（NavTab5）与声明式参数表单渲染 |
| `d2c948a` | **feat**: 动作类型收敛为单一「🔌 插件动作」+ 按插件分组的子下拉；切换类型不清空已配动作引用；空状态区分「还没选」与「根本没得选」 |
| `2de9ccf` | **fix**: 插件动作通知时机收紧（避免加载中途触发 UI 刷新竞态）；自检新增 `--skip-invoke` |
| `a41a6ff` | **feat**: 拆分只读来源区与可写宿主区（`PluginPaths` 单一路径出口）；候选扫描与安装；`plugins\ → plugin-data\` 一次性迁移 |
| `3d90cde` | **docs**: 插件系统三份文档（设计/实现/性能与 API）与目录模型对齐 |
| `c9fb0a0` | **test**: 插件页 UI 回归 4 用例；修复 4 枚因缺 `Name` 而永远不进自动化树的按钮 |
| `a2b1b35` | **test**: 对齐 11 个跨版本漂移的既有用例（简洁模式隐藏高级 UI / 折叠 Expander / 状态归属等），套件转绿 |
| `790c025` | **fix**: CLI 通道输出落到调用方终端（WinExe 附父控制台）；自检报告写入失败不再静默（多候选回退 + 失败时全文回显） |
| `4787efe` | **test**: `scan_dir` 夹具清理失败全分支出声、还原「原本不存在」现场；更新检查用例不再依赖网络 |

新增文件集中在 `StarPie.Plugin.Abstractions/`（SDK，9 个文件）、`WinPieGestures/Plugin/`（宿主，23 个文件）、`samples/`（2 个样例）、`tests/test_plugins.py` 与三份 `PLUGIN_SYSTEM_*.md` 文档。

---

## 附带产物

- **两个官方样例**（即插件作者的参考模板）：
  - `samples/HelloAction` —— 最小可用形态：自定义动作 + 参数表单 + 词条 + 图标 + 宿主服务调用 + 幂等 Shutdown；`plugin.schema.json` 是清单的权威 Schema；
  - `samples/ScreenBrightness` —— 真实系统状态交互（屏幕亮度），自检用它验证真实调用路径。
- **文档**：`PLUGIN_SYSTEM_DESIGN.md`（架构与目录模型）、`PLUGIN_SYSTEM_IMPLEMENTATION.md`（实现细节）、`PLUGIN_SYSTEM_PERFORMANCE_AND_API.md`（性能基准与 API 参考）。
- **README / README_EN / CHANGELOG / AGENTS.md** 同步更新。

---

## 验证

全部可在命令行复现：

```bash
# 1. 构建：0 警告 0 错误
dotnet build WinPieGestures/WinPieGestures.csproj -c Release

# 2. 插件全链路自检（沙箱内跑，不碰真实插件目录；--skip-invoke 跳过真实改系统状态的动作）
StarPie.exe --plugin-selftest samples/HelloAction/bin/.../StarPie.Plugin.HelloAction.dll --skip-invoke
#    → 自检结论：PASS —— 全链路可用
#    覆盖：静态识别 → 候选安装 → 启用注册 → 参数校验 → 词条命中率 → 卸载 → 环境还原

# 3. 路径诊断（两条目录模型的落点一目了然）
StarPie.exe --plugin-paths

# 4. UI 回归（pytest + pywinauto，隔离 venv）
python -m pytest tests/ -v
#    → 26 passed（22 settings + 4 plugins）
```

自检沙箱验证要点（均为自动化断言，非人工目测）：

- 装完必须**真的处于运行态**（只断言「装上了」会放过「装上了但永远启用不了」这类真 bug）；
- 卸载后插件目录能被即时删除（ALC 真卸载的硬证据）；
- 假文本文件伪装 `.dll` → 判定「无法识别」且不给安装按钮；
- 候选安装后宿主目录内的程序集恰好 1 枚；
- 来源区目录删除后：候选归零、宿主**不会重建该目录**。

---

## 已知边界 / 后续

- 「同一插件 ID 多来源」的优先级规则尚未定义（当前 `ExtraScanDirectories` 未启用，不会出现该场景）；启用前会先把规则定下来。
- 收藏（favorite）类依赖登录态的能力不在本期范围。
- 插件市场的签名校验属后续里程碑，本期信任边界 = 用户手动点击安装。

---

## 给 reviewer 的建议动线

1. 先读 `PLUGIN_SYSTEM_DESIGN.md`（≈目录模型 + 加载卸载）——这两个决定影响面最大；
2. 再看 `StarPie.Plugin.Abstractions/`（SDK 表面，全部公开契约在此）；
3. 最后看 `WinPieGestures/Plugin/PluginHost.cs` 的 `Load()/Unload()` 与 `PluginSelfTest.cs` 的自检断言——前者是正确性核心，后者是「上面所有坑」的自动化防线。

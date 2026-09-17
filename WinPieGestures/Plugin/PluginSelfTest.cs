using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using StarPie.Plugin;

namespace WinPieGestures.Plugins;

/// <summary>
/// 插件系统端到端自检。
/// <para>
/// 它把「识别 → 安装 → 启用 → 注册 → 调用 → 停用 → 卸载」整条链路跑一遍并输出报告，
/// 存在的意义有两个：① 无界面环境下也能验证插件系统是否真的能跑通（CI 回归）；
/// ② 用户报告「插件装不上」时，一个命令就能拿到全链路证据。
/// </para>
/// <para>
/// 用法：<c>StarPie.exe --plugin-selftest &lt;插件.dll&gt; [报告输出路径] [--skip-invoke]</c>
/// </para>
/// <para>
/// <b>自检整体跑在临时沙箱里</b>：两个根目录（可写宿主区与只读扫描目录）都会被钉到
/// <c>%TEMP%\StarPie-PluginSelfTest-&lt;随机&gt;\</c> 下，跑完即删。以前它直接跑在真实插件目录上，
/// 等于每做一次回归就动一次用户已经装好的插件。
/// </para>
/// </summary>
internal static class PluginSelfTest
{
    public static int Run(string dllPath, string? reportPath, bool skipInvoke = false)
    {
        var report = new StringBuilder();
        bool pass = true;

        void Line(string text)
        {
            report.AppendLine(text);
            // 实时打到终端。这条通道以前只写 Debug（进调试器）与最终的报告文件，
            // 命令行里跑完什么都看不到 —— 而它存在的意义恰恰是「一条命令拿到全链路证据」，
            // 前提是那条命令的输出真的看得见（父控制台的接入见 App.AttachParentConsoleIfCli）。
            Console.WriteLine(text);
            System.Diagnostics.Debug.WriteLine(text);
        }

        void Fail(string stage, string reason)
        {
            pass = false;
            Line($"  [FAIL] {stage}：{reason}");
        }

        PluginCandidate? FindCandidate(string fileName) => PluginHost.Candidates.FirstOrDefault(
            c => string.Equals(c.FileName, fileName, StringComparison.OrdinalIgnoreCase));

        Line("====================================================");
        Line("StarPie 插件系统端到端自检");
        Line($"时间：{DateTime.Now:yyyy-MM-dd HH:mm:ss}");
        Line($"宿主版本：{PluginManifestReader.HostVersion} / SDK 契约：{PluginApi.ApiVersion}");
        Line($"目标文件：{dllPath}");
        if (skipInvoke)
        {
            Line("运行模式：--skip-invoke —— 跳过真实调用，不会改变本机环境");
        }
        Line("====================================================");

        // ---- 沙箱：把两个根目录钉到临时位置，跑完即删 ----
        string sandboxRoot = Path.Combine(
            Path.GetTempPath(), "StarPie-PluginSelfTest-" + Guid.NewGuid().ToString("N"));
        string sandboxHostRoot = Path.Combine(sandboxRoot, "plugin-data");
        string sandboxScanRoot = Path.Combine(sandboxRoot, "plugin");

        try
        {
            Directory.CreateDirectory(sandboxHostRoot);
            Directory.CreateDirectory(sandboxScanRoot);
            PluginPaths.OverrideRootsForTesting(sandboxHostRoot, sandboxScanRoot);
            Line($"沙箱目录：{sandboxRoot}（真实插件目录不会被触碰）");
        }
        catch (Exception sandboxError)
        {
            // 建不出沙箱就如实说明，不要假装自己是隔离的
            Line($"[WARN] 无法创建自检沙箱（{sandboxError.Message}），本次将直接跑在真实插件目录上。");
        }
        Line("====================================================");

        string? installedPluginId = null;

        try
        {
            // ---- 0 初始化 ----
            Line("");
            Line("[0] 初始化插件系统");
            var sw = Stopwatch.StartNew();

            // 自检是无界面短命进程，不该被计入启动健康统计。
            PluginHost.HeadlessMode = true;
            PluginHost.Initialize();
            sw.Stop();
            Line($"  插件根目录：{PluginPaths.Root}");
            Line($"  便携模式：{PluginPaths.IsPortable}");
            Line($"  初始化耗时：{sw.Elapsed.TotalMilliseconds:F1} ms");
            Line($"  已登记插件：{PluginHost.InstalledCount} 个");

            // ---- 1 静态识别 ----
            Line("");
            Line("[1] 静态识别（不加载程序集）");
            sw.Restart();
            PluginScanResult scan = PluginHost.PrepareInstall(dllPath);
            sw.Stop();
            Line($"  识别耗时：{sw.Elapsed.TotalMilliseconds:F2} ms");
            Line($"  结论：{(scan.Accepted ? "通过" : "拒绝")}");

            if (!scan.Accepted)
            {
                Line($"  原因码：{scan.Failure}");
                Line($"  标题：{PluginScanFailureText.Title(scan.Failure)}");
                Line($"  详情：{scan.ErrorDetail}");
                Line($"  修复建议：{PluginScanFailureText.Hint(scan.Failure)}");
                Fail("静态识别", scan.DescribeFailure());
                return Write(report, reportPath, pass);
            }

            PluginManifest manifest = scan.Manifest!;
            Line($"  清单来源：{scan.ManifestSource}");
            Line($"  ID：{manifest.Id}");
            Line($"  名称：{manifest.Name} v{manifest.Version}");
            Line($"  作者：{manifest.Author}");
            Line($"  许可证：{manifest.License}");
            Line($"  能力声明：{manifest.ResolveCapabilities()}");
            Line($"  入口类型：{scan.EntryTypeFullName ?? "(未解析)"}");
            Line($"  实际 TFM：{scan.TargetFramework}");
            Line($"  架构：{scan.MachineText}（依赖文件：{(scan.HasDependencyFile ? "有" : "无")}）");
            Line($"  文件大小：{scan.FileSizeText}");
            Line($"  SHA256：{scan.Sha256}");
            Line($"  签名：{(scan.IsSigned ? $"已签名（{scan.SignerSubject}）" : "未签名")}");

            installedPluginId = manifest.Id;

            // ---- 2 安装 ----
            Line("");
            Line("[2] 安装（复制落盘 + 登记为 Disabled）");
            var options = new PluginInstallOptions
            {
                Acknowledged = true,
                OverwriteExisting = true,
                EnableAfterInstall = false,
                AcknowledgedCapabilities = manifest.Capabilities,
            };
            PluginInstallResult install = PluginHost.CommitInstall(scan, options);
            if (!install.Success)
            {
                Fail("安装", install.Error);
                return Write(report, reportPath, pass);
            }
            Line($"  安装成功：{install.PluginId}");

            PluginInstance? instance = PluginHost.Find(install.PluginId);
            Line($"  安装后状态：{instance?.State}（已加载：{instance?.IsLoaded}）");
            if (instance?.IsLoaded == true)
            {
                Fail("安装语义", "安装后不应加载程序集，但要保持内存红线");
            }

            // ---- 3 启用 + 4 调用 ----
            // 刻意放进独立方法：这两步会拿到 PluginActionRegistration，而它的 Contribution
            // 指向插件程序集里的类型实例。这些引用若留在 Run 的栈帧上，第 5 步卸载时插件的
            // ALC 就回收不掉 —— 自检会把自己测挂，报告里出现假的「需要重启才能释放」。
            string? stageError = RunEnableAndInvoke(install.PluginId, Line, skipInvoke);
            if (stageError != null)
            {
                Fail("启用与调用", stageError);
            }

            // ---- 5 停用 ----
            Line("");
            Line("[5] 停用（撤销贡献点 → 剪断订阅 → Shutdown → 卸载 ALC）");
            sw.Restart();
            bool disabled = PluginHost.Disable(install.PluginId, out string disableError);
            sw.Stop();
            Line($"  停用结果：{(disabled ? "成功" : "失败")}（{sw.Elapsed.TotalMilliseconds:F1} ms）");
            if (!disabled) Fail("停用", disableError);

            // 等延迟判定给出最终结论。同步探测常常因为调用栈还没展开而回收不掉，
            // 不等它就会把「其实已经释放」误报成「需要重启」。
            instance = PluginHost.Find(install.PluginId);
            bool unloaded = instance?.WaitForUnloadVerdict(5000) ?? false;
            Line($"  停用后状态：{instance?.State}");
            Line($"  需要重启才能释放：{instance?.RequiresRestart}");
            if (!unloaded)
            {
                Fail("ALC 卸载", "插件程序集未被回收，停用要重启才能真正生效");
            }
            Line($"  剩余已注册动作：{PluginHost.GetRegisteredActions().Count} 个");

            if (PluginHost.GetRegisteredActions().Count != 0)
            {
                Fail("贡献点撤销", "停用后仍有动作残留在注册表里");
            }

            // ---- 6 卸载 ----
            Line("");
            Line("[6] 卸载（删除目录 + 移除登记）");
            bool uninstalled = PluginHost.Uninstall(install.PluginId, removePluginData: true, out string uninstallError);
            Line($"  卸载结果：{(uninstalled ? "成功" : "失败")}");
            if (!uninstalled)
            {
                Fail("卸载", uninstallError);
            }
            else
            {
                installedPluginId = null;
            }

            // ---- 3d 只读扫描目录（候选识别 → 单枚复制 → 装后状态）----
            Line("");
            Line("[3d] 只读扫描目录与候选安装（沙箱内）");

            string candidateFileName = Path.GetFileName(dllPath);
            const string DecoyFileName = "notaplugin.dll";

            // ① 空目录必须是 0 个候选
            int emptyCount = PluginHost.ScanCandidates();
            if (emptyCount != 0)
            {
                Fail("候选扫描", $"空的扫描目录里扫出了 {emptyCount} 个候选");
            }

            // ② 放一枚真插件，再放一枚「看着像 dll 其实不是」的文件
            File.Copy(dllPath, Path.Combine(sandboxScanRoot, candidateFileName), overwrite: true);
            File.WriteAllText(Path.Combine(sandboxScanRoot, DecoyFileName), "这只是一个文本文件，不是程序集。");
            PluginHost.ScanCandidates();

            PluginCandidate? real = FindCandidate(candidateFileName);
            PluginCandidate? decoy = FindCandidate(DecoyFileName);

            if (real == null)
            {
                Fail("候选扫描", $"扫描目录里没有扫出 {candidateFileName}");
            }
            else if (real.State != PluginCandidateState.Installable)
            {
                Fail("候选扫描", $"未安装过的插件应判为「可安装」，实际是 {real.State}");
            }

            if (decoy == null)
            {
                Fail("候选扫描", "非程序集文件没有被扫出来 —— 用户会以为「放进去了却毫无反应」");
            }
            else
            {
                Line($"  非程序集文件的结论：{decoy.StateText}｜{decoy.Note}");
                if (decoy.State != PluginCandidateState.Rejected || decoy.CanInstall)
                {
                    Fail("候选扫描", "非程序集文件必须判为「无法识别」且不给安装按钮");
                }
            }

            Line($"  候选数：{PluginHost.Candidates.Count} 个（可安装 {PluginHost.Candidates.Count(x => x.CanInstall)} 个）");

            if (real != null)
            {
                // ③ 点「安装」—— 与界面上那个按钮完全同一条路
                bool installedByCandidate = PluginHost.InstallCandidate(real, out string candidateError);
                if (!installedByCandidate)
                {
                    Fail("候选安装", candidateError);
                }
                else
                {
                    PluginInstance? installed = PluginHost.Find(real.PluginId!);
                    Line($"  安装后状态：{installed?.State}｜登记来源：{installed?.Entry.Source}");

                    // 裸 DLL 安装必须「装完就能跑」。这里曾经是个真缺陷：裸 dll 安装不回填
                    // plugin.json，而安装目录的识别要求目录里有清单 —— 于是插件装得上却永远
                    // 启用不了，报错是一句与真实原因无关的「插件目录里缺少 plugin.json」。
                    if (installed?.State != PluginRuntimeState.Active)
                    {
                        Fail("候选安装启用",
                            $"候选安装后插件应处于运行态，实际是 {installed?.State}（{installed?.LastError}）");
                    }

                    string installedManifest = PluginPaths.GetManifestPath(installed?.ManagedDirectory ?? "");
                    if (!File.Exists(installedManifest))
                    {
                        Fail("候选安装启用", $"裸 DLL 安装没有回填清单，后续识别与启用都会失败：{installedManifest}");
                    }

                    // C1 回归断言：裸 DLL 安装只复制那一枚，绝不能把扫描目录里的邻居一起搬走。
                    // 搬走邻居的后果不是「多几个文件」这么轻：装了 A 却连带出现 B，
                    // 而且 B 还会因为目录里存在两枚业务 dll 而识别失败。
                    string managedDirectory = installed?.ManagedDirectory ?? "";
                    string[] managedDlls = Directory.Exists(managedDirectory)
                        ? Directory.GetFiles(managedDirectory, "*.dll", SearchOption.TopDirectoryOnly)
                        : Array.Empty<string>();

                    Line($"  宿主目录内的程序集：{managedDlls.Length} 枚" +
                        (managedDlls.Length > 0 ? $"（{string.Join("、", managedDlls.Select(Path.GetFileName))}）" : ""));

                    if (managedDlls.Length != 1
                        || !string.Equals(Path.GetFileName(managedDlls[0]), candidateFileName, StringComparison.OrdinalIgnoreCase))
                    {
                        Fail("单枚复制",
                            $"裸 DLL 安装只应复制 {candidateFileName} 这一枚，实际宿主目录里有 {managedDlls.Length} 枚");
                    }

                    if (!string.Equals(installed?.Entry.Source, "ScanDirectory", StringComparison.Ordinal))
                    {
                        Fail("安装来源", $"候选安装的登记来源应为 ScanDirectory，实际是 {installed?.Entry.Source}");
                    }

                    // ④ 重扫：同一枚文件应变成「已装同版本」
                    PluginHost.ScanCandidates();
                    PluginCandidate? afterInstall = FindCandidate(candidateFileName);
                    Line($"  重扫后状态：{afterInstall?.StateText ?? "(消失)"}");
                    if (afterInstall?.State != PluginCandidateState.Installed)
                    {
                        Fail("装后状态",
                            $"装完之后同一枚文件应判为「已装同版本」，实际是 {afterInstall?.State.ToString() ?? "(消失)"}");
                    }

                    // ⑤ 同 ID 撞车：两枚都必须是「ID 重复」且都不给安装按钮
                    string duplicateName = "copy-" + candidateFileName;
                    File.Copy(dllPath, Path.Combine(sandboxScanRoot, duplicateName), overwrite: true);
                    PluginHost.ScanCandidates();

                    PluginCandidate? first = FindCandidate(candidateFileName);
                    PluginCandidate? second = FindCandidate(duplicateName);
                    Line($"  ID 重复：{first?.StateText ?? "(消失)"} / {second?.StateText ?? "(消失)"}");

                    if (first?.State != PluginCandidateState.Duplicate || second?.State != PluginCandidateState.Duplicate)
                    {
                        Fail("ID 重复", "扫描目录里两枚 dll 声明同一 ID 时，两者都必须判为「ID 重复」");
                    }
                    else if (first.CanInstall || second.CanInstall)
                    {
                        Fail("ID 重复", "ID 重复的候选一律不能给安装按钮 —— 装哪一枚都说不清");
                    }
                    else if (first.Note.IndexOf(real.PluginId!, StringComparison.OrdinalIgnoreCase) < 0)
                    {
                        Fail("ID 重复", $"冲突说明里应写明撞车的是哪个 ID，实际是：{first.Note}");
                    }
                    else if (string.Equals(first.FileName, second.FileName, StringComparison.OrdinalIgnoreCase))
                    {
                        Fail("ID 重复", "两行候选必须各自显示自己的文件名，否则用户看不出该删哪一个");
                    }

                    // ⑤.5 重启等价态 —— 「已启用但尚未加载」这条路径
                    //
                    // 每次重启后插件都处在这个状态：SyncFromDisk 按登记表把实例造出来，
                    // 但只要没开预加载，程序集就不会被加载，贡献点目录里自然也没有它。
                    // 修复前这条路径上会同时出两种错，都属于「看起来像用户配置丢了」：
                    //   ① 界面上把用户配好的动作标成「已失效」—— 用户什么都没做；
                    //   ② 按下动作回一句「插件动作未注册……可能已被禁用或卸载」—— 与事实不符。
                    //
                    // 探针体独立成方法的原因见它的文档注释：本段要停用插件并等 ALC 回收结论，
                    // 而它持有的 PluginActionRegistration 绝不能留在 Run 的帧上。
                    string? restartError = RunRestartEquivalenceProbe(installed, real.PluginId!, Line);

                    if (restartError != null)
                    {
                        Fail("重启等价态", restartError);
                    }

                    // ⑥ 收拾干净：停用 → 等 ALC 回收结论 → 卸载 → 删掉扫描目录
                    //
                    // 顺序和 [5]/[6] 一致，不能省掉「等回收」这一步：插件程序集还挂在
                    // 未卸载的 ALC 上时文件是锁着的，此刻删目录会失败 —— 而失败又只体现在
                    // 一个被吞掉的异常里，表现就是临时目录里一次次堆出残留沙箱。
                    PluginHost.Disable(real.PluginId!, out _);
                    PluginHost.Find(real.PluginId!)?.WaitForUnloadVerdict(5000);

                    if (!PluginHost.Uninstall(real.PluginId!, removePluginData: true, out string cleanupError))
                    {
                        Fail("候选安装清理", cleanupError);
                    }
                }

                Directory.Delete(sandboxScanRoot, recursive: true);
                int afterDelete = PluginHost.ScanCandidates();
                bool recreated = Directory.Exists(sandboxScanRoot);

                Line($"  扫描目录删除后：候选 {afterDelete} 个，目录被重建={recreated}");
                if (afterDelete != 0)
                {
                    Fail("候选扫描", $"扫描目录已删除，却仍扫出 {afterDelete} 个候选");
                }
                if (recreated)
                {
                    Fail("扫描目录", "扫描目录不存在时被重新创建了 —— 程序装在只读位置会直接变成权限错误");
                }
            }

            // ---- 3f 内建动作批量接缝 ----
            //
            // 仍留在内建动作表里的那些动作，只验「注册 / 别名 / 投影 / 校验」四件事，
            // 刻意不做真实执行 ——
            // 它们一旦真跑就会去按热键、拉起程序、打开浏览器、弹出资源管理器，
            // 在自检进程里全是实打实的副作用。这正是 --skip-invoke 的用意：
            // 把「接缝连通性」与「动作真实效果」分成两件事，前者每次都验，
            // 后者由用户在真机上自己确认。
            //
            // 【为什么改成数据驱动】
            // 下面这份断言表原先是手写的，动作每外移一个就得有人记得删一行。
            // 忘了删的表现是自检报「某动作不在内建动作表里」—— 而那恰恰是**预期行为**。
            // 「预期的事被报成缺陷」比不报更坏：它会训练人忽略这条消息。
            // 所以下面直接遍历 BuiltinActionCatalog.SnapshotAll()，不再手抄清单；
            // 真正属于设计意图的东西（哪些类型**不该**留在表里）另起一个写死的断言，
            // 因为那是「我们决定它不在」，无法从快照里推导出来。
            Line("");
            Line("[3f] 内建动作批量接缝（注册 / 别名 / 投影 / 校验）");

            // 【交割不变量】被随包动作包认领的顶层类型，必须已经不在内建动作表里。
            //
            // 加认领声明与删内建登记是一次「交割」，必须同时发生：
            // 只加认领不删登记，同一个 Type 就挂着两条执行路径，而 ClassifyAction
            // 让内建优先 —— 结果是插件里那份代码永远不会被调用，改了也看不出效果。
            // 这个清单必须写死：它表达的是设计意图，不是当前实现状态的快照。
            string[] migratedTypes =
            {
                "Launch", "WebUrl", "Url", "Folder", "OpenFolder", "Command", "ShellTool",
            };

            foreach (string migratedType in migratedTypes)
            {
                if (BuiltinActionCatalog.TryGet(migratedType, out BuiltinActionRegistration stillBuiltin))
                {
                    Fail("内建动作注册",
                        $"「{migratedType}」既已被随包动作包认领、又还留在内建动作表里（{stillBuiltin.FullId}）—— " +
                        "同一个类型挂着两条执行路径，且内建优先，插件里那份代码永远不会被调用。" +
                        "外移动作时「加认领声明」与「删 BuiltinActionCatalog 里的登记」必须同时完成");
                }
            }

            var builtinCases = new (string Type, string? Alias, ActionItem Probe, int Fields, string? Key, string Expect, string EmptyLabel)[]
            {
                ("Hotkey", null,
                    new ActionItem { Type = "Hotkey", Parameter = "Ctrl+Alt+S" },
                    1, "hotkey", "Ctrl+Alt+S", "快捷键"),

                // 【已移出本表】Launch / WebUrl（别名 Url）/ Folder（别名 OpenFolder）/
                //             Command / ShellTool
                //
                // 它们不再是内建动作，而是随包插件 StarPie.Plugin.BasicActions 认领的顶层类型。
                // 留在上面只会以「不在内建动作表里」失败 —— 而那个失败恰恰是**预期行为**，
                // 不是缺陷。它们的注册 / 参数 / 投影 / 校验改由 [3i] 按认领链路验证，
                // 并且那里多验一条本表没有的：认领指向的贡献点必须真的存在。
                ("System", null,
                    new ActionItem { Type = "System", Parameter = "Minimize" },
                    1, "preset", "Minimize", "系统功能"),

                // 无参数动作：Key 传 null，只验「已登记 + 参数声明确实为空」。
                // 它没有必填项，也没有「空值」这一说 —— 硬套下面的必填 / 空值检查只会得到假失败。
                ("Ocr", "ScreenOcr",
                    new ActionItem { Type = "Ocr" },
                    0, null, "", ""),

                ("Tile", null,
                    new ActionItem { Type = "Tile", Parameter = "2L" },
                    1, "layout", "2L", "平铺方式"),

                ("ToggleTopmost", null,
                    new ActionItem { Type = "ToggleTopmost", Parameter = "" },
                    0, null, "", ""),

                ("MoveMonitor", null,
                    new ActionItem { Type = "MoveMonitor", Parameter = "" },
                    0, null, "", ""),

                ("WindowOpacity", null,
                    new ActionItem { Type = "WindowOpacity", Parameter = "80" },
                    1, "opacity", "80", "透明度"),

                ("SwitchWindow", null,
                    new ActionItem { Type = "SwitchWindow", Parameter = "1" },
                    1, "index", "1", "任务栏位置"),
            };

            int builtinVerified = 0;

            foreach (var (caseType, caseAlias, caseProbe, caseFields, caseKey, caseExpect, caseEmptyLabel) in builtinCases)
            {
                if (!BuiltinActionCatalog.TryGet(caseType, out BuiltinActionRegistration caseReg))
                {
                    Fail("内建动作注册", $"「{caseType}」不在内建动作表里 —— 用户配好的该动作会落回旧路径");
                    continue;
                }

                string caseLabel = $"{caseType}｜{caseReg.FullId}";

                // 别名是这里最该盯的一处：丢了不会有任何报错，只会让老配置里的动作静默失效 ——
                // 用户看到的是「这个扇区按下去没反应」，而原因藏在两个字符串不相等上。
                if (caseAlias != null)
                {
                    if (!BuiltinActionCatalog.TryGet(caseAlias, out BuiltinActionRegistration byAlias)
                        || !string.Equals(byAlias.FullId, caseReg.FullId, StringComparison.Ordinal))
                    {
                        Fail("内建动作别名", $"别名「{caseAlias}」没能解析回 {caseReg.FullId} —— 老配置里的该写法会失效");
                    }
                    else
                    {
                        caseLabel += $"（别名 {caseAlias} ✓）";
                    }
                }

                if (caseReg.Contribution.Parameters.Count != caseFields)
                {
                    Fail("内建动作参数", $"{caseType} 应声明 {caseFields} 个参数，实际 {caseReg.Contribution.Parameters.Count} 个");
                    continue;
                }

                if (caseKey == null)
                {
                    // 无参数动作到此为止。
                    builtinVerified++;
                    Line($"  {caseLabel}｜参数 0 项（无需参数）");
                    continue;
                }

                // System 的选项是从 SlotViewModel 的预设表即时生成的。这条断言守的是
                // 「将来有人图省事把它改回硬编码」—— 那样每加一个系统预设就会漏掉同步，
                // 而且不会有任何报错，只是新预设在这个面板里选不到。
                if (caseType == "System")
                {
                    int optionCount = caseReg.Contribution.Parameters[0].Options?.Count ?? 0;

                    if (optionCount != SlotViewModel.SystemPresetList.Count)
                    {
                        Fail("内建动作参数",
                            $"System 的选项数（{optionCount}）与预设表（{SlotViewModel.SystemPresetList.Count}）不一致 —— 预设表已不是唯一数据源");
                    }
                }

                // Tile 的选项 = 全部布局码 + 循环 / 反向循环 / 还原三个特殊标记。
                // 少一个标记的后果是那种用法在界面上彻底选不到，而且不会有任何报错。
                if (caseType == "Tile")
                {
                    int optionCount = caseReg.Contribution.Parameters[0].Options?.Count ?? 0;
                    int expectedOptions = WindowTiler.LayoutKeys.Count + 3;

                    if (optionCount != expectedOptions)
                    {
                        Fail("内建动作参数",
                            $"Tile 的选项数（{optionCount}）应为布局表 {WindowTiler.LayoutKeys.Count} + 3 个特殊标记 = {expectedOptions}");
                    }
                }

                // 必填项漏标，统一表单就会放行空值，等于把静默失效的门重新打开。
                if (!caseReg.Contribution.Parameters.Any(f => f.Key == caseKey && f.Required))
                {
                    Fail("内建动作参数", $"{caseType} 的 {caseKey} 必须标为必填");
                }

                Dictionary<string, string> projectedTestCase = caseReg.ProjectParameters(caseProbe);
                string projectedValue = projectedTestCase.GetValueOrDefault(caseKey) ?? "";

                if (!string.Equals(projectedValue, caseExpect, StringComparison.Ordinal))
                {
                    Fail("内建动作参数投影", $"{caseType} 的 {caseKey} 没有正确投影（得到 '{projectedValue}'）—— 用户配的值送不到执行体");
                }

                string? emptyVerdict = caseReg.Contribution.Validate(
                    new Dictionary<string, string> { [caseKey] = "   " });

                if (emptyVerdict == null)
                {
                    Fail("内建动作校验", $"{caseType} 的{caseEmptyLabel}为空时没有被拦下 —— 静默失效又回来了");
                }

                if (caseReg.Contribution.Validate(projectedTestCase) != null)
                {
                    Fail("内建动作校验", $"{caseType} 的合法参数被误判为不合法");
                }

                // 顺带说明：布尔字段「不变文化字面量」那条约定（投影成 "true" 而不是 "True"）
                // 原先挂在 Launch 上验证。Launch 外移之后，那条断言搬去了 [3i]，
                // 并且改成对整个宿主字段白名单核对 —— 覆盖面比只盯一个 Launch 更宽。

                builtinVerified++;
                Line($"  {caseLabel}｜参数 {caseFields} 项｜投影「{projectedValue}」✓｜必填 ✓｜空值拦截 ✓");
            }

            if (builtinVerified == builtinCases.Length)
            {
                Line($"  全部 {builtinVerified} 个动作：注册 ✓｜别名 ✓｜投影 ✓｜必填 ✓｜空值拦截 ✓");
            }

            // ---- 3g 端到端派发 ----
            //
            // 上面两段验的是「接缝本身好不好使」，但都直接调了接缝方法，绕过了
            // Execute() 开头那段判据 —— 而用户按下扇区走的恰恰是 Execute()。
            // 万一那段判据写错了位置（比如挪到 switch 之后），上面照样全绿，
            // 真机上却依然在走老路径 —— 典型的「测试说没问题、用户说没反应」。
            //
            // 【为什么不再用「跑一个动作看产物」的写法】
            // 这一段原先的探针是 Type="Command" + 一条 echo 命令，靠它产出的文件证明
            // 「被接走了」。Command 外移成插件动作之后这个探针不再成立；更要紧的是，
            // 「真跑也无害」的候选动作已经一个不剩 —— 剩下那八个内建动作，每一个真跑
            // 都会去按热键、抓屏 OCR、或是改用户窗口的平铺与透明度，副作用都不小。
            //
            // 所以拆成两层，各自解决一个问题：
            //   一、判据层：直接断言 ClassifyAction 对每一类 Type 的归属。它就是 Execute
            //       分派的本体（那边写的是 switch (ClassifyAction(...))），于是「顺序错了」
            //       这件事在这里就能钉死，不必真跑一个动作再反推它走了哪条路。
            //   二、端到端层：照旧走真的 Execute()，但改观测「它有没有出声」——
            //       静默失效的判据从来不是「产物没出来」，而是「什么都没告诉用户」。
            //       这一层顺带覆盖了 switch 的 default 分支（从前那里是一条 break）。
            Line("");
            Line("[3g] 端到端派发（Execute → ClassifyAction → 各分支）");

            // 一、判据层。
            //
            // 遍历快照而不是手抄清单（理由同 [3f]）：内建表里现有几个、叫什么，它自己最清楚。
            foreach (BuiltinActionRegistration builtinReg in BuiltinActionCatalog.SnapshotAll())
            {
                if (ActionExecutor.ClassifyAction(builtinReg.Type) != ActionExecutor.ActionDispatchKind.Builtin)
                {
                    Fail("派发判据",
                        $"内建动作「{builtinReg.Type}」（{builtinReg.FullId}）的归属不是 Builtin —— " +
                        "有别的分支抢在它前面，用户配好的这个动作不会执行到内建实现");
                }

                // 别名也要归到同一条路。别名解析靠 BuiltinActionCatalog.TryGet，
                // 而 ClassifyAction 的第一个判断就是它 —— 这里顺带把这条依赖钉住。
                foreach (string alias in builtinReg.Aliases)
                {
                    if (ActionExecutor.ClassifyAction(alias) != ActionExecutor.ActionDispatchKind.Builtin)
                    {
                        Fail("派发判据", $"内建动作别名「{alias}」（{builtinReg.FullId}）的归属不是 Builtin");
                    }
                }
            }

            // 已外移的类型绝不能还是 Builtin。
            //
            // 这里只断言「不是 Builtin」而不写死 SpecialCase：此刻认领表是不是空的，
            // 取决于运行到这里的时机（[2] 装的是社区插件，它认领不了顶层类型，
            // 所以正常情况下此刻表是空的）。把「非内建」和「已认领」两件事混在一条断言里，
            // 会让它变脆 —— 而脆的断言最后都会被人删掉。
            foreach (string migratedType in migratedTypes)
            {
                if (ActionExecutor.ClassifyAction(migratedType) == ActionExecutor.ActionDispatchKind.Builtin)
                {
                    Fail("派发判据",
                        $"已外移的「{migratedType}」仍被判为内建动作 —— " +
                        "它一定还留在 BuiltinActionCatalog 里（见 [3f] 的交割不变量）");
                }
            }

            // switch 兜底那三类例外，以及谁都认不出来的类型，必须落在 SpecialCase。
            // 前三个是契约；最后那个是 default 分支的入口，它决定「配置写错时会不会出声」。
            foreach (string specialType in new[] { "Text", "String", "TileRestore", "Plugin", "这个类型不存在" })
            {
                if (ActionExecutor.ClassifyAction(specialType) != ActionExecutor.ActionDispatchKind.SpecialCase)
                {
                    Fail("派发判据", $"「{specialType}」应落在 switch 兜底，实际归属不是 SpecialCase");
                }
            }

            if (ActionExecutor.ClassifyAction("   ") != ActionExecutor.ActionDispatchKind.SpecialCase
                || ActionExecutor.ClassifyAction(null) != ActionExecutor.ActionDispatchKind.SpecialCase)
            {
                Fail("派发判据", "空白 / 空 Type 应落在 switch 兜底，由 default 分支出声报错");
            }

            // 二、端到端层：真的调 Execute()，只观测「有没有出声」。
            //
            // 没有通知汇时 NotifyUser 会静默降级为写日志（那正是它在无界面进程里的正确行为），
            // 所以这里挂一个汇来接收。挂上之后**务必在 finally 里摘掉** ——
            // 它是全局静态的，留着会让后面所有段落的提示都流进这个已经不再读的列表，
            // 表面无异常、实际把后续观测悄悄吞掉。
            var dispatchNotifications = new List<(string Title, string Message)>();
            Action<string, string> dispatchSink = (title, message) => dispatchNotifications.Add((title, message));

            int dispatchChecks = 0;

            try
            {
                PluginNotificationHub.Sink = dispatchSink;

                // ① 认不出来的 Type：必须出声。
                //
                // 从前这里是一条 break —— 用户按下去什么都不会发生，也没有任何提示。
                // 这条断言守的就是那次修复：判据可以兜底，但兜底绝不能是静默。
                dispatchNotifications.Clear();
                ActionExecutor.Execute(new ActionItem
                {
                    Type = "自检用的未知类型",
                    Name = "自检探针",
                });

                if (dispatchNotifications.Count == 0)
                {
                    Fail("端到端派发",
                        "未知 Type 经 Execute() 之后没有任何提示 —— 用户看到的是「按下去没反应」，与配置错误无关");
                }
                else
                {
                    string unknownMessage = dispatchNotifications[dispatchNotifications.Count - 1].Message;

                    if (unknownMessage.IndexOf("无法识别", StringComparison.Ordinal) < 0)
                    {
                        Fail("端到端派发", $"未知 Type 的提示里没说清是类型不认识，实际文案：{unknownMessage}");
                    }
                    else
                    {
                        Line($"  未知 Type：出声 ✓（{dispatchNotifications.Count} 条提示）");
                        dispatchChecks++;
                    }
                }

                // ② 内建动作的参数校验失败：必须出声，且必须是「先校验、再执行」。
                //
                // 这一条同时验证了判据层之外的东西：内建分支确实被接走了（没接走的话它会掉进
                // default，提示文案会是「无法识别」而不是「必填」）。探针用空白 Parameter，
                // 于是执行体根本不会被调用 —— 这正是它比旧探针好的地方：
                // 拿一个**注定不会执行**的动作去验证派发，就不必再找了「真跑也无害」的动作。
                dispatchNotifications.Clear();
                ActionExecutor.Execute(new ActionItem
                {
                    Type = "Tile",
                    Name = "自检探针",
                    Parameter = "   ",
                });

                if (dispatchNotifications.Count == 0)
                {
                    Fail("端到端派发",
                        "内建动作的参数为空时没有任何提示 —— 用户填漏一个必填项，按下去却什么都没有发生");
                }
                else if (dispatchNotifications[dispatchNotifications.Count - 1].Message.IndexOf("无法识别", StringComparison.Ordinal) >= 0)
                {
                    Fail("端到端派发", "内建动作掉进了 switch 的 default 分支 —— 内建优先这条判据没生效");
                }
                else
                {
                    Line($"  内建动作校验失败：出声 ✓（{dispatchNotifications.Count} 条提示）");
                    dispatchChecks++;
                }

                // ③ 已外移的类型，在「认领表里没有它」的时点：必须出声，不能静默。
                //
                // 这一条正是外移动作最容易出的岔子。把动作从内建表里删掉、却没把认领建起来，
                // 用户看到的现象与「这个动作从来没做过」完全一样；而这里能证明宿主至少说了话。
                dispatchNotifications.Clear();
                ActionExecutor.Execute(new ActionItem
                {
                    Type = "Command",
                    Name = "自检探针",
                    Parameter = "echo 不应被执行",
                    CommandTerminal = "cmd_hidden",
                });

                if (dispatchNotifications.Count == 0)
                {
                    Fail("端到端派发",
                        "已外移的类型在无人认领时静默无事 —— 用户完全无从判断是配置错了、还是动作包没装");
                }
                else
                {
                    string unclaimedMessage = dispatchNotifications[dispatchNotifications.Count - 1].Message;
                    Line($"  已外移类型无人认领：出声 ✓（{unclaimedMessage}）");
                    dispatchChecks++;
                }
            }
            catch (Exception dispatchError)
            {
                Fail("端到端派发", $"Execute 抛出异常：{dispatchError}");
            }
            finally
            {
                // 摘掉汇：它是全局的，留在那里会污染后面所有段落的观测。
                PluginNotificationHub.Sink = null;

                if (dispatchChecks == 3)
                {
                    Line("  三条端到端断言全部通过：判据正确，且三条异常路径都会出声");
                }
            }

            // ---- 3h 随包分发的插件 ----
            //
            // 随包插件与用户自己装的插件，差别全在那三条规则上：首启自动装并启用、
            // 用户停用后绝不被偷偷启用、不可卸载只可停用。
            // 第二条是这一段最要紧的 —— 它是「我明明关过它」这类投诉的唯一来源，
            // 而且这种错在开发机上永远不会自己暴露（开发者不会去停用自己的插件）。
            Line("");
            Line("[3h] 随包分发的插件（自动安装 / 尊重停用 / 不可卸载）");

            // 被测插件的 ID 一律取自它自己的清单，不写死样例插件的 ID。
            // 写死的后果是：拿随包动作包（或任何别的插件）跑这段自检时，
            // 它会报「随包插件没有被自动装上」—— 而那与事实毫无关系，
            // 排查的人会先去怀疑打包，白白绕一圈。
            string bundledPluginId = manifest.Id;

            if (!Directory.Exists(sandboxScanRoot))
            {
                Directory.CreateDirectory(sandboxScanRoot);
            }
            File.Copy(dllPath, Path.Combine(sandboxScanRoot, candidateFileName), overwrite: true);

            int bundledFirst = PluginHost.AutoInstallBundledPlugins();
            PluginInstance? bundled = PluginHost.Find(bundledPluginId);

            if (bundled == null)
            {
                Fail("随包安装", "程序目录 plugin\\ 里的插件没有被自动装上 —— 用户打开会发现轮盘里是空的");
            }
            else
            {
                Line($"  首次启动：随包装入 {bundledFirst} 个｜随包标记={bundled.Entry.Bundled}｜" +
                    $"登记启用={bundled.Entry.Enabled}｜程序集已加载={bundled.IsLoaded}");

                if (!bundled.Entry.Bundled)
                {
                    Fail("随包安装", "自动装上来的插件没被标记为随包 —— 不可卸载与文件补回都依赖这个标记");
                }

                if (!bundled.Entry.Enabled)
                {
                    Fail("随包安装", "随包插件应默认启用，否则用户一打开就发现自带动作是缺的");
                }

                // 自动安装不许顺带加载程序集：它跑在启动最早期，这里加载会让启动开销
                // 随随包插件数量线性增长（R1 红线）。
                if (bundled.IsLoaded)
                {
                    Fail("随包安装", "自动安装过程中加载了程序集 —— 启动开销会随随包插件数量线性增长");
                }

                int bundledAgain = PluginHost.AutoInstallBundledPlugins();
                Line($"  重复启动：再装入 {bundledAgain} 个（应为 0，幂等）");
                if (bundledAgain != 0)
                {
                    Fail("随包安装", $"重复自动安装应无事可做，实际又处理了 {bundledAgain} 个");
                }

                // 用户停用之后，启动流程绝不能把它重新启用。
                PluginHost.Disable(bundledPluginId, out _);
                bundled.WaitForUnloadVerdict(5000);
                int bundledAfterDisable = PluginHost.AutoInstallBundledPlugins();

                Line($"  用户停用后再启动：再装入 {bundledAfterDisable} 个｜登记启用={bundled.Entry.Enabled}");

                if (bundled.Entry.Enabled || bundledAfterDisable != 0)
                {
                    Fail("随包安装",
                        "用户停用过的随包插件被启动流程重新启用了 —— 「我明明关过它」只能来自这里");
                }

                // 宿主区文件被删：应当补回，且「停用」这个选择不受影响。
                string managedDirectory = bundled.ManagedDirectory;
                if (Directory.Exists(managedDirectory)) Directory.Delete(managedDirectory, recursive: true);

                int bundledRestored = PluginHost.AutoInstallBundledPlugins();
                bool payloadBack = Directory.Exists(managedDirectory)
                    && Directory.GetFiles(managedDirectory, "*.dll", SearchOption.TopDirectoryOnly).Length > 0;

                Line($"  宿主区文件被删后再启动：补回 {bundledRestored} 个｜文件已回={payloadBack}｜" +
                    $"登记启用={bundled.Entry.Enabled}");

                if (!payloadBack)
                {
                    Fail("随包安装", "随包插件的宿主区文件被删后没有补回 —— 它会一直显示成「加载失败」");
                }

                if (bundled.Entry.Enabled)
                {
                    Fail("随包安装", "补文件把用户「停用」的选择改掉了 —— 补文件不是让它复活的理由");
                }

                // 不可卸载：必须拒绝，并且说清「改用停用」。
                bool bundledUninstalled = PluginHost.Uninstall(bundledPluginId, removePluginData: true, out string bundledUninstallError);
                Line($"  卸载随包插件的结论：{bundledUninstallError}");

                if (bundledUninstalled)
                {
                    Fail("随包安装", "随包插件被卸载掉了 —— 它下次启动还会回来，等于骗用户白点一下");
                }
                else if (bundledUninstallError.IndexOf("停用", StringComparison.Ordinal) < 0)
                {
                    Fail("随包安装", $"拒绝卸载时必须告诉用户该改用什么，实际文案：{bundledUninstallError}");
                }
            }

            // ---- 3i 顶层类型认领 ----
            //
            // 这一段验的是「随包动作包接管用户配置里的顶层 Type」这条主干：
            // Launch / WebUrl / Folder / Command / ShellTool 从内建动作表里搬走、改由插件执行，
            // 而用户配置一个字都不用改（Type 字符串仍是老样子）。
            //
            // 特意跑在这里而不是 [3c]：认领只对**随包**插件生效，而 [2] 走的是社区安装路径
            // （Bundled=false），那时候认领表本来就该是空的。上面 [3h] 刚把这个插件
            // 按随包方式装上，那正是本段唯一成立的时点。
            //
            // 这条链路上有四处「错了也不报错」的坑，全部在这里钉死：
            //   ① 清单声明了认领，认领表却没建起来 —— 动作会掉进 switch 的 default 分支；
            //   ② 认领指向一个不存在的贡献点 —— 按下去只得到一句「插件没提供这个动作」；
            //   ③ 被认领的动作同时出现在「🔌 插件」子下拉里 —— 用户会在两处配到同一个动作，
            //      而两处的持久化形态互不兼容；
            //   ④ 宿主字段白名单与投影器漂移 —— 用户配的路径 / 网址送不进插件，且毫无提示。
            Line("");
            Line("[3i] 顶层类型认领（随包动作包接管用户配置里的顶层 Type）");

            if (manifest.ClaimedTypes == null || manifest.ClaimedTypes.Count == 0)
            {
                Line("  本插件的清单未声明顶层类型认领（StarPiePluginTypeClaims），跳过。");
                Line("  这不是缺陷：社区插件本来就不允许认领顶层类型，只能走 Type=\"Plugin\" + 引用。");
            }
            else if (bundled == null)
            {
                Fail("类型认领", "插件未能作为随包插件登记，认领链路无从验证");
            }
            else
            {
                // 断言体独立成方法且禁止内联 —— 与 RunEnableAndInvoke / RunRestartEquivalenceProbe
                // 同一个理由，而且这里是硬需求：它会拿到 PluginActionRegistration
                // （指向插件程序集里的类型实例）。
                RunTypeClaimChecks(manifest, Line, Fail);

                // ⑧ 认领建起来之后，判据要把这些类型送到 ClaimedType 这条路上去。
                //
                // [3g] 里断言的是「它们不是 Builtin」；这里补上另一半：认领生效之后，
                // Execute() 必须真的把它们交给插件，而不是继续掉进 switch 的 default。
                // 两段合起来才闭环 —— 只看 [3g] 的话，「从内建删了、认领也没建」同样能过。
                int claimRouted = 0;
                foreach (PluginTypeClaim claimed in manifest.ClaimedTypes)
                {
                    if (ActionExecutor.ClassifyAction(claimed.TypeName) == ActionExecutor.ActionDispatchKind.ClaimedType)
                    {
                        claimRouted++;
                    }
                    else
                    {
                        Fail("类型认领",
                            $"已认领的「{claimed.TypeName}」经 Execute() 的判据仍然不是 ClaimedType —— " +
                            "用户配好的这个动作不会走到插件实现");
                    }
                }

                // 反向核对：认领表里不能有任何一项同时还是内建动作。
                // RebuildClaimTable 里有一条「内建优先」的拒绝规则，这条断言就是它的守卫 ——
                // 规则被删掉不会有任何编译错误，只会让同一个 Type 悄悄挂上两条路。
                foreach (PluginHost.PluginTypeClaimBinding claimBinding in PluginHost.SnapshotClaims())
                {
                    if (BuiltinActionCatalog.TryGet(claimBinding.TypeName, out BuiltinActionRegistration claimedAlsoBuiltin))
                    {
                        Fail("类型认领",
                            $"「{claimBinding.TypeName}」既在认领表里、又还在内建动作表里" +
                            $"（{claimedAlsoBuiltin.FullId}）—— 内建优先的拒绝规则没生效");
                    }
                }

                Line($"  认领判据：{claimRouted}/{manifest.ClaimedTypes.Count} 项经 Execute() 判据路由到插件 ✓");

                // ⑦ 停用该随包插件之后：认领仍在（只有这样才能对用户说出「包被停用了」），
                // 但可用性判断必须为 false，并给出可操作的文案 —— 绝不静默什么都不做。
                //
                // 这一段刻意留在 Run 的帧上执行：上面刚把插件程序集拉起来，而 ALC 的回收结论
                // 对「谁还持着插件侧对象」极其敏感。停用与等待必须发生在不再持有任何
                // 插件类型引用的帧上，否则结论永远是「需要重启」，沙箱里那份 dll 也永远删不掉
                // （实测：每跑一次自检都在 %TEMP% 里留下一坨删不掉的 .pending-delete 残留）。
                PluginHost.Disable(bundledPluginId, out _);
                bool claimUnloaded = bundled.WaitForUnloadVerdict(5000);

                string probeTypeName = manifest.ClaimedTypes[0].TypeName;
                bool stillClaimed = PluginHost.TryResolveClaimedType(probeTypeName, out _);
                bool available = PluginHost.IsClaimedTypeAvailable(probeTypeName, out string unavailableReason);

                Line($"  停用后：程序集已释放={claimUnloaded}｜认领仍在={stillClaimed}｜可用={available}");
                Line($"  给用户的提示：{unavailableReason}");

                if (!stillClaimed)
                {
                    Fail("类型认领",
                        "插件被停用后认领从表里消失了 —— 用户只会看到「无法识别的动作类型」，猜不到是动作包被停了");
                }
                else if (available)
                {
                    Fail("类型认领", "插件已停用，可用性判断却仍为 true —— 执行会走进插件的失败路径，而不是给出提示");
                }
                else if (unavailableReason.IndexOf("停用", StringComparison.Ordinal) < 0)
                {
                    Fail("类型认领", $"不可用提示里没有告诉用户「去启用它」，实际文案：{unavailableReason}");
                }
            }

            // ---- 3k 随包插件的登记信息随文件刷新 ----
            //
            // 【为什么单独守这一段】
            // 登记表里的认领清单 / 版本 / 能力集合，全都是**首次安装那一刻的快照**。
            // 两种常见情形会让快照永久过时：
            //   · 随包插件升级后新增了认领 —— 新类型永远不生效；
            //   · 把某个类型从一个包挪到另一个包（拆包）—— 旧包的老快照与新包的新声明
            //     同时认领同一个类型，触发 RebuildClaimTable 的「多包抢同一类型 → 整对拒绝」，
            //     **两个包的全部动作一起失效**，而用户唯一能看到的线索是日志里一行 Error。
            //
            // 验收标准是两条，缺一不可：
            //   ① 登记信息必须被来源区那枚 dll 的当前元数据刷新（拆包才交割得掉）；
            //   ② 刷新**绝不能**碰用户的选择（Enabled / Preload）——
            //     否则它就变成了「偷偷把用户停用过的包重新启用」，比原来的 bug 更糟。
            Line("");
            Line("[3k] 随包插件的登记信息随文件刷新（拆包与升级的前置）");

            if (bundled == null)
            {
                Line("  （随包插件未能登记，本段跳过 —— 上面 [3h] 已给出失败原因）");
            }
            else
            {
                // 只传 ID，不传实例：本方法会按启动顺序重跑 SyncFromDisk，
                // 帧上不持有任何可能牵涉插件侧对象的引用（与 RunEnableAndInvoke 同一纪律）。
                RunBundledMetadataRefreshChecks(bundledPluginId, manifest, Line, Fail);
            }

            // 清理现场：走 UninstallCore(respectBundledGuard: false) 而不是用户路径 ——
            // 用户路径上的那道守卫正是本段被测的东西，拿它来收尾就成了用被测对象验证它自己。
            PluginHost.Disable(bundledPluginId, out _);
            PluginHost.Find(bundledPluginId)?.WaitForUnloadVerdict(5000);

            if (!PluginHost.UninstallCore(bundledPluginId, removePluginData: true,
                    respectBundledGuard: false, out string bundledCleanupError))
            {
                Fail("随包安装清理", bundledCleanupError);
            }

            if (Directory.Exists(sandboxScanRoot))
            {
                Directory.Delete(sandboxScanRoot, recursive: true);
            }

            // ---- 3j 宿主服务面与能力门禁 ----
            //
            // 这一段验的是「插件干活时真正碰到的那两层宿主接口」，与具体插件无关，
            // 所以刻意放在随包插件清理之后 —— 它不需要任何插件在场，也不加载程序集。
            //
            // 守的是一处**设计意图**，而不是某个具体实现：
            // 「安装确认页上展示的能力，真的对应一个后果」。
            //
            // 必须在这里说清的是：门禁换来的**不是安全**。进程内插件本来就能自己
            // Process.Start，SDK 拦不住 —— 它拦的只是「让宿主替你干活」这条路径。
            // 用户看到「本插件需要「进程」能力」与「它其实什么都能干」之间的矛盾，
            // 是进程内插件模型的固有代价；摊开写在这里，免得后来者以为这里守住了什么。
            //
            // 反过来，这条门禁要是漏了，插件清单里的能力声明就成了一句空话：
            // 安装页照旧弹一个「需要「进程」能力」的确认框，用户点了同意，
            // 而这个勾选在运行时没有任何对应物 —— 那才是真正骗人的地方。
            Line("");
            Line("[3j] 宿主服务面与能力门禁（命令 / Shell 动词）");

            // ① 类型关系：拒绝异常刻意不继承 PluginContractException。
            //
            // 后者会让宿主把插件整体标记为加载失败并卸载 —— 而「清单里漏了一行能力声明」
            // 远不到那个程度。真继承上去，用户看到的是「插件突然坏了 / 被系统禁用了」，
            // 排查方向会完全跑偏。
            if (typeof(PluginContractException).IsAssignableFrom(typeof(PluginCapabilityDeniedException)))
            {
                Fail("能力门禁",
                    "PluginCapabilityDeniedException 继承了 PluginContractException —— " +
                    "漏写一行能力声明会让整个插件被卸载，而用户看到的提示是「插件坏了」");
            }

            const string gateProbePluginId = "starpie.selftest.gate";
            var deniedCommandService = new PluginCommandService(gateProbePluginId, PluginCapability.None);
            var deniedShellService = new PluginShellService(gateProbePluginId, PluginCapability.None);

            // ② 未声明 Process：必须拒绝。
            //
            // 探针传的是空命令 / 空动词 —— 但这一点都不影响结论：
            // 门禁是 RequireCapability 的第一件事，排在「空值短路」之前，
            // 所以被拒绝时命令根本没被分析过。更重要的是，它证明门禁确实在 Guard **之外** ——
            // 若挪进 Guard 里，异常会被吞掉、转成一个 false 返回值，
            // 用户看到的是「命令没执行」，而不是「本插件缺少「进程」能力」。
            (bool commandDenied, string commandGateDetail) =
                ProbeCapabilityGate(() => deniedCommandService.Run(""));

            if (commandDenied)
            {
                Line($"  Commands.Run：{commandGateDetail} ✓");
            }
            else
            {
                Fail("能力门禁", $"未声明 Process 的插件调用 Commands.Run 没有被正确拒绝：{commandGateDetail}");
            }

            (bool shellDenied, string shellGateDetail) =
                ProbeCapabilityGate(() => deniedShellService.Invoke(""));

            if (shellDenied)
            {
                Line($"  Shell.Invoke：{shellGateDetail} ✓");
            }
            else
            {
                Fail("能力门禁", $"未声明 Process 的插件调用 Shell.Invoke 没有被正确拒绝：{shellGateDetail}");
            }

            // ③ 声明了 Process：同一个调用必须放行。
            //
            // 少了这一半，把门禁写成「永远拒绝」也能通过上面两条 ——
            // 而那会让所有正常插件都废掉，且现象与「插件坏了」一模一样。
            var allowedCommandService = new PluginCommandService(
                gateProbePluginId, PluginCapability.Process | PluginCapability.FileSystem);

            try
            {
                bool emptyCommandResult = allowedCommandService.Run("   ");

                if (emptyCommandResult)
                {
                    Fail("能力门禁", "空命令竟然报告执行成功 —— 空值短路失效，用户会以为命令跑过了");
                }
                else
                {
                    Line("  已声明 Process：放行 ✓（空命令由空值短路拦下，未真的起进程）");
                }
            }
            catch (PluginCapabilityDeniedException denied)
            {
                Fail("能力门禁", $"已声明 Process 却被拒绝（{denied.Capability}）—— 门禁判据写错了，正常插件会全部废掉");
            }
            catch (Exception gateError)
            {
                Fail("能力门禁", $"已声明 Process 的调用抛出异常：{gateError}");
            }

            // ④ 元数据不受门禁约束，这是刻意的。
            //
            // 插件的 Parameters 是属性，声明期（注册前）就要读这两份清单。
            // 在那里抛异常，一个「忘了声明能力」的插件会在注册阶段整个崩掉 ——
            // 而它其实只是不能在运行时干活而已。门禁拦的是**产生后果**的调用。
            try
            {
                IReadOnlyList<CommandTerminalOption> terminals = deniedCommandService.Terminals;
                IReadOnlyList<ShellVerbOption> shellVerbs = deniedShellService.Verbs;

                if (terminals.Count == 0)
                {
                    Fail("宿主服务面", "终端清单为空 —— 「运行命令」动作的终端下拉会是空的，用户选不了终端");
                }
                else if (!terminals.Any(t => string.Equals(t.Id, "cmd", StringComparison.OrdinalIgnoreCase)))
                {
                    Fail("宿主服务面", "终端清单里没有 \"cmd\" —— 动作的默认值在界面上选不中任何一项");
                }
                else if (terminals.Any(t => string.IsNullOrWhiteSpace(t.DisplayName)))
                {
                    Fail("宿主服务面", "终端清单里有显示名为空的项 —— 下拉里会出现一个没有文字的选项");
                }
                else if (terminals.Select(t => t.Id).Distinct(StringComparer.OrdinalIgnoreCase).Count() != terminals.Count)
                {
                    Fail("宿主服务面", "终端清单里有重复的标识 —— 下拉选中项会错位到另一个终端上");
                }
                else
                {
                    Line($"  终端清单：{terminals.Count} 项，含 cmd ✓（未声明能力也能读，因为它不产生后果）");
                }

                // Shell 动词：用户配置里存的是短 ID（copy_path），不是 Verb（Windows.CopyAsPath）。
                // 清单漏项不会有任何报错 —— 只会让那个动作在挑选器里找不到对应项。
                if (shellVerbs.Count == 0)
                {
                    Fail("宿主服务面", "Shell 动词清单为空 —— 该动作的下拉会是空的");
                }
                else if (!shellVerbs.Any(v => string.Equals(v.Id, "copy_path", StringComparison.OrdinalIgnoreCase)))
                {
                    Fail("宿主服务面",
                        "Shell 动词清单里没有 \"copy_path\" —— 用户配置里存的就是这个短 ID，" +
                        "少了它老配置在挑选器里找不到对应项（注意：清单要的是 Id，不是 Verb）");
                }
                else if (shellVerbs.Select(v => v.Id).Distinct(StringComparer.OrdinalIgnoreCase).Count() != shellVerbs.Count)
                {
                    Fail("宿主服务面", "Shell 动词清单里有重复的标识 —— 选中项会错位");
                }
                else
                {
                    Line($"  Shell 动词清单：{shellVerbs.Count} 项，含 copy_path ✓");
                }
            }
            catch (PluginCapabilityDeniedException deniedMeta)
            {
                Fail("宿主服务面",
                    $"读元数据（终端 / 动词清单）被能力门禁拦下了（{deniedMeta.ServiceName}）—— " +
                    "插件的 Parameters 是声明期就要读它的，这会让忘了声明的插件在注册阶段整个崩掉");
            }

            // ⑤ SDK 契约版本号的内部一致性。
            //
            // ApiVersion 是个手写常量：C# 的常量插值只对 string 常量成立，
            // 这两个组成部分是 int，所以拼不出来（CS0133）。这处重复只能靠断言守。
            // 漏改的表现极其隐蔽：插件按 ApiVersion 做兼容判断，而它和真实版本号对不上。
            string expectedApiVersion = $"{PluginApi.ApiVersionMajor}.{PluginApi.ApiVersionMinor}";

            if (!string.Equals(PluginApi.ApiVersion, expectedApiVersion, StringComparison.Ordinal))
            {
                Fail("SDK 契约",
                    $"ApiVersion（{PluginApi.ApiVersion}）与主次版本号（{expectedApiVersion}）不一致 —— " +
                    "两者手写在两处，改了其中一个却忘了另一个");
            }
            else
            {
                Line($"  SDK 契约版本：{PluginApi.ApiVersion} ✓（与主次版本号一致）");
            }

            // ---- 7 环境还原性检查 ----
            Line("");
            Line("[7] 环境还原性检查");
            Line($"  残留登记插件：{PluginHost.InstalledCount} 个");
            Line($"  残留插件词条：{I18n.ExternalTranslationCount} 条");
            if (I18n.ExternalTranslationCount != 0)
            {
                Fail("词条清理", "卸载后仍有插件词条残留（会造成语言切换时显示脏数据）");
            }
        }
        catch (Exception ex)
        {
            Fail("未捕获异常", ex.ToString());
        }
        finally
        {
            // 自检失败时不要把用户的插件目录弄脏
            if (installedPluginId != null)
            {
                try
                {
                    PluginHost.Uninstall(installedPluginId, removePluginData: true, out _);
                }
                catch
                {
                }
            }

            // 删掉整个沙箱。删不掉要如实说 —— 静默吞掉的话，临时目录会一次次堆出残留，
            // 而下次排查「磁盘怎么满了」时没人会想到是自检干的。
            //
            // 删之前先催 GC 并重试：ALC 的卸载是异步的，卸载判定跑完不代表 CLR 已经放开
            // 文件句柄。不催的话这条删除几乎必然失败，每跑一次自检就在 %TEMP% 里
            // 留下一坨 .pending-delete 残留（实测连续几次自检就攒下了好几个）。
            try
            {
                const int attempts = 5;

                for (int attempt = 1; attempt <= attempts; attempt++)
                {
                    GC.Collect();
                    GC.WaitForPendingFinalizers();
                    GC.Collect();

                    try
                    {
                        Directory.Delete(sandboxRoot, recursive: true);
                        break;
                    }
                    catch (Exception) when (attempt < attempts)
                    {
                        System.Threading.Thread.Sleep(200);
                    }
                }
            }
            catch (Exception cleanupError)
            {
                Line($"  [WARN] 沙箱未能删除（{cleanupError.Message}）：{sandboxRoot}");
                Line("         残留里只有 .pending-delete 目录，不影响正确性；但请顺手清掉，");
                Line("         否则 %TEMP% 会随每次自检一点点堆起来。");
            }
        }

        Line("");
        Line("====================================================");
        Line(pass ? "自检结论：PASS —— 全链路可用" : "自检结论：FAIL —— 见上面 [FAIL] 项");
        Line("====================================================");

        return Write(report, reportPath, pass);
    }

    /// <summary>
    /// 阶段 3（启用）+ 阶段 4（调用）。
    /// <para>
    /// <b>必须独立成方法并禁止内联</b>：本方法持有 <see cref="PluginActionRegistration"/>，
    /// 它间接指向插件程序集里的类型实例。只有让这些引用随本方法的栈帧一起消失，
    /// 后续「停用 → ALC 卸载」的判定才可能为真。
    /// </para>
    /// </summary>
    /// <returns>失败原因；<c>null</c> 表示两个阶段都通过。</returns>
    [MethodImpl(MethodImplOptions.NoInlining)]
    private static string? RunEnableAndInvoke(string pluginId, Action<string> line, bool skipInvoke = false)
    {
        // ---- 3 启用 ----
        line("");
        line("[3] 启用（加载 → 实例化 → Initialize → 提交贡献点）");
        var sw = Stopwatch.StartNew();
        bool enabled = PluginHost.Enable(pluginId, out string enableError);
        sw.Stop();
        line($"  启用结果：{(enabled ? "成功" : "失败")}");
        line($"  启用耗时：{sw.Elapsed.TotalMilliseconds:F1} ms");
        if (!enabled) return $"启用失败：{enableError}";

        PluginInstance? instance = PluginHost.Find(pluginId);
        line($"  加载耗时（内部计量）：{instance?.LastLoadMs:F1} ms");
        line($"  运行时状态：{instance?.State}");

        List<PluginActionRegistration> actions = PluginHost.GetRegisteredActions();
        line($"  已注册动作：{actions.Count} 个");
        foreach (PluginActionRegistration action in actions)
        {
            line($"    · {action.FullId} | {action.DisplayName} | {action.Kind} | 参数 {action.Parameters.Count} 项");
        }
        if (actions.Count == 0) return "插件启用成功但一个动作都没注册";

        // 词条命中率单独成段。显示名有字面文案兜底，所以「词条没接上」在界面上
        // 与「接上了」长得一模一样 —— 必须在这里显式暴露，否则插件作者要等到
        // 用户切换语言、发现名字没变，才会意识到自己的 key 一直没生效。
        int keyed = 0;
        int resolved = 0;
        var missed = new List<string>();

        foreach (PluginActionRegistration action in actions)
        {
            if (string.IsNullOrEmpty(action.DisplayNameKey)) continue;
            keyed++;
            if (action.DisplayNameFromI18n) resolved++;
            else missed.Add($"{action.ShortId}（{action.DisplayNameKey}）");
        }

        line("");
        line($"  词条解析：声明了 DisplayNameKey 的 {keyed} 个动作中，命中 {resolved} 个");

        // 一个 key 都没声明不算问题：字面 DisplayName 是完全合法且推荐的兜底写法。
        if (keyed > 0 && resolved < keyed)
        {
            line($"    ⚠️ 未命中：{string.Join("、", missed)}");
            line("    这些动作会退回字面 DisplayName 显示，译文不会生效。");
        }

        // ---- 3b 参数校验 ----
        //
        // 这一段验证的是「声明即校验」：插件只声明 ParameterField、一行校验代码都不写，
        // 宿主也必须能拦下空值、越界值与非法选项。
        // 之所以要在这里断言，是因为这一层「没生效」时完全没有外在症状 ——
        // 界面照常渲染、边界值照常存进配置，直到用户触发时插件自己拒绝才暴露。
        line("");
        line("[3b] 参数校验（声明驱动的约束）");

        List<PluginActionRegistration> parameterized = actions.Where(a => a.Parameters.Count > 0).ToList();

        if (parameterized.Count == 0)
        {
            line("  本插件没有声明任何参数，跳过。");
        }
        else
        {
            foreach (PluginActionRegistration candidate in parameterized)
            {
                line($"  样本动作：{candidate.ShortId}（声明 {candidate.Parameters.Count} 项）");

                foreach (ParameterField field in candidate.Parameters)
                {
                    string range = field.Min.HasValue && field.Max.HasValue
                        ? $"　范围 {FormatBound(field.Min.Value)}~{FormatBound(field.Max.Value)}"
                        : (field.Max.HasValue ? $"　上限 {FormatBound(field.Max.Value)}" : "");

                    line($"    · {field.Key}｜{field.Type}｜必填={field.Required}{range}");
                }

                bool hasRequired = candidate.Parameters.Any(
                    p => p.Required && p.Type != ParameterFieldType.Bool);

                // ① 全空输入：声明了必填就必须被拦下
                List<PluginParameterIssue> emptyIssues = PluginParameterValidator.Validate(
                    candidate.Parameters,
                    new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase));

                line($"    ① 全空输入 → {emptyIssues.Count} 项不通过" +
                     (emptyIssues.Count > 0 ? $"（{emptyIssues[0]}）" : ""));

                if (hasRequired && emptyIssues.Count == 0)
                {
                    return $"「{candidate.ShortId}」声明了必填参数，但全空输入未被拦下 —— 空值会直接存进配置。";
                }

                // ②③④ 都需要一份「除被测字段外其余都合法」的基线。
                //
                // 只有当插件为每个字段都声明了 DefaultValue 时这份基线才存在。
                // 否则我们只能自己编一个值（比如给热键字段填 "x"），而那个值可能
                // 恰好过不了插件自己的 ValidationRegex —— 于是断言会因为「别的字段」而
                // 通过或失败，测试自己制造出假阳性与假阴性。自检工具宁可少测一种情形，
                // 也不能给出不可信的结论。
                bool baselineAvailable = candidate.Parameters.All(
                    p => string.IsNullOrEmpty(p.Key) || p.DefaultValue != null);

                if (!baselineAvailable)
                {
                    line("    ②～④ 跳过：本动作有字段未声明 DefaultValue，无法构造可信的基线输入。");
                    continue;
                }

                Dictionary<string, string> baseline = CollectDefaults(candidate.Parameters);

                // ② 越界输入：把带上限的数值字段设成 上限+1。
                // 断言的是「该字段名下确实出现了错误」，而不是「错误总数 > 0」——
                // 后者可能来自另一个字段，让这条断言在错误的原因下通过。
                ParameterField? ranged = candidate.Parameters.FirstOrDefault(
                    p => p.Type == ParameterFieldType.Number && p.Max.HasValue);

                if (ranged != null)
                {
                    var overflow = new Dictionary<string, string>(baseline, StringComparer.OrdinalIgnoreCase);
                    string tooBig = FormatBound(ranged.Max!.Value + 1);
                    overflow[ranged.Key] = tooBig;

                    List<PluginParameterIssue> overflowIssues =
                        PluginParameterValidator.Validate(candidate.Parameters, overflow);

                    bool attributed = overflowIssues.Any(
                        i => string.Equals(i.Key, ranged.Key, StringComparison.OrdinalIgnoreCase));

                    line($"    ② {ranged.Key}={tooBig}（上限 {FormatBound(ranged.Max.Value)}）→ " +
                         (attributed ? "已拦下" : "未拦下"));

                    if (!attributed)
                    {
                        return $"「{candidate.ShortId}」的 {ranged.Key} 超过声明上限却未被拦下。";
                    }
                }

                // ③ 正向用例：按声明的默认值填充，必须全部通过。
                // 缺了这条，任何「一律报错」的实现都能骗过上面两条断言。
                List<PluginParameterIssue> validIssues =
                    PluginParameterValidator.Validate(candidate.Parameters, baseline);

                line($"    ③ 按声明默认值填充 → {validIssues.Count} 项不通过" +
                     (validIssues.Count > 0 ? $"（{validIssues[0]}）" : ""));

                if (validIssues.Count > 0)
                {
                    return $"「{candidate.ShortId}」合法的默认值被判为不合法，会拦住本可正常使用的配置。";
                }

                // ④ 两层校验（宿主声明约束 + 插件自定义）必须对同一份输入给出一致结论。
                // 结论相反时用户会遇到最难自查的一种状态：表单全绿，一触发却被拒。
                //
                // 这里只警告、不判失败：有些插件的规则本身就与默认值互斥
                // （例如「起止时间不能相同」而两者默认值恰好相同），那是声明的写法问题，
                // 不该被自检判成宿主缺陷。
                ActionItem? probeItem = PluginHost.CreateActionItem(candidate.FullId, baseline);
                if (probeItem != null)
                {
                    PluginHost.PluginActionValidation unified =
                        PluginHost.ValidateActionParameters(probeItem);

                    line($"    ④ 走统一入口校验同一份输入 → {(unified.IsValid ? "通过" : "不通过")}");

                    if (!unified.IsValid)
                    {
                        line($"       ⚠️ {unified.Describe()}");
                        line("          声明约束与插件自定义校验结论相反。若两者规则本身互斥（如默认值不满足自定规则），");
                        line("          属声明写法问题；否则说明有一层漏判。此项不判失败，请作者自行确认。");
                    }
                }
            }
        }

        // ---- 3c 选择器接缝 ----
        line("");
        line("[3c] 动作选择器接缝（类型收敛 + 按插件分组的子下拉）");

        // 类型下拉里的插件项只能有一项。
        // 若像早先那样把每个插件动作都平铺进去，装十个插件就会多出上百项，
        // 把内置动作挤到看不见的地方 —— 而内置项的顺序属于用户的肌肉记忆。
        List<ActionTypeItem> typeItems = PluginActionBinding.BuildActionTypeItems();
        line($"    类型下拉里的插件项：{typeItems.Count} 项（应为 1 项）");
        if (typeItems.Count != 1)
        {
            return $"类型下拉里的插件项应为 1 项，实际 {typeItems.Count} 项 —— 装一个插件就多一项会把内置动作挤走。";
        }
        if (!string.Equals(typeItems[0].Tag, PluginApi.ActionTypeName, StringComparison.Ordinal))
        {
            return $"类型下拉的插件项 Tag 应为 {PluginApi.ActionTypeName}，实际是「{typeItems[0].Tag}」。";
        }

        // 子下拉候选：每个已注册动作都必须出现，且同一插件的动作落在同一分组。
        List<PluginActionRegistration> registered = PluginHost.GetRegisteredActions();
        List<PluginActionItem> options = PluginActionBinding.BuildPluginActionItems();

        line($"    子下拉候选：{options.Count} 项 / 已注册动作 {registered.Count} 项");
        if (options.Count != registered.Count)
        {
            return "子下拉候选数与已注册动作数不一致 —— 用户会看到少了动作，却无从判断少了哪些。";
        }

        var groupOfPlugin = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (PluginActionRegistration registration in registered)
        {
            PluginActionItem? option = options.FirstOrDefault(o => o.FullId == registration.FullId);
            if (option == null)
            {
                return $"已注册动作 {registration.FullId} 未出现在子下拉候选里。";
            }
            if (string.IsNullOrWhiteSpace(option.GroupName))
            {
                return $"动作 {registration.FullId} 没有分组名 —— 它在下拉里会成为没有归属的孤儿项。";
            }

            // 同一插件的动作必须归入同一分组。若按动作名去分组，
            // 一个插件的各个动作会各自成组，界面立刻变成一锅粥。
            if (groupOfPlugin.TryGetValue(registration.PluginId, out string? existing))
            {
                if (!string.Equals(existing, option.GroupName, StringComparison.Ordinal))
                {
                    return $"同一插件（{registration.PluginId}）的动作被分到了不同分组：" +
                           $"「{existing}」与「{option.GroupName}」。";
                }
            }
            else
            {
                groupOfPlugin[registration.PluginId] = option.GroupName;
            }
        }

        // 插件显示名是否真的互相冲突 —— 只有冲突时，组标题才允许带上插件 ID 后缀。
        List<string> pluginIds = new List<string>(groupOfPlugin.Keys);
        var displayNames = pluginIds
            .Select(id => PluginActionBinding.ResolvePluginDisplayName(id))
            .ToList();
        bool nameCollision = displayNames.Count != displayNames.Distinct(StringComparer.Ordinal).Count();

        foreach (string ownerId in pluginIds)
        {
            string groupName = groupOfPlugin[ownerId];
            string expectedName = PluginActionBinding.ResolvePluginDisplayName(ownerId);
            int count = registered.Count(r => string.Equals(r.PluginId, ownerId, StringComparison.Ordinal));
            line($"    分组「{groupName}」→ {count} 个动作");

            if (!nameCollision)
            {
                // 插件名互不相同是常态，此时组标题必须就是插件名本身。
                // 多出任何后缀都会让用户以为装了别的什么插件 —— 而这类问题在界面上
                // 看起来完全正常，只有对着插件列表才发现对不上。
                if (!string.Equals(groupName, expectedName, StringComparison.Ordinal))
                {
                    return $"插件 {ownerId} 的分组名「{groupName}」应为「{expectedName}」—— " +
                           "插件名并不重复，不该给组标题加后缀。";
                }
                continue;
            }

            if (!groupName.StartsWith(expectedName, StringComparison.Ordinal))
            {
                return $"插件 {ownerId} 的分组名「{groupName}」与它的显示名「{expectedName}」不一致 —— " +
                       "子下拉的组标题会与详情面板里的插件标识对不上号。";
            }
        }

        // 写读往返：子下拉选中 → 落库 → 再投影回下拉，必须仍是同一个动作。
        // 这条路断了会出现最难查的一类故障：界面看着正常，触发时却是另一个动作。
        foreach (PluginActionRegistration registration in registered)
        {
            var probe = new ActionItem
            {
                Type = PluginApi.ActionTypeName,
                ExtensionData = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase),
            };

            if (!PluginActionBinding.Apply(probe, registration.FullId))
            {
                return $"写入动作 {registration.FullId} 失败。";
            }

            string? projected = PluginActionBinding.ProjectSelectedAction(probe);
            if (!string.Equals(projected, registration.FullId, StringComparison.Ordinal))
            {
                return $"动作 {registration.FullId} 写入后投影回来变成了「{projected ?? "(空)"}」—— " +
                       "界面会显示成没选动作。";
            }

            if (PluginActionBinding.IsReferenceBroken(probe))
            {
                return $"刚写入的动作 {registration.FullId} 立刻被判为「引用已失效」。";
            }

            if (!string.Equals(probe.Type, PluginApi.ActionTypeName, StringComparison.Ordinal))
            {
                return $"写入后 Type 变成了「{probe.Type}」，应为 {PluginApi.ActionTypeName} —— 类型下拉会选不中。";
            }
        }
        line($"    写读往返：{registered.Count} 个动作全部一致");

        // 切回内置类型必须清干净，否则留下「内置类型 + 悬挂插件引用 + 插件参数」的混合状态，
        // 那种配置界面上看不出来，却会在导出与执行时各表现一次。
        var cleared = new ActionItem { Type = PluginApi.ActionTypeName };
        PluginActionBinding.Apply(cleared, registered[0].FullId);
        PluginActionBinding.Clear(cleared);
        if (cleared.PluginActionRef != null || cleared.ExtensionData != null)
        {
            return "Clear 之后仍有插件引用或插件参数残留。";
        }
        line("    切回内置类型：插件引用与插件参数均已清空");

        // ---- 4 调用 ----
        line("");
        line("[4] 调用动作（走与轮盘完全相同的接缝）");

        if (skipInvoke)
        {
            // 只想确认识别 / 注册 / 参数校验 / 选择器接缝时应当走这条：「真执行一次动作」
            // 对亮度、音量、剪贴板这类动作就是实打实的副作用，CI 与排查问题
            // 都不该顺手改动用户的机器（实测踩过：反复跑自检把屏幕亮度从 15% 推到 75%）。
            line("  已跳过（--skip-invoke）：识别、注册、参数校验与选择器接缝断言均已跑过，本机环境未被改动。");
            return null;
        }

        // 这一节是**真执行**，不是只读检查。
        // 明写出来是必要的：自检报告通篇读起来像一次静态体检，
        // 而亮度插件这类动作一旦被执行就会真的改变系统状态 ——
        // 作者若以为它是只读的，就会在排查问题时反复跑自检，
        // 结果是把用户的屏幕、音量或剪贴板越改越乱却毫无察觉。
        line("  ⚠️ 本节会真实调用一次动作，可能改变系统状态（如亮度、音量、剪贴板）。");
        PluginActionRegistration first = actions[0];
        ActionItem? actionItem = PluginHost.CreateActionItem(first.FullId);
        if (actionItem == null) return "CreateActionItem 返回 null";

        line($"  动作 Type：{actionItem.Type}");
        line($"  引用：{actionItem.PluginActionRef}");
        line($"  参数：{DescribeParameters(actionItem.ExtensionData)}");

        sw.Restart();
        PluginExecuteOutcome outcome = PluginHost.ExecutePluginAction(actionItem);
        sw.Stop();

        line($"  是否被处理：{outcome.Handled}");
        line($"  成功：{outcome.Success}");
        line($"  后台执行：{outcome.QueuedToBackground}");
        line($"  返回信息：{outcome.Message}");
        line($"  调用耗时：{sw.Elapsed.TotalMilliseconds:F3} ms");

        if (!outcome.Handled || !outcome.Success) return $"动作调用失败：{outcome.Message}";

        // 负向用例：引用一个不存在的贡献点。
        //
        // 这里刻意**不用**「缺少必填参数」来构造负例：那需要真的调用一次动作，
        // 对无参数的动作（例如亮度插件的多数动作）会真的被执行一遍，
        // 于是「自检」本身产生了副作用 —— 屏幕亮度被多调了一次。
        // 改用不存在的贡献点，既能验证宿主的防御路径（不崩溃、不静默成功），
        // 又保证零副作用，而且对任何插件都成立。
        line("  负向用例：引用不存在的贡献点（零副作用）");
        var ghost = new ActionItem
        {
            Type = PluginApi.ActionTypeName,
            Name = first.DisplayName,
            PluginActionRef = new PluginActionRef { PluginId = first.PluginId, ContributionId = "no_such_contribution_zzz" },
            ExtensionData = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase),
        };
        PluginExecuteOutcome ghostOutcome = PluginHost.ExecutePluginAction(ghost);
        line($"    被处理={ghostOutcome.Handled} 成功={ghostOutcome.Success} 信息={ghostOutcome.Message}");

        if (ghostOutcome.Success)
        {
            return "宿主防御异常：引用不存在的贡献点却报告成功，用户会看到一个不存在的动作被静默执行。";
        }

        return null;
    }

    /// <summary>
    /// [3d] ⑤.5「重启等价态」的探针体：插件已启用、但程序集尚未惰性加载。
    /// <para>
    /// <b>必须独立成方法并禁止内联</b>：本方法会持有 <see cref="PluginActionRegistration"/>
    /// （它指向插件程序集里的类型实例），而它内部要停用插件并等 ALC 回收结论。
    /// 这些引用若留在调用方（<c>Run</c>）的栈帧上，结论必然变成「需要重启」，
    /// 沙箱里那份 dll 也就永远删不掉 ——
    /// 实测表现就是每跑一次自检都在 %TEMP% 里留下一坨 <c>.pending-delete</c> 残留。
    /// </para>
    /// </summary>
    /// <returns>失败原因；<c>null</c> 表示本段通过。</returns>
    [MethodImpl(MethodImplOptions.NoInlining)]
    private static string? RunRestartEquivalenceProbe(
        PluginInstance? installed,
        string pluginId,
        Action<string> line)
    {
        PluginActionRegistration? restartTarget = installed?.OwnedActions.FirstOrDefault();

        if (restartTarget == null)
        {
            return "插件没有注册任何动作，无法验证「已启用但尚未加载」这条路径";
        }

        // 先把「用户配好的那个动作」照原样造出来 —— 停用会清空 OwnedActions。
        var restartProbe = new ActionItem
        {
            Type = PluginActionBinding.TypeName,
            Name = restartTarget.DisplayName,
            PluginActionRef = new PluginActionRef
            {
                PluginId = pluginId,
                ContributionId = restartTarget.ShortId,
            },
            ExtensionData = CollectDefaults(restartTarget.Parameters),
        };

        string restartFullId = restartTarget.FullId;

        // 模拟重启：停用（连带卸载程序集）→ 只把登记态改回「已启用」→ 与磁盘对账。
        // 全程不调 Enable，程序集因此保持未加载 —— 这正是真实重启后的状态。
        PluginHost.Disable(pluginId, out _);
        installed!.WaitForUnloadVerdict(5000);
        PluginRegistryStore.SetEnabled(pluginId, true);
        PluginHost.SyncFromDisk();

        bool loadedAfterRestart = installed.IsLoaded;
        line($"  重启等价态：程序集已加载={loadedAfterRestart}，" +
            $"登记启用={installed.Entry.Enabled}，目录内动作={PluginHost.Catalog.SnapshotActionIds().Count} 个");

        if (loadedAfterRestart)
        {
            return "模拟方式失效：本该未加载的程序集却已加载，本段前提不成立";
        }

        if (!installed.Entry.Enabled)
        {
            return "模拟方式失效：登记态未回到「已启用」，本段前提不成立";
        }

        if (PluginActionBinding.IsReferenceBroken(restartProbe))
        {
            return "仅仅因为「尚未惰性加载」，用户配好的动作就被判成「已失效」。" +
                   "用户什么都没做，界面上却显示配置丢了 —— " +
                   "必须区分「插件没了」与「插件还没加载」这两种完全不同的情况";
        }

        PluginExecuteOutcome restartOutcome = PluginHost.ExecutePluginAction(restartProbe);
        bool recovered = PluginHost.TryGetAction(restartFullId, out _);

        line($"  重启后按下动作：Handled={restartOutcome.Handled}，Success={restartOutcome.Success}，" +
            $"惰性加载后目录内动作={PluginHost.Catalog.SnapshotActionIds().Count} 个");

        if (!recovered)
        {
            return $"按下动作后插件仍未被拉起、贡献点仍不在目录里：{restartFullId}。" +
                   $"执行结论：{restartOutcome.Message}";
        }

        return null;
    }

    /// <summary>
    /// [3k] 随包插件登记信息刷新的断言体：模拟「老用户带着过时的登记快照升级 / 拆包」。
    /// <para>
    /// <b>独立成方法</b>：它按真实启动顺序重跑 <c>AutoInstall → SyncFromDisk → RebuildClaimTable</c>，
    /// 会就地更新内存实例。与 <see cref="RunTypeClaimChecks"/> 同一纪律 ——
    /// 帧上不持有任何插件侧类型引用，否则后面「停用 → 等 ALC 回收」的结论永远是「需要重启」，
    /// 沙箱里那份 dll 也删不掉。
    /// </para>
    /// </summary>
    /// <param name="pluginId">随包插件的 ID（取自它自己的清单，不写死）。</param>
    /// <param name="manifest">来源区那枚 dll 的真实清单 —— 它就是「文件的事实」。</param>
    [MethodImpl(MethodImplOptions.NoInlining)]
    private static void RunBundledMetadataRefreshChecks(
        string pluginId,
        PluginManifest manifest,
        Action<string> line,
        Action<string, string> fail)
    {
        PluginRegistryEntry? entry = PluginRegistryStore.FindEntry(pluginId);
        if (entry == null)
        {
            fail("登记刷新", $"找不到 {pluginId} 的登记条目，刷新链路无从验证");
            return;
        }

        // 制造一份「过时的旧快照」。
        //
        // 刻意用一个**此刻不存在的假类型名**，而不是「真实声明里少几项」：
        // 后者在「登记表恰好没被刷新」时仍可能因为与真实声明部分重合而侥幸通过，
        // 那就成了用一个必然成立的断言去证明一件没发生的事。
        const string StaleVersion = "0.0.1-stale";
        entry.ClaimedTypes = new List<string> { "Stale.Type=staleContribution" };
        entry.Version = StaleVersion;
        PluginRegistryStore.UpsertEntry(entry);

        // 先记下用户的选择。刷新**只同步「文件是什么」，不同步「用户怎么选」**，
        // 下面两个断言守的就是这条线。
        bool enabledBefore = entry.Enabled;
        bool preloadBefore = entry.Preload;

        // 按真实启动顺序重跑。三步缺一不可、顺序也不能换 ——
        // 认领表读的是实例上的登记条目，而实例是 SyncFromDisk 按登记表建立的。
        PluginHost.AutoInstallBundledPlugins();
        PluginHost.SyncFromDisk();
        PluginHost.RebuildClaimTable();

        PluginRegistryEntry? refreshed = PluginRegistryStore.FindEntry(pluginId);
        if (refreshed == null)
        {
            fail("登记刷新", $"重跑启动流程后 {pluginId} 的登记条目消失了");
            return;
        }

        List<string> expectedClaims = manifest.ClaimedTypes
            .Where(claim => !string.IsNullOrWhiteSpace(claim.TypeName)
                         && !string.IsNullOrWhiteSpace(claim.ContributionId))
            .Select(claim => claim.ToWire())
            .ToList();

        var actualClaims = new HashSet<string>(
            refreshed.ClaimedTypes ?? new List<string>(),
            StringComparer.OrdinalIgnoreCase);

        line($"  旧快照（认领 1 项 / v{StaleVersion}）→ 重跑启动流程：" +
             $"认领 {actualClaims.Count} 项 / v{refreshed.Version}｜启用 {enabledBefore}→{refreshed.Enabled}");

        if (actualClaims.Count != expectedClaims.Count)
        {
            fail("登记刷新",
                $"认领清单没有随文件刷新：文件声明 {expectedClaims.Count} 项，登记表里是 {actualClaims.Count} 项。" +
                "拆包时这会让新旧两个包同时认领同一个类型，触发「整对拒绝」——两个包的动作一起失效，" +
                "而用户唯一能看到的线索是日志里一行 Error");
        }
        else
        {
            foreach (string expected in expectedClaims)
            {
                if (!actualClaims.Contains(expected))
                {
                    fail("登记刷新",
                        $"认领清单缺少「{expected}」—— 该类型会掉进 switch 的 default 分支，静默无反应");
                }
            }
        }

        if (!string.Equals(refreshed.Version, manifest.Version, StringComparison.Ordinal))
        {
            fail("登记刷新",
                $"版本号没有刷新：文件是 v{manifest.Version}，登记表里还是 v{refreshed.Version} —— " +
                "插件页会长期显示一个早已不存在的版本");
        }

        if (refreshed.Enabled != enabledBefore)
        {
            fail("登记刷新",
                $"刷新登记信息时改动了用户的启用状态（{enabledBefore} → {refreshed.Enabled}）—— " +
                "「我明明关过它」只能来自这里");
        }

        if (refreshed.Preload != preloadBefore)
        {
            fail("登记刷新",
                $"刷新登记信息时改动了预加载开关（{preloadBefore} → {refreshed.Preload}）");
        }

        // 登记表字段刷新了、认领表没重建 —— 用户的动作照样解析不到。
        // 所以还要把「新声明真的进了认领表」验掉，只比对登记表字段是不够的。
        int routed = 0;
        foreach (string wire in refreshed.ClaimedTypes ?? new List<string>())
        {
            List<PluginTypeClaim> parsed = PluginTypeClaim.ParseAll(wire, out _);
            if (parsed.Count != 1) continue;

            if (PluginHost.TryResolveClaimedType(parsed[0].TypeName, out PluginHost.PluginTypeClaimBinding binding)
                && string.Equals(binding.PluginId, pluginId, StringComparison.OrdinalIgnoreCase))
            {
                routed++;
            }
            else
            {
                fail("登记刷新",
                    $"认领表里没有「{parsed[0].TypeName}」（或它指向了别的插件）—— " +
                    "登记表刷新了但认领表没跟上，配置里的动作会落进 switch 的 default");
            }
        }

        if (routed > 0)
        {
            line($"  认领表已按新声明重建：{routed} 项全部路由到 {pluginId} ✓");
        }
    }

    /// <summary>
    /// [3i] 顶层类型认领的断言体（随包动作包接管用户配置里的顶层 Type）。
    /// <para>
    /// <b>必须独立成方法并禁止内联</b>：本方法会持有 <see cref="PluginActionRegistration"/>
    /// 与 <see cref="PluginExecuteOutcome"/> 之外的插件侧对象。只有让这些引用随本方法的
    /// 栈帧一起消失，后续「停用 → 等 ALC 回收结论」才可能为真 —— 而删沙箱就发生在
    /// <c>Run</c> 内部，结论为假就必然留下删不掉的残留。
    /// </para>
    /// </summary>
    [MethodImpl(MethodImplOptions.NoInlining)]
    private static void RunTypeClaimChecks(
        PluginManifest manifest,
        Action<string> line,
        Action<string, string> fail)
    {
        // ① 占位类型必须永远可解析。
        //
        // 未配置的新扇区 Type 是 "Hotkey"（见 WheelLayer.EnsureLayers）；它一旦被外移，
        // 用户停用该动作包之后，所有空扇区的触发都会变成一句「包已停用」。
        if (!BuiltinActionCatalog.TryGet("Hotkey", out _))
        {
            fail("类型认领", "Hotkey 不在内建动作表里 —— 未配置扇区的占位类型会变成不可解析（停用动作包后更明显）");
        }

        // ② 保留类型名 "Plugin" 不可被认领：它对应的是另一套持久化形态，
        // 被认领等于两条截然不同的执行路径挤在同一个 Type 上。
        if (PluginHost.TryResolveClaimedType(PluginApi.ActionTypeName, out _))
        {
            fail("类型认领", $"保留类型名 {PluginApi.ActionTypeName} 被认领了 —— 两条执行路径会挤在同一个 Type 上");
        }

        // ③ 内建优先：还留在内建动作表里的类型，认领会被宿主静默拒绝。
        // 让这个冲突在这里显形，而不是等用户发现「动作行为怎么还是旧的」。
        foreach (PluginTypeClaim declared in manifest.ClaimedTypes)
        {
            if (BuiltinActionCatalog.TryGet(declared.TypeName, out BuiltinActionRegistration stillBuiltin))
            {
                fail("类型认领",
                    $"清单认领的 \"{declared.TypeName}\" 仍由内建动作 {stillBuiltin.FullId} 提供 —— " +
                    "认领会被宿主拒绝，动作行为退回旧路径（双轨制）");
            }
        }

        // 认领表本身是纯字符串（读一次登记表就有），但「贡献点存不存在」得插件真的加载起来
        // 才问得出来。这里显式启用，把后面几条断言的前提摆清楚。
        if (!PluginHost.Enable(manifest.Id, out string claimEnableError))
        {
            fail("类型认领", $"启用随包插件失败，认领链路无从验证：{claimEnableError}");
            return;
        }

        List<PluginHost.PluginTypeClaimBinding> claims = PluginHost.SnapshotClaims();

        line($"  认领表：{claims.Count} 项" +
            (claims.Count > 0
                ? $"（{string.Join("、", claims.Select(c => $"{c.TypeName}→{c.FullId}"))}）"
                : ""));

        // ④ 清单里声明的每一条都必须真的生效，且指向的贡献点必须真的存在。
        foreach (PluginTypeClaim declared in manifest.ClaimedTypes)
        {
            int claimIndex = claims.FindIndex(
                c => string.Equals(c.TypeName, declared.TypeName, StringComparison.OrdinalIgnoreCase));

            if (claimIndex < 0)
            {
                fail("类型认领",
                    $"清单认领的 \"{declared.TypeName}\" 没有进入认领表 —— " +
                    "配置里所有该类型的动作都会掉进「无法识别的动作类型」，用户的配置等于丢了");
                continue;
            }

            PluginHost.PluginTypeClaimBinding binding = claims[claimIndex];
            string expectedFullId = $"{manifest.Id}.{declared.ContributionId}";

            if (!string.Equals(binding.FullId, expectedFullId, StringComparison.Ordinal))
            {
                fail("类型认领",
                    $"\"{declared.TypeName}\" 认领到了 {binding.FullId}，清单声明的是 {expectedFullId}");
                continue;
            }

            // 认领表是纯字符串，不校验的话一个拼错的短 ID 会被原样接受，
            // 直到用户按下去才暴露 —— 而那时看到的是「插件没提供这个动作」，
            // 与真正的原因（清单写错了）隔着好几层。
            if (!PluginHost.TryGetAction(expectedFullId, out PluginActionRegistration claimedAction))
            {
                fail("类型认领",
                    $"认领指向的贡献点 {expectedFullId} 并不存在（插件已加载）—— " +
                    "按下去只会得到一句「插件没提供这个动作」");
                continue;
            }

            // 被认领的动作必须从「🔌 插件」子下拉里消失。否则同一个动作在两处都能选中，
            // 而两处写出的配置形态互不兼容（Type="Launch" vs Type="Plugin" + 引用）。
            bool inSubDropdown = PluginActionBinding.BuildPluginActionItems()
                .Any(o => string.Equals(o.FullId, expectedFullId, StringComparison.Ordinal));
            bool excluded = PluginHost.IsClaimedContribution(expectedFullId);

            if (!excluded || inSubDropdown)
            {
                fail("类型认领",
                    $"{expectedFullId} 仍出现在「插件」子下拉里（IsClaimedContribution={excluded}）—— " +
                    "同一个动作会在两处配到，写出两套互不兼容的配置");
                continue;
            }

            line($"  {declared.TypeName} → {claimedAction.FullId}｜{claimedAction.DisplayName}｜" +
                $"参数 {claimedAction.Parameters.Count} 项｜已排除出子下拉 ✓");
        }

        // ⑤ 宿主字段白名单与投影器不能漂移。
        // 这两处任一处改了名字，用户配的路径 / 网址就送不进插件，
        // 症状却只是「动作没反应」—— 要一路翻到投影器才会发现。
        var fieldProbe = new ActionItem
        {
            Parameter = "p",
            Arguments = "a",
            CommandTerminal = "t",
            BrowserChoice = "b",
            BrowserPath = "bp",
            RunAsStandardUser = true,
        };

        Dictionary<string, string> fieldProjection = ActionParameterProjection.Project(fieldProbe);

        foreach (string fieldKey in HostActionFields.All)
        {
            if (!ActionParameterProjection.CanProject(fieldKey))
            {
                fail("参数投影", $"宿主字段「{fieldKey}」无法被投影 —— 用户配的值送不进插件");
            }
            else if (!fieldProjection.ContainsKey(fieldKey))
            {
                fail("参数投影", $"宿主字段「{fieldKey}」在有值时没有被投影出来");
            }
        }

        // 布尔字段只认小写字面量：投影成 "True" 之类的写法在别的区域设置下
        // 会静默退回默认值 —— 而这里退回的是「以普通用户身份运行」，即降权失效。
        string runAs = fieldProjection.GetValueOrDefault(nameof(ActionItem.RunAsStandardUser)) ?? "";
        if (!string.Equals(runAs, "true", StringComparison.Ordinal))
        {
            fail("参数投影", $"RunAsStandardUser 应投影为 \"true\"，实际 '{runAs}'");
        }

        line($"  宿主字段投影：{HostActionFields.All.Length} 个字段全部可投影 ✓（布尔为小写字面量 ✓）");

        // ⑥ 认领类型的动作必须真的被派发到该插件。
        //
        // 这里刻意挑「参数不全、会被宿主校验拦下」的一条来派发：整条链路
        // （解析认领 → 找到实例 → 查贡献点 → 投影参数 → 校验）全部走一遍，
        // 但绝不会调用插件的 ExecuteAsync —— 自检不能顺手启动一个程序。
        // 若本包全部认领动作在空参数下都合法，就如实说明并跳过，不制造假通过。
        ActionItem? dispatchProbeAction = null;
        PluginHost.PluginTypeClaimBinding dispatchProbeClaim = default;

        foreach (PluginHost.PluginTypeClaimBinding claim in claims)
        {
            var probe = new ActionItem { Type = claim.TypeName, Name = "自检用" };

            PluginHost.PluginActionValidation probeVerdict = PluginHost.ValidateActionParameters(
                claim.FullId, ActionParameterProjection.Project(probe));

            if (!probeVerdict.IsValid)
            {
                dispatchProbeClaim = claim;
                dispatchProbeAction = probe;
                break;
            }
        }

        if (dispatchProbeAction == null)
        {
            line("  派发链路：本包全部认领动作在空参数下都合法，为免真实副作用跳过派发探针。");
            return;
        }

        PluginExecuteOutcome claimOutcome =
            PluginHost.ExecuteClaimedAction(dispatchProbeAction, dispatchProbeClaim);

        line($"  派发链路：Type='{dispatchProbeAction.Type}' → Handled={claimOutcome.Handled}，" +
            $"Success={claimOutcome.Success}｜{claimOutcome.Message}");

        // 被校验拦下 ⇒ Handled=true（确实被认领链路接走了）+ Success=false。
        // Handled=false 意味着它又掉回了「谁都不认识」—— 那正是本段要防的静默失效。
        if (!claimOutcome.Handled)
        {
            fail("类型认领派发",
                $"认领类型 {dispatchProbeAction.Type} 没有被执行器接走（Handled=false）—— " +
                "动作触发时不会有任何反应，也没有任何提示");
        }
        else if (claimOutcome.Success)
        {
            fail("类型认领派发",
                $"空参数的 {dispatchProbeAction.Type} 竟然报告成功 —— " +
                "必填参数没有被拦下，用户的空动作会被当成合法配置执行");
        }
    }

    /// <summary>
    /// 跑一次「应当被能力门禁拒绝」的调用，把结论压成一行可读文本。
    /// <para>
    /// 三种结果必须分得清，否则这条断言等于没写：
    /// <list type="number">
    /// <item>抛了 <see cref="PluginCapabilityDeniedException"/> —— 门禁生效，再看它拦得够不够清楚；</item>
    /// <item>抛了别的异常 —— 门禁没生效，是别的东西炸了；</item>
    /// <item>正常返回 —— 门禁根本不存在。</item>
    /// </list>
    /// 只判「有没有抛异常」会把后两种混在一起，而它们的修法完全不同。
    /// </para>
    /// <para>
    /// 「拦得清楚」的三条判据：能力必须是 <see cref="PluginCapability.Process"/>（不是别的）、
    /// 异常类型不能是 <see cref="PluginContractException"/>（那会让插件被整体卸载）、
    /// 消息里要给出修复动作（去清单里补一行，而不是「权限不足」四个字）。
    /// </para>
    /// </summary>
    private static (bool Denied, string Detail) ProbeCapabilityGate(Func<bool> call)
    {
        try
        {
            bool accepted = call();
            return (false, $"调用被直接放行（返回 {accepted}）—— 门禁不存在");
        }
        catch (PluginCapabilityDeniedException denied)
        {
            if (denied.Capability != PluginCapability.Process)
            {
                return (false, $"拒绝时归因的能力是 {denied.Capability}，应为 Process");
            }

            if (!denied.Message.Contains("capabilities", StringComparison.OrdinalIgnoreCase))
            {
                return (false, $"拒绝消息里没说清该怎么修（未提到清单里的 capabilities 数组）：{denied.Message}");
            }

            return (true, $"已拒绝（{denied.ServiceName} / {denied.Capability}）");
        }
        catch (Exception other)
        {
            return (false, $"抛出的不是 PluginCapabilityDeniedException，而是 {other.GetType().Name}：{other.Message}");
        }
    }

    /// <summary>
    /// 收集插件声明的默认值，作为「应当合法」的基线输入。
    /// <para>
    /// 刻意<b>只</b>照抄声明，不为缺失默认值的字段编造任何值：
    /// 编出来的值（例如给热键字段填 <c>"x"</c>）可能过不了插件自己的
    /// <c>ValidationRegex</c>，于是自检会因为「测试自己造的输入」而报出宿主缺陷。
    /// 调用方需先用 <c>baselineAvailable</c> 确认每个字段都有默认值。
    /// </para>
    /// </summary>
    private static Dictionary<string, string> CollectDefaults(IReadOnlyList<ParameterField> fields)
    {
        var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        foreach (ParameterField field in fields)
        {
            if (string.IsNullOrEmpty(field.Key)) continue;
            if (field.DefaultValue == null) continue;
            result[field.Key] = field.DefaultValue;
        }

        return result;
    }

    /// <summary>把范围边界显示成「0」而不是「0.0」，避免报告里出现无意义的尾数。</summary>
    private static string FormatBound(double value) =>
        value == Math.Floor(value) && Math.Abs(value) < 1e15
            ? ((long)value).ToString(CultureInfo.InvariantCulture)
            : value.ToString("0.####", CultureInfo.InvariantCulture);

    private static string DescribeParameters(Dictionary<string, string>? parameters)
    {
        if (parameters == null || parameters.Count == 0) return "(无)";

        var parts = new List<string>();
        foreach (KeyValuePair<string, string> pair in parameters)
        {
            parts.Add($"{pair.Key}={pair.Value}");
        }
        return string.Join(", ", parts);
    }

    private static int Write(StringBuilder report, string? reportPath, bool pass)
    {
        string text = report.ToString();
        string stamp = DateTime.Now.ToString("yyyyMMdd-HHmmss");

        // 报告是这条通道唯一的产物，**绝不能写不出去还不作声**。
        // 这里以前是个空的 catch：结果是「退出码 0、报告却遍寻不着」，而且毫无线索 ——
        // 一次成功的自检看起来和一次静默失败一模一样。
        string[] candidates = !string.IsNullOrWhiteSpace(reportPath)
            ? new[] { reportPath! }
            : new[]
            {
                Path.Combine(Path.GetTempPath(), $"starpie-plugin-selftest-{stamp}.txt"),
                // 临时目录写不进去（权限受限、被重定向、被清理）时退到日志目录：
                // 那里必然可写，否则日志本身也写不了。
                Path.Combine(AppLogger.GetLogFolderPath(), $"starpie-plugin-selftest-{stamp}.txt"),
            };

        string? written = null;
        Exception? lastError = null;

        foreach (string candidate in candidates)
        {
            try
            {
                string? directory = Path.GetDirectoryName(candidate);
                if (!string.IsNullOrEmpty(directory))
                {
                    Directory.CreateDirectory(directory);
                }
                File.WriteAllText(candidate, text, Encoding.UTF8);
                written = candidate;
                break;
            }
            catch (Exception ex)
            {
                lastError = ex;
            }
        }

        if (written is null)
        {
            AppLogger.LogError("自检报告写入失败（已尝试全部候选路径）", lastError ?? new IOException("未知原因"));
            Console.WriteLine($"[WARN] 自检报告写入失败：{lastError?.Message}");
            Console.WriteLine("报告未能落盘，以下为完整内容：");
            Console.WriteLine(text);
        }
        else
        {
            AppLogger.LogInfo($"自检报告已写入：{written}");
            Console.WriteLine();
            Console.WriteLine($"报告已写入：{written}");
        }

        return pass ? 0 : 1;
    }
}

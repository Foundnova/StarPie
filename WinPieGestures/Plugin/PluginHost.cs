using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using StarPie.Plugin;

namespace WinPieGestures.Plugins;

/// <summary>安装选项。由安装确认卡收集，体现「用户手动选择启用」的产品语义。</summary>
internal sealed class PluginInstallOptions
{
    /// <summary>安装后立即启用。默认 false —— 安装与启用是两个动作。</summary>
    public bool EnableAfterInstall { get; set; }

    /// <summary>目标插件已存在时是否覆盖。</summary>
    public bool OverwriteExisting { get; set; }

    /// <summary>用户是否勾选了「我已了解此插件将以 StarPie 当前权限在进程内运行」。</summary>
    public bool Acknowledged { get; set; }

    /// <summary>用户确认过的能力集合（写入 registry，用于升级时比对是否新增了高风险能力）。</summary>
    public List<string> AcknowledgedCapabilities { get; set; } = new();

    /// <summary>开发者模式：只登记外部路径，不复制文件（便于附加调试器与热重载）。</summary>
    public bool DeveloperExternalPath { get; set; }

    /// <summary>
    /// 写进 <c>registry.json</c> 的安装来源：<c>UserSelectedFile</c>（文件对话框）/
    /// <c>ScanDirectory</c>（只读扫描目录）。
    /// <para>用途只有一个：日后排查「这个插件是怎么进来的」。不做任何逻辑分支。</para>
    /// </summary>
    public string SourceKind { get; set; } = "UserSelectedFile";

    /// <summary>
    /// 本次安装的是<b>随主程序分发的插件</b>。
    /// <para>
    /// 这一类与用户自己装的插件有三处行为差异，都在登记表里用这个标记驱动：
    /// 首启自动安装并启用（不必用户逐个点安装）、不可卸载（只可停用）、
    /// 被从宿主区删掉后会在下次启动时补回来。
    /// </para>
    /// <para>
    /// 刻意不复用 <see cref="SourceKind"/> 来判分支：那个字段的注释写明「不做任何逻辑分支」，
    /// 只用于事后排查。要分支就单独立一个字段，免得日后有人往 Source 里加个新取值
    /// 就悄悄改变了安装语义。
    /// </para>
    /// </summary>
    public bool Bundled { get; set; }
}

internal sealed class PluginInstallResult
{
    public bool Success { get; init; }
    public string PluginId { get; init; } = "";
    public string Error { get; init; } = "";
    public bool Enabled { get; init; }
}

/// <summary>
/// 插件系统门面 —— 主程序与插件世界之间<b>唯一</b>的对外入口。
/// <para>
/// 除 <see cref="PluginHost"/> 之外的宿主模块（Scanner / Loader / Catalog / Invoker / RegistryStore）
/// 全部是 <c>internal</c> 且不对外暴露。这样做的目的是把「主程序需要改动的面」压到最小：
/// <c>ActionExecutor</c> 只需要认识这一个类型的一个方法。
/// </para>
/// </summary>
internal static class PluginHost
{
    /// <summary>贡献点注册表。全局唯一实例。</summary>
    public static readonly PluginCatalog Catalog = new();

    private static readonly object Gate = new();
    private static readonly Dictionary<string, PluginInstance> Instances = new(StringComparer.OrdinalIgnoreCase);

    private static bool _initialized;
    private static bool _enabled = true;
    private static bool _developerMode;
    private static PluginsPreference _preferences = new();

    /// <summary>安全模式：启动时若判定上次是插件导致的崩溃，本次不加载任何插件。</summary>
    private static bool _safeModeActive;

    /// <summary>
    /// 无界面模式（<c>--plugin-selftest</c> / <c>--plugin-paths</c>）：跑完即退，不参与
    /// 启动健康记账。必须在 <see cref="Initialize"/> 之前置位。
    /// </summary>
    public static bool HeadlessMode { get; set; }

    /// <summary>托盘气泡注入点。UI 层设置后插件通知即可显示为气泡。</summary>
    public static Action<string, string>? NotificationSink
    {
        get => PluginNotificationHub.Sink;
        set => PluginNotificationHub.Sink = value;
    }

    public static bool IsInitialized => _initialized;
    public static bool IsEnabled => _enabled;
    public static bool IsSafeModeActive => _safeModeActive;
    public static bool IsDeveloperMode => _developerMode;

    /// <summary>已安装插件数量（不含被忽略的目录）。</summary>
    public static int InstalledCount
    {
        get { lock (Gate) return Instances.Count; }
    }

    // ------------------------------------------------------------------ 生命周期

    /// <summary>
    /// 初始化插件系统。必须在主程序启动早期、且**不阻塞首帧**的前提下调用。
    /// <para>
    /// 这里只做三件廉价的事：解析路径、清理残留、纯静态扫描清单。<b>不加载任何程序集</b>，
    /// 因此零插件用户的启动开销与接入插件系统之前完全一致（守住 R1 内存与启动红线）。
    /// </para>
    /// </summary>
    public static void Initialize()
    {
        if (_initialized) return;
        _initialized = true;

        try
        {
            _preferences = ConfigManager.CurrentConfig?.Plugins ?? new PluginsPreference();
            _enabled = _preferences.EnablePluginSystem;
            _developerMode = _preferences.DeveloperMode;

            PluginPaths.Configure(_preferences.PortableMode);

            if (!_enabled)
            {
                AppLogger.LogInfo("[plugin] 插件系统已在设置中关闭，跳过初始化。");
                return;
            }

            if (!PluginPaths.EnsureDirectories())
            {
                AppLogger.LogWarn($"[plugin] 插件目录创建失败，插件系统将不可用：{PluginPaths.Root}");
            }

            PluginLogger.CleanOldPluginLogs();
            CleanupPendingDeletions();

            CheckSafeMode();

            // 随包分发的插件：先装进来，再让 SyncFromDisk 按登记表建立实例。
            // 顺序不能反 —— 反了的话这次装上的插件要等下次启动才出现在列表里。
            int bundledInstalled = AutoInstallBundledPlugins();

            int discovered = SyncFromDisk();

            // 认领表必须在 SyncFromDisk 之后建：它读的是实例上的登记条目，
            // 而实例是 SyncFromDisk 按登记表建立的。顺序反了这个表就是空的，
            // 表现是「随包动作包明明装上了，配置里的 Command 却没人认领」。
            RebuildClaimTable();

            AppLogger.LogInfo(
                $"[plugin] 插件系统就绪：宿主区={PluginPaths.Root}，扫描目录={PluginPaths.ScanRoot}" +
                $"（存在={PluginPaths.ScanRootExists}），已登记 {Instances.Count} 个插件" +
                $"（本次扫描新发现 {discovered} 个，随包装入 {bundledInstalled} 个），安全模式={_safeModeActive}");

            if (!_safeModeActive && _preferences.PreloadOnStartup)
            {
                SchedulePreload();
            }

            if (!HeadlessMode)
            {
                ScheduleStartupHealthCheck();
            }
        }
        catch (Exception ex)
        {
            // 插件系统初始化失败绝不能影响主程序启动
            AppLogger.LogError("[plugin] 插件系统初始化失败（已降级为「无插件」运行）", ex);
            _enabled = false;
        }
    }

    /// <summary>宿主退出前的收尾：停用全部插件并落盘健康度。</summary>
    public static void ShutdownAll()
    {
        if (!_initialized || !_enabled) return;

        List<PluginInstance> snapshot;
        lock (Gate)
        {
            snapshot = new List<PluginInstance>(Instances.Values);
        }

        foreach (PluginInstance instance in snapshot)
        {
            try
            {
                if (!instance.IsLoaded) continue;
                instance.Unload();
                instance.FlushHealth();
            }
            catch (Exception ex)
            {
                AppLogger.LogError($"[plugin] 退出时停用 {instance.PluginId} 失败", ex);
            }
        }
    }

    // ------------------------------------------------------------------ 安装

    /// <summary>
    /// 第一步：识别用户手动选择的 <c>.dll</c>。只读元数据，不执行任何插件代码。
    /// 返回结果即是「安装确认卡」要展示的全部内容。
    /// </summary>
    public static PluginScanResult PrepareInstall(string dllPath)
    {
        try
        {
            // 与候选扫描走同一条来源区判定。少了这一步会自相矛盾：同一枚随包 dll
            // 放在来源区能被自动装上，手工选中它却被告知「占用了保留前缀」——
            // 用户拿到的是两条互相打架的结论，而两条都出自同一个宿主。
            return PluginScanner.ScanSelectedDll(dllPath, allowReservedIdPrefix: IsInOfficialSourceDirectory(dllPath));
        }
        catch (Exception ex)
        {
            AppLogger.LogError("[plugin] 识别所选文件时发生未预期异常", ex);
            var result = new PluginScanResult { DllPath = dllPath, Accepted = false };
            return result;
        }
    }

    /// <summary>
    /// 第二步：用户确认后落盘。复制到插件目录并登记为 <b>Disabled</b>。
    /// <para>注意：这里<b>不会加载程序集</b> —— 「安装」与「启用」刻意分成两个动作。</para>
    /// </summary>
    public static PluginInstallResult CommitInstall(PluginScanResult scan, PluginInstallOptions options)
    {
        options ??= new PluginInstallOptions();

        if (scan?.Manifest == null || !scan.Accepted)
        {
            return new PluginInstallResult { Success = false, Error = "识别未通过，无法安装。" };
        }

        if (!options.Acknowledged)
        {
            return new PluginInstallResult { Success = false, Error = "需要先勾选风险确认才能安装。" };
        }

        PluginManifest manifest = scan.Manifest;

        lock (Gate)
        {
            try
            {
                string installPath = manifest.Id;
                string targetDirectory = Path.Combine(PluginPaths.Root, installPath);
                bool alreadyExists = Directory.Exists(targetDirectory) || PluginRegistryStore.FindEntry(manifest.Id) != null;

                if (alreadyExists && !options.OverwriteExisting)
                {
                    return new PluginInstallResult
                    {
                        Success = false,
                        PluginId = manifest.Id,
                        Error = $"已存在同 ID 的插件（{manifest.Id}）。如需替换请勾选「覆盖已有插件」。",
                    };
                }

                // 已被加载的插件不允许直接覆盖文件，否则会得到「文件被占用」这种看不懂的报错
                PluginInstance? existing = Find(manifest.Id);
                if (existing != null && existing.IsLoaded)
                {
                    return new PluginInstallResult
                    {
                        Success = false,
                        PluginId = manifest.Id,
                        Error = "该插件正在运行，请先停用再覆盖安装。",
                    };
                }

                if (!options.DeveloperExternalPath)
                {
                    if (!CopyPayload(scan, targetDirectory, options.OverwriteExisting, out string copyError))
                    {
                        return new PluginInstallResult { Success = false, PluginId = manifest.Id, Error = copyError };
                    }
                }
                else if (!_developerMode)
                {
                    return new PluginInstallResult
                    {
                        Success = false,
                        Error = "「外部路径登记」需要先在插件页开启开发者模式。",
                    };
                }

                var entry = new PluginRegistryEntry
                {
                    Id = manifest.Id,
                    Name = manifest.Name,
                    Version = manifest.Version,
                    Description = manifest.Description,
                    Author = manifest.Author,
                    License = manifest.License,
                    Homepage = manifest.Homepage,
                    InstallPath = installPath,
                    ExternalPath = options.DeveloperExternalPath ? scan.DllPath : null,
                    Enabled = false,
                    Preload = false,
                    EntrySha256 = scan.Sha256,
                    SignerThumbprint = scan.SignerThumbprint,
                    CapabilitiesAck = new List<string>(options.AcknowledgedCapabilities),
                    AckedAt = DateTimeOffset.Now.ToString("yyyy-MM-ddTHH:mm:sszzz"),
                    AckedHostVersion = PluginManifestReader.HostVersion,
                    Source = options.DeveloperExternalPath ? "DeveloperPath" : options.SourceKind,
                    Bundled = options.Bundled,
                    ClaimedTypes = ClaimWire(manifest, options.Bundled),
                    InstalledAt = DateTimeOffset.Now.ToString("yyyy-MM-ddTHH:mm:sszzz"),
                };

                PluginRegistryStore.UpsertEntry(entry);

                var instance = new PluginInstance(manifest.Id, entry, scan);
                lock (Gate)
                {
                    Instances[manifest.Id] = instance;
                }

                string source = scan.ManifestSource == "AssemblyMetadata" ? "（程序集元数据）" : "";
                AppLogger.LogInfo(
                    $"[plugin] 已安装 {manifest.Id} v{manifest.Version}{source}，" +
                    $"SHA256={scan.Sha256Short}，签名={scan.IsSigned}，能力={string.Join(",", entry.CapabilitiesAck)}");

                // 新装的插件可能带来认领（目前只有随包插件能成功认领，这里照常重算一次，
                // 免得将来放宽这条规则时漏掉这个入口）。
                RebuildClaimTable();

                bool enabled = false;
                if (options.EnableAfterInstall)
                {
                    enabled = Enable(manifest.Id, out string enableError);
                    if (!enabled)
                    {
                        AppLogger.LogWarn($"[plugin] 安装后自动启用 {manifest.Id} 失败：{enableError}");
                    }
                }

                return new PluginInstallResult
                {
                    Success = true,
                    PluginId = manifest.Id,
                    Enabled = enabled,
                };
            }
            catch (Exception ex)
            {
                AppLogger.LogError($"[plugin] 安装 {manifest.Id} 失败", ex);
                return new PluginInstallResult { Success = false, PluginId = manifest.Id, Error = ex.Message };
            }
        }
    }

    // ------------------------------------------------------------------ 随包分发的插件

    /// <summary>
    /// 把「随主程序分发」的插件装进来 —— 也就是程序目录下只读扫描目录里的那些 <c>.dll</c>。
    /// <para>
    /// 与用户在插件页手动点「安装」的区别只有一处：这些<b>不等用户点</b>。
    /// 它们随发行包一起来，属于「打开就该有」的东西；要求用户先点十几次安装，
    /// 才让轮盘里出现本来自带的动作，是把打包方的分内事推给了用户。
    /// </para>
    /// <para>
    /// <b>三条规则</b>：
    /// <list type="number">
    /// <item>登记表里<b>没有</b>这个 ID：自动安装并启用。</item>
    /// <item>登记表里<b>已有</b>这个 ID（无论当前是启用还是停用）：一律不动。
    /// 用户停用过的插件绝不能在下次启动时被偷偷启用 —— 那是这一类设计最容易犯、
    /// 也最让人恼火的错（「我明明关过它」）。</item>
    /// <item>登记表里有、但宿主区的文件没了：补回来，并<b>保留原来的启用状态</b>。
    /// 随包插件不可卸载只可停用，文件不见了属于「坏了」，不是「卸载了」。</item>
    /// </list>
    /// </para>
    /// <para>
    /// 必须在 <see cref="SyncFromDisk"/> <b>之前</b>调用：安装往登记表里写条目，
    /// 而实例是 SyncFromDisk 按登记表建立的 —— 顺序反过来，这次装上的插件得等下次启动才露面。
    /// </para>
    /// <para>
    /// 全程不加载任何程序集、不执行任何插件代码（识别只读静态元数据）。
    /// 因此「程序目录下没有插件」的用户，启动开销与接入插件系统之前完全一致（R1 红线）。
    /// </para>
    /// </summary>
    /// <returns>本次新装或补回的插件数量。</returns>
    public static int AutoInstallBundledPlugins()
    {
        if (!PluginPaths.ScanRootExists) return 0;

        int acted = 0;

        try
        {
            // 与 ScanCandidates 同样的约定：扁平，只认顶层 *.dll，不递归子目录。
            string[] files = Directory.GetFiles(PluginPaths.ScanRoot, "*.dll", SearchOption.TopDirectoryOnly);
            Array.Sort(files, StringComparer.OrdinalIgnoreCase);

            foreach (string file in files)
            {
                try
                {
                    if (EnsureBundledPlugin(file)) acted++;
                }
                catch (Exception ex)
                {
                    // 一枚坏文件绝不能拖垮启动 —— 这条路径跑在最早期，抛出去就是整个程序起不来。
                    AppLogger.LogWarn($"[plugin] 随包插件 {Path.GetFileName(file)} 处理失败（已跳过）：{ex.Message}");
                }
            }
        }
        catch (Exception ex)
        {
            AppLogger.LogError("[plugin] 扫描随包插件目录失败", ex);
        }

        return acted;
    }

    /// <summary>处理随包目录里的一枚 <c>.dll</c>；返回是否真的动了登记表或磁盘。</summary>
    private static bool EnsureBundledPlugin(string file)
    {
        PluginScanResult scan = ScanCandidateFile(file);

        if (!scan.Accepted || scan.Manifest == null)
        {
            // 认不出来就装不上。这里必须出声：它不会出现在候选列表里（那需要扫描目录这一侧
            // 的完整分类），用户既装不上也不知道为什么，只能靠日志。
            AppLogger.LogWarn(
                $"[plugin] 随包目录里的 {Path.GetFileName(file)} 无法识别，已跳过：{scan.DescribeFailure()}。" +
                "随包插件装不上是打包问题，需要重新打包。");
            return false;
        }

        PluginManifest manifest = scan.Manifest;
        PluginRegistryEntry? existing = PluginRegistryStore.FindEntry(manifest.Id);

        if (existing != null)
        {
            // 已登记的随包插件：**文件才是权威事实来源**。
            //
            // 两件事都要做，且顺序不能反 —— 先让登记信息与当前这枚 dll 对齐，
            // 再判断宿主区的副本还在不在。
            //
            // 【为什么必须刷新】从前这里只做「文件补回」，把登记表当成了不可变的事实。
            // 但登记表里的认领清单、版本号、能力集合全都是**首次安装那一刻的快照**，
            // 之后随包插件升级、或把某个类型从一个包挪到另一个包（拆包），
            // 快照就永久过时了。过时的认领清单有两种发作方式：
            //   · 新增的认领永远不生效（升级后功能静默缺失）；
            //   · 旧包与新包同时认领同一个类型 —— 触发「多包抢同一类型 → 整对拒绝」，
            //     两个包的所有动作一起失效，而用户唯一能看到的线索是日志里一行 Error。
            // 用户对它的 Enabled / Preload 选择照旧一个字都不改，见 RefreshBundledMetadata。
            bool refreshed = RefreshBundledMetadata(existing, scan);

            // 两个动作互不相干，不能用 || 短路掉任意一个：刷新元数据成功
            // 不代表宿主区的文件还在。
            bool restored = RestoreBundledPayload(existing, scan);

            return refreshed || restored;
        }

        PluginInstallResult result = CommitInstall(scan, new PluginInstallOptions
        {
            Acknowledged = true,        // 随包插件没有「用户确认」这一步可言
            OverwriteExisting = false,
            EnableAfterInstall = false, // 见下方注释：不能走 Enable，它会立刻加载程序集
            SourceKind = "Bundled",
            Bundled = true,
            AcknowledgedCapabilities = manifest.Capabilities is { Count: > 0 } capabilities
                ? new List<string>(capabilities)
                : new List<string>(),
        });

        if (!result.Success)
        {
            AppLogger.LogWarn($"[plugin] 随包插件 {manifest.Id} 自动安装失败：{result.Error}");
            return false;
        }

        // 「已启用」只写登记态，**刻意不调用 Enable** —— Enable 会同步加载程序集，
        // 而 Initialize 阶段的契约是「不加载任何程序集」（R1 内存与启动红线）。
        // 程序集交由首次执行、或界面枚举贡献点时按需拉起；那条路径由自检 [3d] 的
        // 「重启等价态」段守着，不是假设。
        PluginRegistryStore.SetEnabled(manifest.Id, true);
        PluginInstance? installed = Find(manifest.Id);
        if (installed != null)
        {
            installed.Entry.Enabled = true;   // 与 SetEnabled 双写：不假设登记表存的是同一份引用
        }

        AppLogger.LogInfo(
            $"[plugin] 已自动安装随包插件 {manifest.Id} v{manifest.Version}（来源：程序目录 {PluginPaths.ScanDirectoryName}\\）");
        return true;
    }

    /// <summary>
    /// 用来源区那枚 <c>.dll</c> 的<b>当前实际元数据</b>，刷新一条已登记随包插件的登记信息。
    /// <para>
    /// <b>只同步「文件是什么」，绝不同步「用户怎么选」。</b>
    /// 刷新的是认领清单 / 能力集合 / 版本 / 名称 / 描述；
    /// <c>Enabled</c> / <c>Preload</c> / <c>InstallPath</c> / <c>Bundled</c> /
    /// <c>ExternalPath</c> / 安装与确认时间戳一律不碰。
    /// </para>
    /// <para>
    /// <b>为什么认领清单必须以文件为准</b>：它是「用户配置里的 <c>Type</c> 该由谁执行」的
    /// 唯一依据（见 <see cref="RebuildClaimTable"/>），而它的正确取值只取决于
    /// 当前这枚 dll 声明了什么。把安装时的快照当成不可变事实，会让
    /// 「随包插件升级后新增认领」永远不生效；而在拆包场景下更糟 ——
    /// 旧包的快照与新包的新声明会同时认领同一个类型，触发「多包抢同一类型 → 整对拒绝」，
    /// <b>两个包的全部动作一起失效</b>，用户侧毫无线索。
    /// </para>
    /// <para>
    /// <b>不破坏「初始化不加载程序集」</b>：<paramref name="scan"/> 来自
    /// <see cref="ScanCandidateFile"/>，是纯静态 PE 元数据读取，不执行任何插件代码。
    /// </para>
    /// <para>
    /// <b>只对随包条目生效</b>：用户自己装的插件即使撞了同一个 ID，也不该被程序目录里的一枚
    /// 同 ID 文件改写 —— 那等于开了一个「往来源区丢个 dll 就能改别人登记信息」的后门。
    /// </para>
    /// </summary>
    /// <returns>登记信息是否真的发生了变化（未变化时不落盘）。</returns>
    private static bool RefreshBundledMetadata(PluginRegistryEntry existing, PluginScanResult scan)
    {
        if (!existing.Bundled) return false;
        if (!string.IsNullOrWhiteSpace(existing.ExternalPath)) return false;
        if (scan.Manifest == null) return false;

        PluginManifest manifest = scan.Manifest;

        // 认领清单走 ClaimWire：非随包一律返回空表这条规则在这里同样适用，
        // 不另开一条判断路径 —— 两处各写一份，迟早会不一致。
        List<string> claimed = ClaimWire(manifest, bundled: true);

        // 能力集合同步为清单里的当前值。随包插件的能力由发行方决定，
        // 用户对这一项没有「逐项拒绝」的操作（要么用、要么整体停用整个包），
        // 所以这里不需要重新征求确认；插件页显示的就是当前事实。
        List<string> capabilities = manifest.Capabilities is { Count: > 0 }
            ? new List<string>(manifest.Capabilities)
            : new List<string>();

        bool changed = false;

        if (!SameStringList(claimed, existing.ClaimedTypes))
        {
            existing.ClaimedTypes = claimed;
            changed = true;
        }

        if (!SameStringList(capabilities, existing.CapabilitiesAck))
        {
            existing.CapabilitiesAck = capabilities;
            changed = true;
        }

        if (!string.Equals(existing.Version, manifest.Version, StringComparison.Ordinal))
        {
            existing.Version = manifest.Version;
            changed = true;
        }

        if (!string.Equals(existing.Name, manifest.Name, StringComparison.Ordinal))
        {
            existing.Name = manifest.Name;
            changed = true;
        }

        if (!string.Equals(existing.Description, manifest.Description, StringComparison.Ordinal))
        {
            existing.Description = manifest.Description;
            changed = true;
        }

        if (!changed) return false;

        // 只在真有变化时落盘：这个方法每次启动都会跑到，无条件写会让 registry.json 的
        // 修改时间每次都变，备份工具与「配置是否被改过」的判断全部失去意义。
        PluginRegistryStore.UpsertEntry(existing);

        AppLogger.LogInfo(
            $"[plugin] 随包插件 {existing.Id} 的登记信息已按当前文件刷新为 v{existing.Version}，" +
            $"认领 {existing.ClaimedTypes.Count} 项、能力 [{string.Join(",", existing.CapabilitiesAck)}]" +
            $"（启用状态保持为 {existing.Enabled}，未受影响）");

        return true;
    }

    /// <summary>
    /// 两个字符串列表是否等价（忽略大小写与顺序）。
    /// <para>
    /// <b>刻意忽略顺序</b>：这里只想知道「该不该落盘」。清单里换个声明次序不该被当成变化 ——
    /// 那会让每次调整 csproj 里认领串的书写顺序都触发一次不必要的写盘。
    /// </para>
    /// </summary>
    private static bool SameStringList(List<string>? left, List<string>? right)
    {
        int leftCount = left?.Count ?? 0;
        int rightCount = right?.Count ?? 0;
        if (leftCount != rightCount) return false;
        if (leftCount == 0) return true;

        var set = new HashSet<string>(right!, StringComparer.OrdinalIgnoreCase);
        foreach (string item in left!)
        {
            if (!set.Contains(item)) return false;
        }

        return true;
    }

    /// <summary>
    /// 随包插件的文件补回：登记表里已有条目，但宿主区的程序集不在了。
    /// <para>
    /// <b>绝不改动 <c>Enabled</c></b> —— 用户停用过就是停用，补文件不是让它复活的理由。
    /// 只对「本来就标记为随包」的条目生效：用户自己装的插件被他删掉是他的自由，
    /// 宿主没有义务（也不该）把它变回来。
    /// </para>
    /// </summary>
    private static bool RestoreBundledPayload(PluginRegistryEntry existing, PluginScanResult scan)
    {
        if (!existing.Bundled) return false;
        if (!string.IsNullOrWhiteSpace(existing.ExternalPath)) return false;

        string directory = Path.Combine(
            PluginPaths.Root,
            string.IsNullOrWhiteSpace(existing.InstallPath) ? existing.Id : existing.InstallPath);

        bool payloadMissing = !Directory.Exists(directory)
            || Directory.GetFiles(directory, "*.dll", SearchOption.TopDirectoryOnly).Length == 0;

        if (!payloadMissing) return false;

        if (!CopyPayload(scan, directory, overwrite: true, out string error))
        {
            AppLogger.LogWarn($"[plugin] 随包插件 {existing.Id} 的文件补回失败：{error}");
            return false;
        }

        AppLogger.LogInfo(
            $"[plugin] 随包插件 {existing.Id} 的宿主区文件缺失，已从程序目录补回" +
            $"（启用状态保持为 {existing.Enabled}，不因补文件而改变）");
        return true;
    }

    // ------------------------------------------------------------------ 顶层类型认领

    /// <summary>
    /// 一条已生效的认领：用户配置里 <c>Type="Launch"</c> 这个字符串由哪个插件的哪个贡献点负责。
    /// </summary>
    public readonly struct PluginTypeClaimBinding
    {
        /// <summary><see cref="ActionItem.Type"/> 的取值，如 <c>Launch</c>。</summary>
        public string TypeName { get; init; }

        /// <summary>认领它的插件 ID。</summary>
        public string PluginId { get; init; }

        /// <summary>认领它的贡献点全 ID，形如 <c>starpie.builtin.basicactions.launch</c>。</summary>
        public string FullId { get; init; }
    }

    private static readonly object ClaimGate = new();

    private static Dictionary<string, PluginTypeClaimBinding> s_claimedTypes =
        new(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// 重建顶层类型认领表。<b>幂等</b>，登记表或启用状态变动后随时可重跑。
    /// <para>
    /// <b>全程不加载任何程序集</b> —— 认领来自登记表里持久化的那一份字符串（见
    /// <see cref="PluginRegistryEntry.ClaimedTypes"/>），读取就是一次反序列化。
    /// 这是「轮盘首次触发不产生几百毫秒停顿」这条要求的前提：宿主必须在启动最早期、
    /// 一个插件都还没加载时，就知道配置里引用到的类型该由哪个插件负责。
    /// </para>
    /// </summary>
    public static void RebuildClaimTable()
    {
        var table = new Dictionary<string, PluginTypeClaimBinding>(StringComparer.OrdinalIgnoreCase);
        var wanted = new List<WantedClaim>();
        var rejected = new List<string>();

        List<PluginRegistryEntry> entries;
        lock (Gate)
        {
            entries = new List<PluginRegistryEntry>(Instances.Count);
            foreach (PluginInstance instance in Instances.Values) entries.Add(instance.Entry);
        }

        foreach (PluginRegistryEntry entry in entries)
        {
            if (entry.ClaimedTypes == null || entry.ClaimedTypes.Count == 0) continue;

            // 双保险：写入时已经拦过一次（ClaimWire），这里再拦一次是因为 registry.json
            // 是用户能手改的纯文本文件，不能把「只有随包插件能认领」这条规则只押在写入路径上。
            if (!entry.Bundled)
            {
                rejected.Add(
                    $"插件 {entry.Id} 声明了 {entry.ClaimedTypes.Count} 项顶层类型认领，" +
                    "但它不是随包插件 —— 整条拒绝");
                continue;
            }

            foreach (string wire in entry.ClaimedTypes)
            {
                List<PluginTypeClaim> parsed = PluginTypeClaim.ParseAll(wire, out List<string> malformed);
                if (parsed.Count != 1 || malformed.Count > 0)
                {
                    rejected.Add($"插件 {entry.Id} 的认领项 \"{wire}\" 格式非法，已跳过");
                    continue;
                }

                PluginTypeClaim claim = parsed[0];

                // "Plugin" 是社区插件动作的保留类型名，认领它会把两条完全不同的执行路径
                // 挤到同一个 Type 上。
                if (string.Equals(claim.TypeName, PluginApi.ActionTypeName, StringComparison.OrdinalIgnoreCase))
                {
                    rejected.Add($"插件 {entry.Id} 试图认领保留类型名 \"{PluginApi.ActionTypeName}\"，已拒绝");
                    continue;
                }

                // 内建动作优先：还留在 BuiltinActionCatalog 里的类型不许被认领。
                // 否则同一个 Type 会同时挂着两条执行路径，哪条生效取决于调用顺序 ——
                // 这正是整个改造要消除的「双轨制」本身。
                if (BuiltinActionCatalog.TryGet(claim.TypeName, out BuiltinActionRegistration stillBuiltin))
                {
                    rejected.Add(
                        $"插件 {entry.Id} 认领的 \"{claim.TypeName}\" 仍由内建动作 {stillBuiltin.FullId} 提供，" +
                        "认领已拒绝");
                    continue;
                }

                wanted.Add(new WantedClaim(claim.TypeName, entry.Id, $"{entry.Id}.{claim.ContributionId}"));
            }
        }

        // 裁决阶段刻意与收集阶段分开：两个插件抢同一个类型时，先来的赢得毫无道理，
        // 而后来的盖掉先来的更糟 —— 那是静默劫持。整对拒绝 + 一条 Error，
        // 让这个错误在日志里一眼可见（它一定是打包错误，不是用户操作）。
        foreach (IGrouping<string, WantedClaim> group in wanted.GroupBy(w => w.TypeName, StringComparer.OrdinalIgnoreCase))
        {
            List<WantedClaim> items = group.ToList();

            if (items.Count > 1)
            {
                rejected.Add(
                    $"类型 \"{group.Key}\" 被多个插件同时认领" +
                    $"（{string.Join("、", items.Select(i => i.PluginId))}），已全部拒绝 —— " +
                    "这属于打包错误，请只保留一个提供方");
                continue;
            }

            WantedClaim item = items[0];
            table[item.TypeName] = new PluginTypeClaimBinding
            {
                TypeName = item.TypeName,
                PluginId = item.PluginId,
                FullId = item.FullId,
            };
        }

        lock (ClaimGate)
        {
            s_claimedTypes = table;
        }

        foreach (string message in rejected)
        {
            AppLogger.LogError($"[plugin] 顶层类型认领被拒绝：{message}");
        }

        if (table.Count > 0)
        {
            AppLogger.LogInfo(
                $"[plugin] 顶层类型认领表已建立（{table.Count} 项）：" +
                string.Join("、", table.Values.Select(b => $"{b.TypeName}→{b.PluginId}")));
        }
    }

    private readonly struct WantedClaim
    {
        public WantedClaim(string typeName, string pluginId, string fullId)
        {
            TypeName = typeName;
            PluginId = pluginId;
            FullId = fullId;
        }

        public string TypeName { get; }
        public string PluginId { get; }
        public string FullId { get; }
    }

    /// <summary>
    /// 查某个 <c>ActionItem.Type</c> 是否被随包插件认领。
    /// <para>
    /// <b>认领与可用是两件事</b>：插件被停用时这里照样返回 true（登记表里认领还在），
    /// 于是调用方能把「动作所属的包被停用了」这句话说给用户听，
    /// 而不是让这个扇区落进 switch 的无匹配分支、无声无息地什么都不做。
    /// 可用性判断见 <see cref="IsClaimedTypeAvailable"/>。
    /// </para>
    /// </summary>
    public static bool TryResolveClaimedType(string? type, out PluginTypeClaimBinding binding)
    {
        binding = default;
        if (string.IsNullOrWhiteSpace(type)) return false;

        Dictionary<string, PluginTypeClaimBinding> table;
        lock (ClaimGate)
        {
            table = s_claimedTypes;
        }

        return table.TryGetValue(type!.Trim(), out binding);
    }

    /// <summary>当前生效的全部认领（界面与自检用）。</summary>
    public static List<PluginTypeClaimBinding> SnapshotClaims()
    {
        lock (ClaimGate)
        {
            return new List<PluginTypeClaimBinding>(s_claimedTypes.Values);
        }
    }

    /// <summary>某个插件认领的类型名（界面与自检用）；它没认领任何类型时返回空表。</summary>
    public static List<string> ClaimedTypeNamesOf(string pluginId)
    {
        var names = new List<string>();
        if (string.IsNullOrWhiteSpace(pluginId)) return names;

        foreach (PluginTypeClaimBinding binding in SnapshotClaims())
        {
            if (string.Equals(binding.PluginId, pluginId, StringComparison.OrdinalIgnoreCase))
            {
                names.Add(binding.TypeName);
            }
        }

        return names;
    }

    /// <summary>
    /// 这个贡献点是不是「被认领的顶层类型」。
    /// <para>
    /// 用途只有一个：把它从插件动作子下拉里<b>排除掉</b>。被认领的动作已经出现在
    /// 「动作类型」主下拉里（用它们自己的 Type 名），如果同时也列进「🔌 插件」子下拉，
    /// 用户会在两个地方看到同一个动作，而它们的持久化形态完全不同
    /// （一个是 <c>Type="Launch"</c>，另一个是 <c>Type="Plugin"</c> + 引用）——
    /// 换个地方配同一个动作会写出两套不兼容的配置。
    /// </para>
    /// </summary>
    public static bool IsClaimedContribution(string? fullId)
    {
        if (string.IsNullOrWhiteSpace(fullId)) return false;

        foreach (PluginTypeClaimBinding binding in SnapshotClaims())
        {
            if (string.Equals(binding.FullId, fullId, StringComparison.OrdinalIgnoreCase)) return true;
        }

        return false;
    }

    /// <summary>
    /// 认领某个类型的插件此刻<b>能不能干活</b>。不能时返回 false，并给出给用户看的原因。
    /// <para>
    /// 三种不可用：插件没登记、被用户停用、因连续出错被自动隔离。
    /// 返回的 <paramref name="reason"/> 会原样显示给用户 —— 所以它必须说清「该怎么办」，
    /// 而不是「不可用」三个字。
    /// </para>
    /// </summary>
    public static bool IsClaimedTypeAvailable(string? type, out string reason)
    {
        reason = "";

        if (!TryResolveClaimedType(type, out PluginTypeClaimBinding binding))
        {
            return true; // 不是认领类型，不归这里管
        }

        PluginInstance? instance = Find(binding.PluginId);
        if (instance == null)
        {
            reason = $"该动作由随包插件「{binding.PluginId}」提供，但宿主里找不到它的登记记录。" +
                     "请重启 StarPie；若仍不行，说明插件文件已损坏。";
            return false;
        }

        if (!instance.Entry.Enabled)
        {
            reason = $"该动作属于内置动作包「{instance.Entry.Name}」，它当前已被停用。" +
                     "到「设置 → 插件」重新启用即可恢复。";
            return false;
        }

        if (instance.State == PluginRuntimeState.Quarantined)
        {
            reason = $"内置动作包「{instance.Entry.Name}」因连续出错被自动停用，已跳过本次执行。";
            return false;
        }

        return true;
    }

    /// <summary>
    /// 把清单里的认领声明转成登记表用的线格式。
    /// <para>
    /// <b>非随包插件一律返回空表。</b> 认领顶层类型等于接管用户配置里的一整类动作 ——
    /// 用户配好的启动项、网址、文件夹扇区会整体改由这个插件执行。
    /// 这个权力只给随主程序一起分发、与宿主同一个构建产出的插件。
    /// 不在这里拦的话，任何第三方插件都能声明自己认领 <c>"Launch"</c>。
    /// </para>
    /// </summary>
    private static List<string> ClaimWire(PluginManifest manifest, bool bundled)
    {
        if (manifest.ClaimedTypes == null || manifest.ClaimedTypes.Count == 0)
        {
            return new List<string>();
        }

        if (!bundled)
        {
            AppLogger.LogWarn(
                $"[plugin] {manifest.Id} 声明了 {manifest.ClaimedTypes.Count} 项顶层类型认领，" +
                "但它不是随包插件，认领已忽略。");
            return new List<string>();
        }

        return manifest.ClaimedTypes
            .Where(claim => !string.IsNullOrWhiteSpace(claim.TypeName)
                         && !string.IsNullOrWhiteSpace(claim.ContributionId))
            .Select(claim => claim.ToWire())
            .ToList();
    }

    // ------------------------------------------------------------------ 启用 / 停用

    /// <summary>启用插件：加载 → 实例化 → Initialize → 贡献点提交。</summary>
    public static bool Enable(string pluginId, out string error)
    {
        error = "";

        if (!_enabled)
        {
            error = "插件系统已在设置中关闭。";
            return false;
        }

        PluginInstance? instance = Find(pluginId);
        if (instance == null)
        {
            error = $"插件未安装：{pluginId}";
            return false;
        }

        if (instance.IsLoaded)
        {
            return true;
        }

        lock (Gate)
        {
            if (!instance.Load(out string failure))
            {
                error = failure;
                return false;
            }

            instance.Entry.Enabled = true;
            PluginRegistryStore.UpsertEntry(instance.Entry);

            // 启用的插件需要重新注册它的事件订阅（Load 里已经通过 Events 服务登记，无需额外动作）
            foreach (PluginActionRegistration action in instance.OwnedActions)
            {
                // 占位：注册 token 由 PluginContext 内部持有，这里只做日志
                _ = action;
            }
        }

        NotifyPluginSetChanged();
        return true;
    }

    /// <summary>
    /// 把「已启用但尚未加载」的插件全部拉起来。幂等，重复调用是廉价的空转。
    /// <para>
    /// <b>为什么需要它</b>：贡献点目录只在插件加载后才被填充，而插件默认是惰性加载的 ——
    /// 于是「已启用、但本次会话还没被用到过」的插件，它的动作在界面上根本列不出来。
    /// 用户打开设置看到的是「动作下拉是空的」，而他的配置明明还引用着那些动作。
    /// </para>
    /// <para>
    /// 调用时机是「界面即将枚举贡献点」的那一刻，也就是真正需要目录非空的时候。
    /// 这是<b>同步</b>加载，而调用方通常是 UI 线程：插件多、或某个插件初始化慢时会有可感停顿。
    /// 随主程序分发的插件都是小程序集，可以接受；将来接入体积大的第三方插件时，
    /// 这里应改成后台加载 + 加载完成后通知界面重建列表。
    /// </para>
    /// </summary>
    public static void EnsureEnabledPluginsLoaded()
    {
        if (!_initialized || !_enabled || _safeModeActive) return;

        List<PluginInstance> targets = ListInstances()
            .Where(instance => instance.Entry.Enabled && !instance.IsLoaded)
            .ToList();

        foreach (PluginInstance instance in targets)
        {
            try
            {
                if (!instance.Load(out string failure))
                {
                    AppLogger.LogWarn($"[plugin] 按需加载 {instance.PluginId} 失败：{failure}");
                }
            }
            catch (Exception ex)
            {
                AppLogger.LogError($"[plugin] 按需加载 {instance.PluginId} 异常", ex);
            }
        }
    }

    /// <summary>停用插件：撤销贡献点 → 剪断订阅 → Shutdown → 尽力卸载 ALC。</summary>
    public static bool Disable(string pluginId, out string error)
    {
        error = "";
        PluginInstance? instance = Find(pluginId);
        if (instance == null)
        {
            error = $"插件未安装：{pluginId}";
            return false;
        }

        try
        {
            // 卸载结论是**异步**得出的：同步那一瞬间调用栈往往还没展开，此时下结论多半是错的。
            // 所以这里只登记回调，等最终结论出来再决定要不要提示用户「重启」。
            instance.UnloadVerdictFinalized = collected =>
            {
                if (collected) return;

                // 回调可能跑在线程池的延迟判定线程上，日志与托盘气泡都必须回到 UI 线程
                new PluginDispatcherFacade().Post(() =>
                {
                    AppLogger.LogWarn(
                        $"[plugin] {pluginId} 已停用，但插件程序集未能释放（ALC 卸载失败）；" +
                        "请重启 StarPie 以彻底回收其内存。");
                    NotifyUser(
                        "插件已停用，但内存未释放",
                        $"{pluginId} 的程序集仍被引用，重启 StarPie 后才能彻底回收。");
                });
            };

            instance.Unload();
            instance.FlushHealth();

            instance.Entry.Enabled = false;
            PluginRegistryStore.UpsertEntry(instance.Entry);

            NotifyPluginSetChanged();
            return true;
        }
        catch (Exception ex)
        {
            error = ex.Message;
            AppLogger.LogError($"[plugin] 停用 {pluginId} 失败", ex);
            return false;
        }
    }

    /// <summary>卸载插件（停用 + 删除目录 + 移除登记）。随包插件会被拒绝，见下方守卫。</summary>
    public static bool Uninstall(string pluginId, bool removePluginData, out string error) =>
        UninstallCore(pluginId, removePluginData, respectBundledGuard: true, out error);

    /// <summary>
    /// 卸载的实际实现。
    /// <para>
    /// <paramref name="respectBundledGuard"/> 为 <c>false</c> 时无视「随包插件不可卸载」这条规则。
    /// 目前只有自检会用到 —— 它必须把现场收拾干净，而收拾现场恰恰<b>不能</b>走用户路径，
    /// 因为那条路径上的守卫正是被测对象。用户界面永远只走 <see cref="Uninstall"/>。
    /// </para>
    /// </summary>
    internal static bool UninstallCore(string pluginId, bool removePluginData, bool respectBundledGuard, out string error)
    {
        error = "";

        PluginInstance? instance = Find(pluginId);
        if (instance == null)
        {
            error = $"插件未安装：{pluginId}";
            return false;
        }

        // 随包插件不可卸载。它的文件随发行包一起来，卸载只会让它下次启动又出现 ——
        // 给一个「点了没用」的按钮，比诚实地说明原因糟糕得多。
        // 用户真正想要的（别让它挡路）用「停用」就能达成：动作从可选列表消失，配置仍保留。
        if (respectBundledGuard && instance.Entry.Bundled)
        {
            error = $"「{instance.Entry.Name}」随 StarPie 一起分发，不能卸载。"
                  + "如果不想用它，请改用「停用」—— 停用后它的动作会从可选列表里消失，已有配置也不会丢。";
            return false;
        }

        Disable(pluginId, out _);

        try
        {
            // 外部路径登记：程序集留在开发者自己的目录里，宿主只拥有「登记」这一行数据。
            // 卸载必须只摘登记、绝不碰磁盘 —— 那条路径下往往就是开发者的编译输出目录。
            if (instance.IsExternal)
            {
                PluginRegistryStore.RemoveEntry(pluginId);
                lock (Gate)
                {
                    Instances.Remove(pluginId);
                }

                AppLogger.LogInfo(
                    $"[plugin] 已卸载 {pluginId}（外部路径登记，源文件未删除：{instance.Entry.ExternalPath}）");
                NotifyPluginSetChanged();
                return true;
            }

            string directory = instance.ManagedDirectory;
            if (removePluginData && Directory.Exists(directory))
            {
                try
                {
                    Directory.Delete(directory, recursive: true);
                }
                catch (Exception deleteError)
                {
                    // 文件被占用（多为 ALC 未卸载干净）：改名挂起，下次启动时清理
                    string pending = Path.Combine(PluginPaths.Root, ".pending-delete-" + Guid.NewGuid().ToString("N"));
                    try
                    {
                        Directory.Move(directory, pending);
                        AppLogger.LogWarn(
                            $"[plugin] {pluginId} 目录被占用，已挂起删除，将于下次启动时清理：{deleteError.Message}");
                    }
                    catch
                    {
                        error = $"删除插件目录失败（文件被占用）：{deleteError.Message}。请重启 StarPie 后重试。";
                        return false;
                    }
                }
            }

            PluginRegistryStore.RemoveEntry(pluginId);
            lock (Gate)
            {
                Instances.Remove(pluginId);
            }

            AppLogger.LogInfo($"[plugin] 已卸载 {pluginId}（保留数据={!removePluginData}）");
            NotifyPluginSetChanged();
            return true;
        }
        catch (Exception ex)
        {
            error = ex.Message;
            AppLogger.LogError($"[plugin] 卸载 {pluginId} 失败", ex);
            return false;
        }
    }

    // ------------------------------------------------------------------ 参数校验接缝

    private static readonly IReadOnlyDictionary<string, string> EmptyParameters =
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// 插件动作的参数校验结果。
    /// <para>
    /// 刻意把「宿主发现的声明违规」与「插件自己给的说法」分成两份：
    /// 前者能精确对应到某个字段，可以就地标红；后者只是一句话，只能整体展示。
    /// 混成一个字符串会丢掉字段定位能力。
    /// </para>
    /// </summary>
    public sealed class PluginActionValidation
    {
        /// <summary>违反 <see cref="ParameterField"/> 声明约束的字段。</summary>
        public List<PluginParameterIssue> DeclaredIssues { get; init; } = new();

        /// <summary><see cref="IActionContribution.Validate"/> 返回的原因。</summary>
        public string? PluginMessage { get; init; }

        public bool IsValid => DeclaredIssues.Count == 0 && string.IsNullOrEmpty(PluginMessage);

        /// <summary>压成一句给用户看的中文。</summary>
        public string? Describe()
        {
            if (DeclaredIssues.Count > 0) return DeclaredIssues[0].ToString();
            return string.IsNullOrEmpty(PluginMessage) ? null : PluginMessage;
        }
    }

    /// <summary>
    /// <b>插件动作参数校验的唯一入口。</b>
    /// <para>
    /// <see cref="IActionContribution.Validate"/> 的注释写着「宿主会在<b>保存动作</b>与<b>执行前</b>各调用一次」，
    /// 但如果两条路径各写一遍，它们迟早会分叉 —— 用户就会遇到
    /// 「保存时一切正常、触发时却说参数不合法」这种最令人困惑的状态。
    /// 因此两处都走这里，顺序固定为：先宿主底线（声明的约束），再插件自定义。
    /// </para>
    /// <para>
    /// 本方法<b>保证不抛异常</b>。
    /// </para>
    /// </summary>
    public static PluginActionValidation ValidateActionParameters(ActionItem? action)
    {
        if (action?.PluginActionRef == null || !action.PluginActionRef.IsValid)
        {
            return new PluginActionValidation();
        }

        // 社区插件动作的参数来源固定是 ExtensionData。
        // 认领了顶层类型的随包插件不走这里 —— 它的参数由宿主的字段投影器给出，
        // 由 ExecuteClaimedAction 直接调下面那个按全 ID 校验的重载。
        return ValidateActionParameters(action.PluginActionRef.FullId, action.ExtensionData ?? EmptyParameters);
    }

    /// <summary>
    /// 按<b>贡献点全 ID + 一份参数</b>校验。
    /// <para>
    /// 存在这个重载是为了让两条执行路径（社区插件动作 / 认领类型）共用同一段校验，
    /// 而不是各自组装一遍。参数从哪来是调用方的事，怎么判合不合规是这里的事。
    /// </para>
    /// <para>本方法<b>保证不抛异常</b>。</para>
    /// </summary>
    public static PluginActionValidation ValidateActionParameters(
        string fullId,
        IReadOnlyDictionary<string, string> parameters)
    {
        try
        {
            if (!Catalog.TryGetAction(fullId, out PluginActionRegistration registration))
            {
                // 贡献点已不在目录里时不做参数校验。
                // 真正的问题是「这个动作已经不可用」，此时报参数错误会把用户引向完全错误的方向。
                return new PluginActionValidation();
            }

            IReadOnlyDictionary<string, string> actual = parameters ?? EmptyParameters;

            // ① 宿主底线：只认 ParameterField 声明的约束，不依赖插件是否记得自查。
            List<PluginParameterIssue> declaredIssues =
                PluginParameterValidator.Validate(registration.Parameters, actual);

            // ② 插件自定义：处理声明表达不了的规则（例如「起止时间不能相同」）。
            string? pluginMessage = null;
            try
            {
                string? result = registration.Contribution.Validate(actual);
                if (!string.IsNullOrWhiteSpace(result)) pluginMessage = result!.Trim();
            }
            catch (Exception ex)
            {
                AppLogger.LogError($"[plugin] 动作 {registration.FullId} 的参数校验抛出异常", ex);
                pluginMessage = $"插件自身的校验逻辑出错：{ex.GetBaseException().Message}（这是插件的问题，请反馈给插件作者）";
            }

            return new PluginActionValidation
            {
                DeclaredIssues = declaredIssues,
                PluginMessage = pluginMessage,
            };
        }
        catch (Exception ex)
        {
            // 校验器自己坏掉时放行。宁可让插件在执行里自行拒绝，
            // 也不要因为宿主这一环出错就让用户的手势彻底点不动。
            AppLogger.LogError("[plugin] 参数校验流程异常（已放行）", ex);
            return new PluginActionValidation();
        }
    }

    // ------------------------------------------------------------------ 执行接缝

    /// <summary>
    /// <b>主程序唯一的调用入口。</b><see cref="ActionExecutor"/> 在 <c>switch</c> 未命中时调用它。
    /// <para>
    /// 这个方法<b>保证不抛异常</b>，并且绝不把插件异常冒泡给 <see cref="ActionExecutor.Execute"/> ——
    /// 因为那里的 <c>catch</c> 会弹 <c>MessageBox</c>，在无人值守时会把动作线程卡死。
    /// </para>
    /// </summary>
    public static PluginExecuteOutcome ExecutePluginAction(ActionItem action)
    {
        if (action?.PluginActionRef == null || !action.PluginActionRef.IsValid)
        {
            return PluginExecuteOutcome.NotHandled;
        }

        PluginActionRef reference = action.PluginActionRef;

        // 社区插件动作的参数一律来自 ExtensionData：键由插件自己起语义化名字，
        // 宿主一个都不知道，也不该知道。
        Dictionary<string, string> parameters = action.ExtensionData != null
            ? new Dictionary<string, string>(action.ExtensionData, StringComparer.OrdinalIgnoreCase)
            : new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        return ExecuteRegisteredAction(reference.PluginId, reference.FullId, action.Name, parameters);
    }

    /// <summary>
    /// <b>认领了顶层类型的随包插件</b>的执行入口。
    /// <para>
    /// 与 <see cref="ExecutePluginAction"/> 只差一件事：参数从哪来。
    /// 认领类型的动作在用户配置里仍然是 <c>Type="Launch"</c> 这种老形态，参数散在
    /// <see cref="ActionItem"/> 的裸字段上，所以走宿主的字段投影器现读现装
    /// （见 <see cref="ActionParameterProjection"/>）。除此之外，找实例 → 必要时拉起 →
    /// 查目录 → 校验 → 调用，与社区插件动作是<b>同一条</b>链路。
    /// </para>
    /// </summary>
    public static PluginExecuteOutcome ExecuteClaimedAction(ActionItem action, PluginTypeClaimBinding binding)
    {
        if (action == null) return PluginExecuteOutcome.NotHandled;

        string displayName = string.IsNullOrWhiteSpace(action.Name) ? binding.TypeName : action.Name;

        return ExecuteRegisteredAction(
            binding.PluginId,
            binding.FullId,
            displayName,
            ActionParameterProjection.Project(action));
    }

    /// <summary>
    /// 插件贡献点的统一执行链路，两条入口（社区插件 / 认领类型）共用。
    /// <para>
    /// 这个方法<b>保证不抛异常</b>，并且绝不把插件异常冒泡给 <see cref="ActionExecutor.Execute"/> ——
    /// 因为那里的 <c>catch</c> 会弹 <c>MessageBox</c>，在无人值守时会把动作线程卡死。
    /// </para>
    /// </summary>
    private static PluginExecuteOutcome ExecuteRegisteredAction(
        string pluginId,
        string fullId,
        string displayName,
        Dictionary<string, string> parameters)
    {
        try
        {
            // 【顺序至关重要】必须先找到实例、必要时把它拉起来，再去查贡献点目录。
            // 反过来的话会得出一个**错误归因**的结论：目录里查不到 ≠「插件没了」，
            // 也可能是「插件已启用、只是还没被惰性加载」—— 而惰性加载恰恰是本设计
            // 为了守住内存红线（R1）刻意做的。
            // 曾经这里的顺序是反的，于是每次重启后用户配好的插件动作都会拿到一句
            // 「插件可能已被禁用或卸载」，而插件其实好好的 —— 100% 复现的假故障。
            PluginInstance? instance = Find(pluginId);

            if (instance == null)
            {
                return new PluginExecuteOutcome
                {
                    Handled = true,
                    Success = false,
                    Message = $"插件「{pluginId}」未安装。",
                };
            }

            if (!instance.IsLoaded)
            {
                if (!instance.Entry.Enabled)
                {
                    return new PluginExecuteOutcome
                    {
                        Handled = true,
                        Success = false,
                        Message = $"插件「{instance.Entry.Name}」当前未启用，请在「插件」页启用后再试。",
                    };
                }

                // 惰性加载：Enabled 但尚未加载（内存红线的代价就是首次调用要额外等一次加载）
                AppLogger.LogInfo($"[plugin] 首次引用触发惰性加载：{pluginId}");
                if (!Enable(pluginId, out string loadError))
                {
                    return new PluginExecuteOutcome
                    {
                        Handled = true,
                        Success = false,
                        Message = $"插件「{instance.Entry.Name}」加载失败：{loadError}",
                    };
                }
            }

            if (!Catalog.TryGetAction(fullId, out PluginActionRegistration registration))
            {
                // 走到这里的含义是确定的：插件要么本来就在跑、要么刚被拉起来，
                // 而它确实没有提供这个贡献点 —— 只有这种情形才配得上「动作没了」的结论。
                return new PluginExecuteOutcome
                {
                    Handled = true,
                    Success = false,
                    Message = $"插件「{instance.Entry.Name}」没有提供动作「{displayName}」（{fullId}）。" +
                              "插件版本可能已变化，请重新编辑该槽位。",
                };
            }

            if (instance.State == PluginRuntimeState.Quarantined)
            {
                return new PluginExecuteOutcome
                {
                    Handled = true,
                    Success = false,
                    Message = $"插件「{instance.Entry.Name}」因连续出错已被自动禁用，已跳过本次执行。",
                };
            }

            // 参数校验：与设置面板共用同一个入口。
            // 这样「保存时通过」与「执行时通过」永远是同一个判断，
            // 不会出现用户填好参数、存下了、触发却说不合法的情况。
            PluginActionValidation validation = ValidateActionParameters(fullId, parameters);
            if (!validation.IsValid)
            {
                return new PluginExecuteOutcome
                {
                    Handled = true,
                    Success = false,
                    Message = $"{registration.DisplayName} 参数不合法：{validation.Describe()}",
                };
            }

            // 交给插件的是参数的一份拷贝：即使它在 ExecuteAsync 里改写字典，
            // 也污染不到用户正在编辑的配置对象。
            // （上游两处已经各建了一份字典，这里再拷一次是刻意的 —— 它让「拷贝」这件事
            //   只依赖本方法的入参，将来多一个入口也不会漏。）
            return PluginInvoker.Invoke(
                instance,
                registration,
                new Dictionary<string, string>(parameters, StringComparer.OrdinalIgnoreCase));
        }
        catch (Exception ex)
        {
            // 最外层兜底：这里无论如何都不能抛出去
            AppLogger.LogError("[plugin] 执行插件动作时发生未预期异常（已拦截）", ex);
            return new PluginExecuteOutcome
            {
                Handled = true,
                Success = false,
                Message = "插件动作执行时发生内部错误，详情见日志。",
            };
        }
    }

    // ------------------------------------------------------------------ 界面数据

    /// <summary>动作下拉里的插件动作分组（供 <c>SlotViewModel</c> 聚合）。</summary>
    public static List<ActionTypeItem> GetPluginActionItems()
    {
        var items = new List<ActionTypeItem>();
        foreach (PluginActionRegistration action in Catalog.SnapshotActions())
        {
            items.Add(new ActionTypeItem
            {
                Tag = PluginApi.ActionTypeName,
                DisplayText = $"🔌 {action.DisplayName}",
            });
        }
        return items;
    }

    /// <summary>已注册的插件动作（供槽位编辑器按插件分组展示）。</summary>
    public static List<PluginActionRegistration> GetRegisteredActions() => Catalog.SnapshotActions();

    public static bool TryGetAction(string fullId, out PluginActionRegistration registration) =>
        Catalog.TryGetAction(fullId, out registration);

    /// <summary>
    /// 某个引用<b>本来就应该可用吗</b> —— 用来把「已失效」与「只是还没加载」分开。
    /// <para>
    /// 这两个状态在界面上必须表现不同，因为成因差别很大：
    /// <list type="bullet">
    /// <item><b>已失效</b>：插件被停用 / 卸载 / 因连续出错被自动隔离，或插件升级后去掉了那个贡献点。
    /// 用户需要知道「你配的东西不在了」。</item>
    /// <item><b>还没加载</b>：插件已启用，只是惰性加载尚未发生。<b>这是本设计的正常中间态</b> ——
    /// 每次重启后所有插件都处在这个状态。此时判它失效，就是在冤枉用户。</item>
    /// </list>
    /// </para>
    /// <para>
    /// 所以判据必须分层：先问「贡献点的作者还在不在」（登记表），再问「目录里有没有」（贡献点目录）。
    /// 单看目录是不行的 —— 目录在插件未加载时本来就是空的。
    /// </para>
    /// </summary>
    public static bool IsContributionExpected(string pluginId, string fullId)
    {
        if (string.IsNullOrWhiteSpace(pluginId) || string.IsNullOrWhiteSpace(fullId)) return false;

        PluginInstance? instance = Find(pluginId);
        if (instance == null) return false;                                 // 插件不在了
        if (!instance.Entry.Enabled) return false;                          // 被用户停用
        if (instance.State == PluginRuntimeState.Quarantined) return false; // 连续出错被自动隔离
        if (!instance.IsLoaded) return true;                                // 已启用但未加载：不能断言失效

        return Catalog.TryGetAction(fullId, out _);                         // 正在跑：以目录为准
    }

    /// <summary>预览文案。失败时返回空串，绝不抛异常。</summary>
    public static string PreviewAction(string fullId, IReadOnlyDictionary<string, string> parameters)
    {
        try
        {
            return Catalog.TryGetAction(fullId, out PluginActionRegistration registration)
                ? registration.Contribution.Preview(parameters) ?? ""
                : "";
        }
        catch
        {
            return "";
        }
    }

    /// <summary>
    /// 构造一条指向插件动作的 <see cref="ActionItem"/>。
    /// <para>
    /// 刻意做成静态工厂而不是让调用方自己拼字段：插件动作的持久化形态（<c>Type="Plugin"</c> +
    /// <c>PluginActionRef</c> + <c>ExtensionData</c>）是契约的一部分，散落在各处手拼迟早会写出不一致的配置。
    /// </para>
    /// </summary>
    public static ActionItem? CreateActionItem(string fullId, Dictionary<string, string>? parameters = null)
    {
        if (!Catalog.TryGetAction(fullId, out PluginActionRegistration registration))
        {
            return null;
        }

        var action = new ActionItem
        {
            Type = PluginApi.ActionTypeName,
            Name = registration.DisplayName,
            IconKey = registration.IconKey ?? "",
            PluginActionRef = new PluginActionRef
            {
                PluginId = registration.PluginId,
                ContributionId = registration.ShortId,
            },
        };

        // 用参数默认值填充，让「新建动作」后立即就是可用的
        var merged = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (ParameterField field in registration.Parameters)
        {
            if (!string.IsNullOrWhiteSpace(field.DefaultValue))
            {
                merged[field.Key] = field.DefaultValue!;
            }
        }
        if (parameters != null)
        {
            foreach (KeyValuePair<string, string> pair in parameters)
            {
                merged[pair.Key] = pair.Value;
            }
        }

        action.ExtensionData = merged.Count > 0 ? merged : null;
        return action;
    }

    /// <summary>
    /// 向用户提示一条与插件有关的信息。
    /// <para>优先走托盘气泡；没有可用托盘时降级为写日志 —— <b>绝不用 MessageBox</b>，
    /// 因为它会阻塞动作线程。</para>
    /// </summary>
    public static void NotifyUser(string title, string message)
    {
        if (string.IsNullOrWhiteSpace(message)) return;

        try
        {
            Action<string, string>? sink = PluginNotificationHub.Sink;
            if (sink != null)
            {
                sink(title ?? "StarPie 插件", message);
                return;
            }
        }
        catch
        {
        }

        AppLogger.LogInfo($"[plugin] 提示：{title} - {message}");
    }

    /// <summary>插件列表快照（供插件管理页）。</summary>
    public static List<PluginInstance> ListInstances()    {
        lock (Gate)
        {
            return Instances.Values
                .OrderBy(i => i.Entry.Name, StringComparer.CurrentCultureIgnoreCase)
                .ToList();
        }
    }

    public static PluginInstance? Find(string pluginId)
    {
        if (string.IsNullOrWhiteSpace(pluginId)) return null;
        lock (Gate)
        {
            return Instances.TryGetValue(pluginId, out PluginInstance? instance) ? instance : null;
        }
    }

    /// <summary>把全部插件的健康度落盘（宿主退出或界面刷新时调用）。</summary>
    public static void FlushHealth()
    {
        foreach (PluginInstance instance in ListInstances())
        {
            try
            {
                instance.FlushHealth();
            }
            catch
            {
            }
        }
    }

    /// <summary>开发者模式开关。开启时允许「只登记外部路径不复制文件」。</summary>
    public static void SetDeveloperMode(bool enabled)
    {
        _developerMode = enabled;
        _preferences.DeveloperMode = enabled;
    }

    public static void SetEnabled(bool enabled)
    {
        _enabled = enabled;
        _preferences.EnablePluginSystem = enabled;

        if (!enabled)
        {
            ShutdownAll();
        }
    }

    // ------------------------------------------------------------------ 轮盘事件广播

    private static readonly Dictionary<string, List<Action<ActionContext>>> WheelOpeningHandlers = new(StringComparer.OrdinalIgnoreCase);
    private static readonly Dictionary<string, List<Action>> WheelClosedHandlers = new(StringComparer.OrdinalIgnoreCase);

    public static IDisposable RegisterWheelOpening(string pluginId, Action<ActionContext> handler)
    {
        lock (Gate)
        {
            if (!WheelOpeningHandlers.TryGetValue(pluginId, out List<Action<ActionContext>>? list))
            {
                list = new List<Action<ActionContext>>();
                WheelOpeningHandlers[pluginId] = list;
            }
            list.Add(handler);
        }

        return new RegistrationToken(() =>
        {
            lock (Gate)
            {
                if (WheelOpeningHandlers.TryGetValue(pluginId, out List<Action<ActionContext>>? list))
                {
                    list.Remove(handler);
                }
            }
        });
    }

    public static IDisposable RegisterWheelClosed(string pluginId, Action handler)
    {
        lock (Gate)
        {
            if (!WheelClosedHandlers.TryGetValue(pluginId, out List<Action>? list))
            {
                list = new List<Action>();
                WheelClosedHandlers[pluginId] = list;
            }
            list.Add(handler);
        }

        return new RegistrationToken(() =>
        {
            lock (Gate)
            {
                if (WheelClosedHandlers.TryGetValue(pluginId, out List<Action>? list))
                {
                    list.Remove(handler);
                }
            }
        });
    }

    /// <summary>
    /// 广播「轮盘即将呈现」。
    /// <para>
    /// <b>必须由 UI 线程调用</b>（调用点应使用 <c>Dispatcher.BeginInvoke</c> 投递），
    /// 因为轮盘的呈现路径直接挂在鼠标钩子之后，插件代码绝不允许出现在那条路径上（红线 R2）。
    /// </para>
    /// </summary>
    public static void RaiseWheelOpening(ActionContext context)
    {
        if (!_enabled) return;

        List<Action<ActionContext>> handlers = new();
        lock (Gate)
        {
            foreach (List<Action<ActionContext>> list in WheelOpeningHandlers.Values)
            {
                handlers.AddRange(list);
            }
        }

        foreach (Action<ActionContext> handler in handlers)
        {
            try
            {
                handler(context);
            }
            catch (Exception ex)
            {
                AppLogger.LogError("[plugin] OnWheelOpening 回调异常（已拦截）", ex);
            }
        }
    }

    public static void RaiseWheelClosed()
    {
        if (!_enabled) return;

        List<Action> handlers = new();
        lock (Gate)
        {
            foreach (List<Action> list in WheelClosedHandlers.Values)
            {
                handlers.AddRange(list);
            }
        }

        foreach (Action handler in handlers)
        {
            try
            {
                handler();
            }
            catch (Exception ex)
            {
                AppLogger.LogError("[plugin] OnWheelClosed 回调异常（已拦截）", ex);
            }
        }
    }

    // ------------------------------------------------------------------ 磁盘同步

    /// <summary>
    /// 把磁盘上的插件目录同步到内存登记表。返回本次「新发现」的数量。
    /// <para>只读清单文件，不加载程序集；新发现的插件一律登记为 Disabled。</para>
    /// </summary>
    public static int SyncFromDisk()
    {
        int discovered = 0;

        try
        {
            var knownIds = new HashSet<string>(
                PluginRegistryStore.SnapshotEntries().Select(e => e.Id),
                StringComparer.OrdinalIgnoreCase);

            // ① 已登记（含开发者外部路径）
            foreach (PluginRegistryEntry entry in PluginRegistryStore.SnapshotEntries())
            {
                PluginScanResult scan = ScanEntry(entry);

                // 关键：已在内存里的实例必须「就地更新」，绝不能 new 一个替换掉。
                // 旧实例仍然持有可回收加载上下文与已注册的贡献点，把它从字典里摘掉
                // 就会造出「孤儿」—— 动作还挂在轮盘上，宿主却再也找不到实例来卸载它，
                // 于是程序集、文件锁和内存全部无法释放。
                // 用户第二次进入插件管理页就会踩到这个坑（那里会先与磁盘对账）。
                PluginInstance? existing;
                lock (Gate)
                {
                    Instances.TryGetValue(entry.Id, out existing);
                }

                if (existing != null)
                {
                    existing.Entry = entry;
                    existing.Scan = scan;

                    // 只允许「尚未加载」的实例因识别失败而降级；
                    // 否则会把一个正在正常运行的插件误标成不兼容。
                    if (!scan.Accepted && existing.State != PluginRuntimeState.Active)
                    {
                        existing.MarkIncompatible(scan.DescribeFailure());
                    }

                    continue;
                }

                PluginInstance instance = new(entry.Id, entry, scan);

                if (!scan.Accepted)
                {
                    instance.MarkIncompatible(scan.DescribeFailure());
                }

                lock (Gate)
                {
                    Instances[entry.Id] = instance;
                }
            }

            // ② 手工放进「可写宿主区」目录（plugin-data\）但尚未登记的。
            // 注意这里只认「子目录 + plugin.json」——它对应的是「用户已经手工安装好了」，
            // 与只读来源区 <程序目录>\plugin\ 里那些待安装候选完全不是一回事（见 ScanCandidates）。
            foreach (string directory in Directory.GetDirectories(PluginPaths.Root))
            {
                string name = Path.GetFileName(directory);
                if (name.StartsWith(".pending-delete", StringComparison.OrdinalIgnoreCase)) continue;
                if (!File.Exists(PluginPaths.GetManifestPath(directory))) continue;

                PluginScanResult scan = PluginScanner.ScanInstalledPlugin(directory);
                if (!scan.Accepted || scan.Manifest == null) continue;
                if (!knownIds.Add(scan.Manifest.Id)) continue;

                var entry = new PluginRegistryEntry
                {
                    Id = scan.Manifest.Id,
                    Name = scan.Manifest.Name,
                    Version = scan.Manifest.Version,
                    Description = scan.Manifest.Description,
                    Author = scan.Manifest.Author,
                    License = scan.Manifest.License,
                    Homepage = scan.Manifest.Homepage,
                    InstallPath = name,
                    Enabled = false,
                    EntrySha256 = scan.Sha256,
                    Source = "Discovered",
                    InstalledAt = DateTimeOffset.Now.ToString("yyyy-MM-ddTHH:mm:sszzz"),
                };
                PluginRegistryStore.UpsertEntry(entry);

                var instance = new PluginInstance(entry.Id, entry, scan);
                lock (Gate)
                {
                    Instances[entry.Id] = instance;
                }
                discovered++;
            }
        }
        catch (Exception ex)
        {
            AppLogger.LogError("[plugin] 同步插件目录失败", ex);
        }

        return discovered;
    }

    // ------------------------------------------------------------------ 只读扫描目录（候选）

    private static IReadOnlyList<PluginCandidate> _candidates = Array.Empty<PluginCandidate>();

    /// <summary>最近一次扫描出的候选插件清单。UI 直接读这个，不要自己去遍历目录。</summary>
    public static IReadOnlyList<PluginCandidate> Candidates
    {
        get { lock (Gate) { return _candidates; } }
    }

    /// <summary>
    /// 扫描<b>只读</b>目录 <c>程序目录\plugin</c>，得出「待安装候选」清单。
    /// <para>
    /// 与 <see cref="SyncFromDisk"/> 的<b>根本区别</b>：这里发现的东西<b>不会</b>登记、
    /// <b>不会</b>加载、也<b>不会</b>出现在插件列表里。它只说「这里躺着这些 .dll，
    /// 你可以装」，装不装由用户点按钮决定。
    /// </para>
    /// <para>
    /// 反过来，<see cref="SyncFromDisk"/> 第 ② 段会自动登记的是<b>可写宿主区</b>里
    /// 「子目录 + plugin.json」的手工投放 —— 那已经是安装产物了，与这里的候选是两回事。
    /// </para>
    /// <para>
    /// 目录不存在时直接得到空清单，<b>绝不创建它</b>：程序目录可能是只读的，
    /// 「本机没有随包附带的插件」本来就是完全正常的状态。
    /// </para>
    /// </summary>
    /// <returns>本次识别出的候选数量（含被拒绝、重复的）。</returns>
    public static int ScanCandidates()
    {
        var list = new List<PluginCandidate>();

        try
        {
            if (!PluginPaths.ScanRootExists)
            {
                lock (Gate) { _candidates = list; }
                return 0;
            }

            // 目录名固定从 PluginPaths 取；这里不递归子目录 ——
            // 扫描目录的约定就是「扁平，只放 .dll」，子目录一律不认。
            string[] files = Directory.GetFiles(PluginPaths.ScanRoot, "*.dll", SearchOption.TopDirectoryOnly);
            Array.Sort(files, StringComparer.OrdinalIgnoreCase);

            var scans = new List<PluginScanResult>(files.Length);
            foreach (string file in files)
            {
                scans.Add(ScanCandidateFile(file));
            }

            // 同 ID 计数按扫描目录内部去重统计（大小写不敏感）：这是识别「两枚 dll 撞 ID」的依据。
            var idCounts = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            foreach (PluginScanResult scan in scans)
            {
                string? id = scan.Manifest?.Id;
                if (string.IsNullOrWhiteSpace(id)) continue;
                idCounts[id!] = idCounts.TryGetValue(id!, out int n) ? n + 1 : 1;
            }

            var installed = new Dictionary<string, PluginRegistryEntry>(StringComparer.OrdinalIgnoreCase);
            foreach (PluginRegistryEntry entry in PluginRegistryStore.SnapshotEntries())
            {
                installed[entry.Id] = entry;
            }

            foreach (PluginScanResult scan in scans)
            {
                (PluginCandidateState state, string note) = ClassifyCandidate(scan, idCounts, installed);
                list.Add(new PluginCandidate
                {
                    DllPath = scan.DllPath,
                    FileName = Path.GetFileName(scan.DllPath),
                    Scan = scan,
                    State = state,
                    Note = note,
                });
            }
        }
        catch (Exception ex)
        {
            AppLogger.LogError("[plugin] 扫描只读插件目录失败", ex);
        }

        lock (Gate) { _candidates = list; }
        return list.Count;
    }

    /// <summary>识别扫描目录里的一枚 dll。异常一律转成「识别未通过」而不是上抛 —— 一枚坏文件不该让整页空掉。</summary>
    private static PluginScanResult ScanCandidateFile(string file)
    {
        try
        {
            return PluginScanner.ScanSelectedDll(file, allowReservedIdPrefix: IsInOfficialSourceDirectory(file));
        }
        catch (Exception ex)
        {
            return new PluginScanResult
            {
                DllPath = file,
                SourceDirectory = Path.GetDirectoryName(file) ?? "",
                Accepted = false,
                Failure = PluginScanFailure.NotDotNetAssembly,
                ErrorDetail = ex.Message,
            };
        }
    }

    /// <summary>
    /// 这枚文件是不是躺在<b>随程序分发的只读来源区</b>里（<c>&lt;程序目录&gt;\plugin\</c>）。
    /// <para>
    /// 用途只有一个：让来自来源区的清单放行保留 ID 前缀（官方包用 <c>starpie.*</c> 命名，
    /// 这正是保留命名空间的用途）。用户自己挑的 dll、以及开发者登记的外部路径一律为 false，
    /// 于是社区插件照旧拿不到官方命名空间。
    /// </para>
    /// <para>
    /// 判定同时接受「当前生效的来源区」（<see cref="PluginPaths.ScanRoot"/>）与
    /// 「主程序目录下的 <c>plugin\</c>」—— 前者是为了让自检的沙箱能真实模拟来源区，
    /// 后者是规则的本义。两者在正常运行时是同一个目录。
    /// </para>
    /// </summary>
    private static bool IsInOfficialSourceDirectory(string file)
    {
        try
        {
            string? directory = Path.GetDirectoryName(Path.GetFullPath(file));
            if (string.IsNullOrEmpty(directory)) return false;

            directory = Path.TrimEndingDirectorySeparator(directory);

            // 两个候选，任一命中即可：
            //
            //   ① 当前生效的来源区（PluginPaths.ScanRoot）。自检会把根目录钉到临时沙箱，
            //      那时沙箱里的 plugin\ 正是「我们正在模拟的那个来源区」，必须算 ——
            //      否则随包安装这条路径在自检里根本跑不起来。
            //   ② 主程序目录下的 plugin\。这是规则的本义。用 BaseDirectory 而不是 ScanRoot，
            //      是因为即便根目录被重定向，真实程序目录里的那一份仍然是随包分发的那一份；
            //      用户跑 --plugin-selftest 时指的通常也正是它。
            if (PluginPaths.ScanRootExists
                && string.Equals(
                    directory,
                    Path.TrimEndingDirectorySeparator(Path.GetFullPath(PluginPaths.ScanRoot)),
                    StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            string programSourceRoot = Path.TrimEndingDirectorySeparator(
                Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, PluginPaths.ScanDirectoryName)));

            return string.Equals(directory, programSourceRoot, StringComparison.OrdinalIgnoreCase);
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// 判定一枚候选与「已装的那份」是什么关系。
    /// <para>
    /// 顺序不能换：① 先看识别过没过（没过的连 ID 都没有，谈不上比较）；
    /// ② 再看扫描目录内部有没有撞 ID（自身有歧义就不该继续比）；
    /// ③ 再看已装的那份是不是外部路径登记（那种情况下根本不该复制文件进来）；
    /// ④ 最后才比版本与哈希。
    /// </para>
    /// </summary>
    private static (PluginCandidateState State, string Note) ClassifyCandidate(
        PluginScanResult scan,
        IReadOnlyDictionary<string, int> idCounts,
        IReadOnlyDictionary<string, PluginRegistryEntry> installed)
    {
        if (!scan.Accepted || scan.Manifest == null)
        {
            return (PluginCandidateState.Rejected, $"无法安装：{scan.DescribeFailure()}");
        }

        string id = scan.Manifest.Id;

        if (idCounts.TryGetValue(id, out int sameId) && sameId > 1)
        {
            return (PluginCandidateState.Duplicate,
                $"扫描目录里有 {sameId} 枚 .dll 声明了同一个 ID（{id}），无法判断该装哪一枚。请只保留需要的那一个文件。");
        }

        if (!installed.TryGetValue(id, out PluginRegistryEntry? entry))
        {
            return (PluginCandidateState.Installable, "尚未安装，可直接安装。");
        }

        if (!string.IsNullOrWhiteSpace(entry.ExternalPath))
        {
            return (PluginCandidateState.ExternalRegistered,
                $"同一个 ID 已被开发者模式的外部路径登记占用：{entry.ExternalPath}。" +
                "如需改为安装副本，请先在列表里卸载那条登记。");
        }

        string installedVersion = entry.Version ?? "";
        string candidateVersion = scan.Manifest.Version ?? "";

        bool sameHash = !string.IsNullOrWhiteSpace(entry.EntrySha256)
            && string.Equals(entry.EntrySha256, scan.Sha256, StringComparison.OrdinalIgnoreCase);

        if (SimpleVersion.TryParse(installedVersion, out SimpleVersion oldVersion)
            && SimpleVersion.TryParse(candidateVersion, out SimpleVersion newVersion))
        {
            int compare = newVersion.CompareTo(oldVersion);

            if (compare == 0)
            {
                return sameHash
                    ? (PluginCandidateState.Installed, $"已装同一个版本（v{installedVersion}），无需重复安装。")
                    : (PluginCandidateState.Replaced,
                        $"已装的 v{installedVersion} 与这枚文件版本号相同但内容不同（哈希不一致）。" +
                        "覆盖安装会用它替换现有文件。");
            }

            if (compare > 0)
            {
                return (PluginCandidateState.Update, $"已装 v{installedVersion}，这枚是更新的 v{candidateVersion}。");
            }

            return (PluginCandidateState.Downgrade,
                $"已装 v{installedVersion}，这枚是更旧的 v{candidateVersion}。一般不建议降级。");
        }

        return (PluginCandidateState.VersionUnknown,
            $"已装版本「{installedVersion}」与候选版本「{candidateVersion}」至少有一侧解析不了，无法比较新旧。" +
            (sameHash ? "内容与已装的一致。" : "内容与已装的不同。"));
    }

    /// <summary>
    /// 把一枚候选装进可写宿主区并启用。这是候选卡片上那个按钮的全部逻辑。
    /// <para>
    /// 安装动作本身仍复用 <see cref="CommitInstall"/>，这里只负责三件事：
    /// ① 拦住不允许安装的状态；② 替用户处理「正在运行所以文件被锁」；
    /// ③ 装完立刻重扫候选，让列表刷新成「已装同版本」。
    /// </para>
    /// </summary>
    public static bool InstallCandidate(PluginCandidate candidate, out string error)
    {
        error = "";

        if (candidate == null)
        {
            error = "候选为空。";
            return false;
        }

        if (!candidate.CanInstall)
        {
            error = $"当前状态不允许安装：{candidate.StateText}。{candidate.Note}";
            return false;
        }

        PluginScanResult scan = candidate.Scan;
        if (scan.Manifest == null)
        {
            error = "识别结果里没有清单，无法安装。";
            return false;
        }

        string pluginId = scan.Manifest.Id;

        // 覆盖安装必须先让文件解锁。插件是惰性加载的（Preload 默认 false），
        // 但一旦用户已经用过它的动作，程序集就被加载、文件就被占用，
        // 此时直接覆盖只会得到一句「文件被占用」——对用户就是「更新失败，原因不明」。
        // 这里主动停用再装：对用户始终只是「一次点击」。
        PluginInstance? existing = Find(pluginId);
        if (existing is { IsLoaded: true })
        {
            AppLogger.LogInfo($"[plugin] 覆盖安装 {pluginId} 前先行停用以解除文件占用");
            Disable(pluginId, out _);
        }

        var options = new PluginInstallOptions
        {
            // 能走到这个按钮前，用户已经在候选卡片上看过说明并点了确认。
            Acknowledged = true,
            OverwriteExisting = true,
            EnableAfterInstall = true,
            SourceKind = "ScanDirectory",
            AcknowledgedCapabilities = scan.Manifest.Capabilities is { Count: > 0 } capabilities
                ? new List<string>(capabilities)
                : new List<string>(),
        };

        PluginInstallResult result = CommitInstall(scan, options);
        if (!result.Success)
        {
            error = result.Error;
            ScanCandidates();
            return false;
        }

        ScanCandidates();
        return true;
    }

    /// <summary>重新扫描单个插件（用户点了「刷新」）。</summary>
    public static PluginScanResult Rescan(string pluginId)
    {
        PluginInstance? instance = Find(pluginId);
        if (instance == null)
        {
            return new PluginScanResult { Accepted = false, Failure = PluginScanFailure.DllNotFound, ErrorDetail = "插件未登记。" };
        }

        PluginScanResult scan = ScanEntry(instance.Entry);
        instance.Scan = scan;

        if (!scan.Accepted && !instance.IsLoaded)
        {
            instance.MarkIncompatible(scan.DescribeFailure());
        }
        else if (scan.Accepted)
        {
            instance.ClearError();
        }

        return scan;
    }

    private static PluginScanResult ScanEntry(PluginRegistryEntry entry)
    {
        try
        {
            if (!string.IsNullOrWhiteSpace(entry.ExternalPath))
            {
                return PluginScanner.ScanSelectedDll(entry.ExternalPath!);
            }

            string directory = Path.Combine(
                PluginPaths.Root,
                string.IsNullOrWhiteSpace(entry.InstallPath) ? entry.Id : entry.InstallPath);

            // 已登记的插件：保留前缀在它进入系统那一刻就查过了，这里不再复查。
            // 「扫描时放行、装载时拒绝」会让随包插件装得上却永远起不来，
            // 而错误信息指着 ID 说事，与真实原因毫无关系。
            return PluginScanner.ScanInstalledPlugin(directory, allowReservedIdPrefix: true);
        }
        catch (Exception ex)
        {
            return new PluginScanResult
            {
                Accepted = false,
                Failure = PluginScanFailure.ManifestInvalid,
                ErrorDetail = ex.Message,
                SourceDirectory = entry.Id,
            };
        }
    }

    // ------------------------------------------------------------------ 安全模式

    /// <summary>
    /// 启动时判定是否需要进入安全模式。
    /// <para>
    /// 判据：上一次启动时记录「已加载插件集」，并有连续 2 次在启动后 30 秒内异常退出。
    /// 触发后自动禁用那批插件，让用户至少能进得去设置页。
    /// </para>
    /// </summary>
    private static void CheckSafeMode()
    {
        try
        {
            PluginHealthFile health = PluginRegistryStore.Health;

            if (!string.IsNullOrWhiteSpace(health.SafeModeUntil)
                && DateTimeOffset.TryParse(health.SafeModeUntil, out DateTimeOffset until)
                && until > DateTimeOffset.Now)
            {
                _safeModeActive = true;
                AppLogger.LogWarn(
                    $"[plugin] 已进入安全模式（至 {until:yyyy-MM-dd HH:mm}），本次启动不加载任何插件。" +
                    "如果确认插件没有问题，可在插件页「重置插件系统」。");
                return;
            }

            if (health.ConsecutiveStartupFailures >= 2 && health.LastStartupPluginSet.Count > 0)
            {
                _safeModeActive = true;

                foreach (string pluginId in health.LastStartupPluginSet)
                {
                    PluginRegistryStore.SetEnabled(pluginId, false);
                    AppLogger.LogWarn($"[plugin] 安全模式：已自动禁用疑似导致启动失败的插件 {pluginId}");
                }

                PluginRegistryStore.MutateHealthFile(h =>
                {
                    h.SafeModeUntil = DateTimeOffset.Now.AddDays(1).ToString("yyyy-MM-ddTHH:mm:sszzz");
                    h.LastStartupPluginSet.Clear();
                    h.ConsecutiveStartupFailures = 0;
                });

                PluginNotificationHub.Sink?.Invoke(
                    "StarPie 已进入插件安全模式",
                    "检测到连续两次启动异常，已临时禁用上次加载的插件。请到「插件」页检查。");
            }

            // 标记一次「启动中」，30 秒后若仍存活则清零（见 ScheduleStartupHealthCheck）
            //
            // 无界面模式（自检 / 路径诊断）不参与记账：它们跑完立刻退出，永远活不到
            // 30 秒健康检查那一刻，于是计数只增不减。而安全模式的判据是
            // 「连续两次启动异常 **且** 上次启动加载过插件」—— 用户装好插件正常用着，
            // 连着跑两次自检就可能被判定为「启动异常」，下次打开 GUI 时插件被自动禁用。
            // 这种误伤比少记一次数严重得多。
            if (!HeadlessMode)
            {
                PluginRegistryStore.MutateHealthFile(h => h.ConsecutiveStartupFailures++);
            }
        }
        catch (Exception ex)
        {
            AppLogger.LogError("[plugin] 安全模式判定失败", ex);
        }
    }

    /// <summary>启动 30 秒后确认存活：清零失败计数并记录本次加载的插件集。</summary>
    private static void ScheduleStartupHealthCheck()
    {
        try
        {
            var timer = new System.Threading.Timer(_ =>
            {
                try
                {
                    List<string> loaded = ListInstances()
                        .Where(i => i.IsLoaded)
                        .Select(i => i.PluginId)
                        .ToList();

                    PluginRegistryStore.MutateHealthFile(h =>
                    {
                        h.ConsecutiveStartupFailures = 0;
                        h.LastStartupPluginSet = loaded;
                    });

                    FlushHealth();
                    AppLogger.LogInfo(
                        $"[plugin] 启动健康检查通过：本会话加载 {loaded.Count} 个插件" +
                        (loaded.Count > 0 ? $"（{string.Join(", ", loaded)}）" : ""));
                }
                catch
                {
                }
            }, null, TimeSpan.FromSeconds(30), Timeout.InfiniteTimeSpan);

            _ = timer;
        }
        catch
        {
        }
    }

    /// <summary>启动后台预加载（仅 Preload=true 的已启用插件），不阻塞首帧。</summary>
    private static void SchedulePreload()
    {
        try
        {
            Task.Run(async () =>
            {
                // 刻意延迟：让主程序先把首帧、托盘、钩子都装好
                await Task.Delay(TimeSpan.FromSeconds(3)).ConfigureAwait(false);

                foreach (PluginInstance instance in ListInstances())
                {
                    if (!instance.Entry.Enabled || !instance.Entry.Preload) continue;
                    if (instance.IsLoaded) continue;

                    try
                    {
                        if (!instance.Load(out string failure))
                        {
                            AppLogger.LogWarn($"[plugin] 预加载 {instance.PluginId} 失败：{failure}");
                        }
                    }
                    catch (Exception ex)
                    {
                        AppLogger.LogError($"[plugin] 预加载 {instance.PluginId} 异常", ex);
                    }

                    await Task.Delay(200).ConfigureAwait(false);
                }
            });
        }
        catch (Exception ex)
        {
            AppLogger.LogError("[plugin] 调度预加载失败", ex);
        }
    }

    /// <summary>
    /// 登记表或启用状态变了。所有变更路径（安装 / 启用 / 停用 / 卸载）都汇聚到这里，
    /// 于是「变更之后要重算什么」只有这一处需要维护。
    /// </summary>
    private static void NotifyPluginSetChanged()
    {
        // 认领表必须跟着登记表走：卸载一个随包动作包之后，它认领的那些 Type
        // 若还留在表里，宿主会继续按「已停用」去解释它们 —— 而插件其实已经不存在了。
        RebuildClaimTable();

        try
        {
            ConfigManager.MarkConfigurationChanged();
        }
        catch
        {
        }
    }

    // ------------------------------------------------------------------ 工具

    /// <summary>清理上次启动挂起的删除目录。</summary>
    private static void CleanupPendingDeletions()
    {
        try
        {
            foreach (string directory in Directory.GetDirectories(PluginPaths.Root, ".pending-delete-*"))
            {
                try
                {
                    Directory.Delete(directory, recursive: true);
                    AppLogger.LogInfo($"[plugin] 已清理挂起删除的目录：{Path.GetFileName(directory)}");
                }
                catch
                {
                }
            }
        }
        catch
        {
        }
    }

    /// <summary>
    /// 按识别结果决定「复制什么」。
    /// <para>
    /// 规则只有一条，但必须说清为什么：<b>有没有 <c>plugin.json</c>，就是「这个目录是不是一个插件包」的判据</b>。
    /// </para>
    /// <list type="bullet">
    /// <item><c>ManifestSource == "Manifest"</c>：用户指的那个目录里有 <c>plugin.json</c>，
    /// 也就是在声明「这个目录整体是一个插件包」（可能带依赖 dll、图标、资源）。此时<b>整目录复制</b>。</item>
    /// <item><c>ManifestSource == "AssemblyMetadata"</c>：裸 DLL，靠程序集元数据兜底。
    /// 这种情况下 <c>SourceDirectory</c> 只表示「那枚 dll 碰巧躺在哪个目录」，它<b>不是</b>插件包 ——
    /// 可能正好是「下载」文件夹，也可能就是只读扫描目录 <c>plugin/</c>。
    /// 此时<b>只复制那一枚 dll</b>。
    /// <para>
    /// 早期版本在这里无条件整目录复制，有两个真实后果：从「下载」文件夹装一枚裸 dll
    /// 会把整个下载目录搬进插件目录；从 <c>plugin/</c> 安装则会把邻居插件的 dll 一起搬走
    /// —— 于是出现「只装了 A，B 也莫名其妙出现了」。
    /// </para></item>
    /// </list>
    /// </summary>
    private static bool CopyPayload(PluginScanResult scan, string targetDirectory, bool overwrite, out string error)
    {
        if (string.Equals(scan.ManifestSource, "Manifest", StringComparison.Ordinal))
        {
            return CopyDirectory(scan.SourceDirectory, targetDirectory, overwrite, out error);
        }

        if (!CopySingleFile(scan.DllPath, targetDirectory, overwrite, out error))
        {
            return false;
        }

        // 裸 DLL 装完之后必须回填一份清单，否则安装目录「缺 plugin.json」，
        // 后续识别（进而是启用）会直接失败 —— 表现是「装上了却怎么都启不动」。
        return WriteGeneratedManifest(scan, targetDirectory, out error);
    }

    /// <summary>
    /// 为裸 DLL 安装回填 <c>plugin.json</c>：把扫描阶段已经确认过的事实固化成清单。
    /// <para>
    /// 只回填「确定的」：ID、名称、版本、作者、能力、入口程序集文件名。
    /// <b>EntryType</b> 也一并写上 —— 扫描阶段已经解析出来了，写下来能让后续加载不再依赖
    /// 「唯一实现」这种约定推断。
    /// </para>
    /// </summary>
    private static bool WriteGeneratedManifest(PluginScanResult scan, string targetDirectory, out string error)
    {
        PluginManifest source = scan.Manifest!;

        var manifest = new PluginManifest
        {
            SchemaVersion = PluginApi.ManifestSchemaVersion,
            Id = source.Id,
            Name = source.Name,
            Description = source.Description,
            Author = source.Author,
            Homepage = source.Homepage,
            License = source.License,
            Version = source.Version,
            ApiVersion = source.ApiVersion,
            MinHostVersion = source.MinHostVersion,
            MaxHostVersion = source.MaxHostVersion,
            TargetFramework = source.TargetFramework,
            Platform = source.Platform,
            Assembly = Path.GetFileName(scan.DllPath),
            EntryType = scan.EntryTypeFullName,
            Capabilities = new List<string>(source.Capabilities),
            Contributions = new PluginContributions { Actions = true },
            Tags = new List<string>(source.Tags),
        };

        return PluginManifestReader.TryWrite(targetDirectory, manifest, out error);
    }

    /// <summary>只复制一枚程序集（裸 DLL 安装用）。</summary>
    private static bool CopySingleFile(string sourceFile, string targetDirectory, bool overwrite, out string error)
    {
        error = "";
        try
        {
            if (string.IsNullOrWhiteSpace(sourceFile) || !File.Exists(sourceFile))
            {
                error = $"源文件不存在：{sourceFile}";
                return false;
            }

            string fileName = Path.GetFileName(sourceFile);

            // 与整目录复制保持同一条规则：SDK 契约程序集由宿主统一提供，插件不该自带一份。
            if (string.Equals(fileName, PluginApi.AbstractionsAssemblyName + ".dll", StringComparison.OrdinalIgnoreCase))
            {
                error = $"{fileName} 是宿主统一提供的 SDK 契约程序集，不能作为插件安装。";
                return false;
            }

            if (!Directory.Exists(targetDirectory))
            {
                Directory.CreateDirectory(targetDirectory);
            }
            else if (overwrite)
            {
                ClearPreviousPayload(targetDirectory);
            }
            else
            {
                error = $"目标目录已存在：{targetDirectory}";
                return false;
            }

            File.Copy(sourceFile, Path.Combine(targetDirectory, fileName), overwrite: true);
            return true;
        }
        catch (Exception ex)
        {
            error = $"复制插件文件失败：{ex.Message}";
            return false;
        }
    }

    /// <summary>
    /// 覆盖安装裸 DLL 前，清掉上一次的「程序集 + 清单」。
    /// <para>
    /// 不清会踩两个坑：① 目录里留下两枚业务 dll，识别时的「唯一业务 dll」约定直接失效，
    /// 插件变成「找不到程序集」；② 上一次若是带 <c>plugin.json</c> 的包，残留清单会继续
    /// 接管识别，新装的裸 dll 会被判成「清单声明的入口类型不存在」。
    /// </para>
    /// <para>
    /// 只清「载荷」，<b>保留插件私有数据</b>：<c>data\</c> 目录与 <c>settings.json</c>
    /// 都是用户的东西，更新一次版本不该把它们清空。
    /// </para>
    /// </summary>
    private static void ClearPreviousPayload(string targetDirectory)
    {
        try
        {
            foreach (string file in Directory.GetFiles(targetDirectory, "*", SearchOption.TopDirectoryOnly))
            {
                if (string.Equals(Path.GetFileName(file), "settings.json", StringComparison.OrdinalIgnoreCase)) continue;
                File.Delete(file);
            }

            foreach (string directory in Directory.GetDirectories(targetDirectory, "*", SearchOption.TopDirectoryOnly))
            {
                if (string.Equals(Path.GetFileName(directory), "data", StringComparison.OrdinalIgnoreCase)) continue;
                Directory.Delete(directory, recursive: true);
            }
        }
        catch (Exception ex)
        {
            AppLogger.LogWarn($"[plugin] 覆盖安装前清理旧载荷失败（将按原样覆盖）：{ex.Message}");
        }
    }

    private static bool CopyDirectory(string source, string target, bool overwrite, out string error)
    {
        error = "";
        try
        {
            if (!Directory.Exists(source))
            {
                error = $"源目录不存在：{source}";
                return false;
            }

            if (Directory.Exists(target) && !overwrite)
            {
                error = $"目标目录已存在：{target}";
                return false;
            }

            Directory.CreateDirectory(target);

            foreach (string file in Directory.GetFiles(source, "*", SearchOption.AllDirectories))
            {
                string relative = Path.GetRelativePath(source, file);
                string destination = Path.Combine(target, relative);

                // 不复制宿主会统一提供的 SDK 程序集，避免现场出现「类型身份分裂」的隐患
                string fileName = Path.GetFileName(file);
                if (string.Equals(fileName, PluginApi.AbstractionsAssemblyName + ".dll", StringComparison.OrdinalIgnoreCase))
                {
                    AppLogger.LogWarn($"[plugin] 已跳过安装包内的 {fileName}（由宿主统一提供）。");
                    continue;
                }

                string? destinationDirectory = Path.GetDirectoryName(destination);
                if (!string.IsNullOrEmpty(destinationDirectory) && !Directory.Exists(destinationDirectory))
                {
                    Directory.CreateDirectory(destinationDirectory);
                }

                File.Copy(file, destination, overwrite: true);
            }

            return true;
        }
        catch (Exception ex)
        {
            error = $"复制插件文件失败：{ex.Message}";
            return false;
        }
    }
}

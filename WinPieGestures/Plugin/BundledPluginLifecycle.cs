using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using StarPie.Plugin;

namespace WinPieGestures.Plugins;

/// <summary>
/// 随包插件生命周期：只读来源区同步、元数据刷新、载荷补回与停止分发清理。
/// PluginHost 只负责在初始化时编排，本模块不加载任何插件程序集。
/// </summary>
internal static class BundledPluginLifecycle
{
    internal static int Synchronize(
        Func<string, PluginScanResult> scanCandidate,
        Func<PluginScanResult, PluginInstallOptions, PluginInstallResult> installCandidate)
    {
        if (!PluginPaths.ScanRootExists) return 0;

        string[] files;
        try
        {
            files = Directory.GetFiles(PluginPaths.ScanRoot, "*.dll", SearchOption.TopDirectoryOnly);
            Array.Sort(files, StringComparer.OrdinalIgnoreCase);
        }
        catch (Exception ex)
        {
            AppLogger.LogError("[plugin] 扫描随包插件目录失败", ex);
            return 0;
        }

        var seenIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        int unrecognized = 0;
        int changed = 0;

        foreach (string file in files)
        {
            PluginScanResult scan;
            try { scan = scanCandidate(file); }
            catch (Exception ex)
            {
                unrecognized++;
                AppLogger.LogWarn($"[plugin] 随包插件 {Path.GetFileName(file)} 处理失败：{ex.Message}");
                continue;
            }

            if (!scan.Accepted || scan.Manifest == null)
            {
                unrecognized++;
                AppLogger.LogWarn($"[plugin] 随包插件 {Path.GetFileName(file)} 无法识别：{scan.DescribeFailure()}");
                continue;
            }

            PluginManifest manifest = scan.Manifest;
            seenIds.Add(manifest.Id);
            PluginRegistryEntry? existing = PluginRegistryStore.FindEntry(manifest.Id);

            if (existing == null)
            {
                PluginInstallResult result = installCandidate(scan, new PluginInstallOptions
                {
                    Acknowledged = true,
                    OverwriteExisting = false,
                    EnableAfterInstall = false,
                    SourceKind = "Bundled",
                    Bundled = true,
                    AcknowledgedCapabilities = new List<string>(manifest.Capabilities),
                });

                if (!result.Success)
                {
                    AppLogger.LogWarn($"[plugin] 随包插件 {manifest.Id} 自动安装失败：{result.Error}");
                    continue;
                }

                PluginRegistryStore.SetEnabled(manifest.Id, true);
                changed++;
                continue;
            }

            if (!existing.Bundled)
            {
                AppLogger.LogWarn($"[plugin] 随包插件 {manifest.Id} 与用户已安装的同 ID 插件冲突，保留用户版本。");
                continue;
            }

            bool metadataChanged = RefreshMetadata(existing, scan);
            bool payloadChanged = RestorePayload(existing, scan);
            if (metadataChanged || payloadChanged) changed++;
        }

        if (files.Length == 0 || unrecognized > 0)
        {
            if (files.Length == 0)
                AppLogger.LogWarn("[plugin] 随包来源区为空，本轮跳过停止分发清理。");
            else
                AppLogger.LogWarn($"[plugin] 随包来源区有 {unrecognized} 枚文件无法识别，本轮跳过停止分发清理。");
            return changed;
        }

        foreach (PluginRegistryEntry entry in PluginRegistryStore.SnapshotEntries()
                     .Where(entry => entry.Bundled && !seenIds.Contains(entry.Id)))
        {
            try
            {
                DeletePayloadKeepData(entry);
                PluginRegistryStore.RemoveEntry(entry.Id);
                changed++;
                AppLogger.LogInfo($"[plugin] 随包插件 {entry.Id} 已停止分发，已清理载荷并保留 data 目录。");
            }
            catch (Exception ex)
            {
                AppLogger.LogWarn($"[plugin] 清理停止分发的随包插件 {entry.Id} 失败：{ex.Message}");
            }
        }

        return changed;
    }

    private static bool RefreshMetadata(PluginRegistryEntry entry, PluginScanResult scan)
    {
        PluginManifest manifest = scan.Manifest!;
        List<string> claims = BuildClaimWire(manifest);
        bool changed = entry.Name != manifest.Name
            || entry.Version != manifest.Version
            || entry.Description != manifest.Description
            || entry.Author != manifest.Author
            || entry.License != manifest.License
            || entry.Homepage != manifest.Homepage
            || entry.EntrySha256 != scan.Sha256
            || !entry.CapabilitiesAck.SequenceEqual(manifest.Capabilities)
            || !entry.ClaimedTypes.SequenceEqual(claims);

        if (!changed) return false;

        entry.Name = manifest.Name;
        entry.Version = manifest.Version;
        entry.Description = manifest.Description;
        entry.Author = manifest.Author;
        entry.License = manifest.License;
        entry.Homepage = manifest.Homepage;
        entry.EntrySha256 = scan.Sha256;
        entry.CapabilitiesAck = new List<string>(manifest.Capabilities);
        entry.ClaimedTypes = claims;
        PluginRegistryStore.UpsertEntry(entry);
        return true;
    }

    private static bool RestorePayload(PluginRegistryEntry entry, PluginScanResult scan)
    {
        string directory = Path.Combine(PluginPaths.Root,
            string.IsNullOrWhiteSpace(entry.InstallPath) ? entry.Id : entry.InstallPath);
        string expectedDll = Path.Combine(directory, Path.GetFileName(scan.DllPath));
        if (Directory.Exists(directory) && File.Exists(expectedDll)) return false;

        if (!PluginHost.CopyPayload(scan, directory, overwrite: true, out string error))
        {
            AppLogger.LogWarn($"[plugin] 随包插件 {entry.Id} 载荷补回失败：{error}");
            return false;
        }
        return true;
    }

    internal static List<string> BuildClaimWire(PluginManifest manifest) =>
        manifest.ClaimedTypes
            .Where(claim => !string.IsNullOrWhiteSpace(claim.TypeName)
                         && !string.IsNullOrWhiteSpace(claim.ContributionId))
            .Select(claim => claim.ToWire())
            .ToList();

    private static void DeletePayloadKeepData(PluginRegistryEntry entry)
    {
        if (!string.IsNullOrWhiteSpace(entry.ExternalPath)) return;
        string directory = Path.Combine(PluginPaths.Root,
            string.IsNullOrWhiteSpace(entry.InstallPath) ? entry.Id : entry.InstallPath);
        if (!Directory.Exists(directory)) return;

        foreach (string file in Directory.GetFiles(directory, "*", SearchOption.TopDirectoryOnly))
            File.Delete(file);

        foreach (string subdirectory in Directory.GetDirectories(directory, "*", SearchOption.TopDirectoryOnly))
        {
            if (string.Equals(Path.GetFileName(subdirectory), "data", StringComparison.OrdinalIgnoreCase)) continue;
            Directory.Delete(subdirectory, recursive: true);
        }
    }
}

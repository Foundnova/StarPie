using System;
using System.Collections.Generic;
using System.Linq;

namespace WinPieGestures.Plugins;

internal sealed class PluginTypeClaimBinding
{
    public string TypeName { get; init; } = "";
    public string PluginId { get; init; } = "";
    public string ContributionId { get; init; } = "";
    public string FullId => $"{PluginId}.{ContributionId}";
}

/// <summary>官方在线插件的旧动作类型兼容路由表。</summary>
internal static class PluginActionClaimRegistry
{
    private static readonly object Gate = new();
    private static Dictionary<string, PluginTypeClaimBinding> _claims =
        new(StringComparer.OrdinalIgnoreCase);

    public static void Rebuild(IEnumerable<PluginRegistryEntry> entries)
    {
        var candidates = new Dictionary<string, List<PluginTypeClaimBinding>>(StringComparer.OrdinalIgnoreCase);

        foreach (PluginRegistryEntry entry in entries.Where(entry => entry.Official))
        {
            foreach (string wire in entry.ClaimedTypes ?? new List<string>())
            {
                List<StarPie.Plugin.PluginTypeClaim> parsed =
                    StarPie.Plugin.PluginTypeClaim.ParseAll(wire, out List<string> malformed);
                if (malformed.Count > 0 || parsed.Count != 1)
                {
                    AppLogger.LogWarn($"[plugin] {entry.Id} 的类型认领格式无效：{wire}");
                    continue;
                }

                StarPie.Plugin.PluginTypeClaim claim = parsed[0];
                if (string.Equals(claim.TypeName, StarPie.Plugin.PluginApi.ActionTypeName,
                        StringComparison.OrdinalIgnoreCase)
                    || BuiltinActionCatalog.TryGet(claim.TypeName, out _))
                {
                    AppLogger.LogError($"[plugin] {entry.Id} 试图认领保留或内建类型 {claim.TypeName}，已拒绝。");
                    continue;
                }

                if (!candidates.TryGetValue(claim.TypeName, out List<PluginTypeClaimBinding>? list))
                {
                    list = new List<PluginTypeClaimBinding>();
                    candidates[claim.TypeName] = list;
                }

                list.Add(new PluginTypeClaimBinding
                {
                    TypeName = claim.TypeName,
                    PluginId = entry.Id,
                    ContributionId = claim.ContributionId,
                });
            }
        }

        var next = new Dictionary<string, PluginTypeClaimBinding>(StringComparer.OrdinalIgnoreCase);
        foreach ((string type, List<PluginTypeClaimBinding> bindings) in candidates)
        {
            if (bindings.Count == 1)
            {
                next[type] = bindings[0];
                continue;
            }

            AppLogger.LogError($"[plugin] 顶层类型 {type} 被多个官方插件认领：{string.Join(", ", bindings.Select(x => x.PluginId))}；全部拒绝。");
        }

        lock (Gate) _claims = next;
    }

    public static bool TryResolve(string? type, out PluginTypeClaimBinding binding)
    {
        binding = null!;
        if (string.IsNullOrWhiteSpace(type)) return false;
        lock (Gate) return _claims.TryGetValue(type.Trim(), out binding!);
    }

    public static IReadOnlyList<PluginTypeClaimBinding> Snapshot()
    {
        lock (Gate) return _claims.Values.ToArray();
    }
}

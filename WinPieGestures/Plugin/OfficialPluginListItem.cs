using System;

namespace WinPieGestures.Plugins;

/// <summary>官方插件商店列表项，仅承载目录展示状态，不把网络逻辑塞进 WPF 绑定对象。</summary>
internal sealed class OfficialPluginListItem
{
    public OfficialPluginModule Module { get; }
    public string DisplayName => Module.Name;
    public string VersionText => string.IsNullOrWhiteSpace(Module.Version) ? "" : $"v{Module.Version}";
    public string SummaryText => $"{Module.Id}　|　{(Module.TypeClaims.Count == 0 ? "官方动作模块" : string.Join("、", Module.TypeClaims))}";
    public string StateText { get; }
    public string InstallButtonText { get; }
    public bool CanInstall { get; }
    public bool IsInstalled { get; }

    public OfficialPluginListItem(OfficialPluginModule module, string? installedVersion)
    {
        Module = module;
        IsInstalled = !string.IsNullOrWhiteSpace(installedVersion);
        if (!IsInstalled)
        {
            StateText = "未安装";
            InstallButtonText = "⬇️ 下载并安装";
            CanInstall = true;
        }
        else if (string.Equals(installedVersion, module.Version, StringComparison.OrdinalIgnoreCase))
        {
            StateText = "已是最新";
            InstallButtonText = "已安装";
            CanInstall = false;
        }
        else
        {
            StateText = $"已装 v{installedVersion} · 有更新";
            InstallButtonText = "⬆️ 更新";
            CanInstall = true;
        }
    }
}

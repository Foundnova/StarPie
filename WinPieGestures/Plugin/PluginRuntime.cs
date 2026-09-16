using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using StarPie.Plugin;

namespace WinPieGestures.Plugins;

/// <summary>SPP 调用路径的稳定 ID。路径 ID 属于协议语义，不与具体 C# 类型名绑定。</summary>
internal static class PluginPathIds
{
    public const string ActionExecution = "action-execution";
    public const string InteractionEvent = "interaction-event";
    public const string WheelStructure = "wheel-structure";
}

/// <summary>
/// 一条插件调用路径的宿主模块。
/// <para>
/// 这个共同接口只统一路径的生命周期，不试图把动作、事件和轮盘结构压成同一种请求/结果。
/// 每条路径仍通过自己的强类型方法承载业务语义。
/// </para>
/// </summary>
internal abstract class PluginPathModule
{
    public abstract string PathId { get; }

    public virtual void OnPluginStopping(string pluginId)
    {
    }

    public virtual void OnPluginStopped(string pluginId)
    {
    }
}

/// <summary>
/// 宿主支持的调用路径注册表。新增路径只需注册新的 <see cref="PluginPathModule"/>，
/// 公共生命周期广播无需再增加中央 switch。
/// </summary>
internal sealed class PluginPathRegistry
{
    private readonly object _gate = new();
    private readonly Dictionary<string, PluginPathModule> _modules = new(StringComparer.OrdinalIgnoreCase);

    public void Register(PluginPathModule module)
    {
        ArgumentNullException.ThrowIfNull(module);
        if (string.IsNullOrWhiteSpace(module.PathId))
        {
            throw new ArgumentException("插件路径模块必须提供稳定的 PathId。", nameof(module));
        }

        lock (_gate)
        {
            if (!_modules.TryAdd(module.PathId, module))
            {
                throw new InvalidOperationException($"插件调用路径重复注册：{module.PathId}");
            }
        }
    }

    public IReadOnlyList<string> SnapshotPathIds()
    {
        lock (_gate)
        {
            return _modules.Keys.OrderBy(static id => id, StringComparer.Ordinal).ToArray();
        }
    }

    public void NotifyPluginStopping(string pluginId)
    {
        foreach (PluginPathModule module in SnapshotModules())
        {
            try
            {
                module.OnPluginStopping(pluginId);
            }
            catch (Exception ex)
            {
                AppLogger.LogError($"[plugin] 路径 {module.PathId} 处理插件停止通知时异常", ex);
            }
        }
    }

    public void NotifyPluginStopped(string pluginId)
    {
        foreach (PluginPathModule module in SnapshotModules())
        {
            try
            {
                module.OnPluginStopped(pluginId);
            }
            catch (Exception ex)
            {
                AppLogger.LogError($"[plugin] 路径 {module.PathId} 处理插件已停止通知时异常", ex);
            }
        }
    }

    private PluginPathModule[] SnapshotModules()
    {
        lock (_gate)
        {
            return _modules.Values.ToArray();
        }
    }
}

/// <summary>
/// 三条路径共享的调用协调器。当前先统一异常隔离和诊断入口；后续活动调用租约、取消与超时
/// 会在这里扩展，而不复制到每一条路径。
/// </summary>
internal sealed class PluginCallCoordinator
{
    public T Invoke<T>(
        string pathId,
        string operation,
        Func<T> callback,
        Func<Exception, T> fallback)
    {
        ArgumentNullException.ThrowIfNull(callback);
        ArgumentNullException.ThrowIfNull(fallback);

        try
        {
            return callback();
        }
        catch (Exception ex)
        {
            AppLogger.LogError($"[plugin:{pathId}] {operation} 发生未预期异常（已隔离）", ex);
            return fallback(ex);
        }
    }

    public void Invoke(string pathId, string operation, Action callback)
    {
        ArgumentNullException.ThrowIfNull(callback);

        try
        {
            callback();
        }
        catch (Exception ex)
        {
            AppLogger.LogError($"[plugin:{pathId}] {operation} 发生未预期异常（已隔离）", ex);
        }
    }

    public async ValueTask<T> InvokeAsync<T>(
        string pathId,
        string operation,
        Func<ValueTask<T>> callback,
        Func<Exception, T> fallback)
    {
        ArgumentNullException.ThrowIfNull(callback);
        ArgumentNullException.ThrowIfNull(fallback);

        try
        {
            return await callback().ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            AppLogger.LogError($"[plugin:{pathId}] {operation} 发生未预期异常（已隔离）", ex);
            return fallback(ex);
        }
    }
}

/// <summary>
/// 插件调用运行时。主程序仍只接触 <see cref="PluginHost"/>；本类型在门面后统一登记路径、
/// 通过 <see cref="PluginCallCoordinator"/> 治理调用入口并广播插件生命周期，具体路径保持强类型实现。
/// </summary>
internal sealed class PluginRuntime
{
    private readonly PluginPathRegistry _paths = new();
    private readonly PluginCallCoordinator _calls = new();

    public PluginRuntime(Func<ActionItem, PluginExecuteOutcome> legacyActionExecutor)
    {
        Actions = new ActionExecutionPathModule(legacyActionExecutor);
        Interactions = new InteractionEventPathModule();
        WheelStructures = new WheelStructurePathModule();

        _paths.Register(Actions);
        _paths.Register(Interactions);
        _paths.Register(WheelStructures);
    }

    public ActionExecutionPathModule Actions { get; }

    public InteractionEventPathModule Interactions { get; }

    public WheelStructurePathModule WheelStructures { get; }

    public IReadOnlyList<string> SupportedPathIds => _paths.SnapshotPathIds();

    public PluginExecuteOutcome ExecuteAction(ActionItem action) =>
        _calls.Invoke(
            PluginPathIds.ActionExecution,
            "执行动作",
            () => Actions.Execute(action),
            static _ => new PluginExecuteOutcome
            {
                Handled = true,
                Success = false,
                Message = "插件动作运行时发生内部错误，详情见日志。",
            });

    public IDisposable RegisterWheelOpening(string pluginId, Action<ActionContext> handler) =>
        _calls.Invoke(
            PluginPathIds.InteractionEvent,
            "注册 wheel.opening 兼容订阅",
            () => Interactions.RegisterWheelOpening(pluginId, handler),
            static _ => new RegistrationToken(static () => { }));

    public IDisposable RegisterWheelClosed(string pluginId, Action handler) =>
        _calls.Invoke(
            PluginPathIds.InteractionEvent,
            "注册 wheel.closed 兼容订阅",
            () => Interactions.RegisterWheelClosed(pluginId, handler),
            static _ => new RegistrationToken(static () => { }));

    public void RaiseWheelOpening(ActionContext context) =>
        _calls.Invoke(
            PluginPathIds.InteractionEvent,
            "广播 wheel.opening 兼容事件",
            () => Interactions.RaiseWheelOpening(context));

    public void RaiseWheelClosed() =>
        _calls.Invoke(
            PluginPathIds.InteractionEvent,
            "广播 wheel.closed 兼容事件",
            Interactions.RaiseWheelClosed);

    /// <summary>统一交互事件路径入口。当前仅建立强类型接缝，正式队列分发将在后续实现。</summary>
    public int PublishInteractionEvent(PluginInteractionEventEnvelope interactionEvent) =>
        _calls.Invoke(
            PluginPathIds.InteractionEvent,
            "发布统一交互事件",
            () => Interactions.Publish(interactionEvent),
            static _ => 0);

    /// <summary>统一轮盘结构路径入口。当前没有结构提供者时返回空快照。</summary>
    public ValueTask<PluginWheelStructureSnapshot> QueryWheelStructureAsync(
        PluginWheelStructureRequest request,
        CancellationToken cancellationToken) =>
        _calls.InvokeAsync(
            PluginPathIds.WheelStructure,
            "查询轮盘结构",
            () => WheelStructures.QueryAsync(request, cancellationToken),
            static _ => PluginWheelStructureSnapshot.Empty);

    public void NotifyPluginStopping(string pluginId) => _paths.NotifyPluginStopping(pluginId);

    public void NotifyPluginStopped(string pluginId) => _paths.NotifyPluginStopped(pluginId);
}

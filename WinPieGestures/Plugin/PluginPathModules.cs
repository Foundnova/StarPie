using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using StarPie.Plugin;

namespace WinPieGestures.Plugins;

/// <summary>
/// 动作执行路径模块。当前先适配既有动作实现，下一阶段再把解析、惰性加载、校验和调用租约迁入本模块。
/// </summary>
internal sealed class ActionExecutionPathModule : PluginPathModule
{
    private readonly Func<ActionItem, PluginExecuteOutcome> _legacyExecutor;

    public ActionExecutionPathModule(Func<ActionItem, PluginExecuteOutcome> legacyExecutor)
    {
        _legacyExecutor = legacyExecutor ?? throw new ArgumentNullException(nameof(legacyExecutor));
    }

    public override string PathId => PluginPathIds.ActionExecution;

    public PluginExecuteOutcome Execute(ActionItem action) => _legacyExecutor(action);
}

/// <summary>交互事件的只读信封。当前为宿主内部模型，不属于公共 SDK 契约。</summary>
internal sealed class PluginInteractionEventEnvelope
{
    public string EventType { get; init; } = "";
    public int SchemaVersion { get; init; } = 1;
    public long SessionId { get; init; }
    public long Sequence { get; init; }
    public DateTimeOffset Timestamp { get; init; } = DateTimeOffset.UtcNow;
    public ActionContext Context { get; init; } = new();
}

/// <summary>
/// 交互事件路径模块。旧版 Opening/Closed 订阅暂时由此托管；统一事件队列和背压行为后续补齐。
/// </summary>
internal sealed class InteractionEventPathModule : PluginPathModule
{
    private readonly object _gate = new();
    private readonly Dictionary<string, List<Action<ActionContext>>> _wheelOpeningHandlers =
        new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, List<Action>> _wheelClosedHandlers =
        new(StringComparer.OrdinalIgnoreCase);

    public override string PathId => PluginPathIds.InteractionEvent;

    public IDisposable RegisterWheelOpening(string pluginId, Action<ActionContext> handler)
    {
        ArgumentNullException.ThrowIfNull(handler);

        lock (_gate)
        {
            if (!_wheelOpeningHandlers.TryGetValue(pluginId, out List<Action<ActionContext>>? list))
            {
                list = new List<Action<ActionContext>>();
                _wheelOpeningHandlers[pluginId] = list;
            }
            list.Add(handler);
        }

        return new RegistrationToken(() => RemoveWheelOpening(pluginId, handler));
    }

    public IDisposable RegisterWheelClosed(string pluginId, Action handler)
    {
        ArgumentNullException.ThrowIfNull(handler);

        lock (_gate)
        {
            if (!_wheelClosedHandlers.TryGetValue(pluginId, out List<Action>? list))
            {
                list = new List<Action>();
                _wheelClosedHandlers[pluginId] = list;
            }
            list.Add(handler);
        }

        return new RegistrationToken(() => RemoveWheelClosed(pluginId, handler));
    }

    public void RaiseWheelOpening(ActionContext context)
    {
        foreach (Action<ActionContext> handler in SnapshotWheelOpeningHandlers())
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

    public void RaiseWheelClosed()
    {
        foreach (Action handler in SnapshotWheelClosedHandlers())
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

    /// <summary>
    /// 统一交互事件入口占位。返回实际投递数；当前尚未开放统一事件贡献，因此固定为 0。
    /// </summary>
    public int Publish(PluginInteractionEventEnvelope interactionEvent)
    {
        ArgumentNullException.ThrowIfNull(interactionEvent);
        return 0;
    }

    public override void OnPluginStopping(string pluginId)
    {
        lock (_gate)
        {
            _wheelOpeningHandlers.Remove(pluginId);
            _wheelClosedHandlers.Remove(pluginId);
        }
    }

    private void RemoveWheelOpening(string pluginId, Action<ActionContext> handler)
    {
        lock (_gate)
        {
            if (!_wheelOpeningHandlers.TryGetValue(pluginId, out List<Action<ActionContext>>? list)) return;
            list.Remove(handler);
            if (list.Count == 0) _wheelOpeningHandlers.Remove(pluginId);
        }
    }

    private void RemoveWheelClosed(string pluginId, Action handler)
    {
        lock (_gate)
        {
            if (!_wheelClosedHandlers.TryGetValue(pluginId, out List<Action>? list)) return;
            list.Remove(handler);
            if (list.Count == 0) _wheelClosedHandlers.Remove(pluginId);
        }
    }

    private Action<ActionContext>[] SnapshotWheelOpeningHandlers()
    {
        lock (_gate)
        {
            var handlers = new List<Action<ActionContext>>();
            foreach (List<Action<ActionContext>> list in _wheelOpeningHandlers.Values) handlers.AddRange(list);
            return handlers.ToArray();
        }
    }

    private Action[] SnapshotWheelClosedHandlers()
    {
        lock (_gate)
        {
            var handlers = new List<Action>();
            foreach (List<Action> list in _wheelClosedHandlers.Values) handlers.AddRange(list);
            return handlers.ToArray();
        }
    }
}

/// <summary>轮盘结构查询请求占位。当前只建立宿主内部强类型接缝。</summary>
internal sealed class PluginWheelStructureRequest
{
    public string ProviderId { get; init; } = "";
    public string ProfileId { get; init; } = "";
    public ActionContext Context { get; init; } = new();
}

/// <summary>轮盘结构快照占位。正式节点模型稳定前不向公共 SDK 暴露。</summary>
internal sealed class PluginWheelStructureSnapshot
{
    public static readonly PluginWheelStructureSnapshot Empty = new();

    public bool IsAvailable { get; init; }
    public string ProviderId { get; init; } = "";
}

/// <summary>轮盘结构路径模块。当前未注册结构提供者时始终返回空快照。</summary>
internal sealed class WheelStructurePathModule : PluginPathModule
{
    public override string PathId => PluginPathIds.WheelStructure;

    public ValueTask<PluginWheelStructureSnapshot> QueryAsync(
        PluginWheelStructureRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        _ = cancellationToken;
        return ValueTask.FromResult(PluginWheelStructureSnapshot.Empty);
    }
}
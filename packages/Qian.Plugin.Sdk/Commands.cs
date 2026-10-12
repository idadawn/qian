using System.Data.Common;
using System.Text.Json;
using Kun.Core.Cqrs;
using Qian.Data;
using Qian.Identity.Abstractions;
using Qian.Runtime;

namespace Qian.Plugin.Sdk;

/// <summary>真实 Mediator 请求与生成契约的类型关联。</summary>
public interface ICapabilityRequest<TResponse> : IRequest<TResponse>, Kun.Authorization.IPermissionProtectedRequest
{
    /// <summary>由代码声明和 JsonTypeInfo 生成的契约。</summary>
    static abstract CapabilityContract QianCapability { get; }
}

/// <summary>SDK 生产执行端拥有的事件 Definition，供基础组件注册强制监听器。</summary>
public static class CommandEvents
{
    /// <summary>围绕实际 Mediator handler 的命令或查询执行。</summary>
    public static readonly QianEvent<CommandInvocation, object?> Dispatching = QianEvent.Define<CommandInvocation, object?>("command/dispatching", DispatchMode.Waterfall);
}

/// <summary>一次已经绑定业务事务的调用；输入和身份不得从日志字符串化展开。</summary>
public sealed class CommandInvocation
{
    private readonly HashSet<CommandPolicyKind> _entered = [];
    private int _terminalCalls;
    internal CommandInvocation(CapabilityContract contract, JsonElement input, Principal principal, string channel,
        IPluginContext source, DbTransaction transaction, CancellationToken cancellationToken)
    {
        Id = Guid.CreateVersion7(); StartedAt = DateTimeOffset.UtcNow;
        TraceId = System.Diagnostics.Activity.Current?.TraceId.ToHexString() ?? System.Diagnostics.ActivityTraceId.CreateRandom().ToHexString();
        Contract = contract; Input = input.Clone(); Principal = principal; Channel = channel;
        Source = source; Transaction = transaction; CancellationToken = cancellationToken;
    }
    /// <summary>本次执行实例的审计标识。</summary>
    public Guid Id { get; }
    /// <summary>执行开始时刻。</summary>
    public DateTimeOffset StartedAt { get; }
    /// <summary>沿用实际追踪上下文，无上下文时创建 W3C 标识。</summary>
    public string TraceId { get; }
    /// <summary>不可变代码契约。</summary>
    public CapabilityContract Contract { get; }
    /// <summary>实际请求的 JSON 投影，仅用于验证和脱敏；不得复制到普通日志。</summary>
    public JsonElement Input { get; }
    /// <summary>从已认证 Identity 端口复制的主体快照。</summary>
    public Principal Principal { get; }
    /// <summary>宿主在注册能力时确定的调用通道。</summary>
    public string Channel { get; }
    /// <summary>本次调用的真实插件与 scope 上下文。</summary>
    public IPluginContext Source { get; }
    /// <summary>与业务、领域事件和 Outbox 同一连接上的外层事务。</summary>
    public DbTransaction Transaction { get; }
    /// <summary>请求取消及基础策略卸载共同控制的令牌。</summary>
    public CancellationToken CancellationToken { get; }
    internal void Enter(CommandPolicyKind kind) { lock (_entered) { if (!_entered.Add(kind)) throw new InvalidOperationException("同一调用不能重复进入基础策略。"); } }
    internal void RequireTerminal()
    {
        CancellationToken.ThrowIfCancellationRequested();
        lock (_entered) if (_entered.Count != 3) throw CommandPolicies.Denied();
        if (Interlocked.Increment(ref _terminalCalls) != 1) throw new InvalidOperationException("同一能力调用只能执行一次 handler。");
    }
    internal void RequireExecuted() { if (Volatile.Read(ref _terminalCalls) != 1) throw CommandPolicies.Denied(); }
    /// <inheritdoc />
    public override string ToString() => "CommandInvocation(redacted)";
}

/// <summary>实际 Mediator scope 内共享的业务数据会话；只能由 SDK 事务行为绑定。</summary>
public sealed class CommandDataSession<TContext> where TContext : QianDbContext
{
    private IDataSession<TContext>? _session;
    private bool _readOnly;
    /// <summary>创建尚未绑定的会话端口。</summary>
    public CommandDataSession() { }
    /// <summary>与外层事务一致的真实业务上下文。</summary>
    public TContext Context => Current.Context;
    /// <summary>同一 Data 会话的 Outbox 缓冲。</summary>
    public Kun.EventBus.Outbox.IOutboxWriter Outbox => _readOnly ? throw new InvalidOperationException("查询不能写入集成事件。") : Current.Outbox;
    internal IDataSession<TContext> Current => _session ?? throw new InvalidOperationException("能力数据会话尚未绑定或已经结束。");
    internal void Bind(IDataSession<TContext> session, bool readOnly)
    {
        if (_session is not null) throw new InvalidOperationException("业务数据会话不能重复绑定。");
        _session = session; _readOnly = readOnly;
    }
    internal void Clear() => _session = null;
}

using System.Data;
using System.Diagnostics.CodeAnalysis;
using System.Text.Json;
using System.Text.Json.Serialization.Metadata;
using Kun.Core.Cqrs;
using Kun.Core.MultiTenancy;
using Kun.Mediator;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Qian.Data;
using Qian.Authz.Abstractions;
using Qian.Runtime.Errors;
using Kun.Core.Results;
using Qian.Identity.Abstractions;
using Qian.Runtime;
using Qian.Events;

namespace Qian.Plugin.Sdk;

/// <summary>以封闭泛型注册真正的 Mediator 请求、事务和基础执行事件。</summary>
public static class QianCommandRegistration
{
    /// <summary>宿主指定实际插件上下文与通道；请求体不能选择这些值。handler 使用同 scope 的 CommandDataSession。</summary>
    public static KunMediatorBuilder AddQianCapability<TRequest, TResponse, [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)] THandler, TContext>(
        this KunMediatorBuilder builder, Func<IServiceProvider, IPluginContext> source, string channel = "api")
        where TRequest : ICapabilityRequest<TResponse>
        where THandler : class, IRequestHandler<TRequest, TResponse>
        where TContext : QianDbContext
    {
        ArgumentNullException.ThrowIfNull(builder); ArgumentNullException.ThrowIfNull(source);
        if (channel is not ("ui" or "api" or "mcp" or "a2a" or "job" or "event" or "hook")) throw new ArgumentException("调用通道无效。", nameof(channel));
        var contract = TRequest.QianCapability;
        if (contract.RequestTypeInfo is not JsonTypeInfo<TRequest> request || contract.ResponseTypeInfo is not JsonTypeInfo<TResponse> response)
            throw new ArgumentException("生成契约与 Mediator 请求响应类型不一致。");
        builder.Services.AddSingleton(new CommandRegistration<TRequest, TResponse>(source, channel));
        builder.Services.AddTransient<THandler>();
        builder.Services.TryAddScoped<Kun.Core.Security.ICurrentUser>(sp => sp.GetRequiredService<Qian.Identity.Abstractions.ICurrentUser>());
        builder.Services.TryAddScoped<CommandDataSession<TContext>>();
        builder.Services.TryAddScoped(_ => new CommandState());
        builder.AddPipelineBehavior<TRequest, TResponse, CommandTransactionBehavior<TRequest, TResponse, TContext>>();
        builder.AddPipelineBehavior<TRequest, TResponse, CapabilityDispatchBehavior<TRequest, TResponse>>();
        builder.AddPipelineBehavior<TRequest, TResponse, CommandSaveBehavior<TRequest, TResponse, TContext>>();
        return builder.AddAuthorizedHandler<TRequest, TResponse, CommandHandlerProxy<TRequest, TResponse, THandler>>(
            error => throw new QianErrorException(new QianError(error.Type == ErrorType.Unauthorized ? PlatformErrors.Unauthenticated : PlatformErrors.AuthzDenied)),
            contract.Id, request, response);
    }
}

internal sealed record CommandRegistration<TRequest, TResponse>(Func<IServiceProvider, IPluginContext> Source, string Channel)
    where TRequest : ICapabilityRequest<TResponse>;
internal sealed class CommandState
{
    internal CommandInvocation? Invocation { get; set; }
    internal CommandPolicies.PolicySnapshot? Policies { get; set; }
}

internal sealed class CommandTransactionBehavior<TRequest, TResponse, TContext>(IServiceProvider services,
    CommandRegistration<TRequest, TResponse> registration, CommandState state, CommandDataSession<TContext> work,
    ITenantContext tenant, Qian.Identity.Abstractions.ICurrentUser currentUser, Kun.Core.Security.ICurrentUser frameworkUser) : IPipelineBehavior<TRequest, TResponse>
    where TRequest : ICapabilityRequest<TResponse> where TContext : QianDbContext
{
    public async ValueTask<TResponse> Handle(TRequest request, RequestHandlerDelegate<TResponse> next, CancellationToken cancellationToken)
    {
        var source = registration.Source(services); var contract = TRequest.QianCapability;
        if (source.Entry.Plugin != contract.ComponentId || state.Invocation is not null) throw CommandPolicies.Denied();
        var principal = Capture(currentUser, tenant, source);
        if (!frameworkUser.IsAuthenticated || frameworkUser.UserId != principal.Subject || frameworkUser.PrincipalKind != ((Kun.Core.Security.ICurrentUser)currentUser).PrincipalKind)
            throw CommandPolicies.Denied();
        var input = JsonSerializer.SerializeToElement(request, contract.RequestTypeInfo);
        var policies = CommandPolicies.Require(source);
        using var linked = policies.Link(cancellationToken); policies.Check();
        await using var session = await source.Data().OpenAsync<TContext>(linked.Token).ConfigureAwait(false);
        await using var transaction = await session.Context.Database.BeginTransactionAsync(IsolationLevel.Serializable, linked.Token).ConfigureAwait(false);
        work.Bind(session, contract.Metadata.GetProperty("sideEffect").GetString() == "read");
        state.Invocation = new CommandInvocation(contract, input, principal,
            HookCommandOrigin.Channel(registration.Channel, contract.Id, source.Scope), source, transaction.GetDbTransaction(), linked.Token);
        state.Policies = policies;
        try
        {
            // 集成事件只能在命令 handler 同 scope 发出：handler、领域通知与保存都在这个来源作用域内，信封的 producer、actor 与因果链取自本次调用。
            using var events = EventEnvelopeScope.Begin(EventOriginOf(contract, principal, state.Invocation));
            var result = await next().ConfigureAwait(false);
            state.Invocation.RequireExecuted(); policies.Check(); linked.Token.ThrowIfCancellationRequested();
            if (tenant.TenantId != principal.TenantId) throw CommandPolicies.Denied();
            await transaction.CommitAsync(linked.Token).ConfigureAwait(false); return result;
        }
        finally { state.Invocation = null; state.Policies = null; work.Clear(); }
    }

    private static Principal Capture(Qian.Identity.Abstractions.ICurrentUser user, ITenantContext tenant, IPluginContext source)
    {
        var principal = user.Principal; var tenantId = tenant.TenantId;
        if (!user.IsAuthenticated || principal is null || user.UserId != principal.Subject || string.IsNullOrWhiteSpace(principal.Subject) ||
            principal.Kind is not (PrincipalKind.User or PrincipalKind.Service or PrincipalKind.Agent) || principal.TenantId != tenantId ||
            !Guid.TryParseExact(tenantId, "D", out var parsed) || parsed == Guid.Empty || parsed.ToString("D") != tenantId ||
            (source.Scope != ScopeKey.Global && (source.Scope.Kind != "tenant" || source.Scope.Id != tenantId))) throw CommandPolicies.Denied();
        return new Principal
        {
            Issuer = principal.Issuer, Audience = Array.AsReadOnly(principal.Audience.ToArray()), Subject = principal.Subject, Kind = principal.Kind,
            TenantId = principal.TenantId, MembershipId = principal.MembershipId, IdentityRevision = principal.IdentityRevision,
            ProjectId = principal.ProjectId, PluginId = principal.PluginId, Roles = Array.AsReadOnly(principal.Roles.ToArray()), Scope = principal.Scope,
            SessionId = principal.SessionId, TokenId = principal.TokenId, IssuedAt = principal.IssuedAt, NotBefore = principal.NotBefore,
            ExpiresAt = principal.ExpiresAt, Actor = CopyActor(principal.Actor, 0)
        };
    }
    // RFC 8693：有 act 时 act.sub 才是实际行事方，sub 是被代表的委托人。关联 id 沿用外层（如消费端触发的命令），因果是本次调用。
    private static EventOrigin EventOriginOf(CapabilityContract contract, Principal principal, CommandInvocation invocation)
    {
        var actor = principal.Actor is { } delegated
            ? new EventActor { Kind = KindOf(delegated.Kind), Id = delegated.Subject, OnBehalfOf = principal.Subject }
            : new EventActor { Kind = KindOf(principal.Kind), Id = principal.Subject };
        var activity = System.Diagnostics.Activity.Current;
        var traceParent = activity is { IdFormat: System.Diagnostics.ActivityIdFormat.W3C } && activity.TraceId.ToHexString() == invocation.TraceId
            ? activity.Id
            : "00-" + invocation.TraceId + "-" + System.Diagnostics.ActivitySpanId.CreateRandom().ToHexString() + "-00";
        return new EventOrigin(contract.ComponentId, actor)
        {
            CorrelationId = EventEnvelopeScope.Current?.CorrelationId, CausationId = invocation.Id, TraceParent = traceParent
        };
        static EventActorKind KindOf(PrincipalKind kind) => kind switch
        {
            PrincipalKind.User => EventActorKind.User, PrincipalKind.Service => EventActorKind.Service, PrincipalKind.Agent => EventActorKind.Agent,
            _ => throw CommandPolicies.Denied()
        };
    }
    private static DelegationActor? CopyActor(DelegationActor? actor, int depth)
    {
        if (actor is null) return null;
        if (depth >= 8) throw CommandPolicies.Denied();
        return new DelegationActor { Subject = actor.Subject, Kind = actor.Kind, Actor = CopyActor(actor.Actor, depth + 1) };
    }
}

internal sealed class CapabilityDispatchBehavior<TRequest, TResponse>(CommandState state) : IPipelineBehavior<TRequest, TResponse>
    where TRequest : ICapabilityRequest<TResponse>
{
    public async ValueTask<TResponse> Handle(TRequest request, RequestHandlerDelegate<TResponse> next, CancellationToken cancellationToken)
    {
        var invocation = state.Invocation ?? throw CommandPolicies.Denied();
        var result = await invocation.Source.DispatchAsync(CommandEvents.Dispatching, invocation, async () =>
        {
            state.Policies!.Check(); invocation.RequireTerminal(); return (object?)await next().ConfigureAwait(false);
        }, invocation.CancellationToken).ConfigureAwait(false);
        return (TResponse)result!;
    }
}

internal sealed class CommandSaveBehavior<TRequest, TResponse, TContext>(CommandState state, CommandDataSession<TContext> work) : IPipelineBehavior<TRequest, TResponse>
    where TRequest : ICapabilityRequest<TResponse> where TContext : QianDbContext
{
    public async ValueTask<TResponse> Handle(TRequest request, RequestHandlerDelegate<TResponse> next, CancellationToken cancellationToken)
    {
        var invocation = state.Invocation ?? throw CommandPolicies.Denied();
        var result = await next().ConfigureAwait(false);
        if (invocation.Contract.Metadata.GetProperty("sideEffect").GetString() == "read")
        {
            if (work.Current.Context.ChangeTracker.HasChanges()) throw new QianErrorException(new QianError(PlatformErrors.InputInvalid));
            return result;
        }
        await work.Current.SaveChangesAsync(invocation.CancellationToken).ConfigureAwait(false); return result;
    }
}

// Kun 在组装管道时创建 handler；代理把真正的业务对象延迟到租户/权限检查之后，并转交联动取消令牌。
internal sealed class CommandHandlerProxy<TRequest, TResponse, [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)] THandler>(IServiceProvider services, CommandState state)
    : IRequestHandler<TRequest, TResponse>
    where TRequest : ICapabilityRequest<TResponse>
    where THandler : class, IRequestHandler<TRequest, TResponse>
{
    public ValueTask<TResponse> Handle(TRequest request, CancellationToken cancellationToken)
    {
        var invocation = state.Invocation ?? throw CommandPolicies.Denied();
        invocation.CancellationToken.ThrowIfCancellationRequested();
        return services.GetRequiredService<THandler>().Handle(request, invocation.CancellationToken);
    }
}

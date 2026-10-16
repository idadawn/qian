using System.Text.Json;
using Qian.Runtime;
using Qian.Authz.Abstractions;
using Qian.Runtime.Errors;
using Qian.Surfaces.Abstractions;

namespace Qian.Plugin.Sdk;

/// <summary>把实际能力暴露为 hook handler；参数转换来自可信代码，不从声明里执行脚本。</summary>
public static class HookCapabilityRegistration
{
    /// <summary>目标能力必须已在同一代码组件中登记；hook 调用继续经过真实 Mediator、权限、业务事务与审计。</summary>
    public static IDisposable ExposeCapabilityHook<TRequest, TResponse>(this IPluginContext context, string name, Func<HookNotification, TRequest> request)
        where TRequest : ICapabilityRequest<TResponse>
    {
        ArgumentNullException.ThrowIfNull(context); ArgumentException.ThrowIfNullOrWhiteSpace(name); ArgumentNullException.ThrowIfNull(request);
        var contract = TRequest.QianCapability;
        if (contract.ComponentId != context.Entry.Plugin) throw new QianErrorException(new QianError(PlatformErrors.InputInvalid));
        var binding = context.Inject<IExecutableCapability>(CapabilityBindingRegistration.Key, contract.Id);
        return context.Provide<IHookHandler>(HookDefinition.Handlers, contract.ComponentId + "/" + name, new CapabilityHook<TRequest>(binding, request));
    }
    private sealed class CapabilityHook<TRequest>(IExecutableCapability binding, Func<HookNotification, TRequest> convert) : IHookHandler
        where TRequest : Kun.Authorization.IPermissionProtectedRequest
    {
        public async ValueTask<JsonElement> InvokeAsync(HookNotification notification, CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(notification);
            using var linked = CancellationTokenSource.CreateLinkedTokenSource(notification.CancellationToken, cancellationToken);
            // 只有此真实 hook 适配器能够消费开户凭据；普通 Invoke/Send 没有该分支。
            if (TenantInitializationAuthorization.HasGrant && binding.Contract.Metadata.GetProperty("sideEffect").GetString() is not ("read" or "write"))
                throw new QianErrorException(new QianError(PlatformErrors.AuthzDenied));
            using var initialization = TenantInitializationAuthorization.Enter(notification.Source, binding.Contract.Id, typeof(TRequest), TRequest.RequiredPermissions);
            using var origin = HookCommandOrigin.Begin(binding.Contract.Id, notification.Source.Scope);
            var input = JsonSerializer.SerializeToElement(convert(notification), binding.Contract.RequestTypeInfo);
            return await binding.InvokeAsync(notification.Source, input, linked.Token).ConfigureAwait(false);
        }
    }
}

// 仅 SDK 的真实 handler 适配器能设置；请求体不能选择审计通道。
internal static class HookCommandOrigin
{
    private static readonly AsyncLocal<Origin?> Current = new();
    internal static IDisposable Begin(string capability, ScopeKey scope)
    {
        var prior = Current.Value; var next = new Origin(capability, scope); Current.Value = next;
        return new Lease(prior, next);
    }
    internal static string Channel(string fallback, string capability, ScopeKey scope) => Current.Value is { } current && current.Capability == capability && current.Scope == scope ? "hook" : fallback;
    private sealed record Origin(string Capability, ScopeKey Scope);
    private sealed class Lease(Origin? prior, Origin current) : IDisposable
    {
        public void Dispose() { if (ReferenceEquals(Current.Value, current)) Current.Value = prior; }
    }
}

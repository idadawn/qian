using System.Diagnostics.CodeAnalysis;
using System.Text.Json.Serialization.Metadata;
using Kun.Authorization;
using Kun.Core.Cqrs;
using Kun.Core.Results;
using Kun.Mediator;
using Kun.Mediator.Behaviors;
using Microsoft.Extensions.DependencyInjection;

namespace Qian.Authz.Abstractions;

/// <summary>把 Kun 逐命令权限行为与处理器登记到同一 builder；不另实现权限引擎。</summary>
public static class AuthorizedHandlerRegistration
{
    /// <summary>注册受保护命令；权限声明必须非空且符合 R9。拒绝结果由调用方映射。</summary>
    public static KunMediatorBuilder AddAuthorizedHandler<TRequest, TResponse,
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)] THandler>(
        this KunMediatorBuilder builder, Func<Error, TResponse> denied)
        where TRequest : IRequest<TResponse>, IPermissionProtectedRequest
        where THandler : class, IRequestHandler<TRequest, TResponse>
    {
        Validate<TRequest>(builder, denied);
        builder.AddPermissionBehavior<TRequest, TResponse>(denied);
        return builder.AddRequestHandler<TRequest, TResponse, THandler>();
    }

    /// <summary>同时登记稳定能力 ID 与源生成 Schema，权限行为仍在同一 builder 中逐命令装配。</summary>
    public static KunMediatorBuilder AddAuthorizedHandler<TRequest, TResponse,
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)] THandler>(
        this KunMediatorBuilder builder, Func<Error, TResponse> denied, string capabilityId,
        JsonTypeInfo<TRequest> request, JsonTypeInfo<TResponse> response)
        where TRequest : IRequest<TResponse>, IPermissionProtectedRequest
        where THandler : class, IRequestHandler<TRequest, TResponse>
    {
        Validate<TRequest>(builder, denied);
        builder.AddRequestHandler<TRequest, TResponse, THandler>(capabilityId, request, response);
        return builder.AddPermissionBehavior<TRequest, TResponse>(denied);
    }

    // 仅 SDK 使用的受限开户分支；正常调用仍委托 Kun 原有 PermissionBehavior，不复制 RBAC 算法。
    internal static KunMediatorBuilder AddAuthorizedHandler<TRequest, TResponse,
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)] THandler>(
        this KunMediatorBuilder builder, Func<Error, TResponse> denied, string capabilityId,
        JsonTypeInfo<TRequest> request, JsonTypeInfo<TResponse> response, bool tenantInitialization)
        where TRequest : IRequest<TResponse>, IPermissionProtectedRequest
        where THandler : class, IRequestHandler<TRequest, TResponse>
    {
        Validate<TRequest>(builder, denied);
        if (!tenantInitialization) return builder.AddAuthorizedHandler<TRequest, TResponse, THandler>(denied, capabilityId, request, response);
        builder.AddAuthorizedHandler<TRequest, TResponse, THandler>(denied, capabilityId, request, response);
        var descriptor = builder.Services.Last();
        var factory = descriptor.ImplementationFactory ?? throw new InvalidOperationException("Kun 权限行为工厂缺失。");
        builder.Services[^1] = new ServiceDescriptor(descriptor.ServiceType,
            sp => factory(new InitializationPermissionServices(sp)), descriptor.Lifetime);
        return builder;
    }

    private static void Validate<TRequest>(KunMediatorBuilder builder, Delegate denied)
        where TRequest : IPermissionProtectedRequest
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentNullException.ThrowIfNull(denied);
        var permissions = TRequest.RequiredPermissions;
        if (permissions is null || permissions.Count == 0 || permissions.Any(p => !PermissionCode.IsValid(p)))
            throw new ArgumentException("受保护命令必须声明非空且符合 R9 的 RequiredPermissions。", nameof(TRequest));
    }
}

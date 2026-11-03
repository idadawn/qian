using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using Qian.Authz.Abstractions;
using Qian.Data;
using Qian.Identity.Abstractions;
using Qian.Plugin.Sdk;
using Qian.Runtime;
using Qian.Runtime.Errors;

namespace Qian.Tenancy.Signup;

/// <summary>
/// 清单分发的稳定入口（<c>qian.tenancy.signup</c>，装进 identity profile）；程序化装配用 <see cref="TenancySignupPlugin"/>。
/// 装入即开放公开申请接口，不装即不提供自助开通。
/// </summary>
[QianPlugin(TenancySignupDefinition.ComponentId)]
[QianConfigSchema(ComponentConfiguration.Schema)]
[DelegatePlugin(typeof(TenancySignupPlugin))]
[Provides(typeof(TenancySignupDefinition), nameof(TenancySignupDefinition.Key))]
public sealed class TenancySignupComponent : IQianPlugin
{
    /// <inheritdoc />
    public IReadOnlyList<ServiceKey> Inject => [DataDefinition.Key, TenancyDefinition.Key, TenantProvisioning.Key, TenantLifecycle.Key, TenantOperations.Key, IdentityDefinition.AccountsKey];
    /// <summary>可选依赖（清单 optionalInject），含义见 <see cref="TenancySignupPlugin.OptionalInject"/>。</summary>
    public static IReadOnlyList<ServiceKey> OptionalInject => TenancySignupPlugin.OptionalInject;
    /// <inheritdoc />
    public ValueTask ApplyAsync(IPluginContext ctx, JsonElement config, CancellationToken ct) => new TenancySignupPlugin().ApplyAsync(ctx, config, ct);
}

/// <summary>开通申请 Provider：提供 <see cref="ITenantSignup"/>，登记本组件的错误码。宿主须先以 AddQianData 登记 <see cref="TenancySignupDbContext"/>。</summary>
public sealed class TenancySignupPlugin : IQianPlugin
{
    /// <inheritdoc />
    public IReadOnlyList<ServiceKey> Inject => [DataDefinition.Key, TenancyDefinition.Key, TenantProvisioning.Key, TenantLifecycle.Key, TenantOperations.Key, IdentityDefinition.AccountsKey];

    /// <summary>
    /// 可选依赖，与清单的 optionalInject 同一份：Authz 超管旁路（运营端点的平台授权与平台审计链；缺席时运营端点一律 404，identity profile 默认不装 Authz）
    /// 与发件端口（缺席时发验证码返回 503 SIGNUP_UNAVAILABLE，审核结果通知跳过）。都在调用时解析，不阻塞装载。
    /// </summary>
    public static IReadOnlyList<ServiceKey> OptionalInject => [AuthzDefinition.SuperadminBypassKey, TenancySignupDefinition.MessengerKey];

    /// <inheritdoc />
    public ValueTask ApplyAsync(IPluginContext ctx, JsonElement config, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(ctx); ct.ThrowIfCancellationRequested();
        if (ctx.Scope != ScopeKey.Global) throw new QianErrorException(new QianError(PlatformErrors.InputInvalid));
        var options = TenancySignupOptions.Read(config);
        // 数据上下文由宿主登记（连接、schema 与迁移属于部署）；没登记就无处存申请，装载失败而不是首个请求才报错。
        if (ctx.Services.GetService<IDataContextCatalog>() is not { } contexts || !contexts.ContextTypes.Contains(typeof(TenancySignupDbContext)))
            throw new QianErrorException(new QianError(PlatformErrors.PreconditionFailed));
        var errors = ctx.Services.GetRequiredService<ErrorCatalog>();
        foreach (var code in TenancySignupErrors.All) ctx.Effect(errors.Register(code));
        var service = new TenantSignupService(ctx, options);
        ctx.Effect(service.Dispose);
        ctx.Provide<ITenantSignup>(TenancySignupDefinition.Key, service);
        return ValueTask.CompletedTask;
    }
}

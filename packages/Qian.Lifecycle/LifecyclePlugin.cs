using System.Text.Json;
using Kun.Core.MultiTenancy;
using Microsoft.Extensions.DependencyInjection;
using Qian.Audit;
using Qian.Authz.Abstractions;
using Qian.Config;
using Qian.Data;
using Qian.Registry;
using Qian.Runtime;
using Qian.Runtime.Errors;

namespace Qian.Lifecycle;

/// <summary>平台宿主提供的生命周期装配输入：已安装组件版本的进程内钩子由宿主解析，不从数据库或制品内容加载程序集。</summary>
public sealed class LifecycleRuntimeOptions
{
    /// <summary>按组件 ID 与精确版本返回 module 形态的钩子实现；组件没有钩子时返回 null，钩子步骤记为成功、无操作。</summary>
    public Func<string, string, IPluginLifecycleHooks?> Hooks { get; init; } = static (_, _) => null;
}

/// <summary>平台 Lifecycle Provider（服务键 ctx.Lifecycle）：只进 platform profile，依赖真实的 Data、Registry 与 Config（信任根与秘密解析）。</summary>
public sealed class LifecyclePlugin(LifecycleRuntimeOptions options) : IQianPlugin
{
    /// <inheritdoc />
    public IReadOnlyList<ServiceKey> Inject => [DataDefinition.Key, RegistryDefinition.Key, ConfigDefinition.Key];
    /// <summary>
    /// 可选依赖，与清单的 optionalInject 同一份：审计服务（生命周期操作与反向权限批准入审计链）与 Authz 超管旁路（平台主体的管理面判定）。
    /// 缺席时仍装载，按调用解析；缺审计时生命周期操作与批准 fail-closed 并记宿主级日志，缺旁路时平台主体的管理面调用为 404。
    /// </summary>
    public static IReadOnlyList<ServiceKey> OptionalInject => [AuditDefinition.Key, AuthzDefinition.SuperadminBypassKey];
    /// <inheritdoc />
    public ValueTask ApplyAsync(IPluginContext ctx, JsonElement config, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(ctx); ArgumentNullException.ThrowIfNull(options);
        ct.ThrowIfCancellationRequested();
        if (ctx.Scope != ScopeKey.Global) throw new QianErrorException(new QianError(PlatformErrors.InputInvalid));
        var settings = LifecycleSettings.Parse(config);
        var time = ctx.Services.GetService<TimeProvider>() ?? TimeProvider.System;
        var executor = settings.Executor?.Create(ctx, time);
        if (executor is IDisposable disposable) ctx.Effect(disposable);
        ctx.Effect(LifecycleErrors.Register(ctx.Services.GetRequiredService<ErrorCatalog>()));
        var controller = new LifecycleController(ctx, ctx.Services.GetRequiredService<ITenantContext>(), options, settings, new TrustRoots(ctx, time), executor, time);
        ctx.Provide<IPluginLifecycleController>(LifecycleDefinition.Key, controller);
        return ValueTask.CompletedTask;
    }
}

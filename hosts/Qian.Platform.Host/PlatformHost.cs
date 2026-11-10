using System.Diagnostics;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Kun.Core.MultiTenancy;
using Qian.Audit;
using Qian.Authz.AspNetCore;
using Qian.Capabilities.AspNetCore;
using Qian.Config;
using Qian.Data;
using Qian.Plugin.Sdk.AspNetCore;
using Qian.Runtime;
using Qian.Runtime.Errors;
using Qian.Runtime.Errors.AspNetCore;
using Qian.Tenancy.AspNetCore;

namespace Qian.Platform.Host;

/// <summary>
/// platform profile 的生产宿主（S1-DEP-009）。装配拆成可组合的几步，<c>Program.cs</c> 只是按顺序调用：
/// <see cref="CreateBuilder"/>（配置源）→ <see cref="AddQianPlatform"/>（扩展登记 + profile 合成 + DI）→ <see cref="UseQianPlatform"/>（中间件）→
/// <see cref="MapQianPlatform"/>（健康、平台表面、运营接口与扩展端点）。S1-DEP-011 在同一宿主上并入身份分支时复用这些步骤，经扩展登记与 <c>configure</c> 插入自己的部分。
/// </summary>
public static class PlatformHost
{
    /// <summary>宿主入口：<c>--migrate-only</c> 只执行包内迁移后退出，否则装配、监听直到停机。</summary>
    public static async Task<int> RunAsync(string[] args, Action<PlatformHostComposition>? extend = null, Action<WebApplicationBuilder>? configure = null)
    {
        ArgumentNullException.ThrowIfNull(args);
        await using var app = Build(args, extend, configure);
        if (QianDataMigrationExtensions.IsMigrateOnly(args))
        {
            // K8s 迁移 Job 先行：只在 advisory lock 下执行有包内迁移的上下文，不启动宿主；在线副本启动时只校验。
            await app.Services.GetRequiredService<IDataMigrator>().MigrateAsync().ConfigureAwait(false);
            return 0;
        }
        // 先停止 HTTP，再逆序释放插件树（运行时是托管服务，随宿主停止释放）；DI 容器最后释放。
        await app.RunAsync().ConfigureAwait(false);
        return 0;
    }

    /// <summary>按 Program 的顺序装配出完整宿主（未启动）。</summary>
    public static WebApplication Build(string[] args, Action<PlatformHostComposition>? extend = null, Action<WebApplicationBuilder>? configure = null)
    {
        var builder = CreateBuilder(args);
        configure?.Invoke(builder);
        var composition = builder.AddQianPlatform(args, extend);
        var app = builder.Build();
        app.UseQianPlatform();
        app.MapQianPlatform(composition);
        return app;
    }

    /// <summary>配置源与网关宿主相同：<c>appsettings.json</c>（及环境名文件）→ <c>QIAN_</c> 前缀环境变量 → 命令行；内容根是程序目录。</summary>
    public static WebApplicationBuilder CreateBuilder(string[] args)
    {
        ArgumentNullException.ThrowIfNull(args);
        var builder = WebApplication.CreateBuilder(new WebApplicationOptions { Args = args, ContentRootPath = AppContext.BaseDirectory });
        builder.Configuration.Sources.Clear();
        builder.Configuration.SetBasePath(AppContext.BaseDirectory).AddJsonFile("appsettings.json", optional: true, reloadOnChange: false)
            .AddJsonFile("appsettings." + builder.Environment.EnvironmentName + ".json", optional: true, reloadOnChange: false)
            .AddEnvironmentVariables("QIAN_").AddCommandLine(args.Where(arg => arg != "--migrate-only").ToArray());
        return builder;
    }

    /// <summary>读设置、收扩展登记、合成 platform profile 并登记宿主 DI 与插件树运行时；profile 不合格或有条目没有 Provider 即抛错。</summary>
    public static PlatformHostComposition AddQianPlatform(this WebApplicationBuilder builder, string[] args, Action<PlatformHostComposition>? extend = null)
    {
        ArgumentNullException.ThrowIfNull(builder); ArgumentNullException.ThrowIfNull(args);
        var composition = new PlatformHostComposition(builder, PlatformHostSettings.Read(builder.Configuration, args));
        extend?.Invoke(composition);
        composition.Compose();
        return composition;
    }

    /// <summary>
    /// 中间件：ErrorDetail → 网关 S2S 认证 → 按令牌 <c>tenant_id</c> 进入租户 scope（平台身份不进任何租户）→ 授权 → 插件端点可见性 → 授权 PEP（生产模式，处理器内判定的端点也比对身份修订下限）。
    /// </summary>
    public static WebApplication UseQianPlatform(this WebApplication app)
    {
        ArgumentNullException.ThrowIfNull(app);
        var tenantContext = app.Services.GetRequiredService<ITenantContext>();
        app.UseQianErrors(); app.UseAuthentication();
        app.Use(async (http, next) =>
        {
            if (GatewayS2s.Principal(http) is { TenantId: { } tenant })
            {
                using var scope = tenantContext.BeginScope(tenant);
                await next(http).ConfigureAwait(false);
            }
            else await next(http).ConfigureAwait(false);
        });
        app.UseAuthorization(); app.UseQianPluginEndpoints(); app.UseQianAuthzEnforcement();
        return app;
    }

    /// <summary>健康端点（匿名）、平台表面（受众 <c>qian-platform</c>）、平台运营接口（受众 <c>qian-operations</c>）与扩展端点。</summary>
    public static WebApplication MapQianPlatform(this WebApplication app, PlatformHostComposition composition)
    {
        ArgumentNullException.ThrowIfNull(app); ArgumentNullException.ThrowIfNull(composition);
        var runtime = app.Services.GetRequiredService<PlatformRuntime>();
        // K8s 探针：存活只看进程；就绪要求插件树全部条目 Ready 且 Registry 已启动。两者都不回显任何组件细节。
        app.MapGet("/healthz", () => Results.Text("ok")).AllowAnonymous().ExcludeFromDescription();
        app.MapGet("/readyz", () => runtime.IsReady ? Results.Text("ready") : Results.Text("starting", statusCode: StatusCodes.Status503ServiceUnavailable))
            .AllowAnonymous().ExcludeFromDescription();
        app.MapQianCapabilities(http => Scoped(http, "capabilities"));
        app.MapQianPlatformConsole(http => Console(http, runtime));
        app.MapQianAuthz(http => Scoped(http, "authz"));
        foreach (var map in composition.Endpoints) map(app, runtime);
        app.MapQianTenancy(_ => runtime.Global("tenancy"));
        foreach (var map in composition.OperationEndpoints) map(app, runtime);
        return app;
    }

    // 全局 Provider 上下文按请求租户收窄；没有租户（平台身份）即未认证，组件不在即 404。
    internal static IPluginContext Scoped(HttpContext http, string id)
    {
        var tenant = http.RequestServices.GetRequiredService<ITenantContext>().TenantId;
        if (!Guid.TryParseExact(tenant, "D", out var parsed) || parsed == Guid.Empty) throw new QianErrorException(new QianError(PlatformErrors.Unauthenticated));
        return http.RequestServices.GetRequiredService<PlatformRuntime>().Global(id).Scoped(new ScopeKey("tenant", tenant!));
    }

    // 平台控制台目录用 Capabilities 的全局上下文，只给不带租户的请求（平台身份）；租户成员的请求按不存在拒绝，与目录服务自己的主体检查一致。
    private static IPluginContext Console(HttpContext http, PlatformRuntime runtime)
    {
        if (!string.IsNullOrEmpty(http.RequestServices.GetRequiredService<ITenantContext>().TenantId)) throw new QianErrorException(new QianError(PlatformErrors.NotFound));
        return runtime.Global("capabilities");
    }
}

/// <summary>秘密访问的审计接收端：每次解析（含拒绝）写平台审计链，写失败不返回密钥（与网关宿主同一做法）。</summary>
internal sealed class PlatformSecretAudit(Func<IPluginContext> source) : ISecretAccessObserver
{
    public async ValueTask RecordAsync(SecretAccessEvent access, CancellationToken cancellationToken = default)
    {
        var context = source();
        if (access.TenantId is not null) throw new InvalidOperationException("平台宿主的秘密只允许平台域。");
        using var payload = JsonDocument.Parse(new JsonObject { ["reference"] = access.Reference, ["allowed"] = access.Allowed }.ToJsonString());
        var inputHash = Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(access.Reference)));
        var trace = Activity.Current?.TraceId.ToHexString() ?? ActivityTraceId.CreateRandom().ToHexString();
        await context.Audit().AppendPlatformAsync(context, new AuditRecord(Guid.CreateVersion7(), null, "service", access.Caller, null,
            "qian.config:secret:resolve", "1.0.0", inputHash, "api", null, access.Outcome, trace, DateTimeOffset.UtcNow, payload.RootElement), cancellationToken).ConfigureAwait(false);
    }
}

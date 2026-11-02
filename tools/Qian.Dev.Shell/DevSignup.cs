using System.Text.Json;
using Kun.EntityFrameworkCore.Extensions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Qian.Data;
using Qian.Dev.TenantOps;
using Qian.Runtime;
using Qian.Runtime.Errors.AspNetCore;
using Qian.Tenancy.Signup;

namespace Qian.Dev.Shell;

// 租户自助开通（S1-TEN-019）挂进身份 / 业务宿主：与身份服务同宿主（identity profile 的形状），申请人接口（/api/signup/verifications、
// /session、/code-availability、/me/**）与公开页（/signup，有构建产物时）走身份的公开分支，不经业务管线；
// 发件端口由开发期发件箱提供（只在 Development、只在回环宿主上登记），读取路由 GET /__dev/signup/outbox 要带状态目录里的开发令牌。
// 运营接口（/api/signup/applications/**）在平台控制台交付（S1-YUE-018）之前不在本环境暴露。
internal sealed class DevSignup
{
    internal const string TokenFileName = "signup-outbox.token";
    private readonly string? _uiDist;
    private readonly Func<PluginLoader> _loader;

    internal DevSignup(Uri issuer, string state, string? uiDist, Func<PluginLoader> loader)
    {
        _uiDist = uiDist; _loader = loader;
        // 开发令牌每次启动重新生成，只写进状态目录（仅本人可读）；E2E 从同一文件读取。
        var token = DevSignupOutbox.NewToken();
        TokenFile = Path.Combine(state, TokenFileName);
        File.WriteAllText(TokenFile, token);
        if (!OperatingSystem.IsWindows()) File.SetUnixFileMode(TokenFile, UnixFileMode.UserRead | UnixFileMode.UserWrite);
        // 宿主本身不改环境（同 DevIdentity 给协议库的声明）：开发期发件箱只在这里以开发环境登记，且只接受回环监听地址。
        Outbox = new DevSignupOutbox(new DevIdentity.DevelopmentEnvironment(), [issuer], token);
    }

    internal DevSignupOutbox Outbox { get; }
    internal string TokenFile { get; }
    internal bool HostsPages => _uiDist is not null;

    internal static void Configure(WebApplicationBuilder builder, DevDatabase database) =>
        builder.Services.AddQianData<TenancySignupDbContext>((_, options) => options.UseNpgsql(database.App).UseKunSchema(TenancySignupDbContext.Schema));

    // 开通申请库与生产同一套迁移，授权给应用登录（全局表，无 RLS）。
    internal static string SchemaSql(IServiceProvider services)
    {
        using var scope = services.CreateScope();
        return scope.ServiceProvider.GetRequiredService<TenancySignupDbContext>().GetService<IMigrator>().GenerateScript() +
            $"GRANT USAGE ON SCHEMA {TenancySignupDbContext.Schema} TO {DevDatabase.AppRole}; GRANT SELECT, INSERT, UPDATE, DELETE ON ALL TABLES IN SCHEMA {TenancySignupDbContext.Schema} TO {DevDatabase.AppRole};";
    }

    // 身份组件之后挂：先挂发件箱（提供 TenancySignupMessenger），再挂开通申请组件。
    internal async Task MountAsync(PluginLoader loader, CancellationToken ct)
    {
        await loader.MountAsync([
            new PluginDefinition(new PluginEntry("tenancy.signup.outbox", "qian.dev.signup-outbox", "1.0.0", ScopeKey.Global, 0, JsonSerializer.SerializeToElement(new { })),
                new OutboxPlugin(Outbox), [TenancySignupDefinition.MessengerKey]),
            new PluginDefinition(new PluginEntry("tenancy.signup", TenancySignupDefinition.ComponentId, "1.0.0", ScopeKey.Global, 1, JsonSerializer.SerializeToElement(new { })),
                new TenancySignupPlugin(), [TenancySignupDefinition.Key])], ct).ConfigureAwait(false);
        DevPlatform.EnsureReady(loader, "tenancy.signup.outbox"); DevPlatform.EnsureReady(loader, "tenancy.signup");
    }

    /// <summary>身份公开分支要接手的路径：申请人接口、公开页与开发期发件箱的读取路由。</summary>
    internal bool Owns(PathString path) => TenancySignupEndpoints.IsApplicantPath(path) || path.StartsWithSegments(DevSignupOutbox.Route) ||
        (HostsPages && path.StartsWithSegments(TenancySignupPagesHosting.PagesPath));

    // 身份分支在业务管线的错误映射之前：开通申请的路径在分支里自带 ErrorDetail 映射（绑定失败的 400 / 415 等空响应体也写成 ErrorDetail），再托管公开页。
    internal void Use(IApplicationBuilder branch)
    {
        branch.UseWhen(http => TenancySignupEndpoints.IsApplicantPath(http.Request.Path), errors => errors.UseQianErrors());
        if (_uiDist is not null) branch.UseQianTenancySignupPages(_uiDist);
    }

    internal void Map(IEndpointRouteBuilder endpoints)
    {
        endpoints.MapQianTenancySignupApplicant(_ => _loader().GetContext("tenancy.signup", ScopeKey.Global));
        endpoints.MapGet(DevSignupOutbox.Route, Outbox.Read);
    }

    private sealed class OutboxPlugin(DevSignupOutbox outbox) : IQianPlugin
    {
        public IReadOnlyList<ServiceKey> Inject => [];
        public ValueTask ApplyAsync(IPluginContext ctx, JsonElement config, CancellationToken ct)
        {
            ctx.Provide<ISignupMessenger>(TenancySignupDefinition.MessengerKey, outbox);
            return ValueTask.CompletedTask;
        }
    }
}

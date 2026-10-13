using System.Security.Claims;
using Kun.Authorization;
using Kun.Core.Cqrs;
using Kun.Core.MultiTenancy;
using Kun.Core.Results;
using Kun.Core.Security;
using Kun.EntityFrameworkCore.Extensions;
using Kun.EventBus.DependencyInjection;
using Kun.Mediator;
using Kun.Mediator.DependencyInjection;
using Kun.MultiTenancy;
using Kun.Security.Security;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Qian.Authz.Abstractions;
using Qian.Data;

namespace Qian.Authz.Tests;

public sealed partial class PermissionCatalogPersistenceTests
{
    [Fact]
    public async Task 正式SDK鉴权管道与真实PG角色_同名服务不继承用户授权()
    {
        var userTenant = new AsyncLocalTenantContext(); var serviceTenant = new AsyncLocalTenantContext();
        var registry = new PermissionRegistry(); var catalog = Catalog(); using var active = registry.Register("1.0.0", catalog.Permissions);
        using var userHost = PrincipalHost("user", userTenant, registry); using var serviceHost = PrincipalHost("service", serviceTenant, registry);
        await userHost.StartAsync(Ct); await serviceHost.StartAsync(Ct); await Prepare(userHost); await Store(userHost).PutAsync(catalog, Ct);
        using var userScope = userTenant.BeginScope("tenant-a"); using var serviceScope = serviceTenant.BeginScope("tenant-a");
        var roles = new PostgresTenantRoleStore(userHost.Services.GetRequiredService<IDataService>(), userTenant);
        await roles.EnsureBuiltinsAsync("bootstrap", Ct); await roles.AssignAsync("tenant.user", "user", "shared-id", "bootstrap", Ct);
        await roles.InstallDefaultsAsync("qian.sample.crm", "1.0.0", null, "bootstrap", Ct);
        var userResult = await userHost.Services.GetRequiredService<ISender>().Send(new KindBoundaryCommand(), Ct);
        var serviceResult = await serviceHost.Services.GetRequiredService<ISender>().Send(new KindBoundaryCommand(), Ct);
        Assert.True(userResult.IsSuccess); Assert.False(serviceResult.IsSuccess);
        Assert.Equal(ErrorType.Forbidden, serviceResult.Error.Type);
        Assert.Equal(1, userHost.Services.GetRequiredService<KindBoundaryCounter>().Calls);
        Assert.Equal(0, serviceHost.Services.GetRequiredService<KindBoundaryCounter>().Calls);
    }

    private IHost PrincipalHost(string kind, AsyncLocalTenantContext tenant, IPermissionRegistry registry)
    {
        var builder = Microsoft.Extensions.Hosting.Host.CreateApplicationBuilder();
        var http = new DefaultHttpContext { User = new ClaimsPrincipal(new ClaimsIdentity([
            new Claim("sub", "shared-id"), new Claim("tenant_id", "tenant-a"), new Claim("principal_kind", kind)], "validated")) };
        builder.Services.AddSingleton<ICurrentUser>(new JwtCurrentUser(new KindBoundaryAccessor(http)));
        builder.Services.AddSingleton<ITenantContext>(tenant); builder.Services.AddSingleton(registry); builder.Services.AddKunEventBus();
        builder.Services.AddQianData<AuthzDbContext>((_, options) => options.UseNpgsql(postgres.AppConnectionString).UseKunSchema(AuthzDbContext.Schema));
        builder.Services.AddSingleton<IPermissionChecker>(sp => new RbacPermissionChecker(sp.GetRequiredService<IDataService>(), tenant, registry));
        builder.Services.AddSingleton<KindBoundaryCounter>();
        builder.Services.AddKunMediator(builder => builder.AddAuthorizedHandler<KindBoundaryCommand, Result<int>, KindBoundaryHandler>(Result<int>.Fail));
        return builder.Build();
    }
    private sealed class KindBoundaryAccessor(HttpContext context) : IHttpContextAccessor { public HttpContext? HttpContext { get; set; } = context; }
}

internal sealed record KindBoundaryCommand : IRequest<Result<int>>, IPermissionProtectedRequest
{
    public static IReadOnlyList<string> RequiredPermissions => ["qian.sample.crm:customer:read"];
}
internal sealed class KindBoundaryCounter { public int Calls { get; set; } }
internal sealed class KindBoundaryHandler(KindBoundaryCounter counter) : IRequestHandler<KindBoundaryCommand, Result<int>>
{
    public ValueTask<Result<int>> Handle(KindBoundaryCommand request, CancellationToken cancellationToken)
    { counter.Calls++; return ValueTask.FromResult(Result<int>.Ok(1)); }
}

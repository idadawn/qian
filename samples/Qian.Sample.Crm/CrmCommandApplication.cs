using System.Security.Claims;
using Kun.AspNetCore.Authorization;
using Qian.Plugin.Sdk.AspNetCore;
using Kun.Authorization;
using Kun.Core.MultiTenancy;
using Kun.EntityFrameworkCore.Extensions;
using Kun.EventBus;
using Kun.EventBus.DependencyInjection;
using Kun.Mediator;
using Kun.Mediator.DependencyInjection;
using Kun.Security.DependencyInjection;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Qian.Audit;
using Qian.Authz;
using Qian.Authz.Abstractions;
using Qian.Authz.AspNetCore;
using Qian.Data;
using Qian.Events;
using Qian.Plugin.Sdk;
using Qian.Tenancy;

namespace Qian.Sample.Crm;

/// <summary>独立参考宿主：真实 S2S、RBAC、租户、业务/Outbox/审计事务。凭据和迁移由宿主负责。</summary>
public static class CrmCommandApplication
{
    // Kun 的事件类型表只认第一次 AddKunEventBus，中介者只认最后一次 AddKunMediator：嵌入平台组件（Registry、Lifecycle）的宿主
    // 经 events / mediator 把自己的登记并进本宿主的同一次调用，而不是另行调用。
    public static WebApplication Build(string[] args, Action<WebApplicationBuilder>? configure = null, Action<IEventTypeRegistrar>? events = null,
        Action<KunMediatorBuilder>? mediator = null)
    {
        var app = CrmApplication.Build(args, builder =>
        {
            configure?.Invoke(builder);
            var reader = builder.Configuration["S2S:ReaderClientId"] ?? throw new InvalidOperationException("缺少 S2S:ReaderClientId。");
            var writer = builder.Configuration["S2S:WriterClientId"] ?? throw new InvalidOperationException("缺少 S2S:WriterClientId。");
            // 身份适配器先于数据约定登记，避免框架的匿名兜底占住当前主体端口。
            builder.Services.AddKunSecurity();
            builder.Services.AddHttpContextAccessor();
            builder.Services.ConfigureHttpJsonOptions(options =>
            {
                options.SerializerOptions.TypeInfoResolverChain.Insert(0, CrmCommandJson.Default);
                options.SerializerOptions.PropertyNameCaseInsensitive = false;
                options.SerializerOptions.UnmappedMemberHandling = System.Text.Json.Serialization.JsonUnmappedMemberHandling.Disallow;
                options.SerializerOptions.RespectRequiredConstructorParameters = true;
                options.SerializerOptions.RespectNullableAnnotations = true;
            });
            builder.Services.AddScoped<Qian.Identity.Abstractions.ICurrentUser, CrmCurrentUser>();
            builder.Services.PostConfigure<JwtBearerOptions>(JwtBearerDefaults.AuthenticationScheme, options =>
            {
                var previous = options.Events.OnTokenValidated;
                options.Events.OnTokenValidated = async context =>
                {
                    if (previous is not null) await previous(context).ConfigureAwait(false);
                    if (context.Result is not null && !context.Result.Succeeded) return;
                    CrmCurrentUser.Capture(context);
                };
            });
            builder.Services.AddAuthorizationBuilder().AddPolicy("crm-data-writer", policy => policy.RequireAuthenticatedUser()
                .RequireClaim("sub", writer).RequireClaim("principal_kind", "service")
                .RequireAssertion(context => (context.User.FindFirstValue("scope") ?? "").Split(' ').Contains("crm.write", StringComparer.Ordinal)));
            builder.Services.AddAuthorizationBuilder().AddPolicy("crm-command-reader", policy => policy.RequireAuthenticatedUser().RequireClaim("principal_kind", "service")
                .RequireAssertion(context => context.User.FindFirstValue("sub") is { } subject && (subject == reader || subject == writer))
                .RequireAssertion(context => (context.User.FindFirstValue("scope") ?? "").Split(' ').Contains("crm.read", StringComparer.Ordinal)));
            builder.Services.AddKunEventBus(registrar =>
            {
                registrar.RegisterCrmEvents().RegisterTenancyEvents();
                events?.Invoke(registrar);
            });
            // Outbox 行即 v1 信封；配置了 broker 才投递到 RabbitMQ，未配置时只写 Outbox（本地无 MQ 的开发与测试）。
            builder.Services.AddQianEvents();
            if (builder.Configuration.GetConnectionString("Events") is { Length: > 0 })
                builder.Services.AddQianEventsBroker(_ => Connection("Events"));
            builder.Services.AddSingleton<IPermissionRegistry, PermissionRegistry>();
            builder.Services.AddQianData<AuthzDbContext>((_, options) => options.UseNpgsql(Connection("Authz")).UseKunSchema(AuthzDbContext.Schema));
            builder.Services.AddQianData<TenancyDbContext>((_, options) => options.UseNpgsql(Connection("Tenancy")).UseKunSchema(TenancyDbContext.Schema));
            builder.Services.AddQianData<AuditDbContext>((_, options) => options.UseNpgsql(Connection("Audit")).UseKunSchema(AuditDbContext.Schema));
            builder.Services.AddSingleton<IPermissionChecker>(sp => new RbacPermissionChecker(sp.GetRequiredService<IDataService>(),
                sp.GetRequiredService<ITenantContext>(), sp.GetRequiredService<IPermissionRegistry>()));
            builder.Services.AddSingleton<CrmCommandRuntime>();
            builder.Services.AddHostedService(sp => sp.GetRequiredService<CrmCommandRuntime>());
            builder.Services.AddKunMediator(registry =>
            {
                registry.AddQianTenancyEvents();
                registry.AddQianCapability<CreateCustomerCommand, CustomerItem, CreateCustomerHandler, CrmDbContext>(sp => sp.GetRequiredService<CrmCommandRuntime>().Source());
                registry.AddQianCapability<ListCustomersQuery, CustomerPage, ListCustomersHandler, CrmDbContext>(sp => sp.GetRequiredService<CrmCommandRuntime>().Source());
                registry.AddQianCapability<GetCustomerQuery, CustomerItem, GetCustomerHandler, CrmDbContext>(sp => sp.GetRequiredService<CrmCommandRuntime>().Source());
                mediator?.Invoke(registry);
            });
            string Connection(string name) => builder.Configuration.GetConnectionString(name) ?? throw new InvalidOperationException("缺少连接引用：" + name);
        }, mapReadEndpoints: false);
        app.Use(async (context, next) =>
        {
            if (context.Request.Path.StartsWithSegments("/api/customers") && context.User.Identity?.IsAuthenticated == true)
                _ = context.RequestServices.GetRequiredService<CrmCommandRuntime>().Source();
            await next(context).ConfigureAwait(false);
        });
        app.MapPost("/api/customers", async (CreateCustomerCommand command, CrmCommandRuntime runtime, CancellationToken ct) =>
        {
            var source = runtime.Source(); var commands = source.Inject<ICrmCommands>(CrmDefinition.Key);
            var result = await commands.CreateAsync(command, ct).ConfigureAwait(false);
            return Results.Created("/api/customers/" + result.Id, result);
        }).RequireAuthorization("crm-data-writer").RequireKunPermission("qian.sample.crm:customer:write")
            .WithMetadata(new PluginEndpointBinding("crm", "qian.sample.crm", CapabilityBindingRegistration.Key, "qian.sample.crm/customer.create")).Produces<CustomerItem>(StatusCodes.Status201Created);
        app.MapGet("/api/customers", async (int? skip, int? take, CrmCommandRuntime runtime, CancellationToken ct) =>
            Results.Ok(await runtime.Source().Inject<ICrmCommands>(CrmDefinition.Key).ListAsync(new ListCustomersQuery(skip ?? 0, take ?? 100), ct).ConfigureAwait(false)))
            .RequireAuthorization("crm-command-reader").RequireKunPermission("qian.sample.crm:customer:read")
            .WithMetadata(new PluginEndpointBinding("crm", "qian.sample.crm", CapabilityBindingRegistration.Key, "qian.sample.crm/customer.list")).Produces<CustomerPage>();
        app.MapGet("/api/customers/{id:guid}", async (Guid id, CrmCommandRuntime runtime, CancellationToken ct) =>
            Results.Ok(await runtime.Source().Inject<ICrmCommands>(CrmDefinition.Key).GetAsync(new GetCustomerQuery(id), ct).ConfigureAwait(false)))
            .RequireAuthorization("crm-command-reader").RequireKunPermission("qian.sample.crm:customer:read")
            .WithMetadata(new PluginEndpointBinding("crm", "qian.sample.crm", CapabilityBindingRegistration.Key, "qian.sample.crm/customer.get")).Produces<CustomerItem>();
        // 当前主体在本租户的有效权限（含风险、step-up 与终端元数据）；主体与租户只取自已验证令牌。
        app.MapQianAuthz(http => http.RequestServices.GetRequiredService<CrmCommandRuntime>().AuthzSource());
        return app;
    }
}

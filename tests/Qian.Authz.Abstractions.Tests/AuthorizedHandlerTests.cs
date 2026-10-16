using System.Text.Json;
using System.Text.Json.Serialization;
using Kun.Authorization;
using Kun.Core.Cqrs;
using Kun.Core.MultiTenancy;
using Kun.Core.Security;
using Kun.Mediator;
using Kun.Mediator.DependencyInjection;
using Microsoft.Extensions.DependencyInjection;
using Qian.Runtime;

namespace Qian.Authz.Abstractions.Tests;

internal sealed record ProtectedCommand : IRequest<int>, IPermissionProtectedRequest
{
    public static IReadOnlyList<string> RequiredPermissions => ["qian.sample.crm:customer:read", "qian.sample.crm:customer:export"];
}
internal sealed record EmptyCommand : IRequest<int>, IPermissionProtectedRequest
{
    public static IReadOnlyList<string> RequiredPermissions => [];
}
internal sealed record InvalidCommand : IRequest<int>, IPermissionProtectedRequest
{
    public static IReadOnlyList<string> RequiredPermissions => ["invalid"];
}
internal sealed class Handler(List<string> calls) : IRequestHandler<ProtectedCommand, int>
{
    public ValueTask<int> Handle(ProtectedCommand request, CancellationToken cancellationToken) { calls.Add("handler"); return ValueTask.FromResult(42); }
}
internal sealed class EmptyHandler : IRequestHandler<EmptyCommand, int>
{
    public ValueTask<int> Handle(EmptyCommand request, CancellationToken cancellationToken) => ValueTask.FromResult(0);
}
internal sealed class InvalidHandler : IRequestHandler<InvalidCommand, int>
{
    public ValueTask<int> Handle(InvalidCommand request, CancellationToken cancellationToken) => ValueTask.FromResult(0);
}
[JsonSerializable(typeof(ProtectedCommand))]
[JsonSerializable(typeof(int))]
internal sealed partial class AuthzTestJson : JsonSerializerContext;

public sealed class AuthorizedHandlerTests
{
    [Fact]
    public void 初始化注册拒绝非Kun权限行为的最后描述符()
    {
        var services = new ReplacingServices();
        var builder = new KunMediatorBuilder(services);
        var method = typeof(AuthorizedHandlerRegistration).GetMethods(System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static)
            .Single(value => value.Name == "AddAuthorizedHandler" && value.GetParameters().Length == 6)
            .MakeGenericMethod(typeof(ProtectedCommand), typeof(int), typeof(Handler));
        var invocation = Assert.Throws<System.Reflection.TargetInvocationException>(() => method.Invoke(null,
            [builder, (Func<Kun.Core.Results.Error, int>)(_ => -1), "qian.sample.crm/export", AuthzTestJson.Default.ProtectedCommand, AuthzTestJson.Default.Int32, true]));
        Assert.Equal("Kun 权限行为登记类型与预期不一致。", Assert.IsType<InvalidOperationException>(invocation.InnerException).Message);
        Assert.True(services.Replaced);
    }
    private sealed class ReplacingServices : System.Collections.ObjectModel.Collection<ServiceDescriptor>, IServiceCollection
    {
        internal bool Replaced { get; private set; }
        protected override void InsertItem(int index, ServiceDescriptor item)
        {
            if (item.ServiceType.IsConstructedGenericType && item.ServiceType.GetGenericTypeDefinition().FullName == "Kun.Mediator.Behaviors.PermissionBehavior`2")
            { item = ServiceDescriptor.Scoped<object>(_ => new object()); Replaced = true; }
            base.InsertItem(index, item);
        }
    }
    [Theory]
    [InlineData(null, true, false)]
    [InlineData(false, true, false)]
    [InlineData(true, true, false)]
    [InlineData(null, true, true)]
    [InlineData(false, true, true)]
    [InlineData(true, true, true)]
    [InlineData(true, false, false)]
    public async Task Real_Kun_dispatch_denies_before_handler_and_checks_every_permission(bool? allow, bool authenticated, bool capability)
    {
        var calls = new List<string>();
        var services = new ServiceCollection();
        services.AddSingleton(calls);
        services.AddSingleton<ICurrentUser>(new User(authenticated));
        services.AddSingleton<ITenantContext>(new Tenant());
        if (allow is not null) services.AddSingleton<IPermissionChecker>(new Checker(allow.Value, calls));
        services.AddKunMediator(builder =>
        {
            if (capability) builder.AddAuthorizedHandler<ProtectedCommand, int, Handler>(_ => -1,
                "qian.sample.crm:export", AuthzTestJson.Default.ProtectedCommand, AuthzTestJson.Default.Int32);
            else builder.AddAuthorizedHandler<ProtectedCommand, int, Handler>(_ => -1);
        });
        await using var provider = services.BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true, ValidateOnBuild = true });
        var result = await provider.GetRequiredService<ISender>().Send(new ProtectedCommand(), TestContext.Current.CancellationToken);
        Assert.Equal(allow == true && authenticated ? 42 : -1, result);
        if (allow == true && authenticated) Assert.Equal([.. ProtectedCommand.RequiredPermissions, "handler"], calls);
        else Assert.DoesNotContain("handler", calls);
        if (capability) Assert.NotNull(provider.GetRequiredService<ICapabilityRegistry>().Find("qian.sample.crm:export"));
    }

    [Fact]
    public void Reflection_registration_that_bypasses_QIAN0005_is_refused_before_any_handler_runs()
    {
        // QIAN0005 只在编译期拦截；反射注册由 Kun 2026.10.13 起的 Mediator 构建期兜底拒绝（S1-KUN-014，S1-DEC-019 R66）。
        var calls = new List<string>();
        var services = new ServiceCollection();
        services.AddSingleton(calls);
        services.AddSingleton<ICurrentUser>(new User(true));
        services.AddSingleton<ITenantContext>(new Tenant());
        services.AddKunMediator(builder => typeof(KunMediatorBuilder).GetMethods()
            .Single(method => method.Name == nameof(KunMediatorBuilder.AddRequestHandler) && method.GetParameters().Length == 0)
            .MakeGenericMethod(typeof(ProtectedCommand), typeof(int), typeof(Handler))
            .Invoke(builder, null));
        using var provider = services.BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true, ValidateOnBuild = true });

        var error = Assert.Throws<InvalidOperationException>(() => provider.GetRequiredService<ISender>());

        Assert.Contains(typeof(ProtectedCommand).FullName!, error.Message, StringComparison.Ordinal);
        Assert.Empty(calls);
    }

    [Fact]
    public void Invalid_or_empty_permissions_fail_during_registration()
    {
        var builder = new KunMediatorBuilder(new ServiceCollection());
        Assert.Throws<ArgumentException>(() => builder.AddAuthorizedHandler<EmptyCommand, int, EmptyHandler>(_ => -1));
        Assert.Throws<ArgumentException>(() => builder.AddAuthorizedHandler<InvalidCommand, int, InvalidHandler>(_ => -1));
    }

    [Fact]
    public async Task Authz_service_key_uses_real_scope_injection_and_unmount()
    {
        using var services = new ServiceCollection().BuildServiceProvider();
        await using var loader = new PluginLoader(services);
        var checker = new Checker(true, []);
        await loader.MountAsync([new PluginDefinition(
            new PluginEntry("authz", "qian.authz", "0.1.0", ScopeKey.Global, 0, JsonSerializer.SerializeToElement(new { })),
            new Provider(checker), [AuthzDefinition.Key])], TestContext.Current.CancellationToken);
        var context = loader.GetContext("authz", ScopeKey.Global);
        Assert.Same(checker, context.Authz());
        await loader.UnmountAsync("authz", ScopeKey.Global, TestContext.Current.CancellationToken);
        Assert.Throws<ObjectDisposedException>(() => context.Authz());
        await loader.MountAsync([new PluginDefinition(
            new PluginEntry("empty", "qian.empty", "0.1.0", ScopeKey.Global, 0, JsonSerializer.SerializeToElement(new { })),
            new Provider(null), [])], TestContext.Current.CancellationToken);
        Assert.Throws<ServiceNotReadyException>(() => loader.GetContext("empty", ScopeKey.Global).Authz());
    }

    private sealed class Provider(IAuthzService? service) : IQianPlugin
    {
        public IReadOnlyList<ServiceKey> Inject => [];
        public ValueTask ApplyAsync(IPluginContext ctx, JsonElement config, CancellationToken ct)
        { if (service is not null) ctx.Provide(AuthzDefinition.Key, service); return ValueTask.CompletedTask; }
    }
    private sealed class User(bool authenticated) : ICurrentUser
    {
        public string? UserId => authenticated ? "user-1" : null;
        public IReadOnlyList<string> Roles => [];
        public bool IsAuthenticated => authenticated;
    }
    private sealed class Tenant : ITenantContext
    {
        public string? TenantId => "tenant-a";
        public bool IsMultiTenant => true;
    }
    private sealed class Checker(bool allow, List<string> calls) : IAuthzService
    {
        public IDataPermissionFilter DataPermissions => throw new NotSupportedException();
        public IFieldPolicy Fields => throw new NotSupportedException();
        public ValueTask<IReadOnlyList<EffectiveGrant>> ListEffectivePermissionsAsync(AuthorizationSubject subject, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public ValueTask<PermissionDecision> CheckAsync(PermissionRequest request, CancellationToken cancellationToken = default)
        {
            Assert.Equal("tenant-a", request.Subject.TenantId);
            Assert.Equal("user-1", request.Subject.Id);
            calls.Add(request.Permission);
            return ValueTask.FromResult(new PermissionDecision(allow, "fixture", []));
        }
        public ValueTask<IReadOnlyList<PermissionDecision>> BatchCheckAsync(IReadOnlyList<PermissionRequest> requests, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public ValueTask<IReadOnlyList<string>> ListAllowedActionsAsync(AuthorizationSubject subject, string resourceType, string? resourceId = null, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public ValueTask<IReadOnlyList<string>> ListPermissionsAsync(AuthorizationSubject subject, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public ValueTask<PermissionDecision> ExplainAsync(PermissionRequest request, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public ValueTask<AuthorizationExplanation> ExplainDetailedAsync(PermissionRequest request, CancellationToken cancellationToken = default) => throw new NotSupportedException();
    }
}

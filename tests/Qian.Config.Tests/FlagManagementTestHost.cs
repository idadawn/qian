using System.Data.Common;
using System.Text;
using System.Text.Json;
using Kun.Authorization;
using Kun.Core.MultiTenancy;
using Kun.MultiTenancy;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using Qian.Audit;
using Qian.Authz.Abstractions;
using Qian.Config.Apollo;
using Qian.Data.Native;
using Qian.Data.Native.Postgres;
using Qian.Identity.Abstractions;
using Qian.Plugin.Sdk;
using Qian.Runtime;
using Qian.Testing.Containers;

namespace Qian.Config.Tests;

internal sealed class FlagManagementTestHost : IAsyncDisposable
{
    internal const string TokenReference = "secretRef://platform/apollo/flags/editor";
    internal const string ReceiptBinding = "flags-receipts";
    internal ServiceProvider Services { get; }
    internal PluginLoader Loader { get; }
    internal MutableUser User { get; } = new();
    internal RecordingAuthz Authz { get; } = new();
    internal RecordingAudit Audit { get; } = new();
    internal RotatingSecrets Secrets { get; }
    internal IPluginContext Context => Loader.GetContext("flags", ScopeKey.Global);
    internal IFeatureFlagDrafts Drafts => Context.Inject<IFeatureFlagDrafts>(FeatureFlagManagement.Key);
    internal IExecutableCapability Capability => Context.Inject<IExecutableCapability>(CapabilityBindingRegistration.Key, FeatureFlagManagement.SetCapabilityId);
    internal string PlatformConnection { get; }
    internal string Role { get; }
    private readonly IConfigurationRoot _root;

    private FlagManagementTestHost(Uri portal, string appId, string token, string platformConnection, string role, IConfigurationRoot? root, Action<IServiceCollection>? configure)
    {
        Role = role; PlatformConnection = platformConnection;
        _root = root ?? new ConfigurationBuilder().AddInMemoryCollection().Build();
        Secrets = new RotatingSecrets(token);
        var guard = FeatureFlagDraftStorage.ExpectedGuard(role);
        var services = new ServiceCollection().AddLogging().AddQianConfiguration(_root);
        services.AddSingleton<ICurrentUser>(User).AddSingleton<ISecretResolverFactory>(Secrets).AddSingleton<ITenantContext>(new AsyncLocalTenantContext());
        services.AddQianNativeData(new(ReceiptBinding, role, NativeDataAccess.Platform, _ => ValueTask.FromResult(platformConnection),
            [new(FeatureFlagDraftStorage.Schema, FeatureFlagDraftStorage.Table, FeatureFlagDraftStorage.GuardPolicy, guard, guard)]));
        services.AddQianFeatureFlagManagement(new Dictionary<string, ApolloFlagManagementBinding>
        { ["primary"] = new(portal, appId, "UAT", "default", "apollo", TokenReference, ReceiptBinding) });
        configure?.Invoke(services);
        Services = services.BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true });
        Loader = new PluginLoader(Services);
    }
    internal static async Task<FlagManagementTestHost> Create(PostgresFixture postgres, Uri portal, string appId, string token,
        CancellationToken ct, IConfigurationRoot? root = null, string? reuseConnection = null, string? reuseRole = null, Action<IServiceCollection>? configure = null)
    {
        var role = reuseRole ?? "flags_" + Guid.NewGuid().ToString("N"); var password = Guid.NewGuid().ToString("N");
        if (reuseRole is null)
        {
            await using var admin = new NpgsqlConnection(postgres.ConnectionString); await admin.OpenAsync(ct);
            await using (var command = new NpgsqlCommand($"CREATE ROLE {role} LOGIN NOINHERIT NOSUPERUSER NOBYPASSRLS NOCREATEDB NOCREATEROLE NOREPLICATION PASSWORD '{password}';", admin))
                await command.ExecuteNonQueryAsync(ct);
            await FeatureFlagDraftStorage.MigrateAsync(admin, role, ct);
        }
        var connection = reuseConnection ?? new NpgsqlConnectionStringBuilder(postgres.ConnectionString) { Username = role, Password = password }.ConnectionString;
        var result = new FlagManagementTestHost(portal, appId, token, connection, role, root, configure);
        try
        {
            await result.Loader.MountAsync([
                Define("config", "qian.config", new ConfigurationPlugin(), [ConfigDefinition.Key], "{}"),
                Define("data", "qian.data.native", new NativeDataComponent(), [NativeDataDefinition.Key], "{\"connections\":[\"flags-receipts\"]}"),
                Define("authz", "test.authz", new Ports(result.Authz, result.Audit), [AuthzEnforcement.Key, AuditDefinition.Key], "{}")], ct);
            Assert.True(result.Loader.GetState("data", ScopeKey.Global) == PluginState.Ready, result.Loader.GetFailure("data", ScopeKey.Global)?.ToString());
            await result.MountFlags(ct);
            return result;
        }
        catch { await result.DisposeAsync(); throw; }
    }
    internal async ValueTask MountFlags(CancellationToken ct)
    {
        await Loader.MountAsync([Define("flags", "qian.config.flags", new FeatureFlagManagementPlugin(), [FeatureFlagManagement.Key], "{\"bindingId\":\"primary\"}")], ct);
        Assert.True(Loader.GetState("flags", ScopeKey.Global) == PluginState.Ready, Loader.GetFailure("flags", ScopeKey.Global)?.ToString());
    }
    private static PluginDefinition Define(string id, string plugin, IQianPlugin instance, IReadOnlyList<ServiceKey> keys, string config) =>
        new(new PluginEntry(id, plugin, "1.0.0", ScopeKey.Global, 0, JsonElement.Parse(config)), instance, keys);
    public async ValueTask DisposeAsync() { await Loader.DisposeAsync(); await Services.DisposeAsync(); (_root as IDisposable)?.Dispose(); }
    private sealed class Ports(RecordingAuthz authz, RecordingAudit audit) : IQianPlugin
    {
        public IReadOnlyList<ServiceKey> Inject => [];
        public ValueTask ApplyAsync(IPluginContext ctx, JsonElement config, CancellationToken ct)
        { ctx.Provide<IAuthzEnforcement>(AuthzEnforcement.Key, authz); ctx.Provide<IAuditService>(AuditDefinition.Key, audit); return ValueTask.CompletedTask; }
    }
    internal sealed class MutableUser : ICurrentUser
    {
        internal Principal? Value { get; set; } = PlatformPrincipal();
        public Principal? Principal => Value;
        public string? UserId => Value?.Subject;
        public IReadOnlyList<string> Roles => Value?.Roles ?? [];
        public bool IsAuthenticated => Value is not null;
        internal static Principal PlatformPrincipal(string? tenant = null, PrincipalKind kind = PrincipalKind.User, string subject = "operator") => new()
        { Issuer = "fixture", Audience = ["flags"], Subject = subject, Kind = kind, TenantId = tenant, Roles = [], Scope = "", SessionId = "", TokenId = "", IssuedAt = 0, NotBefore = 0, ExpiresAt = long.MaxValue };
    }
    internal sealed class RecordingAuthz : IAuthzEnforcement
    {
        internal bool Allow { get; set; } = true;
        internal List<EnforcementRequest> Requests { get; } = [];
        public ValueTask<AuthorizationDecision> EnforceAsync(EnforcementRequest request, CancellationToken cancellationToken = default)
        {
            lock (Requests) Requests.Add(request);
            return ValueTask.FromResult(new AuthorizationDecision(Guid.CreateVersion7(), request,
                new AuthorizationExplanation(new PermissionDecision(Allow, Allow ? "allowed" : "denied", []), [], [], []), true, true, DateTimeOffset.UtcNow));
        }
        // 修订下限不是本组用例的对象：替身不单独比对（声明了权限点的端点在 EnforceAsync 里判定）。
        public ValueTask<AuthorizationDecision?> CheckIdentityRevisionAsync(EnforcementRequest request, CancellationToken cancellationToken = default) => ValueTask.FromResult<AuthorizationDecision?>(null);
    }
    internal sealed class RotatingSecrets(string token) : ISecretResolverFactory, ISecretResolver
    {
        internal string Token { get; set; } = token;
        internal int Reads { get; private set; }
        public ISecretResolver ForPlugin(IPluginContext context) { Assert.Equal("qian.config.flags", context.Entry.Plugin); return this; }
        public ISecretResolver ForHost(string policyId, ITenantContext? tenantContext = null) => throw new NotSupportedException();
        public ValueTask<SecretValue> ResolveAsync(string reference, CancellationToken cancellationToken = default)
        { Assert.Equal(TokenReference, reference); Reads++; return ValueTask.FromResult(new SecretValue(Encoding.UTF8.GetBytes(Token))); }
    }
    internal sealed class RecordingAudit : IAuditService
    {
        internal List<AuditRecord> Records { get; } = [];
        internal bool FailStart { get; set; }
        internal bool FailCompletion { get; set; }
        public Task<AuditChainEntry> AppendPlatformAsync(IPluginContext producer, AuditRecord record, CancellationToken cancellationToken = default)
        {
            if ((record.Result == "started" && FailStart) || (record.Result != "started" && FailCompletion)) throw new InvalidOperationException("fixture-audit-failure");
            lock (Records) { Records.Add(record); return Task.FromResult(new AuditChainEntry(Records.Count, null, new string('0', 64), record)); }
        }
        public Task<AuditChainEntry> AppendAsync(AuditRecord record, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<AuditChainEntry> AppendEnlistedAsync(AuditRecord record, DbTransaction transaction, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<AuditChainEntry> AppendPlatformEnlistedAsync(IPluginContext producer, AuditRecord record, DbTransaction transaction, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<IReadOnlyList<AuditChainEntry>> ReadAsync(long afterSequence, int take, string? onBehalfOf = null, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<IReadOnlyList<AuditChainEntry>> ReadPlatformAsync(IPluginContext producer, long afterSequence, int take, string? onBehalfOf = null, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<AuditChainVerification> VerifyAsync(CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<AuditChainVerification> VerifyPlatformAsync(IPluginContext producer, CancellationToken cancellationToken = default) => throw new NotSupportedException();
    }
}

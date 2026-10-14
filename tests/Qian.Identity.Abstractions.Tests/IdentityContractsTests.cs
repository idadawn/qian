using System.Reflection;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Xml.Linq;
using Json.Schema;
using Kun.Core.Domain;
using Kun.EventBus;
using Kun.EventBus.DependencyInjection;
using Microsoft.Extensions.DependencyInjection;
using Qian.Runtime;

namespace Qian.Identity.Abstractions.Tests;

public sealed class IdentityContractsTests
{
    private static readonly string Root = FindRoot();
    private static readonly Guid PersonId = Guid.Parse("aaaaaaaa-bbbb-4ccc-8ddd-eeeeeeeeeeee");
    private static readonly Guid SessionId = Guid.Parse("11111111-2222-4333-8444-555555555555");

    [Fact]
    public void 当前身份经正式Kun接口读取时保留主体类型_缺少快照不默认成用户()
    {
        using var fixtures = JsonDocument.Parse(File.ReadAllText(Path.Combine(Root, "contracts/v0/fixtures/s2s-claims.valid.json")));
        foreach (var fixture in fixtures.RootElement.EnumerateArray())
        {
            var data = fixture.GetProperty("data");
            var principal = data.Deserialize(IdentityJson.PrincipalTypeInfo)!;
            Kun.Core.Security.ICurrentUser current = new CurrentUserSnapshot(principal);
            Assert.Equal(data.GetProperty("principal_kind").GetString(), current.PrincipalKind);
        }
        Kun.Core.Security.ICurrentUser anonymous = new CurrentUserSnapshot(null);
        Assert.Equal(string.Empty, anonymous.PrincipalKind);
    }

    private sealed class CurrentUserSnapshot(Principal? principal) : ICurrentUser
    {
        public Principal? Principal { get; } = principal;
        public string? UserId => Principal?.Subject;
        public IReadOnlyList<string> Roles => Principal?.Roles ?? [];
        public bool IsAuthenticated => Principal is not null;
    }

    [Fact]
    public void S2sClaimNames_ExactlyMatchFrozenSchema_AndOidcAliasesStaySeparate()
    {
        using var schema = JsonDocument.Parse(File.ReadAllText(Path.Combine(Root, "contracts/v0/s2s-claims.schema.json")));
        var required = schema.RootElement.GetProperty("required").EnumerateArray().Select(e => e.GetString()!).Order().ToArray();
        var properties = schema.RootElement.GetProperty("properties").EnumerateObject().Select(e => e.Name).Order().ToArray();
        Assert.Equal(required, IdentityClaimNames.S2sRequired.Order());
        Assert.Equal(properties, IdentityClaimNames.S2sRequired.Concat(IdentityClaimNames.S2sOptional).Order());
        Assert.Contains("sid", IdentityClaimNames.IdentityToken);
        Assert.DoesNotContain("sid", properties);
        Assert.DoesNotContain("amr", properties);
        Assert.DoesNotContain("profile", properties);
        Assert.DoesNotContain("permissions", properties);
    }

    [Fact]
    public void EveryFrozenValidPrincipal_RoundTripsWithoutInventingClaims()
    {
        using var fixtures = JsonDocument.Parse(File.ReadAllText(Path.Combine(Root, "contracts/v0/fixtures/s2s-claims.valid.json")));
        var schema = JsonSchema.FromText(File.ReadAllText(Path.Combine(Root, "contracts/v0/s2s-claims.schema.json")));
        foreach (var fixture in fixtures.RootElement.EnumerateArray())
        {
            var payload = fixture.GetProperty("data");
            var principal = payload.Deserialize(IdentityJson.PrincipalTypeInfo)!;
            var json = JsonSerializer.Serialize(principal, IdentityJson.PrincipalTypeInfo);
            using var parsed = JsonDocument.Parse(json);
            Assert.True(schema.Evaluate(parsed.RootElement).IsValid, fixture.GetProperty("name").GetString());
            Assert.True(JsonNode.DeepEquals(JsonNode.Parse(payload.GetRawText()), JsonNode.Parse(json)));
            Assert.DoesNotContain(principal.SessionId, principal.ToString(), StringComparison.Ordinal);
        }
    }

    [Fact]
    public void ClientAgent_IsStableString_AndNumericPrincipalKindsAreRejected()
    {
        var client = new Client("agent-1", ClientType.Agent, [], [], [], null);
        using var document = JsonDocument.Parse(JsonSerializer.Serialize(client, IdentityJson.ClientTypeInfo));
        Assert.Equal("agent", document.RootElement.GetProperty("type").GetString());
        using var fixtures = JsonDocument.Parse(File.ReadAllText(Path.Combine(Root, "contracts/v0/fixtures/s2s-claims.valid.json")));
        var data = JsonNode.Parse(fixtures.RootElement[0].GetProperty("data").GetRawText())!;
        data["principal_kind"] = 1;
        Assert.Throws<JsonException>(() => JsonSerializer.Deserialize(data.ToJsonString(), IdentityJson.PrincipalTypeInfo));
    }

    [Fact]
    public void AllIdentityEvents_HaveStableAttributes_Schemas_AndSourceGeneratedRegistry()
    {
        var services = new ServiceCollection();
        services.AddKunEventBus(builder => builder.RegisterIdentityEvents());
        using var provider = services.BuildServiceProvider();
        var registry = provider.GetRequiredService<IEventTypeRegistry>();
        var schemas = new BuildOptions { SchemaRegistry = new SchemaRegistry() };
        _ = JsonSchema.FromText(File.ReadAllText(Path.Combine(Root, "contracts/v0/s2s-claims.schema.json")), schemas);
        IdentityIntegrationEvent[] events = [
            new IdentityLoginEvent(PersonId, 1, SessionId, "web", ["pwd"]),
            new IdentityLogoutEvent(PersonId, 2, SessionId, "user-request"),
            new IdentityPasswordChangedEvent(PersonId, 3),
            new IdentityLockedEvent(PersonId, 4, "policy"),
            new IdentitySessionRevokedEvent(PersonId, 5, SessionId, "password-changed")
        ];
        Assert.Equal(IdentityEventIds.All.Order(), registry.GetAll().Select(e => e.EventId).Order());
        // event_id、occurred_at 与 tenant_id 只在写入端生成的信封上（S1-DEC-020），载荷类型不再是 Kun 的 IIntegrationEvent。
        Assert.False(typeof(IIntegrationEvent).IsAssignableFrom(typeof(IdentityIntegrationEvent)));
        foreach (var value in events)
        {
            var type = value.GetType();
            var id = type.GetCustomAttribute<IntegrationEventIdAttribute>()!.EventType;
            Assert.Same(type, registry.Resolve(id));
            Assert.NotNull(registry.GetJsonTypeInfo(type));
            using var published = JsonDocument.Parse(IdentitySchemas.Event(id));
            var members = published.RootElement.GetProperty("properties").EnumerateObject().Select(member => member.Name).Order(StringComparer.Ordinal).ToArray();
            Assert.Equal(IdentityJson.EventTypeInfo(id)!.Properties.Select(property => property.Name).Order(StringComparer.Ordinal), members);
            Assert.DoesNotContain(members, member => member is "event_id" or "occurred_at" or "tenant_id");
            var schema = JsonSchema.FromText(IdentitySchemas.Event(id), schemas);
            using var payload = JsonDocument.Parse(JsonSerializer.Serialize(value, IdentityJson.EventTypeInfo(id)!));
            Assert.True(schema.Evaluate(payload.RootElement).IsValid, id + ": " + payload.RootElement.GetRawText());
            Assert.DoesNotContain(SessionId.ToString(), value.ToString(), StringComparison.Ordinal);
            Assert.False(payload.RootElement.TryGetProperty("password", out _));
            Assert.False(payload.RootElement.TryGetProperty("refresh_token", out _));
        }
    }

    [Fact]
    public void DefinitionReferencesOnlyRuntimeAndFrameworkEventPort()
    {
        var project = XDocument.Load(Path.Combine(Root, "packages/Qian.Identity.Abstractions/Qian.Identity.Abstractions.csproj"));
        Assert.Equal(["../Qian.Runtime/Qian.Runtime.csproj"], project.Descendants("ProjectReference").Select(e => (string)e.Attribute("Include")!).ToArray());
        Assert.Equal(["Kun.EventBus.Abstractions"], project.Descendants("PackageReference").Select(e => (string)e.Attribute("Include")!).ToArray());
        Assert.DoesNotContain(typeof(IIdentityService).Assembly.GetReferencedAssemblies(), assembly => assembly.Name == "Kun.EventBus" || assembly.Name!.Contains("Redis", StringComparison.Ordinal) || assembly.Name.Contains("OpenIddict", StringComparison.Ordinal) || assembly.Name.Contains("EntityFrameworkCore", StringComparison.Ordinal) || assembly.Name.Contains("Identity.Server", StringComparison.Ordinal));
    }

    [Fact]
    public void EventContracts_DoNotPullEventBusProviderOrRedisIntoDefinitionPackage()
    {
        using var assets = JsonDocument.Parse(File.ReadAllText(Path.Combine(Root,
            "packages/Qian.Identity.Abstractions/obj/project.assets.json")));
        var libraries = assets.RootElement.GetProperty("libraries").EnumerateObject()
            .Select(property => property.Name.Split('/')[0]).ToArray();
        Assert.Contains("Kun.EventBus.Abstractions", libraries);
        Assert.DoesNotContain("Kun.EventBus", libraries);
        Assert.DoesNotContain("StackExchange.Redis", libraries);
    }

    [Fact]
    public void SensitiveProof_IsNotPrintedAndIsClearedOnDispose()
    {
        using var proof = new AuthenticationProof(Encoding.UTF8.GetBytes("synthetic-test-proof"));
        var memory = proof.Data;
        Assert.DoesNotContain("synthetic", proof.ToString(), StringComparison.Ordinal);
        proof.Dispose();
        Assert.All(memory.ToArray(), value => Assert.Equal(0, value));
        Assert.Throws<ObjectDisposedException>(() => { _ = proof.Data; });
    }

    [Fact]
    public async Task IdentityAccessor_ResolvesTheMountedProviderInCurrentScope()
    {
        using var services = new ServiceCollection().BuildServiceProvider();
        await using var runtime = new PluginLoader(services);
        using var config = JsonDocument.Parse("{}");
        var provider = new FakeIdentity();
        var entry = new PluginEntry("identity", "qian.identity.client", "1.0.0", ScopeKey.Global, 0, config.RootElement.Clone());
        await runtime.MountAsync([new PluginDefinition(entry, new Provider(provider), [IdentityDefinition.Key])], TestContext.Current.CancellationToken);
        Assert.Same(provider, runtime.GetContext("identity", ScopeKey.Global).Identity());
    }

    private sealed class Provider(IIdentityService service) : IQianPlugin
    {
        public IReadOnlyList<ServiceKey> Inject => [];
        public ValueTask ApplyAsync(IPluginContext context, JsonElement config, CancellationToken ct)
        {
            context.Provide(IdentityDefinition.Key, service);
            return ValueTask.CompletedTask;
        }
    }
    private sealed class FakeIdentity : IIdentityService
    {
        public Principal? CurrentPrincipal => null;
        public IIdentityConnectorRegistry Connectors => throw new NotSupportedException();
        public Task<Person?> GetPersonAsync(Guid id, CancellationToken ct) => Task.FromResult<Person?>(null);
        public Task<Membership?> GetMembershipAsync(Guid id, CancellationToken ct) => Task.FromResult<Membership?>(null);
        public Task<IReadOnlyList<Session>> ListSessionsAsync(Guid id, CancellationToken ct) => Task.FromResult<IReadOnlyList<Session>>([]);
        public Task RevokeSessionsAsync(SessionRevocation request, CancellationToken ct) => Task.CompletedTask;
    }

    [Fact]
    public void RuntimeIdentityEvents_UseSpecifiedDispatchModes()
    {
        Assert.Equal("Identity", IdentityDefinition.Key.Name);
        Assert.Equal(DispatchMode.Waterfall, IdentityRuntimeEvents.SigningIn.Mode);
        Assert.Equal(DispatchMode.Emit, IdentityRuntimeEvents.SignedIn.Mode);
        Assert.Equal(DispatchMode.Emit, IdentityRuntimeEvents.SignedOut.Mode);
        Assert.Equal(DispatchMode.Parallel, IdentityRuntimeEvents.MemberDisabled.Mode);
    }

    private static string FindRoot()
    {
        for (var directory = new DirectoryInfo(AppContext.BaseDirectory); directory is not null; directory = directory.Parent)
            if (File.Exists(Path.Combine(directory.FullName, "Qian.slnx"))) return directory.FullName;
        throw new InvalidOperationException("找不到仓库根目录。");
    }
}

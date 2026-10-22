using System.Text.Json;
using System.Reflection;
using Microsoft.Extensions.Configuration;
using Qian.Config.Apollo;
using Qian.Plugin.Sdk;
using Microsoft.Extensions.DependencyInjection;
using Qian.Config;
using Qian.Identity.Abstractions;
using Qian.Runtime;
using Qian.Runtime.Errors;

namespace Qian.Identity.Connector.Oidc.Tests;

// 配置、清单与组件装配：凭据只接受 secretRef，清单合法且不含任何配置值，组件按 scope 注册、卸载即撤销。
public sealed class OidcComponentTests
{
    private static readonly string Root = FindRoot();
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private static JsonElement Config(object value) => JsonSerializer.SerializeToElement(value);

    private static readonly string[] ProfileOnly = ["profile"], UnknownTerminal = ["pc", "tv"];

    private static Dictionary<string, object?> Valid(string? secret = null, string issuer = FakeOpenIdProvider.Issuer, string? name = null, string? displayName = null) => new Dictionary<string, object?>
    {
        ["issuer"] = issuer, ["clientId"] = FakeOpenIdProvider.ClientId, ["clientSecret"] = secret ?? FakeOpenIdProvider.SecretRef, ["name"] = name, ["displayName"] = displayName
    }.Where(pair => pair.Value is not null).ToDictionary(StringComparer.Ordinal);

    private static IReadOnlyList<QianFieldError> Invalid(object config)
    {
        var error = Assert.Throws<QianErrorException>(() => OidcConnectorOptions.FromConfig(Config(config)));
        Assert.Equal(PlatformErrors.InputInvalid, error.Error.Code);
        return error.Error.Errors!;
    }

    [Theory]
    [InlineData("test-client-secret:with/&+=chars-0123456789")]
    [InlineData("vault://identity/oidc")]
    [InlineData("secretRef://platform/../etc/passwd")]
    [InlineData("secretRef://other/identity/oidc")]
    public void 客户端密钥只接受secretRef_错误里不回显输入(string secret)
    {
        var error = Assert.Throws<QianErrorException>(() => OidcConnectorOptions.FromConfig(Config(Valid(secret))));
        var field = Assert.Single(error.Error.Errors!);
        Assert.Equal((PlatformErrors.FormatInvalid, "#/clientSecret"), (field.Code, field.Pointer));
        Assert.DoesNotContain(secret, JsonSerializer.Serialize(error.Error), StringComparison.Ordinal);
        Assert.DoesNotContain(secret, error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void 合法配置补齐缺省值_打印不带配置()
    {
        var options = OidcConnectorOptions.FromConfig(Config(Valid("secretRef://tenant/identity/oidc")));
        Assert.Equal("oidc", options.Name);
        Assert.Equal("企业身份源", options.DisplayName);
        Assert.Equal(["openid", "profile", "email"], options.Scopes);
        Assert.Equal([ConnectorTerminals.Pc, ConnectorTerminals.Mobile], options.Terminals);
        Assert.True(options.RequireHttps);
        Assert.Equal("OidcConnectorOptions(redacted)", options.ToString());
    }

    [Fact]
    public void 必填_传输要求_scope与终端逐项报字段错误()
    {
        Assert.Contains(Invalid(new { clientId = "x", clientSecret = FakeOpenIdProvider.SecretRef }), e => e.Code == PlatformErrors.Required && e.Pointer == "#/issuer");
        Assert.Contains(Invalid(new { issuer = "https://idp.example.test", clientSecret = FakeOpenIdProvider.SecretRef }), e => e.Code == PlatformErrors.Required && e.Pointer == "#/clientId");
        Assert.Contains(Invalid(new { issuer = "https://idp.example.test", clientId = "x" }), e => e.Code == PlatformErrors.Required && e.Pointer == "#/clientSecret");
        // 明文 http 只在显式关闭 https 要求、且 issuer 是回环地址时接受。
        Assert.Contains(Invalid(Valid(issuer: "http://127.0.0.1:9000/application/o/qian/")), e => e.Pointer == "#/issuer");
        Assert.Contains(Invalid(new { issuer = "http://idp.example.test/", clientId = "x", clientSecret = FakeOpenIdProvider.SecretRef, requireHttps = false }), e => e.Pointer == "#/issuer");
        Assert.Contains(Invalid(Valid(issuer: "https://user:pass@idp.example.test/")), e => e.Pointer == "#/issuer");
        Assert.Equal("http://127.0.0.1:9000/application/o/qian/", OidcConnectorOptions.FromConfig(Config(new
        {
            issuer = "http://127.0.0.1:9000/application/o/qian/", clientId = "x", clientSecret = FakeOpenIdProvider.SecretRef, requireHttps = false
        })).Issuer);
        Assert.Contains(Invalid(new { issuer = FakeOpenIdProvider.Issuer, clientId = "x", clientSecret = FakeOpenIdProvider.SecretRef, scopes = ProfileOnly }),
            e => e.Code == PlatformErrors.ValueNotAllowed && e.Pointer == "#/scopes");
        Assert.Contains(Invalid(new { issuer = FakeOpenIdProvider.Issuer, clientId = "x", clientSecret = FakeOpenIdProvider.SecretRef, terminals = UnknownTerminal }),
            e => e.Code == PlatformErrors.ValueNotAllowed && e.Pointer == "#/terminals/1");
        Assert.Contains(Invalid(new { issuer = FakeOpenIdProvider.Issuer, clientId = "x", clientSecret = FakeOpenIdProvider.SecretRef, name = "Corp SSO" }),
            e => e.Code == PlatformErrors.PatternMismatch && e.Pointer == "#/name");
        Assert.Contains(Invalid(new { issuer = FakeOpenIdProvider.Issuer, clientId = "x", clientSecret = FakeOpenIdProvider.SecretRef, plainSecret = "x" }),
            e => e.Code == PlatformErrors.UnknownProperty && e.Pointer == "#/plainSecret");
        Assert.Contains(Invalid(new { issuer = FakeOpenIdProvider.Issuer, clientId = "x", clientSecret = 42 }), e => e.Code == PlatformErrors.TypeMismatch && e.Pointer == "#/clientSecret");
    }

    [Fact]
    public void 清单合法_配置Schema就是组件文件夹那一份_描述不含配置值()
    {
        using var handler = new HttpClientHandler();
        using var connector = new OidcIdentityConnector(OidcConnectorOptions.FromConfig(Config(Valid(displayName: "集团统一身份"))), new FakeSecrets(), handler);
        var descriptor = connector.Descriptor;
        Assert.Empty(ConnectorManifest.Check(descriptor));
        Assert.Equal(("oidc", "集团统一身份", "oidc"), (descriptor.Id, descriptor.DisplayName, descriptor.Protocol));
        Assert.Equal(["pc", "mobile"], descriptor.Terminals);
        Assert.Equal(ConnectorManifest.CallbackTemplate, descriptor.CallbackTemplate);
        using var file = JsonDocument.Parse(File.ReadAllText(Path.Combine(Root, "plugins/qian.identity.connector.oidc/schemas/config.schema.json")));
        var distributed = System.Text.Json.Nodes.JsonNode.Parse(file.RootElement.GetRawText())!.AsObject();
        Assert.Equal("oidc", distributed["properties"]!["name"]!["const"]!.GetValue<string>());
        distributed["properties"]!["name"]!.AsObject().Remove("const");
        distributed.Remove("x-qian-generated");
        using var adapter = JsonDocument.Parse(distributed.ToJsonString());
        Assert.True(JsonElement.DeepEquals(adapter.RootElement, descriptor.ConfigSchema));
        var secret = descriptor.ConfigSchema.GetProperty("properties").GetProperty("clientSecret");
        Assert.True(secret.GetProperty(ConnectorManifest.SensitiveKeyword).GetBoolean());
        // 清单只描述配置结构，任何配置值（issuer、客户端标识、secretRef、密钥）都不进入描述。
        var serialized = JsonSerializer.Serialize(descriptor);
        foreach (var value in (string[])[FakeOpenIdProvider.Issuer, FakeOpenIdProvider.ClientId, FakeOpenIdProvider.SecretRef, FakeOpenIdProvider.ClientSecret])
            Assert.DoesNotContain(value, serialized, StringComparison.Ordinal);
    }

    [Fact]
    public async Task 组件按scope注册_租户项遮蔽全局项_卸载即撤销()
    {
        await using var services = new ServiceCollection().AddQianConfiguration(new ConfigurationBuilder().Build()).BuildServiceProvider();
        using var manifest = JsonDocument.Parse(File.ReadAllText(Path.Combine(Root, "plugins/qian.identity.connector.oidc/.qian-plugin/plugin.json")));
        var code = ((IComponentCodeMetadataFactory)Activator.CreateInstance(typeof(OidcConnectorComponent).Assembly.GetCustomAttribute<QianCodeMetadataAttribute>()!.FactoryType)!).Create();
        var entry = Type.GetType(manifest.RootElement.GetProperty("runtime").GetProperty("entry").GetString()!, throwOnError: true)!;
        Assert.Equal(code.Provides, manifest.RootElement.GetProperty("provides").EnumerateArray().Select(item => item.GetString()!));
        Assert.Equal(code.RuntimeEntry, manifest.RootElement.GetProperty("runtime").GetProperty("entry").GetString());
        await using var runtime = new PluginLoader(services);
        using var op = new FakeOpenIdProvider(TimeProvider.System);
        var secrets = new FakeSecrets();
        var tenant = new ScopeKey("tenant", Guid.CreateVersion7().ToString("D"));
        Assert.Equal([IdentityDefinition.Key, ConfigDefinition.Key], new OidcConnectorPlugin().Inject);
        Assert.Equal([IdentityDefinition.Key], new OidcConnectorPlugin(secrets).Inject);
        await runtime.MountAsync([
            new(new PluginEntry("config", "qian.config", "1.0.0", ScopeKey.Global, 0, Config(new { })), new ConfigurationPlugin(), [ConfigDefinition.Key]),
            new(new PluginEntry("identity", "qian.identity.server", "1.0.0", ScopeKey.Global, 0, Config(new { })), new StubIdentity(), [IdentityDefinition.Key]),
            new(new PluginEntry("oidc", "qian.identity.connector.oidc", "1.0.0", ScopeKey.Global, 1, Config(Valid())), (IQianPlugin)Activator.CreateInstance(entry)!,
                code.Provides.Select(name => new ServiceKey(name)).ToArray()),
            new(new PluginEntry("oidc", "qian.identity.connector.oidc", "1.0.0", tenant, 1, Config(Valid(displayName: "租户自己的 IdP"))), (IQianPlugin)Activator.CreateInstance(entry)!,
                code.Provides.Select(name => new ServiceKey(name)).ToArray()),
            new(new PluginEntry("bad", "qian.identity.connector.oidc", "1.0.0", ScopeKey.Global, 1, Config(Valid("plain-text-secret", name: "bad"))), new OidcConnectorPlugin(secrets, op),
                [new ServiceKey("Identity.Connectors:bad")])], Ct);
        await runtime.WhenIdleAsync(Ct);
        var global = runtime.GetContext("identity", ScopeKey.Global);
        Assert.Equal("企业身份源", Assert.Single(global.IdentityConnectors().List()).DisplayName);
        Assert.Equal("租户自己的 IdP", global.Scoped(tenant).IdentityConnectors().Find("oidc")!.Descriptor.DisplayName);
        Assert.Equal(PluginState.Failed, runtime.GetState("bad", ScopeKey.Global));
        var failure = Assert.IsType<QianErrorException>(runtime.GetFailure("bad", ScopeKey.Global));
        Assert.Contains(failure.Error.Errors!, e => e.Pointer == "#/clientSecret");
        Assert.Null(global.IdentityConnectors().Find("bad"));

        await runtime.UnmountAsync("oidc", tenant, Ct);
        Assert.Equal("企业身份源", global.Scoped(tenant).IdentityConnectors().Find("oidc")!.Descriptor.DisplayName);
        await runtime.UnmountAsync("oidc", ScopeKey.Global, Ct);
        Assert.Null(global.IdentityConnectors().Find("oidc"));
        Assert.Empty(global.Scoped(tenant).IdentityConnectors().List());
        await runtime.MountAsync([new(new PluginEntry("renamed", "qian.identity.connector.oidc", "1.0.0", ScopeKey.Global, 1, Config(Valid(name: "corporate"))),
            (IQianPlugin)Activator.CreateInstance(entry)!, code.Provides.Select(name => new ServiceKey(name)).ToArray())], Ct);
        Assert.Equal(PluginState.Failed, runtime.GetState("renamed", ScopeKey.Global));
        Assert.Contains(Assert.IsType<QianErrorException>(runtime.GetFailure("renamed", ScopeKey.Global)).Error.Errors!, error => error.Pointer == "#/name");
        Assert.Empty(global.IdentityConnectors().List());
    }

    private sealed class StubIdentity : IQianPlugin, IIdentityService
    {
        public IReadOnlyList<ServiceKey> Inject => [];
        public ValueTask ApplyAsync(IPluginContext ctx, JsonElement config, CancellationToken ct)
        {
            ctx.Provide<IIdentityService>(IdentityDefinition.Key, this);
            return ValueTask.CompletedTask;
        }
        public Principal? CurrentPrincipal => null;
        public IIdentityConnectorRegistry Connectors => throw new NotSupportedException();
        public Task<Person?> GetPersonAsync(Guid personId, CancellationToken cancellationToken = default) => Task.FromResult<Person?>(null);
        public Task<Membership?> GetMembershipAsync(Guid membershipId, CancellationToken cancellationToken = default) => Task.FromResult<Membership?>(null);
        public Task<IReadOnlyList<Session>> ListSessionsAsync(Guid personId, CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyList<Session>>([]);
        public Task RevokeSessionsAsync(SessionRevocation request, CancellationToken cancellationToken = default) => Task.CompletedTask;
    }

    private static string FindRoot()
    {
        for (var directory = new DirectoryInfo(AppContext.BaseDirectory); directory is not null; directory = directory.Parent)
            if (File.Exists(Path.Combine(directory.FullName, "Qian.slnx"))) return directory.FullName;
        throw new InvalidOperationException("找不到仓库根目录。");
    }
}

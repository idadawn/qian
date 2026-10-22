using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using Qian.Runtime;
using Qian.Runtime.Errors;

namespace Qian.Identity.Abstractions.Tests;

// 连接器清单校验与注册表 seam：清单不合法注册即失败；注册名必须等于清单 id；租户 scope 的同名注册遮蔽全局项，
// 被 restriction 过滤的与不存在的查起来完全一样。
public sealed class ConnectorManifestTests
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private static JsonElement Schema(string json) => JsonDocument.Parse(json).RootElement.Clone();

    private static readonly JsonElement GoodSchema = Schema("""
        {"type":"object","additionalProperties":false,"properties":{
          "issuer":{"type":"string"},
          "clientSecret":{"type":"string","x-qian-sensitive":true,"pattern":"^secretRef://(platform|tenant)/.+$"},
          "advanced":{"type":"object","additionalProperties":false,"properties":{"apiKey":{"type":"string","x-qian-sensitive":true,"pattern":"^secretRef://platform/.+$"}}}}}
        """);

    private static ConnectorDescriptor Descriptor(string id = "corp-sso", string displayName = "企业身份源", string protocol = "oidc", string[]? terminals = null, JsonElement? schema = null) =>
        new(id, displayName, protocol, terminals ?? [ConnectorTerminals.Pc, ConnectorTerminals.Mobile], schema ?? GoodSchema);

    private static string[] Pointers(ConnectorDescriptor descriptor) => [.. ConnectorManifest.Check(descriptor).Select(error => error.Pointer!)];

    [Fact]
    public void 合法清单无错_回调模板固定由身份服务托管()
    {
        Assert.Empty(ConnectorManifest.Check(Descriptor()));
        Assert.Equal("/connect/callback/{connectorId}", Descriptor().CallbackTemplate);
        Assert.Equal("/connect/callback/corp-sso", ConnectorManifest.CallbackPath("corp-sso"));
        Assert.Throws<ArgumentException>(() => ConnectorManifest.CallbackPath("../admin"));
        Assert.Equal(["pc", "mobile", "workbench"], ConnectorTerminals.All);
    }

    [Theory]
    [InlineData("Corp")]
    [InlineData("1oidc")]
    [InlineData("oidc/x")]
    [InlineData("")]
    public void 标识必须是小写短名(string id)
    {
        Assert.Equal(["#/id"], Pointers(Descriptor(id: id)));
        Assert.False(ConnectorManifest.IsValidId(id));
    }

    [Fact]
    public void 名称_协议_终端与回调模板逐项报错()
    {
        Assert.Equal(["#/displayName"], Pointers(Descriptor(displayName: " ")));
        Assert.Equal(["#/displayName"], Pointers(Descriptor(displayName: "企业\n身份")));
        Assert.Equal(["#/protocol"], Pointers(Descriptor(protocol: "OIDC 1.0")));
        Assert.Equal(["#/terminals"], Pointers(Descriptor(terminals: [])));
        Assert.Equal(["#/terminals/1", "#/terminals/2"], Pointers(Descriptor(terminals: ["pc", "tv", "pc"])));
        Assert.Equal(["#/callbackTemplate"], Pointers(Descriptor() with { CallbackTemplate = "/oidc/callback" }));
    }

    [Fact]
    public void 配置Schema必须封闭_密钥类字段必须标敏感且只能填secretRef()
    {
        Assert.Equal(["#/configSchema"], Pointers(Descriptor(schema: Schema("""{"type":"array"}"""))));
        Assert.Equal(["#/configSchema/additionalProperties"], Pointers(Descriptor(schema: Schema("""{"type":"object","properties":{}}"""))));
        // 名字像密钥却没标敏感（含嵌套 object、以及带 / 的属性名的转义）。
        Assert.Equal(["#/configSchema/properties/client_secret/x-qian-sensitive"],
            Pointers(Descriptor(schema: Schema("""{"type":"object","additionalProperties":false,"properties":{"client_secret":{"type":"string"}}}"""))));
        Assert.Equal(["#/configSchema/properties/nested/properties/db~1Password/x-qian-sensitive"],
            Pointers(Descriptor(schema: Schema("""{"type":"object","additionalProperties":false,"properties":{"nested":{"type":"object","additionalProperties":false,"properties":{"db/Password":{"type":"string"}}}}}"""))));
        // 敏感字段只能是字符串，且 pattern 限定为 secretRef。
        Assert.Equal(["#/configSchema/properties/token/type"],
            Pointers(Descriptor(schema: Schema("""{"type":"object","additionalProperties":false,"properties":{"token":{"type":"object","additionalProperties":false,"x-qian-sensitive":true,"pattern":"^secretRef://x"}}}"""))));
        Assert.Equal(["#/configSchema/properties/apiKey/pattern"],
            Pointers(Descriptor(schema: Schema("""{"type":"object","additionalProperties":false,"properties":{"apiKey":{"type":"string","x-qian-sensitive":true}}}"""))));
        Assert.Equal(["#/configSchema/properties/privateKey/pattern"],
            Pointers(Descriptor(schema: Schema("""{"type":"object","additionalProperties":false,"properties":{"privateKey":{"type":"string","x-qian-sensitive":true,"pattern":".*"}}}"""))));
        var error = Assert.Throws<QianErrorException>(() => ConnectorManifest.Validate(Descriptor(id: "Bad", schema: Schema("""{"type":"object"}"""))));
        Assert.Equal(PlatformErrors.InputInvalid, error.Error.Code);
        Assert.Equal(["#/id", "#/configSchema/additionalProperties"], error.Error.Errors!.Select(e => e.Pointer));
    }

    [Fact]
    public async Task 注册表_注册名须等于清单id_租户项遮蔽全局项_过滤与不存在不可区分()
    {
        await using var services = new ServiceCollection().BuildServiceProvider();
        await using var runtime = new PluginLoader(services);
        var tenantA = new ScopeKey("tenant", Guid.CreateVersion7().ToString("D"));
        var tenantB = new ScopeKey("tenant", Guid.CreateVersion7().ToString("D"));
        await runtime.MountAsync([
            new(new PluginEntry("global", "test.connectors", "1.0.0", ScopeKey.Global, 0, Schema("{}")), new Registers(new Fake("oidc", "全局"), new Fake("wecom", "企业微信")),
                [new ServiceKey("Identity.Connectors:oidc"), new ServiceKey("Identity.Connectors:wecom")]),
            new(new PluginEntry("tenant-a", "test.connectors", "1.0.0", tenantA, 0, Schema("{}")), new Registers(new Fake("oidc", "A 自己的 IdP")),
                [new ServiceKey("Identity.Connectors:oidc")])], Ct);
        var global = runtime.GetContext("global", ScopeKey.Global);
        Assert.Equal(["全局", "企业微信"], global.IdentityConnectors().List().Select(d => d.DisplayName));
        Assert.Equal(["A 自己的 IdP", "企业微信"], global.Scoped(tenantA).IdentityConnectors().List().Select(d => d.DisplayName));
        Assert.Equal("全局", global.Scoped(tenantB).IdentityConnectors().Find("oidc")!.Descriptor.DisplayName);

        // B 租户把企业微信过滤掉：Find 与 List 的表现和根本不存在的连接器完全一样。
        using (global.Restrict(tenantB, registration => registration.Name != "wecom"))
        {
            var view = global.Scoped(tenantB).IdentityConnectors();
            Assert.Null(view.Find("wecom"));
            Assert.Null(view.Find("dingtalk"));
            Assert.Equal(["oidc"], view.List().Select(d => d.Id));
            Assert.NotNull(global.Scoped(tenantA).IdentityConnectors().Find("wecom"));
        }
        Assert.NotNull(global.Scoped(tenantB).IdentityConnectors().Find("wecom"));
        // 非法名字直接查不到，不抛异常。
        Assert.Null(global.IdentityConnectors().Find("../oidc"));
        Assert.Null(global.IdentityConnectors().Find(""));

        var mismatch = await Mount(runtime, "mismatch", new Registers(("saml", new Fake("oidc-2", "x"))));
        Assert.Contains(mismatch.Error.Errors!, e => e.Pointer == "#/id");
        var invalid = await Mount(runtime, "invalid", new Registers(new Fake("corp", "x", Schema("""{"type":"object","additionalProperties":false,"properties":{"password":{"type":"string"}}}"""))));
        Assert.Contains(invalid.Error.Errors!, e => e.Pointer == "#/configSchema/properties/password/x-qian-sensitive");
        Assert.Null(global.IdentityConnectors().Find("corp"));
    }

    [Fact]
    public void 连接器错误码归身份组件_与契约fixture逐项一致()
    {
        // AUTH_* 由身份组件登记（owner qian.identity），不进平台内置目录 error-codes.json；契约 fixture 里的那份导出与代码逐项一致，防漂移。
        var codes = new[] { IdentityErrors.BindingConflict, IdentityErrors.BindingExpired, IdentityErrors.ConnectorDenied, IdentityErrors.ConnectorStateInvalid };
        Assert.All(codes, code => Assert.Contains(code, IdentityErrors.All));
        Assert.Equal([(409, "AUTH_BINDING_CONFLICT"), (422, "AUTH_BINDING_EXPIRED"), (422, "AUTH_CONNECTOR_DENIED"), (422, "AUTH_CONNECTOR_STATE_INVALID")],
            codes.Select(code => (code.Status ?? 0, code.Code)));
        Assert.All(codes, code => Assert.Equal((ErrorKind.Business, "qian.identity", false), (code.Kind, code.Owner, code.Retryable)));
        var catalog = new ErrorCatalog();
        foreach (var code in codes) catalog.Register(code);
        using var exported = JsonDocument.Parse(catalog.ExportJson());
        using var fixtures = JsonDocument.Parse(File.ReadAllText(Path.Combine(FindRoot(), "contracts/v0/fixtures/error-catalog.valid.json")));
        var fixture = fixtures.RootElement.EnumerateArray().Single(item => item.GetProperty("name").GetString()!.StartsWith("身份组件登记的连接器业务错误码", StringComparison.Ordinal));
        Assert.True(JsonElement.DeepEquals(exported.RootElement, fixture.GetProperty("data")), catalog.ExportJson());
        using var platform = JsonDocument.Parse(File.ReadAllText(Path.Combine(FindRoot(), "contracts/v0/error-codes.json")));
        Assert.DoesNotContain(platform.RootElement.GetProperty("codes").EnumerateArray(), entry => entry.GetProperty("code").GetString()!.StartsWith("AUTH_", StringComparison.Ordinal));
    }

    private static string FindRoot()
    {
        for (var directory = new DirectoryInfo(AppContext.BaseDirectory); directory is not null; directory = directory.Parent)
            if (File.Exists(Path.Combine(directory.FullName, "Qian.slnx"))) return directory.FullName;
        throw new InvalidOperationException("找不到仓库根目录。");
    }

    private static async Task<QianErrorException> Mount(PluginLoader runtime, string id, IQianPlugin plugin)
    {
        await runtime.MountAsync([new(new PluginEntry(id, "test.connectors", "1.0.0", ScopeKey.Global, 0, Schema("{}")), plugin, [])], Ct);
        Assert.Equal(PluginState.Failed, runtime.GetState(id, ScopeKey.Global));
        return Assert.IsType<QianErrorException>(runtime.GetFailure(id, ScopeKey.Global));
    }

    private sealed class Registers(params (string Name, IIdentityConnector Connector)[] connectors) : IQianPlugin
    {
        public Registers(params Fake[] connectors) : this([.. connectors.Select(c => (c.Descriptor.Id, (IIdentityConnector)c))]) { }
        public IReadOnlyList<ServiceKey> Inject => [];
        public ValueTask ApplyAsync(IPluginContext ctx, JsonElement config, CancellationToken ct)
        {
            foreach (var (name, connector) in connectors) ctx.IdentityConnectors().Register(name, connector);
            return ValueTask.CompletedTask;
        }
    }

    private sealed class Fake(string id, string displayName, JsonElement? schema = null) : IIdentityConnector
    {
        public ConnectorDescriptor Descriptor { get; } = new(id, displayName, "oidc", [ConnectorTerminals.Pc], schema ?? GoodSchema);
        public Task<ChallengeResult> BeginSignInAsync(ConnectorContext context, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<ExternalAssertion> CompleteSignInAsync(ConnectorCallback callback, CancellationToken cancellationToken = default) => throw new NotSupportedException();
    }
}

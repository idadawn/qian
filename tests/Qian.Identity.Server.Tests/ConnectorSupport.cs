using System.Collections.Concurrent;
using System.Net;
using System.Text.Json;
using Microsoft.AspNetCore.WebUtilities;
using Qian.Identity.Abstractions;
using Qian.Runtime;
using Qian.Runtime.Errors;

namespace Qian.Identity.Server.Tests;

// 连接器 TCK 的假连接器：模拟一个上游身份源。授权地址带 state / nonce / PKCE challenge；「用户在上游同意」由测试调用 Approve，
// 得到绑定了 nonce 与外部 subject 的一次性授权码；回调时按协议核对授权码与 nonce，记录调用次数（验证回调幂等）。
internal sealed class FakeConnector(string id = FakeConnector.DefaultId, string displayName = "测试身份源") : IIdentityConnector
{
    internal const string DefaultId = "fake-idp";
    internal const string Issuer = "https://idp.fake.test";
    private static readonly JsonElement Schema = JsonDocument.Parse("""
        {"type":"object","additionalProperties":false,"properties":{
          "issuer":{"type":"string"},"clientSecret":{"type":"string","x-qian-sensitive":true,"pattern":"^secretRef://(platform|tenant)/.+$"}}}
        """).RootElement.Clone();
    private readonly ConcurrentDictionary<string, (string Subject, string Nonce, JsonElement Profile)> _codes = new(StringComparer.Ordinal);
    private int _begun, _completed;

    public ConnectorDescriptor Descriptor { get; } = new(id, displayName, "oidc", [ConnectorTerminals.Pc, ConnectorTerminals.Mobile], Schema);

    /// <summary>CompleteSignInAsync 的调用次数。</summary>
    internal int Completed => Volatile.Read(ref _completed);
    internal int Begun => Volatile.Read(ref _begun);
    /// <summary>每次回调处理前的人为延迟，用来放大并发回调的竞争窗口。</summary>
    internal TimeSpan Delay { get; set; }
    /// <summary>最近一次发起时身份服务给的上下文（state、nonce、PKCE verifier、回调地址）。</summary>
    internal ConnectorContext? LastBegin { get; private set; }

    public Task<ChallengeResult> BeginSignInAsync(ConnectorContext context, CancellationToken cancellationToken = default)
    {
        Interlocked.Increment(ref _begun);
        LastBegin = context;
        var target = QueryHelpers.AddQueryString(Issuer + "/authorize", new Dictionary<string, string?>
        {
            ["client_id"] = "qian", ["redirect_uri"] = context.CallbackUri.AbsoluteUri, ["state"] = context.State, ["nonce"] = context.Nonce,
            ["code_challenge"] = context.CodeVerifier is null ? null : Microsoft.IdentityModel.Tokens.Base64UrlEncoder.Encode(System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.ASCII.GetBytes(context.CodeVerifier))),
            ["code_challenge_method"] = "S256"
        });
        return Task.FromResult(new ChallengeResult(new Uri(target), null));
    }

    /// <summary>上游用户同意：返回发往回调地址的参数（code 与原样回传的 state）。nonce 缺省取授权地址里的值，传入别的值即模拟被篡改的断言。</summary>
    internal Dictionary<string, string> Approve(string redirectUri, string subject, string? nonce = null, string name = "外部张三", string? email = "zhang.ext@corp.example")
    {
        var query = QueryHelpers.ParseQuery(new Uri(redirectUri).Query);
        var code = Guid.NewGuid().ToString("N");
        var profile = JsonSerializer.SerializeToElement(new Dictionary<string, object?> { ["name"] = name, ["email"] = email, ["preferred_username"] = "zhang.ext" });
        _codes[code] = (subject, nonce ?? query["nonce"].ToString(), profile);
        return new Dictionary<string, string>(StringComparer.Ordinal) { ["code"] = code, ["state"] = query["state"].ToString() };
    }

    public async Task<ExternalAssertion> CompleteSignInAsync(ConnectorCallback callback, CancellationToken cancellationToken = default)
    {
        Interlocked.Increment(ref _completed);
        if (Delay > TimeSpan.Zero) await Task.Delay(Delay, cancellationToken);
        if (callback.Context.CodeVerifier is not { Length: >= 43 }) throw new QianErrorException(new QianError(PlatformErrors.BadGateway));
        if (!callback.Parameters.TryGetValue("code", out var code) || !_codes.TryRemove(code, out var grant)) throw new QianErrorException(new QianError(PlatformErrors.BadGateway));
        if (grant.Nonce != callback.Context.Nonce) throw new QianErrorException(new QianError(IdentityErrors.ConnectorStateInvalid));
        return new ExternalAssertion(Issuer, grant.Subject, null, grant.Profile, ["pwd"]);
    }
}

/// <summary>只认一个 secretRef 的密钥解析替身；记录解析次数。</summary>
internal sealed class StaticSecrets(string reference, string value) : Qian.Config.ISecretResolver
{
    private int _resolved;
    internal string Reference => reference;
    internal int Resolved => Volatile.Read(ref _resolved);

    public ValueTask<Qian.Config.SecretValue> ResolveAsync(string requested, CancellationToken cancellationToken = default)
    {
        Interlocked.Increment(ref _resolved);
        if (requested != reference) throw new QianErrorException(new QianError(Qian.Config.SecretErrors.AccessDenied));
        return ValueTask.FromResult(new Qian.Config.SecretValue(System.Text.Encoding.UTF8.GetBytes(value)));
    }
}

/// <summary>把若干连接器登记到所在 scope 的 Identity.Connectors 注册表的组件。</summary>
internal sealed class ConnectorsPlugin(params IIdentityConnector[] connectors) : IQianPlugin
{
    public IReadOnlyList<ServiceKey> Inject => [IdentityDefinition.Key];

    public ValueTask ApplyAsync(IPluginContext ctx, JsonElement config, CancellationToken ct)
    {
        foreach (var connector in connectors) ctx.IdentityConnectors().Register(connector.Descriptor.Id, connector);
        return ValueTask.CompletedTask;
    }

    /// <summary>真实的通用 OIDC 连接器组件条目（注册名取配置的 name，缺省 oidc）。</summary>
    internal static PluginDefinition Oidc(string id, object config, Qian.Config.ISecretResolver secrets, HttpMessageHandler handler, string name = "oidc", ScopeKey? scope = null) =>
        new(new PluginEntry(id, "qian.identity.connector.oidc", "1.0.0", scope ?? ScopeKey.Global, 1, JsonSerializer.SerializeToElement(config)),
            new Qian.Identity.Connector.Oidc.OidcConnectorPlugin(secrets, handler), [new ServiceKey("Identity.Connectors:" + name)]);

    internal static PluginDefinition Define(string id, ScopeKey scope, params IIdentityConnector[] connectors) =>
        new(new PluginEntry(id, "test.connectors", "1.0.0", scope, 1, JsonSerializer.SerializeToElement(new { })), new ConnectorsPlugin(connectors),
            [.. connectors.Select(c => new ServiceKey("Identity.Connectors:" + c.Descriptor.Id))]);
}

/// <summary>浏览器视角的连接器登录步骤：发起、上游回调、界面落点读取流程、确认绑定。</summary>
internal static class ConnectorFlow
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    internal static ScopeKey TenantScope(Guid tenant) => new("tenant", tenant.ToString("D"));

    internal static async Task MountAsync(this AccountsHost.Running host, params PluginDefinition[] definitions)
    {
        await host.Loader.MountAsync(definitions, Ct);
        await host.Loader.WhenIdleAsync(Ct);
    }

    internal sealed record Started(Guid Flow, string RedirectUri);

    internal static async Task<Started> Begin(this AccountsHost.Browser browser, string tenant, string connector = FakeConnector.DefaultId, string? returnUrl = null, string client = "web")
    {
        var (status, body, _) = await browser.Json(HttpMethod.Post, $"api/identity/connectors/{connector}/begin", new { client_id = client, tenant, return_url = returnUrl });
        Assert.True(status == HttpStatusCode.OK, body.ToString());
        return new Started(body.GetProperty("flow").GetGuid(), body.GetProperty("redirect_uri").GetString()!);
    }

    /// <summary>浏览器带着上游给的参数回到统一回调；返回原始响应（302 落点或错误）。</summary>
    internal static Task<HttpResponseMessage> Callback(this AccountsHost.Browser browser, IReadOnlyDictionary<string, string> parameters, string connector = FakeConnector.DefaultId) =>
        browser.Http.GetAsync(QueryHelpers.AddQueryString($"connect/callback/{connector}", parameters.ToDictionary(p => p.Key, p => (string?)p.Value)), Ct);

    /// <summary>回调必须 302 回界面落点；返回落点里的流程号。</summary>
    internal static async Task<Guid> Landed(this AccountsHost.Browser browser, IReadOnlyDictionary<string, string> parameters, string connector = FakeConnector.DefaultId)
    {
        using var response = await browser.Callback(parameters, connector);
        Assert.True(response.StatusCode == HttpStatusCode.Redirect, await response.Content.ReadAsStringAsync(Ct));
        var location = response.Headers.Location!.OriginalString;
        Assert.StartsWith($"/login/connector/{connector}?flow=", location, StringComparison.Ordinal);
        return Guid.ParseExact(QueryHelpers.ParseQuery(location[location.IndexOf('?', StringComparison.Ordinal)..])["flow"].ToString(), "D");
    }

    /// <summary>浏览器跟随上游给出的完整回跳地址回到统一回调；必须 302 回界面落点，返回流程号。</summary>
    internal static async Task<Guid> Follow(this AccountsHost.Browser browser, Uri callback, string connector)
    {
        using var response = await browser.Http.GetAsync(callback, Ct);
        Assert.True(response.StatusCode == HttpStatusCode.Redirect, await response.Content.ReadAsStringAsync(Ct));
        var location = response.Headers.Location!.OriginalString;
        Assert.StartsWith($"/login/connector/{connector}?flow=", location, StringComparison.Ordinal);
        return Guid.ParseExact(QueryHelpers.ParseQuery(location[location.IndexOf('?', StringComparison.Ordinal)..])["flow"].ToString(), "D");
    }

    internal static async Task<JsonElement> View(this AccountsHost.Browser browser, Guid flow)
    {
        var (status, body, _) = await browser.Json(HttpMethod.Get, $"api/identity/connector-flows/{flow:D}");
        Assert.True(status == HttpStatusCode.OK, body.ToString());
        return body;
    }

    internal static Task<(HttpStatusCode Status, JsonElement Body, HttpResponseMessage Raw)> Verify(this AccountsHost.Browser browser, Guid flow, string identifier, string password) =>
        browser.Json(HttpMethod.Post, $"api/identity/connector-flows/{flow:D}/signin", new { identifier, password });

    internal static Task<(HttpStatusCode Status, JsonElement Body, HttpResponseMessage Raw)> Bind(this AccountsHost.Browser browser, Guid flow, string mode) =>
        browser.Json(HttpMethod.Post, $"api/identity/connector-flows/{flow:D}/bind", new { mode });

    /// <summary>发起 → 上游同意 → 回调，停在界面落点；返回流程号。</summary>
    internal static async Task<Guid> Arrive(this AccountsHost.Browser browser, FakeConnector connector, string tenant, string subject, string? returnUrl = null)
    {
        var started = await browser.Begin(tenant, connector.Descriptor.Id, returnUrl);
        return await browser.Landed(connector.Approve(started.RedirectUri, subject), connector.Descriptor.Id);
    }

    internal static string Code(JsonElement problem) => problem.GetProperty("code").GetString()!;

    /// <summary>库内（绕过 RLS）某租户、连接器、外部 subject 的有效绑定所指向的成员；没有绑定返回空串。</summary>
    internal static Task<string> Bound(this AccountsHost.Running host, Guid tenant, string connector, string subject) =>
        host.Text("SELECT coalesce(max(membership_id::text), '') FROM plugin_qian_identity.external_identities WHERE tenant_id = @t AND connector_id = @c AND subject = @s AND NOT is_deleted",
            ("t", tenant.ToString("D")), ("c", connector), ("s", subject));
}

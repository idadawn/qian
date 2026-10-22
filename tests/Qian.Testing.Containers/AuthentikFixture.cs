using System.Net;
using System.Net.Http.Headers;
using System.Text.Json;
using DotNet.Testcontainers.Builders;
using DotNet.Testcontainers.Containers;
using DotNet.Testcontainers.Networks;
using Testcontainers.PostgreSql;

namespace Qian.Testing.Containers;

/// <summary>
/// 真实 Authentik（server + worker + 专属 PostgreSQL），作为 OIDC 连接器的上游身份源（S1-IDP-007 验收 1）。
/// 启动后用引导令牌经 REST API 开 OAuth2 客户端与用户；<see cref="SignInAsync"/> 像浏览器一样走 Authentik 的登录流程（flow executor），
/// 返回 Authentik 回跳到依赖方回调地址的完整 URL。口令与令牌每次运行随机生成。
/// </summary>
public sealed class AuthentikFixture : ContainerFixture<IContainer>
{
    private const ushort Port = 9000;
    private static readonly string[] GrantTypes = ["authorization_code", "refresh_token"];
    private static readonly string[] RequiredFlows = ["default-provider-authorization-implicit-consent", "default-provider-invalidation-flow", "default-authentication-flow"];
    private static readonly string[] RequiredScopes = ["openid", "email", "profile"];
    private const string Database = "authentik";
    private INetwork? _network;
    private PostgreSqlContainer? _postgres;
    private IContainer? _worker;
    private HttpClient? _api;
    private readonly string _databasePassword = NewSecret();
    private readonly string _secretKey = NewSecret() + NewSecret();
    private readonly string _token = NewSecret() + NewSecret();

    /// <summary>Authentik 的根地址（宿主机可达的回环地址）。</summary>
    public Uri BaseUri { get; private set; } = new("http://localhost/");

    /// <summary>为一个依赖方开出的 OAuth2 客户端。</summary>
    public sealed record OAuthClient(string Issuer, string ClientId, string ClientSecret)
    {
        /// <inheritdoc />
        public override string ToString() => $"OAuthClient({ClientId})";
    }

    /// <summary>Authentik 里的一个本地用户。</summary>
    public sealed record User(string Username, string Password, string Email)
    {
        /// <inheritdoc />
        public override string ToString() => $"User({Username})";
    }

    /// <inheritdoc />
    protected override async ValueTask OnStartingAsync(CancellationToken cancellationToken)
    {
        _network = new NetworkBuilder().Build();
        await _network.CreateAsync(cancellationToken).ConfigureAwait(false);
        _postgres = new PostgreSqlBuilder(ContainerImages.Postgres)
            .WithNetwork(_network).WithNetworkAliases("authentik-db")
            .WithDatabase(Database).WithUsername("authentik").WithPassword(_databasePassword)
            .Build();
        await _postgres.StartAsync(cancellationToken).ConfigureAwait(false);
        // worker 负责应用内置蓝图（缺省登录流程、引导令牌等），与 server 并行启动。
        _worker = Authentik("worker").Build();
        await _worker.StartAsync(cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    protected override IContainer CreateContainer() =>
        Authentik("server").WithPortBinding(Port, assignRandomHostPort: true)
            .WithWaitStrategy(Wait.ForUnixContainer().UntilHttpRequestIsSucceeded(request => request.ForPort(Port).ForPath("/-/health/ready/"),
                wait => wait.WithTimeout(TimeSpan.FromMinutes(5))))
            .Build();

    private ContainerBuilder Authentik(string command) => new ContainerBuilder(ContainerImages.Authentik)
        .WithNetwork(_network!).WithCommand(command)
        .WithEnvironment("AUTHENTIK_SECRET_KEY", _secretKey)
        .WithEnvironment("AUTHENTIK_POSTGRESQL__HOST", "authentik-db").WithEnvironment("AUTHENTIK_POSTGRESQL__NAME", Database)
        .WithEnvironment("AUTHENTIK_POSTGRESQL__USER", "authentik").WithEnvironment("AUTHENTIK_POSTGRESQL__PASSWORD", _databasePassword)
        .WithEnvironment("AUTHENTIK_BOOTSTRAP_PASSWORD", NewSecret()).WithEnvironment("AUTHENTIK_BOOTSTRAP_TOKEN", _token)
        .WithEnvironment("AUTHENTIK_BOOTSTRAP_EMAIL", "admin@example.invalid")
        .WithEnvironment("AUTHENTIK_ERROR_REPORTING__ENABLED", "false").WithEnvironment("AUTHENTIK_DISABLE_UPDATE_CHECK", "true")
        .WithEnvironment("AUTHENTIK_DISABLE_STARTUP_ANALYTICS", "true");

    /// <inheritdoc />
    protected override async ValueTask OnStartedAsync(IContainer container, CancellationToken cancellationToken)
    {
        BaseUri = new UriBuilder("http", container.Hostname, container.GetMappedPublicPort(Port)).Uri;
        _api = new HttpClient(new HttpClientHandler { UseProxy = false }) { BaseAddress = BaseUri, Timeout = TimeSpan.FromSeconds(30) };
        _api.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", _token);
        _api.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        // server 就绪不代表蓝图已应用：等到引导令牌可用、授权与失效流程和 OAuth2 scope 映射都已存在。
        // 流程来自 default/flow-*.yaml，scope 映射来自 system/providers-oauth2.yaml，worker 分别应用、先后不定，只等流程会在慢机器上先放行（S1-QA-015）。
        var deadline = DateTimeOffset.UtcNow + TimeSpan.FromMinutes(3);
        var missing = "";
        while (true)
        {
            try
            {
                var flows = await FlowsAsync(cancellationToken).ConfigureAwait(false);
                var scopes = await ScopeMappingsAsync(cancellationToken).ConfigureAwait(false);
                missing = string.Join("、", RequiredFlows.Where(slug => !flows.ContainsKey(slug))
                    .Concat(RequiredScopes.Where(scope => !scopes.ContainsKey(scope)).Select(scope => "scope " + scope)));
                if (missing.Length == 0) return;
            }
            catch (HttpRequestException error) { missing = error.Message; }
            if (DateTimeOffset.UtcNow > deadline) throw new InvalidOperationException("Authentik 的内置蓝图在 3 分钟内没有就绪，仍缺：" + missing);
            await Task.Delay(TimeSpan.FromSeconds(2), cancellationToken).ConfigureAwait(false);
        }
    }

    /// <summary>开一个机密客户端（授权码 + refresh，RS256 签名，sub 为散列后的用户标识），只登记给定的回调地址（严格匹配）。</summary>
    public async Task<OAuthClient> CreateClientAsync(Uri redirectUri, CancellationToken cancellationToken = default)
    {
        var flows = await FlowsAsync(cancellationToken).ConfigureAwait(false);
        var key = (await GetAsync("api/v3/crypto/certificatekeypairs/?has_key=true", cancellationToken).ConfigureAwait(false)).GetProperty("results")[0].GetProperty("pk").GetString();
        var mappings = await ScopeMappingsAsync(cancellationToken).ConfigureAwait(false);
        var suffix = NewSecret()[..8];
        var (slug, clientId, secret) = ("qian-" + suffix, "qian-" + suffix, NewSecret() + NewSecret());
        var provider = await PostAsync("api/v3/providers/oauth2/", new
        {
            name = slug, authorization_flow = flows["default-provider-authorization-implicit-consent"], invalidation_flow = flows["default-provider-invalidation-flow"],
            client_type = "confidential", client_id = clientId, client_secret = secret, grant_types = GrantTypes,
            redirect_uris = new[] { new { matching_mode = "strict", url = redirectUri.AbsoluteUri } }, signing_key = key,
            property_mappings = RequiredScopes.Select(scope => mappings[scope]).ToArray(), sub_mode = "hashed_user_id"
        }, cancellationToken).ConfigureAwait(false);
        await PostAsync("api/v3/core/applications/", new { name = "Qian " + suffix, slug, provider = provider.GetProperty("pk").GetInt32() }, cancellationToken).ConfigureAwait(false);
        return new OAuthClient(new Uri(BaseUri, $"application/o/{slug}/").AbsoluteUri, clientId, secret);
    }

    /// <summary>建一个本地用户并设置口令。</summary>
    public async Task<User> CreateUserAsync(string name, CancellationToken cancellationToken = default)
    {
        var username = "user-" + NewSecret()[..8];
        var email = username + "@example.invalid";
        var user = await PostAsync("api/v3/core/users/", new { username, name, email, is_active = true, path = "users" }, cancellationToken).ConfigureAwait(false);
        var password = "Pw-" + NewSecret();
        await PostAsync($"api/v3/core/users/{user.GetProperty("pk").GetInt32()}/set_password/", new { password }, cancellationToken).ConfigureAwait(false);
        return new User(username, password, email);
    }

    /// <summary>
    /// 以一个全新的浏览器会话打开授权地址，经 flow executor 依次完成标识、口令与（隐式）同意阶段，
    /// 返回 Authentik 302 到回调地址的完整 URL（含 code、state 与 iss）。
    /// </summary>
    public async Task<Uri> SignInAsync(Uri authorization, User user, Uri callbackPrefix, CancellationToken cancellationToken = default)
    {
        var cookies = new CookieContainer();
        using var browser = new HttpClient(new HttpClientHandler { AllowAutoRedirect = false, UseCookies = true, CookieContainer = cookies, UseProxy = false })
            { Timeout = TimeSpan.FromSeconds(30) };
        browser.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        var next = authorization;
        for (var hop = 0; hop < 20; hop++)
        {
            // 回调地址交给调用方的浏览器去访问，这里不碰。
            if (next.AbsoluteUri.StartsWith(callbackPrefix.AbsoluteUri, StringComparison.Ordinal)) return next;
            using var response = await browser.GetAsync(next, cancellationToken).ConfigureAwait(false);
            var location = response.Headers.Location is { } target ? new Uri(next, target)
                : throw new InvalidOperationException($"Authentik 授权在 {next.AbsolutePath} 停下，状态 {(int)response.StatusCode}。");
            // 登录界面 /if/flow/<slug>/?…：改走同一流程的 executor API，查询串原样作为 query 参数。
            if (location.AbsolutePath.StartsWith("/if/flow/", StringComparison.Ordinal))
            {
                var slug = location.AbsolutePath["/if/flow/".Length..].Trim('/');
                var executor = new Uri(BaseUri, $"api/v3/flows/executor/{slug}/?query={Uri.EscapeDataString(location.Query.TrimStart('?'))}");
                next = await ExecuteFlowAsync(browser, cookies, executor, user, cancellationToken).ConfigureAwait(false);
                continue;
            }
            next = location;
        }
        throw new InvalidOperationException("Authentik 授权的跳转次数超出上限。");
    }

    // 逐个阶段提交，直到流程给出 xak-flow-redirect，或 executor 直接 302 到流程之外；返回下一跳地址。
    private async Task<Uri> ExecuteFlowAsync(HttpClient browser, CookieContainer cookies, Uri executor, User user, CancellationToken ct)
    {
        var (challenge, exit) = await StepAsync(browser, new HttpRequestMessage(HttpMethod.Get, executor), ct).ConfigureAwait(false);
        for (var stage = 0; stage < 10; stage++)
        {
            if (exit is not null) return exit;
            var component = challenge.GetProperty("component").GetString();
            if (component == "xak-flow-redirect") return new Uri(BaseUri, challenge.GetProperty("to").GetString()!);
            object answer = component switch
            {
                "ak-stage-identification" => new { component, uid_field = user.Username },
                "ak-stage-password" => new { component, password = user.Password },
                "ak-stage-consent" => new { component, token = challenge.GetProperty("token").GetString() },
                _ => throw new InvalidOperationException($"未预期的 Authentik 阶段：{component}，{challenge}")
            };
            var request = new HttpRequestMessage(HttpMethod.Post, executor) { Content = Json(answer) };
            // Authentik 的写请求要带 authentik_csrf Cookie 对应的头。
            if (cookies.GetCookies(BaseUri)["authentik_csrf"] is { } csrf) request.Headers.Add("X-authentik-CSRF", csrf.Value);
            (challenge, exit) = await StepAsync(browser, request, ct).ConfigureAwait(false);
        }
        throw new InvalidOperationException("Authentik 登录流程的阶段数超出上限。");
    }

    // 发出请求：302 回 executor 的跟着 GET 取下一个阶段；302 到别处表示流程结束，返回那个地址。
    private async Task<(JsonElement Challenge, Uri? Exit)> StepAsync(HttpClient browser, HttpRequestMessage request, CancellationToken ct)
    {
        var current = request;
        for (var hop = 0; hop < 5; hop++)
        {
            HttpResponseMessage response;
            try { response = await browser.SendAsync(current, ct).ConfigureAwait(false); }
            finally { current.Dispose(); }
            using (response)
            {
                if (response.StatusCode is HttpStatusCode.Redirect or HttpStatusCode.Found && response.Headers.Location is { } location)
                {
                    var target = new Uri(BaseUri, location);
                    if (!target.AbsolutePath.StartsWith("/api/v3/flows/executor/", StringComparison.Ordinal)) return (default, target);
                    current = new HttpRequestMessage(HttpMethod.Get, target);
                    continue;
                }
                var text = await response.Content.ReadAsStringAsync(ct).ConfigureAwait(false);
                if (!response.IsSuccessStatusCode) throw new InvalidOperationException($"Authentik 流程请求失败：{(int)response.StatusCode} {text[..Math.Min(text.Length, 500)]}");
                return (JsonDocument.Parse(text).RootElement.Clone(), null);
            }
        }
        throw new InvalidOperationException("Authentik 流程内部跳转次数超出上限。");
    }

    private async Task<Dictionary<string, string>> FlowsAsync(CancellationToken ct) =>
        (await GetAsync("api/v3/flows/instances/?page_size=100", ct).ConfigureAwait(false)).GetProperty("results").EnumerateArray()
            .ToDictionary(f => f.GetProperty("slug").GetString()!, f => f.GetProperty("pk").GetString()!, StringComparer.Ordinal);

    private async Task<Dictionary<string, string>> ScopeMappingsAsync(CancellationToken ct) =>
        (await GetAsync("api/v3/propertymappings/provider/scope/?page_size=100", ct).ConfigureAwait(false)).GetProperty("results").EnumerateArray()
            .ToDictionary(m => m.GetProperty("scope_name").GetString()!, m => m.GetProperty("pk").GetString()!, StringComparer.Ordinal);

    private async Task<JsonElement> GetAsync(string path, CancellationToken ct)
    {
        using var response = await Api.GetAsync(path, ct).ConfigureAwait(false);
        var text = await response.Content.ReadAsStringAsync(ct).ConfigureAwait(false);
        if (!response.IsSuccessStatusCode) throw new HttpRequestException($"Authentik API {path}：{(int)response.StatusCode} {text[..Math.Min(text.Length, 300)]}");
        return JsonDocument.Parse(text).RootElement.Clone();
    }

    private async Task<JsonElement> PostAsync(string path, object body, CancellationToken ct)
    {
        using var response = await Api.PostAsync(path, Json(body), ct).ConfigureAwait(false);
        var text = await response.Content.ReadAsStringAsync(ct).ConfigureAwait(false);
        if (!response.IsSuccessStatusCode) throw new InvalidOperationException($"Authentik API {path}：{(int)response.StatusCode} {text[..Math.Min(text.Length, 500)]}");
        return text.Length == 0 ? default : JsonDocument.Parse(text).RootElement.Clone();
    }

    private HttpClient Api => _api ?? throw new InvalidOperationException("Authentik 尚未启动。");

    // 先序列化成定长正文：Authentik 前置代理不接受分块传输的请求体（JsonContent 默认分块）。
    private static StringContent Json(object body) => new(JsonSerializer.Serialize(body), System.Text.Encoding.UTF8, "application/json");

    /// <inheritdoc />
    protected override async ValueTask OnDisposingAsync()
    {
        Interlocked.Exchange(ref _api, null)?.Dispose();
        var worker = Interlocked.Exchange(ref _worker, null);
        if (worker is not null) await worker.DisposeAsync().ConfigureAwait(false);
    }

    /// <inheritdoc />
    protected override async ValueTask OnDisposedAsync()
    {
        var postgres = Interlocked.Exchange(ref _postgres, null);
        try { if (postgres is not null) await postgres.DisposeAsync().ConfigureAwait(false); }
        finally
        {
            var network = Interlocked.Exchange(ref _network, null);
            if (network is not null) await network.DisposeAsync().ConfigureAwait(false);
        }
    }
}

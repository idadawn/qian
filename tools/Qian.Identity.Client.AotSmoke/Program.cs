using System.Net;
using System.Runtime.CompilerServices;
using System.Text.Json;
using Qian.Identity.Abstractions;
using Qian.Identity.Client;
using Qian.Runtime;

// Identity.Client 组件的 NativeAOT 冒烟（S1-IDP-014）：按 gateway 的用法装配依赖方会话与下游 S2S 校验，经 PluginLoader 挂载组件，
// 再在回环端口上逐个请求端点。发行方指向回环上无人监听的端口，discovery 立即失败，不依赖外网。全部符合预期才退出 0。
if (RuntimeFeature.IsDynamicCodeSupported)
{
    Console.Error.WriteLine("冒烟须以 NativeAOT 原生二进制运行：dotnet publish -c Release -r <rid>。");
    return 2;
}

var builder = WebApplication.CreateSlimBuilder(args);
builder.WebHost.UseUrls("http://127.0.0.1:0");
builder.Logging.SetMinimumLevel(LogLevel.Warning);
builder.Services.AddDataProtection();
builder.Services.AddQianIdentityClient(new IdentityClientOptions
{
    Authority = new Uri("https://127.0.0.1:9/"), ClientId = "qian-gateway", Scopes = ["openid", "offline_access"],
    Resource = "https://api.qian.test", RedirectUri = new Uri("https://gw.qian.test/auth/callback"), UseInMemorySessionStore = true
});
builder.Services.AddQianIdentityS2s(new IdentityS2sOptions
{
    Issuer = "https://gw.qian.test/s2s", Audience = "qian-platform", JwksUri = new Uri("https://127.0.0.1:9/.well-known/jwks.json")
});
builder.Services.AddAuthorization();

await using var app = builder.Build();
app.UseRouting();
app.UseQianIdentityClient();
app.UseAuthentication();
app.UseAuthorization();
app.MapQianIdentityClient();
app.MapGet("/smoke/s2s", (RequestDelegate)(http => http.Response.WriteAsync("ok")))
    .RequireAuthorization(policy => policy.AddAuthenticationSchemes(IdentityClientDefaults.S2sScheme).RequireAuthenticatedUser());
await app.StartAsync();

using var client = new HttpClient { BaseAddress = new Uri(app.Urls.Single()), Timeout = TimeSpan.FromSeconds(30) };
var failures = 0;
await Expect("挂载前登录端点关闭", HttpMethod.Get, "/auth/login", HttpStatusCode.NotFound);

// 挂载清单 runtime.entry 指向的 IdentityClientComponent，形状与 profile 条目相同：组件 ID、global scope 与 JSON 配置；
// 配置直接解析 JSON，不序列化匿名类型。
var loader = new PluginLoader(app.Services);
using (var config = JsonDocument.Parse("""{"issuer":"https://127.0.0.1:9/","clientId":"qian-gateway"}"""))
    await loader.MountAsync([new PluginDefinition(new PluginEntry("identity", "qian.identity.client", "1.0.0", ScopeKey.Global, 0, config.RootElement.Clone()),
        new IdentityClientComponent(), [IdentityDefinition.Key])]);
if (loader.GetState("identity", ScopeKey.Global) != PluginState.Ready)
{
    Console.Error.WriteLine("组件未就绪：" + loader.GetFailure("identity", ScopeKey.Global)?.GetType().Name);
    return 1;
}
await Expect("登录取 discovery 失败回错误详情", HttpMethod.Get, "/auth/login", HttpStatusCode.BadGateway, "BAD_GATEWAY");
await Expect("无会话取防伪令牌", HttpMethod.Get, "/auth/csrf", HttpStatusCode.Unauthorized, "UNAUTHENTICATED");
await Expect("无会话登出是幂等空操作", HttpMethod.Post, "/auth/logout", HttpStatusCode.NoContent);
await Expect("无令牌访问 S2S 保护端点", HttpMethod.Get, "/smoke/s2s", HttpStatusCode.Unauthorized);
await loader.DisposeAsync();
await Expect("卸载后登录端点关闭", HttpMethod.Get, "/auth/login", HttpStatusCode.NotFound);
await app.StopAsync();
Console.WriteLine(failures == 0 ? "NativeAOT 冒烟通过。" : $"NativeAOT 冒烟失败 {failures} 项。");
return failures == 0 ? 0 : 1;

async Task Expect(string step, HttpMethod method, string path, HttpStatusCode status, string? code = null)
{
    using var request = new HttpRequestMessage(method, path);
    using var response = await client.SendAsync(request);
    string? actual = null;
    if (response.Content.Headers.ContentType?.MediaType == "application/problem+json")
    {
        using var detail = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        actual = detail.RootElement.TryGetProperty("code", out var value) ? value.GetString() : null;
    }
    var passed = response.StatusCode == status && (code is null || actual == code);
    if (!passed) failures++;
    Console.WriteLine($"{(passed ? "通过" : "失败")}：{step}（{method} {path} → {(int)response.StatusCode}{(actual is null ? "" : " " + actual)}，预期 {(int)status}{(code is null ? "" : " " + code)}）");
}

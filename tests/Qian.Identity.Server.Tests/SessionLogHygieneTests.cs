using System.Net;
using System.Reflection;
using System.Text.RegularExpressions;
using Kun.Identity.Abstractions;
using Microsoft.Extensions.Logging;
using Qian.Tenancy;
using Qian.Testing.Containers;
using static Qian.Identity.Server.Tests.AccountSupport;
using static Qian.Identity.Server.Tests.SessionSupport;

namespace Qian.Identity.Server.Tests;

// 日志卫生（S1-IDP-004 验收 4）：会话标识 sid、令牌与密钥不进日志；审计只记 sid 的哈希。
// 两道关：静态——Identity Server 的全部日志点都是 [LoggerMessage]，没有一条带会话标识；运行时——整套会话流程跑完，本组件的日志里找不到任何会话标识或凭据。
public sealed partial class SessionLogHygieneTests(PostgresFixture postgres, RedisFixture redis) : IClassFixture<PostgresFixture>, IClassFixture<RedisFixture>
{
    private static string Root()
    {
        var root = new DirectoryInfo(AppContext.BaseDirectory);
        while (root is not null && !File.Exists(Path.Combine(root.FullName, "Qian.slnx"))) root = root.Parent;
        return root?.FullName ?? throw new InvalidOperationException("找不到仓库根目录（Qian.slnx）。");
    }

    [GeneratedRegex(@"\b(?:_logger|logger|Logger)\s*\.\s*(?:Log\w*|BeginScope)\s*\(", RegexOptions.CultureInvariant)]
    private static partial Regex DirectLoggerCall();

    [GeneratedRegex(@"sid|session", RegexOptions.CultureInvariant | RegexOptions.IgnoreCase)]
    private static partial Regex SessionWord();

    [GeneratedRegex(@"\{(\w+)\}", RegexOptions.CultureInvariant)]
    private static partial Regex Placeholder();

    [Fact]
    public void 全部日志点都是LoggerMessage_参数与模板里没有会话标识()
    {
        // 直接调用 ILogger 的 Log* 方法会绕开下面的检查，所以源码里不允许出现。
        var separator = Path.DirectorySeparatorChar;
        var sources = Directory.EnumerateFiles(Path.Combine(Root(), "packages", "Qian.Identity.Server"), "*.cs", SearchOption.AllDirectories)
            .Where(path => !path.Contains($"{separator}obj{separator}", StringComparison.Ordinal) && !path.Contains($"{separator}bin{separator}", StringComparison.Ordinal)).ToArray();
        Assert.NotEmpty(sources);
        Assert.All(sources, path => Assert.Empty(DirectLoggerCall().Matches(File.ReadAllText(path))));

        // 每个 [LoggerMessage]：参数名和模板占位符都不涉及会话；也不记录 Guid（会话、人员、租户标识都是 Guid）。
        const BindingFlags all = BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly;
        var messages = typeof(IdentityAccountService).Assembly.GetTypes().SelectMany(type => type.GetMethods(all))
            .Select(method => (Method: method, Attribute: method.GetCustomAttribute<LoggerMessageAttribute>())).Where(item => item.Attribute is not null).ToArray();
        Assert.True(messages.Length >= 4, "反射应当找到全部 [LoggerMessage] 日志点，实际只有 " + messages.Length);
        foreach (var (method, attribute) in messages)
        {
            var where = method.DeclaringType!.Name + "." + method.Name;
            foreach (var parameter in method.GetParameters().Where(p => p.ParameterType != typeof(ILogger)))
            {
                Assert.False(SessionWord().IsMatch(parameter.Name!), $"{where} 的参数 {parameter.Name} 涉及会话标识");
                Assert.NotEqual(typeof(Guid), parameter.ParameterType);
            }
            foreach (Match placeholder in Placeholder().Matches(attribute!.Message ?? ""))
                Assert.False(SessionWord().IsMatch(placeholder.Groups[1].Value), $"{where} 的模板占位符 {placeholder.Groups[1].Value} 涉及会话标识");
        }
    }

    [Fact]
    public async Task 整套会话流程跑完_本组件的日志里没有会话标识_任何日志里都没有令牌与密钥_审计只记哈希()
    {
        var options = new IdentityAccountOptions { Login = Policy(max: 100).Login, Session = new SessionPolicy { ReconcileInterval = TimeSpan.FromSeconds(1), MaxConcurrentSessions = 4 } };
        await using var host = await AccountsHost.Start(postgres, options, redis: redis.ConnectionString, logLevel: LogLevel.Debug);
        var acme = host.Tenants.Add("acme"); var beta = host.Tenants.Add("beta");
        var (admin, adminPhone) = await CreatePerson(host, "管理员");
        var (person, phone) = await CreatePerson(host, "两边的人");
        await Join(host, acme, admin.Id, admin: true); await Join(host, acme, person.Id); await Join(host, beta, person.Id);
        var gamma = host.Tenants.Add("gamma");
        var (other, otherPhone) = await CreatePerson(host, "丙方");
        await Join(host, gamma, other.Id);
        var sessions = new List<Guid>(); var secrets = new List<string> { host.Secret, Password, "Another-Pass-42" };
        async Task<(Guid Session, OidcTokenResponse Tokens, AccountsHost.Browser Browser)> Enter(string number, Guid tenant, string client = "web")
        {
            var browser = host.NewBrowser();
            var (session, tokens) = await SignIn(host, browser, number, tenant, client);
            sessions.Add(session); secrets.AddRange([tokens.AccessToken, tokens.RefreshToken!, tokens.IdToken!]);
            return (session, tokens, browser);
        }
        var adminBrowser = host.NewBrowser();
        await adminBrowser.Login(adminPhone, Password, "web", acme.Id);

        // 登录、刷新、管理员强制下线、超过并发上限被踢（上限 4）、登出。
        var (_, webTokens, _) = await Enter(phone, acme.Id);
        var (mobile, _, _) = await Enter(phone, acme.Id, "mobile");
        await Enter(phone, beta.Id);
        using (var kun = host.Kun("web")) secrets.Add((await kun.RefreshAsync(webTokens.RefreshToken!, Id(person.Id), Ct)).RefreshToken!);
        Assert.Equal(1, (await adminBrowser.Json(HttpMethod.Post, $"api/identity/sessions/{Id(mobile)}:revoke")).Body.GetProperty("revoked").GetInt32());
        await Enter(phone, acme.Id); await Enter(phone, acme.Id);
        var (_, _, lastBrowser) = await Enter(phone, acme.Id);
        var (_, outTokens, outBrowser) = await Enter(phone, beta.Id);
        Assert.Equal(HttpStatusCode.Redirect, await Logout(host, outBrowser, outTokens.IdToken!));
        // 租户冻结后由对账吊销；改密吊销该人的全部会话。
        await Enter(otherPhone, gamma.Id);
        host.Tenants.SetStatus(gamma.Id, TenantStatus.Suspended);
        Assert.Equal(HttpStatusCode.NoContent, (await lastBrowser.Json(HttpMethod.Post, "api/identity/me/password", new { current_password = Password, new_password = "Another-Pass-42" })).Status);
        await Eventually(async () => (await EventsOf(host, "identity.session_revoked.v1")).Count + (await EventsOf(host, "identity.logout.v1")).Count >= sessions.Count, TimeSpan.FromSeconds(20), "全部会话的吊销事件");

        var identifiers = sessions.SelectMany(session => new[] { session.ToString("D"), session.ToString("N") }).SelectMany(text => new[] { text, text.ToUpperInvariant() }).ToArray();
        Assert.NotEmpty(host.Logs.Entries);
        // 本组件（类别以 Qian 开头）的日志：没有任何会话标识。
        Assert.All(host.Logs.Entries.Where(entry => entry.Category.StartsWith("Qian", StringComparison.Ordinal)), entry => Assert.All(identifiers, id => Assert.DoesNotContain(id, entry.Message, StringComparison.Ordinal)));
        // 所有类别的日志（含第三方库）：没有令牌、客户端密钥和密码。
        Assert.All(host.Logs.Entries, entry => Assert.All(secrets, secret => Assert.DoesNotContain(secret, entry.Message, StringComparison.Ordinal)));
        // 审计只记 sid 的哈希（列宽就是 16），每次成功登录都有一条。
        foreach (var session in sessions) Assert.NotEqual("0", await host.Text("SELECT count(*) FROM plugin_qian_identity.login_audits WHERE session_hash = @h", ("h", AuditHash(session))));
        Assert.Equal("0", await host.Text("SELECT count(*) FROM plugin_qian_identity.login_audits WHERE length(session_hash) <> 16"));
    }
}

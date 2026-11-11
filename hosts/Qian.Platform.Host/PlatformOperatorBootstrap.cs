using System.Diagnostics;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Kun.Core.MultiTenancy;
using Qian.Audit;
using Qian.Authz;
using Qian.Identity.Abstractions;

namespace Qian.Platform.Host;

/// <summary>
/// 首个平台运营账号的一次性引导（<c>--bootstrap-operator</c>，S1-DEP-011，R150 ③）：与 <c>--migrate-only</c> 同形，作为 K8s Job 在迁移之后运行一次。
/// 要求身份分支在进程内。用户名与显示名取自配置 <c>Bootstrap:Username</c> / <c>Bootstrap:DisplayName</c>（命令行可给），初始密码取自 Secret 文件 <c>Bootstrap:PasswordFile</c>。
/// 挂载插件树后，先在无租户上下文中只读确认库里没有平台超管，再经身份账号端口建平台身份（要求首登改密），最后在同一把平台角色锁里再判一次并授平台超管，写一条平台审计链记录。
/// 库里已有平台超管时拒绝执行（退出码 1），不能拿它给第二个人提权；同名的平台身份已存在（上一次在授权之前失败）时沿用它、不重置密码。
/// 不启动 HTTP 监听，跑完即逆序卸载插件树。
/// </summary>
internal static class PlatformOperatorBootstrap
{
    internal const string Argument = "--bootstrap-operator";
    private const string Actor = "platform-bootstrap";

    internal static bool IsRequested(IEnumerable<string> args) => args.Contains(Argument, StringComparer.Ordinal);

    internal static async Task RunAsync(WebApplication app, CancellationToken ct = default)
    {
        var configuration = app.Services.GetRequiredService<IConfiguration>();
        if (!PlatformIdentity.Enabled(configuration)) throw new InvalidOperationException("引导首个平台运营账号要求身份分支在进程内（Identity:InProcess=true）。");
        var username = Required(configuration, "Bootstrap:Username");
        var displayName = Required(configuration, "Bootstrap:DisplayName");
        var password = Encoding.UTF8.GetBytes(PlatformHostSettings.ReadSecret(Required(configuration, "Bootstrap:PasswordFile")));
        var runtime = app.Services.GetRequiredService<PlatformRuntime>();
        var authz = runtime.Profile.Entries.SingleOrDefault(entry => entry.Id == "authz");
        var platformRole = authz is not null && authz.Config.TryGetProperty("platformRole", out var role) && role.ValueKind == JsonValueKind.String ? role.GetString()!
            : throw new InvalidOperationException("引导首个平台运营账号要求 authz 条目配置 platformRole（平台授权登录）。");
        var logger = app.Services.GetRequiredService<ILogger<PlatformRuntime>>();
        try
        {
            await runtime.StartAsync(ct).ConfigureAwait(false);
            using var neutral = app.Services.GetRequiredService<ITenantContext>().BeginScope(null);
            if (await PlatformSuperadminBootstrap.ExistsAsync(app.Services, platformRole, ct).ConfigureAwait(false))
                throw new InvalidOperationException("库里已有平台超管，引导拒绝执行：之后的平台超管授予与撤销走平台运营流程。");
            var accounts = runtime.Global("identity").IdentityAccounts();
            var person = await accounts.FindPersonAsync(username, ct).ConfigureAwait(false);
            if (person is null)
            {
                using var proof = new AuthenticationProof(password);
                person = await accounts.CreatePersonAsync(new CreatePerson(displayName, [new IdentifierInput(IdentifierKind.Username, username, true)])
                    { InitialPassword = proof, RequirePasswordChange = true, PlatformAccess = true }, ct).ConfigureAwait(false);
            }
            else if (!person.PlatformAccess || person.UserName != username)
                throw new InvalidOperationException("用户名 " + username + " 已被一个不是平台身份的账号占用，引导拒绝执行。");
            var subject = person.Id.ToString("D");
            if (!await PlatformSuperadminBootstrap.AssignFirstAsync(app.Services, platformRole, subject, Actor, ct).ConfigureAwait(false))
                throw new InvalidOperationException("库里已有平台超管，引导拒绝执行：之后的平台超管授予与撤销走平台运营流程。");
            var producer = runtime.Global("authz");
            using var payload = JsonDocument.Parse(JsonSerializer.Serialize(new { subject, role = "platform.superadmin", passwordChangeRequired = true }));
            var trace = Activity.Current?.TraceId.ToHexString() ?? ActivityTraceId.CreateRandom().ToHexString();
            await producer.Audit().AppendPlatformAsync(producer, new AuditRecord(Guid.CreateVersion7(), null, "service", Actor, null, "qian.authz:platform-role:bootstrap", "1.0.0",
                Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(subject))), "job", "platform:superadmin/" + subject, "allowed", trace, DateTimeOffset.UtcNow,
                payload.RootElement), ct).ConfigureAwait(false);
            BootstrapLog.Assigned(logger, subject);
        }
        finally
        {
            CryptographicOperations.ZeroMemory(password);
            await runtime.DisposeAsync().ConfigureAwait(false);
        }
    }

    private static string Required(IConfiguration configuration, string key) =>
        configuration[key] is { Length: > 0 } value ? value : throw new InvalidOperationException("缺少引导配置：" + key + "。");
}

internal static partial class BootstrapLog
{
    [LoggerMessage(EventId = 2, Level = LogLevel.Information, Message = "首个平台运营账号已建好并授予平台超管（自然人 {Subject}），首次登录须修改初始密码。")]
    internal static partial void Assigned(ILogger logger, string subject);
}

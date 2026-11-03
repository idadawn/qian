using System.Globalization;
using System.Threading.RateLimiting;
using Kun.Core.MultiTenancy;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Npgsql;
using Qian.Data;
using Qian.Identity.Abstractions;
using Qian.Runtime;
using Qian.Runtime.Errors;

namespace Qian.Tenancy.Signup;

// 开通申请服务。申请人一侧只认本组件的申请人会话（令牌只存 SHA-256），运营一侧由宿主端点完成平台授权后调用；
// 一律拒绝租户上下文：申请发生在租户存在之前，开通编排与状态机也只接受无租户上下文的调用。
internal sealed partial class TenantSignupService : ITenantSignup, IDisposable
{
    private readonly IPluginContext _context;
    private readonly TenancySignupOptions _options;
    private readonly ITenantContext _tenant;
    private readonly TimeProvider _clock;
    private readonly PartitionedRateLimiter<string> _codeChecks;
    private int _disposed;

    internal TenantSignupService(IPluginContext context, TenancySignupOptions options)
    {
        _context = context; _options = options;
        _tenant = context.Services.GetRequiredService<ITenantContext>();
        _clock = context.Services.GetService<TimeProvider>() ?? TimeProvider.System;
        // 租户号可用性只读、无副作用：按来源地址的固定窗口限流放在进程内即可（多副本时阈值按副本数放大）。
        _codeChecks = PartitionedRateLimiter.Create<string, string>(source => RateLimitPartition.GetFixedWindowLimiter(source, _ => new FixedWindowRateLimiterOptions
        { PermitLimit = options.CodeChecksPerSourcePerMinute, Window = TimeSpan.FromMinutes(1), QueueLimit = 0, AutoReplenishment = true }));
    }

    internal TenancySignupOptions Options => _options;

    public void Dispose()
    {
        if (Interlocked.Exchange(ref _disposed, 1) == 0) _codeChecks.Dispose();
    }

    private IIdentityAccounts Accounts => _context.Inject<IIdentityAccounts>(IdentityDefinition.AccountsKey);
    private ITenantDirectory Directory => _context.Inject<ITenantDirectory>(TenancyDefinition.Key);
    private ITenantProvisioning Provisioning => _context.Inject<ITenantProvisioning>(TenantProvisioning.Key);
    private ITenantLifecycle Lifecycle => _context.Inject<ITenantLifecycle>(TenantLifecycle.Key);
    private ISignupMessenger? Messenger => _context.TryInject<ISignupMessenger>(TenancySignupDefinition.MessengerKey);

    private void Check()
    {
        ObjectDisposedException.ThrowIf(Volatile.Read(ref _disposed) != 0, this);
        if (_tenant.TenantId is not null) throw Error(PlatformErrors.AuthzDenied);
    }

    private ValueTask<IDataSession<TenancySignupDbContext>> OpenAsync(CancellationToken ct) => _context.Data().OpenAsync<TenancySignupDbContext>(ct);

    // 会话级 advisory lock：同一申请的审核、重试、撤回串行，开通编排跨多个事务也不会被并发改动；进程退出即释放。
    private async Task<T> LockedAsync<T>(string key, Func<Task<T>> work, CancellationToken ct)
    {
        await using var session = await OpenAsync(ct).ConfigureAwait(false);
        await session.Context.Database.OpenConnectionAsync(ct).ConfigureAwait(false);
        await using var command = session.Context.Database.GetDbConnection().CreateCommand();
        command.CommandText = "SELECT pg_advisory_lock(hashtextextended(@key, 0))";
        var parameter = command.CreateParameter(); parameter.ParameterName = "key"; parameter.Value = TenancySignupDefinition.ComponentId + ":" + key; command.Parameters.Add(parameter);
        await command.ExecuteNonQueryAsync(ct).ConfigureAwait(false);
        try { return await work().ConfigureAwait(false); }
        finally
        {
            command.CommandText = "SELECT pg_advisory_unlock(hashtextextended(@key, 0))";
            await command.ExecuteNonQueryAsync(CancellationToken.None).ConfigureAwait(false);
        }
    }

    // 事务级 advisory lock：限流计数与写入在同一事务里串行。
    private static Task<int> XactLockAsync(TenancySignupDbContext db, string key, CancellationToken ct)
    {
        var value = TenancySignupDefinition.ComponentId + ":" + key;
        return db.Database.ExecuteSqlInterpolatedAsync($"SELECT pg_advisory_xact_lock(hashtextextended({value}, 0))", ct);
    }

    private static bool IsUnique(DbUpdateException error, string index) =>
        error.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation } postgres && postgres.ConstraintName == index;

    private static string Source(SignupRequestContext context)
    {
        var source = context.SourceAddress?.Trim();
        return string.IsNullOrEmpty(source) ? "unknown" : source.Length > 64 ? source[..64] : source;
    }

    // 平台身份不进租户（Q12）；停用、锁定、已清除的账号不能申请。
    private static bool Eligible(Person person) => person.Status == AccountStatuses.Active && !person.PlatformAccess;

    private static QianErrorException Error(ErrorCode code) => new(new QianError(code));

    private static QianErrorException RateLimited(TimeSpan retryAfter)
    {
        var error = new QianErrorException(new QianError(PlatformErrors.RateLimited));
        error.Data[TenancySignupErrors.RetryAfterDataKey] = (int)Math.Clamp(Math.Ceiling(retryAfter.TotalSeconds), 1, 86400);
        return error;
    }

    private static void ValidateActor(string actor)
    {
        if (string.IsNullOrWhiteSpace(actor) || actor.Length > 200 || actor.Any(char.IsControl)) throw Error(PlatformErrors.InputInvalid);
    }

    private static string Text(int value) => value.ToString(CultureInfo.InvariantCulture);

    // 身份组件的错误位置换成本组件请求体的字段；映射表之外的位置换成整个请求体，不把身份内部的字段名带进开通契约。
    private static readonly Dictionary<string, string> AccountPointers = new(StringComparer.Ordinal)
    { ["#/display_name"] = "#/displayName", ["#/new_password"] = "#/password", ["#/identifiers"] = "#", ["#/identifiers/0/value"] = "#" };

    private static QianErrorException Remap(QianErrorException error)
    {
        string Map(string pointer) => AccountPointers.TryGetValue(pointer, out var mapped) ? mapped : "#";
        var source = error.Error;
        if (source.Pointer is null && source.Errors.Count == 0) return error;
        return new QianErrorException(new QianError(source.Code, source.Args)
        {
            Pointer = source.Pointer is null ? null : Map(source.Pointer), Allowed = source.Allowed,
            Errors = [.. source.Errors.Select(item => new QianFieldError(item.Code, Map(item.Pointer), item.Args) { Allowed = item.Allowed })]
        }, error);
    }

    [LoggerMessage(EventId = 2001, Level = LogLevel.Warning, Message = "开通申请验证码发送失败，该验证码作废：挑战 {Challenge}")]
    private static partial void LogSendFailed(ILogger logger, Guid challenge, Exception error);
    [LoggerMessage(EventId = 2002, Level = LogLevel.Warning, Message = "开通申请审核结果通知发送失败，不影响审核：申请 {Application}，结果 {Kind}")]
    private static partial void LogNotifyFailed(ILogger logger, Guid application, SignupResultKind kind, Exception error);
    [LoggerMessage(EventId = 2003, Level = LogLevel.Warning, Message = "开通申请的开通编排或激活失败，申请转为开通失败：申请 {Application}，原因 {Reason}")]
    private static partial void LogProvisionFailed(ILogger logger, Guid application, string reason, Exception error);
}

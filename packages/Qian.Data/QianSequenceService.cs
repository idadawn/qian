using System.Data.Common;
using System.Text.RegularExpressions;
using Kun.Core.MultiTenancy;
using Kun.EntityFrameworkCore.Extensions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using Qian.Runtime.Errors;

namespace Qian.Data;

/// <summary>序号服务的宿主设置。</summary>
public sealed class QianSequenceOptions
{
    /// <summary>计算日期段与周期所用的时区。默认 UTC；面向国内租户时通常设为 Asia/Shanghai。</summary>
    public TimeZoneInfo TimeZone { get; set; } = TimeZoneInfo.Utc;
}

/// <summary>序号服务的宿主装配。</summary>
public static class QianSequenceExtensions
{
    /// <summary>
    /// 登记序号服务及其表所在的 qian.data 系统上下文（schema plugin_qian_data）。<paramref name="configure"/> 只需配置连接（如 UseNpgsql），
    /// schema 由本方法固定；表结构随包内迁移交付，宿主先以 <see cref="IDataMigrator"/> 迁移，启动时只校验。
    /// </summary>
    public static IServiceCollection AddQianSequences(this IServiceCollection services, Action<IServiceProvider, DbContextOptionsBuilder> configure,
        Action<QianSequenceOptions>? options = null)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configure);
        services.AddQianData<DataSystemDbContext>((sp, builder) =>
        {
            configure(sp, builder);
            builder.UseKunSchema(DataSystemDbContext.Schema);
        });
        var settings = services.AddOptions<QianSequenceOptions>();
        if (options is not null) settings.Configure(options);
        services.TryAddSingleton(TimeProvider.System);
        services.TryAddSingleton<ISequenceService>(sp => new QianSequenceService(sp.GetRequiredService<IServiceScopeFactory>(),
            sp.GetRequiredService<ITenantContext>(), sp.GetRequiredService<TimeProvider>(), sp.GetRequiredService<IOptions<QianSequenceOptions>>()));
        services.AddHostedService<SequenceErrorRegistration>();
        return services;
    }
}

/// <summary>
/// 序号服务：规则与计数器都按租户隔离（RLS），取号是一条 <c>INSERT … ON CONFLICT DO UPDATE … RETURNING</c>，在自己的连接上自动提交。
/// 计数器按（规则、修订、作用域、周期）分行，规则修改或周期变化都会从 1 开始。
/// </summary>
internal sealed partial class QianSequenceService(IServiceScopeFactory scopes, ITenantContext tenant, TimeProvider clock, IOptions<QianSequenceOptions> options)
    : ISequenceService
{
    private const string Rules = DataSystemDbContext.Schema + ".sequence_rules";
    private const string Counters = DataSystemDbContext.Schema + ".sequence_counters";

    public async Task<SequenceRule> DefineAsync(string ruleCode, string pattern, CancellationToken cancellationToken = default)
    {
        ValidateCode(ruleCode);
        var parsed = SequencePattern.Parse(pattern);
        var tenantId = RequireTenant();
        await using var scope = scopes.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<DataSystemDbContext>();
        await context.Database.OpenConnectionAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var connection = context.Database.GetDbConnection();
            // 模式不变时 WHERE 不成立、不返回行，修订号保持原值。
            var revision = await ScalarAsync(connection, $"""
                INSERT INTO {Rules} (tenant_id, code, pattern, revision, updated_at) VALUES (@tenant, @code, @pattern, 1, now())
                ON CONFLICT (tenant_id, code) DO UPDATE SET pattern = EXCLUDED.pattern, revision = {Rules}.revision + 1, updated_at = now()
                WHERE {Rules}.pattern <> EXCLUDED.pattern
                RETURNING revision
                """, cancellationToken, ("tenant", tenantId), ("code", ruleCode), ("pattern", parsed.Text)).ConfigureAwait(false)
                ?? await ScalarAsync(connection, $"SELECT revision FROM {Rules} WHERE tenant_id = @tenant AND code = @code",
                    cancellationToken, ("tenant", tenantId), ("code", ruleCode)).ConfigureAwait(false);
            return new SequenceRule(ruleCode, parsed.Text, Convert.ToInt32(revision, System.Globalization.CultureInfo.InvariantCulture));
        }
        finally { await context.Database.CloseConnectionAsync().ConfigureAwait(false); }
    }

    public async Task<string> NextAsync(string ruleCode, string scopeKey, CancellationToken cancellationToken = default)
    {
        ValidateCode(ruleCode);
        if (string.IsNullOrEmpty(scopeKey) || !ScopeKeyFormat().IsMatch(scopeKey))
            throw Invalid("作用域键须为 1 到 128 个字母、数字或 _ . : / - 字符。");
        var tenantId = RequireTenant();
        await using var scope = scopes.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<DataSystemDbContext>();
        await context.Database.OpenConnectionAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var connection = context.Database.GetDbConnection();
            string pattern; int revision;
            await using (var read = Command(connection, $"SELECT pattern, revision FROM {Rules} WHERE tenant_id = @tenant AND code = @code", ("tenant", tenantId), ("code", ruleCode)))
            await using (var reader = await read.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false))
            {
                if (!await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
                    throw new QianErrorException(new QianError(DataErrors.SequenceRuleNotFound, new Dictionary<string, string> { ["rule"] = ruleCode }));
                pattern = reader.GetString(0);
                revision = reader.GetInt32(1);
            }
            var parsed = SequencePattern.Parse(pattern);
            var local = TimeZoneInfo.ConvertTime(clock.GetUtcNow(), options.Value.TimeZone).DateTime;
            var period = parsed.Period(local);
            var value = Convert.ToInt64(await ScalarAsync(connection, $"""
                INSERT INTO {Counters} (tenant_id, rule_code, revision, scope_key, period, value) VALUES (@tenant, @code, @revision, @scope, @period, 1)
                ON CONFLICT (tenant_id, rule_code, revision, scope_key, period) DO UPDATE SET value = {Counters}.value + 1
                RETURNING value
                """, cancellationToken, ("tenant", tenantId), ("code", ruleCode), ("revision", revision), ("scope", scopeKey), ("period", period)).ConfigureAwait(false),
                System.Globalization.CultureInfo.InvariantCulture);
            if (value > parsed.MaxValue)
                throw new QianErrorException(new QianError(DataErrors.SequenceExhausted, new Dictionary<string, string> { ["rule"] = ruleCode }));
            return parsed.Format(local, value);
        }
        finally { await context.Database.CloseConnectionAsync().ConfigureAwait(false); }
    }

    // 序号属于租户；没有租户上下文时拒绝，不落到平台级共享计数。
    private string RequireTenant() => tenant.TenantId is { Length: > 0 } id
        ? id
        : throw new QianDataMisconfiguredException("序号服务必须在租户作用域内调用。");

    private static void ValidateCode(string ruleCode)
    {
        if (string.IsNullOrEmpty(ruleCode) || !RuleCodeFormat().IsMatch(ruleCode))
            throw Invalid("规则编码须以小写字母开头，由小写字母、数字或 _ . - 组成，不超过 64 个字符。");
    }

    private static QianErrorException Invalid(string reason) =>
        new(new QianError(DataErrors.SequenceRuleInvalid, new Dictionary<string, string> { ["reason"] = reason }));

    private static async Task<object?> ScalarAsync(DbConnection connection, string sql, CancellationToken cancellationToken, params (string Name, object Value)[] parameters)
    {
        await using var command = Command(connection, sql, parameters);
        var result = await command.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false);
        return result is DBNull ? null : result;
    }

    private static DbCommand Command(DbConnection connection, string sql, params (string Name, object Value)[] parameters)
    {
        var command = connection.CreateCommand();
        command.CommandText = sql;
        foreach (var (name, value) in parameters)
        {
            var parameter = command.CreateParameter();
            parameter.ParameterName = name;
            parameter.Value = value;
            command.Parameters.Add(parameter);
        }
        return command;
    }

    [GeneratedRegex("^[a-z][a-z0-9_.-]{0,63}$", RegexOptions.CultureInvariant)]
    private static partial Regex RuleCodeFormat();

    [GeneratedRegex("^[A-Za-z0-9_.:/-]{1,128}$", RegexOptions.CultureInvariant)]
    private static partial Regex ScopeKeyFormat();
}

/// <summary>宿主启动时登记序号服务的错误码，停止时撤销。</summary>
internal sealed class SequenceErrorRegistration(ErrorCatalog catalog) : IHostedService, IDisposable
{
    private readonly List<IDisposable> _registrations = [];

    public Task StartAsync(CancellationToken cancellationToken)
    {
        if (_registrations.Count == 0)
            foreach (var code in new[] { DataErrors.SequenceRuleInvalid, DataErrors.SequenceRuleNotFound, DataErrors.SequenceExhausted })
                _registrations.Add(catalog.Register(code));
        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        Dispose();
        return Task.CompletedTask;
    }

    public void Dispose()
    {
        foreach (var registration in _registrations) registration.Dispose();
        _registrations.Clear();
    }
}

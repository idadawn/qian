using System.Net;
using System.Net.Sockets;
using System.Text.Json;
using System.Text.RegularExpressions;
using Kun.Core.MultiTenancy;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;
using Npgsql;
using Qian.Config;
using Qian.Runtime.Errors;

namespace Qian.Data;

/// <summary>数据连接档案的宿主设置。</summary>
public sealed class QianDataConnectionOptions
{
    /// <summary>解析连接口令所用的宿主密钥策略 ID；须在 AddQianSecrets 的策略中登记，只批准 tenant 作用域的路径（如 data/*）。</summary>
    public string SecretPolicyId { get; set; } = "qian.data";
    /// <summary>单次连通测试的超时（含解析目标主机），超过即视为不可达；1 秒到 1 分钟，默认 5 秒。</summary>
    public TimeSpan TestTimeout { get; set; } = TimeSpan.FromSeconds(5);
    /// <summary>连通测试的出网约束：目标主机解析出的每个地址都须通过，连接只用检查过的地址。</summary>
    public DataConnectionEgressOptions Egress { get; } = new();
}

/// <summary>
/// 连通测试的出网约束，网段写作 CIDR（如 10.0.0.0/8、fd00::/8）。这是进程内的一道检查：完整的出口代理与 SSRF 基线按 PRD 7.17 归 S2，
/// 生产环境仍应以 NetworkPolicy 或出口代理限定可达网段。
/// </summary>
public sealed class DataConnectionEgressOptions
{
    /// <summary>
    /// 一律拒绝的网段，优先于放行网段。缺省含回环、链路本地（含云元数据 169.254.169.254）、100.100.100.200（阿里云元数据）、
    /// 未指定地址、组播与广播；可追加集群内部网段（如 K3s 的 10.42.0.0/16 与 10.43.0.0/16），本机开发可移除回环。
    /// </summary>
    public IList<string> BlockedNetworks { get; } =
    [
        "127.0.0.0/8", "::1/128", "169.254.0.0/16", "fe80::/10", "100.100.100.200/32",
        "0.0.0.0/8", "::/128", "224.0.0.0/4", "ff00::/8", "255.255.255.255/32"
    ];

    /// <summary>非空时只放行落在这些网段内的地址（如租户数据库所在子网）；为空不额外限制。</summary>
    public IList<string> AllowedNetworks { get; } = [];

    /// <summary>
    /// 地址是否允许出网：不在拒绝网段内，且放行网段为空或包含它。IPv4 映射的 IPv6 地址按 IPv4 判定。网段写法无效时抛 FormatException。
    /// </summary>
    public bool Permits(IPAddress address)
    {
        ArgumentNullException.ThrowIfNull(address);
        if (address.IsIPv4MappedToIPv6) address = address.MapToIPv4();
        if (BlockedNetworks.Any(network => IPNetwork.Parse(network).Contains(address))) return false;
        return AllowedNetworks.Count == 0 || AllowedNetworks.Any(network => IPNetwork.Parse(network).Contains(address));
    }

    internal bool IsValid() => BlockedNetworks.Concat(AllowedNetworks).All(network => IPNetwork.TryParse(network, out _));
}

/// <summary>数据连接档案的宿主装配。</summary>
public static class QianDataConnectionExtensions
{
    /// <summary>
    /// 登记 <see cref="IDataConnectionRegistry"/> 及其表所在的 qian.data 系统上下文（与序号服务共用，schema plugin_qian_data），
    /// 并登记基准类型目录与方言注册表。<paramref name="configure"/> 只需配置连接；表随包内迁移交付。
    /// 连通测试经宿主登记的 ISecretResolverFactory（AddQianSecrets）解析口令，方言由 IDataDialect 组件提供（如 AddQianPgDialect）。
    /// </summary>
    public static IServiceCollection AddQianDataConnections(this IServiceCollection services, Action<IServiceProvider, DbContextOptionsBuilder> configure,
        Action<QianDataConnectionOptions>? options = null)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configure);
        services.AddQianDataTypes();
        DataSystemContext.Add(services, configure);
        var settings = services.AddOptions<QianDataConnectionOptions>()
            .Validate(value => !string.IsNullOrWhiteSpace(value.SecretPolicyId) && value.TestTimeout >= TimeSpan.FromSeconds(1)
                && value.TestTimeout <= TimeSpan.FromMinutes(1), "SecretPolicyId 不能为空，TestTimeout 须在 1 秒到 1 分钟之间。")
            .Validate(value => value.Egress.IsValid(), "Egress 的网段须写作有效的 CIDR，如 10.0.0.0/8 或 fd00::/8。")
            .ValidateOnStart();
        if (options is not null) settings.Configure(options);
        services.TryAddSingleton(TimeProvider.System);
        services.TryAddSingleton<IDataConnectionRegistry>(sp => new DataConnectionRegistry(sp.GetRequiredService<IServiceScopeFactory>(),
            sp.GetRequiredService<ITenantContext>(), sp.GetRequiredService<IDataDialectRegistry>(), sp.GetRequiredService<TimeProvider>(),
            sp.GetRequiredService<IOptions<QianDataConnectionOptions>>()));
        return services;
    }
}

/// <summary>
/// 数据连接档案：按租户隔离（RLS + 查询过滤），口令只以租户 secretRef 保存，连通测试时在使用点经宿主密钥策略解析，
/// 用后即释放；任何返回值和错误都不含口令。
/// </summary>
internal sealed partial class DataConnectionRegistry(IServiceScopeFactory scopes, ITenantContext tenant, IDataDialectRegistry dialects,
    TimeProvider clock, IOptions<QianDataConnectionOptions> options) : IDataConnectionRegistry
{
    public async Task<DataConnectionList> ListAsync(CancellationToken cancellationToken = default)
    {
        RequireTenant();
        await using var scope = scopes.CreateAsyncScope();
        var rows = await Connections(scope).AsNoTracking().OrderBy(connection => connection.Name).ToArrayAsync(cancellationToken).ConfigureAwait(false);
        return new DataConnectionList(Array.AsReadOnly(rows.Select(Profile).ToArray()));
    }

    public async Task<DataConnectionProfile> GetAsync(Guid id, CancellationToken cancellationToken = default)
    {
        RequireTenant();
        await using var scope = scopes.CreateAsyncScope();
        return Profile(await FindAsync(Connections(scope).AsNoTracking(), id, cancellationToken).ConfigureAwait(false));
    }

    public async Task<DataConnectionProfile> CreateAsync(DataConnectionDraft draft, CancellationToken cancellationToken = default)
    {
        RequireTenant();
        Validate(draft);
        await using var scope = scopes.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<DataSystemDbContext>();
        var record = new DataConnectionRecord(Guid.CreateVersion7());
        Apply(record, draft);
        context.Add(record);
        await SaveAsync(context, draft.Name, cancellationToken).ConfigureAwait(false);
        return Profile(record);
    }

    public async Task<DataConnectionProfile> UpdateAsync(Guid id, DataConnectionDraft draft, CancellationToken cancellationToken = default)
    {
        RequireTenant();
        Validate(draft);
        await using var scope = scopes.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<DataSystemDbContext>();
        var record = await FindAsync(context.Set<DataConnectionRecord>(), id, cancellationToken).ConfigureAwait(false);
        Apply(record, draft);
        await SaveAsync(context, draft.Name, cancellationToken).ConfigureAwait(false);
        return Profile(record);
    }

    public async Task DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        RequireTenant();
        await using var scope = scopes.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<DataSystemDbContext>();
        // 软删拦截器把删除改写为 is_deleted = true，并记录删除时间与主体。
        context.Remove(await FindAsync(context.Set<DataConnectionRecord>(), id, cancellationToken).ConfigureAwait(false));
        await SaveAsync(context, null, cancellationToken).ConfigureAwait(false);
    }

    public async Task<DataConnectionTestResult> TestAsync(Guid id, CancellationToken cancellationToken = default)
    {
        RequireTenant();
        await using var scope = scopes.CreateAsyncScope();
        var record = await FindAsync(Connections(scope).AsNoTracking(), id, cancellationToken).ConfigureAwait(false);
        var dialect = dialects.Find(record.Dialect)
            ?? throw new QianErrorException(new QianError(DataErrors.DialectNotFound, new Dictionary<string, string> { ["dialect"] = record.Dialect }));
        var secrets = scope.ServiceProvider.GetService<ISecretResolverFactory>()
            ?? throw new QianErrorException(new QianError(SecretErrors.Unavailable));
        var settings = options.Value;
        var started = clock.GetTimestamp();
        // 先检查出网目标再解析口令：被拒的目标既不出网，也不读取密钥。
        var address = await ResolveAsync(record.Host, settings, cancellationToken).ConfigureAwait(false);
        using var password = await secrets.ForHost(settings.SecretPolicyId, tenant).ResolveAsync(record.SecretRef, cancellationToken).ConfigureAwait(false);
        var remaining = settings.TestTimeout - clock.GetElapsedTime(started);
        if (remaining <= TimeSpan.Zero) throw Unreachable(null);
        var probing = clock.GetTimestamp();
        var probe = await dialect.ProbeAsync(new DataConnectionTarget(address.ToString(), record.Port, record.Database, record.UserName, record.Schema, record.Encoding),
            password, remaining, cancellationToken).ConfigureAwait(false);
        var latency = (int)Math.Min(int.MaxValue, Math.Round(clock.GetElapsedTime(probing).TotalMilliseconds));
        return new DataConnectionTestResult(record.Dialect, probe.ServerVersion, latency);
    }

    // 解析目标主机的全部地址，任一地址被拒即整体拒绝（多条记录或解析结果轮换都绕不过）；连接只用检查过的第一个地址，不再二次解析。
    // IP 字面量（含 127.1、十六进制这类变形写法）按解析后的地址判定，交给方言的也是规范写法。
    private static async Task<IPAddress> ResolveAsync(string host, QianDataConnectionOptions settings, CancellationToken cancellationToken)
    {
        IPAddress[] addresses;
        if (IPAddress.TryParse(host, out var literal)) addresses = [literal];
        else
        {
            using var deadline = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            deadline.CancelAfter(settings.TestTimeout);
            try { addresses = await Dns.GetHostAddressesAsync(host, deadline.Token).ConfigureAwait(false); }
            catch (OperationCanceledException error) when (!cancellationToken.IsCancellationRequested) { throw Unreachable(error); }
            catch (SocketException error) { throw Unreachable(error); }
        }
        if (addresses.Length == 0) throw Unreachable(null);
        if (!addresses.All(settings.Egress.Permits)) throw new QianErrorException(new QianError(DataErrors.ConnectionTargetDenied));
        return addresses[0].IsIPv4MappedToIPv6 ? addresses[0].MapToIPv4() : addresses[0];
    }

    private static QianErrorException Unreachable(Exception? inner) => new(new QianError(DataErrors.ConnectionUnreachable), inner);

    private static DbSet<DataConnectionRecord> Connections(AsyncServiceScope scope) =>
        scope.ServiceProvider.GetRequiredService<DataSystemDbContext>().Set<DataConnectionRecord>();

    private static async Task<DataConnectionRecord> FindAsync(IQueryable<DataConnectionRecord> connections, Guid id, CancellationToken cancellationToken) =>
        await connections.SingleOrDefaultAsync(connection => connection.Id == id, cancellationToken).ConfigureAwait(false)
            ?? throw new QianErrorException(new QianError(DataErrors.ConnectionNotFound));

    private static async Task SaveAsync(DataSystemDbContext context, string? name, CancellationToken cancellationToken)
    {
        try { await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false); }
        catch (DbUpdateConcurrencyException error)
        {
            throw new QianErrorException(new QianError(PlatformErrors.Conflict), error);
        }
        catch (DbUpdateException error) when (name is not null && error.InnerException is PostgresException
            { SqlState: PostgresErrorCodes.UniqueViolation, ConstraintName: DataSystemDbContext.NameIndex })
        {
            throw new QianErrorException(new QianError(DataErrors.ConnectionNameConflict, new Dictionary<string, string> { ["name"] = name }), error);
        }
    }

    private static void Apply(DataConnectionRecord record, DataConnectionDraft draft)
    {
        record.Name = draft.Name;
        record.Dialect = draft.Dialect;
        record.Host = draft.Host;
        record.Port = draft.Port;
        record.Database = draft.Database;
        record.UserName = draft.UserName;
        record.SecretRef = draft.SecretRef;
        record.Schema = draft.Schema;
        record.Encoding = draft.Encoding;
    }

    private static DataConnectionProfile Profile(DataConnectionRecord record) => new(record.Id, record.Name, record.Dialect, record.Host, record.Port,
        record.Database, record.UserName, record.SecretRef, record.Schema, record.Encoding, record.CreatedAt, record.UpdatedAt);

    private void Validate(DataConnectionDraft draft)
    {
        ArgumentNullException.ThrowIfNull(draft);
        var errors = new List<QianFieldError>();
        Check(errors, "#/name", draft.Name, 64, NamePattern());
        if (string.IsNullOrEmpty(draft.Dialect)) errors.Add(new QianFieldError(PlatformErrors.Required, "#/dialect"));
        else if (dialects.Find(draft.Dialect) is null)
            errors.Add(new QianFieldError(PlatformErrors.ValueNotAllowed, "#/dialect") { Allowed = Codes(dialects.List()) });
        Check(errors, "#/host", draft.Host, 253, HostPattern());
        if (draft.Port is < 1 or > 65535) errors.Add(new QianFieldError(PlatformErrors.OutOfRange, "#/port"));
        Check(errors, "#/database", draft.Database, 63, IdentifierPattern());
        Check(errors, "#/userName", draft.UserName, 63, IdentifierPattern());
        if (draft.Schema is not null) Check(errors, "#/schema", draft.Schema, 63, IdentifierPattern());
        if (draft.Encoding is not null) Check(errors, "#/encoding", draft.Encoding, 32, EncodingPattern());
        // 只接受租户作用域的引用：平台与开发引用不能被租户借去连接任意主机。
        if (string.IsNullOrEmpty(draft.SecretRef)) errors.Add(new QianFieldError(PlatformErrors.Required, "#/secretRef"));
        else if (draft.SecretRef.Length > 256 || !SecretReference.IsValid(draft.SecretRef) || SecretReference.Parse(draft.SecretRef).Scope != "tenant")
            errors.Add(new QianFieldError(PlatformErrors.PatternMismatch, "#/secretRef"));
        if (errors.Count > 0) throw new QianErrorException(new QianError(PlatformErrors.InputInvalid) { Errors = errors });
    }

    private static void Check(List<QianFieldError> errors, string pointer, string? value, int maxLength, Regex pattern)
    {
        if (string.IsNullOrEmpty(value)) errors.Add(new QianFieldError(PlatformErrors.Required, pointer));
        else if (value.Length > maxLength) errors.Add(new QianFieldError(PlatformErrors.OutOfRange, pointer));
        else if (!pattern.IsMatch(value)) errors.Add(new QianFieldError(PlatformErrors.PatternMismatch, pointer));
    }

    private static JsonElement[] Codes(IReadOnlyList<DbDialect> registered)
    {
        // 方言代码受描述数据校验约束为小写字母与数字，可直接拼成 JSON 字符串。
        using var document = JsonDocument.Parse("[" + string.Join(",", registered.Select(dialect => "\"" + dialect.Code + "\"")) + "]");
        return document.RootElement.EnumerateArray().Select(code => code.Clone()).ToArray();
    }

    private void RequireTenant()
    {
        if (string.IsNullOrEmpty(tenant.TenantId)) throw new QianDataMisconfiguredException("数据连接档案只能在已验证租户的上下文中使用。");
    }

    [GeneratedRegex(@"\A[^\s\p{Cc}]([^\p{Cc}]*[^\s\p{Cc}])?\z", RegexOptions.CultureInvariant)]
    private static partial Regex NamePattern();
    [GeneratedRegex(@"\A[A-Za-z0-9][A-Za-z0-9.:_-]*\z", RegexOptions.CultureInvariant)]
    private static partial Regex HostPattern();
    [GeneratedRegex(@"\A[^\s;'""\\]+\z", RegexOptions.CultureInvariant)]
    private static partial Regex IdentifierPattern();
    [GeneratedRegex(@"\A[A-Za-z0-9_-]+\z", RegexOptions.CultureInvariant)]
    private static partial Regex EncodingPattern();
}

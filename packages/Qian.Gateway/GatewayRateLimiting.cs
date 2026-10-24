using System.Diagnostics;
using System.Globalization;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using System.Threading.RateLimiting;
using Kun.Gateway.RateLimiting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.IdentityModel.JsonWebTokens;
using Qian.Runtime.Errors;
using Qian.Runtime.Errors.AspNetCore;
using StackExchange.Redis;
using Yarp.ReverseProxy.Model;

namespace Qian.Gateway;

internal static class GatewayRateLimiting
{
    internal static void Add(IServiceCollection services, GatewayConfiguration configuration)
    {
        services.TryAddSingleton<IRedisRateLimitStore>(provider => new GatewayRedisRateLimitStore(
            provider.GetRequiredService<IConnectionMultiplexer>(), configuration.RateLimits.KeyPrefix));
        services.AddSingleton(provider => new GatewayRateLimitPolicy(configuration, provider.GetRequiredService<IRedisRateLimitStore>()));
        services.AddOptions<RateLimiterOptions>().Configure<GatewayRateLimitPolicy>((options, policy) =>
        {
            options.AddPolicy("tenant", policy);
            options.AddPolicy("user", policy);
        });
    }
}

// 复用 Kun 的计数端口；INCR 与到期设置在同一 Lua 操作内完成，并修复没有 TTL 的遗留桶。
// 不拥有宿主的 Redis 连接，不复制签发器或会话实现。
internal sealed class GatewayRedisRateLimitStore(IConnectionMultiplexer redis, string prefix) : IRedisRateLimitStore
{
    private const string Hit = """
        local count = redis.call('INCR', KEYS[1])
        local ttl = redis.call('PTTL', KEYS[1])
        if ttl < 0 then
            redis.call('PEXPIRE', KEYS[1], ARGV[1])
            ttl = tonumber(ARGV[1])
        end
        return {count, ttl}
        """;

    public async Task<RateLimitHitResult> HitAsync(string partitionKey, TimeSpan window, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(partitionKey);
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(window, TimeSpan.Zero);
        cancellationToken.ThrowIfCancellationRequested();
        var result = (RedisResult[])(await redis.GetDatabase().ScriptEvaluateAsync(Hit,
            [(RedisKey)(prefix + ":" + partitionKey)], [(RedisValue)(long)Math.Ceiling(window.TotalMilliseconds)])
            .WaitAsync(cancellationToken).ConfigureAwait(false))!;
        return new((long)result[0], TimeSpan.FromMilliseconds((long)result[1]));
    }
}

// 通过 RequireRateLimiting(policy) 绑定真实代理端点，读取 YARP 的声明式策略；
// 采用 ASP.NET 的公开端点策略接口，保留宿主/Kun 对其它端点登记的命名策略。
internal sealed class GatewayRateLimitPolicy(GatewayConfiguration configuration, IRedisRateLimitStore store) : IRateLimiterPolicy<string>
{
    private readonly Dictionary<string, GatewayRoute> _routes = configuration.Routes.ToDictionary(route => route.Id, StringComparer.Ordinal);

    public RateLimitPartition<string> GetPartition(HttpContext httpContext)
    {
        var route = httpContext.GetEndpoint()?.Metadata.GetMetadata<RouteModel>()
            ?? throw new InvalidOperationException("代理限流端点缺少静态路由。");
        var (key, limit) = Partition(httpContext, route.Config.RouteId);
        return RateLimitPartition.Get(key, _ => new GatewayRedisRateLimiter(store, key, limit, TimeSpan.FromSeconds(configuration.RateLimits.WindowSeconds)));
    }

    internal (string Key, int Limit) Partition(HttpContext http, string routeId)
    {
        var route = _routes[routeId];
        var limits = configuration.RateLimits;
        var address = http.Connection.RemoteIpAddress;
        if (address?.IsIPv4MappedToIPv6 == true) address = address.MapToIPv4();
        var dimensions = new List<string> { configuration.Issuer, route.Id, route.RateLimiterPolicy, "ip", address?.ToString() ?? "unknown" };
        var limit = limits.IpPermitLimit;
        var tenantClaim = One(http.User, "tenant_id");
        var tenant = tenantClaim is not null && Guid.TryParseExact(tenantClaim.Value, "D", out var tenantId) ? tenantId.ToString("D") : null;
        var platform = tenantClaim?.ValueType == JsonClaimValueTypes.JsonNull;
        var subject = One(http.User, "sub")?.Value;
        var kind = One(http.User, "principal_kind")?.Value;
        if (http.User.Identity?.IsAuthenticated == true && kind is "user" or "service")
        {
            if (route.RateLimiterPolicy == "tenant" && tenant is not null)
            {
                dimensions[3] = "tenant"; dimensions[4] = tenant; limit = limits.TenantPermitLimit;
            }
            else if (route.RateLimiterPolicy == "user" && subject is { Length: > 0 } && (tenant is not null || platform))
            {
                dimensions[3] = "principal"; dimensions[4] = tenant ?? "platform";
                dimensions.Add(kind); dimensions.Add(subject); limit = limits.UserPermitLimit;
            }
        }
        // 长度前缀保证字段边界无歧义；Redis 中不存明文租户、主体或 IP。
        var material = string.Concat(dimensions.Select(value => value.Length.ToString(CultureInfo.InvariantCulture) + ":" + value));
        return (Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(material))), limit);
    }

    private static Claim? One(ClaimsPrincipal principal, string type)
    {
        var claims = principal.FindAll(type).Take(2).ToArray();
        return claims.Length == 1 && claims[0].Value.Length <= 255 && !claims[0].Value.Any(char.IsControl) ? claims[0] : null;
    }

    public Func<OnRejectedContext, CancellationToken, ValueTask>? OnRejected => Reject;

    private static async ValueTask Reject(OnRejectedContext context, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var seconds = context.Lease.TryGetMetadata(MetadataName.RetryAfter, out var retry) ? Math.Max(1, Math.Ceiling(retry.TotalSeconds)) : 1;
        context.HttpContext.Response.Headers.RetryAfter = seconds.ToString(CultureInfo.InvariantCulture);
        await QianResults.Error(new QianError(PlatformErrors.RateLimited)).ExecuteAsync(context.HttpContext).ConfigureAwait(false);
    }
}

// 同步快路径不做网络 I/O；ASP.NET 在未取到许可后调用 AcquireAsync，每个请求只执行一次 Redis 命中。
internal sealed class GatewayRedisRateLimiter(IRedisRateLimitStore store, string key, int limit, TimeSpan window) : RateLimiter
{
    private long _lastUse = Stopwatch.GetTimestamp();
    private int _active;
    public override TimeSpan? IdleDuration => Volatile.Read(ref _active) == 0 ? Stopwatch.GetElapsedTime(Interlocked.Read(ref _lastUse)) : null;
    public override RateLimiterStatistics? GetStatistics() => null;
    protected override RateLimitLease AttemptAcquireCore(int permitCount) => new GatewayRateLimitLease(permitCount == 0, null);
    protected override async ValueTask<RateLimitLease> AcquireAsyncCore(int permitCount, CancellationToken cancellationToken)
    {
        if (permitCount == 0) return new GatewayRateLimitLease(true, null);
        ArgumentOutOfRangeException.ThrowIfNotEqual(permitCount, 1);
        Interlocked.Increment(ref _active);
        try
        {
            var hit = await store.HitAsync(key, window, cancellationToken).ConfigureAwait(false);
            if (hit.Count < 1 || hit.ResetsIn < TimeSpan.Zero) throw new QianErrorException(new QianError(PlatformErrors.Unavailable));
            return new GatewayRateLimitLease(hit.Count <= limit, hit.ResetsIn);
        }
        catch (RedisException) { throw new QianErrorException(new QianError(PlatformErrors.Unavailable)); }
        catch (TimeoutException) { throw new QianErrorException(new QianError(PlatformErrors.Unavailable)); }
        finally
        {
            Interlocked.Exchange(ref _lastUse, Stopwatch.GetTimestamp());
            Interlocked.Decrement(ref _active);
        }
    }
}

internal sealed class GatewayRateLimitLease(bool acquired, TimeSpan? retry) : RateLimitLease
{
    public override bool IsAcquired => acquired;
    public override IEnumerable<string> MetadataNames => retry.HasValue ? [MetadataName.RetryAfter.Name] : [];
    public override bool TryGetMetadata(string metadataName, out object? metadata)
    {
        metadata = metadataName == MetadataName.RetryAfter.Name && retry.HasValue ? retry.Value : null;
        return metadata is not null;
    }
}

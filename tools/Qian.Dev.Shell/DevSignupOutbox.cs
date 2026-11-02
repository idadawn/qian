using System.Collections.Concurrent;
using System.Net;
using System.Security.Cryptography;
using System.Text;
using Qian.Tenancy.Signup;

namespace Qian.Dev.Shell;

/// <summary>
/// 开发期发件箱（S1-TEN-019）：<see cref="ISignupMessenger"/> 的开发实现，把验证码与审核结果记在内存里，供手测与 E2E 经
/// <c>GET /__dev/signup/outbox?phone=</c> 读取。只在 Development 环境、只在只监听回环地址的宿主上登记（构造时核对，否则抛出）；
/// 读取路由只答回环连接与回环主机名，并且要带状态目录里的开发令牌（请求头 <see cref="TokenHeader"/>），其余一律 404。
/// 申请人没有可登录的身份，这个路由没法走 BFF 的「已认证 + 防伪」：本机文件里的令牌充当认证，自定义请求头让跨源页面无法代发。
/// 不是产品契约；通知组件（S1-NTF-003）交付短信渠道后删除或替换。
/// </summary>
internal sealed class DevSignupOutbox : ISignupMessenger
{
    internal const string Route = "/__dev/signup/outbox";
    internal const string TokenHeader = "X-Qian-Dev-Token";
    private const int Keep = 20;
    private readonly ConcurrentDictionary<string, Box> _boxes = new(StringComparer.Ordinal);
    private readonly byte[] _token;

    internal DevSignupOutbox(IHostEnvironment environment, IReadOnlyList<Uri> listen, string token)
    {
        ArgumentNullException.ThrowIfNull(environment); ArgumentNullException.ThrowIfNull(listen); ArgumentNullException.ThrowIfNull(token);
        if (!environment.IsDevelopment()) throw new InvalidOperationException("开发期发件箱只在 Development 环境登记。");
        if (listen.Count == 0 || listen.Any(uri => !IsLoopbackHost(uri.Host))) throw new InvalidOperationException("开发期发件箱只登记在只监听回环地址的宿主上。");
        if (token.Length < 32) throw new ArgumentException("开发令牌至少 32 个字符。", nameof(token));
        _token = Encoding.UTF8.GetBytes(token);
    }

    /// <summary>新的开发令牌（32 字节随机数的十六进制）。</summary>
    internal static string NewToken() => Convert.ToHexString(RandomNumberGenerator.GetBytes(32));

    public ValueTask SendVerificationCodeAsync(SignupVerificationMessage message, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(message);
        var box = _boxes.GetOrAdd(message.Phone, _ => new Box());
        lock (box)
        {
            // 按消息 Id 去重（端口约定）：同一挑战只记一次。
            if (box.Codes.All(code => code.Id != message.Id))
            {
                box.Codes.Insert(0, new DevSignupCode(message.Id, message.Code, message.ExpiresAt, DateTimeOffset.UtcNow));
                if (box.Codes.Count > Keep) box.Codes.RemoveRange(Keep, box.Codes.Count - Keep);
            }
        }
        return ValueTask.CompletedTask;
    }

    public ValueTask SendResultAsync(SignupResultMessage message, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(message);
        var box = _boxes.GetOrAdd(message.Phone, _ => new Box());
        var kind = message.Kind == SignupResultKind.Approved ? "approved" : "rejected";
        lock (box)
        {
            if (box.Notifications.All(item => item.Id != message.Id || item.Kind != kind))
            {
                box.Notifications.Insert(0, new DevSignupNotification(message.Id, kind, message.ApplicationNumber, message.TenantCode, message.ValidUntil, message.Reason, DateTimeOffset.UtcNow));
                if (box.Notifications.Count > Keep) box.Notifications.RemoveRange(Keep, box.Notifications.Count - Keep);
            }
        }
        return ValueTask.CompletedTask;
    }

    /// <summary>读取路由：最近的验证码与通知，新的在前。号码可写 E.164（加号要编码为 %2B）或国内 11 位手机号。</summary>
    internal IResult Read(HttpContext http)
    {
        ArgumentNullException.ThrowIfNull(http);
        http.Response.Headers.CacheControl = "no-store";
        if (!IsLoopback(http.Connection.RemoteIpAddress) || !IsLoopback(http.Connection.LocalIpAddress) || !IsLoopbackHost(http.Request.Host.Host)) return Results.NotFound();
        if (!http.Request.Headers.TryGetValue(TokenHeader, out var presented) || presented.Count != 1 ||
            !CryptographicOperations.FixedTimeEquals(Encoding.UTF8.GetBytes(presented[0] ?? ""), _token)) return Results.NotFound();
        if (Normalize(http.Request.Query["phone"].ToString()) is not { } phone) return Results.BadRequest();
        if (!_boxes.TryGetValue(phone, out var box)) return Results.Json(new DevSignupOutboxView(phone, [], []));
        lock (box) return Results.Json(new DevSignupOutboxView(phone, [.. box.Codes], [.. box.Notifications]));
    }

    // 只取数字：13 位且以 86 开头视为已带国家码，11 位视为国内手机号补 +86，其余按已带国家码处理。
    internal static string? Normalize(string? raw)
    {
        var digits = new string((raw ?? "").Where(char.IsAsciiDigit).ToArray());
        if (digits.Length is < 7 or > 15) return null;
        return digits.Length == 11 && digits[0] == '1' ? "+86" + digits : "+" + digits;
    }

    internal static bool IsLoopbackHost(string host) =>
        string.Equals(host, "localhost", StringComparison.OrdinalIgnoreCase) || (IPAddress.TryParse(host.Trim('[', ']'), out var address) && IPAddress.IsLoopback(address));

    private static bool IsLoopback(IPAddress? address) => address is not null && IPAddress.IsLoopback(address.IsIPv4MappedToIPv6 ? address.MapToIPv4() : address);

    private sealed class Box
    {
        internal List<DevSignupCode> Codes { get; } = [];
        internal List<DevSignupNotification> Notifications { get; } = [];
    }
}

/// <summary>发件箱里的一条验证码。</summary>
internal sealed record DevSignupCode(Guid Id, string Code, DateTimeOffset ExpiresAt, DateTimeOffset SentAt);

/// <summary>发件箱里的一条审核结果通知；kind 为 approved / rejected。</summary>
internal sealed record DevSignupNotification(Guid Id, string Kind, string ApplicationNumber, string? TenantCode, DateTimeOffset? ValidUntil, string? Reason, DateTimeOffset SentAt);

/// <summary>读取路由的响应。</summary>
internal sealed record DevSignupOutboxView(string Phone, IReadOnlyList<DevSignupCode> Codes, IReadOnlyList<DevSignupNotification> Notifications);

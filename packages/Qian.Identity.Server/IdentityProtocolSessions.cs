using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using OpenIddict.Abstractions;
using static OpenIddict.Abstractions.OpenIddictConstants;

namespace Qian.Identity.Server;

/// <summary>
/// 会话吊销在协议侧的收尾：吊销该会话（sid）名下的全部授权与令牌（含 refresh 族），并为登记了后通道登出地址、
/// 且确实持有该会话授权的客户端登记一条登出投递（由 <see cref="IdentityBackchannelWorker"/> 重试送达）。
/// 登出端点和会话中心的旁路处理共用这一处；幂等——重复调用不会再吊销已吊销的授权，也不会重复登记投递。
/// </summary>
internal static class IdentityProtocolSessions
{
    /// <param name="services">带作用域的服务提供者（请求作用域或自建作用域），OpenIddict 管理器与 <see cref="IdentityDbContext"/> 都取自它。</param>
    /// <param name="options">宿主登记的客户端。</param>
    /// <param name="subject">Person 的 sub（<c>D</c> 格式）。</param>
    /// <param name="sid">会话标识（<c>D</c> 格式）。</param>
    /// <param name="ct">取消令牌。</param>
    internal static async Task RevokeAsync(IServiceProvider services, IdentityServerOptions options, string subject, string sid, CancellationToken ct)
    {
        var authorizations = services.GetRequiredService<IOpenIddictAuthorizationManager>();
        var applications = services.GetRequiredService<IOpenIddictApplicationManager>();
        var tokens = services.GetRequiredService<IOpenIddictTokenManager>();
        var db = services.GetRequiredService<IdentityDbContext>();
        await using var transaction = await db.Database.BeginTransactionAsync(ct).ConfigureAwait(false);
        await db.Database.ExecuteSqlInterpolatedAsync($"SELECT pg_advisory_xact_lock(hashtextextended({"qian.identity:logout:" + subject + ":" + sid}, 0))", ct).ConfigureAwait(false);
        foreach (var client in options.Clients)
        {
            var application = await applications.FindByClientIdAsync(client.Application.ClientId!, ct).ConfigureAwait(false);
            if (application is null) continue;
            var applicationId = await applications.GetIdAsync(application, ct).ConfigureAwait(false);
            var revoked = false;
            var matching = new List<object>();
            await foreach (var authorization in authorizations.FindAsync(subject, applicationId, Statuses.Valid, AuthorizationTypes.AdHoc, null, ct).ConfigureAwait(false)) matching.Add(authorization);
            foreach (var authorization in matching)
            {
                var properties = await authorizations.GetPropertiesAsync(authorization, ct).ConfigureAwait(false);
                if (!properties.TryGetValue("sid", out var value) || value.GetString() != sid) continue;
                var id = await authorizations.GetIdAsync(authorization, ct).ConfigureAwait(false);
                await authorizations.TryRevokeAsync(authorization, ct).ConfigureAwait(false);
                await tokens.RevokeByAuthorizationIdAsync(id!, ct).ConfigureAwait(false);
                revoked = true;
            }
            if (revoked && client.BackchannelLogoutUri is not null &&
                !await db.LogoutDeliveries.AnyAsync(row => row.ClientId == client.Application.ClientId && row.SessionId == sid, ct).ConfigureAwait(false))
                db.LogoutDeliveries.Add(new LogoutDelivery { ClientId = client.Application.ClientId!, Subject = subject, SessionId = sid });
        }
        await db.SaveChangesAsync(ct).ConfigureAwait(false);
        await transaction.CommitAsync(ct).ConfigureAwait(false);
    }
}

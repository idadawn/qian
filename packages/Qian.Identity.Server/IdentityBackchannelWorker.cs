using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;

namespace Qian.Identity.Server;

// 与授权族吊销同事务登记，网络失败不回滚已生效的本地登出；多副本用 SKIP LOCKED 分担投递。
internal sealed partial class IdentityBackchannelWorker(IServiceScopeFactory scopes, IdentityServerOptions options,
    IdentityProtocolGate gate, IHttpClientFactory clients, ILogger<IdentityBackchannelWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                if (gate.Active && await DeliverOne(stoppingToken).ConfigureAwait(false)) continue;
                await Task.Delay(TimeSpan.FromSeconds(1), stoppingToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { return; }
            catch (Exception error)
            {
                DispatchFailed(logger, error.GetType().Name);
                await Task.Delay(TimeSpan.FromSeconds(1), stoppingToken).ConfigureAwait(false);
            }
        }
    }

    private async Task<bool> DeliverOne(CancellationToken ct)
    {
        await using var scope = scopes.CreateAsyncScope(); var db = scope.ServiceProvider.GetRequiredService<IdentityDbContext>();
        await using var transaction = await db.Database.BeginTransactionAsync(ct).ConfigureAwait(false);
        var rows = await db.LogoutDeliveries.FromSqlRaw("""
            SELECT * FROM plugin_qian_identity.logout_deliveries
            WHERE delivered_at IS NULL AND next_attempt_at <= now()
            ORDER BY created_at LIMIT 1 FOR UPDATE SKIP LOCKED
            """).ToListAsync(ct).ConfigureAwait(false);
        if (rows.Count == 0) return false;
        var row = rows[0]; row.Attempts++;
        var client = options.Clients.SingleOrDefault(client => client.Application.ClientId == row.ClientId);
        try
        {
            // 客户端已从可信注册配置移除时不能继续向旧地址发送。
            if (client?.BackchannelLogoutUri is null) throw new InvalidOperationException();
            var now = DateTimeOffset.UtcNow;
            var certificate = options.SigningCertificates.Where(cert => cert.NotBefore.ToUniversalTime() <= now.UtcDateTime && cert.NotAfter.ToUniversalTime() > now.UtcDateTime)
                .OrderByDescending(cert => cert.NotAfter).First();
            var token = new JsonWebTokenHandler().CreateToken(new SecurityTokenDescriptor
            {
                Issuer = options.Issuer.AbsoluteUri, Audience = row.ClientId, IssuedAt = now.UtcDateTime, NotBefore = now.UtcDateTime,
                Expires = now.AddMinutes(2).UtcDateTime, SigningCredentials = new X509SigningCredentials(certificate, SecurityAlgorithms.RsaSha256),
                Claims = new Dictionary<string, object>
                {
                    ["sub"] = row.Subject, ["sid"] = row.SessionId, ["jti"] = Guid.CreateVersion7().ToString(),
                    ["events"] = JsonSerializer.Deserialize<JsonElement>("""{"http://schemas.openid.net/event/backchannel-logout":{}}""")
                }
            });
            using var request = new HttpRequestMessage(HttpMethod.Post, client.BackchannelLogoutUri)
                { Content = new FormUrlEncodedContent(new Dictionary<string, string> { ["logout_token"] = token }) };
            using var response = await clients.CreateClient("Qian.Identity.Backchannel").SendAsync(request, ct).ConfigureAwait(false);
            response.EnsureSuccessStatusCode(); row.DeliveredAt = now;
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested) { throw; }
        catch (Exception error)
        {
            DispatchFailed(logger, error.GetType().Name);
            row.NextAttemptAt = DateTimeOffset.UtcNow.AddSeconds(Math.Min(60, row.Attempts));
        }
        await db.SaveChangesAsync(ct).ConfigureAwait(false); await transaction.CommitAsync(ct).ConfigureAwait(false);
        return true;
    }
    [LoggerMessage(EventId = 2001, Level = LogLevel.Error, Message = "后通道登出投递失败，将从持久队列重试；ErrorType={ErrorType}")]
    private static partial void DispatchFailed(ILogger logger, string errorType);
}

using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using Qian.Identity.Abstractions;
using Qian.Runtime.Errors;

namespace Qian.Identity.Server;

// 图形验证码：按登录策略开关。答案只存加盐哈希，单次有效；存数据库，所以多副本部署时任一实例签发的挑战都能在另一实例校验。
internal sealed record CaptchaChallenge(Guid Id, string ImageSvg, DateTimeOffset ExpiresAt);

internal sealed partial class IdentityAccountService
{
    // 去掉形近字符（0/O、1/I/L），用户不会因为看错而被拒。
    private const string CaptchaAlphabet = "ABCDEFGHJKMNPQRSTUVWXYZ23456789";
    private const int CaptchaLength = 5, MaxLiveCaptchas = 20000;
    private static readonly TimeSpan CaptchaLifetime = TimeSpan.FromMinutes(2);

    // 策略要求验证码时统一在这里拦截：缺失或错误都是单独的结构化错误，且不消耗密码尝试次数。
    private async ValueTask<SignInResult?> CheckCaptchaAsync(LoginInput input, AuditSubject subject, Guid? personId, CancellationToken ct)
    {
        if (input.Captcha is null)
            return await RejectAsync(subject, new QianError(IdentityErrors.CaptchaRequired), "captcha_required", personId, ct).ConfigureAwait(false);
        if (!await VerifyCaptchaAsync(input.Captcha, ct).ConfigureAwait(false))
            return await RejectAsync(subject, new QianError(IdentityErrors.CaptchaInvalid), "captcha_invalid", personId, ct).ConfigureAwait(false);
        return null;
    }

    /// <summary>签发一个挑战；策略为 Off 时视为未启用。</summary>
    internal async Task<CaptchaChallenge> IssueCaptchaAsync(CancellationToken ct)
    {
        if (_account.Login.Captcha == CaptchaMode.Off) throw Error(PlatformErrors.NotFound);
        var now = Now; var id = Guid.NewGuid(); var expires = now + CaptchaLifetime;
        var answer = string.Create(CaptchaLength, 0, static (span, _) => { for (var i = 0; i < span.Length; i++) span[i] = CaptchaAlphabet[RandomNumberGenerator.GetInt32(CaptchaAlphabet.Length)]; });
        using var neutral = TenantScope(null);
        await using var session = await OpenAsync(ct).ConfigureAwait(false);
        var db = session.Context;
        // 顺手清理过期挑战；存量过大说明有人在灌，直接限流而不是无限增长。
        await IdentitySql.ExecuteAsync(db, $"DELETE FROM {IdentitySql.Captchas} WHERE expires_at < @cutoff", ct, ("cutoff", now - TimeSpan.FromMinutes(10))).ConfigureAwait(false);
        var live = await IdentitySql.QueryAsync(db, $"SELECT count(*) FROM {IdentitySql.Captchas}", r => r.GetInt64(0), ct).ConfigureAwait(false);
        if (live[0] >= MaxLiveCaptchas) throw Error(PlatformErrors.RateLimited);
        db.Captchas.Add(new CaptchaRow { Id = id, AnswerHash = CaptchaHash(id, answer), ExpiresAt = expires });
        await session.SaveChangesAsync(ct).ConfigureAwait(false);
        return new CaptchaChallenge(id, RenderCaptcha(answer), expires);
    }

    // 删除即消费：无论答对答错，这个挑战都不能再用，防止对同一挑战反复猜测。
    private async Task<bool> VerifyCaptchaAsync(CaptchaAnswer answer, CancellationToken ct)
    {
        if (!Guid.TryParseExact(answer.ChallengeId, "D", out var id) || string.IsNullOrWhiteSpace(answer.Answer) || answer.Answer.Length > 16) return false;
        using var neutral = TenantScope(null);
        await using var session = await OpenAsync(ct).ConfigureAwait(false);
        var stored = await IdentitySql.QueryAsync(session.Context, $"DELETE FROM {IdentitySql.Captchas} WHERE id = @id AND expires_at > @now RETURNING answer_hash",
            r => (byte[])r[0], ct, ("id", id), ("now", Now)).ConfigureAwait(false);
        return stored.Count == 1 && CryptographicOperations.FixedTimeEquals(stored[0], CaptchaHash(id, answer.Answer));
    }

    private static byte[] CaptchaHash(Guid id, string answer) =>
        SHA256.HashData(Encoding.UTF8.GetBytes(id.ToString("D") + ":" + answer.Normalize(NormalizationForm.FormKC).Trim().ToUpperInvariant()));

    // 纯 SVG、无外部字体：逐字随机旋转与偏移，叠加干扰线与点。它只是挡住最廉价的脚本，不是反机器人的终点。
    private static string RenderCaptcha(string answer)
    {
        static int R(int low, int high) => RandomNumberGenerator.GetInt32(low, high);
        var svg = new StringBuilder("<svg xmlns=\"http://www.w3.org/2000/svg\" width=\"160\" height=\"56\" viewBox=\"0 0 160 56\"><rect width=\"160\" height=\"56\" fill=\"#f4f6f8\"/>");
        for (var i = 0; i < 4; i++)
            svg.Append(CultureInfo.InvariantCulture, $"<path d=\"M{R(0, 20)} {R(8, 48)} C{R(40, 70)} {R(0, 56)} {R(90, 120)} {R(0, 56)} {R(140, 160)} {R(8, 48)}\" stroke=\"#{R(0x80, 0xc0):x2}{R(0x80, 0xc0):x2}{R(0x80, 0xc0):x2}\" fill=\"none\" stroke-width=\"1.5\"/>");
        for (var i = 0; i < answer.Length; i++)
        {
            var x = 16 + i * 28 + R(-3, 4); var y = 38 + R(-4, 5);
            svg.Append(CultureInfo.InvariantCulture, $"<text x=\"{x}\" y=\"{y}\" font-family=\"monospace\" font-size=\"{R(28, 35)}\" font-weight=\"bold\" fill=\"#{R(0x10, 0x60):x2}{R(0x10, 0x60):x2}{R(0x10, 0x60):x2}\" transform=\"rotate({R(-22, 23)} {x} {y})\">{answer[i]}</text>");
        }
        for (var i = 0; i < 24; i++)
            svg.Append(CultureInfo.InvariantCulture, $"<circle cx=\"{R(0, 160)}\" cy=\"{R(0, 56)}\" r=\"{R(1, 3)}\" fill=\"#{R(0x60, 0xb0):x2}{R(0x60, 0xb0):x2}{R(0x60, 0xb0):x2}\"/>");
        return svg.Append("</svg>").ToString();
    }
}

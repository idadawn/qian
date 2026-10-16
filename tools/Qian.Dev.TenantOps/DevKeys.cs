using System.Security.Cryptography;
using System.Text.Json;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;

namespace Qian.Dev.TenantOps;

// 本机开发签发方：私钥首次使用时在状态目录生成（仅本人可读），不进仓库，也不打印。
// 令牌形状与集成测试相同（冻结的 S2S claim 集），只被本机手测宿主接受，不代表 Identity 已签发或登录（IDP-002/003）。
internal static class DevKeys
{
    internal const string Issuer = "qian-dev-local";
    internal const string OperationsAudience = "qian-dev-tenant-ops";
    internal const string BusinessAudience = "qian.sample.crm";
    internal const string Superadmin = "root";
    private const string KeyFile = "ops-signing-key.pem";

    internal static RsaSecurityKey LoadOrCreate(string state)
    {
        Directory.CreateDirectory(state);
        var path = Path.Combine(state, KeyFile);
        RSA rsa;
        if (File.Exists(path)) { rsa = RSA.Create(); rsa.ImportFromPem(File.ReadAllText(path)); }
        else
        {
            rsa = RSA.Create(2048);
            var pem = rsa.ExportPkcs8PrivateKeyPem();
            var options = new FileStreamOptions { Mode = FileMode.CreateNew, Access = FileAccess.Write };
            if (!OperatingSystem.IsWindows()) options.UnixCreateMode = UnixFileMode.UserRead | UnixFileMode.UserWrite;
            using var file = new FileStream(path, options);
            using var writer = new StreamWriter(file); writer.Write(pem);
        }
        var key = new RsaSecurityKey(rsa);
        key.KeyId = Base64UrlEncoder.Encode(JsonWebKeyConverter.ConvertFromRSASecurityKey(PublicOnly(key)).ComputeJwkThumbprint());
        return key;
    }

    internal static RsaSecurityKey PublicOnly(RsaSecurityKey key)
    {
        var rsa = RSA.Create(); rsa.ImportParameters(key.Rsa.ExportParameters(false));
        return new RsaSecurityKey(rsa) { KeyId = key.KeyId };
    }

    // CRM 业务宿主的 S2S:PublicJwks：只放公钥。
    internal static string PublicJwks(RsaSecurityKey key)
    {
        var jwk = JsonWebKeyConverter.ConvertFromRSASecurityKey(PublicOnly(key));
        return JsonSerializer.Serialize(new { keys = new[] { new { kty = jwk.Kty, kid = jwk.Kid, n = jwk.N, e = jwk.E } } });
    }

    internal static string Token(RsaSecurityKey key, string subject, string? tenant, string kind, TimeSpan lifetime, string audience = OperationsAudience)
    {
        var now = DateTimeOffset.UtcNow.ToUnixTimeSeconds(); var id = Guid.CreateVersion7().ToString();
        var payload = new Dictionary<string, object?>
        {
            ["iss"] = Issuer, ["aud"] = new[] { audience }, ["sub"] = subject, ["principal_kind"] = kind, ["tenant_id"] = tenant,
            ["roles"] = Array.Empty<string>(), ["scope"] = "", ["session_id"] = id, ["jti"] = id,
            ["iat"] = now, ["nbf"] = now, ["exp"] = now + (long)lifetime.TotalSeconds
        };
        return new JsonWebTokenHandler().CreateToken(JsonSerializer.Serialize(payload), new SigningCredentials(key, SecurityAlgorithms.RsaSha256));
    }
}

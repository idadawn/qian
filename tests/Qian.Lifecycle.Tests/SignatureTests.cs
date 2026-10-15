using System.Text;
using System.Text.Json;
using Qian.Registry;
using Qian.Runtime.Errors;
using Qian.Testing.Containers;

namespace Qian.Lifecycle.Tests;

// S1-RT-005 验收 3：强制验签——未签名、签名不符（签的是别的内容 / 内容被改 / 公钥不在信任根 / 签名字节被改）一律安装失败且不留任何记录；
// 信任根经配置节 Trust:PlatformKeys 引用 K8s Secret，轮换时新旧公钥有效期重叠，重叠期内旧签名仍有效，过期或撤下后失效。
public sealed class SignatureTests(PostgresFixture postgres) : IClassFixture<PostgresFixture>
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task 未签名与签名不符的制品安装失败_不登记制品也不留修订_正确签名安装成功且回写注册表()
    {
        await using var host = await LifecycleHost.StartAsync(postgres, ct: Ct);
        var platform = new SigningKey();
        host.Trust(0, "k1", platform, host.Time.Now.AddDays(-1));
        var files = Artifacts.Declarative("acme.notes", "1.0.0");
        var digest = Artifacts.Digest(files);

        await AssertCode(LifecycleErrors.ArtifactUnsigned, () => host.Lifecycle.InstallAsync(new PluginArtifact(files, digest, null), "operator", Ct).AsTask());
        // 签名不符：签的是另一份内容的摘要。
        var other = Artifacts.Declarative("acme.notes", "1.0.1");
        await AssertCode(LifecycleErrors.SignatureInvalid, () => host.Lifecycle.InstallAsync(new PluginArtifact(files, digest, platform.Sign(Artifacts.Digest(other))), "operator", Ct).AsTask());
        // 内容被改：沿用原签名，摘要随内容重算后与签名对不上；沿用旧摘要则在验签之前因内容与摘要不一致被拒。
        var tampered = new Dictionary<string, ReadOnlyMemory<byte>>(files, StringComparer.Ordinal) { ["README.md"] = Encoding.UTF8.GetBytes("injected") };
        await AssertCode(LifecycleErrors.SignatureInvalid, () => host.Lifecycle.InstallAsync(new PluginArtifact(tampered, Artifacts.Digest(tampered), platform.Sign(digest)), "operator", Ct).AsTask());
        await AssertCode(PlatformErrors.InputInvalid, () => host.Lifecycle.InstallAsync(new PluginArtifact(tampered, digest, platform.Sign(digest)), "operator", Ct).AsTask());
        // 公钥不在信任根中；签名字节被改一位；KeyId 冒用信任根指纹但签名来自别的私钥。
        var stranger = new SigningKey();
        await AssertCode(LifecycleErrors.SignatureInvalid, () => host.Lifecycle.InstallAsync(new PluginArtifact(files, digest, stranger.Sign(digest)), "operator", Ct).AsTask());
        var flipped = platform.Sign(digest).Value.ToArray(); flipped[10] ^= 0x01;
        await AssertCode(LifecycleErrors.SignatureInvalid, () => host.Lifecycle.InstallAsync(new PluginArtifact(files, digest, new ArtifactSignature(platform.Fingerprint, flipped)), "operator", Ct).AsTask());
        await AssertCode(LifecycleErrors.SignatureInvalid, () => host.Lifecycle.InstallAsync(new PluginArtifact(files, digest, new ArtifactSignature(platform.Fingerprint, stranger.Sign(digest).Value)), "operator", Ct).AsTask());

        // 失败的安装不经 Registry 登记，也不写修订。
        Assert.Empty(await host.Lifecycle.GetRevisionsAsync("acme.notes", Ct));
        Assert.Equal(0, await LifecycleHost.CountAsync(postgres, "SELECT count(*) FROM plugin_qian_registry.plugin_artifacts", Ct));
        Assert.Equal(0, await LifecycleHost.CountAsync(postgres, "SELECT count(*) FROM plugin_qian_lifecycle.plugin_revisions", Ct));

        var installed = await host.Lifecycle.InstallAsync(new PluginArtifact(files, digest, platform.Sign(digest)), "operator", Ct);
        Assert.Equal(("acme.notes", 1, "1.0.0", PluginRevisionStates.Installed, "declarative", platform.Fingerprint, digest),
            (installed.PluginId, installed.Revision, installed.Version, installed.State, installed.Form, installed.SignedBy, installed.ArtifactDigest));
        // 验签结论与安装状态回写 Registry 的制品目录记录。
        Assert.Equal(RegistryArtifactStates.Installed, await LifecycleHost.ScalarAsync(postgres, $"SELECT state FROM plugin_qian_registry.plugin_artifacts WHERE digest = '{digest}'", Ct));
        var verification = JsonSerializer.Deserialize<JsonElement>((await LifecycleHost.ScalarAsync(postgres,
            $"SELECT signature_verification FROM plugin_qian_registry.plugin_artifacts WHERE digest = '{digest}'", Ct))!);
        Assert.Equal(("ed25519", platform.Fingerprint), (verification.GetProperty("algorithm").GetString(), verification.GetProperty("keyId").GetString()));
        // 同一摘要重复安装返回已有修订，不新增记录。
        Assert.Equal(1, (await host.Lifecycle.InstallAsync(new PluginArtifact(files, digest, platform.Sign(digest)), "operator", Ct)).Revision);
        Assert.Single(await host.Lifecycle.GetRevisionsAsync("acme.notes", Ct));
        // 秘密解析留有审计记录（只有引用与结论，不含公钥内容）。
        Assert.Contains(host.Secrets.Events, access => access is { Caller: "qian.lifecycle", Reference: "secretRef://platform/trust/k1", Allowed: true });
    }

    [Fact]
    public async Task 信任根轮换_重叠期内旧公钥签名仍有效_过期与撤下后失效_新公钥生效前不可信()
    {
        await using var host = await LifecycleHost.StartAsync(postgres, ct: Ct);
        var start = host.Time.Now;
        var old = new SigningKey(); var next = new SigningKey();
        host.Trust(0, "k1", old, start.AddDays(-30), start.AddDays(10));
        Assert.Equal(1, (await host.Lifecycle.InstallAsync(Artifacts.Signed(Artifacts.Declarative("acme.notes", "1.0.0"), old), "operator", Ct)).Revision);

        // 轮换：发布新公钥，有效期从一天后开始，与旧公钥重叠九天；生效之前新公钥的签名不被接受。
        host.Trust(1, "k2", next, start.AddDays(1));
        await AssertCode(LifecycleErrors.SignatureInvalid, () => host.Lifecycle.InstallAsync(Artifacts.Signed(Artifacts.Declarative("acme.notes", "1.0.1"), next), "operator", Ct).AsTask());

        host.Time.Now = start.AddDays(5);
        Assert.Equal((2, old.Fingerprint), Pick(await host.Lifecycle.InstallAsync(Artifacts.Signed(Artifacts.Declarative("acme.notes", "1.0.1"), old), "operator", Ct)));
        Assert.Equal((3, next.Fingerprint), Pick(await host.Lifecycle.InstallAsync(Artifacts.Signed(Artifacts.Declarative("acme.notes", "1.0.2"), next), "operator", Ct)));

        // 重叠期结束：旧公钥的签名失效，新公钥照常。
        host.Time.Now = start.AddDays(11);
        await AssertCode(LifecycleErrors.SignatureInvalid, () => host.Lifecycle.InstallAsync(Artifacts.Signed(Artifacts.Declarative("acme.notes", "1.0.3"), old), "operator", Ct).AsTask());
        Assert.Equal((4, next.Fingerprint), Pick(await host.Lifecycle.InstallAsync(Artifacts.Signed(Artifacts.Declarative("acme.notes", "1.0.3"), next), "operator", Ct)));

        // 撤下新公钥（配置删除即生效，不重启）：它签名的制品随即不可安装；已安装的修订不受影响。
        host.Distrust(1);
        await AssertCode(LifecycleErrors.SignatureInvalid, () => host.Lifecycle.InstallAsync(Artifacts.Signed(Artifacts.Declarative("acme.notes", "1.0.4"), next), "operator", Ct).AsTask());
        Assert.Equal(["1.0.0", "1.0.1", "1.0.2", "1.0.3"], (await host.Lifecycle.GetRevisionsAsync("acme.notes", Ct)).Select(revision => revision.Version));
        static (int, string) Pick(PluginRevisionView view) => (view.Revision, view.SignedBy);
    }

    internal static async Task AssertCode(ErrorCode code, Func<Task> action)
    {
        var error = await Assert.ThrowsAsync<QianErrorException>(action);
        Assert.Equal(code.Code, error.Error.Code.Code);
    }
}

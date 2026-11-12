using System.Text.Json;
using Kun.Core.MultiTenancy;
using Qian.Lifecycle;
using Qian.Registry;
using Qian.Runtime.Errors;

namespace Qian.Platform.Host;

/// <summary>
/// 系统组件修订的安装 Job（<c>--install-system-revisions</c>，S1-DEP-011，R151 ①）：与 <c>--migrate-only</c> 同形，每次部署在迁移之后、在线副本之前运行，跑完退出。
/// 开户编排第 03 步把宿主预装的 system 级组件逐个开通进租户，Lifecycle 要求每个组件有已激活、经信任根验签、摘要等于预装组件文件夹的修订；
/// 这一步把镜像里的预装组件按签名清单装成修订并激活。
/// <list type="bullet">
/// <item>签名清单（<c>SystemRevisions:SignaturesFile</c>，ConfigMap 挂载）由 <c>scripts/sign-system-revisions.py</c> 在构建机上对同一镜像生成；私钥不进集群，公钥经 Secret 进
/// Lifecycle 信任根（<c>Trust:PlatformKeys</c>）。清单缺任何一个预装组件的签名即失败，并列出全部缺项。</item>
/// <item>幂等：同摘要的修订已激活则不动；已安装未激活的只激活；新摘要（镜像换了组件文件夹）安装后激活，旧修订按 Lifecycle 的规则保留可回切。</item>
/// <item>安装与激活经 Lifecycle 写平台审计链，载荷里的操作者是 <c>platform-release</c>。</item>
/// </list>
/// 不启动 HTTP 监听，跑完即逆序卸载插件树。正式的发布链签名与信任根轮换归 S1-DEP-005。
/// </summary>
internal static class PlatformSystemRevisions
{
    internal const string Argument = "--install-system-revisions";
    internal const string SignaturesFileKey = "SystemRevisions:SignaturesFile";
    internal const string Format = "qian.system-revisions.signatures/v1";
    private const string Actor = "platform-release";

    internal static bool IsRequested(IEnumerable<string> args) => args.Contains(Argument, StringComparer.Ordinal);

    internal static async Task RunAsync(WebApplication app, CancellationToken ct = default)
    {
        var configuration = app.Services.GetRequiredService<IConfiguration>();
        var signatures = Signatures.Read(configuration[SignaturesFileKey] is { Length: > 0 } file ? file
            : throw new InvalidOperationException("安装系统组件修订要求 " + SignaturesFileKey + "（签名清单的文件路径）。"));
        var runtime = app.Services.GetRequiredService<PlatformRuntime>();
        var logger = app.Services.GetRequiredService<ILogger<PlatformRuntime>>();
        try
        {
            await runtime.StartAsync(ct).ConfigureAwait(false);
            using var neutral = app.Services.GetRequiredService<ITenantContext>().BeginScope(null);
            var components = runtime.Global("registry").Registry().GetInstalledComponents().Where(SystemModule)
                .OrderBy(component => component.PluginId, StringComparer.Ordinal).ToArray();
            var missing = components.Where(component => signatures.Find(component) is null)
                .Select(component => component.PluginId + "@" + component.Version + "（" + (component.ArtifactDigest ?? "无摘要") + "）").ToArray();
            if (missing.Length > 0)
                throw new InvalidOperationException("签名清单缺少这些预装组件的签名（清单须由 scripts/sign-system-revisions.py 对本镜像生成）：" + string.Join("、", missing));
            var lifecycle = runtime.Global("lifecycle").Lifecycle();
            int installed = 0, activated = 0, unchanged = 0;
            foreach (var component in components)
            {
                var digest = component.ArtifactDigest!;
                var revision = (await lifecycle.GetRevisionsAsync(component.PluginId, ct).ConfigureAwait(false)).SingleOrDefault(item => item.ArtifactDigest == digest);
                var existed = revision is not null;
                try
                {
                    if (revision is null)
                    {
                        var files = component.FilePaths.ToDictionary(path => path, path => new ReadOnlyMemory<byte>(component.ReadFile(path)!), StringComparer.Ordinal);
                        revision = await lifecycle.InstallAsync(new PluginArtifact(files, digest, new ArtifactSignature(signatures.KeyId, signatures.Find(component)!)), Actor, ct)
                            .ConfigureAwait(false);
                        installed++;
                    }
                    if (revision.State == PluginRevisionStates.Active)
                    {
                        if (existed) unchanged++;
                        continue;
                    }
                    await lifecycle.ActivateAsync(revision.PluginId, revision.Revision, Actor, ct).ConfigureAwait(false);
                    activated++;
                }
                catch (QianErrorException error)
                {
                    // 错误码与逐项字段错误一并带出：验签失败、制品校验不过、修订冲突等都要让运维看得出是哪个组件、哪一项。
                    var fields = string.Join("；", error.Error.Errors.Select(item =>
                        item.Pointer + " " + item.Code.Code + (item.Args.Count == 0 ? "" : "（" + string.Join("，", item.Args.Select(arg => arg.Key + "=" + arg.Value)) + "）")));
                    throw new InvalidOperationException("安装或激活 " + component.PluginId + "@" + component.Version + " 失败：" + error.Error.Code.Code +
                        (fields.Length == 0 ? "" : "：" + fields), error);
                }
            }
            SystemRevisionsLog.Done(logger, components.Length, installed, activated, unchanged);
        }
        finally
        {
            await runtime.DisposeAsync().ConfigureAwait(false);
        }
    }

    // 宿主预装的 system 级 module 组件（与签名脚本选组件的口径相同；骨架清单进不了预装绑定，不必再判）。
    private static bool SystemModule(InstalledComponentMetadata component) =>
        Text(component.Manifest, "policy", "tier") == "system" && Text(component.Manifest, "runtime", "form") == "module";

    private static string? Text(JsonElement element, string parent, string name) =>
        element.TryGetProperty(parent, out var section) && section.ValueKind == JsonValueKind.Object && section.TryGetProperty(name, out var value) &&
        value.ValueKind == JsonValueKind.String ? value.GetString() : null;

    // 签名清单：{"format":"qian.system-revisions.signatures/v1","keyId":"sha256:…","source":"…","artifacts":[{"plugin","version","digest","signature"}]}。
    // signature 是对 D4 目录摘要 32 字节原始值的 Ed25519 签名（Base64，64 字节）；验签由 Lifecycle 对信任根做，这里只核对形状与重复。
    private sealed class Signatures
    {
        private readonly Dictionary<(string Plugin, string Version, string Digest), byte[]> _items = [];
        internal string KeyId { get; private init; } = "";

        internal byte[]? Find(InstalledComponentMetadata component) =>
            component.ArtifactDigest is { } digest && _items.TryGetValue((component.PluginId, component.Version, digest), out var value) ? value : null;

        internal static Signatures Read(string path)
        {
            using var document = JsonDocument.Parse(File.ReadAllBytes(path));
            var root = document.RootElement;
            if (root.ValueKind != JsonValueKind.Object || Field(root, "format") != Format)
                throw Invalid("format 必须是 " + Format + "。");
            var keyId = Field(root, "keyId");
            if (keyId is not { Length: 71 } || !keyId.StartsWith("sha256:", StringComparison.Ordinal)) throw Invalid("keyId 必须是 sha256:<64 位小写十六进制>。");
            if (!root.TryGetProperty("artifacts", out var artifacts) || artifacts.ValueKind != JsonValueKind.Array) throw Invalid("缺少 artifacts 数组。");
            var result = new Signatures { KeyId = keyId };
            foreach (var item in artifacts.EnumerateArray())
            {
                var plugin = Field(item, "plugin"); var version = Field(item, "version"); var digest = Field(item, "digest"); var signature = Field(item, "signature");
                if (plugin is null || version is null || digest is not { Length: 71 } || signature is null) throw Invalid("artifacts 的每一项都要有 plugin、version、digest 与 signature。");
                byte[] value;
                try { value = Convert.FromBase64String(signature); }
                catch (FormatException) { throw Invalid(plugin + " 的 signature 不是 Base64。"); }
                if (value.Length != 64) throw Invalid(plugin + " 的 signature 不是 64 字节的 Ed25519 签名。");
                if (!result._items.TryAdd((plugin, version, digest), value)) throw Invalid(plugin + "@" + version + " 重复出现。");
            }
            return result;
        }

        private static string? Field(JsonElement element, string name) =>
            element.ValueKind == JsonValueKind.Object && element.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String ? value.GetString() : null;

        private static InvalidOperationException Invalid(string reason) => new("签名清单不合格：" + reason);
    }
}

internal static partial class SystemRevisionsLog
{
    [LoggerMessage(EventId = 3, Level = LogLevel.Information,
        Message = "系统组件修订已就绪：{Count} 个预装组件，新安装 {Installed}、新激活 {Activated}、原已激活 {Unchanged}。")]
    internal static partial void Done(ILogger logger, int count, int installed, int activated, int unchanged);
}

using System.Security.Cryptography;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;

namespace Qian.Lifecycle;

// runtime.form: service 修订的最小 Kustomize 片段：Deployment + Service + kustomization。资源、NetworkPolicy、迁移 Job 等完整片段由
// qian pack 的部署任务（S1-DEP-004）生成；这里只产出让 Argo 收敛、让健康观测有据可依的部分。标量一律写成 JSON 字符串（合法 YAML）。
internal static class KustomizeFragment
{
    internal const string ContainerPort = "http";

    // 工作负载名含版本：同一组件的多个版本可以同时运行，租户按自己的版本路由。超长时截断并加摘要后缀，保持 DNS-1123 标签。
    internal static string Workload(string pluginId, string version)
    {
        var builder = new StringBuilder();
        foreach (var c in (pluginId + "-" + version).ToLowerInvariant())
        {
            var next = char.IsAsciiLetterLower(c) || char.IsAsciiDigit(c) ? c : '-';
            if (next == '-' && (builder.Length == 0 || builder[^1] == '-')) continue;
            builder.Append(next);
        }
        var name = builder.ToString().Trim('-');
        if (name.Length == 0 || !char.IsAsciiLetterLower(name[0])) name = "p-" + name;
        if (name.Length <= 63) return name;
        var suffix = Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(pluginId + "@" + version)))[..8];
        return name[..54].TrimEnd('-') + "-" + suffix;
    }

    // 同一请求总是生成逐字节相同的文件，重复提交不产生新的提交。
    internal static IReadOnlyDictionary<string, string> Render(DeploymentRequest request, string workload, string? ns)
    {
        var labels = new[] { ("app.kubernetes.io/name", workload), ("app.kubernetes.io/part-of", "qian"), ("app.kubernetes.io/managed-by", "qian.lifecycle") };
        var annotations = new[]
        {
            ("qian.dev/plugin", request.PluginId), ("qian.dev/version", request.Version),
            ("qian.dev/revision", request.Revision.ToString(System.Globalization.CultureInfo.InvariantCulture)), ("qian.dev/artifact-digest", request.ArtifactDigest)
        };
        var deployment = new StringBuilder()
            .Append("apiVersion: apps/v1\nkind: Deployment\nmetadata:\n  name: ").Append(Q(workload)).Append('\n')
            .Append(Map("  ", "labels", labels)).Append(Map("  ", "annotations", annotations))
            .Append("spec:\n  replicas: 1\n  selector:\n    matchLabels:\n      app.kubernetes.io/name: ").Append(Q(workload)).Append('\n')
            .Append("  template:\n    metadata:\n").Append(Map("      ", "labels", labels)).Append(Map("      ", "annotations", annotations))
            .Append("    spec:\n      automountServiceAccountToken: false\n      containers:\n      - name: \"plugin\"\n        image: ").Append(Q(request.Image)).Append('\n')
            .Append("        ports:\n        - name: ").Append(Q(ContainerPort)).Append("\n          containerPort: ").Append(request.Port).Append('\n')
            .Append("        readinessProbe:\n          httpGet:\n            path: ").Append(Q(request.HealthPath)).Append("\n            port: ").Append(Q(ContainerPort)).Append('\n')
            .Append("          periodSeconds: 5\n")
            .Append("        securityContext:\n          allowPrivilegeEscalation: false\n          capabilities:\n            drop: [\"ALL\"]\n          seccompProfile:\n            type: \"RuntimeDefault\"\n")
            .ToString();
        var service = new StringBuilder()
            .Append("apiVersion: v1\nkind: Service\nmetadata:\n  name: ").Append(Q(workload)).Append('\n').Append(Map("  ", "labels", labels))
            .Append("spec:\n  selector:\n    app.kubernetes.io/name: ").Append(Q(workload)).Append('\n')
            .Append("  ports:\n  - name: ").Append(Q(ContainerPort)).Append("\n    port: ").Append(request.Port).Append("\n    targetPort: ").Append(Q(ContainerPort)).Append('\n')
            .ToString();
        var kustomization = "apiVersion: kustomize.config.k8s.io/v1beta1\nkind: Kustomization\n" + (ns is null ? "" : "namespace: " + Q(ns) + "\n") +
            "resources:\n- deployment.yaml\n- service.yaml\n";
        return new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["deployment.yaml"] = deployment, ["service.yaml"] = service, ["kustomization.yaml"] = kustomization
        };
    }

    // 根 kustomization 只由本执行器维护：资源列表即各工作负载目录，按序数排序，已有条目保留。
    internal static string Root(IEnumerable<string> workloads) => "apiVersion: kustomize.config.k8s.io/v1beta1\nkind: Kustomization\nresources:\n" +
        string.Concat(workloads.Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal).Select(name => "- " + name + "\n"));

    internal static IReadOnlyList<string> RootResources(string content) => content.Split('\n')
        .Select(line => line.Trim()).Where(line => line.StartsWith("- ", StringComparison.Ordinal)).Select(line => line[2..].Trim())
        .Where(name => name.Length is > 0 and <= 63 && name.All(c => char.IsAsciiLetterLower(c) || char.IsAsciiDigit(c) || c == '-')).ToArray();

    // 期望状态的内容摘要：Local 执行器的提交凭据。
    internal static string Fingerprint(IReadOnlyDictionary<string, string> files) => "sha256:" + Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(
        string.Concat(files.OrderBy(file => file.Key, StringComparer.Ordinal).Select(file => file.Key + "\n" + file.Value + "\n")))));

    private static string Map(string indent, string name, IEnumerable<(string Key, string Value)> items) =>
        indent + name + ":\n" + string.Concat(items.Select(item => indent + "  " + item.Key + ": " + Q(item.Value) + "\n"));

    // JSON 双引号字符串即 YAML 双引号标量；不做 HTML 转义，片段保持可读。
    private static readonly JsonSerializerOptions Scalar = new() { Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping };
    private static string Q(string value) => JsonSerializer.Serialize(value, Scalar);
}

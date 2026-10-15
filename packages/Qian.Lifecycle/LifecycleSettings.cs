using System.Text.Json;
using Qian.Config;
using Qian.Runtime;
using Qian.Runtime.Errors;

namespace Qian.Lifecycle;

// qian.lifecycle 条目配置（plugins/qian.lifecycle/schemas/config.schema.json）：profile 装载已按 Schema 校验，这里再严格解析一次，未知字段一律拒绝。
internal sealed class LifecycleSettings
{
    internal TimeSpan RunLease { get; private init; } = TimeSpan.FromMinutes(10);
    internal ExecutorSettings? Executor { get; private init; }

    internal static LifecycleSettings Parse(JsonElement config)
    {
        if (config.ValueKind is JsonValueKind.Undefined or JsonValueKind.Null) return new LifecycleSettings();
        if (config.ValueKind != JsonValueKind.Object) throw Invalid("#", "配置必须是对象。");
        var lease = TimeSpan.FromMinutes(10); ExecutorSettings? executor = null;
        foreach (var property in config.EnumerateObject())
        {
            switch (property.Name)
            {
                case "runLeaseSeconds": lease = TimeSpan.FromSeconds(Integer(property.Value, "#/runLeaseSeconds", 30, 3600)); break;
                case "executor": executor = ExecutorSettings.Parse(property.Value); break;
                default: throw Invalid("#/" + property.Name, "未知配置项。");
            }
        }
        return new LifecycleSettings { RunLease = lease, Executor = executor };
    }

    internal static int Integer(JsonElement value, string pointer, int minimum, int maximum) =>
        value.ValueKind == JsonValueKind.Number && value.TryGetInt32(out var number) && number >= minimum && number <= maximum
            ? number : throw Invalid(pointer, $"必须是 {minimum}–{maximum} 的整数。");

    internal static string Text(JsonElement value, string pointer, int maximum = 2048) =>
        value.ValueKind == JsonValueKind.String && value.GetString() is { Length: > 0 } text && text.Length <= maximum && !text.Any(char.IsControl)
            ? text : throw Invalid(pointer, "必须是非空字符串。");

    internal static string Secret(JsonElement value, string pointer) =>
        Text(value, pointer) is var text && SecretReference.IsValid(text) && text.StartsWith("secretRef://platform/", StringComparison.Ordinal)
            ? text : throw Invalid(pointer, "必须是 secretRef://platform/… 引用。");

    internal static QianErrorException Invalid(string pointer, string reason) => new(new QianError(PlatformErrors.InputInvalid)
    {
        Errors = [new QianFieldError(LifecycleErrors.Invalid, "plugins/qian.lifecycle/config" + pointer, new Dictionary<string, string> { ["reason"] = reason })]
    });
}

// 部署执行器配置：gitops 提交 Kustomize 片段到部署仓库并读 K8s API；local 把同样的片段写到本机目录并直接探测本机端点。
internal abstract class ExecutorSettings
{
    internal TimeSpan HealthTimeout { get; private protected init; } = TimeSpan.FromSeconds(5);

    internal abstract IDeploymentExecutor Create(IPluginContext context, TimeProvider time);

    internal static ExecutorSettings Parse(JsonElement value)
    {
        if (value.ValueKind != JsonValueKind.Object || !value.TryGetProperty("kind", out var kind) || kind.ValueKind != JsonValueKind.String)
            throw LifecycleSettings.Invalid("#/executor/kind", "执行器必须写明 kind。");
        return kind.GetString() switch
        {
            "gitops" => GitOpsSettings.Read(value),
            "local" => LocalSettings.Read(value),
            _ => throw LifecycleSettings.Invalid("#/executor/kind", "执行器只能是 gitops 或 local。")
        };
    }
}

internal sealed class GitOpsSettings : ExecutorSettings
{
    internal required string Repository { get; init; }
    internal string Branch { get; private init; } = "main";
    internal string Path { get; private init; } = "apps/qian-plugins";
    internal string Namespace { get; private init; } = "qian-plugins";
    internal string? WorkingDirectory { get; private init; }
    internal required Uri Server { get; init; }
    internal required string Token { get; init; }
    internal string? CertificateAuthority { get; private init; }

    internal override IDeploymentExecutor Create(IPluginContext context, TimeProvider time) => new GitOpsDeploymentExecutor(this, context);

    internal static GitOpsSettings Read(JsonElement value)
    {
        string? repository = null, branch = null, path = null, ns = null, working = null, token = null, authority = null;
        Uri? server = null; var timeout = 5;
        foreach (var property in value.EnumerateObject())
        {
            var pointer = "#/executor/" + property.Name;
            switch (property.Name)
            {
                case "kind": break;
                case "repository": repository = Repo(LifecycleSettings.Text(property.Value, pointer), pointer); break;
                case "branch": branch = Name(LifecycleSettings.Text(property.Value, pointer, 200), pointer); break;
                case "path": path = RelativePath(LifecycleSettings.Text(property.Value, pointer, 400), pointer); break;
                case "namespace": ns = Label(LifecycleSettings.Text(property.Value, pointer, 63), pointer); break;
                case "workingDirectory": working = Absolute(LifecycleSettings.Text(property.Value, pointer), pointer); break;
                case "healthTimeoutSeconds": timeout = LifecycleSettings.Integer(property.Value, pointer, 1, 60); break;
                case "kubernetes":
                    if (property.Value.ValueKind != JsonValueKind.Object) throw LifecycleSettings.Invalid(pointer, "必须是对象。");
                    foreach (var item in property.Value.EnumerateObject())
                    {
                        var inner = pointer + "/" + item.Name;
                        switch (item.Name)
                        {
                            case "server":
                                server = Uri.TryCreate(LifecycleSettings.Text(item.Value, inner), UriKind.Absolute, out var uri) && uri.Scheme == Uri.UriSchemeHttps &&
                                    uri.UserInfo.Length == 0 && uri.Query.Length == 0 && uri.Fragment.Length == 0 ? uri : throw LifecycleSettings.Invalid(inner, "必须是不含凭据的 https 地址。");
                                break;
                            case "token": token = LifecycleSettings.Secret(item.Value, inner); break;
                            case "certificateAuthority": authority = LifecycleSettings.Secret(item.Value, inner); break;
                            default: throw LifecycleSettings.Invalid(inner, "未知配置项。");
                        }
                    }
                    break;
                default: throw LifecycleSettings.Invalid(pointer, "未知配置项。");
            }
        }
        if (repository is null) throw LifecycleSettings.Invalid("#/executor/repository", "gitops 执行器必须配置部署仓库。");
        if (server is null || token is null) throw LifecycleSettings.Invalid("#/executor/kubernetes", "gitops 执行器必须配置 K8s API 地址与令牌引用。");
        return new GitOpsSettings
        {
            Repository = repository, Branch = branch ?? "main", Path = path ?? "apps/qian-plugins", Namespace = ns ?? "qian-plugins", WorkingDirectory = working,
            Server = server, Token = token, CertificateAuthority = authority, HealthTimeout = TimeSpan.FromSeconds(timeout)
        };
    }

    // 远端仓库只接受不含凭据的 https / ssh 地址或本机绝对路径；凭据由宿主环境的 git 凭据助手提供。
    private static string Repo(string value, string pointer)
    {
        if (System.IO.Path.IsPathFullyQualified(value)) return value;
        if (Uri.TryCreate(value, UriKind.Absolute, out var uri) && uri.Scheme is "https" or "ssh" && uri.UserInfo.Length == 0 && uri.Query.Length == 0 && uri.Fragment.Length == 0)
            return value;
        throw LifecycleSettings.Invalid(pointer, "部署仓库必须是不含凭据的 https / ssh 地址或本机绝对路径。");
    }

    private static string Name(string value, string pointer) =>
        value.All(c => char.IsAsciiLetterOrDigit(c) || c is '-' or '_' or '.' or '/') && !value.StartsWith('-') && !value.Contains("..", StringComparison.Ordinal)
            ? value : throw LifecycleSettings.Invalid(pointer, "分支名无效。");

    internal static string Label(string value, string pointer) =>
        value.Length <= 63 && value.All(c => char.IsAsciiLetterLower(c) || char.IsAsciiDigit(c) || c == '-') && char.IsAsciiLetterLower(value[0]) && value[^1] != '-'
            ? value : throw LifecycleSettings.Invalid(pointer, "必须是 DNS-1123 标签。");

    private static string RelativePath(string value, string pointer)
    {
        var segments = value.Split('/');
        return segments.All(segment => segment.Length > 0 && segment is not ("." or "..") && segment.All(c => char.IsAsciiLetterOrDigit(c) || c is '-' or '_' or '.'))
            ? value : throw LifecycleSettings.Invalid(pointer, "必须是仓库内的相对路径。");
    }

    internal static string Absolute(string value, string pointer) =>
        System.IO.Path.IsPathFullyQualified(value) ? value : throw LifecycleSettings.Invalid(pointer, "必须是绝对路径。");
}

internal sealed class LocalSettings : ExecutorSettings
{
    internal required string StateDirectory { get; init; }
    internal IReadOnlyDictionary<string, Uri> Endpoints { get; private init; } = new Dictionary<string, Uri>();

    internal override IDeploymentExecutor Create(IPluginContext context, TimeProvider time) => new LocalDeploymentExecutor(this);

    internal static LocalSettings Read(JsonElement value)
    {
        string? directory = null; var endpoints = new Dictionary<string, Uri>(StringComparer.Ordinal); var timeout = 5;
        foreach (var property in value.EnumerateObject())
        {
            var pointer = "#/executor/" + property.Name;
            switch (property.Name)
            {
                case "kind": break;
                case "stateDirectory": directory = GitOpsSettings.Absolute(LifecycleSettings.Text(property.Value, pointer), pointer); break;
                case "healthTimeoutSeconds": timeout = LifecycleSettings.Integer(property.Value, pointer, 1, 60); break;
                case "endpoints":
                    if (property.Value.ValueKind != JsonValueKind.Object) throw LifecycleSettings.Invalid(pointer, "必须是对象。");
                    foreach (var item in property.Value.EnumerateObject())
                    {
                        var inner = pointer + "/" + item.Name;
                        endpoints[item.Name] = Uri.TryCreate(LifecycleSettings.Text(item.Value, inner), UriKind.Absolute, out var uri) && uri.Scheme is "http" or "https" &&
                            uri.UserInfo.Length == 0 && uri.AbsolutePath == "/" && uri.Query.Length == 0 && uri.Fragment.Length == 0
                            ? uri : throw LifecycleSettings.Invalid(inner, "必须是不含路径与凭据的 http(s) 地址。");
                    }
                    break;
                default: throw LifecycleSettings.Invalid(pointer, "未知配置项。");
            }
        }
        if (directory is null) throw LifecycleSettings.Invalid("#/executor/stateDirectory", "local 执行器必须配置期望状态目录。");
        return new LocalSettings { StateDirectory = directory, Endpoints = endpoints, HealthTimeout = TimeSpan.FromSeconds(timeout) };
    }
}

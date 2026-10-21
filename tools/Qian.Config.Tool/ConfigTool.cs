using System.Globalization;
using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Qian.Profiles;
using Qian.Profiles.Abstractions;
using Qian.Surfaces;

namespace Qian.Config.Tool;

// 由 profile 装配结果推导每个宿主的 Apollo namespace 骨架（PRD 7.8：插件 config.schema → namespace 骨架与校验器），可选写出骨架文件、
// 与 ConfigService 上的线上键比对。global 条目里配置 Schema 有属性的组件各得一个 namespace qian.plugin.<组件 id>；
// 配置组件自身（qian.config、qian.config.*）的条目是引导配置，不进 Apollo。输出只有键名、计数与 Schema 指针，不读取也不回显配置值。
internal static class ConfigTool
{
    internal const string Usage = "用法：Qian.Config.Tool <仓库根> [--profile <名称>]... [--out <目录>]\n" +
        "      Qian.Config.Tool <仓库根> --compare <ConfigService 地址> [--profile <名称>]... [--app-id <AppId>] [--cluster <集群>] [--access-key-env <环境变量名>]\n" +
        "      Qian.Config.Tool <仓库根> --compare <导出的 JSON 文件> --profile <名称>";
    internal const string NamespacePrefix = "qian.plugin.";
    // ApolloConfigDB：AppNamespace.Name / Namespace.NamespaceName 为 varchar(32)，Item.Key 为 varchar(128)。
    internal const int NamespaceLimit = 32;
    internal const int KeyLimit = 128;
    private const string ApolloPlugin = "qian.config.apollo";

    internal static async Task<int> RunAsync(string[] args, TextWriter output, TextWriter error, CancellationToken ct = default)
    {
        if (ToolOptions.Parse(args) is not { } options) { await error.WriteLineAsync(Usage).ConfigureAwait(false); return 2; }
        try
        {
            var (profiles, diagnostics) = Plan(options);
            if ((options.AppId is not null || options.CompareFile is not null) && profiles.Count(profile => profile.AppId is not null) > 1)
            { await error.WriteLineAsync("--app-id 与导出的 JSON 只能与单个启用 Apollo 的 profile 一起使用。").ConfigureAwait(false); return 2; }
            if (options.Output is { } directory) Write(directory, profiles);
            var findings = new List<KeyFinding>();
            var online = new Dictionary<(string, string), string>();
            if (options.CompareFile is { } file)
            {
                var exported = ReadExport(file);
                foreach (var profile in profiles.Where(profile => profile.AppId is not null))
                    foreach (var plan in profile.Namespaces.Where(plan => plan.Skeleton.Name.Length <= NamespaceLimit))
                        Collect(profile, plan, exported.GetValueOrDefault(plan.Skeleton.Name));
            }
            if (options.Compare is { } server)
            {
                var secret = options.AccessKeyVariable is { } variable ? Environment.GetEnvironmentVariable(variable) : null;
                if (options.AccessKeyVariable is not null && string.IsNullOrEmpty(secret))
                { await error.WriteLineAsync("--access-key-env 指定的环境变量为空，拒绝发送未签名请求。").ConfigureAwait(false); return 2; }
                using var client = new HttpClient { BaseAddress = new Uri(server.AbsoluteUri.TrimEnd('/') + "/"), Timeout = TimeSpan.FromSeconds(30) };
                foreach (var profile in profiles.Where(profile => profile.AppId is not null))
                    foreach (var plan in profile.Namespaces.Where(plan => plan.Skeleton.Name.Length <= NamespaceLimit))
                        Collect(profile, plan, await FetchAsync(client, options.AppId ?? profile.AppId!, options.Cluster, plan.Skeleton.Name, secret, ct).ConfigureAwait(false));
            }
            var report = new ToolReport(profiles.Select(profile => new ProfileReport(profile.Name, options.AppId ?? profile.AppId,
                    profile.Namespaces.Select(plan => new NamespaceReport(plan.Skeleton.Name, plan.Entry, plan.Plugin, plan.Schema,
                        plan.Skeleton.Keys.Count, plan.Skeleton.Keys.Count(key => key.Required), plan.Skeleton.Keys.Count(key => key.Sensitive), plan.Subscribed,
                        online.GetValueOrDefault((profile.Name, plan.Skeleton.Name)))).ToArray())).ToArray(),
                diagnostics, findings);
            await output.WriteLineAsync(JsonSerializer.Serialize(report, JsonSerializerOptions.Web)).ConfigureAwait(false);
            return findings.Count != 0 || diagnostics.Any(item => item.Level == "error") ? 1 : 0;

            // 线上没有、宿主也没订阅的 namespace 不参与比对；宿主已订阅却不存在即缺键。
            void Collect(ProfilePlan profile, NamespacePlan plan, IReadOnlyList<string>? keys)
            {
                online[(profile.Name, plan.Skeleton.Name)] = keys is null ? "absent" : "present";
                if (keys is not null) findings.AddRange(plan.Skeleton.Compare(profile.Name, keys));
                else if (plan.Subscribed) findings.Add(new KeyFinding("missing", profile.Name, plan.Skeleton.Name, plan.Skeleton.Name, "#"));
            }
        }
        catch (Exception failure) when (failure is IOException or UnauthorizedAccessException or JsonException or InvalidDataException or ArgumentException or
            InvalidOperationException or HttpRequestException or TaskCanceledException or ToolException)
        {
            // 只给出固定原因，不透传异常原文：线上响应体或路径里可能带配置值。
            await error.WriteLineAsync(failure is ToolException tool ? tool.Message : failure switch
            {
                HttpRequestException or TaskCanceledException => "ConfigService 不可达或超时。",
                JsonException or InvalidDataException => "配置 Schema 或 ConfigService 响应不是可解释的 JSON。",
                _ => "仓库目录、profile 或配置 Schema 不可读。"
            }).ConfigureAwait(false);
            return 2;
        }
    }

    private static (IReadOnlyList<ProfilePlan>, IReadOnlyList<ToolDiagnostic>) Plan(ToolOptions options)
    {
        var root = Path.GetFullPath(options.Root);
        var names = options.Profiles.Count != 0 ? options.Profiles : Directory.EnumerateDirectories(Path.Combine(root, "profiles"))
            .Where(directory => File.Exists(Path.Combine(directory, "qian.profile.yml")))
            .Select(Path.GetFileName).OfType<string>().Order(StringComparer.Ordinal).ToArray();
        if (names.Count == 0) throw new ToolException("没有可推导的 profile。");
        var loader = new ProfileLoader(new SurfaceValidator());
        var profiles = new List<ProfilePlan>();
        var diagnostics = new List<ToolDiagnostic>();
        foreach (var name in names)
        {
            var result = loader.Load(new ProfileLoadOptions { WorkingDirectory = root, Profile = name, IncludeHomePatch = false });
            if (!result.IsValid)
            {
                diagnostics.Add(new ToolDiagnostic("error", "PROFILE_INVALID", name, null, "profile 装配未通过校验，无法推导 namespace 骨架。"));
                continue;
            }
            var entries = result.Snapshot!.Entries.Where(entry => entry.Scope == "global").ToArray();
            var apollo = entries.FirstOrDefault(entry => Id(entry) == ApolloPlugin);
            if (apollo is null) { profiles.Add(new ProfilePlan(name, null, [])); continue; }
            var subscribed = Strings(apollo.Config, "namespaces") is { Count: > 0 } declared ? declared : ["application"];
            var plans = new List<NamespacePlan>();
            foreach (var group in entries.Where(entry => !IsConfigComponent(Id(entry))).GroupBy(Id, StringComparer.Ordinal).OrderBy(group => group.Key, StringComparer.Ordinal))
            {
                var entry = group.First();
                var file = SchemaFile(entry);
                if (!File.Exists(file)) continue;
                using var schema = JsonDocument.Parse(File.ReadAllText(file));
                if (!schema.RootElement.TryGetProperty("properties", out var properties) || properties.ValueKind != JsonValueKind.Object || !properties.EnumerateObject().Any()) continue;
                var skeleton = NamespaceSkeleton.Create(NamespacePrefix + group.Key, schema.RootElement);
                plans.Add(new NamespacePlan(string.Join(',', group.Select(item => item.Id)), group.Key,
                    Path.GetRelativePath(root, file).Replace(Path.DirectorySeparatorChar, '/'), subscribed.Contains(skeleton.Name, StringComparer.Ordinal), skeleton));
                if (skeleton.Name.Length > NamespaceLimit)
                    diagnostics.Add(new ToolDiagnostic("warning", "CONFIG_NAMESPACE_TOO_LONG", name, skeleton.Name,
                        $"namespace 名 {skeleton.Name.Length} 个字符，超过 Apollo 的 {NamespaceLimit} 字符上限，无法建立；不参与线上比对。"));
                foreach (var key in skeleton.Keys.Where(key => skeleton.Name.Length + 1 + key.Path.Length > KeyLimit))
                    diagnostics.Add(new ToolDiagnostic("warning", "CONFIG_KEY_TOO_LONG", name, skeleton.Name, $"键 {key.Path} 加上 namespace 前缀超过 Apollo 的 {KeyLimit} 字符上限。"));
            }
            profiles.Add(new ProfilePlan(name, Text(apollo.Config, "appId"), plans));
        }
        return (profiles, diagnostics);
    }

    private static bool IsConfigComponent(string id) => id == "qian.config" || id.StartsWith("qian.config.", StringComparison.Ordinal);
    private static string Id(ResolvedProfileEntry entry) => entry.Manifest.GetProperty("id").GetString()!;

    private static string SchemaFile(ResolvedProfileEntry entry)
    {
        var relative = entry.Manifest.TryGetProperty("runtime", out var runtime) && runtime.ValueKind == JsonValueKind.Object &&
            runtime.TryGetProperty("configSchema", out var custom) && custom.ValueKind == JsonValueKind.String ? custom.GetString()! : "./schemas/config.schema.json";
        var directory = Path.GetFullPath(entry.ComponentDirectory);
        var file = Path.GetFullPath(Path.Combine(directory, relative));
        return file.StartsWith(directory + Path.DirectorySeparatorChar, StringComparison.Ordinal) ? file : throw new ToolException("配置 Schema 越出组件目录。");
    }

    private static string? Text(JsonElement config, string name) =>
        config.ValueKind == JsonValueKind.Object && config.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String ? value.GetString() : null;

    private static List<string>? Strings(JsonElement config, string name) =>
        config.ValueKind == JsonValueKind.Object && config.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.Array
            ? value.EnumerateArray().Where(item => item.ValueKind == JsonValueKind.String).Select(item => item.GetString()!).ToList() : null;

    // 导出格式 { "<namespace>": { "<键>": "<值>" } }，值只用于确认形状，不读取内容。
    private static Dictionary<string, IReadOnlyList<string>> ReadExport(string file)
    {
        using var document = JsonDocument.Parse(File.ReadAllText(file));
        if (document.RootElement.ValueKind != JsonValueKind.Object) throw new ToolException("导出的 JSON 必须是 namespace 到扁平字符串对象的映射。");
        var result = new Dictionary<string, IReadOnlyList<string>>(StringComparer.Ordinal);
        foreach (var item in document.RootElement.EnumerateObject())
        {
            if (item.Value.ValueKind != JsonValueKind.Object || item.Value.EnumerateObject().Any(pair => pair.Value.ValueKind is not (JsonValueKind.String or JsonValueKind.Null)))
                throw new ToolException("导出的 JSON 必须是 namespace 到扁平字符串对象的映射。");
            result[item.Name] = item.Value.EnumerateObject().Select(pair => pair.Name).ToArray();
        }
        return result;
    }

    private static async Task<IReadOnlyList<string>?> FetchAsync(HttpClient client, string appId, string cluster, string name, string? secret, CancellationToken ct)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, $"configs/{Uri.EscapeDataString(appId)}/{Uri.EscapeDataString(cluster)}/{Uri.EscapeDataString(name)}");
        if (secret is not null)
        {
            // Apollo 公开 AccessKey 协议：签名 = Base64(HMAC-SHA1(密钥, 时间戳 + "\n" + 路径与查询))，与 Qian.Config.Apollo 运行期一致。
            var uri = new Uri(client.BaseAddress!, request.RequestUri!);
            var timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds().ToString(CultureInfo.InvariantCulture);
#pragma warning disable CA5350 // Apollo 公共 AccessKey 协议指定 HMAC-SHA1；不用于新密码学方案。
            var signature = Convert.ToBase64String(HMACSHA1.HashData(Encoding.UTF8.GetBytes(secret), Encoding.UTF8.GetBytes(timestamp + "\n" + uri.PathAndQuery)));
#pragma warning restore CA5350
            request.Headers.Add("Timestamp", timestamp);
            request.Headers.TryAddWithoutValidation("Authorization", $"Apollo {appId}:{signature}");
        }
        using var response = await client.SendAsync(request, ct).ConfigureAwait(false);
        if (response.StatusCode == HttpStatusCode.NotFound) return null;
        if (response.StatusCode is HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden) throw new ToolException("ConfigService 拒绝访问：AccessKey 未提供或签名不符。");
        if (!response.IsSuccessStatusCode) throw new ToolException($"ConfigService 返回 {(int)response.StatusCode}。");
        await using var body = await response.Content.ReadAsStreamAsync(ct).ConfigureAwait(false);
        using var document = await JsonDocument.ParseAsync(body, cancellationToken: ct).ConfigureAwait(false);
        return document.RootElement.ValueKind == JsonValueKind.Object && document.RootElement.TryGetProperty("configurations", out var configurations) &&
            configurations.ValueKind == JsonValueKind.Object
            ? configurations.EnumerateObject().Select(item => item.Name).ToArray()
            : throw new ToolException("ConfigService 响应缺少 configurations。");
    }

    // 骨架按 Apollo Portal 文本模式书写（每行「键 = 值」，以第一个 = 分隔，不转义）：只写 Schema 默认值；
    // 没有默认值的键与敏感键写成注释，敏感键只能填 secretRef://…。
    private static void Write(string directory, IReadOnlyList<ProfilePlan> profiles)
    {
        foreach (var profile in profiles.Where(profile => profile.AppId is not null))
        {
            var target = Directory.CreateDirectory(Path.Combine(Path.GetFullPath(directory), profile.Name)).FullName;
            foreach (var plan in profile.Namespaces)
            {
                var text = new StringBuilder();
                text.Append(CultureInfo.InvariantCulture, $"# Apollo namespace {plan.Skeleton.Name}（AppId {profile.AppId}，profile {profile.Name}）\n");
                text.Append(CultureInfo.InvariantCulture, $"# 由 {plan.Schema} 生成；只含 Schema 默认值。注释行是没有默认值或敏感的键，敏感键只能填 secretRef://…\n");
                foreach (var key in plan.Skeleton.Keys.Where(key => key.Required))
                    text.Append(CultureInfo.InvariantCulture, $"# 必填：{plan.Skeleton.Name}:{key.Path}\n");
                foreach (var key in plan.Skeleton.Keys)
                {
                    var name = plan.Skeleton.Name + ":" + key.Path;
                    if (key.Sensitive) { text.Append(CultureInfo.InvariantCulture, $"# {name} = secretRef://platform/…\n"); continue; }
                    var lines = key.Defaults.Where(item => item.Value.IndexOfAny(['\r', '\n']) < 0).ToArray();
                    if (lines.Length == 0)
                    {
                        var placeholder = key.Kind switch { "array" => name + ":0", "map" => name + ":<名称>", _ => name };
                        text.Append(CultureInfo.InvariantCulture, $"# {placeholder} =\n");
                        continue;
                    }
                    foreach (var (path, value) in lines) text.Append(CultureInfo.InvariantCulture, $"{plan.Skeleton.Name}:{path} = {value}\n");
                }
                File.WriteAllText(Path.Combine(target, plan.Skeleton.Name + ".properties"), text.ToString(), new UTF8Encoding(false));
            }
        }
    }
}

internal sealed record ToolOptions(string Root, IReadOnlyList<string> Profiles, string? Output, Uri? Compare, string? CompareFile, string? AppId, string Cluster, string? AccessKeyVariable)
{
    internal static ToolOptions? Parse(string[] args)
    {
        string? root = null, output = null, appId = null, cluster = null, variable = null, file = null; Uri? compare = null;
        var profiles = new List<string>();
        for (var i = 0; i < args.Length; i++)
        {
            var value = i + 1 < args.Length ? args[i + 1] : null;
            switch (args[i])
            {
                case "--profile" when value is not null: profiles.Add(value); i++; break;
                case "--out" when value is not null && output is null: output = value; i++; break;
                case "--app-id" when value is not null && appId is null: appId = value; i++; break;
                case "--cluster" when value is not null && cluster is null: cluster = value; i++; break;
                case "--access-key-env" when value is not null && variable is null: variable = value; i++; break;
                // http(s) 地址是 ConfigService，其余按导出的 JSON 文件；地址不得内嵌凭据。
                case "--compare" when value is not null && compare is null && file is null:
                    if (value.StartsWith("http://", StringComparison.OrdinalIgnoreCase) || value.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
                    {
                        if (!Uri.TryCreate(value, UriKind.Absolute, out compare) || compare.UserInfo.Length != 0) return null;
                    }
                    else if (Uri.TryCreate(value, UriKind.Absolute, out var other) && !other.IsFile) return null;
                    else file = value;
                    i++; break;
                case var positional when !positional.StartsWith("--", StringComparison.Ordinal) && root is null: root = positional; break;
                default: return null;
            }
        }
        if (root is null || compare is null && (appId is not null || cluster is not null || variable is not null)) return null;
        return new ToolOptions(root, profiles, output, compare, file, appId, cluster ?? "default", variable);
    }
}

internal sealed record ProfilePlan(string Name, string? AppId, IReadOnlyList<NamespacePlan> Namespaces);
internal sealed record NamespacePlan(string Entry, string Plugin, string Schema, bool Subscribed, NamespaceSkeleton Skeleton);
internal sealed record ToolDiagnostic(string Level, string Code, string Profile, string? Namespace, string Reason);
internal sealed record NamespaceReport(string Name, string Entry, string Plugin, string Schema, int Keys, int Required, int Sensitive, bool Subscribed, string? Online);
internal sealed record ProfileReport(string Profile, string? AppId, IReadOnlyList<NamespaceReport> Namespaces);
internal sealed record ToolReport(IReadOnlyList<ProfileReport> Profiles, IReadOnlyList<ToolDiagnostic> Diagnostics, IReadOnlyList<KeyFinding> Findings);
internal sealed class ToolException(string message) : Exception(message);

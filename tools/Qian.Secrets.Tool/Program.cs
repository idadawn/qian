using System.Text.Json;
using Qian.Config;
using Qian.Profiles;
using Qian.Profiles.Abstractions;
using Qian.Surfaces;

// CI 输入只读；输出仅诊断位置与规则，不输出配置值或异常原文。
if (args.Length >= 2 && args[0] == "--profiles") return ScanProfiles(args[1], args[2..]);
var apollo = args.Length != 0 && args[0] == "--apollo";
var files = apollo ? args[1..] : args;
if (files.Length is < 1 or > 2)
{
    Console.Error.WriteLine("用法：Qian.Secrets.Tool [--apollo] <配置 JSON> [配置 Schema JSON]\n      Qian.Secrets.Tool --profiles <仓库根> [profile 名称...]");
    return 2;
}
try
{
    using var config = JsonDocument.Parse(File.ReadAllText(files[0]));
    using var schema = files.Length == 2 ? JsonDocument.Parse(File.ReadAllText(files[1])) : null;
    IReadOnlyList<SecretDiagnostic> diagnostics;
    if (apollo)
    {
        if (config.RootElement.ValueKind != JsonValueKind.Object ||
            config.RootElement.EnumerateObject().Any(item => item.Value.ValueKind is not (JsonValueKind.String or JsonValueKind.Null)))
        { Console.Error.WriteLine("Apollo 输入必须是扁平字符串对象。"); return 2; }
        diagnostics = SecretSafety.ScanConfiguration(config.RootElement.EnumerateObject()
            .ToDictionary(item => item.Name, item => item.Value.GetString(), StringComparer.OrdinalIgnoreCase), schema?.RootElement);
    }
    else diagnostics = SecretSafety.Scan(config.RootElement, schema?.RootElement);
    Console.WriteLine(JsonSerializer.Serialize(diagnostics, JsonSerializerOptions.Web));
    return diagnostics.Count == 0 ? 0 : 1;
}
catch (Exception error) when (error is IOException or UnauthorizedAccessException or JsonException or ArgumentException)
{
    Console.Error.WriteLine("配置扫描输入不可读或不是合法 JSON。");
    return 2;
}

// 装配仓库 profile 的最终生效配置，逐条目按组件配置 Schema 扫描；装配失败同样阻断，因为无法确认配置不含明文。
static int ScanProfiles(string rootArgument, string[] requested)
{
    try
    {
        var root = Path.GetFullPath(rootArgument);
        var names = requested.Length != 0 ? requested : Directory.EnumerateDirectories(Path.Combine(root, "profiles"))
            .Where(directory => File.Exists(Path.Combine(directory, "qian.profile.yml")))
            .Select(Path.GetFileName).OfType<string>().Order(StringComparer.Ordinal).ToArray();
        if (names.Length == 0) { Console.Error.WriteLine("没有可扫描的 profile。"); return 2; }
        var loader = new ProfileLoader(new SurfaceValidator());
        var findings = new List<ProfileSecretFinding>();
        foreach (var name in names)
        {
            var result = loader.Load(new ProfileLoadOptions { WorkingDirectory = root, Profile = name, IncludeHomePatch = false });
            if (!result.IsValid)
            {
                findings.AddRange(result.Diagnostics.Where(item => item.Severity == "error").Select(item =>
                    new ProfileSecretFinding(name, "", item.Error.Code, item.File, "profile 装配未通过校验，无法确认配置不含明文密钥。")));
                continue;
            }
            foreach (var entry in result.Snapshot!.Entries)
            {
                using var schema = ReadSchema(entry);
                findings.AddRange(SecretSafety.Scan(entry.Config, schema?.RootElement).Select(item =>
                    new ProfileSecretFinding(name, entry.Scope + "/" + entry.Id, item.Code, item.Location, item.Reason)));
            }
        }
        Console.WriteLine(JsonSerializer.Serialize(findings, JsonSerializerOptions.Web));
        return findings.Count == 0 ? 0 : 1;
    }
    catch (Exception error) when (error is IOException or UnauthorizedAccessException or JsonException or ArgumentException or InvalidOperationException)
    {
        Console.Error.WriteLine("profile 目录或配置 Schema 不可读。");
        return 2;
    }
}

static JsonDocument? ReadSchema(ResolvedProfileEntry entry)
{
    var relative = entry.Manifest.ValueKind == JsonValueKind.Object && entry.Manifest.TryGetProperty("runtime", out var runtime) &&
        runtime.ValueKind == JsonValueKind.Object && runtime.TryGetProperty("configSchema", out var custom) && custom.ValueKind == JsonValueKind.String
        ? custom.GetString()! : "./schemas/config.schema.json";
    var directory = Path.GetFullPath(entry.ComponentDirectory);
    var file = Path.GetFullPath(Path.Combine(directory, relative));
    if (!file.StartsWith(directory + Path.DirectorySeparatorChar, StringComparison.Ordinal)) throw new ArgumentException("配置 Schema 越出组件目录。");
    return File.Exists(file) ? JsonDocument.Parse(File.ReadAllText(file)) : null;
}

internal sealed record ProfileSecretFinding(string Profile, string Entry, string Code, string Location, string Reason);

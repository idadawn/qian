using System.Text.Json;
using Qian.Config;

// CI 输入只读；输出仅诊断位置与规则，不输出配置值或异常原文。
var apollo = args.Length != 0 && args[0] == "--apollo";
var files = apollo ? args[1..] : args;
if (files.Length is < 1 or > 2)
{
    Console.Error.WriteLine("用法：Qian.Secrets.Tool [--apollo] <配置 JSON> [配置 Schema JSON]");
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

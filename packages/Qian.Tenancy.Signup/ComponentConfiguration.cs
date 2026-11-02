using System.Text.Json;
using System.Text.RegularExpressions;
using Qian.Runtime.Errors;

namespace Qian.Tenancy.Signup;

// 配置声明的代码真相源；组件目录中的 schemas/config.schema.json 由 qian-plugin sync 派生。缺省值与 TenancySignupOptions 一致（单元测试核对）。
internal static class ComponentConfiguration
{
    internal const string Schema = """
        {
          "$schema": "https://json-schema.org/draft/2020-12/schema",
          "type": "object",
          "additionalProperties": false,
          "properties": {
            "trialDays": { "type": "integer", "minimum": 1, "maximum": 3650, "default": 30, "description": "试用开通的缺省天数：到期日缺省为审核当时加这么多天。" },
            "defaultUsers": { "type": "integer", "minimum": 1, "maximum": 100000, "default": 20, "description": "缺省用户配额，含租户管理员、不含平台超管。" },
            "code": {
              "type": "object",
              "additionalProperties": false,
              "description": "申请人可用的租户号：小写字母或数字，长度与保留字。",
              "properties": {
                "minLength": { "type": "integer", "minimum": 1, "maximum": 64, "default": 3 },
                "maxLength": { "type": "integer", "minimum": 1, "maximum": 64, "default": 32 },
                "reserved": {
                  "type": "array",
                  "uniqueItems": true,
                  "maxItems": 1000,
                  "items": { "type": "string", "pattern": "^[a-z0-9]{1,64}$" },
                  "default": ["admin", "api", "app", "assets", "console", "default", "help", "internal", "login", "mail", "platform", "portal", "public", "qian", "root", "signup", "static", "support", "system", "test", "www"]
                }
              }
            },
            "verification": {
              "type": "object",
              "additionalProperties": false,
              "description": "短信验证码：位数、有效期、错误上限、重发间隔与限流。",
              "properties": {
                "length": { "type": "integer", "minimum": 4, "maximum": 8, "default": 6 },
                "lifetimeSeconds": { "type": "integer", "minimum": 60, "maximum": 3600, "default": 300 },
                "maxAttempts": { "type": "integer", "minimum": 1, "maximum": 20, "default": 5 },
                "resendSeconds": { "type": "integer", "minimum": 10, "maximum": 3600, "default": 60 },
                "perPhonePerHour": { "type": "integer", "minimum": 1, "maximum": 1000, "default": 5 },
                "perSourcePerHour": { "type": "integer", "minimum": 1, "maximum": 100000, "default": 20 }
              }
            },
            "session": {
              "type": "object",
              "additionalProperties": false,
              "description": "申请人会话（只属于本组件，不是登录会话）。",
              "properties": {
                "lifetimeMinutes": { "type": "integer", "minimum": 5, "maximum": 1440, "default": 30 }
              }
            },
            "codeAvailability": {
              "type": "object",
              "additionalProperties": false,
              "description": "租户号可用性查询按来源地址限流。",
              "properties": {
                "perSourcePerMinute": { "type": "integer", "minimum": 1, "maximum": 100000, "default": 30 }
              }
            },
            "defaultCountryCode": { "type": "string", "pattern": "^[1-9][0-9]{0,2}$", "default": "86", "description": "手机号不带国家码时补的国家码，与身份服务相同。" }
          }
        }
        """;
}

/// <summary>组件配置（规格 §4）；缺省值与配置 Schema 的 default 一致。</summary>
internal sealed partial record TenancySignupOptions
{
    internal static readonly IReadOnlyList<string> DefaultReserved =
        ["admin", "api", "app", "assets", "console", "default", "help", "internal", "login", "mail", "platform", "portal", "public", "qian", "root", "signup", "static", "support", "system", "test", "www"];
    internal int TrialDays { get; init; } = 30;
    internal int DefaultUsers { get; init; } = 20;
    internal int CodeMinLength { get; init; } = 3;
    internal int CodeMaxLength { get; init; } = 32;
    internal IReadOnlySet<string> Reserved { get; init; } = new HashSet<string>(DefaultReserved, StringComparer.Ordinal);
    internal int CodeLength { get; init; } = 6;
    internal TimeSpan CodeLifetime { get; init; } = TimeSpan.FromSeconds(300);
    internal int MaxAttempts { get; init; } = 5;
    internal TimeSpan ResendInterval { get; init; } = TimeSpan.FromSeconds(60);
    internal int PerPhonePerHour { get; init; } = 5;
    internal int PerSourcePerHour { get; init; } = 20;
    internal TimeSpan SessionLifetime { get; init; } = TimeSpan.FromMinutes(30);
    internal int CodeChecksPerSourcePerMinute { get; init; } = 30;
    internal string DefaultCountryCode { get; init; } = "86";

    // 严格读取：未知键、重复键、类型或范围不对一律拒绝（宿主可能不先按 Schema 校验程序化装配的配置）。
    internal static TenancySignupOptions Read(JsonElement config)
    {
        if (config.ValueKind is JsonValueKind.Undefined or JsonValueKind.Null) return new TenancySignupOptions();
        var root = Object(config);
        var options = new TenancySignupOptions();
        foreach (var property in root)
        {
            options = property.Name switch
            {
                "trialDays" => options with { TrialDays = Int(property.Value, 1, 3650) },
                "defaultUsers" => options with { DefaultUsers = Int(property.Value, 1, 100000) },
                "code" => ReadCode(options, property.Value),
                "verification" => ReadVerification(options, property.Value),
                "session" => ReadSession(options, property.Value),
                "codeAvailability" => ReadCodeAvailability(options, property.Value),
                "defaultCountryCode" => options with { DefaultCountryCode = CountryCode(property.Value) },
                _ => throw Invalid()
            };
        }
        if (options.CodeMinLength > options.CodeMaxLength) throw Invalid();
        return options;
    }

    private static TenancySignupOptions ReadCode(TenancySignupOptions options, JsonElement value)
    {
        foreach (var property in Object(value))
        {
            options = property.Name switch
            {
                "minLength" => options with { CodeMinLength = Int(property.Value, 1, 64) },
                "maxLength" => options with { CodeMaxLength = Int(property.Value, 1, 64) },
                "reserved" => options with { Reserved = ReadReserved(property.Value) },
                _ => throw Invalid()
            };
        }
        return options;
    }

    private static TenancySignupOptions ReadVerification(TenancySignupOptions options, JsonElement value)
    {
        foreach (var property in Object(value))
        {
            options = property.Name switch
            {
                "length" => options with { CodeLength = Int(property.Value, 4, 8) },
                "lifetimeSeconds" => options with { CodeLifetime = TimeSpan.FromSeconds(Int(property.Value, 60, 3600)) },
                "maxAttempts" => options with { MaxAttempts = Int(property.Value, 1, 20) },
                "resendSeconds" => options with { ResendInterval = TimeSpan.FromSeconds(Int(property.Value, 10, 3600)) },
                "perPhonePerHour" => options with { PerPhonePerHour = Int(property.Value, 1, 1000) },
                "perSourcePerHour" => options with { PerSourcePerHour = Int(property.Value, 1, 100000) },
                _ => throw Invalid()
            };
        }
        return options;
    }

    private static TenancySignupOptions ReadSession(TenancySignupOptions options, JsonElement value)
    {
        foreach (var property in Object(value))
            options = property.Name == "lifetimeMinutes" ? options with { SessionLifetime = TimeSpan.FromMinutes(Int(property.Value, 5, 1440)) } : throw Invalid();
        return options;
    }

    private static TenancySignupOptions ReadCodeAvailability(TenancySignupOptions options, JsonElement value)
    {
        foreach (var property in Object(value))
            options = property.Name == "perSourcePerMinute" ? options with { CodeChecksPerSourcePerMinute = Int(property.Value, 1, 100000) } : throw Invalid();
        return options;
    }

    private static List<JsonProperty> Object(JsonElement value)
    {
        if (value.ValueKind != JsonValueKind.Object) throw Invalid();
        var properties = value.EnumerateObject().ToList();
        if (properties.Select(property => property.Name).Distinct(StringComparer.Ordinal).Count() != properties.Count) throw Invalid();
        return properties;
    }

    private static int Int(JsonElement value, int minimum, int maximum) =>
        value.ValueKind == JsonValueKind.Number && value.TryGetInt32(out var number) && number >= minimum && number <= maximum ? number : throw Invalid();

    private static HashSet<string> ReadReserved(JsonElement value)
    {
        if (value.ValueKind != JsonValueKind.Array || value.GetArrayLength() > 1000) throw Invalid();
        var result = new HashSet<string>(StringComparer.Ordinal);
        foreach (var item in value.EnumerateArray())
            if (item.ValueKind != JsonValueKind.String || item.GetString() is not { } word || !ReservedWord().IsMatch(word) || !result.Add(word)) throw Invalid();
        return result;
    }

    private static string CountryCode(JsonElement value) =>
        value.ValueKind == JsonValueKind.String && value.GetString() is { } code && CountryCodePattern().IsMatch(code) ? code : throw Invalid();

    private static QianErrorException Invalid() => new(new QianError(PlatformErrors.InputInvalid));
}

internal sealed partial record TenancySignupOptions
{
    [GeneratedRegex(@"\A[a-z0-9]{1,64}\z", RegexOptions.CultureInvariant)]
    private static partial Regex ReservedWord();
    [GeneratedRegex(@"\A[1-9][0-9]{0,2}\z", RegexOptions.CultureInvariant)]
    private static partial Regex CountryCodePattern();
}

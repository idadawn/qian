using System.Text.Json;
using System.Text.RegularExpressions;
using Qian.Runtime;
using Qian.Runtime.Errors;

namespace Qian.Identity.Abstractions;

/// <summary>连接器支持的终端（PRD 7.4.2：PC / 移动 / 工作台）。</summary>
public static class ConnectorTerminals
{
    /// <summary>桌面浏览器。</summary>
    public const string Pc = "pc";
    /// <summary>移动端浏览器或 App 内嵌页。</summary>
    public const string Mobile = "mobile";
    /// <summary>企业工作台（如企业微信）内免登。</summary>
    public const string Workbench = "workbench";
    /// <summary>全部终端词汇。</summary>
    public static IReadOnlyList<string> All { get; } = Array.AsReadOnly(new[] { Pc, Mobile, Workbench });
}

/// <summary>
/// 连接器清单（connector.manifest）：<see cref="ConnectorDescriptor"/> 的机器可读约定与校验。
/// 清单写明协议、支持终端、配置 JSON Schema 与回调模板；配置里的密钥字段只接受 secretRef，
/// 所以 Schema 中凡是密钥类属性都必须带 <c>x-qian-sensitive: true</c>、类型为 string，且 pattern 以 <c>^secretRef://</c> 开头。
/// 注册表在 <c>Register</c> 时校验，TCK 用同一校验器。
/// </summary>
public static partial class ConnectorManifest
{
    /// <summary>统一回调路由模板；由身份 Provider 托管并校验 state / nonce，连接器不自带回调。</summary>
    public const string CallbackTemplate = "/connect/callback/{connectorId}";

    /// <summary>配置 Schema 里标记密钥字段的扩展关键字（与配置扫描同一词汇）。</summary>
    public const string SensitiveKeyword = "x-qian-sensitive";

    /// <summary>连接器标识（即注册名）是否合法：小写字母开头，小写字母、数字或连字符，最长 64。</summary>
    public static bool IsValidId(string? id) => id is not null && IdPattern().IsMatch(id);

    /// <summary>按回调模板展开某个连接器的回调路径。</summary>
    public static string CallbackPath(string connectorId) =>
        IsValidId(connectorId) ? CallbackTemplate.Replace("{connectorId}", connectorId, StringComparison.Ordinal)
            : throw new ArgumentException("连接器标识不合法。", nameof(connectorId));

    /// <summary>逐项检查清单，返回字段错误（pointer 指向描述的成员）；空列表表示通过。</summary>
    public static IReadOnlyList<QianFieldError> Check(ConnectorDescriptor descriptor)
    {
        ArgumentNullException.ThrowIfNull(descriptor);
        var errors = new List<QianFieldError>();
        if (!IsValidId(descriptor.Id)) errors.Add(new QianFieldError(PlatformErrors.PatternMismatch, "#/id"));
        if (string.IsNullOrWhiteSpace(descriptor.DisplayName)) errors.Add(new QianFieldError(PlatformErrors.Required, "#/displayName"));
        else if (descriptor.DisplayName.Length > 64 || descriptor.DisplayName.Any(char.IsControl)) errors.Add(new QianFieldError(PlatformErrors.OutOfRange, "#/displayName"));
        if (descriptor.Protocol is null || !ProtocolPattern().IsMatch(descriptor.Protocol)) errors.Add(new QianFieldError(PlatformErrors.PatternMismatch, "#/protocol"));
        if (descriptor.Terminals is null || descriptor.Terminals.Count == 0) errors.Add(new QianFieldError(PlatformErrors.Required, "#/terminals"));
        else
        {
            var seen = new HashSet<string>(StringComparer.Ordinal);
            for (var i = 0; i < descriptor.Terminals.Count; i++)
                if (descriptor.Terminals[i] is not { } terminal || !ConnectorTerminals.All.Contains(terminal) || !seen.Add(terminal))
                    errors.Add(new QianFieldError(PlatformErrors.ValueNotAllowed, ErrorPointer.Input("terminals", i)));
        }
        if (descriptor.CallbackTemplate != CallbackTemplate) errors.Add(new QianFieldError(PlatformErrors.ValueNotAllowed, "#/callbackTemplate"));
        CheckSchema(descriptor.ConfigSchema, "#/configSchema", errors, 0);
        return errors;
    }

    /// <summary>清单不合法时抛 INPUT_INVALID，<c>errors[]</c> 列出每个违规位置。</summary>
    public static void Validate(ConnectorDescriptor descriptor)
    {
        var errors = Check(descriptor);
        if (errors.Count > 0) throw new QianErrorException(new QianError(PlatformErrors.InputInvalid) { Errors = [.. errors.Take(100)] });
    }

    // 配置 Schema：关闭的 object；密钥类属性必须标记敏感，敏感属性必须是 string 且 pattern 以 ^secretRef:// 开头（只能填 secretRef）。嵌套 object 同样检查。
    private static void CheckSchema(JsonElement schema, string pointer, List<QianFieldError> errors, int depth)
    {
        if (depth > 16) { errors.Add(new QianFieldError(PlatformErrors.OutOfRange, pointer)); return; }
        if (schema.ValueKind != JsonValueKind.Object || !schema.TryGetProperty("type", out var type) || type.ValueKind != JsonValueKind.String || type.GetString() != "object")
        { errors.Add(new QianFieldError(PlatformErrors.TypeMismatch, pointer)); return; }
        if (!schema.TryGetProperty("additionalProperties", out var additional) || additional.ValueKind != JsonValueKind.False)
            errors.Add(new QianFieldError(PlatformErrors.ValueNotAllowed, pointer + "/additionalProperties"));
        if (!schema.TryGetProperty("properties", out var properties)) return;
        if (properties.ValueKind != JsonValueKind.Object) { errors.Add(new QianFieldError(PlatformErrors.TypeMismatch, pointer + "/properties")); return; }
        foreach (var property in properties.EnumerateObject())
        {
            var at = pointer + "/properties/" + Escape(property.Name);
            var value = property.Value;
            if (value.ValueKind != JsonValueKind.Object) { errors.Add(new QianFieldError(PlatformErrors.TypeMismatch, at)); continue; }
            var sensitive = value.TryGetProperty(SensitiveKeyword, out var flag) && flag.ValueKind == JsonValueKind.True;
            var kind = value.TryGetProperty("type", out var t) && t.ValueKind == JsonValueKind.String ? t.GetString() : null;
            if (SecretName().IsMatch(property.Name) && !sensitive) errors.Add(new QianFieldError(PlatformErrors.ValueNotAllowed, at + "/" + SensitiveKeyword));
            if (sensitive && kind != "string") errors.Add(new QianFieldError(PlatformErrors.TypeMismatch, at + "/type"));
            if (sensitive && (!value.TryGetProperty("pattern", out var pattern) || pattern.ValueKind != JsonValueKind.String ||
                !pattern.GetString()!.StartsWith("^secretRef://", StringComparison.Ordinal)))
                errors.Add(new QianFieldError(PlatformErrors.ValueNotAllowed, at + "/pattern"));
            if (kind == "object") CheckSchema(value, at, errors, depth + 1);
        }
    }

    private static string Escape(string name) => name.Replace("~", "~0", StringComparison.Ordinal).Replace("/", "~1", StringComparison.Ordinal);

    [GeneratedRegex(@"\A[a-z][a-z0-9-]{0,63}\z", RegexOptions.CultureInvariant | RegexOptions.NonBacktracking)]
    private static partial Regex IdPattern();

    [GeneratedRegex(@"\A[a-z][a-z0-9-]{0,31}\z", RegexOptions.CultureInvariant | RegexOptions.NonBacktracking)]
    private static partial Regex ProtocolPattern();

    // 名字像密钥的属性：secret、password、token、credential、private key、api key。
    [GeneratedRegex(@"secret|passw(or)?d|token|credential|private_?key|api_?key", RegexOptions.CultureInvariant | RegexOptions.IgnoreCase | RegexOptions.NonBacktracking)]
    private static partial Regex SecretName();
}

/// <summary>
/// 绑定到某个插件上下文的连接器注册表（R38 注册表 seam）：注册即 <c>ctx.Provide(Identity.Connectors, name, connector)</c>，
/// 句柄进入调用方自己的效果栈，scope 遮蔽与 restriction 由运行时统一执行；被过滤与不存在同样查不到。
/// </summary>
internal sealed class PluginConnectorRegistry(IPluginContext context) : IIdentityConnectorRegistry
{
    public IDisposable Register(string name, IIdentityConnector connector)
    {
        ArgumentNullException.ThrowIfNull(connector);
        var descriptor = connector.Descriptor ?? throw new QianErrorException(new QianError(PlatformErrors.InputInvalid));
        ConnectorManifest.Validate(descriptor);
        if (name != descriptor.Id)
            throw new QianErrorException(new QianError(PlatformErrors.InputInvalid) { Errors = [new QianFieldError(PlatformErrors.ValueNotAllowed, "#/id")] });
        return context.Provide<IIdentityConnector>(IdentityDefinition.ConnectorsKey, name, connector);
    }

    public IIdentityConnector? Find(string name) =>
        ConnectorManifest.IsValidId(name) ? context.TryInject<IIdentityConnector>(new ServiceKey(IdentityDefinition.ConnectorsKey.Name + ":" + name)) : null;

    public IReadOnlyList<ConnectorDescriptor> List() =>
        [.. context.List(IdentityDefinition.ConnectorsKey).Select(registration => registration.Value).OfType<IIdentityConnector>()
            .Select(connector => connector.Descriptor).OrderBy(descriptor => descriptor.Id, StringComparer.Ordinal)];
}

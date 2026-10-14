using System.Text.Json;
using Qian.Runtime.Errors;

namespace Qian.Capabilities;

// 组件配置，结构与 plugins/qian.capabilities/schemas/config.schema.json 一致；宿主未经 profile 装载时同样按此校验与取默认值。
internal sealed record CapabilitiesOptions(int CatalogTtlMs)
{
    internal const int DefaultCatalogTtlMs = 1000;
    // 目录只是客户端缓存提示；上限与 PRD 7.5「授权变更 ≤ 3 秒生效」的口径一致，执行入口仍每次重新判定。
    internal const int MaxCatalogTtlMs = 3000;
    internal static CapabilitiesOptions Default { get; } = new(DefaultCatalogTtlMs);

    internal static CapabilitiesOptions Parse(JsonElement config)
    {
        if (config.ValueKind != JsonValueKind.Object) throw Invalid(new QianFieldError(PlatformErrors.TypeMismatch, ErrorPointer.Input()));
        var ttl = DefaultCatalogTtlMs; var errors = new List<QianFieldError>(); var names = new HashSet<string>(StringComparer.Ordinal);
        foreach (var member in config.EnumerateObject())
        {
            // pointer 最长 1024 字符：成员名过长时指向整个配置。
            var pointer = ErrorPointer.Input(member.Name) is var named && ErrorPointer.IsValid(named) ? named : ErrorPointer.Input();
            if (!names.Add(member.Name)) throw new QianErrorException(new QianError(PlatformErrors.BadRequest));
            if (member.Name != "catalogTtlMs") { errors.Add(new QianFieldError(PlatformErrors.UnknownProperty, pointer)); continue; }
            // 与 JSON Schema 的 integer 同义：1.0、2e3 这类小数部分为零的数值也是整数。
            if (member.Value.ValueKind != JsonValueKind.Number || !member.Value.TryGetDecimal(out var value) || value != decimal.Truncate(value)) errors.Add(new QianFieldError(PlatformErrors.TypeMismatch, pointer));
            else if (value is < 0 or > MaxCatalogTtlMs) errors.Add(new QianFieldError(PlatformErrors.OutOfRange, pointer));
            else ttl = (int)value;
        }
        if (errors.Count != 0) throw new QianErrorException(new QianError(PlatformErrors.InputInvalid) { Errors = errors });
        return new CapabilitiesOptions(ttl);
    }

    private static QianErrorException Invalid(QianFieldError error) => new(new QianError(PlatformErrors.InputInvalid) { Errors = [error] });
}

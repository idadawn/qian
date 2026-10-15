using System.Text.Json;
using Qian.Runtime.Errors;

namespace Qian.Capabilities;

// 组件配置，结构与 plugins/qian.capabilities/schemas/config.schema.json 一致；宿主未经 profile 装载时同样按此校验与取默认值。
internal sealed record CapabilitiesOptions(int CatalogTtlMs, int ReceiptMaxBytes)
{
    internal const int DefaultCatalogTtlMs = 1000;
    // 目录只是客户端缓存提示；上限与 PRD 7.5「授权变更 ≤ 3 秒生效」的口径一致，执行入口仍每次重新判定。
    internal const int MaxCatalogTtlMs = 3000;
    // 幂等回执保存响应体的上限（UTF-8 字节）。超出时只记「已执行」；上限 1 MiB 与回执表的检查约束一致。
    internal const int DefaultReceiptMaxBytes = 65536;
    internal const int MaxReceiptMaxBytes = 1048576;
    internal static CapabilitiesOptions Default { get; } = new(DefaultCatalogTtlMs, DefaultReceiptMaxBytes);

    internal static CapabilitiesOptions Parse(JsonElement config)
    {
        if (config.ValueKind != JsonValueKind.Object) throw Invalid(new QianFieldError(PlatformErrors.TypeMismatch, ErrorPointer.Input()));
        var ttl = DefaultCatalogTtlMs; var receipt = DefaultReceiptMaxBytes;
        var errors = new List<QianFieldError>(); var names = new HashSet<string>(StringComparer.Ordinal);
        foreach (var member in config.EnumerateObject())
        {
            // pointer 最长 1024 字符：成员名过长时指向整个配置。
            var pointer = ErrorPointer.Input(member.Name) is var named && ErrorPointer.IsValid(named) ? named : ErrorPointer.Input();
            if (!names.Add(member.Name)) throw new QianErrorException(new QianError(PlatformErrors.BadRequest));
            switch (member.Name)
            {
                case "catalogTtlMs": ttl = Integer(member.Value, MaxCatalogTtlMs, pointer, errors) ?? ttl; break;
                case "receiptMaxBytes": receipt = Integer(member.Value, MaxReceiptMaxBytes, pointer, errors) ?? receipt; break;
                default: errors.Add(new QianFieldError(PlatformErrors.UnknownProperty, pointer)); break;
            }
        }
        if (errors.Count != 0) throw new QianErrorException(new QianError(PlatformErrors.InputInvalid) { Errors = errors });
        return new CapabilitiesOptions(ttl, receipt);
    }

    // 与 JSON Schema 的 integer 同义：1.0、2e3 这类小数部分为零的数值也是整数。
    private static int? Integer(JsonElement value, int max, string pointer, List<QianFieldError> errors)
    {
        if (value.ValueKind != JsonValueKind.Number || !value.TryGetDecimal(out var number) || number != decimal.Truncate(number)) errors.Add(new QianFieldError(PlatformErrors.TypeMismatch, pointer));
        else if (number < 0 || number > max) errors.Add(new QianFieldError(PlatformErrors.OutOfRange, pointer));
        else return (int)number;
        return null;
    }

    private static QianErrorException Invalid(QianFieldError error) => new(new QianError(PlatformErrors.InputInvalid) { Errors = [error] });
}

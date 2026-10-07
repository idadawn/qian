using System.Buffers;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace Qian.Runtime.Errors;

/// <summary>
/// 错误码目录：全局唯一的错误码登记处，渲染 <see cref="ErrorDetail"/>，并导出
/// contracts/v0/error-catalog.schema.json 格式的 JSON（错误码与多语言文案）。
/// </summary>
/// <remarks>
/// 同一错误码只能有一份定义：再次登记完全相同的定义按引用计数共享，定义不同即失败。
/// <see cref="Register"/> 返回的释放函数撤销本次登记，供插件卸载时逆序撤销（ctx.Effect）。线程安全。
/// </remarks>
public sealed partial class ErrorCatalog
{
    private readonly Lock _gate = new();
    private readonly Dictionary<string, Registration> _codes = new(StringComparer.Ordinal);

    /// <summary>含平台内置错误码（<see cref="PlatformErrors.All"/>）的目录。</summary>
    public static ErrorCatalog CreateDefault()
    {
        var catalog = new ErrorCatalog();
        foreach (var code in PlatformErrors.All)
            catalog.Register(code);
        return catalog;
    }

    /// <summary>当前登记的全部错误码，按 code 排序。</summary>
    public IReadOnlyList<ErrorCode> Codes
    {
        get
        {
            lock (_gate)
                return _codes.Values.Select(r => r.Code).OrderBy(c => c.Code, StringComparer.Ordinal).ToArray();
        }
    }

    /// <summary>登记一个错误码。</summary>
    /// <returns>撤销本次登记的释放函数。</returns>
    /// <exception cref="InvalidOperationException">同名错误码已有不同定义。</exception>
    public IDisposable Register(ErrorCode code)
    {
        ArgumentNullException.ThrowIfNull(code);
        lock (_gate)
        {
            if (_codes.TryGetValue(code.Code, out var existing))
            {
                if (!existing.Code.Equals(code))
                {
                    throw new InvalidOperationException(
                        $"错误码 {code.Code} 已由 {existing.Code.Owner} 以不同定义登记；错误码全局唯一，复用请引用同一定义。");
                }
                existing.Count++;
            }
            else
            {
                _codes.Add(code.Code, new Registration(code));
            }
        }
        return new Unregister(this, code);
    }

    /// <summary>按 code 查找。</summary>
    public bool TryGet(string code, [NotNullWhen(true)] out ErrorCode? errorCode)
    {
        lock (_gate)
        {
            if (_codes.TryGetValue(code, out var registration))
            {
                errorCode = registration.Code;
                return true;
            }
        }
        errorCode = null;
        return false;
    }

    /// <summary>该定义是否已登记。</summary>
    public bool Contains(ErrorCode code) => TryGet(code.Code, out var registered) && registered.Equals(code);

    /// <summary>把一次出错渲染为错误响应体。</summary>
    /// <param name="error">出错内容；其错误码与字段错误码都必须已登记。</param>
    /// <param name="traceId">W3C trace-id（32 位小写十六进制）。</param>
    /// <param name="culture">文案语言；默认 <see cref="CultureInfo.CurrentUICulture"/>，找不到时回退 zh-CN。</param>
    /// <param name="instance">出错的请求路径。</param>
    /// <exception cref="InvalidOperationException">错误码未登记。</exception>
    public ErrorDetail CreateDetail(QianError error, string traceId, CultureInfo? culture = null, string? instance = null)
    {
        ArgumentNullException.ThrowIfNull(error);
        if (traceId is null || !TraceIdPattern().IsMatch(traceId))
            throw new ArgumentException($"'{traceId}' 不是 W3C trace-id（32 位小写十六进制）。", nameof(traceId));
        EnsureRegistered(error.Code);

        List<FieldError>? errors = null;
        if (error.Errors.Count > 0)
        {
            if (error.Pointer is not null || error.Allowed is not null)
                throw new ArgumentException("有 errors 时顶层 pointer / allowed 由 errors[0] 决定，不要另设。", nameof(error));
            errors = new List<FieldError>(error.Errors.Count);
            foreach (var item in error.Errors)
            {
                EnsureRegistered(item.Code);
                errors.Add(new FieldError
                {
                    Code = item.Code.Code,
                    Message = item.Code.FormatMessage(culture, item.Args),
                    Pointer = item.Pointer,
                    Allowed = item.Allowed,
                });
            }
        }

        var message = error.Code.FormatMessage(culture, error.Args);
        return new ErrorDetail
        {
            Type = error.Code.Type!,
            Title = error.Code.GetTitle(culture),
            Status = error.Code.Status!.Value,
            Detail = message,
            Instance = string.IsNullOrEmpty(instance) ? null : instance,
            Code = error.Code.Code,
            Message = message,
            TraceId = traceId,
            Pointer = errors?[0].Pointer ?? error.Pointer,
            Allowed = errors is null ? error.Allowed : errors[0].Allowed,
            Errors = errors,
        };
    }

    /// <summary>导出全部错误码与多语言文案（error-catalog.schema.json 格式，缩进两格、按 code 排序、以换行结尾）。</summary>
    public string ExportJson()
    {
        var buffer = new ArrayBufferWriter<byte>();
        using (var writer = new Utf8JsonWriter(buffer, new JsonWriterOptions
        {
            Indented = true,
            NewLine = "\n",
            Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
        }))
        {
            writer.WriteStartObject();
            writer.WriteString("version", "v0");
            writer.WriteStartArray("codes");
            foreach (var code in Codes)
            {
                writer.WriteStartObject();
                writer.WriteString("code", code.Code);
                writer.WriteString("kind", ErrorCode.KindName(code.Kind));
                if (code.Status is { } status)
                    writer.WriteNumber("status", status);
                if (code.Type is { } type)
                    writer.WriteString("type", type);
                writer.WriteString("owner", code.Owner);
                writer.WriteBoolean("retryable", code.Retryable);
                WriteText(writer, "title", code.TitleText);
                WriteText(writer, "message", code.MessageText);
                writer.WriteEndObject();
            }
            writer.WriteEndArray();
            writer.WriteEndObject();
        }
        return Encoding.UTF8.GetString(buffer.WrittenSpan) + "\n";
    }

    private void EnsureRegistered(ErrorCode code)
    {
        if (!Contains(code))
            throw new InvalidOperationException($"错误码 {code.Code} 未登记到错误码目录（或登记的定义不同）。");
    }

    private static void WriteText(Utf8JsonWriter writer, string name, SortedDictionary<string, string> text)
    {
        writer.WriteStartObject(name);
        foreach (var (locale, value) in text)
            writer.WriteString(locale, value);
        writer.WriteEndObject();
    }

    private void Release(ErrorCode code)
    {
        lock (_gate)
        {
            if (_codes.TryGetValue(code.Code, out var registration) && registration.Code.Equals(code) && --registration.Count == 0)
                _codes.Remove(code.Code);
        }
    }

    [GeneratedRegex("^[0-9a-f]{32}$", RegexOptions.CultureInvariant)]
    private static partial Regex TraceIdPattern();

    private sealed class Registration(ErrorCode code)
    {
        public ErrorCode Code { get; } = code;

        public int Count { get; set; } = 1;
    }

    private sealed class Unregister(ErrorCatalog catalog, ErrorCode code) : IDisposable
    {
        private int _disposed;

        public void Dispose()
        {
            if (Interlocked.Exchange(ref _disposed, 1) == 0)
                catalog.Release(code);
        }
    }
}

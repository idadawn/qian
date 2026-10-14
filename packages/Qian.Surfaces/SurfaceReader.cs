using System.Globalization;
using System.Numerics;
using System.Text.RegularExpressions;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Qian.Surfaces.Abstractions;
using YamlDotNet.Core;
using YamlDotNet.Core.Events;
using YamlDotNet.RepresentationModel;

namespace Qian.Surfaces;

internal static class SurfaceReader
{
    private static readonly UTF8Encoding StrictUtf8 = new(false, true);

    // 与 CLI 的 parseSurface 同序同义。致命错误（语法、大小、嵌套、值、未成对的代理项）只报它自己，先前记下的重复键不再报告。
    internal static SurfaceParseResult Parse(string file, string content, bool markdown, string traceId)
    {
        var lines = new Dictionary<string, int>(StringComparer.Ordinal);
        var diagnostics = new List<SurfaceDiagnostic>();
        var source = content.Replace("\r\n", "\n", StringComparison.Ordinal);
        var offset = 0;
        var body = "";
        SurfaceParseResult Fail(int line, string reason)
        {
            diagnostics.Clear();
            diagnostics.Add(Diagnostics.Create(file, "", line, SurfaceErrors.Syntax, reason, traceId));
            return new SurfaceParseResult(null, diagnostics);
        }
        try
        {
            // 严格编码：字面量的未成对代理项（只可能来自直接传入的字符串，文件按 UTF-8 严格解码）抛 EncoderFallbackException，按值无效处理。
            if (StrictUtf8.GetByteCount(source) > 1_048_576) throw new InvalidDataException("单文件不能超过 1 MiB。");
            if (markdown)
            {
                if (!source.StartsWith("---\n", StringComparison.Ordinal)) throw new InvalidDataException("Markdown 必须以 YAML front matter 开始。");
                var end = source.IndexOf("\n---", 4, StringComparison.Ordinal);
                if (end < 0 || (source.Length > end + 4 && source[end + 4] != '\n')) throw new InvalidDataException("front matter 缺少结束分隔符。");
                body = source[Math.Min(end + 5, source.Length)..];
                source = source[4..end];
                offset = 1;
            }
            JsonElement data;
            if (!markdown && file.EndsWith(".json", StringComparison.Ordinal))
            {
                var bytes = Encoding.UTF8.GetBytes(source);
                var reader = new Utf8JsonReader(bytes, new JsonReaderOptions { MaxDepth = 64 });
                if (!reader.Read()) throw new JsonException();
                var unpaired = 0;
                ReadJson(ref reader, "", source, bytes, lines, diagnostics, file, traceId, ref unpaired);
                if (unpaired > 0) return Fail(unpaired, "JSON 字符串或键名含未成对的代理项。");
                using var document = JsonDocument.Parse(source);
                data = document.RootElement.Clone();
            }
            else
            {
                // YamlDotNet 边扫描边检查：语法错误、alias 与含未成对代理项的双引号标量（SyntaxErrorException 指向标量起点）按出现顺序先到先报，
                // 重复键在 Load 时才检查。扫描器还会抛两类非语法异常：高代理项转义之后紧跟不是低代理项的转义时，组对抛 ArgumentOutOfRangeException（上游缺陷），
                // 按所在双引号标量的起点报，同其他未成对的代理项；未闭合的流式集合等抛 InvalidOperationException，按扫描停下的位置报。
                var scanner = new Scanner(new StringReader(source));
                var parser = new Parser(scanner);
                try
                {
                    while (parser.MoveNext())
                        if (parser.Current is AnchorAlias) throw new InvalidDataException("配置不允许 YAML alias。");
                }
                catch (ArgumentOutOfRangeException)
                {
                    var quote = OpeningQuote(source, (int)scanner.CurrentPosition.Index);
                    return Fail((quote < 0 ? (int)scanner.CurrentPosition.Line : source.AsSpan(0, quote).Count('\n') + 1) + offset, "YAML 语法无效或存在重复键。");
                }
                catch (Exception error) when (error is InvalidOperationException or ArgumentException)
                {
                    return Fail((int)scanner.CurrentPosition.Line + offset, "YAML 语法无效或存在重复键。");
                }
                var yaml = new YamlStream();
                yaml.Load(new StringReader(source));
                if (yaml.Documents.Count > 1) throw new InvalidDataException("必须恰有一个 YAML 文档。");
                lines[""] = 1 + offset;
                var node = yaml.Documents.Count == 0 ? null : ReadYaml(yaml.Documents[0].RootNode, "", offset, lines, 0);
                data = JsonSerializer.SerializeToElement(node);
            }
            return new SurfaceParseResult(new SurfaceSyntax(data, body, lines), diagnostics);
        }
        catch (JsonException error)
        {
            return Fail((int)(error.LineNumber ?? 0) + 1 + offset, "JSON 语法无效。");
        }
        catch (YamlException error)
        {
            return Fail((int)error.Start.Line + offset, "YAML 语法无效或存在重复键。");
        }
        catch (Exception error) when (error is InvalidDataException or ArgumentException or FormatException or OverflowException)
        {
            return Fail(1 + offset, "文件语法、大小、嵌套深度或值无效。");
        }
    }

    private static void ReadJson(ref Utf8JsonReader reader, string pointer, string source, byte[] bytes, Dictionary<string, int> lines,
        List<SurfaceDiagnostic> errors, string file, string traceId, ref int unpaired)
    {
        int Line(long position) => Encoding.UTF8.GetString(bytes.AsSpan(0, (int)position)).Count(c => c == '\n') + 1;
        var line = Line(reader.TokenStartIndex);
        lines.TryAdd(pointer, line);
        if (reader.TokenType == JsonTokenType.StartObject)
        {
            var names = new HashSet<string>(StringComparer.Ordinal);
            while (reader.Read() && reader.TokenType != JsonTokenType.EndObject)
            {
                if (reader.TokenType != JsonTokenType.PropertyName) throw new JsonException();
                var at = Line(reader.TokenStartIndex);
                var name = Text(ref reader, at, ref unpaired);
                var child = pointer + "/" + Diagnostics.Token(name);
                lines[child] = at;
                if (!names.Add(name)) errors.Add(Diagnostics.Create(file, child, lines[child], SurfaceErrors.Syntax, "JSON 对象存在重复键。", traceId));
                if (!reader.Read()) throw new JsonException();
                ReadJson(ref reader, child, source, bytes, lines, errors, file, traceId, ref unpaired);
            }
        }
        else if (reader.TokenType == JsonTokenType.StartArray)
        {
            var index = 0;
            while (reader.Read() && reader.TokenType != JsonTokenType.EndArray)
                ReadJson(ref reader, pointer + "/" + index++, source, bytes, lines, errors, file, traceId, ref unpaired);
        }
        else if (reader.TokenType == JsonTokenType.String) _ = Text(ref reader, line, ref unpaired);
        else if (reader.TokenType == JsonTokenType.Number && !double.IsFinite(reader.GetDouble())) throw new JsonException();
    }

    // 键名与字符串值按 UTF-16 解码。含未成对的代理项转义时 GetString 抛 InvalidOperationException（CLI 的 JSON.parse 照常接受）：
    // 记下首个所在行，读完整篇后再报，语法错误、过深与非有限数值优先；键名改用原文，只为继续读取。
    private static string Text(ref Utf8JsonReader reader, int line, ref int unpaired)
    {
        try { return reader.GetString()!; }
        catch (InvalidOperationException)
        {
            if (unpaired == 0) unpaired = line;
            return Encoding.UTF8.GetString(reader.ValueSpan);
        }
    }

    // 从双引号标量内的位置往回找开引号：标量内的引号都已转义（前面是奇数个反斜杠），第一个前面是偶数个反斜杠的引号即起点。找不到时返回 -1。
    private static int OpeningQuote(string source, int position)
    {
        for (var index = Math.Min(position, source.Length) - 1; index >= 0; index--)
        {
            if (source[index] != '"') continue;
            var slashes = 0;
            while (index - slashes - 1 >= 0 && source[index - slashes - 1] == '\\') slashes++;
            if (slashes % 2 == 0) return index;
        }
        return -1;
    }

    private static JsonNode? ReadYaml(YamlNode node, string pointer, int offset, Dictionary<string, int> lines, int depth)
    {
        if (depth > 64) throw new InvalidDataException("嵌套过深。");
        lines.TryAdd(pointer, (int)node.Start.Line + offset);
        var tag = node.Tag.IsEmpty ? "" : node.Tag.Value;
        if (tag.Length > 0 && tag != "!" && !tag.StartsWith("tag:yaml.org,2002:", StringComparison.Ordinal)) throw new InvalidDataException("不支持自定义 YAML tag。");
        if (node is YamlMappingNode map)
        {
            var result = new JsonObject();
            foreach (var pair in map.Children)
            {
                if (pair.Key is not YamlScalarNode key || key.Value is null) throw new InvalidDataException("YAML 键必须为字符串。");
                var child = pointer + "/" + Diagnostics.Token(key.Value);
                if (result.ContainsKey(key.Value)) throw new InvalidDataException("YAML 存在重复键。");
                lines[child] = (int)key.Start.Line + offset;
                result.Add(key.Value, ReadYaml(pair.Value, child, offset, lines, depth + 1));
            }
            return result;
        }
        if (node is YamlSequenceNode sequence)
        {
            var result = new JsonArray();
            for (var i = 0; i < sequence.Children.Count; i++) result.Add(ReadYaml(sequence.Children[i], pointer + "/" + i, offset, lines, depth + 1));
            return result;
        }
        var scalar = (YamlScalarNode)node;
        var value = scalar.Value ?? "";
        if (tag is "!" or "tag:yaml.org,2002:str" || tag.Length == 0 && scalar.Style is ScalarStyle.SingleQuoted or ScalarStyle.DoubleQuoted or ScalarStyle.Literal or ScalarStyle.Folded)
            return JsonValue.Create(value);
        if (Regex.IsMatch(value, "^(?:[-+]?\\.(?:inf|Inf|INF)|\\.nan|\\.NaN|\\.NAN)$", RegexOptions.CultureInvariant)) throw new InvalidDataException("JSON 不支持非有限数值。");
        if (Regex.IsMatch(value, "^0x[0-9a-fA-F]+$", RegexOptions.CultureInvariant) || Regex.IsMatch(value, "^0o[0-7]+$", RegexOptions.CultureInvariant))
        {
            var sign = value.StartsWith('-') ? -1 : 1;
            var unsigned = value.TrimStart('+', '-');
            var radix = unsigned[1] is 'x' or 'X' ? 16 : 8;
            BigInteger integer = 0;
            foreach (var digit in unsigned[2..].ToLowerInvariant()) integer = integer * radix + (digit <= '9' ? digit - '0' : digit - 'a' + 10);
            var converted = sign * (double)integer;
            if (!double.IsFinite(converted)) throw new InvalidDataException("数值溢出。");
            return JsonValue.Create(converted);
        }
        if (value is "null" or "Null" or "NULL" or "~" or "") return null;
        if (value is "true" or "True" or "TRUE") return JsonValue.Create(true);
        if (value is "false" or "False" or "FALSE") return JsonValue.Create(false);
        var numeric = Regex.IsMatch(value, "^[-+]?(?:[0-9]+|\\.[0-9]+|[0-9]+\\.[0-9]*)(?:[eE][-+]?[0-9]+)?$", RegexOptions.CultureInvariant);
        if (numeric && double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out var number))
        {
            if (!double.IsFinite(number)) throw new InvalidDataException("数值溢出。");
            return JsonValue.Create(number);
        }
        if (tag is "tag:yaml.org,2002:int" or "tag:yaml.org,2002:float" or "tag:yaml.org,2002:bool" or "tag:yaml.org,2002:null")
            throw new InvalidDataException("标量不符合显式 YAML 类型。");
        return JsonValue.Create(value);
    }
}

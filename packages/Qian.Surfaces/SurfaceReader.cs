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
    private const string Generic = "文件语法、大小、嵌套深度或值无效。";
    private const string YamlSyntax = "YAML 语法无效或存在重复键。";
    private const string YamlDocuments = "YAML 只能包含一个文档。";
    private const string YamlCharacters = "YAML 不能含 NUL、单独的回车符、U+0085、U+2028 或 U+2029。";
    private const string YamlDirectives = "不支持 YAML 指令（以 % 开头的行）。";

    // 与 CLI 的 parseSurface 同序同义，行号约定见 README。致命错误（语法、大小、嵌套、值、未成对的代理项）只报它自己，先前记下的重复键不再报告。
    internal static SurfaceParseResult Parse(string file, string content, bool markdown, string traceId)
    {
        var lines = new Dictionary<string, int>(StringComparer.Ordinal);
        var diagnostics = new List<SurfaceDiagnostic>();
        var source = content.Replace("\r\n", "\n", StringComparison.Ordinal);
        if (source.Length > 0 && source[0] == (char)0xFEFF) source = source[1..];
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
                // 读取器多放一层：第 65 层容器由 ReadJson 按过深报告（通用文案），不按语法错误。语法错误、过深与非有限数值按文中顺序先到先报。
                var reader = new Utf8JsonReader(bytes, new JsonReaderOptions { MaxDepth = 65 });
                if (!reader.Read()) throw new JsonException();
                var unpaired = 0;
                ReadJson(ref reader, "", 0, bytes, lines, diagnostics, file, traceId, ref unpaired);
                // 根值之后还有内容时 Read 抛 JsonException，同样先于未成对的代理项。
                if (reader.Read()) throw new JsonException();
                if (unpaired > 0) return Fail(unpaired, "JSON 字符串或键名含未成对的代理项。");
                using var document = JsonDocument.Parse(source);
                data = document.RootElement.Clone();
            }
            else
            {
                // 末尾没有换行时补一个：front matter 截取时去掉了最后的换行，两库对文件末尾块标量是否保留结尾换行的处理也不同。
                if (!source.EndsWith('\n')) source += "\n";
                if (ScanYaml(source) is { } problem) return Fail(problem.Line + offset, problem.Reason);
                var yaml = new YamlStream();
                // 扫描已拒绝标量键重复；Load 只会因非标量键重复再抛，非标量键本就按值无效处理。
                try { yaml.Load(new StringReader(source)); }
                catch (YamlException) { throw new InvalidDataException("YAML 键必须为字符串。"); }
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
            return Fail(1 + offset, Generic);
        }
    }

    // 容器最多嵌套 64 层，第 65 层容器与非有限数值按读到的先后报，同 YAML 用通用文案。
    private static void ReadJson(ref Utf8JsonReader reader, string pointer, int depth, byte[] bytes, Dictionary<string, int> lines,
        List<SurfaceDiagnostic> errors, string file, string traceId, ref int unpaired)
    {
        int Line(long position) => Encoding.UTF8.GetString(bytes.AsSpan(0, (int)position)).Count(c => c == '\n') + 1;
        var line = Line(reader.TokenStartIndex);
        lines.TryAdd(pointer, line);
        if (reader.TokenType is JsonTokenType.StartObject or JsonTokenType.StartArray && depth >= 64) throw new InvalidDataException("嵌套过深。");
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
                ReadJson(ref reader, child, depth + 1, bytes, lines, errors, file, traceId, ref unpaired);
            }
        }
        else if (reader.TokenType == JsonTokenType.StartArray)
        {
            var index = 0;
            while (reader.Read() && reader.TokenType != JsonTokenType.EndArray)
                ReadJson(ref reader, pointer + "/" + index++, depth + 1, bytes, lines, errors, file, traceId, ref unpaired);
        }
        else if (reader.TokenType == JsonTokenType.String) _ = Text(ref reader, line, ref unpaired);
        else if (reader.TokenType == JsonTokenType.Number && !double.IsFinite(reader.GetDouble())) throw new InvalidDataException("JSON 不支持非有限数值。");
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

    private readonly record struct Problem(int Line, string Reason);

    private sealed class Frame(HashSet<string>? names, int indent, bool flow)
    {
        public HashSet<string>? Names { get; } = names;
        public int Indent { get; } = indent;
        public bool Flow { get; } = flow;
        public bool Key { get; set; } = true;
    }

    // 与 CLI 的 scanYaml 同序同义，行号约定见 README。先逐行预检（Prescan）；之后按扫描顺序，语法错误、行首缩进里的制表符、第 65 层集合、续行缩进不足的引号标量、
    // 含未成对代理项的双引号标量，以及两库处理不同、两端都拒绝的写法（块映射里的空键、「-」「?」之后的制表符、紧跟冒号的 alias 键、转义换行之后紧跟空行的双引号标量）
    // 先到先报；整个流扫描无误后，再查 alias 与首个重复键。
    private static Problem? ScanYaml(string source)
    {
        if (Prescan(source) is { } early) return early;
        var tabs = IndentTabs(source);
        var pending = 0;
        Problem? TabBefore(long position) => pending < tabs.Count && tabs[pending] < position ? new Problem(LineAt(source, tabs[pending]), YamlSyntax) : null;
        var scanner = new Scanner(new StringReader(source));
        var parser = new Parser(scanner);
        var frames = new Stack<Frame>();
        var duplicate = 0;
        long previous = 0;
        bool emptyDuplicate = false, alias = false;
        try
        {
            while (parser.MoveNext())
            {
                var current = parser.Current!;
                var flow = frames.Count > 0 && frames.Peek().Flow ? FlowUnderindented(source, previous, current, frames) : -1;
                if (TabBefore(flow >= 0 ? flow : current.Start.Index) is { } tab) return tab;
                if (flow >= 0) return new Problem(LineAt(source, flow), YamlSyntax);
                // 空标量的位置可能落在前面（见下方空键），只往后推。
                var before = previous;
                previous = Math.Max(previous, current.End.Index);
                // YamlDotNet 给空键的位置不可靠（常为文件开头），重复的空键取紧随其后的值所在行，同 yaml 包。
                if (emptyDuplicate) (duplicate, emptyDuplicate) = ((int)current.Start.Line, false);
                switch (current)
                {
                    case MappingEnd or SequenceEnd:
                        frames.Pop();
                        break;
                    case NodeEvent or AnchorAlias:
                        // alias 不中断扫描：YamlDotNet 要先读完一行内的记号才交出 alias（判断它能否作为简单键），其后的语法错误会先抛出；改为整篇无语法错误时再报。
                        alias |= current is AnchorAlias;
                        if (current is MappingStart or SequenceStart && frames.Count >= 64) return new Problem(1, Generic);
                        var parent = frames.Count > 0 ? frames.Peek() : null;
                        // yaml 包把块序列项「-」或显式键「?」之后、节点属性（anchor、tag）或紧凑块集合之前空白里的制表符当作缩进拒绝，YamlDotNet 接受；两端都按语法错误报在所在行。
                        if (parent is { Flow: false } && (current is NodeEvent { Anchor.IsEmpty: false } or NodeEvent { Tag.IsEmpty: false }
                            or MappingStart { Style: MappingStyle.Block } or SequenceStart { Style: SequenceStyle.Block }) && TabAfterIndicator(source, current.Start.Index) is >= 0 and var at)
                            return new Problem(LineAt(source, at), YamlSyntax);
                        // 块映射里的空键（冒号前没有键，含显式键「?」之后为空）：YamlDotNet 18.1.0 在嵌套块映射里会吞掉其后的值（ParseBlockMappingKey 跳过 Value 记号
                        // 却不转入取值状态），两端都拒绝，报在条目开始的行；流式映射不受影响。YamlDotNet 给空键的位置不可靠，从上一个事件之后找条目开头。
                        if (parent is { Names: not null, Key: true, Flow: false } && current is Scalar { Value: "", Style: ScalarStyle.Plain })
                            return new Problem(LineAt(source, EntryStart(source, before)), YamlSyntax);
                        // 块映射第二个起的键是 alias 且紧跟冒号（如 *x: 1）时，YamlDotNet 把冒号留作值指示符，yaml 包按规范把它读进 alias 名（x:）而报找不到锚点；两端都按语法错误报在所在行。
                        if (parent is { Names: not null, Key: true, Flow: false } && current is AnchorAlias && current.End.Index < source.Length && source[(int)current.End.Index] == ':')
                            return new Problem(LineAt(source, (int)current.Start.Index), YamlSyntax);
                        if (parent?.Names is { } names)
                        {
                            if (parent.Key && current is Scalar key && duplicate == 0 && !emptyDuplicate && !names.Add(key.Value))
                            {
                                if (key.Start.Index == key.End.Index) emptyDuplicate = true;
                                else duplicate = (int)key.Start.Line;
                            }
                            parent.Key = !parent.Key;
                        }
                        var indent = parent?.Indent ?? -1;
                        if (current is Scalar { Style: ScalarStyle.Literal or ScalarStyle.Folded } block)
                            while (pending < tabs.Count && tabs[pending] < block.End.Index && tabs[pending] > 0 && source[tabs[pending] - 1] != '\n') pending++;
                        if (current is Scalar { Style: ScalarStyle.SingleQuoted or ScalarStyle.DoubleQuoted } quoted && (Underindented(source, quoted, indent) || EscapedBreakBeforeEmptyLine(source, quoted)))
                            return new Problem((int)quoted.Start.Line, YamlSyntax);
                        if (current is MappingStart { Style: MappingStyle.Block } or SequenceStart { Style: SequenceStyle.Block }) indent = (int)current.Start.Column - 1;
                        var flowStyle = current is MappingStart { Style: MappingStyle.Flow } or SequenceStart { Style: SequenceStyle.Flow };
                        if (current is MappingStart) frames.Push(new Frame(new HashSet<string>(StringComparer.Ordinal), indent, flowStyle));
                        else if (current is SequenceStart) frames.Push(new Frame(null, indent, flowStyle));
                        break;
                }
            }
        }
        catch (YamlException error)
        {
            // 没有位置的错误（如制表符缩进）取扫描停下处；纯量里制表符违反缩进时取制表符所在（End）；其余取 Start，引号标量的错误因此报在标量开始处。
            var mark = error.Start.Equals(Mark.Empty) && error.End.Equals(Mark.Empty) ? scanner.CurrentPosition
                : error.Message.StartsWith("While scanning a plain scalar, found a tab character", StringComparison.Ordinal) ? error.End : error.Start;
            return TabBefore(mark.Index) ?? new Problem(LineAt(source, (int)Math.Min(mark.Index, source.Length)), YamlSyntax);
        }
        catch (ArgumentOutOfRangeException)
        {
            // YamlDotNet 18.1.0 的组对缺陷：高代理项转义之后紧跟不是低代理项的转义时抛出，按所在双引号标量的起点报，同其他未成对的代理项。
            var quote = OpeningQuote(source, (int)scanner.CurrentPosition.Index);
            var at = quote < 0 ? (int)Math.Min(scanner.CurrentPosition.Index, source.Length) : quote;
            return TabBefore(at) ?? new Problem(LineAt(source, at), YamlSyntax);
        }
        catch (Exception error) when (error is InvalidOperationException or ArgumentException)
        {
            // 未闭合的流式集合等抛非语法异常，按扫描停下的位置报。
            var at = (int)Math.Min(scanner.CurrentPosition.Index, source.Length);
            return TabBefore(at) ?? new Problem(LineAt(source, at), YamlSyntax);
        }
        if (TabBefore(long.MaxValue) is { } rest) return rest;
        if (alias) return new Problem(1, Generic);
        return duplicate > 0 ? new Problem(duplicate, YamlSyntax) : null;
    }

    // 两端同一套逐行预检，先于解析：禁用字符；指令行（两库对指令的处理差异很大，%YAML 1.1 还会改变 yaml 包的解析规则）；第二个文档，
    // 即内容或 --- 之后又出现 ---，或 ... 之后还有内容（只有空行与注释不算）。两库对文档边界的计数不同（如多余的 ...），这里不依赖它们。
    private static Problem? Prescan(string source)
    {
        bool content = false, ended = false;
        for (int start = 0, line = 1; start <= source.Length; line++)
        {
            var end = source.IndexOf('\n', start);
            if (end < 0) end = source.Length;
            var text = source.AsSpan(start, end - start);
            foreach (var character in text)
                if (character is (char)0 or '\r' or (char)0x85 or (char)0x2028 or (char)0x2029) return new Problem(line, YamlCharacters);
            if (text.StartsWith("%")) return new Problem(line, YamlDirectives);
            var marker = text.Length == 3 || text.Length > 3 && text[3] is ' ' or '\t' ? text[..3] : default;
            var rest = text.TrimStart(" \t");
            if (marker.SequenceEqual("---"))
            {
                if (content || ended) return new Problem(line, YamlDocuments);
                content = true;
            }
            else if (marker.SequenceEqual("...")) ended = true;
            else if (!rest.IsEmpty && rest[0] != '#')
            {
                if (ended) return new Problem(line, YamlDocuments);
                content = true;
            }
            start = end + 1;
        }
        return null;
    }

    // 流式集合的续行（同 yaml 包的扫描器）：开头的空格须多于所在块集合的缩进，空行与注释行除外；最外层流式集合的结束括号可以与块集合同列。
    // YamlDotNet 不查这一条。检查上一个事件之后、当前事件开始之前的各行，返回首个违规行的行首，没有时为 -1。
    private static int FlowUnderindented(string source, long from, ParsingEvent current, Stack<Frame> frames)
    {
        var indent = frames.Peek().Indent;
        if (indent < 0) return -1;
        for (var newline = source.IndexOf('\n', (int)Math.Min(from, source.Length)); newline >= 0 && newline < current.Start.Index; newline = source.IndexOf('\n', newline + 1))
        {
            var index = newline + 1;
            while (index < source.Length && source[index] == ' ') index++;
            var spaces = index - newline - 1;
            while (index < source.Length && source[index] is ' ' or '\t') index++;
            if (index >= source.Length || source[index] is '\n' or '#' || spaces > indent) continue;
            if (current is SequenceEnd or MappingEnd && index == current.Start.Index && spaces == indent && frames.Count(frame => frame.Flow) == 1) continue;
            return newline + 1;
        }
        return -1;
    }

    private static int LineAt(string source, int index) => source.AsSpan(0, index).Count('\n') + 1;

    // 每行行首空白里（空格之后）的第一个制表符。块标量正文里空格之后的制表符属于内容，由调用方排除。
    private static List<int> IndentTabs(string source)
    {
        var tabs = new List<int>();
        for (var start = 0; ;)
        {
            var index = start;
            while (index < source.Length && source[index] == ' ') index++;
            if (index < source.Length && source[index] == '\t') tabs.Add(index);
            var end = source.IndexOf('\n', start);
            if (end < 0) return tabs;
            start = end + 1;
        }
    }

    // 引号标量的续行（非空行）开头至少要有「所在块集合的缩进 + 1」个空格，同 yaml 包的扫描器；YamlDotNet 只查双引号，且把制表符算作缩进。
    private static bool Underindented(string source, Scalar scalar, int indent)
    {
        if (indent < 0 || scalar.End.Line == scalar.Start.Line) return false;
        var quote = source.IndexOf(scalar.Style == ScalarStyle.DoubleQuoted ? '"' : '\'', (int)scalar.Start.Index);
        var end = (int)Math.Min(scalar.End.Index, source.Length);
        for (var index = quote < 0 ? -1 : source.IndexOf('\n', quote); index >= 0 && index < end; index = source.IndexOf('\n', index + 1))
        {
            var next = index + 1;
            while (next < source.Length && source[next] == ' ') next++;
            if (next < source.Length && source[next] != '\n' && next - index - 1 <= indent) return true;
        }
        return false;
    }

    // 双引号标量里转义的换行（奇数个反斜杠紧跟换行）之后紧跟空行：yaml 包按普通折叠少算一个换行，与规范和 YamlDotNet 不同，两端都按语法错误拒绝。
    private static bool EscapedBreakBeforeEmptyLine(string source, Scalar scalar)
    {
        if (scalar.Style != ScalarStyle.DoubleQuoted || scalar.End.Line == scalar.Start.Line) return false;
        var quote = source.IndexOf('"', (int)scalar.Start.Index);
        var end = (int)Math.Min(scalar.End.Index, source.Length);
        for (var index = quote < 0 ? -1 : source.IndexOf('\n', quote); index >= 0 && index < end; index = source.IndexOf('\n', index + 1))
        {
            var slashes = 0;
            while (source[index - slashes - 1] == '\\') slashes++;
            if (slashes % 2 == 0) continue;
            var next = index + 1;
            while (next < source.Length && source[next] is ' ' or '\t') next++;
            if (next < source.Length && source[next] == '\n') return true;
        }
        return false;
    }

    // 从上一个事件的结束处起，跳过空白、换行与注释，返回首个内容字符的位置（都没有时为末尾）。流式集合结束事件的位置落在右括号上，先跳过它。
    private static int EntryStart(string source, long from)
    {
        var index = (int)Math.Min(from, source.Length);
        if (index < source.Length && source[index] is ']' or '}') index++;
        while (index < source.Length && source[index] is ' ' or '\t' or '\n' or '#')
        {
            if (source[index] != '#') index++;
            else if ((index = source.IndexOf('\n', index)) < 0) index = source.Length;
        }
        return index;
    }

    // 节点开始处往回只隔空格与制表符、其中有制表符、再往前是「-」或「?」时，返回空白开始的位置，否则为 -1。
    private static int TabAfterIndicator(string source, long start)
    {
        var index = (int)Math.Min(start, source.Length) - 1;
        var tab = false;
        while (index >= 0 && source[index] is ' ' or '\t') tab |= source[index--] == '\t';
        return tab && index >= 0 && source[index] is '-' or '?' ? index + 1 : -1;
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

    // 标量按下列规则取值，CLI 的 resolveScalar 逐条对应：显式 !!str 或 !、未加 tag 的引号与块标量为字符串；.inf/.nan 拒绝；0x、0o 整数
    // 与十进制数值按双精度取值（舍入同 JS），超出范围拒绝；null、~、空为 null；true/false 三种大小写为布尔；显式 !!int/!!float/!!bool/!!null
    // 而内容不符时拒绝；其余为字符串。正则用 \z 锚定，避免 $ 在末尾换行之前也成立。
    private static JsonNode? ReadYaml(YamlNode node, string pointer, int offset, Dictionary<string, int> lines, int depth)
    {
        if (depth >= 64 && node is YamlMappingNode or YamlSequenceNode) throw new InvalidDataException("嵌套过深。");
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
        if (Regex.IsMatch(value, "^(?:[-+]?\\.(?:inf|Inf|INF)|\\.nan|\\.NaN|\\.NAN)\\z", RegexOptions.CultureInvariant)) throw new InvalidDataException("JSON 不支持非有限数值。");
        if (Regex.IsMatch(value, "^(?:0x[0-9a-fA-F]+|0o[0-7]+)\\z", RegexOptions.CultureInvariant))
        {
            var radix = value[1] == 'x' ? 16 : 8;
            BigInteger integer = 0;
            foreach (var digit in value[2..].ToLowerInvariant()) integer = integer * radix + (digit <= '9' ? digit - '0' : digit - 'a' + 10);
            // 经十进制文本转换，按 IEEE 就近舍入；(double)BigInteger 会截断低位，与 JS 的 Number(BigInt) 不同。
            var converted = double.Parse(integer.ToString(CultureInfo.InvariantCulture), CultureInfo.InvariantCulture);
            if (!double.IsFinite(converted)) throw new InvalidDataException("数值溢出。");
            return JsonValue.Create(converted);
        }
        if (value is "null" or "Null" or "NULL" or "~" or "") return null;
        if (value is "true" or "True" or "TRUE") return JsonValue.Create(true);
        if (value is "false" or "False" or "FALSE") return JsonValue.Create(false);
        var numeric = Regex.IsMatch(value, "^[-+]?(?:[0-9]+|\\.[0-9]+|[0-9]+\\.[0-9]*)(?:[eE][-+]?[0-9]+)?\\z", RegexOptions.CultureInvariant);
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

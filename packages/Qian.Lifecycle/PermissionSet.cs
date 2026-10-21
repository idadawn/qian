using System.Diagnostics.CodeAnalysis;
using System.Text;
using System.Text.Json;
using Qian.Registry;
using Qian.Runtime.Errors;

namespace Qian.Lifecycle;

// 反向权限五类的规范形：每类去重并按序数排序，秘密以 (name, scope) 为一项。批准记录、开通与升级的门禁和缺失项说明共用这一份。
internal sealed class PermissionSet
{
    private const int MaxItems = 256;
    private const int MaxLength = 512;
    private static readonly string[] SecretScopes = ["platform", "tenant", "dev"];
    private static readonly IComparer<(string Name, string Scope)> SecretOrder =
        Comparer<(string Name, string Scope)>.Create((x, y) => string.CompareOrdinal(x.Name, y.Name) is var name and not 0 ? name : string.CompareOrdinal(x.Scope, y.Scope));

    private PermissionSet(IEnumerable<string> platform, IEnumerable<string> data, IEnumerable<string> egress, IEnumerable<(string Name, string Scope)> secrets, IEnumerable<string> connectSrc)
    {
        Platform = new SortedSet<string>(platform, StringComparer.Ordinal); Data = new SortedSet<string>(data, StringComparer.Ordinal);
        Egress = new SortedSet<string>(egress, StringComparer.Ordinal); Secrets = new SortedSet<(string Name, string Scope)>(secrets, SecretOrder);
        ConnectSrc = new SortedSet<string>(connectSrc, StringComparer.Ordinal);
    }

    internal SortedSet<string> Platform { get; }
    internal SortedSet<string> Data { get; }
    internal SortedSet<string> Egress { get; }
    internal SortedSet<(string Name, string Scope)> Secrets { get; }
    internal SortedSet<string> ConnectSrc { get; }
    internal bool IsEmpty => Platform.Count == 0 && Data.Count == 0 && Egress.Count == 0 && Secrets.Count == 0 && ConnectSrc.Count == 0;
    internal static PermissionSet Empty => new([], [], [], [], []);

    // 制品清单的声明（已过 permissions/v2 Schema）。
    internal static PermissionSet From(RegistryArtifactPermissions declared) => new(declared.Platform, declared.Data, declared.Egress,
        declared.Secrets.Select(secret => (secret.Name, secret.Scope)), declared.Ui.ConnectSrc);

    // 调用方提交的批准内容：缺省的类别视为空；按请求体字段顺序逐项校验形状，pointer 指向请求体中的位置，最多报 100 项。
    internal static PermissionSet Read([NotNull] PermissionGrants? grants)
    {
        if (grants is null) throw Invalid([("#", "批准内容必须是对象。")]);
        var errors = new List<(string Pointer, string Reason)>();
        var platform = Strings(grants.Platform, "#/platform"); var data = Strings(grants.Data, "#/data"); var egress = Strings(grants.Egress, "#/egress");
        var secrets = new List<(string Name, string Scope)>();
        var items = grants.Secrets ?? [];
        if (items.Count > MaxItems) errors.Add(("#/secrets", "每类最多 256 项。"));
        else
            for (var index = 0; index < items.Count; index++)
            {
                if (items[index] is not { } secret) { errors.Add(($"#/secrets/{index}", "秘密项必须是 { name, scope } 对象。")); continue; }
                if (!Valid(secret.Name)) errors.Add(($"#/secrets/{index}/name", "秘密名称无效。"));
                else if (!SecretScopes.Contains(secret.Scope, StringComparer.Ordinal)) errors.Add(($"#/secrets/{index}/scope", "scope 只能是 platform、tenant 或 dev。"));
                else secrets.Add((secret.Name, secret.Scope));
            }
        var connectSrc = Strings(grants.Ui?.ConnectSrc, "#/ui/connectSrc");
        if (errors.Count != 0) throw Invalid(errors);
        return new PermissionSet(platform, data, egress, secrets, connectSrc);

        List<string> Strings(IReadOnlyList<string>? values, string pointer)
        {
            var result = new List<string>();
            if (values is null) return result;
            if (values.Count > MaxItems) { errors.Add((pointer, "每类最多 256 项。")); return result; }
            for (var index = 0; index < values.Count; index++)
                if (Valid(values[index])) result.Add(values[index]); else errors.Add(($"{pointer}/{index}", "批准项必须是非空、不含控制字符且不超过 512 字符的字符串。"));
            return result;
        }
    }

    // 只能批准该版本声明过的项：逐项对照请求体，pointer 指向未声明的项。
    internal static void RequireDeclared(PermissionGrants grants, PermissionSet declared)
    {
        var errors = new List<(string Pointer, string Reason)>();
        Check(grants.Platform, declared.Platform, "#/platform"); Check(grants.Data, declared.Data, "#/data"); Check(grants.Egress, declared.Egress, "#/egress");
        var secrets = grants.Secrets ?? [];
        for (var index = 0; index < secrets.Count; index++)
            if (!declared.Secrets.Contains((secrets[index].Name, secrets[index].Scope))) errors.Add(($"#/secrets/{index}", "该项不在组件版本声明的反向权限中。"));
        Check(grants.Ui?.ConnectSrc, declared.ConnectSrc, "#/ui/connectSrc");
        if (errors.Count != 0) throw Invalid(errors);

        void Check(IReadOnlyList<string>? values, SortedSet<string> allowed, string pointer)
        {
            for (var index = 0; index < (values?.Count ?? 0); index++)
                if (!allowed.Contains(values![index])) errors.Add(($"{pointer}/{index}", "该项不在组件版本声明的反向权限中。"));
        }
    }

    // 本集合中未被 approved 覆盖的项。
    internal PermissionSet Except(PermissionSet approved) => new(Platform.Except(approved.Platform), Data.Except(approved.Data), Egress.Except(approved.Egress),
        Secrets.Where(secret => !approved.Secrets.Contains(secret)), ConnectSrc.Except(approved.ConnectSrc));

    internal bool SetEquals(PermissionSet other) => Platform.SetEquals(other.Platform) && Data.SetEquals(other.Data) && Egress.SetEquals(other.Egress) &&
        Secrets.SetEquals(other.Secrets) && ConnectSrc.SetEquals(other.ConnectSrc);

    internal PermissionGrants ToGrants() => new(Array.AsReadOnly(Platform.ToArray()), Array.AsReadOnly(Data.ToArray()), Array.AsReadOnly(Egress.ToArray()),
        Array.AsReadOnly(Secrets.Select(secret => new SecretGrant(secret.Name, secret.Scope)).ToArray()), new UiGrants(Array.AsReadOnly(ConnectSrc.ToArray())));

    // 批准记录的 jsonb：与 HTTP 视图同形（camelCase），已规范化。
    internal string Serialize()
    {
        using var buffer = new MemoryStream();
        using (var writer = new Utf8JsonWriter(buffer))
        {
            writer.WriteStartObject();
            Array(writer, "platform", Platform); Array(writer, "data", Data); Array(writer, "egress", Egress);
            writer.WriteStartArray("secrets");
            foreach (var secret in Secrets) { writer.WriteStartObject(); writer.WriteString("name", secret.Name); writer.WriteString("scope", secret.Scope); writer.WriteEndObject(); }
            writer.WriteEndArray();
            writer.WriteStartObject("ui"); Array(writer, "connectSrc", ConnectSrc); writer.WriteEndObject();
            writer.WriteEndObject();
        }
        return Encoding.UTF8.GetString(buffer.ToArray());
        static void Array(Utf8JsonWriter writer, string name, IEnumerable<string> values)
        {
            writer.WriteStartArray(name);
            foreach (var value in values) writer.WriteStringValue(value);
            writer.WriteEndArray();
        }
    }

    internal static PermissionSet Parse(string json)
    {
        using var document = JsonDocument.Parse(json);
        var root = document.RootElement;
        return new PermissionSet(Strings(root, "platform"), Strings(root, "data"), Strings(root, "egress"),
            root.GetProperty("secrets").EnumerateArray().Select(item => (item.GetProperty("name").GetString()!, item.GetProperty("scope").GetString()!)).ToArray(),
            Strings(root.GetProperty("ui"), "connectSrc"));
        static string[] Strings(JsonElement element, string name) => element.GetProperty(name).EnumerateArray().Select(item => item.GetString()!).ToArray();
    }

    // 缺失项说明写进错误文案：按类别列出，截断到 500 字符。
    internal string Describe()
    {
        var parts = new List<string>();
        if (Platform.Count != 0) parts.Add("platform " + string.Join(", ", Platform));
        if (Data.Count != 0) parts.Add("data " + string.Join(", ", Data));
        if (Egress.Count != 0) parts.Add("egress " + string.Join(", ", Egress));
        if (Secrets.Count != 0) parts.Add("secrets " + string.Join(", ", Secrets.Select(secret => secret.Scope + ":" + secret.Name)));
        if (ConnectSrc.Count != 0) parts.Add("ui.connectSrc " + string.Join(", ", ConnectSrc));
        var text = string.Join("; ", parts);
        return text.Length <= 500 ? text : text[..499] + "…";
    }

    private static bool Valid(string? value) => !string.IsNullOrWhiteSpace(value) && value.Length <= MaxLength && !value.Any(char.IsControl);

    private static QianErrorException Invalid(IEnumerable<(string Pointer, string Reason)> errors) => new(new QianError(PlatformErrors.InputInvalid)
    {
        Errors = errors.Take(100).Select(error => new QianFieldError(LifecycleErrors.Invalid, error.Pointer, new Dictionary<string, string> { ["reason"] = error.Reason })).ToArray()
    });
}

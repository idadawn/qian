using System.Text.Json;
using Json.Schema;
using Qian.Authz.Abstractions;
using Qian.Registry;
using Qian.Runtime.Errors;
using Qian.Surfaces.Abstractions;

namespace Qian.Authz;

/// <summary>把平台 metadata 与独立宿主属性编译成相同的权限声明，不执行授予或权限判定。</summary>
public sealed class PermissionCatalogCompiler(ISurfaceReader reader)
{
    private readonly JsonSchema _permissions = Schema("authz-permissions");
    private readonly JsonSchema _roles = Schema("authz-roles");

    /// <summary>消费 Registry 的预装声明；不从候选制品、令牌权限数组或客户端授权布尔值取数据。</summary>
    public PermissionCatalog FromInstalled(InstalledComponentMetadata component)
    {
        ArgumentNullException.ThrowIfNull(component);
        var permissions = Document(component, "permissions.json"); var roles = Document(component, "roles.json");
        return FromMetadata(component.PluginId, component.Version, permissions.Data, roles.Data, permissions.File, roles.File);
    }

    /// <summary>验证 metadata 结构和跨字段约束，风险稀疏映射缺省 low，stepUp 缺省空。</summary>
    public PermissionCatalog FromMetadata(string pluginId, string version, JsonElement permissions, JsonElement roles,
        string permissionFile = "metadata/permissions.json", string roleFile = "metadata/roles.json")
    {
        Identity(pluginId, version);
        permissions = Validate(permissions, permissionFile, _permissions);
        roles = Validate(roles, roleFile, _roles);
        var definitions = new Dictionary<string, PermissionDefinition>(StringComparer.Ordinal);
        var index = 0;
        foreach (var resource in permissions.EnumerateArray())
        {
            var location = Source(permissionFile) + "#/" + index++;
            var name = resource.GetProperty("resource").GetString()!;
            var actions = resource.GetProperty("actions").EnumerateArray().Select(action => action.GetString()!).ToHashSet(StringComparer.Ordinal);
            var risk = resource.TryGetProperty("risk", out var risks) ? risks.EnumerateObject().ToDictionary(item => item.Name, item => Risk(item.Value.GetString()!), StringComparer.Ordinal) : [];
            var stepUp = resource.TryGetProperty("stepUp", out var steps) ? steps.EnumerateArray().Select(item => item.GetString()!).ToHashSet(StringComparer.Ordinal) : [];
            var terminals = resource.TryGetProperty("terminals", out var terminalMap) ? terminalMap.EnumerateObject().ToDictionary(item => item.Name,
                item => item.Value.EnumerateArray().Select(value => value.GetString()!).ToArray(), StringComparer.Ordinal) : [];
            if (risk.Keys.Concat(stepUp).Concat(terminals.Keys).Any(action => !actions.Contains(action)))
                throw Invalid(location, "风险、step-up 和终端只能引用本条声明的动作。");
            foreach (var action in actions)
            {
                var definition = new PermissionDefinition(pluginId, name, action, risk.GetValueOrDefault(action, PermissionRisk.Low), stepUp.Contains(action), terminals.GetValueOrDefault(action, []));
                if (!definitions.TryAdd(definition.Code, definition)) throw Invalid(location + "/actions", "权限点重复声明。");
            }
        }
        return Build(pluginId, version, definitions.Values, roles, roleFile);
    }

    /// <summary>消费编译期生成或反射取得的真实 PermissionAttribute；本方法不扫描程序集。</summary>
    public PermissionCatalog FromAttributes(string pluginId, string version, IEnumerable<PermissionAttribute> attributes,
        JsonElement roles, string roleFile = "metadata/roles.json")
    {
        Identity(pluginId, version); ArgumentNullException.ThrowIfNull(attributes);
        var definitions = new Dictionary<string, PermissionDefinition>(StringComparer.Ordinal);
        foreach (var attribute in attributes)
        {
            PermissionDefinition definition;
            try { definition = new PermissionDefinition(pluginId, attribute.Resource, attribute.Action, attribute.Risk, attribute.RequiresStepUp, attribute.Terminals); }
            catch (ArgumentException) { throw Invalid("attributes#/permissions", "属性中的权限声明无效。"); }
            if (!definitions.TryAdd(definition.Code, definition)) throw Invalid("attributes#/permissions", "属性权限点重复声明。");
        }
        return Build(pluginId, version, definitions.Values, Validate(roles, roleFile, _roles), roleFile);
    }

    private static PermissionCatalog Build(string pluginId, string version, IEnumerable<PermissionDefinition> definitions, JsonElement roles, string roleFile)
    {
        var permissions = definitions.OrderBy(permission => permission.Code, StringComparer.Ordinal).ToArray();
        var templates = new List<RoleTemplate>(); var names = new HashSet<string>(StringComparer.Ordinal); var index = 0;
        foreach (var role in roles.EnumerateArray())
        {
            var location = Source(roleFile) + "#/" + index++;
            var name = role.GetProperty("role").GetString()!;
            if (name.StartsWith("platform.", StringComparison.Ordinal)) throw Invalid(location + "/role", "组件默认模板不能授予平台角色。");
            if (!names.Add(name)) throw Invalid(location + "/role", "角色模板重复声明。");
            var patterns = role.GetProperty("grant").EnumerateArray().Select(item => item.GetString()!).ToArray();
            var grants = new HashSet<string>(StringComparer.Ordinal);
            for (var i = 0; i < patterns.Length; i++)
            {
                var pieces = patterns[i].Split(':');
                if (pieces.Length != 3 || pieces[0] != pluginId ||
                    (pieces[1] == "*" ? pieces[2] != "*" : !PermissionCode.IsValid(pluginId + ":" + pieces[1] + ":" + (pieces[2] == "*" ? "read" : pieces[2]))))
                    throw Invalid(location + "/grant/" + i, "角色模板只能引用本组件的确切权限或资源动作通配符。");
                var matches = permissions.Where(permission => (pieces[1] == "*" || permission.Resource == pieces[1]) && (pieces[2] == "*" || permission.Action == pieces[2])).ToArray();
                if (matches.Length == 0) throw Invalid(location + "/grant/" + i, "grant 未匹配任何已登记权限点。");
                foreach (var permission in matches) grants.Add(permission.Code);
            }
            templates.Add(new RoleTemplate(name, patterns, grants.Order(StringComparer.Ordinal).ToArray()));
        }
        return new PermissionCatalog(pluginId, version, permissions, templates.OrderBy(role => role.Role, StringComparer.Ordinal).ToArray());
    }

    private JsonElement Validate(JsonElement data, string file, JsonSchema schema)
    {
        if (string.IsNullOrWhiteSpace(file) || file.Any(char.IsControl)) throw Invalid("metadata#", "元数据来源名无效。");
        var parsed = reader.Parse(file, data.GetRawText());
        if (parsed.Syntax is null || parsed.Diagnostics.Any(d => d.Severity == "error"))
            throw new QianErrorException(new QianError(PlatformErrors.InputInvalid) { Errors = parsed.Diagnostics.Where(d => d.Severity == "error").Select(d =>
                new QianFieldError(AuthzErrors.MetadataInvalid, d.Error.Pointer ?? file + "#", new Dictionary<string, string> { ["reason"] = "元数据 JSON 语法无效。" })).ToArray() });
        var result = schema.Evaluate(parsed.Syntax.Data, new EvaluationOptions { OutputFormat = OutputFormat.Hierarchical });
        if (!result.IsValid) throw new QianErrorException(new QianError(PlatformErrors.InputInvalid)
        {
            Errors = Leaves(result).Take(100).Select(item => new QianFieldError(AuthzErrors.MetadataInvalid, Source(file) + "#" + item.InstanceLocation,
                new Dictionary<string, string> { ["reason"] = "权限元数据不符合 Schema。" })).ToArray()
        });
        return parsed.Syntax.Data;
    }
    private static (string File, JsonElement Data) Document(InstalledComponentMetadata component, string name)
    {
        var found = component.Metadata.Where(item => item.Key.Split('/')[^1] == name).ToArray();
        if (found.Length > 1) throw Invalid("metadata#", "同类元数据来源不唯一。");
        return found.Length == 0 ? ("metadata/" + name, JsonSerializer.SerializeToElement(Array.Empty<object>())) : (found[0].Key, found[0].Value);
    }
    private static IEnumerable<EvaluationResults> Leaves(EvaluationResults result)
    {
        var children = (result.Details ?? []).Where(child => !child.IsValid).ToArray();
        if (children.Length == 0 && !result.IsValid) yield return result;
        foreach (var child in children) foreach (var leaf in Leaves(child)) yield return leaf;
    }
    private static void Identity(string pluginId, string version)
    {
        try { _ = PermissionCode.Format(pluginId, "metadata", "read"); }
        catch (ArgumentException) { throw Invalid("metadata#/pluginId", "组件标识无效。"); }
        if (string.IsNullOrWhiteSpace(version) || version.Length > 200 || version.Any(char.IsControl)) throw Invalid("metadata#/version", "组件版本无效。");
    }
    private static string Source(string file) => string.Join('/', file.Replace('\\', '/').Split('/').Select(Uri.EscapeDataString));
    private static PermissionRisk Risk(string value) => value switch { "low" => PermissionRisk.Low, "medium" => PermissionRisk.Medium, "high" => PermissionRisk.High, "critical" => PermissionRisk.Critical, _ => throw new InvalidOperationException("Schema 未拒绝无效风险词汇。") };
    private static JsonSchema Schema(string name)
    {
        using var input = typeof(PermissionCatalogCompiler).Assembly.GetManifestResourceStream("schemas/" + name + ".schema.json")!;
        using var text = new StreamReader(input);
        return JsonSchema.FromText(text.ReadToEnd(), new BuildOptions { Dialect = Dialect.Draft202012, SchemaRegistry = new SchemaRegistry { Fetch = (_, _) => null } });
    }
    private static QianErrorException Invalid(string location, string reason) => new(new QianError(PlatformErrors.InputInvalid)
    { Errors = [new QianFieldError(AuthzErrors.MetadataInvalid, location, new Dictionary<string, string> { ["reason"] = reason })] });
}

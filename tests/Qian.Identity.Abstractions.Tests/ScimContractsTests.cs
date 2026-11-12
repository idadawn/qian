using System.Text.Json;
using System.Text.Json.Nodes;
using Json.Schema;

namespace Qian.Identity.Abstractions.Tests;

// SCIM 2.0 核心 Schema 契约（S1-IDP-010 验收 3）：contracts/scim/v2 的三份 RFC 7643 Schema 表示、ResourceType 表示、
// 由表示推导的实例 JSON Schema 与样例。服务端点由 S3 实现，这里只锁定契约文件本身。
public sealed class ScimContractsTests
{
    private const string Directory = "contracts/scim/v2";
    private const string CoreUser = "urn:ietf:params:scim:schemas:core:2.0:User";
    private const string CoreGroup = "urn:ietf:params:scim:schemas:core:2.0:Group";
    private const string Enterprise = "urn:ietf:params:scim:schemas:extension:enterprise:2.0:User";
    private static readonly string Root = FindRoot();
    private static readonly EvaluationOptions Strict = new() { OutputFormat = OutputFormat.List, RequireFormatValidation = true };
    private static readonly string[] Types = ["string", "boolean", "decimal", "integer", "dateTime", "binary", "reference", "complex"];
    private static readonly string[] Mutabilities = ["readOnly", "readWrite", "immutable", "writeOnly"];
    private static readonly string[] Returns = ["always", "never", "default", "request"];
    private static readonly string[] Uniquenesses = ["none", "server", "global"];
    private static readonly string[] Common = ["schemas", "id", "externalId", "meta"];
    private static readonly string[] MemberIdentity = ["value", "$ref", "type"];

    // RFC 7643 第 4.1、4.2、4.3 节的属性（按规范大小写）。
    private static readonly string[] UserAttributes = ["userName", "name", "displayName", "nickName", "profileUrl", "title", "userType", "preferredLanguage", "locale", "timezone", "active",
        "password", "emails", "phoneNumbers", "ims", "photos", "addresses", "groups", "entitlements", "roles", "x509Certificates"];
    private static readonly string[] GroupAttributes = ["displayName", "members"];
    private static readonly string[] EnterpriseAttributes = ["employeeNumber", "costCenter", "organization", "division", "department", "manager"];

    private static JsonObject Read(string file) => JsonNode.Parse(File.ReadAllText(Path.Combine(Root, Directory, file)))!.AsObject();
    private static string[] Names(JsonArray attributes) => [.. attributes.Select(attribute => attribute!["name"]!.GetValue<string>())];

    [Fact]
    public void SCIM三份Schema表示的URN与属性集合符合RFC7643_每个属性特征取值合法()
    {
        foreach (var (file, urn, name, expected) in new[]
        {
            ("user.scim-schema.json", CoreUser, "User", UserAttributes), ("group.scim-schema.json", CoreGroup, "Group", GroupAttributes),
            ("enterprise-user.scim-schema.json", Enterprise, "EnterpriseUser", EnterpriseAttributes),
        })
        {
            var schema = Read(file);
            Assert.Equal(["urn:ietf:params:scim:schemas:core:2.0:Schema"], schema["schemas"]!.AsArray().Select(item => item!.GetValue<string>()));
            Assert.Equal((urn, name), (schema["id"]!.GetValue<string>(), schema["name"]!.GetValue<string>()));
            Assert.Equal(("Schema", "/v2/Schemas/" + urn), (schema["meta"]!["resourceType"]!.GetValue<string>(), schema["meta"]!["location"]!.GetValue<string>()));
            Assert.False(string.IsNullOrWhiteSpace(schema["description"]?.GetValue<string>()));
            var attributes = schema["attributes"]!.AsArray();
            Assert.Equal(expected.Order(StringComparer.Ordinal), Names(attributes).Order(StringComparer.Ordinal));
            CheckAttributes(file, attributes, top: true);
        }
        var user = Read("user.scim-schema.json")["attributes"]!.AsArray();
        Assert.Equal(["userName"], user.Where(attribute => attribute!["required"]!.GetValue<bool>()).Select(attribute => attribute!["name"]!.GetValue<string>()));
        Assert.Equal(("server", false), (Attribute(user, "userName")["uniqueness"]!.GetValue<string>(), Attribute(user, "userName")["caseExact"]!.GetValue<bool>()));
        Assert.Equal(("writeOnly", "never"), (Attribute(user, "password")["mutability"]!.GetValue<string>(), Attribute(user, "password")["returned"]!.GetValue<string>()));
        Assert.Equal("readOnly", Attribute(user, "groups")["mutability"]!.GetValue<string>());
        Assert.Equal(["Group", "User"], Attribute(Attribute(user, "groups")["subAttributes"]!.AsArray(), "$ref")["referenceTypes"]!.AsArray().Select(item => item!.GetValue<string>()).Order(StringComparer.Ordinal));
        var group = Read("group.scim-schema.json")["attributes"]!.AsArray();
        Assert.True(Attribute(group, "displayName")["required"]!.GetValue<bool>());
        Assert.All(MemberIdentity, sub => Assert.Equal("immutable", Attribute(Attribute(group, "members")["subAttributes"]!.AsArray(), sub)["mutability"]!.GetValue<string>()));
        var manager = Attribute(Read("enterprise-user.scim-schema.json")["attributes"]!.AsArray(), "manager");
        Assert.Equal(["$ref", "displayName", "value"], Names(manager["subAttributes"]!.AsArray()).Order(StringComparer.Ordinal));
        Assert.Equal(["User"], Attribute(manager["subAttributes"]!.AsArray(), "$ref")["referenceTypes"]!.AsArray().Select(item => item!.GetValue<string>()));
        // ResourceType：User 带可选的企业扩展，Group 没有扩展；引用的 Schema 都在本目录。
        var resourceTypes = JsonNode.Parse(File.ReadAllText(Path.Combine(Root, Directory, "resource-types.json")))!.AsArray();
        Assert.Equal([("Group", "/Groups", CoreGroup), ("User", "/Users", CoreUser)], resourceTypes.Select(item => (item!["id"]!.GetValue<string>(), item["endpoint"]!.GetValue<string>(), item["schema"]!.GetValue<string>())).OrderBy(item => item.Item1, StringComparer.Ordinal));
        var extensions = resourceTypes.Single(item => item!["id"]!.GetValue<string>() == "User")!["schemaExtensions"]!.AsArray();
        Assert.Equal((Enterprise, false), (extensions.Single()!["schema"]!.GetValue<string>(), extensions.Single()!["required"]!.GetValue<bool>()));
        Assert.Null(resourceTypes.Single(item => item!["id"]!.GetValue<string>() == "Group")!["schemaExtensions"]);
        Assert.All(resourceTypes, item => Assert.Equal(("urn:ietf:params:scim:schemas:core:2.0:ResourceType", "ResourceType", "/v2/ResourceTypes/" + item!["id"]!.GetValue<string>()),
            (item!["schemas"]![0]!.GetValue<string>(), item["meta"]!["resourceType"]!.GetValue<string>(), item["meta"]!["location"]!.GetValue<string>())));
    }

    private static JsonObject Attribute(JsonArray attributes, string name) => attributes.Single(attribute => attribute!["name"]!.GetValue<string>() == name)!.AsObject();

    private static void CheckAttributes(string file, JsonArray attributes, bool top)
    {
        // 同一层属性名不区分大小写唯一（RFC 7643 第 2.1 节）。
        Assert.Equal(attributes.Count, Names(attributes).Distinct(StringComparer.OrdinalIgnoreCase).Count());
        foreach (var node in attributes)
        {
            var attribute = node!.AsObject();
            var name = attribute["name"]!.GetValue<string>();
            var at = file + "#" + name;
            var type = attribute["type"]!.GetValue<string>();
            Assert.Contains(type, Types);
            Assert.False(string.IsNullOrWhiteSpace(attribute["description"]?.GetValue<string>()), at);
            Assert.True(attribute["multiValued"] is JsonValue && attribute["required"] is JsonValue, at);
            _ = attribute["multiValued"]!.GetValue<bool>(); _ = attribute["required"]!.GetValue<bool>();
            Assert.Contains(attribute["mutability"]!.GetValue<string>(), Mutabilities);
            Assert.Contains(attribute["returned"]!.GetValue<string>(), Returns);
            if (attribute["uniqueness"] is { } uniqueness) Assert.Contains(uniqueness.GetValue<string>(), Uniquenesses);
            else Assert.Equal("boolean", type);
            // 字符串类属性必须声明大小写敏感性；引用类型必须给出可引用的资源类型；复杂属性必须有子属性，且子属性不能再是复杂属性（第 2.3.8 节）。
            Assert.Equal(type is "string" or "reference" or "binary", attribute["caseExact"] is not null);
            Assert.Equal(type == "reference", attribute["referenceTypes"] is JsonArray { Count: > 0 });
            Assert.Equal(type == "complex", attribute["subAttributes"] is JsonArray { Count: > 0 });
            if (attribute["canonicalValues"] is { } canonical) Assert.NotEmpty(canonical.AsArray());
            if (type == "complex")
            {
                Assert.True(top, at + "：子属性不能是复杂属性");
                CheckAttributes(file, attribute["subAttributes"]!.AsArray(), top: false);
            }
            if (attribute["mutability"]!.GetValue<string>() == "writeOnly") Assert.Equal("never", attribute["returned"]!.GetValue<string>());
        }
    }

    [Fact]
    public void 实例JSONSchema由Schema表示推导_逐属性逐子属性一致()
    {
        var user = Read("user.schema.json");
        var group = Read("group.schema.json");
        Assert.Equal("https://qian.dev/contracts/scim/v2/user.schema.json", user["$id"]!.GetValue<string>());
        Assert.Equal("https://qian.dev/contracts/scim/v2/group.schema.json", group["$id"]!.GetValue<string>());
        var userAttributes = Read("user.scim-schema.json")["attributes"]!.AsArray();
        var enterprise = Read("enterprise-user.scim-schema.json")["attributes"]!.AsArray();
        Assert.Equal(Common.Concat(UserAttributes).Append(Enterprise).Order(StringComparer.Ordinal), user["properties"]!.AsObject().Select(property => property.Key).Order(StringComparer.Ordinal));
        Assert.Equal(Common.Concat(GroupAttributes).Order(StringComparer.Ordinal), group["properties"]!.AsObject().Select(property => property.Key).Order(StringComparer.Ordinal));
        Compare("User", user, userAttributes);
        Compare("Group", group, Read("group.scim-schema.json")["attributes"]!.AsArray());
        var extension = user["properties"]![Enterprise]!.AsObject();
        Compare(Enterprise, extension, enterprise);
        // 根对象：核心 Schema 必填，未声明属性拒绝；企业扩展出现时 schemas 必须列出它的 URN。
        Assert.Equal(["schemas", "userName"], user["required"]!.AsArray().Select(item => item!.GetValue<string>()));
        Assert.Equal(["displayName", "schemas"], group["required"]!.AsArray().Select(item => item!.GetValue<string>()).Order(StringComparer.Ordinal));
        Assert.Equal(CoreUser, user["properties"]!["schemas"]!["contains"]!["const"]!.GetValue<string>());
        Assert.Equal(Enterprise, user["dependentSchemas"]![Enterprise]!["properties"]!["schemas"]!["contains"]!["const"]!.GetValue<string>());
        Assert.True(user["properties"]!["id"]!["readOnly"]!.GetValue<bool>() && user["properties"]!["meta"]!["readOnly"]!.GetValue<bool>());
    }

    // 一个对象 Schema 与一组 SCIM 属性逐一比对：字段集合、必填、类型、多值、只读 / 只写；复杂属性递归比对子属性。
    private static void Compare(string at, JsonObject schema, JsonArray attributes)
    {
        Assert.Equal("object", schema["type"]!.GetValue<string>());
        Assert.False(schema["additionalProperties"]!.GetValue<bool>(), at);
        var properties = schema["properties"]!.AsObject();
        var required = schema["required"]?.AsArray().Select(item => item!.GetValue<string>()).ToHashSet(StringComparer.Ordinal) ?? [];
        foreach (var node in attributes)
        {
            var attribute = node!.AsObject();
            var name = attribute["name"]!.GetValue<string>();
            var property = properties[name]?.AsObject() ?? throw new Xunit.Sdk.XunitException(at + " 缺属性 " + name);
            Assert.Equal(attribute["required"]!.GetValue<bool>(), required.Contains(name));
            var mutability = attribute["mutability"]!.GetValue<string>();
            Assert.Equal(mutability == "readOnly", property["readOnly"]?.GetValue<bool>() ?? false);
            Assert.Equal(mutability == "writeOnly", property["writeOnly"]?.GetValue<bool>() ?? false);
            var item = property;
            if (attribute["multiValued"]!.GetValue<bool>())
            {
                Assert.Equal("array", property["type"]!.GetValue<string>());
                item = property["items"]!.AsObject();
            }
            var expected = attribute["type"]!.GetValue<string>() switch
            {
                "string" => ("string", null), "boolean" => ("boolean", null), "integer" => ("integer", null), "decimal" => ("number", null),
                "dateTime" => ("string", "date-time"), "binary" => ("string", "base64"), "reference" => ("string", "uri-reference"), _ => ("object", (string?)null),
            };
            Assert.Equal(expected, (item["type"]!.GetValue<string>(), item["format"]?.GetValue<string>() ?? item["contentEncoding"]?.GetValue<string>()));
            if (attribute["subAttributes"] is JsonArray sub)
            {
                Assert.Equal(Names(sub).Order(StringComparer.Ordinal), item["properties"]!.AsObject().Select(entry => entry.Key).Order(StringComparer.Ordinal));
                Compare(at + "." + name, item, sub);
            }
        }
    }

    [Fact]
    public void SCIM样例通过实例Schema_缺必填_未声明属性_扩展未声明_类型不对都被拒()
    {
        var options = new BuildOptions { SchemaRegistry = new SchemaRegistry() };
        var user = JsonSchema.FromText(File.ReadAllText(Path.Combine(Root, Directory, "user.schema.json")), options);
        var group = JsonSchema.FromText(File.ReadAllText(Path.Combine(Root, Directory, "group.schema.json")), options);
        string[] Errors(JsonSchema schema, JsonNode instance)
        {
            var result = schema.Evaluate(JsonSerializer.SerializeToElement(instance), Strict);
            return result.IsValid ? [] : [.. (result.Details ?? []).Where(detail => detail.Errors is { Count: > 0 }).Select(detail => detail.InstanceLocation + ": " + string.Join("; ", detail.Errors!.Values)).DefaultIfEmpty("invalid")];
        }
        JsonObject Fixture(string name) => Read("fixtures/" + name);
        foreach (var name in new[] { "user.qian.json", "user.full.json" })
            Assert.True(Errors(user, Fixture(name)) is [], name + "：" + string.Join(" | ", Errors(user, Fixture(name))));
        Assert.True(Errors(group, Fixture("group.qian.json")) is [], string.Join(" | ", Errors(group, Fixture("group.qian.json"))));
        // 完整样例用到了每个核心属性与企业扩展属性。
        var full = Fixture("user.full.json");
        Assert.Equal(Common.Concat(UserAttributes).Append(Enterprise).Where(key => key != "password").Order(StringComparer.Ordinal), full.Select(property => property.Key).Order(StringComparer.Ordinal));
        Assert.Equal(EnterpriseAttributes.Order(StringComparer.Ordinal), full[Enterprise]!.AsObject().Select(property => property.Key).Order(StringComparer.Ordinal));
        // 映射样例：User id 是目录成员 id，部门与主管放在企业扩展里；样例不带口令。
        var mapped = Fixture("user.qian.json");
        Assert.Equal(("zhaoliu", "前端组", "李四"), (mapped["userName"]!.GetValue<string>(), mapped[Enterprise]!["department"]!.GetValue<string>(), mapped[Enterprise]!["manager"]!["displayName"]!.GetValue<string>()));
        Assert.Null(mapped["password"]);
        Assert.Equal(mapped["id"]!.GetValue<string>(), Fixture("group.qian.json")["members"]![0]!["value"]!.GetValue<string>());

        Assert.NotEmpty(Errors(user, Mutate(mapped, node => node.Remove("userName"))));
        Assert.NotEmpty(Errors(user, Mutate(mapped, node => node["schemas"] = new JsonArray(CoreUser))));
        Assert.NotEmpty(Errors(user, Mutate(mapped, node => node["schemas"] = new JsonArray(Enterprise))));
        Assert.NotEmpty(Errors(user, Mutate(mapped, node => node["schemas"]!.AsArray().Add("urn:example:params:scim:schemas:extension:other:1.0:User"))));
        Assert.NotEmpty(Errors(user, Mutate(mapped, node => node["username"] = "zhaoliu")));
        Assert.NotEmpty(Errors(user, Mutate(mapped, node => node["tenant_id"] = "0193a000-0000-7000-8000-000000000900")));
        Assert.NotEmpty(Errors(user, Mutate(mapped, node => node["active"] = "yes")));
        Assert.NotEmpty(Errors(user, Mutate(mapped, node => node["emails"] = new JsonObject { ["value"] = "zhaoliu@example.test" })));
        Assert.NotEmpty(Errors(user, Mutate(mapped, node => node["emails"]![0]!["label"] = "work")));
        Assert.NotEmpty(Errors(user, Mutate(mapped, node => node[Enterprise]!["manager"]!["$ref"] = "not a uri")));
        Assert.NotEmpty(Errors(user, Mutate(mapped, node => node[Enterprise]!["boss"] = "李四")));
        Assert.NotEmpty(Errors(user, Mutate(mapped, node => node["meta"]!["created"] = "昨天")));
        var groupFixture = Fixture("group.qian.json");
        Assert.NotEmpty(Errors(group, Mutate(groupFixture, node => node.Remove("displayName"))));
        Assert.NotEmpty(Errors(group, Mutate(groupFixture, node => node["schemas"] = new JsonArray(CoreUser))));
        Assert.NotEmpty(Errors(group, Mutate(groupFixture, node => node["members"]![0]!["id"] = "x")));
    }

    private static JsonObject Mutate(JsonObject source, Action<JsonObject> change)
    {
        var copy = source.DeepClone().AsObject();
        change(copy);
        return copy;
    }

    private static string FindRoot()
    {
        for (var directory = new DirectoryInfo(AppContext.BaseDirectory); directory is not null; directory = directory.Parent)
            if (File.Exists(Path.Combine(directory.FullName, "Qian.slnx"))) return directory.FullName;
        throw new InvalidOperationException("找不到仓库根目录。");
    }
}

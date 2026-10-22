using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Nodes;
using Json.Schema;
using Microsoft.Extensions.DependencyInjection;
using Qian.Capabilities;
using Qian.Tenancy;

namespace Qian.Sample.Crm.Tests;

public sealed partial class CustomerCommandTests
{
    [Fact]
    public async Task TenantUi_筛选总数稳定分页_字段错误指针_响应与输入Schema来自真实宿主()
    {
        using var platform = new LifecyclePlatform { Management = true };
        await using var business = await Start(lifecycle: platform);
        await using var management = await ManagementHost(business, platform);
        using var client = new HttpClient { BaseAddress = new Uri(management.Urls.Single()) };
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", business.Token(null, "", "root", "user"));
        const string path = "/api/tenancy/tenants";
        // 不带分页参数的真实调用必须成功，不能只检查导出 default。
        Assert.Equal(2, (await Get(path)).GetProperty("items").GetArrayLength());

        var b = await Create("uifilterb", "乙");
        var a = await Create("uifiltera", "甲 100%");
        var first = await Get(path + "?keyword=uifilter&take=1");
        Assert.Equal(2, first.GetProperty("total").GetInt32());
        Assert.Equal("uifiltera", Assert.Single(first.GetProperty("items").EnumerateArray()).GetProperty("code").GetString());
        var second = await Get(path + "?keyword=uifilter&skip=1&take=1");
        Assert.Equal(2, second.GetProperty("total").GetInt32());
        Assert.Equal(b, Assert.Single(second.GetProperty("items").EnumerateArray()).GetProperty("id").GetGuid());
        using (var activated = await client.PostAsJsonAsync(path + "/" + a + ":activate", new { trial = false }, Ct)) Assert.Equal(HttpStatusCode.OK, activated.StatusCode);
        var active = await Get(path + "?keyword=uifilter&status=active");
        Assert.Equal(1, active.GetProperty("total").GetInt32());
        Assert.Equal(a, Assert.Single(active.GetProperty("items").EnumerateArray()).GetProperty("id").GetGuid());
        Assert.Equal(1, (await Get(path + "?keyword=100%25")).GetProperty("total").GetInt32());
        Assert.Equal(0, (await Get(path + "?keyword=_")).GetProperty("total").GetInt32());
        var outOfPage = await Get(path + "?keyword=uifilter&skip=50");
        Assert.Equal(2, outOfPage.GetProperty("total").GetInt32());
        Assert.Empty(outOfPage.GetProperty("items").EnumerateArray());

        foreach (var input in new[] { (Code: "UPPER", Name: "名称", Pointer: "#/code"), (Code: new string('x', 65), Name: "名称", Pointer: "#/code"), (Code: "uivalid", Name: "  ", Pointer: "#/name"), (Code: "uivalid", Name: new string('名', 201), Pointer: "#/name") })
        {
            using var invalid = await client.PostAsJsonAsync(path, new CreateTenant(input.Code, input.Name) { Administrator = new(Guid.NewGuid(), "ui-owner") }, Ct);
            Assert.Equal(HttpStatusCode.BadRequest, invalid.StatusCode);
            var error = await invalid.Content.ReadFromJsonAsync<JsonElement>(Ct);
            Assert.Equal("INPUT_INVALID", error.GetProperty("code").GetString());
            Assert.Equal(input.Pointer, error.GetProperty("pointer").GetString());
            Assert.Equal(input.Pointer, error.GetProperty("errors")[0].GetProperty("pointer").GetString());
        }
        using (var invalidName = await client.PutAsJsonAsync(path + "/" + a, new UpdateTenant("bad\nname", 0), Ct))
        {
            Assert.Equal(HttpStatusCode.BadRequest, invalidName.StatusCode);
            Assert.Equal("#/name", (await invalidName.Content.ReadFromJsonAsync<JsonElement>(Ct)).GetProperty("pointer").GetString());
        }

        var contract = await management.Services.GetRequiredService<IHttpContractSource>().CompileDocumentAsync("qian.tenancy", Ct);
        var document = JsonNode.Parse(contract.GetRawText())!;
        var create = TenantContractSchemaTests.Resolve(document, document["paths"]![path]!["post"]!["requestBody"]!["content"]!["application/json"]!["schema"]!);
        Assert.Equal(64, create["properties"]!["code"]!["maxLength"]!.GetValue<int>());
        Assert.Equal(@"^[a-z0-9]+$(?![\s\S])", create["properties"]!["code"]!["pattern"]!.GetValue<string>());
        Assert.Equal(200, create["properties"]!["name"]!["maxLength"]!.GetValue<int>());
        var metadataSchema = TenantContractSchemaTests.Resolve(document, document["paths"]!["/api/tenancy/meta/state-machine"]!["get"]!["responses"]!["200"]!["content"]!["application/json"]!["schema"]!);
        Assert.NotNull(metadataSchema["properties"]!["states"]);
        Assert.NotNull(metadataSchema["properties"]!["transitions"]);
        var metadata = await Get("/api/tenancy/meta/state-machine");
        Assert.True(JsonElement.DeepEquals(TenantStateMetadata.Document, metadata));
        Assert.True(JsonSchema.FromText(metadataSchema.ToJsonString()).Evaluate(metadata).IsValid);
        var listSchema = TenantContractSchemaTests.Resolve(document, document["paths"]![path]!["get"]!["responses"]!["200"]!["content"]!["application/json"]!["schema"]!);
        var skip = document["paths"]![path]!["get"]!["parameters"]!.AsArray().Single(parameter => parameter!["name"]!.GetValue<string>() == "skip")!["schema"]!;
        Assert.Null(skip["maximum"]);
        Assert.NotNull(listSchema["properties"]!["total"]);
        Assert.Empty(TenantContractSchemaTests.TenantFields(document));

        async Task<Guid> Create(string code, string name)
        {
            using var result = await client.PostAsJsonAsync(path, new CreateTenant(code, name) { Administrator = new(Guid.NewGuid(), "ui-owner-" + code) }, Ct);
            Assert.Equal(HttpStatusCode.Created, result.StatusCode);
            return (await result.Content.ReadFromJsonAsync<JsonElement>(Ct)).GetProperty("id").GetGuid();
        }
        async Task<JsonElement> Get(string url)
        {
            using var result = await client.GetAsync(url, Ct);
            Assert.Equal(HttpStatusCode.OK, result.StatusCode);
            return await result.Content.ReadFromJsonAsync<JsonElement>(Ct);
        }
    }

    [Fact]
    public async Task TenantUi_查询与写入非法输入在HTTP层返回400带pointer_关键字百分号与反斜杠按字面匹配()
    {
        using var platform = new LifecyclePlatform { Management = true };
        await using var business = await Start(lifecycle: platform);
        await using var management = await ManagementHost(business, platform);
        using var client = new HttpClient { BaseAddress = new Uri(management.Urls.Single()) };
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", business.Token(null, "", "root", "user"));
        const string path = "/api/tenancy/tenants";

        // 查询参数：非法状态与超过 200 字符的关键字在 HTTP 层拒绝；恰好 200 字符仍可查询。
        await Invalid(await client.GetAsync(path + "?status=archived", Ct), "#/status");
        await Invalid(await client.GetAsync(path + "?keyword=" + new string('k', 201), Ct), "#/keyword");
        using (var boundary = await client.GetAsync(path + "?keyword=" + new string('k', 200), Ct)) Assert.Equal(HttpStatusCode.OK, boundary.StatusCode);

        // 每组一条字面命中、一条只在通配解释下命中：% 不转义会多出一行，\ 不转义会换成另一行。
        var percent = await Create("uiescpercent", "转义甲%乙");
        await Create("uiescpercentwild", "转义甲丙乙");
        var backslash = await Create("uiescbackslash", @"转义丁\戊");
        await Create("uiescbackslashwild", "转义丁戊");
        Assert.Equal([percent], await Search("甲%乙"));
        Assert.Equal([backslash], await Search(@"丁\戊"));

        // 写入：空租户号、空名称与编辑名称超长在 HTTP 层返回 400 与 pointer，且不落库。
        await Invalid(await client.PostAsJsonAsync(path, new CreateTenant("", "空租户号用例") { Administrator = new(Guid.NewGuid(), "ui-owner") }, Ct), "#/code");
        await Invalid(await client.PostAsJsonAsync(path, new CreateTenant("uiemptyname", "") { Administrator = new(Guid.NewGuid(), "ui-owner") }, Ct), "#/name");
        Assert.Empty(await Search("空租户号用例"));
        Assert.Empty(await Search("uiemptyname"));
        var revision = (await Get(path + "/" + percent)).GetProperty("revision").GetUInt32();
        await Invalid(await client.PutAsJsonAsync(path + "/" + percent, new UpdateTenant(new string('名', 201), revision), Ct), "#/name");
        Assert.Equal("转义甲%乙", (await Get(path + "/" + percent)).GetProperty("name").GetString());

        static async Task Invalid(HttpResponseMessage response, string pointer)
        {
            using (response)
            {
                Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
                var error = await response.Content.ReadFromJsonAsync<JsonElement>(Ct);
                Assert.Equal("INPUT_INVALID", error.GetProperty("code").GetString());
                Assert.Equal(pointer, error.GetProperty("pointer").GetString());
            }
        }
        async Task<Guid> Create(string code, string name)
        {
            using var result = await client.PostAsJsonAsync(path, new CreateTenant(code, name) { Administrator = new(Guid.NewGuid(), "ui-owner-" + code) }, Ct);
            Assert.Equal(HttpStatusCode.Created, result.StatusCode);
            return (await result.Content.ReadFromJsonAsync<JsonElement>(Ct)).GetProperty("id").GetGuid();
        }
        async Task<JsonElement> Get(string url)
        {
            using var result = await client.GetAsync(url, Ct);
            Assert.Equal(HttpStatusCode.OK, result.StatusCode);
            return await result.Content.ReadFromJsonAsync<JsonElement>(Ct);
        }
        async Task<Guid[]> Search(string keyword)
        {
            var page = await Get(path + "?keyword=" + Uri.EscapeDataString(keyword));
            var ids = page.GetProperty("items").EnumerateArray().Select(item => item.GetProperty("id").GetGuid()).ToArray();
            Assert.Equal(ids.Length, page.GetProperty("total").GetInt32());
            return ids;
        }
    }
}

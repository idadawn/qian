using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Json.Schema;
using Kun.Authorization;
using Kun.Core.MultiTenancy;
using Microsoft.AspNetCore.Builder;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using Qian.Audit;
using Qian.Authz;
using Qian.Capabilities;
using Qian.Data;
using Qian.Identity.Abstractions;
using Qian.Plugin.Sdk;
using Qian.Runtime;
using Qian.Runtime.Errors;

namespace Qian.Sample.Crm.Tests;

// S1-RT-019 / S1-RT-020：目录信封与别名头（含 PathBase）、通用执行入口的输入校验 / 幂等重放与回执策略 / Agent 与 Actor 主体、能力上下文迁移。
public sealed partial class CustomerCommandTests
{
    private const string ListId = "qian.sample.crm/customer.list";
    private const string GetId = "qian.sample.crm/customer.get";
    private const string AgentId = "crm-agent-preview";
    private const string Deprecation = "@1791331200";
    private static readonly Lazy<(JsonSchema Catalog, JsonSchema Error)> ContractSchemas = new(LoadContractSchemas);

    [Fact]
    public async Task 目录信封符合冻结契约_别名带Deprecation与successor链接_条目取值与摘要一致_非法参数与匿名请求返回ErrorDetail()
    {
        await using var host = await Start(capabilities: true);
        using var response = await CatalogRequest(host, A, "/api/capabilities");
        Assert.True(response.StatusCode == HttpStatusCode.OK, host.FailureEvidence + await response.Content.ReadAsStringAsync(Ct));
        Assert.Equal("application/json", response.Content.Headers.ContentType?.MediaType);
        Assert.False(response.Headers.Contains("Deprecation")); Assert.False(response.Headers.Contains("Link"));
        var catalog = await Json(response);
        AssertValid(ContractSchemas.Value.Catalog, catalog);
        Assert.Equal(1000, catalog.GetProperty("ttlMs").GetInt32());
        var items = catalog.GetProperty("items").EnumerateArray().ToDictionary(item => item.GetProperty("id").GetString()!, StringComparer.Ordinal);
        Assert.Equal([CreateId, GetId, ListId], items.Keys.Order(StringComparer.Ordinal));
        // schemaSha256 与生产实现 ToolContractHash 一致；该实现按 RFC 8785 的示例与附录 B 样本验证（Qian.Surfaces.Tests/ToolContractHashTests）。
        foreach (var item in items.Values) Assert.Equal(Qian.Surfaces.ToolContractHash.Compute(item), item.GetProperty("schemaSha256").GetString());
        AssertEntry(items[CreateId], "command", "write", true, "crm.write");
        AssertEntry(items[ListId], "query", "read", false, "crm.read");
        AssertEntry(items[GetId], "query", "read", false, "crm.read");

        foreach (var (alias, successor) in new[] { ("/api/kernel/capabilities?kind=command&q=create", "/api/capabilities?kind=command&q=create"),
            ("/api/kernel/capabilities", "/api/capabilities"), ("/api/kernel/capabilities/openapi.json", "/api/capabilities/openapi.json") })
        {
            using var deprecated = await CatalogRequest(host, A, alias);
            Assert.Equal(HttpStatusCode.OK, deprecated.StatusCode);
            Assert.Equal(Deprecation, Assert.Single(deprecated.Headers.GetValues("Deprecation")));
            Assert.Equal("<" + successor + ">; rel=\"successor-version\"", string.Join(", ", deprecated.Headers.GetValues("Link")));
            using var current = await CatalogRequest(host, A, successor);
            Assert.False(current.Headers.Contains("Deprecation")); Assert.False(current.Headers.Contains("Link"));
            Assert.True(JsonElement.DeepEquals(await Json(current), await Json(deprecated)), alias);
        }

        using var queries = await CatalogRequest(host, A, "/api/capabilities?kind=query");
        var queryCatalog = await Json(queries); AssertValid(ContractSchemas.Value.Catalog, queryCatalog);
        Assert.Equal([GetId, ListId], queryCatalog.GetProperty("items").EnumerateArray().Select(item => item.GetProperty("id").GetString()!).Order(StringComparer.Ordinal));
        Assert.All(queryCatalog.GetProperty("items").EnumerateArray(), item => Assert.Equal("query", item.GetProperty("kind").GetString()));
        // 事件与实体目录交付前返回空集合（R77）。
        foreach (var kind in new[] { "event", "entity" })
        {
            using var empty = await CatalogRequest(host, A, "/api/capabilities?kind=" + kind);
            Assert.Empty((await Json(empty)).GetProperty("items").EnumerateArray());
        }
        using var longest = await CatalogRequest(host, A, "/api/capabilities?q=" + new string('x', 200));
        Assert.Equal(HttpStatusCode.OK, longest.StatusCode); Assert.Empty((await Json(longest)).GetProperty("items").EnumerateArray());

        using var badKind = await CatalogRequest(host, A, "/api/capabilities?kind=tool");
        var kindError = await ErrorDetail(badKind, HttpStatusCode.BadRequest, "INPUT_INVALID");
        var kindField = Assert.Single(kindError.GetProperty("errors").EnumerateArray());
        Assert.Equal(("VALUE_NOT_ALLOWED", "#/kind"), (kindField.GetProperty("code").GetString(), kindField.GetProperty("pointer").GetString()));
        Assert.Equal(["command", "query", "event", "entity"], kindField.GetProperty("allowed").EnumerateArray().Select(value => value.GetString()!));
        using var tooLong = await CatalogRequest(host, A, "/api/kernel/capabilities?q=" + new string('x', 201));
        Assert.Equal([("OUT_OF_RANGE", "#/q")], Fields(await ErrorDetail(tooLong, HttpStatusCode.BadRequest, "INPUT_INVALID")));
        using var both = await CatalogRequest(host, A, "/api/capabilities?kind=tool&q=" + new string('x', 201));
        Assert.Equal([("VALUE_NOT_ALLOWED", "#/kind"), ("OUT_OF_RANGE", "#/q")], Fields(await ErrorDetail(both, HttpStatusCode.BadRequest, "INPUT_INVALID")));

        foreach (var path in new[] { "/api/capabilities", "/api/capabilities/openapi.json", "/api/kernel/capabilities", "/api/kernel/capabilities/openapi.json" })
        {
            using var anonymous = await host.Client.GetAsync(path, Ct);
            await ErrorDetail(anonymous, HttpStatusCode.Unauthorized, "UNAUTHENTICATED");
        }
    }

    [Fact]
    public async Task 通用执行入口先按inputSchema校验_不符返回INPUT_INVALID与JSON_Pointer_读不成确定值或数超出decimal范围返回BAD_REQUEST_不可见能力不暴露校验结果_均无写入()
    {
        await using var host = await Start(capabilities: true);
        var key = Guid.CreateVersion7().ToString();
        using var missing = await InvokeRaw(host, A, CreateId, "{\"idempotencyKey\":\"" + key + "\",\"nickname\":\"x\"}");
        Assert.Equal([("REQUIRED", "#/name"), ("UNKNOWN_PROPERTY", "#/nickname")], Fields(await ErrorDetail(missing, HttpStatusCode.BadRequest, "INPUT_INVALID")));
        using var wrong = await InvokeRaw(host, A, CreateId, "{\"idempotencyKey\":\"not-a-uuid\",\"name\":42}");
        Assert.Equal([("FORMAT_INVALID", "#/idempotencyKey"), ("TYPE_MISMATCH", "#/name")], Fields(await ErrorDetail(wrong, HttpStatusCode.BadRequest, "INPUT_INVALID")));
        using var array = await InvokeRaw(host, A, CreateId, "[]");
        Assert.Equal([("TYPE_MISMATCH", "#")], Fields(await ErrorDetail(array, HttpStatusCode.BadRequest, "INPUT_INVALID")));
        using var take = await InvokeRaw(host, A, ListId, "{\"skip\":\"0\"}");
        Assert.Equal([("TYPE_MISMATCH", "#/skip")], Fields(await ErrorDetail(take, HttpStatusCode.BadRequest, "INPUT_INVALID")));
        using var duplicate = await InvokeRaw(host, A, CreateId, "{\"idempotencyKey\":\"" + key + "\",\"name\":\"a\",\"name\":\"b\"}");
        await ErrorDetail(duplicate, HttpStatusCode.BadRequest, "BAD_REQUEST");
        using var broken = await InvokeRaw(host, A, CreateId, "{\"name\":");
        await ErrorDetail(broken, HttpStatusCode.BadRequest, "BAD_REQUEST");
        // 字符串含孤立的 UTF-16 代理项（\ud800 转义）：读不成确定的值，同样是 400 BAD_REQUEST 而不是 500。
        using var surrogate = await InvokeRaw(host, A, CreateId, "{\"idempotencyKey\":\"" + key + "\",\"name\":\"" + "\\" + "ud800\"}");
        await ErrorDetail(surrogate, HttpStatusCode.BadRequest, "BAD_REQUEST");
        // 超出 decimal 范围的数（绝对值大于约 7.9e28，任何位置）：JsonSchema.Net 9.4 比较 enum、const、uniqueItems 时会因此抛异常，
        // 求值前就按 400 BAD_REQUEST 拒绝，不执行。decimal.MaxValue 本身照常进入校验与绑定（int 放不下，按 INPUT_INVALID 拒绝）。
        foreach (var number in new[] { "1e29", "-1e400", "79228162514264337593543950336" })
        {
            using var huge = await InvokeRaw(host, A, CreateId, "{\"idempotencyKey\":\"" + key + "\",\"name\":\"huge\",\"extra\":{\"values\":[1," + number + "]}}");
            await ErrorDetail(huge, HttpStatusCode.BadRequest, "BAD_REQUEST");
            using var skip = await InvokeRaw(host, A, ListId, "{\"skip\":" + number + ",\"take\":10}");
            await ErrorDetail(skip, HttpStatusCode.BadRequest, "BAD_REQUEST");
        }
        using var largest = await InvokeRaw(host, A, ListId, "{\"skip\":79228162514264337593543950335,\"take\":10}");
        await ErrorDetail(largest, HttpStatusCode.BadRequest, "INPUT_INVALID");
        // 先判可见再校验：租户 B 无写权限，同样的坏输入只得到与不存在相同的 404。
        using var hidden = await InvokeRaw(host, B, CreateId, "{\"nickname\":\"x\"}");
        using var unknown = await InvokeRaw(host, B, "qian.sample.crm/customer.unknown", "{\"nickname\":\"x\"}");
        await ErrorDetail(hidden, HttpStatusCode.NotFound, "NOT_FOUND");
        Assert.True(JsonNode.DeepEquals(Normalize(await hidden.Content.ReadAsStringAsync(Ct)), Normalize(await unknown.Content.ReadAsStringAsync(Ct))));
        await AssertWrites(host, A, customers: 0, audit: 0);
        using var accepted = await InvokeRaw(host, A, CreateId, "{\"idempotencyKey\":\"" + key + "\",\"name\":\"schema accepted\"}");
        Assert.True(accepted.StatusCode == HttpStatusCode.OK, host.FailureEvidence + await accepted.Content.ReadAsStringAsync(Ct));
        await AssertWrites(host, A, customers: 1, audit: 1);
    }

    [Fact]
    public async Task 幂等键重放返回首次成功响应且不再执行_同键异体或执行中409_非法键400_失败不留回执_窗口过期后重新执行_并发同键只执行一次()
    {
        // 回放成功由 internal 分级的探针承担：CRM 新建客户的响应含客户名称、标为 pii，回执不存响应体（见下一个用例）。
        var mirror = new ReceiptProbe("customer.mirror", "internal", 16);
        await using var host = await Start(capabilities: true, probes: [mirror]);
        var key = Guid.CreateVersion7(); var body = ProbeInput("idempotent probe");
        using var first = await InvokeProbe(host, mirror.Id, body, key.ToString());
        Assert.True(first.StatusCode == HttpStatusCode.OK, host.FailureEvidence + await first.Content.ReadAsStringAsync(Ct));
        Assert.False(first.Headers.Contains("Idempotent-Replayed"));
        var response = await first.Content.ReadAsStringAsync(Ct);
        // 结构化字段的带引号形式与裸 UUID 是同一个键；探针每次执行都生成新 ID，响应逐字节相同说明是回放。
        using var replay = await InvokeProbe(host, mirror.Id, body, "\"" + key + "\"");
        Assert.Equal(HttpStatusCode.OK, replay.StatusCode);
        Assert.Equal("true", Assert.Single(replay.Headers.GetValues("Idempotent-Replayed")));
        Assert.Equal(response, await replay.Content.ReadAsStringAsync(Ct));
        Assert.Equal(1, mirror.Calls);
        // 同键异体：409 来自回执层，探针不执行。
        using var conflict = await InvokeProbe(host, mirror.Id, ProbeInput("another probe"), key.ToString());
        await ErrorDetail(conflict, HttpStatusCode.Conflict, "CONFLICT");
        foreach (var invalid in new[] { "not-a-uuid", Guid.Empty.ToString(), "", "{" + key + "}", key + ", " + Guid.CreateVersion7() })
        {
            using var rejected = await InvokeProbe(host, mirror.Id, body, invalid);
            await ErrorDetail(rejected, HttpStatusCode.BadRequest, "BAD_REQUEST");
        }
        Assert.Equal(1, mirror.Calls);

        // 回放窗口过期后同键重新执行。
        await Admin($"UPDATE plugin_qian_capabilities.invoke_receipts SET expires_at = now() - interval '1 second' WHERE idempotency_key = '{key}';");
        using var expired = await InvokeProbe(host, mirror.Id, body, key.ToString());
        Assert.Equal(HttpStatusCode.OK, expired.StatusCode); Assert.False(expired.Headers.Contains("Idempotent-Replayed"));
        Assert.NotEqual(response, await expired.Content.ReadAsStringAsync(Ct));
        Assert.Equal(2, mirror.Calls);

        // 同键请求仍在执行（复制同一主体、同一输入的登记行，响应为空、租约未到期）：返回 409，不执行；租约到期后标记失效，同键重新执行并写完成。
        var pending = Guid.CreateVersion7();
        await Admin($"INSERT INTO plugin_qian_capabilities.invoke_receipts (tenant_id, subject_key, capability_id, idempotency_key, request_sha256, response, created_at, expires_at) " +
            $"SELECT tenant_id, subject_key, capability_id, '{pending}', request_sha256, NULL, now(), now() + interval '5 minutes' FROM plugin_qian_capabilities.invoke_receipts WHERE idempotency_key = '{key}';");
        using var inFlight = await InvokeProbe(host, mirror.Id, body, pending.ToString());
        await ErrorDetail(inFlight, HttpStatusCode.Conflict, "CONFLICT");
        Assert.Equal(2, mirror.Calls);
        await Admin($"UPDATE plugin_qian_capabilities.invoke_receipts SET expires_at = now() - interval '1 second' WHERE idempotency_key = '{pending}';");
        using var leaseExpired = await InvokeProbe(host, mirror.Id, body, pending.ToString());
        Assert.Equal(HttpStatusCode.OK, leaseExpired.StatusCode); Assert.False(leaseExpired.Headers.Contains("Idempotent-Replayed"));
        Assert.Equal(3, mirror.Calls);
        var settled = Assert.Single(await AdminRows($"SELECT response, expires_at > now() + interval '23 hours' FROM plugin_qian_capabilities.invoke_receipts WHERE idempotency_key = '{pending}'"));
        Assert.True(JsonNode.DeepEquals(JsonNode.Parse((string)settled[0]), JsonNode.Parse(await leaseExpired.Content.ReadAsStringAsync(Ct)))); Assert.True((bool)settled[1]);

        // 执行失败不留回执：CRM 新建客户的业务幂等键相同而名称不同，CRM 返回 409；撤销登记后，同一个幂等键带正确的输入可以重新执行。
        var command = new CreateCustomerCommand(Guid.CreateVersion7(), "idempotent customer");
        using (var created = await InvokeKeyed(host, A, command, Guid.CreateVersion7().ToString()))
            Assert.True(created.StatusCode == HttpStatusCode.OK, host.FailureEvidence + await created.Content.ReadAsStringAsync(Ct));
        await AssertWrites(host, A, customers: 1, audit: 1);
        var failing = Guid.CreateVersion7();
        using var business = await InvokeKeyed(host, A, command with { Name = "renamed customer" }, failing.ToString());
        await ErrorDetail(business, HttpStatusCode.Conflict, "CONFLICT");
        Assert.Empty(await AdminRows($"SELECT 1 FROM plugin_qian_capabilities.invoke_receipts WHERE idempotency_key = '{failing}'"));
        await AssertWrites(host, A, customers: 1, audit: 1);
        using var recovered = await InvokeKeyed(host, A, command, failing.ToString());
        Assert.Equal(HttpStatusCode.OK, recovered.StatusCode); Assert.False(recovered.Headers.Contains("Idempotent-Replayed"));
        await AssertWrites(host, A, customers: 1, audit: 2);

        // 同键并发：登记经事务级 advisory lock 串行，只有一个请求执行；另一个回放其结果，或在其仍执行时得到 409。
        var concurrent = Guid.CreateVersion7(); var other = ProbeInput("concurrent probe");
        var responses = await Task.WhenAll(InvokeProbe(host, mirror.Id, other, concurrent.ToString()), InvokeProbe(host, mirror.Id, other, concurrent.ToString()));
        try
        {
            var executed = Assert.Single(responses, item => item.StatusCode == HttpStatusCode.OK && !item.Headers.Contains("Idempotent-Replayed"));
            var second = Assert.Single(responses, item => !ReferenceEquals(item, executed));
            if (second.StatusCode != HttpStatusCode.OK) await ErrorDetail(second, HttpStatusCode.Conflict, "CONFLICT");
            else
            {
                Assert.Equal("true", Assert.Single(second.Headers.GetValues("Idempotent-Replayed")));
                Assert.Equal(await executed.Content.ReadAsStringAsync(Ct), await second.Content.ReadAsStringAsync(Ct));
            }
        }
        finally { foreach (var item in responses) item.Dispose(); }
        Assert.Equal(4, mirror.Calls);
    }

    [Fact]
    public async Task 回执按数据分级与上限决定是否保存响应体_不保存时同键重放返回409且不再执行_窗口过期后重新执行_表上有响应体硬上限()
    {
        // 默认上限 64 KiB：pii 能力的小响应按分级不保存；internal 能力约 66 KB 的响应按大小不保存；public 能力的小响应保存。
        var pii = new ReceiptProbe("customer.export", "pii", 64);
        var bulk = new ReceiptProbe("customer.bulk", "internal", 66 * 1024);
        var directory = new ReceiptProbe("customer.directory", "public", 64);
        await using var host = await Start(capabilities: true, probes: [pii, bulk, directory]);
        foreach (var (probe, reason) in new[] { (pii, "data_classification"), (bulk, "response_too_large") })
        {
            var key = Guid.CreateVersion7();
            var body = JsonSerializer.Serialize(new CreateCustomerCommand(Guid.CreateVersion7(), "receipt probe"), JsonSerializerOptions.Web);
            using (var first = await InvokeProbe(host, probe.Id, body, key))
                Assert.True(first.StatusCode == HttpStatusCode.OK, host.FailureEvidence + await first.Content.ReadAsStringAsync(Ct));
            Assert.Equal(1, probe.Calls);
            // 回执只记「已执行」与原因，不存响应体；过期时间同样延到 24 小时回放窗口。
            var row = Assert.Single(await AdminRows($"SELECT response IS NULL, withheld, expires_at > now() + interval '23 hours' FROM plugin_qian_capabilities.invoke_receipts WHERE idempotency_key = '{key}'"));
            Assert.Equal((true, reason, true), ((bool)row[0], (string)row[1], (bool)row[2]));
            using (var replay = await InvokeProbe(host, probe.Id, body, key))
            {
                var error = await ErrorDetail(replay, HttpStatusCode.Conflict, "CAPABILITY_REPLAY_UNAVAILABLE");
                Assert.Contains(reason, error.GetProperty("message").GetString(), StringComparison.Ordinal);
                Assert.False(replay.Headers.Contains("Idempotent-Replayed"));
            }
            Assert.Equal(1, probe.Calls);
            // 同键异体仍先报 CONFLICT，不执行。
            var other = JsonSerializer.Serialize(new CreateCustomerCommand(Guid.CreateVersion7(), "other probe"), JsonSerializerOptions.Web);
            using (var conflict = await InvokeProbe(host, probe.Id, other, key)) await ErrorDetail(conflict, HttpStatusCode.Conflict, "CONFLICT");
            Assert.Equal(1, probe.Calls);
            // 回放窗口过期后同键重新执行。
            await Admin($"UPDATE plugin_qian_capabilities.invoke_receipts SET expires_at = now() - interval '1 second' WHERE idempotency_key = '{key}';");
            using (var again = await InvokeProbe(host, probe.Id, body, key))
            {
                Assert.Equal(HttpStatusCode.OK, again.StatusCode); Assert.False(again.Headers.Contains("Idempotent-Replayed"));
            }
            Assert.Equal(2, probe.Calls);
        }

        // public 分级：保存响应体，同键原样回放、不再执行。
        var publicKey = Guid.CreateVersion7(); var publicBody = ProbeInput("public probe");
        using (var stored = await InvokeProbe(host, directory.Id, publicBody, publicKey))
        using (var replayed = await InvokeProbe(host, directory.Id, publicBody, publicKey))
        {
            Assert.True(stored.StatusCode == HttpStatusCode.OK, host.FailureEvidence + await stored.Content.ReadAsStringAsync(Ct));
            Assert.Equal("true", Assert.Single(replayed.Headers.GetValues("Idempotent-Replayed")));
            Assert.Equal(await stored.Content.ReadAsStringAsync(Ct), await replayed.Content.ReadAsStringAsync(Ct));
        }
        Assert.Equal(1, directory.Calls);
        var kept = Assert.Single(await AdminRows($"SELECT response IS NOT NULL, withheld IS NULL FROM plugin_qian_capabilities.invoke_receipts WHERE idempotency_key = '{publicKey}'"));
        Assert.Equal((true, true), ((bool)kept[0], (bool)kept[1]));

        // 参考应用的 customer.create 响应含客户名称、标为 pii：名称不进回执表，同键重放 409 data_classification，CRM 不再执行（客户与审计不增加）。
        var name = "pii customer " + Guid.CreateVersion7().ToString("N");
        var command = new CreateCustomerCommand(Guid.CreateVersion7(), name); var createKey = Guid.CreateVersion7();
        using (var created = await InvokeKeyed(host, A, command, createKey.ToString()))
        {
            Assert.True(created.StatusCode == HttpStatusCode.OK, host.FailureEvidence + await created.Content.ReadAsStringAsync(Ct));
            Assert.Contains(name, await created.Content.ReadAsStringAsync(Ct), StringComparison.Ordinal);
        }
        await AssertWrites(host, A, customers: 1, audit: 1);
        var receipt = Assert.Single(await AdminRows($"SELECT response IS NULL, withheld FROM plugin_qian_capabilities.invoke_receipts WHERE idempotency_key = '{createKey}'"));
        Assert.Equal((true, "data_classification"), ((bool)receipt[0], (string)receipt[1]));
        Assert.Empty(await AdminRows($"SELECT 1 FROM plugin_qian_capabilities.invoke_receipts WHERE strpos(response, '{name}') > 0"));
        using (var replay = await InvokeKeyed(host, A, command, createKey.ToString()))
        {
            var error = await ErrorDetail(replay, HttpStatusCode.Conflict, "CAPABILITY_REPLAY_UNAVAILABLE");
            Assert.Contains("data_classification", error.GetProperty("message").GetString(), StringComparison.Ordinal);
        }
        await AssertWrites(host, A, customers: 1, audit: 1);

        // 表上的检查约束兜住写入前检查之外的写入：响应体超过 1 MiB、或同时带响应体与不保存原因、或原因不在约定集合内，都被拒绝。
        var source = $"FROM plugin_qian_capabilities.invoke_receipts WHERE capability_id = '{pii.Id}' LIMIT 1";
        foreach (var (response, withheld, constraint) in new[] { ("repeat('x', 1048577)", "NULL", "ck_invoke_receipts_response_size"),
            ("'{}'", "'data_classification'", "ck_invoke_receipts_withheld"), ("NULL", "'other'", "ck_invoke_receipts_withheld") })
        {
            var violation = await Assert.ThrowsAsync<PostgresException>(() => Admin(
                "INSERT INTO plugin_qian_capabilities.invoke_receipts (tenant_id, subject_key, capability_id, idempotency_key, request_sha256, response, withheld, created_at, expires_at) " +
                $"SELECT tenant_id, subject_key, capability_id, '{Guid.CreateVersion7()}', request_sha256, {response}, {withheld}, now(), now() + interval '1 hour' {source};"));
            Assert.Equal((PostgresErrorCodes.CheckViolation, constraint), (violation.SqlState, violation.ConstraintName));
        }
    }

    [Fact]
    public async Task 组件配置非默认值生效_目录ttlMs为2500_回执上限1024字节时恰好1024字节的响应保存并重放_多1字节即不保存()
    {
        // 探针响应是 CustomerItem 的 JSON，名称以外的部分长度固定：按名称长度凑出恰好 1024 字节与 1025 字节的响应。
        var overhead = JsonSerializer.SerializeToUtf8Bytes(new CustomerItem(Guid.Empty, A, ""), CrmCommandJson.Default.CustomerItem).Length;
        var exact = new ReceiptProbe("customer.digest", "internal", 1024 - overhead);
        var over = new ReceiptProbe("customer.overflow", "internal", 1025 - overhead);
        await using var host = await Start(capabilities: true, probes: [exact, over], capabilitiesConfig: new { catalogTtlMs = 2500, receiptMaxBytes = 1024 });
        using (var catalog = await CatalogRequest(host, A, "/api/capabilities"))
        {
            Assert.True(catalog.StatusCode == HttpStatusCode.OK, host.FailureEvidence + await catalog.Content.ReadAsStringAsync(Ct));
            Assert.Equal(2500, (await Json(catalog)).GetProperty("ttlMs").GetInt32());
        }
        // 恰好等于上限：保存，同键原样回放、不再执行。
        var key = Guid.CreateVersion7(); var body = ProbeInput("exact probe");
        using (var stored = await InvokeProbe(host, exact.Id, body, key))
        using (var replayed = await InvokeProbe(host, exact.Id, body, key))
        {
            Assert.True(stored.StatusCode == HttpStatusCode.OK, host.FailureEvidence + await stored.Content.ReadAsStringAsync(Ct));
            var text = await stored.Content.ReadAsStringAsync(Ct);
            Assert.Equal(1024, Encoding.UTF8.GetByteCount(text));
            Assert.Equal("true", Assert.Single(replayed.Headers.GetValues("Idempotent-Replayed")));
            Assert.Equal(text, await replayed.Content.ReadAsStringAsync(Ct));
        }
        Assert.Equal(1, exact.Calls);
        Assert.Equal(1024, (int)Assert.Single(await AdminRows($"SELECT octet_length(response) FROM plugin_qian_capabilities.invoke_receipts WHERE idempotency_key = '{key}'"))[0]);
        // 多 1 字节：只记「已执行」，同键 409 response_too_large。
        var overKey = Guid.CreateVersion7();
        using (var first = await InvokeProbe(host, over.Id, body, overKey)) Assert.Equal(HttpStatusCode.OK, first.StatusCode);
        using (var replay = await InvokeProbe(host, over.Id, body, overKey)) await ErrorDetail(replay, HttpStatusCode.Conflict, "CAPABILITY_REPLAY_UNAVAILABLE");
        Assert.Equal(1, over.Calls);
        Assert.Equal("response_too_large", (string)Assert.Single(await AdminRows($"SELECT withheld FROM plugin_qian_capabilities.invoke_receipts WHERE idempotency_key = '{overKey}'"))[0]);
    }

    [Fact]
    public async Task 回执上限为0时写类能力的响应一律不保存_public与internal能力同键重放都返回409且不再执行()
    {
        var directory = new ReceiptProbe("customer.directory", "public", 0);
        var mirror = new ReceiptProbe("customer.mirror", "internal", 0);
        await using var host = await Start(capabilities: true, probes: [directory, mirror], capabilitiesConfig: new { receiptMaxBytes = 0 });
        foreach (var probe in new[] { directory, mirror })
        {
            var key = Guid.CreateVersion7(); var body = ProbeInput("zero limit");
            using (var first = await InvokeProbe(host, probe.Id, body, key))
                Assert.True(first.StatusCode == HttpStatusCode.OK, host.FailureEvidence + await first.Content.ReadAsStringAsync(Ct));
            var row = Assert.Single(await AdminRows($"SELECT response IS NULL, withheld FROM plugin_qian_capabilities.invoke_receipts WHERE idempotency_key = '{key}'"));
            Assert.Equal((true, "response_too_large"), ((bool)row[0], (string)row[1]));
            using (var replay = await InvokeProbe(host, probe.Id, body, key))
            {
                var error = await ErrorDetail(replay, HttpStatusCode.Conflict, "CAPABILITY_REPLAY_UNAVAILABLE");
                Assert.Contains("response_too_large", error.GetProperty("message").GetString(), StringComparison.Ordinal);
            }
            Assert.Equal(1, probe.Calls);
        }
    }

    [Fact]
    public async Task 绕过端点直接调用CapabilityService_带Actor委托的主体与不存在一样404且处理器不执行_去掉Actor的同一主体才执行()
    {
        var probe = new ReceiptProbe("customer.export", "internal", 16);
        await using var host = await Start(capabilities: true, probes: [probe]);
        var caller = host.App.Services.GetRequiredService<CrmCommandRuntime>().Loader.GetContext("capabilities", ScopeKey.Global).Scoped(new ScopeKey("tenant", A));
        var service = caller.Capabilities();
        var input = JsonSerializer.SerializeToElement(new CreateCustomerCommand(Guid.CreateVersion7(), "direct actor"), CrmCommandJson.Default.CreateCustomerCommand);
        using (host.App.Services.GetRequiredService<ITenantContext>().BeginScope(A))
        {
            // HTTP 认证层已拒绝带 act 的令牌；服务端口自身同样拒绝，不依赖端点。委托方是 Agent 还是同一服务主体都一样。
            foreach (var actor in new[] { new DelegationActor { Subject = AgentId, Kind = PrincipalKind.Agent }, new DelegationActor { Subject = Writer, Kind = PrincipalKind.Service } })
            {
                var delegated = new DirectUser(DirectPrincipal(actor));
                foreach (var id in new[] { CreateId, probe.Id })
                {
                    var denied = await Assert.ThrowsAsync<QianErrorException>(() => service.InvokeAsync(caller, delegated, id, input, Guid.CreateVersion7(), Ct).AsTask());
                    Assert.Equal("NOT_FOUND", denied.Error.Code.Code);
                }
                Assert.Equal("NOT_FOUND", (await Assert.ThrowsAsync<QianErrorException>(() => service.ListAsync(caller, delegated, cancellationToken: Ct).AsTask())).Error.Code.Code);
            }
            Assert.Equal(0, probe.Calls);
            // 对照：同一主体去掉 Actor 后经同一端口执行，说明拒绝来自委托判定而不是装配。
            var direct = await service.InvokeAsync(caller, new DirectUser(DirectPrincipal(null)), probe.Id, input, cancellationToken: Ct);
            Assert.False(direct.Replayed); Assert.Equal(1, probe.Calls);
        }
        // CRM 的真实处理器没有执行：没有客户、审计与回执。
        await AssertWrites(host, A, customers: 0, audit: 0);
        Assert.Empty(await AdminRows("SELECT 1 FROM plugin_qian_capabilities.invoke_receipts"));
    }

    [Fact]
    public async Task 宿主经UsePathBase挂在子路径下_别名的successor链接带PathBase与原查询串_后继路径可用()
    {
        await using var host = await Start(capabilities: true, pathBase: "/qian");
        foreach (var (alias, successor) in new[] { ("/qian/api/kernel/capabilities?kind=command&q=create", "/qian/api/capabilities?kind=command&q=create"),
            ("/qian/api/kernel/capabilities/openapi.json", "/qian/api/capabilities/openapi.json") })
        {
            using var deprecated = await CatalogRequest(host, A, alias);
            Assert.True(deprecated.StatusCode == HttpStatusCode.OK, host.FailureEvidence + await deprecated.Content.ReadAsStringAsync(Ct));
            Assert.Equal(Deprecation, Assert.Single(deprecated.Headers.GetValues("Deprecation")));
            Assert.Equal("<" + successor + ">; rel=\"successor-version\"", string.Join(", ", deprecated.Headers.GetValues("Link")));
            using var current = await CatalogRequest(host, A, successor);
            Assert.Equal(HttpStatusCode.OK, current.StatusCode);
            Assert.True(JsonElement.DeepEquals(await Json(current), await Json(deprecated)), alias);
        }
    }

    [Fact]
    public async Task 只读能力忽略幂等键_每次执行不存回执不带回放头_聚合OpenAPI只为写类能力声明幂等键与不可重放码()
    {
        await using var host = await Start(capabilities: true);
        var key = Guid.CreateVersion7().ToString();
        Assert.Equal(0, (await ListKeyed(host, key)).GetProperty("total").GetInt32());
        using var created = await InvokeRaw(host, A, CreateId, JsonSerializer.Serialize(new CreateCustomerCommand(Guid.CreateVersion7(), "listed customer"), JsonSerializerOptions.Web));
        Assert.Equal(HttpStatusCode.OK, created.StatusCode);
        // 同一个键再次查询看到新客户：查询重新执行，不是回放。
        Assert.Equal(1, (await ListKeyed(host, key)).GetProperty("total").GetInt32());
        Assert.Empty(await AdminRows($"SELECT 1 FROM plugin_qian_capabilities.invoke_receipts WHERE idempotency_key = '{key}'"));

        using var document = await CatalogRequest(host, A, "/api/capabilities/openapi.json");
        Assert.Equal(HttpStatusCode.OK, document.StatusCode);
        var text = await document.Content.ReadAsStringAsync(Ct);
        // 写类能力的错误响应与幂等键说明都列出不可重放码及其两种 reason。
        foreach (var expected in new[] { "CAPABILITY_REPLAY_UNAVAILABLE", "response_too_large", "data_classification" })
            Assert.Contains(expected, text, StringComparison.Ordinal);
        using var parsed = JsonDocument.Parse(text);
        var paths = parsed.RootElement.GetProperty("paths");
        var write = paths.GetProperty("/api/capabilities/" + CreateId + "/invoke").GetProperty("post");
        var parameter = Assert.Single(write.GetProperty("parameters").EnumerateArray());
        Assert.Equal("Idempotency-Key", parameter.GetProperty("name").GetString());
        Assert.True(write.GetProperty("responses").GetProperty("200").GetProperty("headers").TryGetProperty("Idempotent-Replayed", out _));
        foreach (var description in new[] { parameter.GetProperty("description").GetString()!, write.GetProperty("responses").GetProperty("default").GetProperty("description").GetString()! })
        {
            Assert.Contains("409 CONFLICT", description, StringComparison.Ordinal);
            Assert.Contains("409 CAPABILITY_REPLAY_UNAVAILABLE", description, StringComparison.Ordinal);
            Assert.Contains("response_too_large", description, StringComparison.Ordinal);
            Assert.Contains("data_classification", description, StringComparison.Ordinal);
        }
        foreach (var read in new[] { ListId, GetId })
        {
            var operation = paths.GetProperty("/api/capabilities/" + read + "/invoke").GetProperty("post");
            Assert.False(operation.TryGetProperty("parameters", out _), read);
            Assert.False(operation.GetProperty("responses").GetProperty("200").TryGetProperty("headers", out _), read);
            Assert.DoesNotContain("409", operation.GetProperty("responses").GetProperty("default").GetProperty("description").GetString()!, StringComparison.Ordinal);
        }

        async Task<JsonElement> ListKeyed(RunningApp running, string idempotencyKey)
        {
            using var request = new HttpRequestMessage(HttpMethod.Post, "/api/capabilities/" + ListId + "/invoke")
            { Content = new StringContent("{\"skip\":0,\"take\":10}", Encoding.UTF8, "application/json") };
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", running.Token(A, "crm.read crm.write"));
            Assert.True(request.Headers.TryAddWithoutValidation("Idempotency-Key", idempotencyKey));
            using var response = await running.Send(request);
            Assert.True(response.StatusCode == HttpStatusCode.OK, running.FailureEvidence + await response.Content.ReadAsStringAsync(Ct));
            Assert.False(response.Headers.Contains("Idempotent-Replayed"));
            return await Json(response);
        }
    }

    [Fact]
    public async Task Agent主体经通用执行入口_S1授权下无能力_授权预览下仍只见agent_expose能力且执行被命令策略拒绝_委托令牌认证失败()
    {
        var probe = new HumanOnlyProbe();
        await using var host = await Start(capabilities: true, humanOnlyProbe: probe);
        var preview = host.App.Services.GetRequiredService<AgentAuthorizationPreview>();
        var tenant = host.App.Services.GetRequiredService<ITenantContext>(); var data = host.App.Services.GetRequiredService<IDataService>();
        using (tenant.BeginScope(A))
        {
            var roles = new PostgresTenantRoleStore(data, tenant);
            await roles.AssignAsync("tenant.owner", "agent", AgentId, "test-bootstrap", Ct);
            // 预览把 Agent 主体按同 ID 的服务主体求值，这条绑定只在预览打开时生效。
            await roles.AssignAsync("tenant.owner", "service", AgentId, "test-bootstrap", Ct);
        }

        // 服务主体：人工专用能力在目录里、可执行；派生给模型的提示词不含它。
        using var service = await CatalogRequest(host, A, "/api/capabilities");
        Assert.True(service.StatusCode == HttpStatusCode.OK, host.FailureEvidence + await service.Content.ReadAsStringAsync(Ct));
        var serviceItems = (await Json(service)).GetProperty("items").EnumerateArray().ToArray();
        Assert.Equal([CreateId, GetId, ListId, HumanOnlyProbe.Id], serviceItems.Select(item => item.GetProperty("id").GetString()!).Order(StringComparer.Ordinal));
        Assert.False(Assert.Single(serviceItems, item => item.GetProperty("id").GetString() == HumanOnlyProbe.Id).GetProperty("agent").GetProperty("expose").GetBoolean());
        using var prompt = await CatalogRequest(host, A, "/api/acceptance/prompt");
        var promptText = await prompt.Content.ReadAsStringAsync(Ct);
        Assert.Contains("工具：" + CreateId + "\n", promptText, StringComparison.Ordinal);
        Assert.DoesNotContain(HumanOnlyProbe.Id, promptText, StringComparison.Ordinal);
        using var serviceInvoke = await InvokeRaw(host, A, HumanOnlyProbe.Id, "{\"skip\":0,\"take\":10}");
        Assert.True(serviceInvoke.StatusCode == HttpStatusCode.OK, host.FailureEvidence + await serviceInvoke.Content.ReadAsStringAsync(Ct));
        Assert.Equal(1, probe.Calls);

        // S1 实际授权：RBAC 不接受 Agent 主体，即使有角色绑定也看不到、调不到任何能力，响应与不存在一致。
        using var agentCatalog = await AgentRequest(host, HttpMethod.Get, "/api/capabilities");
        var emptyCatalog = await Json(agentCatalog); AssertValid(ContractSchemas.Value.Catalog, emptyCatalog);
        Assert.Empty(emptyCatalog.GetProperty("items").EnumerateArray());
        foreach (var id in new[] { CreateId, HumanOnlyProbe.Id }) await AssertAgentNotFound(host, id);
        await AssertWrites(host, A, customers: 0, audit: 0);

        // 授权预览（模拟后续阶段 RBAC 接受 Agent）：Agent 仍只见 agent.expose = true 的能力。
        preview.Enabled = true;
        try
        {
            using var exposed = await AgentRequest(host, HttpMethod.Get, "/api/capabilities");
            Assert.Equal([CreateId, GetId, ListId], (await Json(exposed)).GetProperty("items").EnumerateArray().Select(item => item.GetProperty("id").GetString()!).Order(StringComparer.Ordinal));
            using var document = await AgentRequest(host, HttpMethod.Get, "/api/capabilities/openapi.json");
            var paths = (await Json(document)).GetProperty("paths");
            Assert.True(paths.TryGetProperty("/api/capabilities/" + CreateId + "/invoke", out _));
            Assert.False(paths.TryGetProperty("/api/capabilities/" + HumanOnlyProbe.Id + "/invoke", out _));
            await AssertAgentNotFound(host, HumanOnlyProbe.Id);
            Assert.Equal(1, probe.Calls);
            // 可见的能力仍先过输入校验；执行时 qian.authz 的命令策略在 S1 拒绝 Agent 主体。
            using var invalid = await AgentRequest(host, HttpMethod.Post, "/api/capabilities/" + CreateId + "/invoke", "{\"name\":\"agent\"}");
            Assert.Equal([("REQUIRED", "#/idempotencyKey")], Fields(await ErrorDetail(invalid, HttpStatusCode.BadRequest, "INPUT_INVALID")));
            using var denied = await AgentRequest(host, HttpMethod.Post, "/api/capabilities/" + CreateId + "/invoke",
                JsonSerializer.Serialize(new CreateCustomerCommand(Guid.CreateVersion7(), "agent customer"), CrmCommandJson.Default.CreateCustomerCommand));
            await ErrorDetail(denied, HttpStatusCode.Forbidden, "AUTHZ_DENIED");
        }
        finally { preview.Enabled = false; }

        // Actor 委托不开放：带 act 的令牌在认证阶段失败，目录与执行入口都返回 401。
        var actor = new { sub = Writer, principal_kind = "service" };
        foreach (var kind in new[] { "agent", "service" })
        {
            using var catalog = await AgentRequest(host, HttpMethod.Get, "/api/capabilities", kind: kind, actor: actor);
            await ErrorDetail(catalog, HttpStatusCode.Unauthorized, "UNAUTHENTICATED");
            using var invoke = await AgentRequest(host, HttpMethod.Post, "/api/capabilities/" + ListId + "/invoke", "{}", kind, actor);
            await ErrorDetail(invoke, HttpStatusCode.Unauthorized, "UNAUTHENTICATED");
        }
        // 没有业务写入；链上只有命令 PEP 对 Agent 主体的拒绝决策（S1-AZ-004：拒绝必记），没有放行或命令审计。
        await AssertWrites(host, A, customers: 0, audit: 0, decisions: [("agent", AgentId, "denied")]);
    }

    [Fact]
    public async Task 能力上下文随包迁移经迁移器在真实PostgreSQL建表_偏好与回执表强制租户RLS_回执只在本租户可见_上限约束NOT_VALID_运维清理SQL按租户边界删除过期回执_重复迁移无操作()
    {
        await using var host = await Start(capabilities: true);
        var schema = CapabilitiesDbContext.Schema;
        string[] migrations;
        using (var scope = host.App.Services.CreateScope())
            migrations = scope.ServiceProvider.GetRequiredService<CapabilitiesDbContext>().Database.GetMigrations().ToArray();
        Assert.NotEmpty(migrations);
        Assert.Equal(migrations, (await AdminRows($"SELECT \"MigrationId\" FROM {schema}.\"__EFMigrationsHistory\" ORDER BY \"MigrationId\"")).Select(row => (string)row[0]));
        foreach (var table in new[] { "navigation_preferences", "invoke_receipts" })
        {
            var flags = Assert.Single(await AdminRows($"SELECT c.relrowsecurity, c.relforcerowsecurity, pg_get_userbyid(c.relowner) FROM pg_class c JOIN pg_namespace n ON n.oid = c.relnamespace WHERE n.nspname = '{schema}' AND c.relname = '{table}'"));
            Assert.True((bool)flags[0], table); Assert.True((bool)flags[1], table);
            // 表由受限的迁移属主建出，不是超级用户。
            Assert.StartsWith("qian_cap_owner_", (string)flags[2], StringComparison.Ordinal);
            var policies = await AdminRows($"SELECT policyname, permissive, cmd, qual FROM pg_policies WHERE schemaname = '{schema}' AND tablename = '{table}' ORDER BY policyname");
            Assert.Equal(["qian_tenant_access", "qian_tenant_guard"], policies.Select(row => (string)row[0]));
            Assert.Equal(["PERMISSIVE", "RESTRICTIVE"], policies.Select(row => (string)row[1]));
            Assert.All(policies, row => Assert.Equal("ALL", (string)row[2]));
            Assert.Contains("app.current_tenant_id", (string)policies[1][3], StringComparison.Ordinal);
        }
        // packages/Qian.Data/README.md 给运维的核查 SQL 原样执行：能力上下文的两张租户表在列，且库里每张租户表都强制 RLS、恰有两条策略。
        var readme = File.ReadAllText(Path.Combine(RepositoryRoot(), "packages/Qian.Data/README.md"));
        var verification = readme.Split("```").Single(block => block.StartsWith("sql", StringComparison.Ordinal) && block.Contains("pg_policies", StringComparison.Ordinal))[3..];
        var tenantTables = await AdminRows(verification);
        Assert.Contains(tenantTables, row => (string)row[0] == schema && (string)row[1] == "invoke_receipts");
        Assert.Contains(tenantTables, row => (string)row[0] == schema && (string)row[1] == "navigation_preferences");
        Assert.All(tenantTables, row => Assert.Equal((true, true, "qian_tenant_access(PERMISSIVE), qian_tenant_guard(RESTRICTIVE)"), ((bool)row[2], (bool)row[3], (string)row[4])));
        // 回执表的响应体硬上限与不保存原因的约束随第二个迁移交付；前者以 NOT VALID 添加（存量行不校验），后者已校验。
        Assert.Equal([("ck_invoke_receipts_response_size", false), ("ck_invoke_receipts_withheld", true)],
            (await AdminRows($"SELECT conname, convalidated FROM pg_constraint WHERE conrelid = '{schema}.invoke_receipts'::regclass AND contype = 'c' ORDER BY conname"))
            .Select(row => ((string)row[0], (bool)row[1])));

        var command = new CreateCustomerCommand(Guid.CreateVersion7(), "receipt owner");
        using var invoked = await InvokeKeyed(host, A, command, Guid.CreateVersion7().ToString());
        Assert.True(invoked.StatusCode == HttpStatusCode.OK, host.FailureEvidence + await invoked.Content.ReadAsStringAsync(Ct));
        Assert.Equal(1L, (long)Assert.Single(await AdminRows($"SELECT count(*) FROM {schema}.invoke_receipts WHERE tenant_id = '{A}'"))[0]);
        // 受限业务登录：未设租户与租户 B 都读不到 A 的回执，只有 A 的会话读得到。
        Assert.Equal(0L, await BusinessCount(host, null)); Assert.Equal(0L, await BusinessCount(host, B)); Assert.Equal(1L, await BusinessCount(host, A));

        // docs/ops/configuration-runbook.md 给运维的过期回执清理 SQL 原样执行。A、B 各添一条已过期的回执，A 原有的回执未过期。
        var runbook = File.ReadAllText(Path.Combine(RepositoryRoot(), "docs/ops/configuration-runbook.md"));
        var cleanup = runbook.Split("```").Where(block => block.StartsWith("sql", StringComparison.Ordinal) && block.Contains("invoke_receipts", StringComparison.Ordinal))
            .Select(block => block[3..]).ToArray();
        var perTenant = Assert.Single(cleanup, block => block.Contains("set_config", StringComparison.Ordinal));
        var allTenants = Assert.Single(cleanup, block => block.Contains("ctid", StringComparison.Ordinal));
        foreach (var tenant in new[] { A, B })
            await Admin($"INSERT INTO {schema}.invoke_receipts (tenant_id, subject_key, capability_id, idempotency_key, request_sha256, response, withheld, created_at, expires_at) " +
                $"SELECT '{tenant}', subject_key, capability_id, '{Guid.CreateVersion7()}', request_sha256, NULL, withheld, now() - interval '2 days', now() - interval '1 day' " +
                $"FROM {schema}.invoke_receipts WHERE tenant_id = '{A}' AND expires_at > now();");
        var census = $"SELECT tenant_id, count(*) FILTER (WHERE expires_at <= now()), count(*) FILTER (WHERE expires_at > now()) FROM {schema}.invoke_receipts GROUP BY tenant_id ORDER BY tenant_id";
        async Task<(string, long, long)[]> Census() => (await AdminRows(census)).Select(row => ((string)row[0], (long)row[1], (long)row[2])).ToArray();
        Assert.Equal([(A, 1L, 1L), (B, 1L, 0L)], await Census());
        // 逐租户的写法：受限业务登录（NOBYPASSRLS）只删得到指定租户的过期回执。
        await using (var business = new NpgsqlConnection(new NpgsqlConnectionStringBuilder(host.BusinessConnection) { Pooling = false }.ConnectionString))
        {
            await business.OpenAsync(Ct);
            await using var purge = new NpgsqlCommand(perTenant.Replace("<租户 ID>", A, StringComparison.Ordinal), business);
            await purge.ExecuteNonQueryAsync(Ct);
        }
        Assert.Equal([(A, 0L, 1L), (B, 1L, 0L)], await Census());
        // 有 BYPASSRLS 的维护角色：分批删除全部租户的过期回执，重复执行到删除 0 行；未过期的回执保留。
        Assert.Equal(1, await Admin(allTenants));
        Assert.Equal(0, await Admin(allTenants));
        Assert.Equal([(A, 0L, 1L)], await Census());

        var again = await host.App.Services.GetRequiredService<IDataMigrator>().MigrateAsync(Ct);
        Assert.Empty(Assert.Single(again, report => report.ContextType == typeof(CapabilitiesDbContext)).Applied);
    }

    private static void AssertEntry(JsonElement entry, string kind, string sideEffect, bool idempotent, string scope)
    {
        Assert.Equal(kind, entry.GetProperty("kind").GetString());
        Assert.Equal(sideEffect, entry.GetProperty("sideEffect").GetString());
        Assert.Equal("allow", entry.GetProperty("policyHint").GetString());
        Assert.Equal(idempotent, entry.GetProperty("idempotent").GetBoolean());
        Assert.Equal([scope], entry.GetProperty("requiredScopes").EnumerateArray().Select(item => item.GetString()!));
        // 三个 CRM 能力的响应都含客户名称（个人信息）。
        Assert.Equal("pii", entry.GetProperty("dataClassification").GetString());
        Assert.Equal("low", entry.GetProperty("costTier").GetString());
        Assert.True(entry.GetProperty("agent").GetProperty("expose").GetBoolean());
        Assert.False(entry.GetProperty("agent").GetProperty("requiresConfirmation").GetBoolean());
    }

    private static async Task<long> BusinessCount(RunningApp host, string? tenant)
    {
        // 独立连接池，会话变量不会回到应用的连接池。
        var probe = new NpgsqlConnectionStringBuilder(host.BusinessConnection) { ApplicationName = "qian-rls-probe", Pooling = false }.ConnectionString;
        await using var connection = new NpgsqlConnection(probe); await connection.OpenAsync(Ct);
        if (tenant is not null)
        {
            await using var set = new NpgsqlCommand("SELECT set_config('app.current_tenant_id', @tenant, false)", connection);
            set.Parameters.AddWithValue("tenant", tenant); await set.ExecuteScalarAsync(Ct);
        }
        await using var count = new NpgsqlCommand($"SELECT count(*) FROM {CapabilitiesDbContext.Schema}.invoke_receipts", connection);
        return (long)(await count.ExecuteScalarAsync(Ct))!;
    }

    private async Task<List<object[]>> AdminRows(string sql)
    {
        await using var connection = new NpgsqlConnection(postgres.ConnectionString); await connection.OpenAsync(Ct);
        await using var command = new NpgsqlCommand(sql, connection); await using var reader = await command.ExecuteReaderAsync(Ct);
        var rows = new List<object[]>();
        while (await reader.ReadAsync(Ct)) { var row = new object[reader.FieldCount]; reader.GetValues(row); rows.Add(row); }
        return rows;
    }

    // audit 为业务 / 命令审计条目数；decisions 为链上允许出现的决策日志（actorType, actorId, result），每条决策日志都必须匹配其中一项。
    private static async Task AssertWrites(RunningApp host, string tenantId, int customers, int audit, (string Kind, string Id, string Result)[]? decisions = null)
    {
        using (host.App.Services.GetRequiredService<ITenantContext>().BeginScope(tenantId))
        {
            await using var session = await host.App.Services.GetRequiredService<IDataService>().OpenAsync<CrmDbContext>(Ct);
            Assert.Equal(customers, await session.Context.Customers.CountAsync(Ct));
            Assert.Equal(customers, await session.Context.OutboxMessages.CountAsync(Ct));
            var trail = host.App.Services.GetRequiredService<CrmCommandRuntime>().Source().Audit();
            var records = (await trail.ReadAsync(0, 100, cancellationToken: Ct)).Select(entry => entry.Record).ToArray();
            var logged = records.Where(record => record.CapabilityId == Qian.Authz.Abstractions.AuthzEnforcement.DecisionCapabilityId).ToArray();
            Assert.Equal(audit, records.Length - logged.Length);
            Assert.All(logged, record => Assert.Contains((record.ActorType, record.ActorId, record.Result), decisions ?? []));
            if (decisions is null) Assert.Empty(logged);
            Assert.Equal(new AuditChainVerification(true, records.Length, null, null), await trail.VerifyAsync(Ct));
        }
    }

    private static async Task AssertAgentNotFound(RunningApp host, string id)
    {
        using var hidden = await AgentRequest(host, HttpMethod.Post, "/api/capabilities/" + id + "/invoke", "{\"skip\":0,\"take\":10}");
        using var unknown = await AgentRequest(host, HttpMethod.Post, "/api/capabilities/qian.sample.crm/customer.unknown/invoke", "{\"skip\":0,\"take\":10}");
        await ErrorDetail(hidden, HttpStatusCode.NotFound, "NOT_FOUND");
        Assert.True(JsonNode.DeepEquals(Normalize(await hidden.Content.ReadAsStringAsync(Ct)), Normalize(await unknown.Content.ReadAsStringAsync(Ct))), id);
    }

    private static Task<HttpResponseMessage> AgentRequest(RunningApp host, HttpMethod method, string path, string? body = null, string kind = "agent", object? actor = null)
    {
        var request = new HttpRequestMessage(method, path);
        if (body is not null) request.Content = new StringContent(body, Encoding.UTF8, "application/json");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", host.Token(A, "crm.read crm.write", AgentId, kind, actor));
        return host.Send(request);
    }

    private static Task<HttpResponseMessage> InvokeRaw(RunningApp host, string tenant, string id, string body)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, "/api/capabilities/" + id + "/invoke") { Content = new StringContent(body, Encoding.UTF8, "application/json") };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", host.Token(tenant, "crm.read crm.write"));
        return host.Send(request);
    }

    private static Principal DirectPrincipal(DelegationActor? actor) => new()
    {
        Issuer = Issuer, Audience = [Audience], Subject = Writer, Kind = PrincipalKind.Service, TenantId = A, Roles = [], Scope = "crm.read crm.write",
        SessionId = Guid.CreateVersion7().ToString(), TokenId = Guid.CreateVersion7().ToString(), IssuedAt = 1, NotBefore = 1, ExpiresAt = long.MaxValue, Actor = actor
    };

    // 不经 HTTP 的已验证主体快照：直接交给 CapabilityService，观察端口自身的判定。
    private sealed class DirectUser(Principal principal) : Qian.Identity.Abstractions.ICurrentUser
    {
        public Principal? Principal { get; } = principal;
        public string? UserId => Principal?.Subject;
        public IReadOnlyList<string> Roles => Principal?.Roles ?? [];
        public bool IsAuthenticated => true;
    }

    private static Task<HttpResponseMessage> InvokeProbe(RunningApp host, string id, string body, Guid key) => InvokeProbe(host, id, body, key.ToString());

    private static Task<HttpResponseMessage> InvokeProbe(RunningApp host, string id, string body, string key)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, "/api/capabilities/" + id + "/invoke") { Content = new StringContent(body, Encoding.UTF8, "application/json") };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", host.Token(A, "crm.read crm.write"));
        Assert.True(request.Headers.TryAddWithoutValidation("Idempotency-Key", key));
        return host.Send(request);
    }

    // 回执探针的输入与 customer.create 同形；每次生成新的业务幂等键。
    private static string ProbeInput(string name) => JsonSerializer.Serialize(new CreateCustomerCommand(Guid.CreateVersion7(), name), JsonSerializerOptions.Web);

    private static Task<HttpResponseMessage> InvokeKeyed(RunningApp host, string tenant, CreateCustomerCommand command, string key)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, "/api/capabilities/" + CreateId + "/invoke") { Content = JsonContent.Create(command) };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", host.Token(tenant, "crm.read crm.write"));
        Assert.True(request.Headers.TryAddWithoutValidation("Idempotency-Key", key));
        return host.Send(request);
    }

    private static async Task<JsonElement> Json(HttpResponseMessage response)
    {
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync(Ct)); return document.RootElement.Clone();
    }

    // 错误体：problem+json、符合冻结的 error-detail Schema 与 contracts/v0 第 3.5 节的语义规则。
    private static async Task<JsonElement> ErrorDetail(HttpResponseMessage response, HttpStatusCode status, string code)
    {
        var text = await response.Content.ReadAsStringAsync(Ct);
        Assert.True(response.StatusCode == status, text);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        using var document = JsonDocument.Parse(text); var body = document.RootElement.Clone();
        AssertValid(ContractSchemas.Value.Error, body);
        Assert.Equal(code, body.GetProperty("code").GetString());
        Assert.Equal((int)status, body.GetProperty("status").GetInt32());
        Assert.EndsWith("/" + code, body.GetProperty("type").GetString(), StringComparison.Ordinal);
        if (body.TryGetProperty("errors", out var errors)) Assert.Equal(errors[0].GetProperty("pointer").GetString(), body.GetProperty("pointer").GetString());
        return body;
    }

    private static (string Code, string Pointer)[] Fields(JsonElement error) => error.GetProperty("errors").EnumerateArray()
        .Select(item => (item.GetProperty("code").GetString()!, item.GetProperty("pointer").GetString()!)).ToArray();

    private static void AssertValid(JsonSchema schema, JsonElement instance)
    {
        var result = schema.Evaluate(instance, new EvaluationOptions { OutputFormat = OutputFormat.List });
        Assert.True(result.IsValid, JsonSerializer.Serialize(result));
    }

    private static (JsonSchema, JsonSchema) LoadContractSchemas()
    {
        var options = new BuildOptions { SchemaRegistry = new SchemaRegistry() };
        string Read(string name) => File.ReadAllText(Path.Combine(RepositoryRoot(), "contracts/v0", name + ".schema.json"));
        JsonSchema.FromText(Read("capability-entry"), options);
        return (JsonSchema.FromText(Read("capability-catalog"), options), JsonSchema.FromText(Read("error-detail"), options));
    }

    // 测试宿主把 Agent 主体的 RBAC 求值换成同 ID 服务主体，模拟后续阶段开放 Agent 授权；默认关闭，关闭时与生产判定相同。
    private static void PreviewAgentAuthorization(WebApplicationBuilder builder) =>
        builder.Host.ConfigureContainer<IServiceCollection>((_, services) =>
        {
            var original = services.Last(item => item.ServiceType == typeof(IPermissionChecker) && !item.IsKeyedService);
            var factory = original.ImplementationFactory ?? throw new InvalidOperationException("授权端口应由工厂登记。");
            services.AddSingleton(sp => new AgentAuthorizationPreview((IPermissionChecker)factory(sp)));
            services.AddSingleton<IPermissionChecker>(sp => sp.GetRequiredService<AgentAuthorizationPreview>());
        });

    internal sealed class AgentAuthorizationPreview(IPermissionChecker inner) : IPermissionChecker
    {
        private int _enabled;
        internal bool Enabled { get => Volatile.Read(ref _enabled) == 1; set => Volatile.Write(ref _enabled, value ? 1 : 0); }
        public ValueTask<PermissionDecision> CheckAsync(PermissionRequest request, CancellationToken cancellationToken = default) => inner.CheckAsync(Map(request), cancellationToken);
        public ValueTask<IReadOnlyList<PermissionDecision>> BatchCheckAsync(IReadOnlyList<PermissionRequest> requests, CancellationToken cancellationToken = default) =>
            inner.BatchCheckAsync(requests.Select(Map).ToArray(), cancellationToken);
        public ValueTask<IReadOnlyList<string>> ListAllowedActionsAsync(AuthorizationSubject subject, string resourceType, string? resourceId = null, CancellationToken cancellationToken = default) =>
            inner.ListAllowedActionsAsync(Map(subject), resourceType, resourceId, cancellationToken);
        public ValueTask<IReadOnlyList<string>> ListPermissionsAsync(AuthorizationSubject subject, CancellationToken cancellationToken = default) => inner.ListPermissionsAsync(Map(subject), cancellationToken);
        public ValueTask<PermissionDecision> ExplainAsync(PermissionRequest request, CancellationToken cancellationToken = default) => inner.ExplainAsync(Map(request), cancellationToken);
        private PermissionRequest Map(PermissionRequest request) => request with { Subject = Map(request.Subject) };
        private AuthorizationSubject Map(AuthorizationSubject subject) => Enabled && subject.Kind == "agent" ? subject with { Kind = "service" } : subject;
    }

    // 只在 humanOnlyProbe 宿主里随 crm 条目登记的人工专用能力（agent.expose = false）；CRM 的三个能力都对 Agent 开放，需要它才能观察过滤。
    internal sealed class HumanOnlyProbe : IExecutableCapability
    {
        internal const string Id = "qian.sample.crm/customer.review";
        private int _calls;
        internal int Calls => Volatile.Read(ref _calls);
        public CapabilityContract Contract { get; } = new("qian.sample.crm",
            """{"name":"customer.review","description":"人工复核当前租户的客户列表；只开放给人和服务主体，不派生为模型可见工具。","sideEffect":"read","policyHint":"allow","idempotent":false,"async":"none","requiredScopes":["crm.read"],"requiredSecrets":[],"dataClassification":"internal","costTier":"low","contractVersion":"crm-v1","audit":false,"agent":{"expose":false,"requiresConfirmation":false},"binding":{"kind":"command","id":"qian.sample.crm/customer.review"},"requiredPermission":"qian.sample.crm:customer:read"}""",
            CrmCommandJson.Default.ListCustomersQuery, CrmCommandJson.Default.CustomerPage);
        public ValueTask<JsonElement> InvokeAsync(IPluginContext caller, JsonElement input, CancellationToken cancellationToken = default)
        {
            Interlocked.Increment(ref _calls);
            return ValueTask.FromResult(JsonSerializer.SerializeToElement(new CustomerPage(0, []), CrmCommandJson.Default.CustomerPage));
        }
    }

    // 回执策略的探针：写类能力（输入同 customer.create，含 idempotencyKey），数据分级与响应体大小可配，记录实际执行次数。
    internal sealed class ReceiptProbe(string name, string classification, int nameLength) : IExecutableCapability
    {
        private int _calls;
        internal string Id => Contract.Id;
        internal int Calls => Volatile.Read(ref _calls);
        public CapabilityContract Contract { get; } = new("qian.sample.crm",
            "{\"name\":\"" + name + "\",\"description\":\"导出当前租户的客户资料；用于验证幂等回执按数据分级与大小决定是否保存响应体。\",\"sideEffect\":\"write\"," +
            "\"policyHint\":\"allow\",\"idempotent\":true,\"async\":\"none\",\"requiredScopes\":[\"crm.write\"],\"requiredSecrets\":[],\"dataClassification\":\"" + classification + "\"," +
            "\"costTier\":\"low\",\"contractVersion\":\"crm-v1\",\"audit\":false,\"agent\":{\"expose\":false,\"requiresConfirmation\":false}," +
            "\"binding\":{\"kind\":\"command\",\"id\":\"qian.sample.crm/" + name + "\"},\"requiredPermission\":\"qian.sample.crm:customer:write\"}",
            CrmCommandJson.Default.CreateCustomerCommand, CrmCommandJson.Default.CustomerItem);
        public ValueTask<JsonElement> InvokeAsync(IPluginContext caller, JsonElement input, CancellationToken cancellationToken = default)
        {
            Interlocked.Increment(ref _calls);
            return ValueTask.FromResult(JsonSerializer.SerializeToElement(new CustomerItem(Guid.CreateVersion7(), caller.Scope.Id, new string('x', nameLength)), CrmCommandJson.Default.CustomerItem));
        }
    }

    private sealed class ProbePlugin(IQianPlugin inner, IReadOnlyList<IExecutableCapability> probes) : IQianPlugin
    {
        public IReadOnlyList<ServiceKey> Inject => inner.Inject;
        public async ValueTask ApplyAsync(IPluginContext ctx, JsonElement config, CancellationToken ct)
        {
            await inner.ApplyAsync(ctx, config, ct);
            foreach (var probe in probes) ctx.Provide<IExecutableCapability>(CapabilityBindingRegistration.Key, probe.Contract.Id, probe);
        }
    }
}

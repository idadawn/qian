using System.Diagnostics;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Nodes;
using Kun.Core.MultiTenancy;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Qian.Audit;
using Qian.Data;
using Qian.Plugin.Sdk;
using Qian.Registry;
using Qian.Runtime;
using Qian.Surfaces;

namespace Qian.Sample.Crm.Tests;

public sealed partial class CapabilityTests
{
    [Fact]
    public async Task 共用产品场景_真实scope遮蔽与restrict交集在目录提示执行一致_双客户端及真库副作用对照()
    {
        using var fixture = JsonDocument.Parse(File.ReadAllText(Path.Combine(RepositoryRoot(), "tck/runtime/capability-filter.json")));
        var spec = fixture.RootElement; Assert.Equal(1, spec.GetProperty("version").GetInt32());
        await using var host = await Start(capabilities: true);
        var runtime = host.App.Services.GetRequiredService<CrmCommandRuntime>().Loader;
        var owner = runtime.GetContext("data", ScopeKey.Global);
        var tenant = host.App.Services.GetRequiredService<ITenantContext>(); var data = host.App.Services.GetRequiredService<IDataService>();
        var patches = new PostgresPatchRepository(data, tenant, new SurfaceValidator());
        var leases = new Dictionary<string, IDisposable>(); var reports = new List<JsonElement>();
        try
        {
            var index = 0;
            foreach (var step in spec.GetProperty("steps").EnumerateArray())
            {
                var tenantId = step.TryGetProperty("tenant", out var alias) ? alias.GetString() switch { "a" => A, "b" => B, _ => throw new InvalidOperationException("未知租户别名。") } : A;
                var scope = new ScopeKey("tenant", tenantId);
                switch (step.GetProperty("op").GetString())
                {
                    case "restrict":
                        var keep = step.GetProperty("keep").EnumerateArray().Select(item => spec.GetProperty("component").GetString() + "/" + item.GetString()).ToHashSet(StringComparer.Ordinal);
                        leases.Add(step.GetProperty("id").GetString()!, owner.Restrict(scope, registration => registration.Key != CapabilityBindingRegistration.Key || keep.Contains(registration.Name)));
                        break;
                    case "release": leases[step.GetProperty("id").GetString()!].Dispose(); leases.Remove(step.GetProperty("id").GetString()!); break;
                    case "patch":
                        using (tenant.BeginScope(tenantId)) await patches.PutAsync(scope, "crm", step.GetProperty("patch"), step.GetProperty("revision").GetInt32(), "shared-tck", Ct);
                        break;
                    case "removePatch":
                        using (tenant.BeginScope(tenantId)) await patches.RemoveAsync(scope, "crm", step.GetProperty("revision").GetInt32(), "shared-tck", Ct);
                        break;
                    case "unmount": await runtime.UnmountAsync("crm", ScopeKey.Global, Ct); break;
                    case "probe": await Probe(step, tenantId, scope, index); break;
                    default: throw new InvalidOperationException("未知产品 TCK 操作。");
                }
                index++;
            }
            if (Environment.GetEnvironmentVariable("QIAN_PRODUCT_FILTER_REPORT") is { Length: > 0 } report)
            {
                Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(report))!);
                File.WriteAllText(report, JsonSerializer.Serialize(new { id = spec.GetProperty("id").GetString(), checks = reports }));
            }
        }
        finally { foreach (var lease in leases.Values.Reverse()) lease.Dispose(); }

        async Task Probe(JsonElement step, string tenantId, ScopeKey scope, int index)
        {
            var scopes = step.TryGetProperty("oauthScopes", out var configured) ? configured.GetString()! : "crm.read crm.write";
            using var list = await CatalogRequest(host, tenantId, "/api/capabilities", scopes); Assert.Equal(HttpStatusCode.OK, list.StatusCode);
            var catalog = (await list.Content.ReadFromJsonAsync<JsonElement>(Ct)).GetProperty("items").EnumerateArray().Select(item => item.GetProperty("id").GetString()!).Order(StringComparer.Ordinal).ToArray();
            var expected = step.GetProperty("visible").EnumerateArray().Select(item => spec.GetProperty("component").GetString() + "/" + item.GetString()).Order(StringComparer.Ordinal).ToArray();
            Assert.Equal(expected, catalog);
            using var promptResponse = await CatalogRequest(host, tenantId, "/api/acceptance/prompt", scopes); Assert.Equal(HttpStatusCode.OK, promptResponse.StatusCode);
            var text = await promptResponse.Content.ReadAsStringAsync(Ct);
            var prompt = spec.GetProperty("capabilities").EnumerateArray().Select(item => item.GetString()!).Where(id => text.Contains("工具：" + id + "\n", StringComparison.Ordinal)).Order(StringComparer.Ordinal).ToArray();
            Assert.Equal(expected, prompt);
            var command = new CreateCustomerCommand(Guid.CreateVersion7(), "shared capability TCK customer");
            using var invoked = await Send(spec.GetProperty("target").GetString()!);
            Assert.True((int)invoked.StatusCode == step.GetProperty("status").GetInt32(), step.GetProperty("id").GetString() + ": " + host.FailureEvidence + await invoked.Content.ReadAsStringAsync(Ct));
            bool? errorEquivalent = null; Guid? customerId = null;
            if (invoked.StatusCode == HttpStatusCode.NotFound)
            {
                using var unknown = await Send(spec.GetProperty("unknown").GetString()!); Assert.Equal(HttpStatusCode.NotFound, unknown.StatusCode);
                var hidden = Normalize(await invoked.Content.ReadAsStringAsync(Ct)); var absent = Normalize(await unknown.Content.ReadAsStringAsync(Ct));
                Assert.Equal("NOT_FOUND", hidden["code"]!.GetValue<string>()); Assert.True(JsonNode.DeepEquals(hidden, absent)); errorEquivalent = true;
            }
            else { var customer = await invoked.Content.ReadFromJsonAsync<CustomerItem>(Ct); Assert.NotNull(customer); Assert.Equal(tenantId, customer.TenantId); customerId = customer.Id; }
            var net = JsonSerializer.SerializeToElement(new { id = step.GetProperty("id").GetString(), catalog, prompt, status = (int)invoked.StatusCode, errorEquivalent });
            var node = await NodeProbe(index, host.Token(tenantId, scopes), tenantId, command, customerId);
            Assert.True(JsonElement.DeepEquals(net, node), "双客户端的真实产品输出不一致。");
            int customers, outboxTotal, auditEntries;
            using (tenant.BeginScope(tenantId))
            {
                await using var session = await data.OpenAsync<CrmDbContext>(Ct);
                customers = await session.Context.Customers.CountAsync(Ct);
                Assert.Equal(step.GetProperty("customers").GetInt32(), customers);
                Assert.Equal(step.GetProperty("customers").GetInt32(), await session.Context.CustomerReceipts.CountAsync(Ct));
                // Outbox 是服务级传输表；这里核对全局增量，不声称它受业务表 RLS 过滤。
                outboxTotal = await session.Context.OutboxMessages.CountAsync(Ct);
                Assert.Equal(step.GetProperty("outboxTotal").GetInt32(), outboxTotal);
                var audit = owner.Scoped(scope).Audit();
                Assert.Equal(new AuditChainVerification(true, step.GetProperty("audit").GetInt32(), null, null), await audit.VerifyAsync(Ct));
                auditEntries = (await audit.ReadAsync(0, 100, cancellationToken: Ct)).Count;
                Assert.Equal(step.GetProperty("audit").GetInt32(), auditEntries);
            }
            var binding = owner.Scoped(scope).List(CapabilityBindingRegistration.Key).SingleOrDefault(item => item.Name == spec.GetProperty("target").GetString());
            Assert.Equal(step.GetProperty("bindingScope").GetString(), binding?.Scope.Kind ?? "none");
            if (step.TryGetProperty("bindingVersion", out var version)) Assert.Equal(version.GetString(), runtime.DumpConfig().Single(entry => entry.Id == binding!.OwnerEntryId && entry.Scope == binding.Scope).Version);
            reports.Add(JsonSerializer.SerializeToElement(new { observation = net, customers, outboxTotal, audit = auditEntries, bindingScope = binding?.Scope.Kind ?? "none" }));

            Task<HttpResponseMessage> Send(string id)
            {
                var request = new HttpRequestMessage(HttpMethod.Post, "/api/capabilities/" + id + "/invoke") { Content = JsonContent.Create(command) };
                request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", host.Token(tenantId, scopes)); return host.Send(request);
            }
        }
        async Task<JsonElement> NodeProbe(int step, string token, string tenantId, CreateCustomerCommand command, Guid? customerId)
        {
            var start = new ProcessStartInfo("node") { WorkingDirectory = RepositoryRoot(), UseShellExecute = false, RedirectStandardInput = true, RedirectStandardOutput = true, RedirectStandardError = true };
            start.ArgumentList.Add("tck/runtime/capability-filter-client.mjs");
            using var process = Process.Start(start)!;
            var output = process.StandardOutput.ReadToEndAsync(Ct); var errors = process.StandardError.ReadToEndAsync(Ct);
            await process.StandardInput.WriteLineAsync(JsonSerializer.Serialize(new { step, token, tenantId, command, customerId, baseUrl = host.Client.BaseAddress!.ToString() }, JsonSerializerOptions.Web)); process.StandardInput.Close();
            using var deadline = CancellationTokenSource.CreateLinkedTokenSource(Ct); deadline.CancelAfter(TimeSpan.FromSeconds(20));
            try { await process.WaitForExitAsync(deadline.Token); } finally { if (!process.HasExited) process.Kill(entireProcessTree: true); }
            var stdout = await output; var stderr = await errors;
            Assert.DoesNotContain(token, stdout + stderr, StringComparison.Ordinal); Assert.True(process.ExitCode == 0, stderr);
            using var result = JsonDocument.Parse(stdout); return result.RootElement.Clone();
        }
    }
}

public abstract partial class CustomerCommandTests
{
    private protected static JsonObject Normalize(string json) { var value = JsonNode.Parse(json)!.AsObject(); value.Remove("trace_id"); value.Remove("instance"); return value; }
}

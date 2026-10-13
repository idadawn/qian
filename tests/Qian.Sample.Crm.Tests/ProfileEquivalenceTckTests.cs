using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Diagnostics;
using System.Text.Json;
using System.Text.Json.Nodes;
using Kun.Core.MultiTenancy;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Qian.Audit;
using Qian.Data;
using Qian.Runtime;

namespace Qian.Sample.Crm.Tests;

public sealed partial class CustomerCommandTests
{
    [Fact]
    public async Task T23_同组真实Provider从两个profile启动_业务权限隔离事务与依赖恢复结果等价()
    {
        using var fixture = JsonDocument.Parse(File.ReadAllText(Path.Combine(RepositoryRoot(), "tck/runtime/profile-equivalence.json")));
        var spec = fixture.RootElement; Assert.Equal(1, spec.GetProperty("version").GetInt32());
        var reports = new Dictionary<string, JsonElement>(); Dictionary<string, string>? businessTypes = null;
        foreach (var mode in spec.GetProperty("profiles").EnumerateArray())
        foreach (var client in spec.GetProperty("clients").EnumerateArray())
        {
            var profile = mode.GetString()!;
            await using var host = await Start(runtimeProfile: profile);
            var loader = host.App.Services.GetRequiredService<CrmCommandRuntime>().Loader;
            var plan = (ProfileModeAssembly)host.App.Services.GetRequiredService<ICrmRuntimeAssembly>();
            Assert.Equal(profile, plan.Snapshot.Name);
            Assert.Equal(spec.GetProperty("businessProviders").EnumerateArray().Select(item => item.GetString()), plan.BusinessTypes.Keys.Order(StringComparer.Ordinal));
            if (businessTypes is null) businessTypes = plan.BusinessTypes.ToDictionary();
            else Assert.Equal(businessTypes.OrderBy(item => item.Key, StringComparer.Ordinal), plan.BusinessTypes.OrderBy(item => item.Key, StringComparer.Ordinal));
            var expectedEntries = spec.GetProperty("businessProviders").EnumerateArray().Select(item => item.GetString()!).ToList();
            if (profile == "platform") expectedEntries.AddRange(spec.GetProperty("platformProviders").EnumerateArray().Select(item => item.GetString()!));
            Assert.Equal(expectedEntries.Order(StringComparer.Ordinal), loader.DumpConfig().Select(item => item.Id).Order(StringComparer.Ordinal));
            foreach (var entry in loader.DumpConfig()) Assert.Equal(PluginState.Ready, loader.GetState(entry.Id, entry.Scope));
            if (profile == "minimal") Assert.Equal("qian.bundle.minimal@1.0.0", Assert.Single(plan.Snapshot.Layers, item => item.Kind == "bundle").Reference);
            else Assert.Equal("qian.bundle.tck-platform@1.0.0", Assert.Single(plan.Snapshot.Layers, item => item.Kind == "bundle").Reference);
            var ids = new Dictionary<string, Guid>(); var observations = new List<JsonElement>();
            foreach (var step in spec.GetProperty("steps").EnumerateArray())
            {
                switch (step.GetProperty("op").GetString())
                {
                    case "request": await Request(step); break;
                    case "tenantStatus":
                        var state = step.GetProperty("value").GetString(); Assert.True(state is "active" or "suspended");
                        await Admin($"UPDATE plugin_qian_tenancy.tenants SET status = '{state}' WHERE id = '{A}'::uuid;"); break;
                    case "unmountAuthz":
                        await loader.UnmountAsync("authz", ScopeKey.Global, Ct); await loader.WhenIdleAsync(Ct);
                        Assert.Equal(PluginState.Pending, loader.GetState("crm", ScopeKey.Global)); break;
                    case "restoreAuthz":
                        await loader.MountAsync([plan.Definitions["authz"]], Ct); await loader.WhenIdleAsync(Ct);
                        Assert.Equal(PluginState.Ready, loader.GetState("crm", ScopeKey.Global)); break;
                    default: throw new InvalidOperationException("未知 T23 操作。");
                }
            }
            var tenant = host.App.Services.GetRequiredService<ITenantContext>(); var data = host.App.Services.GetRequiredService<IDataService>();
            int customers, outbox; long auditA, auditB;
            using (tenant.BeginScope(A))
            {
                await using var session = await data.OpenAsync<CrmDbContext>(Ct);
                customers = await session.Context.Customers.CountAsync(Ct); outbox = await session.Context.OutboxMessages.CountAsync(Ct);
                Assert.Equal(spec.GetProperty("final").GetProperty("customers").GetInt32(), customers);
                Assert.Equal(customers, await session.Context.CustomerReceipts.CountAsync(Ct));
                Assert.Equal(spec.GetProperty("final").GetProperty("outbox").GetInt32(), outbox);
                var chain = await loader.GetContext("audit", ScopeKey.Global).Audit().VerifyAsync(Ct); Assert.True(chain.Valid); auditA = chain.CheckedEntries;
                Assert.Equal(spec.GetProperty("final").GetProperty("auditA").GetInt64(), auditA);
            }
            using (tenant.BeginScope(B))
            {
                await using var session = await data.OpenAsync<CrmDbContext>(Ct); Assert.Empty(await session.Context.Customers.ToListAsync(Ct));
                var chain = await loader.GetContext("audit", ScopeKey.Global).Audit().VerifyAsync(Ct); Assert.True(chain.Valid); auditB = chain.CheckedEntries;
                Assert.Equal(spec.GetProperty("final").GetProperty("auditB").GetInt64(), auditB);
            }
            if (profile == "platform")
            {
                await loader.UnmountAsync("registry", ScopeKey.Global, Ct); await loader.WhenIdleAsync(Ct);
                Assert.Equal(PluginState.Ready, loader.GetState("crm", ScopeKey.Global));
                using var missingRegistry = await host.Post(A, new CreateCustomerCommand(Guid.CreateVersion7(), "must not execute"));
                Assert.Equal(System.Net.HttpStatusCode.NotFound, missingRegistry.StatusCode);
                Assert.Equal("NOT_FOUND", (await missingRegistry.Content.ReadFromJsonAsync<JsonElement>(Ct)).GetProperty("code").GetString());
                using var current = tenant.BeginScope(A); await using var unchanged = await data.OpenAsync<CrmDbContext>(Ct);
                Assert.Equal(customers, await unchanged.Context.Customers.CountAsync(Ct));
                Assert.Equal(auditA, (await loader.GetContext("audit", ScopeKey.Global).Audit().VerifyAsync(Ct)).CheckedEntries);
            }
            await host.App.StopAsync(Ct); Assert.Empty(loader.DumpConfig());
            reports[profile + "/" + client.GetString()] = JsonSerializer.SerializeToElement(new { observations, customers, outbox, auditA, auditB, clean = loader.DumpConfig().Count == 0 });

            async Task Request(JsonElement step)
            {
                var tenantId = step.GetProperty("tenant").GetString() switch { "a" => A, "b" => B, _ => throw new InvalidOperationException("未知租户。") };
                var route = step.GetProperty("path").GetString()!;
                foreach (var capture in ids) route = route.Replace("{" + capture.Key + "}", capture.Value.ToString(), StringComparison.Ordinal);
                var token = host.Token(tenantId, step.TryGetProperty("oauthScopes", out var scopes) ? scopes.GetString()! : "crm.read crm.write");
                int status; string text;
                if (client.GetString() == "dotnet")
                {
                    using var request = new HttpRequestMessage(new HttpMethod(step.GetProperty("method").GetString()!), route);
                    if (step.TryGetProperty("body", out var input)) request.Content = JsonContent.Create(input);
                    if (!step.TryGetProperty("anonymous", out var anonymous) || !anonymous.GetBoolean()) request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
                    using var response = await host.Client.SendAsync(request, Ct); status = (int)response.StatusCode; text = await response.Content.ReadAsStringAsync(Ct);
                }
                else if (client.GetString() == "node")
                {
                    var node = await NodeRequest(step.GetProperty("id").GetString()!, token, tenantId);
                    status = node.GetProperty("status").GetInt32(); text = node.GetProperty("body").GetRawText();
                }
                else throw new InvalidOperationException("未知 T23 客户端。");
                Assert.True(status == step.GetProperty("status").GetInt32(), profile + "/" + step.GetProperty("id").GetString() + ": " + text + host.FailureEvidence);
                var value = string.IsNullOrEmpty(text) ? new JsonObject() : JsonNode.Parse(text)!.AsObject();
                if (step.TryGetProperty("capture", out var captureName))
                {
                    var captured = Guid.Parse(value["id"]!.GetValue<string>()); Assert.NotEqual(Guid.Empty, captured); ids.Add(captureName.GetString()!, captured);
                }
                if (step.TryGetProperty("same", out var same)) Assert.Equal(ids[same.GetString()!], Guid.Parse(value["id"]!.GetValue<string>()));
                if (step.TryGetProperty("name", out var name)) { Assert.Equal(name.GetString(), value["name"]!.GetValue<string>()); Assert.Equal(tenantId, value["tenantId"]!.GetValue<string>()); }
                if (step.TryGetProperty("names", out var names))
                {
                    Assert.Equal(names.GetArrayLength(), value["total"]!.GetValue<int>());
                    Assert.Equal(names.EnumerateArray().Select(item => item.GetString()), value["items"]!.AsArray().Select(item => item!["name"]!.GetValue<string>()).Order(StringComparer.Ordinal));
                }
                value.Remove("trace_id"); value.Remove("instance");
                NormalizeIds(value);
                observations.Add(JsonSerializer.SerializeToElement(new { id = step.GetProperty("id").GetString(), status, body = value }));
            }
            async Task<JsonElement> NodeRequest(string id, string token, string tenantId)
            {
                var start = new ProcessStartInfo("node") { WorkingDirectory = RepositoryRoot(), UseShellExecute = false, RedirectStandardInput = true, RedirectStandardOutput = true, RedirectStandardError = true };
                start.ArgumentList.Add("tck/runtime/profile-equivalence-client.mjs");
                using var process = Process.Start(start)!; var output = process.StandardOutput.ReadToEndAsync(Ct); var errors = process.StandardError.ReadToEndAsync(Ct);
                await process.StandardInput.WriteLineAsync(JsonSerializer.Serialize(new { id, token, tenantId, ids, baseUrl = host.Client.BaseAddress!.ToString() })); process.StandardInput.Close();
                using var deadline = CancellationTokenSource.CreateLinkedTokenSource(Ct); deadline.CancelAfter(TimeSpan.FromSeconds(20));
                try { await process.WaitForExitAsync(deadline.Token); } finally { if (!process.HasExited) process.Kill(entireProcessTree: true); }
                var stdout = await output; var stderr = await errors;
                Assert.False((stdout + stderr).Contains(token, StringComparison.Ordinal), "客户端不能输出身份令牌。"); Assert.True(process.ExitCode == 0, stderr);
                using var parsed = JsonDocument.Parse(stdout); return parsed.RootElement.Clone();
            }
            void NormalizeIds(JsonNode? node)
            {
                if (node is JsonObject obj)
                    foreach (var property in obj.ToArray())
                    {
                        if (property.Key == "id" && property.Value is JsonValue value && value.TryGetValue<string>(out var raw) && Guid.TryParse(raw, out var id))
                            obj[property.Key] = "@" + Assert.Single(ids, item => item.Value == id).Key;
                        else NormalizeIds(property.Value);
                    }
                else if (node is JsonArray array) foreach (var child in array) NormalizeIds(child);
            }
        }
        foreach (var observation in reports.Values) Assert.True(JsonElement.DeepEquals(reports.First().Value, observation), JsonSerializer.Serialize(reports));
        if (Environment.GetEnvironmentVariable("QIAN_PROFILE_EQUIVALENCE_REPORT") is { Length: > 0 } report)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(report))!);
            File.WriteAllText(report, JsonSerializer.Serialize(new { id = "T23", providers = businessTypes, profiles = reports }));
        }
    }
}

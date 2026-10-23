using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Qian.Testing.Containers;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Primitives;
using Qian.Config.Apollo;
using Qian.Runtime.Errors;

namespace Qian.Config.Tests;

[Collection(RealApolloGroup.Name)]
public sealed class RealApolloPortalProtocolTests(RealApolloPortalFixture apollo, PostgresFixture postgres) : IClassFixture<RealApolloPortalFixture>, IClassFixture<PostgresFixture>
{
    [Fact]
    public async Task 真实Portal命名空间权限分离_草稿仅人工发布后进入ConfigService()
    {
        var ct = TestContext.Current.CancellationToken;
        await apollo.PrepareFlagsAsync(ct);
        using var client = new HttpClient { BaseAddress = apollo.PortalUri };
        Assert.Equal(HttpStatusCode.Forbidden, await Write(apollo.UnauthorizedToken));
        Assert.Equal(HttpStatusCode.OK, await Write(apollo.EditToken));
        Assert.Equal("{\"default\":true}", await apollo.ReadDraftAsync("flag.preview", ct));
        using var config = new HttpClient { BaseAddress = apollo.ConfigServiceUri };
        Assert.Equal("{\"default\":false}", await Published());
        Assert.Equal(HttpStatusCode.Forbidden, await apollo.TryPublishAsync(apollo.EditToken, ct));
        await apollo.PublishManuallyAsync(ct);
        await apollo.WaitForPublishedAsync(apollo.AppId, "qian.common.flags", "flag.preview", "{\"default\":true}", ct);
        Assert.Equal("{\"default\":true}", await Published());
        var directory = Directory.CreateDirectory(Path.Combine(Path.GetTempPath(), "qian-flags-portal-" + Guid.NewGuid().ToString("N"))).FullName;
        try
        {
            var root = new ConfigurationBuilder().AddQianConfigurationSources(new QianConfigurationOptions
            {
                BasePath = directory, Arguments = ["--Apollo:AppId=" + apollo.AppId, "--Apollo:Env=UAT",
                    "--Apollo:ConfigServer:0=" + apollo.ConfigServiceUri, "--Apollo:Namespaces:0=qian.common.flags"]
            }).Build();
            await using var host = await FlagManagementTestHost.Create(postgres, apollo.PortalUri, apollo.AppId, apollo.EditToken, ct, root);
            var request = new FeatureFlagDraft { Name = "managed-preview", Rule = new FeatureFlagPolicy { Default = true }, Confirmed = true, IdempotencyKey = Guid.NewGuid() };
            var result = await host.Drafts.SetAsync(request, ct);
            Assert.Equal("pending-publish", result.Status);
            Assert.False(host.Context.Config().Flags.IsEnabled("managed-preview", Guid.NewGuid()));
            using (var draft = JsonDocument.Parse((await apollo.ReadDraftAsync("flag.managed-preview", ct))!))
                Assert.True(draft.RootElement.GetProperty("default").GetBoolean());
            Assert.Equal(result.RuleHash, (await host.Drafts.SetAsync(request, ct)).RuleHash);
            Assert.Equal(1, host.Secrets.Reads); // 持久回执重放不再次请求 Portal。
            var changed = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            using var listen = ChangeToken.OnChange(root.GetReloadToken, () => { if (root["flag.managed-preview"] is not null) changed.TrySetResult(); });
            await apollo.PublishManuallyAsync(ct);
            await changed.Task.WaitAsync(TimeSpan.FromSeconds(60), ct);
            Assert.True(host.Context.Config().Flags.IsEnabled("managed-preview", Guid.NewGuid()));
            host.Secrets.Token = apollo.UnauthorizedToken;
            var rejected = await Assert.ThrowsAsync<QianErrorException>(() => host.Drafts.SetAsync(new FeatureFlagDraft
                { Name = "rejected", Rule = new FeatureFlagPolicy { Default = true }, Confirmed = true, IdempotencyKey = Guid.NewGuid() }, ct).AsTask());
            Assert.Equal("AUTHZ_DENIED", rejected.Error.Code.Code);
            Assert.DoesNotContain(apollo.UnauthorizedToken, rejected.ToString(), StringComparison.Ordinal);
            Assert.All(host.Audit.Records, record => Assert.DoesNotContain(apollo.EditToken, record.Payload.GetRawText(), StringComparison.Ordinal));
        }
        finally { Directory.Delete(directory, true); }
        TestContext.Current.TestOutputHelper!.WriteLine("真实 Portal 2.5.2：无授权 Token 编辑403、编辑 Token PUT200/发布403，独立人工发布后 ConfigService false→true。");

        async Task<HttpStatusCode> Write(string token)
        {
            using var request = new HttpRequestMessage(HttpMethod.Put, "openapi/v1/envs/UAT/apps/" + apollo.AppId + "/clusters/default/namespaces/qian.common.flags/items/flag.preview?createIfNotExists=true");
            request.Headers.TryAddWithoutValidation("Authorization", token);
            request.Content = JsonContent.Create(new { key = "flag.preview", value = "{\"default\":true}", comment = "fixture", dataChangeCreatedBy = "apollo", dataChangeLastModifiedBy = "apollo" });
            using var result = await client.SendAsync(request, ct);
            if ((int)result.StatusCode >= 500)
            {
                using var document = JsonDocument.Parse(await result.Content.ReadAsStringAsync(ct));
                TestContext.Current.TestOutputHelper!.WriteLine("Portal错误类型=" + (document.RootElement.TryGetProperty("exception", out var value) ? value.GetString() : "unknown"));
            }
            return result.StatusCode;
        }
        async Task<string?> Published()
        {
            var value = await config.GetFromJsonAsync<JsonElement>("configs/" + apollo.AppId + "/default/qian.common.flags", ct);
            return value.GetProperty("configurations").GetProperty("flag.preview").GetString();
        }
    }
}

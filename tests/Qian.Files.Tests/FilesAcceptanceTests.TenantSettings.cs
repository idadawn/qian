using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Nodes;
using Qian.Runtime;
using Qian.Tenancy;

namespace Qian.Files.Tests;

// S1-FILE-004 验收 4：清单去骨架后，平台宿主的真实 Registry 接受 qian.files 条目；平台运营经 TEN-004 的设置端点写租户配置，租户配额覆盖组件配置。
public sealed partial class FilesAcceptanceTests
{
    [Fact]
    public async Task 经TEN004写入的租户配额覆盖组件配置_超限QUOTA_EXCEEDED_其他租户仍按组件配置()
    {
        await using var host = await FilesHost.StartAsync(postgres, platform: true, ct: Ct);
        var platform = host.Platform!; var token = host.PlatformToken(); var path = "/api/tenancy/tenants/" + FilesHost.A + "/settings/qian.files";
        // 写入前租户读到的是组件配置（10 MiB）。TEN-004 整体替换配置：运营方在读到的配置上只改配额，存储与上传策略原样带上。
        TenantSettingsDocument current;
        using (var read = await platform.Manage(token, HttpMethod.Get, path))
        {
            Assert.True(read.StatusCode == HttpStatusCode.OK, await read.Content.ReadAsStringAsync(Ct));
            current = (await read.Content.ReadFromJsonAsync<TenantSettingsDocument>(Ct))!;
        }
        Assert.Equal(("qian.files", "files", 0), (current.PluginId, current.EntryId, current.Revision));
        Assert.Equal(FilesHost.Quota, current.Config.GetProperty("quota").GetProperty("perTenantBytes").GetInt64());
        var config = JsonNode.Parse(current.Config.GetRawText())!.AsObject();
        config["quota"] = new JsonObject { ["perTenantBytes"] = MiB };
        using (var written = await platform.Manage(token, HttpMethod.Put, path, new UpdateTenantSettings(JsonSerializer.SerializeToElement(config), 0)))
        {
            Assert.True(written.StatusCode == HttpStatusCode.OK, await written.Content.ReadAsStringAsync(Ct));
            var saved = (await written.Content.ReadFromJsonAsync<TenantSettingsDocument>(Ct))!;
            Assert.Equal((1, (long)MiB), (saved.Revision, saved.Config.GetProperty("quota").GetProperty("perTenantBytes").GetInt64()));
        }
        // Registry 按新配置在 A 的 scope 挂了 qian.files 实例；A 的配额变为 1 MiB，B 仍是组件配置的 10 MiB。
        Assert.Equal(PluginState.Ready, host.Loader.GetState("files", new ScopeKey("tenant", FilesHost.A)));
        Assert.Equal(new FileQuota(MiB, 0, 0), await Quota(host, FilesHost.A));
        Assert.Equal(new FileQuota(FilesHost.Quota, 0, 0), await Quota(host, FilesHost.B));

        await host.Upload(FilesHost.A, Text("tenant a", 768 * 1024));
        using (var exceeded = await host.Presign(FilesHost.A, FilesHost.Request(Text("over", 512 * 1024))))
        {
            var error = await AssertError(exceeded, HttpStatusCode.UnprocessableEntity, "QUOTA_EXCEEDED");
            Assert.Equal("#/size", error.GetProperty("pointer").GetString());
            Assert.Equal("文件大小超出租户配额（剩余 262144 字节）。", error.GetProperty("message").GetString());
        }
        using (var other = await host.Presign(FilesHost.B, FilesHost.Request(Text("tenant b", 2 * MiB))))
            Assert.Equal(HttpStatusCode.Created, other.StatusCode);
    }
}

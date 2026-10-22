using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.Extensions.DependencyInjection;
using Qian.Files.AspNetCore;
using Qian.Profiles;
using Qian.Profiles.Abstractions;
using Qian.Surfaces;

namespace Qian.Files.Tests;

// 组合包里 qian.files 的配置（qian.bundle.base 进 platform；qian.bundle.identity-server 的同名条目默认 disabled）：
// local 后端必须有公开基址，缺 endpoint 会在挂载时报 INPUT_INVALID #/storage/endpoint；基址路径要与直传端点的缺省路径一致。
public sealed class FilesProfileConfigurationTests
{
    [Fact]
    public void platform组合的files配置可被local后端接受_endpoint指向直传端点缺省路径()
    {
        var profile = new ProfileLoader(new SurfaceValidator()).Load(new ProfileLoadOptions
        {
            WorkingDirectory = FilesMetadata.RepositoryRoot(), Profile = "platform", IncludeHomePatch = false
        });
        Assert.True(profile.IsValid, JsonSerializer.Serialize(profile.Diagnostics));
        var settings = FilesConfiguration.Read(Assert.Single(profile.Snapshot!.Entries, entry => entry.Id == "files").Config);
        Assert.Equal("local", settings.Kind);
        var endpoint = new Uri(settings.Storage.GetProperty("endpoint").GetString()!);
        Assert.Equal(new FilesEndpointOptions().TransferPath + "/", endpoint.AbsolutePath);
        // 后端构造时会建根目录，测试机不能写 /var/lib：只把 root 换成临时目录，kind 与 endpoint 原样取自组合包。
        var root = Directory.CreateTempSubdirectory("qian-files-profile-").FullName;
        try
        {
            var storage = JsonNode.Parse(settings.Storage.GetRawText())!.AsObject();
            Assert.True(Path.IsPathFullyQualified(storage["root"]!.GetValue<string>()));
            storage["root"] = root;
            Assert.NotNull(new LocalFileBackendFactory(new byte[32]).Create(new FileBackendContext(
                JsonSerializer.SerializeToElement(storage), TimeProvider.System, new ServiceCollection().BuildServiceProvider())).Transfer);
        }
        finally { Directory.Delete(root, recursive: true); }
    }
}

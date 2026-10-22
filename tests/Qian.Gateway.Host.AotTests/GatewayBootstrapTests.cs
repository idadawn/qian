using System.Security.Cryptography;
using System.Text.Json;
using Qian.Config.Apollo;
using Qian.Testing.Containers;

namespace Qian.Gateway.Host.AotTests;

[Collection(GatewayHostGroup.Name)]
public sealed class GatewayBootstrapTests(ApolloFixture apollo) : IClassFixture<ApolloFixture>
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task 环境与CLI引导的AccessKey文件在Apollo首个签名之前可用_密钥目录与轮换生效(bool commandLine)
    {
        var ct = TestContext.Current.CancellationToken;
        var directory = Directory.CreateTempSubdirectory("qian-gateway-bootstrap-").FullName;
        var mounted = Directory.CreateDirectory(Path.Combine(directory, "mounted")).FullName;
        var keyPath = Path.Combine(directory, "access-key");
        var appId = "bootstrap-" + Guid.NewGuid().ToString("N");
        var secret = Convert.ToHexString(RandomNumberGenerator.GetBytes(24));
        var changes = new Dictionary<string, string?>
        {
            ["QIAN_ConnectionFiles__ApolloAccessKey"] = commandLine ? null : keyPath,
            ["QIAN_Secrets__Directory"] = commandLine ? null : mounted
        };
        var previous = changes.Keys.ToDictionary(key => key, Environment.GetEnvironmentVariable, StringComparer.Ordinal);
        try
        {
            foreach (var pair in changes) Environment.SetEnvironmentVariable(pair.Key, pair.Value);
            await GatewayProcess.PrivateFile(keyPath, secret, ct);
            await File.WriteAllTextAsync(Path.Combine(mounted, "Marker__Mounted"), "yes", ct);
            await File.WriteAllTextAsync(Path.Combine(directory, "appsettings.json"), JsonSerializer.Serialize(new
            {
                Apollo = new { AppId = appId, Env = "UAT", ConfigServer = new[] { apollo.ConfigServiceUri.AbsoluteUri },
                    AccessKey = GatewayBootstrap.AccessKeyReference, CacheDir = Path.Combine(directory, "cache") }
            }), ct);
            await apollo.SetAccessKeyAsync(appId, secret, ct);
            await apollo.PublishAsync(appId, "application", new Dictionary<string, string> { ["Marker:Remote"] = "signed" }, cancellationToken: ct);
            string[] args = commandLine ? ["--ConnectionFiles:ApolloAccessKey=" + keyPath, "--Secrets:Directory=" + mounted] : [];
            var options = GatewayBootstrap.CreateOptions(directory, "Development", args);
            Assert.Equal(mounted, options.SecretDirectory);
            using (var configuration = new ConfigurationManager())
            {
                // 与生产入口相同：Add 时立即加载 Apollo，环境/CLI 最终覆盖此时尚未加入根。
                configuration.Sources.Clear();
                configuration.AddQianConfigurationSources(options);
                Assert.Equal("signed", configuration["Marker:Remote"]);
                Assert.Equal("yes", configuration["Marker:Mounted"]);
            }
            var replacement = Convert.ToHexString(RandomNumberGenerator.GetBytes(24));
            await File.WriteAllTextAsync(keyPath, replacement, ct);
            await apollo.SetAccessKeyAsync(appId, replacement, ct);
            using (var reloaded = new ConfigurationManager())
            {
                reloaded.Sources.Clear();
                reloaded.AddQianConfigurationSources(options);
                Assert.Equal("signed", reloaded["Marker:Remote"]);
            }
            foreach (var cache in Directory.GetFiles(Path.Combine(directory, "cache"), "*.json", SearchOption.AllDirectories))
            {
                var contents = await File.ReadAllTextAsync(cache, ct);
                Assert.False(contents.Contains(secret, StringComparison.Ordinal) || contents.Contains(replacement, StringComparison.Ordinal), "Apollo 缓存不能包含密钥。");
            }
        }
        finally
        {
            foreach (var pair in previous) Environment.SetEnvironmentVariable(pair.Key, pair.Value);
            Directory.Delete(directory, recursive: true);
        }
    }
}

[CollectionDefinition(Name, DisableParallelization = true)]
public sealed class GatewayHostGroup
{
    public const string Name = "gateway-host-acceptance";
}

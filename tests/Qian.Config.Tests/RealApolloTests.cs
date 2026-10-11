using System.Diagnostics;
using System.Text.Json;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Qian.Config.Apollo;
using Qian.Testing.Containers;

namespace Qian.Config.Tests;

public sealed class RealApolloTests(RealApolloFixture apollo) : IClassFixture<RealApolloFixture>
{
    [Fact]
    public async Task 真实Apollo发版推动IOptionsMonitor_离线缓存可启动_无缓存失败()
    {
        var ct = TestContext.Current.CancellationToken;
        var directory = Directory.CreateDirectory(Path.Combine(Path.GetTempPath(), "qian-real-apollo-" + Guid.NewGuid().ToString("N"))).FullName;
        var appId = "qian-config-" + Guid.NewGuid().ToString("N");
        try
        {
            await apollo.CreateAppAsync(appId, ct);
            await apollo.PublishAsync(appId, "Sample:PageSize", "10", ct);
            var configuration = Build("cache");
            using (var owner = (IDisposable)configuration)
            using (var services = new ServiceCollection().AddQianConfiguration(configuration)
                .Configure<SampleOptions>(configuration.GetSection("Sample")).BuildServiceProvider())
            {
                var monitor = services.GetRequiredService<IOptionsMonitor<SampleOptions>>();
                Assert.Equal(10, monitor.CurrentValue.PageSize);
                var updated = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
                using var listener = monitor.OnChange(options => { if (options.PageSize == 25) updated.TrySetResult(); });
                var watch = Stopwatch.StartNew();
                await apollo.PublishAsync(appId, "Sample:PageSize", "25", ct);
                await updated.Task.WaitAsync(TimeSpan.FromSeconds(60), ct);
                Assert.Equal(25, monitor.CurrentValue.PageSize);
                Assert.True(watch.Elapsed < TimeSpan.FromSeconds(60));
                TestContext.Current.TestOutputHelper!.WriteLine("Apollo 真实发布到 IOptionsMonitor 更新耗时：" + watch.Elapsed);
                var evidence = Environment.GetEnvironmentVariable("QIAN_TEST_EVIDENCE_DIR");
                if (!string.IsNullOrWhiteSpace(evidence))
                {
                    Directory.CreateDirectory(evidence);
                    await File.WriteAllTextAsync(Path.Combine(evidence, "apollo-real-reload.json"), JsonSerializer.Serialize(new
                    {
                        server = "Apollo 2.5.2 ConfigService/AdminService", database = "MySQL 8.0.43",
                        before = 10, after = monitor.CurrentValue.PageSize, elapsedMilliseconds = watch.ElapsedMilliseconds,
                        restart = false
                    }), ct);
                }
            }
            await apollo.Container.StopAsync(ct);
            var cached = Build("cache");
            using (var owner = (IDisposable)cached) Assert.Equal("25", cached["Sample:PageSize"]);
            Assert.Throws<InvalidOperationException>(() => Build("no-cache"));
        }
        finally { Directory.Delete(directory, true); }

        IConfigurationRoot Build(string cache) => new ConfigurationBuilder().AddQianConfigurationSources(new QianConfigurationOptions
        {
            BasePath = directory,
            Arguments = ["--Apollo:AppId=" + appId, "--Apollo:Env=UAT", "--Apollo:Cluster=default",
                "--Apollo:ConfigServer:0=" + apollo.ConfigServiceUri.AbsoluteUri, "--Apollo:CacheDir=" + cache]
        }).Build();
    }
    public sealed class SampleOptions { public int PageSize { get; set; } }
}

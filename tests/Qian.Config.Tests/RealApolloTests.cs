using System.Diagnostics;
using System.Text.Json;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Primitives;
using Qian.Config.Apollo;
using Qian.Testing.Containers;

namespace Qian.Config.Tests;

[Collection(RealApolloGroup.Name)]
public sealed class RealApolloTests(RealApolloFixture apollo) : IClassFixture<RealApolloFixture>
{
    [Fact]
    public async Task 真实Apollo发版推动IOptionsMonitor_离线缓存可启动_无缓存失败()
    {
        var ct = TestContext.Current.CancellationToken;
        var directory = Directory.CreateDirectory(Path.Combine(Path.GetTempPath(), "qian-real-apollo-" + Guid.NewGuid().ToString("N"))).FullName;
        var appId = "qian-config-" + Guid.NewGuid().ToString("N");
        var log = new RejectionLog();
        using var schema = JsonDocument.Parse("""{"properties":{"Service":{"properties":{"credential":{"x-qian-sensitive":true}}}}}""");
        const string originalReference = "secretRef://platform/service/original";
        const string rotatedReference = "secretRef://platform/service/rotated";
        try
        {
            await apollo.CreateAppAsync(appId, ct);
            await apollo.PublishAsync(appId, "Sample:PageSize", "10", ct);
            await apollo.PublishAsync(appId, "Service:credential", originalReference, ct);
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
                watch.Stop();
                Assert.Equal(25, monitor.CurrentValue.PageSize);
                Assert.True(watch.Elapsed < TimeSpan.FromSeconds(60));
                TestContext.Current.TestOutputHelper!.WriteLine("Apollo 真实发布到 IOptionsMonitor 更新耗时：" + watch.Elapsed);
                await apollo.PublishAsync(appId, "Service:credential", "synthetic-rejected-literal", ct);
                await log.Rejected.Task.WaitAsync(TimeSpan.FromSeconds(60), ct);
                Assert.Equal(originalReference, configuration["Service:credential"]);
                var cacheFile = Assert.Single(Directory.GetFiles(Path.Combine(directory, "cache", "UAT"), "*.json"));
                Assert.DoesNotContain("synthetic-rejected-literal", await File.ReadAllTextAsync(cacheFile, ct), StringComparison.Ordinal);
                var recovered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
                using var reload = ChangeToken.OnChange(configuration.GetReloadToken, () =>
                { if (configuration["Service:credential"] == rotatedReference) recovered.TrySetResult(); });
                await apollo.PublishAsync(appId, "Service:credential", rotatedReference, ct);
                await recovered.Task.WaitAsync(TimeSpan.FromSeconds(60), ct);
                var evidence = Environment.GetEnvironmentVariable("QIAN_TEST_EVIDENCE_DIR");
                if (!string.IsNullOrWhiteSpace(evidence))
                {
                    Directory.CreateDirectory(evidence);
                    await File.WriteAllTextAsync(Path.Combine(evidence, "apollo-real-reload.json"), JsonSerializer.Serialize(new
                    {
                        server = "Apollo 2.5.2 ConfigService/AdminService", database = "MySQL 8.0.43",
                        before = 10, after = monitor.CurrentValue.PageSize, elapsedMilliseconds = watch.ElapsedMilliseconds,
                        restart = false, sensitiveCandidateRejected = true, cacheContainsRejectedValue = false,
                        recoveredAfterRejection = configuration["Service:credential"] == rotatedReference
                    }), ct);
                }
            }
            await apollo.Container.StopAsync(ct);
            var cached = Build("cache");
            using (var owner = (IDisposable)cached) Assert.Equal("25", cached["Sample:PageSize"]);
            // 无缓存时必须给出明确原因并拒绝启动，不能以空配置继续（S1-CFG-001 第 2 条）。
            var refused = Assert.Throws<InvalidOperationException>(() => Build("no-cache"));
            Assert.Contains("Apollo 不可达且无有效本地缓存", refused.Message, StringComparison.Ordinal);
        }
        finally { Directory.Delete(directory, true); }

        IConfigurationRoot Build(string cache) => new ConfigurationBuilder().AddQianConfigurationSources(new QianConfigurationOptions
        {
            BasePath = directory,
            ApolloConfigurationSchema = schema.RootElement, Logger = log,
            Arguments = ["--Apollo:AppId=" + appId, "--Apollo:Env=UAT", "--Apollo:Cluster=default",
                "--Apollo:ConfigServer:0=" + apollo.ConfigServiceUri.AbsoluteUri, "--Apollo:CacheDir=" + cache]
        }).Build();
    }
    public sealed class SampleOptions { public int PageSize { get; set; } }
    private sealed class RejectionLog : ILogger
    {
        public TaskCompletionSource Rejected { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
        public bool IsEnabled(LogLevel logLevel) => true;
        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
        {
            if (formatter(state, exception).Contains("快照校验失败", StringComparison.Ordinal)) Rejected.TrySetResult();
        }
    }
}

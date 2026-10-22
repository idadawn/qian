using System.Text.Json;
using System.Diagnostics;
using DotNet.Testcontainers.Builders;
using DotNet.Testcontainers.Configurations;
using Npgsql;
using Qian.Testing.Containers;
using Qian.Testing.NativeData;

namespace Qian.Data.Native.Postgres.AotTests;

public sealed class NativeAotTests(PostgresFixture postgres) : IClassFixture<PostgresFixture>
{
    [Fact]
    public async Task Linux严格原生发布产物运行真实受限数据库链路()
    {
        var ct = TestContext.Current.CancellationToken;
        var database = await NativeDataDatabase.Create(postgres, ct);
        var executable = Environment.GetEnvironmentVariable("QIAN_NATIVE_DATA_EXECUTABLE");
        string Inside(string connection) => executable is not null ? connection :
            new NpgsqlConnectionStringBuilder(connection) { Host = postgres.Container.IpAddress, Port = 5432 }.ConnectionString;
        var input = JsonSerializer.SerializeToUtf8Bytes(new
        {
            schema = database.Schema, tenantRole = database.TenantRole, platformRole = database.PlatformRole,
            tenantConnection = Inside(database.TenantConnection), platformConnection = Inside(database.PlatformConnection), predicate = database.Predicate
        });
        try
        {
            if (executable is not null)
            {
                var directory = Directory.CreateTempSubdirectory("qian-native-data-aot-");
                try
                {
                    var path = Path.Combine(directory.FullName, "input.json");
                    var fileOptions = new FileStreamOptions { Mode = FileMode.CreateNew, Access = FileAccess.Write };
                    if (!OperatingSystem.IsWindows()) fileOptions.UnixCreateMode = UnixFileMode.UserRead | UnixFileMode.UserWrite;
                    await using (var file = new FileStream(path, fileOptions)) await file.WriteAsync(input, ct);
                    var start = new ProcessStartInfo(executable) { RedirectStandardOutput = true, RedirectStandardError = true, UseShellExecute = false };
                    start.ArgumentList.Add(path);
                    using var process = Process.Start(start)!;
                    var stdout = process.StandardOutput.ReadToEndAsync(ct); var stderr = process.StandardError.ReadToEndAsync(ct);
                    try { await process.WaitForExitAsync(ct).WaitAsync(TimeSpan.FromSeconds(60), ct); }
                    finally { if (!process.HasExited) { process.Kill(entireProcessTree: true); await process.WaitForExitAsync(CancellationToken.None); } }
                    var output = await stdout; var errors = await stderr;
                    Assert.True(process.ExitCode == 0, "原生探针退出失败：" + errors);
                    Assert.Contains("NativeData Linux NativeAOT 真实数据库探针通过。", output, StringComparison.Ordinal);
                    TestContext.Current.TestOutputHelper!.WriteLine(output);
                }
                finally { directory.Delete(recursive: true); }
                return;
            }
            await using var application = new ContainerBuilder(Environment.GetEnvironmentVariable("QIAN_NATIVE_DATA_IMAGE") ?? "qian-native-data-probe:local")
                .WithResourceMapping(input, "/run/qian-native-data.json")
                .WithWaitStrategy(Wait.ForUnixContainer().UntilMessageIsLogged("NativeData Linux NativeAOT 真实数据库探针通过。",
                    options => options.WithMode(WaitStrategyMode.OneShot).WithTimeout(TimeSpan.FromSeconds(60))))
                .Build();
            await application.StartAsync(ct);
            var logs = await application.GetLogsAsync(ct: ct);
            Assert.Equal(0, await application.GetExitCodeAsync(ct));
            Assert.DoesNotContain(database.TenantConnection, logs.Stdout); Assert.DoesNotContain(database.PlatformConnection, logs.Stderr);
            Assert.Contains("通过：原始 SQL 跨租户隔离", logs.Stdout, StringComparison.Ordinal);
            TestContext.Current.TestOutputHelper!.WriteLine(logs.Stdout);
        }
        finally { Array.Clear(input); }
    }
}

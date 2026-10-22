using System.Text.Json;
using System.Diagnostics;
using DotNet.Testcontainers.Builders;
using DotNet.Testcontainers.Configurations;
using Npgsql;
using Qian.Testing.Containers;
using Qian.Testing.NativeAudit;

namespace Qian.Audit.Native.AotTests;

public sealed class NativeAotTests(PostgresFixture postgres) : IClassFixture<PostgresFixture>
{
    [Fact]
    public async Task Linux严格原生发布产物运行真实受限数据库链路()
    {
        var ct = TestContext.Current.CancellationToken;
        var database = await NativeAuditDatabase.Create(postgres, ct);
        await using (var migration = new NpgsqlConnection(postgres.ConnectionString))
        {
            await migration.OpenAsync(ct);
            await using var command = new NpgsqlCommand($"""
                CREATE SCHEMA plugin_native_audit_probe;
                CREATE TABLE plugin_native_audit_probe.business(tenant_id text NOT NULL, value text NOT NULL);
                ALTER TABLE plugin_native_audit_probe.business ENABLE ROW LEVEL SECURITY;
                ALTER TABLE plugin_native_audit_probe.business FORCE ROW LEVEL SECURITY;
                CREATE POLICY tenant_only ON plugin_native_audit_probe.business USING(tenant_id=NULLIF(current_setting('app.current_tenant_id',true),'')) WITH CHECK(tenant_id=NULLIF(current_setting('app.current_tenant_id',true),''));
                GRANT USAGE ON SCHEMA plugin_native_audit_probe TO {database.Roles.TenantLogin};
                GRANT SELECT,INSERT ON plugin_native_audit_probe.business TO {database.Roles.TenantLogin};
                """, migration);
            await command.ExecuteNonQueryAsync(ct);
        }
        var executable = Environment.GetEnvironmentVariable("QIAN_NATIVE_AUDIT_EXECUTABLE");
        string Inside(string connection) => executable is not null ? connection :
            new NpgsqlConnectionStringBuilder(connection) { Host = postgres.Container.IpAddress, Port = 5432 }.ConnectionString;
        var input = JsonSerializer.SerializeToUtf8Bytes(new
        {
            tenantRole = database.Roles.TenantLogin, platformRole = database.Roles.PlatformLogin, functionOwner = database.Roles.FunctionOwner,
            tenantConnection = Inside(database.TenantConnection), platformConnection = Inside(database.PlatformConnection)
        });
        try
        {
            if (executable is not null)
            {
                var directory = Directory.CreateTempSubdirectory("qian-native-audit-aot-");
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
                    Assert.Contains("NativeAudit Linux NativeAOT 真实数据库探针通过。", output, StringComparison.Ordinal);
                    TestContext.Current.TestOutputHelper!.WriteLine(output);
                }
                finally { directory.Delete(recursive: true); }
                return;
            }
            await using var application = new ContainerBuilder(Environment.GetEnvironmentVariable("QIAN_NATIVE_AUDIT_IMAGE") ?? "qian-native-audit-probe:local")
                .WithResourceMapping(input, "/run/qian-native-audit.json")
                .WithWaitStrategy(Wait.ForUnixContainer().UntilMessageIsLogged("NativeAudit Linux NativeAOT 真实数据库探针通过。",
                    options => options.WithMode(WaitStrategyMode.OneShot).WithTimeout(TimeSpan.FromSeconds(60))))
                .Build();
            await application.StartAsync(ct);
            var logs = await application.GetLogsAsync(ct: ct);
            Assert.Equal(0, await application.GetExitCodeAsync(ct));
            Assert.DoesNotContain(database.TenantConnection, logs.Stdout); Assert.DoesNotContain(database.PlatformConnection, logs.Stderr);
            Assert.Contains("通过：外层回滚审计记录和链头", logs.Stdout, StringComparison.Ordinal);
            TestContext.Current.TestOutputHelper!.WriteLine(logs.Stdout);
        }
        finally { Array.Clear(input); }
    }
}

using System.Data.Common;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Kun.MultiTenancy;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using Qian.Data;
using Qian.Runtime.Errors;
using Qian.Surfaces;
using Qian.Surfaces.Abstractions;

namespace Qian.Registry.Tests;

public sealed partial class PatchPersistenceTests
{
    [Fact]
    public async Task 制品目录保存共用校验后的不可变快照_相同摘要幂等_不自动成为已验签或已发布()
    {
        var tenant = new AsyncLocalTenantContext();
        using var host = Host(tenant); await host.StartAsync(Ct); await Prepare(host);
        var (files, digest, fixture) = ArtifactFixture();
        var content = RegistryArtifactContent.Read(files, digest, new SurfaceValidator(), ArtifactOptions);
        Assert.Equal(fixture.GetProperty("records"), JsonSerializer.SerializeToElement(content.Files, JsonSerializerOptions.Web), JsonElementComparer.Instance);
        var catalog = new PostgresArtifactCatalog(host.Services.GetRequiredService<IDataService>());
        var created = await catalog.SubmitAsync(content, "publisher-a", Ct);
        Assert.Equal("Submitted", created.State); Assert.Null(created.SignatureVerification);
        Assert.Equal("test.catalog", created.Component.Id); Assert.Single(created.Component.Documents, d => d.Kind == "command");
        Assert.Equal("customer", created.Metadata["metadata/permissions.json"][0].GetProperty("resource").GetString());
        files["metadata/permissions.json"] = Encoding.UTF8.GetBytes("[]");
        var repeated = await catalog.SubmitAsync(content, "publisher-b", Ct);
        Assert.Equal(created.SubmittedAt, repeated.SubmittedAt); Assert.Equal("publisher-a", repeated.SubmittedBy);
        var read = await catalog.GetAsync(digest, Ct); Assert.NotNull(read);
        Assert.Equal("customer", read.Metadata["metadata/permissions.json"][0].GetProperty("resource").GetString());
        Assert.DoesNotContain("customer", read.ToString(), StringComparison.Ordinal);
        using (tenant.BeginScope("b"))
        {
            var globalCandidate = await catalog.GetAsync(digest, Ct);
            Assert.Equal("Submitted", globalCandidate!.State); Assert.Null(globalCandidate.SignatureVerification);
        }
        // 全局候选目录中存在一条记录，不等于任何租户已经挂载或获得权益。
        using (tenant.BeginScope("a")) Assert.Empty(await Repository(host, tenant).ListAsync(TenantA, Ct));
    }

    [Fact]
    public void 摘要来源必须覆盖原始字节_跨语言清单不能接受内容替换或路径越界()
    {
        var (files, digest, _) = ArtifactFixture();
        var changed = new Dictionary<string, ReadOnlyMemory<byte>>(files) { ["说明.md"] = Encoding.UTF8.GetBytes("已被替换") };
        var mismatch = Assert.Throws<QianErrorException>(() => RegistryArtifactContent.Read(changed, digest, new SurfaceValidator(), ArtifactOptions));
        Assert.Equal("INPUT_INVALID", mismatch.Error.Code.Code); Assert.Equal("artifact.json#/digest", Assert.Single(mismatch.Error.Errors!).Pointer);
        foreach (var bad in new[] { "../escape", "/escape", "a\\escape", "a/../escape", "x:escape", ".git/config", "line\nbreak" })
        {
            var unsafeFiles = new Dictionary<string, ReadOnlyMemory<byte>>(files) { [bad] = new byte[] { 1 } };
            var error = Assert.Throws<QianErrorException>(() => RegistryArtifactContent.Read(unsafeFiles, digest, new SurfaceValidator(), ArtifactOptions));
            Assert.Equal("artifact.json#/files", Assert.Single(error.Error.Errors!).Pointer);
        }
        var conflicting = new Dictionary<string, ReadOnlyMemory<byte>>(files) { ["METADATA/roles.json"] = Encoding.UTF8.GetBytes("[]") };
        Assert.Throws<QianErrorException>(() => RegistryArtifactContent.Read(conflicting, digest, new SurfaceValidator(), ArtifactOptions));
    }

    [Fact]
    public void 完整摘要不替代静态校验_非法清单与重复元数据键仍被拒绝()
    {
        var (files, _, _) = ArtifactFixture();
        var manifest = JsonNode.Parse(files[".qian-plugin/plugin.json"].Span)!;
        manifest["policy"]!["required"] = true; // private 组件不得成为 required 平台组件。
        files[".qian-plugin/plugin.json"] = Encoding.UTF8.GetBytes(manifest.ToJsonString());
        var invalid = Assert.Throws<QianErrorException>(() => RegistryArtifactContent.Read(files, TestDigest(files), new SurfaceValidator(), ArtifactOptions));
        Assert.Contains(invalid.Error.Errors!, error => error.Pointer == ".qian-plugin/plugin.json#/policy/required");
        manifest["policy"]!["required"] = false;
        files[".qian-plugin/plugin.json"] = Encoding.UTF8.GetBytes(manifest.ToJsonString());
        files["metadata/roles.json"] = Encoding.UTF8.GetBytes("{\"secret\":\"DO-NOT-LEAK\",\"secret\":\"duplicate\"}");
        var duplicate = Assert.Throws<QianErrorException>(() => RegistryArtifactContent.Read(files, TestDigest(files), new SurfaceValidator(), ArtifactOptions));
        Assert.Contains(duplicate.Error.Errors!, error => error.Pointer.StartsWith("metadata/roles.json#", StringComparison.Ordinal));
        Assert.DoesNotContain("DO-NOT-LEAK", duplicate.ToString(), StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("./custom", "custom/")]
    [InlineData("./", "")]
    public void 元数据表面使用声明的目录并只收该层JSON文件(string directory, string prefix)
    {
        var (files, _, _) = ArtifactFixture();
        foreach (var item in files.Where(item => item.Key.StartsWith("metadata/", StringComparison.Ordinal)).ToArray())
        { files.Remove(item.Key); files[prefix + item.Key["metadata/".Length..]] = item.Value; }
        var manifest = JsonNode.Parse(files[".qian-plugin/plugin.json"].Span)!;
        manifest["surfaces"] = new JsonObject { ["metadata"] = directory };
        files[".qian-plugin/plugin.json"] = Encoding.UTF8.GetBytes(manifest.ToJsonString());
        var artifact = RegistryArtifactContent.Read(files, TestDigest(files), new SurfaceValidator(), ArtifactOptions);
        Assert.Equal(2, artifact.Metadata.Count); Assert.Contains(prefix + "roles.json", artifact.Metadata.Keys);
        manifest["surfaces"]!["metadata"] = "./" + prefix + "roles.json";
        files[".qian-plugin/plugin.json"] = Encoding.UTF8.GetBytes(manifest.ToJsonString());
        var invalid = Assert.Throws<QianErrorException>(() => RegistryArtifactContent.Read(files, TestDigest(files), new SurfaceValidator(), ArtifactOptions));
        Assert.Contains(invalid.Error.Errors!, error => error.Pointer == ".qian-plugin/plugin.json#/surfaces/metadata");
    }

    [Fact]
    public async Task 两个独立宿主同时登记同一摘要只保存一份且不覆盖首次提交主体()
    {
        var firstTenant = new AsyncLocalTenantContext(); var secondTenant = new AsyncLocalTenantContext(); var barrier = new ArtifactReadBarrier();
        using var first = Host(firstTenant, barrier); using var second = Host(secondTenant, barrier);
        await first.StartAsync(Ct); await second.StartAsync(Ct); await Prepare(first);
        var (files, digest, _) = ArtifactFixture(); var artifact = RegistryArtifactContent.Read(files, digest, new SurfaceValidator(), ArtifactOptions);
        var firstCatalog = new PostgresArtifactCatalog(first.Services.GetRequiredService<IDataService>());
        var secondCatalog = new PostgresArtifactCatalog(second.Services.GetRequiredService<IDataService>());
        var results = await Task.WhenAll(firstCatalog.SubmitAsync(artifact, "first", Ct), secondCatalog.SubmitAsync(artifact, "second", Ct));
        Assert.True(barrier.Reads >= 2); Assert.Equal(results[0].SubmittedBy, results[1].SubmittedBy); Assert.Equal(results[0].SubmittedAt, results[1].SubmittedAt);
        Assert.All(results, value => { Assert.Equal("Submitted", value.State); Assert.Null(value.SignatureVerification); });
        await using var session = await first.Services.GetRequiredService<IDataService>().OpenAsync<RegistryDbContext>(Ct);
        Assert.Single(session.Context.PluginArtifacts);
    }

    private static SurfaceValidationOptions ArtifactOptions => new() { Profile = "platform", ExecutionPlane = "server", CatalogComplete = true };
    private static (Dictionary<string, ReadOnlyMemory<byte>> Files, string Digest, JsonElement Fixture) ArtifactFixture()
    {
        var root = new DirectoryInfo(AppContext.BaseDirectory);
        while (root is not null && !File.Exists(Path.Combine(root.FullName, "Qian.slnx"))) root = root.Parent;
        var fixture = JsonSerializer.Deserialize<JsonElement>(File.ReadAllText(Path.Combine(root!.FullName, "tck/registry-artifacts/content.json")));
        var files = fixture.GetProperty("files").EnumerateObject().ToDictionary(property => property.Name,
            property => (ReadOnlyMemory<byte>)Encoding.UTF8.GetBytes(property.Value.GetString()!), StringComparer.Ordinal);
        return (files, fixture.GetProperty("digest").GetString()!, fixture);
    }
    private static string TestDigest(IReadOnlyDictionary<string, ReadOnlyMemory<byte>> files)
    {
        // 仅为负例重新计算已知格式；正例使用 CLI 生成并由两侧共同消费的固定摘要。
        var inventory = string.Concat(files.OrderBy(pair => pair.Key, StringComparer.Ordinal).Select(pair => pair.Key + "\t" +
            pair.Value.Length.ToString(System.Globalization.CultureInfo.InvariantCulture) + "\t" + Convert.ToHexStringLower(System.Security.Cryptography.SHA256.HashData(pair.Value.Span)) + "\n"));
        return "sha256:" + Convert.ToHexStringLower(System.Security.Cryptography.SHA256.HashData(Encoding.UTF8.GetBytes(inventory)));
    }
    private sealed class JsonElementComparer : IEqualityComparer<JsonElement>
    {
        internal static readonly JsonElementComparer Instance = new();
        public bool Equals(JsonElement x, JsonElement y) => JsonElement.DeepEquals(x, y);
        public int GetHashCode(JsonElement obj) => 0;
    }
    private sealed class ArtifactReadBarrier : DbCommandInterceptor
    {
        private readonly TaskCompletionSource _both = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private int _reads;
        internal int Reads => Volatile.Read(ref _reads);
        public override async ValueTask<DbDataReader> ReaderExecutedAsync(DbCommand command, CommandExecutedEventData eventData, DbDataReader result, CancellationToken cancellationToken = default)
        {
            if (command.CommandText.StartsWith("SELECT", StringComparison.Ordinal) && command.CommandText.Contains("plugin_artifacts", StringComparison.Ordinal))
            {
                if (Interlocked.Increment(ref _reads) >= 2) _both.TrySetResult();
                await _both.Task.WaitAsync(TimeSpan.FromSeconds(10), cancellationToken);
            }
            return result;
        }
    }
}

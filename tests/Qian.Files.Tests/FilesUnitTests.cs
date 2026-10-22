using System.Text;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using Qian.Runtime;
using Qian.Runtime.Errors;
using Qian.Tenancy;

namespace Qian.Files.Tests;

// 不依赖数据库的规则：服务端类型嗅探、组件配置读取、配额上限的来源顺序、注册表 seam 与 local 直传网关的签名校验。
public sealed class FilesUnitTests
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Theory]
    [InlineData("<!DOCTYPE html><p>hi</p>", "text/plain", "text/html")]
    [InlineData("  \n<HTML><body>hi</body>", "text/plain", "text/html")]
    [InlineData("notes <script>alert(1)</script>", "text/plain", "text/html")]
    [InlineData("<a href=\"javascript:alert(1)\">x</a>", "text/markdown", "text/html")]
    [InlineData("<svg xmlns=\"http://www.w3.org/2000/svg\"/>", "image/png", "image/svg+xml")]
    [InlineData("<?xml version=\"1.0\"?><svg/>", "text/xml", "image/svg+xml")]
    [InlineData("<?xml version=\"1.0\"?><note/>", "text/plain", "application/xml")]
    [InlineData("a,b\n1,2\n", "text/csv", "text/csv")]
    [InlineData("plain words", "application/pdf", "text/plain")]
    [InlineData("{\"a\":1}", "application/json", "application/json")]
    [InlineData("console.log(1)", "text/javascript", "text/javascript")]
    [InlineData("%PDF-1.7\n", "text/plain", "application/pdf")]
    [InlineData("GIF89a....", "image/png", "image/gif")]
    public void 文本与魔数_以内容为准_活跃标记一律按活跃类型(string content, string declared, string expected) =>
        Assert.Equal(expected, ContentSniffer.Detect(Encoding.UTF8.GetBytes(content), declared));

    [Fact]
    public void 二进制_按魔数识别_容器按声明细化_不认识或可执行的为octet_stream()
    {
        byte[] png = [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, 0x00];
        byte[] zip = [0x50, 0x4B, 0x03, 0x04, 0x14, 0x00];
        Assert.Equal("image/png", ContentSniffer.Detect(png, "text/html"));
        Assert.Equal("application/vnd.openxmlformats-officedocument.wordprocessingml.document",
            ContentSniffer.Detect(zip, "application/vnd.openxmlformats-officedocument.wordprocessingml.document"));
        Assert.Equal("application/zip", ContentSniffer.Detect(zip, "text/plain"));
        Assert.Equal(ContentSniffer.OctetStream, ContentSniffer.Detect([0x4D, 0x5A, 0x90, 0x00], "image/png"));
        Assert.Equal(ContentSniffer.OctetStream, ContentSniffer.Detect([0x7F, 0x45, 0x4C, 0x46, 0x02], "text/plain"));
        Assert.Equal(ContentSniffer.OctetStream, ContentSniffer.Detect([0x41, 0x00, 0x42], "text/plain"));
        // 带 BOM 的 UTF-16 同样解码后找活跃标记。
        Assert.Equal("text/html", ContentSniffer.Detect([0xFF, 0xFE, .. Encoding.Unicode.GetBytes("<html><body>")], "text/plain"));
        // 头部截断在多字节字符中间仍按文本处理。
        var chinese = Encoding.UTF8.GetBytes("中文内容");
        Assert.Equal("text/plain", ContentSniffer.Detect(chinese.AsSpan(0, chinese.Length - 1), "text/plain"));
        Assert.Equal((".png", ".html", ""), (ContentSniffer.Extension("image/png"), ContentSniffer.Extension("text/html"), ContentSniffer.Extension("application/x-unknown")));
        Assert.Equal(("text/plain", null), (ContentSniffer.Normalize(" Text/Plain; charset=utf-8"), ContentSniffer.Normalize("text/plain/extra")));
    }

    [Fact]
    public void 组件配置_缺省10GiB配额_策略逐项校验_未知字段一律拒绝()
    {
        var minimal = Read("""{"storage":{"kind":"local","root":"/srv/files"}}""");
        Assert.Equal(("local", FilesConfiguration.DefaultQuotaBytes, 0), (minimal.Kind, minimal.QuotaPerTenantBytes, minimal.Policies.Count));
        Assert.Equal(10L * 1024 * 1024 * 1024, FilesConfiguration.DefaultQuotaBytes);
        var policies = Read("""
            {"storage":{"kind":"local"},"quota":{"perTenantBytes":0},
             "policies":[{"purpose":"acme.crm/attachment","allowedTypes":["text/*","image/png"],"maxSize":10,"maxCount":3}]}
            """);
        Assert.Equal(0L, policies.QuotaPerTenantBytes);
        var policy = Assert.Single(policies.Policies);
        Assert.Equal(("acme.crm/attachment", 10L, (int?)3), (policy.Purpose, policy.MaxSize, policy.MaxCount));
        Assert.Equal(["text/*", "image/png"], policy.AllowedTypes);
        Assert.Equal("acme.crm", FilesConfiguration.OwnerOf(policy.Purpose));
        foreach (var (config, pointer) in new[]
        {
            ("""[]""", "#"),
            ("""{}""", "#/storage"),
            ("""{"storage":{}}""", "#/storage/kind"),
            ("""{"storage":{"kind":"local"},"quota":{"perTenantBytes":-1}}""", "#/quota/perTenantBytes"),
            ("""{"storage":{"kind":"local"},"policies":[{"purpose":"Acme/x","allowedTypes":["text/plain"],"maxSize":1}]}""", "#/policies/0"),
            ("""{"storage":{"kind":"local"},"policies":[{"purpose":"nodot/x","allowedTypes":["text/plain"],"maxSize":1}]}""", "#/policies/0"),
            ("""{"storage":{"kind":"local"},"policies":[{"purpose":"acme.crm/x","allowedTypes":[],"maxSize":1}]}""", "#/policies/0"),
            ("""{"storage":{"kind":"local"},"policies":[{"purpose":"acme.crm/x","allowedTypes":["*/*"],"maxSize":1}]}""", "#/policies/0"),
            ("""{"storage":{"kind":"local"},"policies":[{"purpose":"acme.crm/x","allowedTypes":["text/plain"],"maxSize":0}]}""", "#/policies/0"),
            ("""{"storage":{"kind":"local"},"policies":[{"purpose":"acme.crm/x","allowedTypes":["text/plain"],"maxSize":1,"inline":true}]}""", "#/policies/0"),
            ("""{"storage":{"kind":"local"},"policies":[{"purpose":"acme.crm/x","allowedTypes":["text/plain"],"maxSize":1},{"purpose":"acme.crm/x","allowedTypes":["text/plain"],"maxSize":2}]}""", "#/policies/1")
        })
        {
            var error = Assert.Throws<QianErrorException>(() => Read(config));
            Assert.Equal((PlatformErrors.InputInvalid, pointer), (error.Error.Code, error.Error.Pointer));
        }
    }

    [Fact]
    public async Task 配额上限_租户配置覆盖优先_租户配置不可用或缺省时回退组件配置()
    {
        var reads = new List<(string, string)>();
        object? value = 42L; Exception? failure = null;
        ITenantSettings? settings = new StubSettings((plugin, key) =>
        {
            reads.Add((plugin, key));
            return failure is null ? value : throw failure;
        });
        var runtime = new FilesRuntime(TimeProvider.System, new FileBackendRegistry(), new UploadPolicyRegistry(), 100, () => settings);
        Assert.Equal(42L, await runtime.QuotaLimitAsync(Ct));
        Assert.Equal(("qian.files", "quota.perTenantBytes"), Assert.Single(reads));
        value = null;
        Assert.Equal(100L, await runtime.QuotaLimitAsync(Ct));
        // 宿主没有 Registry（参考宿主、独立模式）或 Registry 里没有 qian.files 条目：回退组件配置，而不是让上传失败。
        foreach (var unavailable in new Exception[] { new ServiceNotReadyException(), new KeyNotFoundException(), new QianErrorException(new QianError(PlatformErrors.NotFound)) })
        {
            failure = unavailable;
            Assert.Equal(100L, await runtime.QuotaLimitAsync(Ct));
        }
        // 其余错误（例如租户上下文无效）照常抛出，不被静默吞掉。
        failure = new QianErrorException(new QianError(PlatformErrors.Unauthenticated));
        Assert.Same(failure, await Assert.ThrowsAsync<QianErrorException>(async () => await runtime.QuotaLimitAsync(Ct)));
        settings = null;
        Assert.Equal(100L, await runtime.QuotaLimitAsync(Ct));
    }

    [Fact]
    public void 注册表_同名独占冲突报REGISTRATION_CONFLICT_释放后可重登_释放幂等()
    {
        var policies = new UploadPolicyRegistry();
        var policy = new UploadPolicy("acme.crm/attachment", ["text/plain"], 10);
        var lease = policies.Register(policy.Purpose, policy);
        var conflict = Assert.Throws<QianErrorException>(() => policies.Register(policy.Purpose, policy with { MaxSize = 20 }));
        Assert.Equal(PlatformErrors.RegistrationConflict, conflict.Error.Code);
        Assert.Equal(new Dictionary<string, string> { ["key"] = "Files.Policies:acme.crm/attachment", ["owner"] = "qian.files" }, conflict.Error.Args);
        Assert.Equal("#/purpose", Assert.Throws<QianErrorException>(() => policies.Register("acme.crm/other", policy)).Error.Pointer);
        lease.Dispose(); lease.Dispose();
        Assert.Null(policies.Find(policy.Purpose));
        using (policies.Register(policy.Purpose, policy with { MaxSize = 20 })) Assert.Equal(20, policies.Find(policy.Purpose)!.MaxSize);

        var backends = new FileBackendRegistry();
        using var local = backends.Register("local", new LocalFileBackendFactory(default));
        Assert.Equal(new Dictionary<string, string> { ["key"] = "Files.Backends:local", ["owner"] = "qian.files" },
            Assert.Throws<QianErrorException>(() => backends.Register("local", new LocalFileBackendFactory(default))).Error.Args);
        Assert.Equal("#/name", Assert.Throws<QianErrorException>(() => backends.Register("Local", new LocalFileBackendFactory(default))).Error.Pointer);
        Assert.Equal(["local"], backends.List());
    }

    [Fact]
    public async Task local直传网关_签名覆盖键与参数_过期越界与暂存键下载一律拒绝()
    {
        var clock = new ManualClock(DateTimeOffset.UtcNow);
        var root = Directory.CreateTempSubdirectory("qian-files-unit-").FullName;
        try
        {
            using var storage = JsonDocument.Parse(JsonSerializer.Serialize(new { kind = "local", root, endpoint = "http://127.0.0.1:5000/files/blobs" }));
            var backend = new LocalFileBackendFactory(new byte[32]).Create(new FileBackendContext(storage.RootElement, clock, new ServiceCollection().BuildServiceProvider()));
            const string Key = "tenants/t1/acme.crm/2026/10/a.upload";
            var upload = await backend.CreateUploadAsync(Key, "text/plain", 3, TimeSpan.FromMinutes(15), Ct);
            var later = await backend.CreateUploadAsync("tenants/t1/acme.crm/2026/10/b.upload", "text/plain", 3, TimeSpan.FromMinutes(15), Ct);
            Assert.Equal("http://127.0.0.1:5000/files/blobs/" + Key, upload.Url.GetLeftPart(UriPartial.Path));
            var gateway = backend.Transfer!;
            var query = Microsoft.AspNetCore.WebUtilities.QueryHelpers.ParseQuery(upload.Url.Query).ToDictionary(pair => pair.Key, pair => pair.Value.ToString());
            var headers = new Dictionary<string, string>(upload.Headers, StringComparer.OrdinalIgnoreCase);
            async Task<FileTransferResult> Put(string key, IReadOnlyDictionary<string, string> values, IReadOnlyDictionary<string, string> head, string body = "abc") =>
                await gateway.ReceiveAsync(new FileTransferRequest(key, values, head), new MemoryStream(Encoding.UTF8.GetBytes(body)), Ct);

            Assert.Equal(FileTransferResult.Rejected, await Put("tenants/t2/acme.crm/2026/10/a.upload", query, headers));
            Assert.Equal(FileTransferResult.Rejected, await Put("tenants/t1/../t2/a.upload", query, headers));
            Assert.Equal(FileTransferResult.Rejected, await Put("other/a.upload", query, headers));
            Assert.Equal(FileTransferResult.Rejected, await Put(Key, new Dictionary<string, string>(query) { ["contentType"] = "text/html" }, headers));
            Assert.Equal(FileTransferResult.Rejected, await Put(Key, query, new Dictionary<string, string>(headers) { ["X-Kun-Max-Bytes"] = "4096" }));
            Assert.Equal(FileTransferResult.TooLarge, await Put(Key, query, headers, "abcd"));
            Assert.False(File.Exists(Path.Combine(root, Key)));
            Assert.Equal(FileTransferResult.Stored, await Put(Key, query, headers));
            Assert.Equal(FileTransferResult.Conflict, await Put(Key, query, headers));
            clock.Advance(TimeSpan.FromMinutes(16));
            Assert.Equal(FileTransferResult.Rejected, await Put("tenants/t1/acme.crm/2026/10/b.upload",
                Microsoft.AspNetCore.WebUtilities.QueryHelpers.ParseQuery(later.Url.Query).ToDictionary(pair => pair.Key, pair => pair.Value.ToString()),
                new Dictionary<string, string>(later.Headers, StringComparer.OrdinalIgnoreCase)));
            Assert.False(File.Exists(Path.Combine(root, "tenants/t1/acme.crm/2026/10/b.upload")));

            // 下载只签正式键：暂存键不能签发；签发的地址恒为附件，改处置参数即失效。
            await Assert.ThrowsAsync<ArgumentException>(async () => await backend.CreateDownloadAsync(Key, "a.txt", TimeSpan.FromMinutes(5), Ct));
            await backend.WriteAsync("tenants/t1/acme.crm/2026/10/a.txt", new MemoryStream("abc"u8.ToArray()), "text/plain", 3, Ct);
            var download = await backend.CreateDownloadAsync("tenants/t1/acme.crm/2026/10/a.txt", "a.txt", TimeSpan.FromMinutes(5), Ct);
            var signed = Microsoft.AspNetCore.WebUtilities.QueryHelpers.ParseQuery(download.Url.Query).ToDictionary(pair => pair.Key, pair => pair.Value.ToString());
            Assert.StartsWith("attachment;", signed["disposition"], StringComparison.Ordinal);
            var opened = await gateway.OpenAsync(new FileTransferRequest("tenants/t1/acme.crm/2026/10/a.txt", signed, new Dictionary<string, string>()), Ct);
            Assert.NotNull(opened);
            await using (opened.Content) Assert.Equal((3L, "text/plain", signed["disposition"]), (opened.Length, opened.ContentType, opened.ContentDisposition));
            Assert.Null(await gateway.OpenAsync(new FileTransferRequest("tenants/t1/acme.crm/2026/10/a.txt",
                new Dictionary<string, string>(signed) { ["disposition"] = "inline" }, new Dictionary<string, string>()), Ct));
            Assert.Null(await gateway.OpenAsync(new FileTransferRequest(Key, signed, new Dictionary<string, string>()), Ct));
        }
        finally { Directory.Delete(root, recursive: true); }
    }

    private static FilesSettings Read(string json)
    {
        using var document = JsonDocument.Parse(json);
        return FilesConfiguration.Read(document.RootElement);
    }

    private sealed class StubSettings(Func<string, string, object?> read) : ITenantSettings
    {
        public ValueTask<T?> Get<T>(string pluginId, string key, CancellationToken cancellationToken = default) => ValueTask.FromResult((T?)read(pluginId, key));
    }
}

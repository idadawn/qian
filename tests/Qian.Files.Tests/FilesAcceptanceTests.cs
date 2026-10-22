using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Kun.Authorization;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Qian.Authz.Abstractions;
using Qian.Plugin.Sdk.AspNetCore;
using Qian.Runtime.Errors;
using Qian.Testing.Containers;

namespace Qian.Files.Tests;

// S1-FILE-001 验收：全部经真实宿主（S2S 令牌、生产模式租户解析与 HTTP PEP、命令管道的租户 / 授权 / 审计、受限业务登录与 RLS）走 HTTP。
public sealed partial class FilesAcceptanceTests(PostgresFixture postgres) : IClassFixture<PostgresFixture>
{
    private const int MiB = 1024 * 1024;
    private static readonly string[] Capabilities = [FilesCapabilities.CompleteUpload, FilesCapabilities.Delete, FilesCapabilities.DownloadUrl, FilesCapabilities.Get,
        FilesCapabilities.PresignUpload, FilesCapabilities.Quota];
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task 验收1_租户A对B的文件ID一律404_存储键以租户前缀开头_受限角色只见本租户行_PEP按租户角色判定()
    {
        await using var host = await FilesHost.StartAsync(postgres, ct: Ct);
        var mine = await host.Upload(FilesHost.A, Text("tenant a file"));
        var theirs = await host.Upload(FilesHost.B, Text("tenant b file"));
        // A 的主体是 tenant.owner，PEP 放行读、写、删；B 的文件对 A 由租户过滤与 RLS 判为不存在，下载地址、元数据、完成与删除一律 404。
        foreach (var (method, path) in new[]
        {
            (HttpMethod.Get, "/api/files/" + theirs.Id + "/download-url"), (HttpMethod.Get, "/api/files/" + theirs.Id),
            (HttpMethod.Post, "/api/files/" + theirs.Id + ":complete"), (HttpMethod.Delete, "/api/files/" + theirs.Id)
        })
        {
            using var response = await host.Send(FilesHost.A, method, path);
            await AssertError(response, HttpStatusCode.NotFound, "NOT_FOUND");
        }
        using (var own = await host.Send(FilesHost.B, HttpMethod.Get, "/api/files/" + theirs.Id + "/download-url"))
            Assert.True(own.StatusCode == HttpStatusCode.OK, await own.Content.ReadAsStringAsync(Ct));

        // 存储键由服务端生成：tenants/{tenant_id}/{plugin_id}/{yyyy}/{MM}/{uuid}{ext}；上传暂存键在同一目录。磁盘上的对象同样只在各自租户目录下。
        var rows = await host.AdminRows($"SELECT tenant_id, storage_key, upload_key, plugin_id FROM {FilesDbContext.Schema}.file_objects");
        Assert.Equal(2, rows.Count);
        Assert.All(rows, row =>
        {
            Assert.StartsWith("tenants/" + row[0] + "/", row[1], StringComparison.Ordinal);
            Assert.StartsWith("tenants/" + row[0] + "/", row[2], StringComparison.Ordinal);
            Assert.Equal("acme.crm", row[3]);
        });
        var month = host.Clock.GetUtcNow().UtcDateTime;
        Assert.Equal($"tenants/{FilesHost.A}/acme.crm/{month:yyyy}/{month:MM}/{mine.Id:D}.txt", mine.StorageKey);
        var objects = Directory.EnumerateFiles(host.StorageRoot, "*", SearchOption.AllDirectories)
            .Select(path => Path.GetRelativePath(host.StorageRoot, path).Replace('\\', '/')).ToArray();
        Assert.NotEmpty(objects);
        Assert.All(objects, path => Assert.True(path.StartsWith("tenants/" + FilesHost.A + "/", StringComparison.Ordinal) ||
            path.StartsWith("tenants/" + FilesHost.B + "/", StringComparison.Ordinal), path));

        // 绕过 EF 与应用层过滤，以受限业务登录（NOSUPERUSER / NOBYPASSRLS）直连：会话租户不同的行看不到，未设置租户时一行也看不到。
        Assert.Equal((1L, 0L), (await host.RestrictedCount(FilesHost.A, mine.Id), await host.RestrictedCount(FilesHost.B, mine.Id)));
        Assert.Equal((0L, 1L), (await host.RestrictedCount(FilesHost.A, theirs.Id), await host.RestrictedCount(FilesHost.B, theirs.Id)));
        Assert.Equal(0L, await host.RestrictedCount("", mine.Id));

        // 生产模式 HTTP PEP（宿主回调 RequireQianPermission）：同一主体在 B 只是 tenant.user，删除被拒；没有角色绑定的主体连申请都被拒；拒绝必记决策审计。
        using (var denied = await host.Send(FilesHost.B, HttpMethod.Delete, "/api/files/" + theirs.Id))
            await AssertError(denied, HttpStatusCode.Forbidden, "AUTHZ_DENIED");
        using (var stranger = await host.Presign(FilesHost.A, FilesHost.Request(Text("stranger")), subject: "files-unbound-client"))
            await AssertError(stranger, HttpStatusCode.Forbidden, "AUTHZ_DENIED");
        var decisions = await host.AdminRows("SELECT tenant_id, actor_id, result, payload->>'permission' FROM plugin_qian_audit.audit_entries " +
            "WHERE result = 'denied' ORDER BY occurred_at, seq");
        Assert.Equal([[FilesHost.B, FilesHost.Subject, "denied", FilesPermissions.Delete], [FilesHost.A, "files-unbound-client", "denied", FilesPermissions.Write]], decisions);
        using (var still = await host.Send(FilesHost.B, HttpMethod.Get, "/api/files/" + theirs.Id))
        {
            Assert.Equal(HttpStatusCode.OK, still.StatusCode);
            Assert.Equal(FileStatus.Available, (await still.Content.ReadFromJsonAsync(FilesContractJson.FileDescriptorTypeInfo, Ct))!.Status);
        }

        // 每个 /api/files 端点都经宿主回调接了 PEP（权限元数据 + 授权要求）并绑定到对应能力；直传端点显式匿名、签名即授权。
        var endpoints = host.Services.GetRequiredService<EndpointDataSource>().Endpoints.OfType<RouteEndpoint>().ToArray();
        var api = endpoints.Where(endpoint => endpoint.RoutePattern.RawText!.StartsWith("/api/files", StringComparison.Ordinal)).ToArray();
        Assert.Equal(6, api.Length);
        Assert.Equal(Capabilities, api.Select(endpoint => Assert.Single(endpoint.Metadata.OfType<PluginEndpointBinding>()).Name).Order(StringComparer.Ordinal));
        Assert.All(api, endpoint =>
        {
            var binding = Assert.Single(endpoint.Metadata.OfType<PluginEndpointBinding>());
            Assert.Equal(("files", "qian.files"), (binding.EntryId, binding.PluginId));
            Assert.StartsWith("qian.files:file:", Assert.Single(endpoint.Metadata.OfType<RequiresPermissionAttribute>()).Permission, StringComparison.Ordinal);
            Assert.NotEmpty(endpoint.Metadata.OfType<IAuthorizeData>());
        });
        var transfer = endpoints.Where(endpoint => endpoint.RoutePattern.RawText!.StartsWith("/files/blobs", StringComparison.Ordinal)).ToArray();
        Assert.Equal(2, transfer.Length);
        Assert.All(transfer, endpoint =>
        {
            Assert.NotNull(endpoint.Metadata.GetMetadata<IAllowAnonymous>());
            Assert.NotNull(endpoint.Metadata.GetMetadata<PermissionEnforcedInHandlerAttribute>());
        });
    }

    [Fact]
    public async Task 验收2_上传text_html后下载恒为附件且禁止嗅探_改处置参数即失效_配置与宿主选项没有处置开关()
    {
        await using var host = await FilesHost.StartAsync(postgres, ct: Ct);
        var html = Encoding.UTF8.GetBytes("<!DOCTYPE html><html><body><script>alert(document.domain)</script></body></html>");
        var file = await host.Upload(FilesHost.A, html, "text/html", "page.html");
        Assert.Equal(("text/html", FileStatus.Available), (file.ContentType, file.Status));
        Assert.EndsWith(".html", file.StorageKey, StringComparison.Ordinal);
        var ticket = await Ticket(host, FilesHost.A, file.Id);
        Assert.InRange(ticket.ExpiresAt - host.Clock.GetUtcNow(), TimeSpan.FromSeconds(1), TimeSpan.FromMinutes(5));
        using (var download = await host.Client.GetAsync(ticket.Url, Ct))
        {
            Assert.Equal(HttpStatusCode.OK, download.StatusCode);
            var disposition = download.Content.Headers.ContentDisposition;
            Assert.NotNull(disposition);
            Assert.Equal("attachment", disposition.DispositionType);
            Assert.Equal("page.html", disposition.FileName?.Trim('"'));
            Assert.Equal("nosniff", Assert.Single(download.Headers.GetValues("X-Content-Type-Options")));
            Assert.Equal("sandbox", Assert.Single(download.Headers.GetValues("Content-Security-Policy")));
            Assert.Equal(html, await download.Content.ReadAsByteArrayAsync(Ct));
        }
        // 签名覆盖处置参数：改成 inline 或删掉都与不存在同为 404，客户端改不了下载方式。
        foreach (var tampered in new[] { WithQuery(ticket.Url, "disposition", "inline"), WithQuery(ticket.Url, "disposition", null) })
        {
            using var response = await host.Client.GetAsync(tampered, Ct);
            await AssertError(response, HttpStatusCode.NotFound, "NOT_FOUND");
        }

        // 没有任何配置项能改变：组件配置 Schema 各层都封闭且没有处置类属性，读取器拒绝未知字段，宿主选项与端点选项也没有对应属性。
        using var schema = JsonDocument.Parse(File.ReadAllText(Path.Combine(FilesMetadata.RepositoryRoot(), "plugins/qian.files/schemas/config.schema.json")));
        var names = PropertyNames(schema.RootElement).ToArray();
        Assert.Contains("perTenantBytes", names);
        Assert.DoesNotContain(names, Switch);
        var properties = schema.RootElement.GetProperty("properties");
        Assert.All(new[] { schema.RootElement, properties.GetProperty("storage"), properties.GetProperty("quota"), properties.GetProperty("policies").GetProperty("items") },
            node => Assert.False(node.GetProperty("additionalProperties").GetBoolean()));
        foreach (var (config, pointer) in new[]
        {
            ("""{"storage":{"kind":"local"},"disposition":"inline"}""", "#/disposition"),
            ("""{"storage":{"kind":"local"},"quota":{"perTenantBytes":1,"inline":true}}""", "#/quota/inline")
        })
        {
            using var document = JsonDocument.Parse(config);
            var error = Assert.Throws<QianErrorException>(() => FilesConfiguration.Read(document.RootElement));
            Assert.Equal((PlatformErrors.InputInvalid, pointer), (error.Error.Code, error.Error.Pointer));
        }
        // storage 的成员由选用的后端在挂载时校验：local 后端同样拒绝未知字段。
        using (var storage = JsonDocument.Parse("""{"kind":"local","root":"/srv/files","endpoint":"http://127.0.0.1/files/blobs/","forceDownload":false}"""))
        {
            var error = Assert.Throws<QianErrorException>(() =>
                new LocalFileBackendFactory(default).Create(new FileBackendContext(storage.RootElement, TimeProvider.System, host.Services)));
            Assert.Equal((PlatformErrors.InputInvalid, "#/storage/forceDownload"), (error.Error.Code, error.Error.Pointer));
        }
        Assert.DoesNotContain(new[] { typeof(FilesSettings), typeof(QianFilesOptions), typeof(AspNetCore.FilesEndpointOptions) }.SelectMany(type => type.GetProperties()),
            property => Switch(property.Name));
    }

    [Fact]
    public async Task 验收3_10MiB配额下第11个MiB返回QUOTA_EXCEEDED_软删释放配额_配额按租户计()
    {
        await using var host = await FilesHost.StartAsync(postgres, quota: 10L * MiB, ct: Ct);
        var files = new List<FileDescriptor>();
        for (var index = 0; index < 10; index++) files.Add(await host.Upload(FilesHost.A, Text("chunk " + index, MiB)));
        Assert.Equal(new FileQuota(10L * MiB, 10L * MiB, 10), await Quota(host, FilesHost.A));

        // 第 11 个 MiB（以及哪怕 1 字节）都超出配额：422 QUOTA_EXCEEDED，指向 size，剩余 0 字节。
        foreach (var size in new[] { MiB, 1 })
        {
            using var exceeded = await host.Presign(FilesHost.A, FilesHost.Request(Text("eleventh", size)));
            var error = await AssertError(exceeded, HttpStatusCode.UnprocessableEntity, "QUOTA_EXCEEDED");
            Assert.Equal("#/size", error.GetProperty("pointer").GetString());
            Assert.Equal("文件大小超出租户配额（剩余 0 字节）。", error.GetProperty("message").GetString());
        }
        // 配额按租户计：B 不受 A 用量影响。
        using (var other = await host.Presign(FilesHost.B, FilesHost.Request(Text("tenant b", MiB))))
            Assert.Equal(HttpStatusCode.Created, other.StatusCode);

        // 软删立即释放配额：已删文件不再可见，再次删除 404（不重复产生副作用），释放出的 1 MiB 可以再申请。
        using (var deleted = await host.Send(FilesHost.A, HttpMethod.Delete, "/api/files/" + files[0].Id))
        {
            Assert.True(deleted.StatusCode == HttpStatusCode.OK, await deleted.Content.ReadAsStringAsync(Ct));
            var descriptor = (await deleted.Content.ReadFromJsonAsync(FilesContractJson.FileDescriptorTypeInfo, Ct))!;
            Assert.Equal(FileStatus.Deleted, descriptor.Status);
            Assert.NotNull(descriptor.DeletedAt);
        }
        Assert.Equal(new FileQuota(10L * MiB, 9L * MiB, 9), await Quota(host, FilesHost.A));
        using (var again = await host.Send(FilesHost.A, HttpMethod.Delete, "/api/files/" + files[0].Id))
            await AssertError(again, HttpStatusCode.NotFound, "NOT_FOUND");
        using (var gone = await host.Send(FilesHost.A, HttpMethod.Get, "/api/files/" + files[0].Id + "/download-url"))
            await AssertError(gone, HttpStatusCode.NotFound, "NOT_FOUND");
        using (var reused = await host.Presign(FilesHost.A, FilesHost.Request(Text("after delete", MiB))))
            Assert.True(reused.StatusCode == HttpStatusCode.Created, await reused.Content.ReadAsStringAsync(Ct));
        using (var full = await host.Presign(FilesHost.A, FilesHost.Request(Text("full again", 1))))
            await AssertError(full, HttpStatusCode.UnprocessableEntity, "QUOTA_EXCEEDED");
        Assert.Equal("deleted", Assert.Single(await host.AdminRows($"SELECT status FROM {FilesDbContext.Schema}.file_objects WHERE id = @id AND is_deleted",
            ("id", files[0].Id)))[0]);
    }

    [Fact]
    public async Task 验收4_直传地址过期后PUT失败_完成时SHA256不符返回错误并标记隔离()
    {
        await using var host = await FilesHost.StartAsync(postgres, ct: Ct);
        // 过期：地址不超过 15 分钟；过期后的 PUT 与不存在同为 404，暂存对象没有写入。完成时限内完成报未上传，过了时限报过期，预占不再计入配额。
        var content = Text("expiring upload");
        var upload = await Presigned(host, FilesHost.A, FilesHost.Request(content));
        Assert.InRange(upload.ExpiresAt - host.Clock.GetUtcNow(), TimeSpan.FromMinutes(14), TimeSpan.FromMinutes(15));
        Assert.Equal(15L, (await Quota(host, FilesHost.A)).UsedBytes);
        host.Clock.Advance(TimeSpan.FromMinutes(15) + TimeSpan.FromSeconds(1));
        using (var late = await host.Put(upload, content)) await AssertError(late, HttpStatusCode.NotFound, "NOT_FOUND");
        var staging = Assert.Single(await host.AdminRows($"SELECT upload_key FROM {FilesDbContext.Schema}.file_objects WHERE id = @id", ("id", upload.FileId)))[0];
        Assert.False(File.Exists(Path.Combine(host.StorageRoot, staging)));
        using (var incomplete = await host.Send(FilesHost.A, HttpMethod.Post, "/api/files/" + upload.FileId + ":complete"))
            await AssertError(incomplete, HttpStatusCode.Conflict, "FILE_UPLOAD_INCOMPLETE");
        host.Clock.Advance(TimeSpan.FromMinutes(10));
        using (var expired = await host.Send(FilesHost.A, HttpMethod.Post, "/api/files/" + upload.FileId + ":complete"))
            await AssertError(expired, HttpStatusCode.Conflict, "FILE_UPLOAD_EXPIRED");
        Assert.Equal(0L, (await Quota(host, FilesHost.A)).UsedBytes);

        // SHA-256 不符：直传本身成功，complete 由服务端自算哈希，返回 422 并把文件标记为隔离；重复完成给同一错误，隔离文件不能签发下载地址。
        var declared = Text("declared content"); var actual = Text("tampered content");
        Assert.Equal(declared.Length, actual.Length);
        var mismatch = await Presigned(host, FilesHost.A, FilesHost.Request(declared));
        using (var put = await host.Put(mismatch, actual)) Assert.Equal(HttpStatusCode.Created, put.StatusCode);
        for (var attempt = 0; attempt < 2; attempt++)
        {
            using var completed = await host.Send(FilesHost.A, HttpMethod.Post, "/api/files/" + mismatch.FileId + ":complete");
            await AssertError(completed, HttpStatusCode.UnprocessableEntity, "FILE_CHECKSUM_MISMATCH");
        }
        using (var get = await host.Send(FilesHost.A, HttpMethod.Get, "/api/files/" + mismatch.FileId))
        {
            var file = (await get.Content.ReadFromJsonAsync(FilesContractJson.FileDescriptorTypeInfo, Ct))!;
            Assert.Equal((FileStatus.Quarantined, FileQuarantineReason.ChecksumMismatch), (file.Status, file.QuarantineReason));
            // 正式对象已删除，暂存对象留作证据（保留期清理归 S1-FILE-003）。
            Assert.False(File.Exists(Path.Combine(host.StorageRoot, file.StorageKey)));
        }
        var evidence = Assert.Single(await host.AdminRows($"SELECT upload_key FROM {FilesDbContext.Schema}.file_objects WHERE id = @id", ("id", mismatch.FileId)))[0];
        Assert.Equal(actual, await File.ReadAllBytesAsync(Path.Combine(host.StorageRoot, evidence), Ct));
        using (var ticket = await host.Send(FilesHost.A, HttpMethod.Get, "/api/files/" + mismatch.FileId + "/download-url"))
            Assert.Contains(FileStatus.Quarantined, (await AssertError(ticket, HttpStatusCode.Conflict, "FILE_NOT_AVAILABLE")).GetProperty("message").GetString(), StringComparison.Ordinal);
        // 隔离文件按声明大小计入配额（README 口径：暂存字节仍占存储），删除后释放；隔离文件不发 files.object.created.v1。
        Assert.Equal(16L, (await Quota(host, FilesHost.A)).UsedBytes);
        using (var removed = await host.Send(FilesHost.A, HttpMethod.Delete, "/api/files/" + mismatch.FileId)) Assert.Equal(HttpStatusCode.OK, removed.StatusCode);
        Assert.Equal(0L, (await Quota(host, FilesHost.A)).UsedBytes);
        Assert.Empty(await host.AdminRows($"SELECT id FROM {FilesDbContext.Schema}.outbox_messages"));
    }

    [Fact]
    public async Task 验收5_申请直传写审计_完成发files_object_created_v1_下载与删除记审计_删除为destructive且需确认()
    {
        await using var host = await FilesHost.StartAsync(postgres, ct: Ct);
        var content = Text("audited content");
        var upload = await Presigned(host, FilesHost.A, FilesHost.Request(content));
        Assert.Equal([["qian.files/presign-upload", "service", FilesHost.Subject, "success", "api", FilesHost.A]], await Audit(host));

        using (var put = await host.Put(upload, content)) Assert.Equal(HttpStatusCode.Created, put.StatusCode);
        using (var completed = await host.Send(FilesHost.A, HttpMethod.Post, "/api/files/" + upload.FileId + ":complete"))
            Assert.Equal(HttpStatusCode.OK, completed.StatusCode);
        var created = Assert.Single(await host.AdminRows($"SELECT event_type_id, tenant_id, payload FROM {FilesDbContext.Schema}.outbox_messages"));
        Assert.Equal((FilesEventIds.ObjectCreated, FilesHost.A), (created[0], created[1]));
        // Outbox 行即 v1 信封：类型、租户、生产方与行事主体取自本次能力调用，事件体在 payload 里。
        using var envelope = JsonDocument.Parse(created[2]);
        Assert.Equal((FilesEventIds.ObjectCreated, FilesHost.A, "qian.files", "service", FilesHost.Subject), (envelope.RootElement.GetProperty("type").GetString(),
            envelope.RootElement.GetProperty("tenant_id").GetString(), envelope.RootElement.GetProperty("producer").GetString(),
            envelope.RootElement.GetProperty("actor").GetProperty("kind").GetString(), envelope.RootElement.GetProperty("actor").GetProperty("id").GetString()));
        var data = envelope.RootElement.GetProperty("payload");
        Assert.Equal(upload.FileId, data.GetProperty("fileId").GetGuid());
        Assert.Equal(("acme.crm", FilesHost.Purpose, 15L, "text/plain"), (data.GetProperty("pluginId").GetString(), data.GetProperty("purpose").GetString(),
            data.GetProperty("size").GetInt64(), data.GetProperty("contentType").GetString()));

        _ = await Ticket(host, FilesHost.A, upload.FileId);
        using (var deleted = await host.Send(FilesHost.A, HttpMethod.Delete, "/api/files/" + upload.FileId)) Assert.Equal(HttpStatusCode.OK, deleted.StatusCode);
        Assert.Equal(["qian.files/presign-upload", "qian.files/complete-upload", "qian.files/download-url", "qian.files/delete"], (await Audit(host)).Select(row => row[0]));
        var events = await host.AdminRows($"SELECT event_type_id FROM {FilesDbContext.Schema}.outbox_messages ORDER BY occurred_at");
        Assert.Equal([FilesEventIds.ObjectCreated, FilesEventIds.ObjectDeleted], events.Select(row => row[0]));

        // 契约：能力 ID 按 R23（<组件 ID>/<名称>）；删除是 destructive、策略提示 confirm、Agent 调用需确认；下载地址是只读能力但每次签发都审计。
        var contracts = new[] { PresignUploadCommand.QianCapability, CompleteUploadCommand.QianCapability, GetFileQuery.QianCapability, DownloadUrlQuery.QianCapability,
            DeleteFileCommand.QianCapability, QuotaQuery.QianCapability };
        Assert.Equal(Capabilities, contracts.Select(contract => contract.Id).Order(StringComparer.Ordinal));
        var delete = DeleteFileCommand.QianCapability.Metadata;
        Assert.Equal(("destructive", "confirm", true, true, FilesPermissions.Delete), (delete.GetProperty("sideEffect").GetString(), delete.GetProperty("policyHint").GetString(),
            delete.GetProperty("agent").GetProperty("requiresConfirmation").GetBoolean(), delete.GetProperty("audit").GetBoolean(), delete.GetProperty("requiredPermission").GetString()));
        var download = DownloadUrlQuery.QianCapability.Metadata;
        Assert.Equal(("read", true), (download.GetProperty("sideEffect").GetString(), download.GetProperty("audit").GetBoolean()));
        using var tool = JsonDocument.Parse(File.ReadAllText(Path.Combine(FilesMetadata.RepositoryRoot(), "plugins/qian.files/tools/delete.json")));
        Assert.Equal(("destructive", "confirm", true, "qian.files/delete"), (tool.RootElement.GetProperty("sideEffect").GetString(), tool.RootElement.GetProperty("policyHint").GetString(),
            tool.RootElement.GetProperty("agent").GetProperty("requiresConfirmation").GetBoolean(), tool.RootElement.GetProperty("binding").GetProperty("id").GetString()));
    }

    internal static byte[] Text(string seed, int length = 0)
    {
        if (length == 0) return Encoding.UTF8.GetBytes(seed);
        var builder = new StringBuilder(length + 64);
        while (builder.Length < length) builder.Append(seed).Append(' ').Append(builder.Length).Append('\n');
        return Encoding.ASCII.GetBytes(builder.ToString(0, length));
    }

    internal static async Task<JsonElement> AssertError(HttpResponseMessage response, HttpStatusCode status, string code)
    {
        var body = await response.Content.ReadAsStringAsync(Ct);
        Assert.True(response.StatusCode == status, (int)response.StatusCode + " " + body);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        using var document = JsonDocument.Parse(body);
        Assert.Equal(code, document.RootElement.GetProperty("code").GetString());
        return document.RootElement.Clone();
    }

    internal static async Task<PresignedUpload> Presigned(FilesHost host, string tenant, PresignUploadRequest request)
    {
        using var response = await host.Presign(tenant, request);
        Assert.True(response.StatusCode == HttpStatusCode.Created, await response.Content.ReadAsStringAsync(Ct));
        return (await response.Content.ReadFromJsonAsync(FilesContractJson.PresignedUploadTypeInfo, Ct))!;
    }

    internal static async Task<DownloadTicket> Ticket(FilesHost host, string tenant, Guid id)
    {
        using var response = await host.Send(tenant, HttpMethod.Get, "/api/files/" + id + "/download-url");
        Assert.True(response.StatusCode == HttpStatusCode.OK, await response.Content.ReadAsStringAsync(Ct));
        return (await response.Content.ReadFromJsonAsync(FilesContractJson.DownloadTicketTypeInfo, Ct))!;
    }

    internal static async Task<FileQuota> Quota(FilesHost host, string tenant)
    {
        using var response = await host.Send(tenant, HttpMethod.Get, "/api/files/quota");
        Assert.True(response.StatusCode == HttpStatusCode.OK, await response.Content.ReadAsStringAsync(Ct));
        return (await response.Content.ReadFromJsonAsync(FilesContractJson.FileQuotaTypeInfo, Ct))!;
    }

    // 能力审计（不含 PEP 决策记录）：能力 ID、主体、结果、通道与租户。
    private static Task<IReadOnlyList<string[]>> Audit(FilesHost host) => host.AdminRows(
        "SELECT capability_id, actor_type, actor_id, result, channel, tenant_id FROM plugin_qian_audit.audit_entries WHERE capability_id LIKE 'qian.files/%' ORDER BY occurred_at, seq");

    internal static Uri WithQuery(Uri url, string name, string? value)
    {
        var query = Microsoft.AspNetCore.WebUtilities.QueryHelpers.ParseQuery(url.Query);
        var pairs = query.Where(pair => pair.Key != name).SelectMany(pair => pair.Value.Select(item => new KeyValuePair<string, string?>(pair.Key, item))).ToList();
        if (value is not null) pairs.Add(new(name, value));
        return new Uri(Microsoft.AspNetCore.WebUtilities.QueryHelpers.AddQueryString(url.GetLeftPart(UriPartial.Path), pairs));
    }

    private static IEnumerable<string> PropertyNames(JsonElement node)
    {
        if (node.ValueKind == JsonValueKind.Array)
        {
            foreach (var item in node.EnumerateArray()) foreach (var name in PropertyNames(item)) yield return name;
            yield break;
        }
        if (node.ValueKind != JsonValueKind.Object) yield break;
        foreach (var property in node.EnumerateObject())
        {
            if (property.Name == "properties" && property.Value.ValueKind == JsonValueKind.Object)
                foreach (var declared in property.Value.EnumerateObject()) yield return declared.Name;
            foreach (var name in PropertyNames(property.Value)) yield return name;
        }
    }

    private static bool Switch(string name) => name.Contains("disposition", StringComparison.OrdinalIgnoreCase) || name.Contains("inline", StringComparison.OrdinalIgnoreCase) ||
        name.Contains("attachment", StringComparison.OrdinalIgnoreCase) || name.Contains("forcedownload", StringComparison.OrdinalIgnoreCase);
}

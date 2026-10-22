using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;

namespace Qian.Files.Tests;

// 验收之外的边界：直传网关只认服务端签发的地址，完成时以服务端嗅探的类型为准，申请按策略与幂等键判定。
public sealed partial class FilesAcceptanceTests
{
    private static readonly string[] TextOnly = ["text/*"];

    [Fact]
    public async Task 直传网关_篡改签名与类型头或缺大小头即404_超出声明大小413_同键重复写409_下载地址不能指向暂存键()
    {
        await using var host = await FilesHost.StartAsync(postgres, ct: Ct);
        var content = Text("gateway content");
        var upload = await Presigned(host, FilesHost.A, FilesHost.Request(content));
        Assert.Equal("PUT", upload.Method);
        Assert.Equal((content.LongLength.ToString(System.Globalization.CultureInfo.InvariantCulture), "text/plain"),
            (upload.RequiredHeaders["X-Kun-Max-Bytes"], upload.RequiredHeaders["Content-Type"]));

        using (var forged = await host.Put(upload with { Url = WithQuery(upload.Url, "signature", "AAAA") }, content))
            await AssertError(forged, HttpStatusCode.NotFound, "NOT_FOUND");
        using (var retyped = await host.Put(upload, content, "text/html")) await AssertError(retyped, HttpStatusCode.NotFound, "NOT_FOUND");
        using (var unbounded = await host.Send(new HttpRequestMessage(HttpMethod.Put, upload.Url)
        {
            Content = new ByteArrayContent(content) { Headers = { ContentType = new MediaTypeHeaderValue("text/plain") } }
        })) await AssertError(unbounded, HttpStatusCode.NotFound, "NOT_FOUND");
        using (var oversized = await host.Put(upload, [.. content, (byte)'!'])) await AssertError(oversized, HttpStatusCode.RequestEntityTooLarge, "PAYLOAD_TOO_LARGE");
        // 超限的半截对象已删除，按签发地址重传仍能成功；对象写入后同一地址不能再覆盖。
        using (var stored = await host.Put(upload, content)) Assert.Equal(HttpStatusCode.Created, stored.StatusCode);
        using (var overwrite = await host.Put(upload, Text("overwrite content"))) await AssertError(overwrite, HttpStatusCode.Conflict, "CONFLICT");

        using (var completed = await host.Send(FilesHost.A, HttpMethod.Post, "/api/files/" + upload.FileId + ":complete"))
            Assert.Equal(HttpStatusCode.OK, completed.StatusCode);
        var ticket = await Ticket(host, FilesHost.A, upload.FileId);
        var staging = Assert.Single(await host.AdminRows($"SELECT upload_key FROM {FilesDbContext.Schema}.file_objects WHERE id = @id", ("id", upload.FileId)))[0];
        var path = ticket.Url.AbsolutePath[..ticket.Url.AbsolutePath.IndexOf("/tenants/", StringComparison.Ordinal)];
        using (var redirected = await host.Client.GetAsync(new Uri(ticket.Url, path + "/" + staging + ticket.Url.Query), Ct))
            await AssertError(redirected, HttpStatusCode.NotFound, "NOT_FOUND");
        // 下载地址过期后与不存在同为 404。
        host.Clock.Advance(TimeSpan.FromMinutes(5) + TimeSpan.FromSeconds(1));
        using (var stale = await host.Client.GetAsync(ticket.Url, Ct)) await AssertError(stale, HttpStatusCode.NotFound, "NOT_FOUND");
    }

    [Fact]
    public async Task 完成时按服务端嗅探的类型判定_声明为png的HTML进隔离_声明为文本的PNG按真实类型入库()
    {
        await using var host = await FilesHost.StartAsync(postgres, ct: Ct);
        var html = Encoding.UTF8.GetBytes("<html><body onload=\"alert(1)\">avatar</body></html>");
        var disguised = await Presigned(host, FilesHost.A, FilesHost.Request(html, "image/png", "avatar.png", FilesHost.Avatar));
        using (var put = await host.Put(disguised, html)) Assert.Equal(HttpStatusCode.Created, put.StatusCode);
        using (var completed = await host.Send(FilesHost.A, HttpMethod.Post, "/api/files/" + disguised.FileId + ":complete"))
            Assert.Contains("text/html", (await AssertError(completed, HttpStatusCode.UnprocessableEntity, "FILE_TYPE_NOT_ALLOWED")).GetProperty("message").GetString(),
                StringComparison.Ordinal);
        using (var get = await host.Send(FilesHost.A, HttpMethod.Get, "/api/files/" + disguised.FileId))
        {
            var file = (await get.Content.ReadFromJsonAsync(FilesContractJson.FileDescriptorTypeInfo, Ct))!;
            Assert.Equal((FileStatus.Quarantined, FileQuarantineReason.TypeNotAllowed, "text/html"), (file.Status, file.QuarantineReason, file.ContentType));
        }

        byte[] png = [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, 0x00, 0x00, 0x00, 0x0D, 0x49, 0x48, 0x44, 0x52, 0x00, 0x00, 0x00, 0x01];
        var image = await host.Upload(FilesHost.A, png, "text/plain", "image.txt");
        Assert.Equal((FileStatus.Available, "image/png"), (image.Status, image.ContentType));
        Assert.EndsWith(image.Id.ToString("D") + ".png", image.StorageKey, StringComparison.Ordinal);
        using var download = await host.Client.GetAsync((await Ticket(host, FilesHost.A, image.Id)).Url, Ct);
        Assert.Equal("image/png", download.Content.Headers.ContentType?.MediaType);
        Assert.Equal("attachment", download.Content.Headers.ContentDisposition?.DispositionType);
    }

    [Fact]
    public async Task 申请按策略判定_未知用途超限类型与超量逐项报错_同一幂等键返回同一文件_改参数或已完成后409()
    {
        await using var host = await FilesHost.StartAsync(postgres, policies: new object[]
        {
            new { purpose = FilesHost.Purpose, allowedTypes = TextOnly, maxSize = 64L, maxCount = 2 }
        }, ct: Ct);
        using (var unknown = await host.Presign(FilesHost.A, FilesHost.Request(Text("unknown"), purpose: "acme.crm/unknown")))
        {
            var error = await AssertError(unknown, HttpStatusCode.BadRequest, "INPUT_INVALID");
            var field = Assert.Single(error.GetProperty("errors").EnumerateArray());
            Assert.Equal(("VALUE_NOT_ALLOWED", "#/purpose"), (field.GetProperty("code").GetString(), field.GetProperty("pointer").GetString()));
            Assert.Equal([FilesHost.Purpose], field.GetProperty("allowed").EnumerateArray().Select(item => item.GetString()));
        }
        using (var large = await host.Presign(FilesHost.A, FilesHost.Request(Text("large", 65))))
            Assert.Equal("#/size", (await AssertError(large, HttpStatusCode.UnprocessableEntity, "FILE_TOO_LARGE")).GetProperty("pointer").GetString());
        using (var typed = await host.Presign(FilesHost.A, FilesHost.Request(Text("pdf"), "application/pdf", "a.pdf")))
        {
            var error = await AssertError(typed, HttpStatusCode.UnprocessableEntity, "FILE_TYPE_NOT_ALLOWED");
            Assert.Equal(["text/*"], error.GetProperty("allowed").EnumerateArray().Select(item => item.GetString()));
        }
        using (var sha = await host.Presign(FilesHost.A, FilesHost.Request(Text("sha")) with { Sha256 = "not-a-digest" }))
            await AssertError(sha, HttpStatusCode.BadRequest, "INPUT_INVALID");
        using (var unsupported = await host.Send(FilesHost.A, HttpMethod.Post, "/api/files/uploads:presign", new StringContent("purpose=x")))
            await AssertError(unsupported, HttpStatusCode.UnsupportedMediaType, "UNSUPPORTED_MEDIA_TYPE");

        // 同一幂等键、同一参数：返回同一文件并重新签发地址；同键改参数 409；完成后同键再申请 409，重复完成返回同一结果。
        var content = Text("idempotent");
        var request = FilesHost.Request(content);
        var first = await Presigned(host, FilesHost.A, request);
        var replay = await Presigned(host, FilesHost.A, request);
        Assert.Equal(first.FileId, replay.FileId);
        using (var changed = await host.Presign(FilesHost.A, request with { Name = "renamed.txt" })) await AssertError(changed, HttpStatusCode.Conflict, "CONFLICT");
        using (var put = await host.Put(replay, content)) Assert.Equal(HttpStatusCode.Created, put.StatusCode);
        for (var attempt = 0; attempt < 2; attempt++)
        {
            using var completed = await host.Send(FilesHost.A, HttpMethod.Post, "/api/files/" + first.FileId + ":complete");
            Assert.Equal(FileStatus.Available, (await completed.Content.ReadFromJsonAsync(FilesContractJson.FileDescriptorTypeInfo, Ct))!.Status);
        }
        using (var after = await host.Presign(FilesHost.A, request)) await AssertError(after, HttpStatusCode.Conflict, "CONFLICT");
        Assert.Single(await host.AdminRows($"SELECT id FROM {FilesDbContext.Schema}.outbox_messages"));

        // maxCount = 2：在用文件（含上传中的预占）达到上限后报 FILE_COUNT_EXCEEDED。
        _ = await Presigned(host, FilesHost.A, FilesHost.Request(Text("second")));
        using (var third = await host.Presign(FilesHost.A, FilesHost.Request(Text("third"))))
            Assert.Equal("#/purpose", (await AssertError(third, HttpStatusCode.UnprocessableEntity, "FILE_COUNT_EXCEEDED")).GetProperty("pointer").GetString());
    }
}

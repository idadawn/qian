using System.Net;
using System.Text;

namespace Qian.Files.Tests;

// S1-FILE-004 验收 5：组件在 metadata/upload-policies.json 声明的上传策略，经 Registry 预装快照汇总后可用于申请直传；与组件配置同用途时以配置为准。
public sealed partial class FilesAcceptanceTests
{
    private static readonly string[] PdfOnly = ["application/pdf"];
    private static readonly string[] PlainOnly = ["text/plain"];

    [Fact]
    public async Task 组件metadata声明的上传策略经预装快照生效_同用途以组件配置为准()
    {
        IReadOnlyList<Registry.InstalledComponentMetadata> installed =
        [
            InstalledSnapshot.Component("acme.docs", "1.0.0", new[] { new { purpose = "acme.docs/contract", allowedTypes = PdfOnly, maxSize = 2048L } }),
            // 与组件配置的 acme.crm/attachment 同用途，但只收 16 字节纯文本：配置（4 MiB，含 text/html）应当胜出。
            InstalledSnapshot.Component("acme.crm", "1.0.0", new[] { new { purpose = FilesHost.Purpose, allowedTypes = PlainOnly, maxSize = 16L } })
        ];
        await using var host = await FilesHost.StartAsync(postgres, installed: installed, ct: Ct);

        var pdf = Encoding.ASCII.GetBytes("%PDF-1.7\n" + new string('x', 512));
        var stored = await host.Upload(FilesHost.A, pdf, "application/pdf", "contract.pdf", "acme.docs/contract");
        Assert.Equal((FileStatus.Available, "acme.docs"), (stored.Status, stored.PluginId));
        Assert.StartsWith($"tenants/{FilesHost.A}/acme.docs/", stored.StorageKey, StringComparison.Ordinal);
        using (var large = await host.Presign(FilesHost.A, FilesHost.Request(Text("large", 4096), "application/pdf", "large.pdf", "acme.docs/contract")))
            Assert.Equal("#/size", (await AssertError(large, HttpStatusCode.UnprocessableEntity, "FILE_TOO_LARGE")).GetProperty("pointer").GetString());
        using (var typed = await host.Presign(FilesHost.A, FilesHost.Request(Text("plain"), purpose: "acme.docs/contract")))
            await AssertError(typed, HttpStatusCode.UnprocessableEntity, "FILE_TYPE_NOT_ALLOWED");

        // 同用途以组件配置为准：HTML 与超过声明 16 字节的大小都按配置放行。
        var html = await host.Upload(FilesHost.A, Text("<p>configured</p>", 1024), "text/html", "page.html");
        Assert.Equal(FileStatus.Available, html.Status);
        using (var unknown = await host.Presign(FilesHost.A, FilesHost.Request(Text("unknown"), purpose: "acme.crm/unknown")))
        {
            var field = Assert.Single((await AssertError(unknown, HttpStatusCode.BadRequest, "INPUT_INVALID")).GetProperty("errors").EnumerateArray());
            Assert.Equal([FilesHost.Purpose, FilesHost.Avatar, "acme.docs/contract"], field.GetProperty("allowed").EnumerateArray().Select(item => item.GetString()));
        }
    }
}

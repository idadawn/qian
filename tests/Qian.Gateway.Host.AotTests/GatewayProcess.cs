using System.Diagnostics;
using System.Globalization;
using System.Net;
using System.Net.Security;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text;

namespace Qian.Gateway.Host.AotTests;

// 只编排独立生产进程，不在生产宿主添加测试端点或替换业务服务。
internal sealed class GatewayProcess : IAsyncDisposable
{
    private readonly Process _process;
    private readonly StringBuilder _output = new();
    private readonly string[] _secrets;
    private readonly Task _stdout;
    private readonly Task _stderr;
    private bool _stopped;
    internal bool Native { get; }
    internal string Output { get { lock (_output) return _output.ToString(); } }
    internal string SafeOutput => _secrets.Aggregate(Output, (text, secret) => text.Replace(secret, "[redacted]", StringComparison.Ordinal));
    private GatewayProcess(Process process, bool native, string[] secrets)
    {
        _process = process; Native = native; _secrets = secrets;
        _stdout = Capture(process.StandardOutput); _stderr = Capture(process.StandardError);
    }
    internal static GatewayProcess Start(string directory, params string[] secrets)
    {
        var executable = Environment.GetEnvironmentVariable("QIAN_GATEWAY_HOST_EXECUTABLE");
        var jit = Environment.GetEnvironmentVariable("QIAN_GATEWAY_HOST_DLL");
        Assert.True((executable is null) != (jit is null), "必须显式选择生产原生可执行文件；JIT 预检只能单独声明 QIAN_GATEWAY_HOST_DLL。");
        if (executable is not null)
        {
            Assert.True(OperatingSystem.IsLinux(), "原生验收必须在 Linux 上运行。");
            Assert.True(File.Exists(executable), "缺少原生生产宿主，不能跳过验收。");
            Assert.False(File.Exists(executable + ".dll"), "不能把托管 DLL 当作原生发布产物。");
            using var artifact = File.OpenRead(executable);
            Span<byte> header = stackalloc byte[4];
            artifact.ReadExactly(header);
            Assert.True(header.SequenceEqual("\u007fELF"u8), "生产宿主必须是 Linux ELF 原生发布产物。");
        }
        var start = new ProcessStartInfo(executable ?? "dotnet")
        {
            WorkingDirectory = directory, RedirectStandardOutput = true, RedirectStandardError = true, UseShellExecute = false
        };
        if (jit is not null) start.ArgumentList.Add(jit);
        start.ArgumentList.Add("--contentRoot=" + directory);
        start.Environment["ASPNETCORE_ENVIRONMENT"] = "Development";
        start.Environment["DOTNET_ENVIRONMENT"] = "Development";
        start.Environment["DOTNET_NOLOGO"] = "true";
        foreach (var name in start.Environment.Keys.Where(key => key.StartsWith("QIAN_", StringComparison.Ordinal)).ToArray()) start.Environment.Remove(name);
        return new GatewayProcess(Process.Start(start)!, executable is not null, secrets);
    }
    internal async Task WaitReady(HttpClient client, Uri address, CancellationToken ct)
    {
        var deadline = DateTime.UtcNow.AddSeconds(90);
        while (DateTime.UtcNow < deadline)
        {
            if (_process.HasExited)
            {
                await Task.WhenAll(_stdout, _stderr);
                Assert.Fail("生产宿主启动提前退出：" + SafeOutput);
            }
            try
            {
                using var ready = await client.GetAsync(new Uri(address, "readyz"), ct);
                if (ready.IsSuccessStatusCode && Output.Contains("qian.gateway 全部业务组件已就绪", StringComparison.Ordinal))
                {
                    if (Native) Assert.True(Output.Contains("dynamic-code=False", StringComparison.Ordinal), "原生产物不应支持动态代码。");
                    return;
                }
            }
            catch (HttpRequestException) { }
            await Task.Delay(100, ct);
        }
        Assert.Fail("生产宿主未在 90 秒内就绪：" + SafeOutput);
    }
    private async Task Capture(StreamReader reader)
    {
        while (await reader.ReadLineAsync() is { } line)
            lock (_output) if (_output.Length < 1_048_576) _output.AppendLine(line);
    }
    internal async Task Stop()
    {
        if (_stopped) return;
        _stopped = true;
        try
        {
            if (!_process.HasExited)
            {
                var signal = new ProcessStartInfo("kill") { UseShellExecute = false };
                signal.ArgumentList.Add("-TERM"); signal.ArgumentList.Add(_process.Id.ToString(CultureInfo.InvariantCulture));
                using var sender = Process.Start(signal)!; await sender.WaitForExitAsync();
                try { await _process.WaitForExitAsync().WaitAsync(TimeSpan.FromSeconds(20)); }
                finally { if (!_process.HasExited) { _process.Kill(entireProcessTree: true); await _process.WaitForExitAsync(); } }
            }
            await Task.WhenAll(_stdout, _stderr);
            Assert.True(_process.ExitCode == 0, "生产宿主未正常释放依赖并退出：" + SafeOutput);
        }
        finally { _process.Dispose(); }
    }
    public async ValueTask DisposeAsync() => await Stop();
    internal static async Task PrivateFile(string path, byte[] content, CancellationToken ct)
    {
        var options = new FileStreamOptions { Access = FileAccess.Write, Mode = FileMode.CreateNew };
        if (!OperatingSystem.IsWindows()) options.UnixCreateMode = UnixFileMode.UserRead | UnixFileMode.UserWrite;
        await using var stream = new FileStream(path, options); await stream.WriteAsync(content, ct);
    }
    internal static Task PrivateFile(string path, string content, CancellationToken ct) => PrivateFile(path, Encoding.UTF8.GetBytes(content), ct);
    internal static X509Certificate2 Certificate()
    {
        using var key = RSA.Create(2048);
        var request = new CertificateRequest("CN=localhost", key, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
        var names = new SubjectAlternativeNameBuilder(); names.AddDnsName("localhost"); names.AddIpAddress(IPAddress.Loopback);
        request.CertificateExtensions.Add(names.Build());
        using var certificate = request.CreateSelfSigned(DateTimeOffset.UtcNow.AddMinutes(-1), DateTimeOffset.UtcNow.AddDays(1));
        return X509CertificateLoader.LoadPkcs12(certificate.Export(X509ContentType.Pkcs12), null, X509KeyStorageFlags.Exportable);
    }
    internal static (HttpClient Client, CookieContainer Cookies) Browser(X509Certificate2 certificate)
    {
        var cookies = new CookieContainer();
        var handler = new SocketsHttpHandler
        {
            AllowAutoRedirect = false, UseCookies = true, CookieContainer = cookies,
            SslOptions = new SslClientAuthenticationOptions
            { RemoteCertificateValidationCallback = (_, actual, _, _) => actual?.GetCertHashString() == certificate.GetCertHashString() }
        };
        return (new HttpClient(handler) { Timeout = TimeSpan.FromSeconds(30) }, cookies);
    }
}

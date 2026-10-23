using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using Npgsql;
using Qian.Testing.Containers;

namespace Qian.Data.Native.Postgres.Tests;

public sealed class NativePoolAdmissionTests(PostgresFixture postgres) : IClassFixture<PostgresFixture>
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task 排队和物理打开共用连接超时预算_超时后归还名额()
    {
        var pendingCleanups = new List<Task>();
        var settings = new NpgsqlConnectionStringBuilder(postgres.ConnectionString) { MaxPoolSize = 1, Timeout = 3, Enlist = false };
        await using var proxy = new StallSecondConnection(settings.Host ?? throw new InvalidOperationException("PostgreSQL fixture 缺少 Host。"), settings.Port, Ct);
        settings.Host = IPAddress.Loopback.ToString(); settings.Port = proxy.Port;
        await using var source = new NpgsqlSlimDataSourceBuilder(settings.ConnectionString).Build();
        using var admission = new NativePoolAdmission(1, 3);
        await using var first = await admission.OpenAsync(source, pendingCleanups.Add, Ct);
        var elapsed = Stopwatch.StartNew();
        var waiting = admission.OpenAsync(source, pendingCleanups.Add, Ct).AsTask();
        await Task.Delay(TimeSpan.FromSeconds(2), Ct);
        Assert.False(waiting.IsCompleted);
        source.Clear(); // 强制下一租约重新建立真实物理连接。
        await first.DisposeAsync();
        await proxy.SecondAccepted.Task.WaitAsync(TimeSpan.FromSeconds(2), Ct);
        var error = await Assert.ThrowsAsync<NpgsqlException>(() => waiting);
        Assert.IsType<TimeoutException>(error.InnerException);
        // 若取得名额后重置预算，约在第 5 秒才超时；正确预算是总共 3 秒。
        Assert.InRange(elapsed.Elapsed, TimeSpan.FromSeconds(2.5), TimeSpan.FromSeconds(4));

        _ = Assert.Single(pendingCleanups);
        Assert.False(pendingCleanups[0].IsCompleted); // API 超时不能丢弃仍在握手的原始打开。
        proxy.ReleaseSecond.TrySetResult();
        await Task.WhenAll(pendingCleanups).WaitAsync(TimeSpan.FromSeconds(5), Ct);
        // 物理打开失败也释放名额；使用真实数据库立即取得下一租约。
        await using var healthy = new NpgsqlSlimDataSourceBuilder(postgres.ConnectionString).Build();
        await using var recovered = await admission.OpenAsync(healthy, pendingCleanups.Add, Ct);
        await using var command = new NpgsqlCommand("SELECT 1", recovered.Connection);
        Assert.Equal(1, await command.ExecuteScalarAsync(Ct));
    }

    [Fact]
    public async Task 无限超时保留用户取消_多名额并发和重复释放不改变容量()
    {
        var pendingCleanups = new List<Task>();
        var settings = new NpgsqlConnectionStringBuilder(postgres.ConnectionString) { MaxPoolSize = 2, Timeout = 0, Enlist = false };
        await using var source = new NpgsqlSlimDataSourceBuilder(settings.ConnectionString).Build();
        using var admission = new NativePoolAdmission(2, 0);
        await using var first = await admission.OpenAsync(source, pendingCleanups.Add, Ct);
        await using var second = await admission.OpenAsync(source, pendingCleanups.Add, Ct);
        using var cancellation = CancellationTokenSource.CreateLinkedTokenSource(Ct);
        var waiting = admission.OpenAsync(source, pendingCleanups.Add, cancellation.Token).AsTask();
        Assert.False(waiting.IsCompleted);
        await cancellation.CancelAsync();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => waiting.WaitAsync(TimeSpan.FromSeconds(5), Ct));
        await Task.WhenAll(first.DisposeAsync().AsTask(), first.DisposeAsync().AsTask());
        await using var replacement = await admission.OpenAsync(source, pendingCleanups.Add, Ct);
        using var stillFullCancellation = CancellationTokenSource.CreateLinkedTokenSource(Ct);
        var stillFull = admission.OpenAsync(source, pendingCleanups.Add, stillFullCancellation.Token).AsTask();
        Assert.False(stillFull.IsCompleted);
        await stillFullCancellation.CancelAsync();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => stillFull);
        Assert.NotEqual(second.Connection.ProcessID, replacement.Connection.ProcessID);
    }

    [Fact]
    public async Task 池满时遵守连接超时_超时不泄漏名额()
    {
        var pendingCleanups = new List<Task>();
        await using var source = new NpgsqlSlimDataSourceBuilder(postgres.ConnectionString).Build();
        using var admission = new NativePoolAdmission(1, 1);
        await using var first = await admission.OpenAsync(source, pendingCleanups.Add, Ct);
        var timeout = await Assert.ThrowsAsync<NpgsqlException>(() => admission.OpenAsync(source, pendingCleanups.Add, Ct).AsTask());
        Assert.IsType<TimeoutException>(timeout.InnerException);
        await first.DisposeAsync();
        await using var next = await admission.OpenAsync(source, pendingCleanups.Add, Ct);
        Assert.Equal(System.Data.ConnectionState.Open, next.Connection.State);
    }

    // 第一条 TCP 连接透明转发至共享 PostgreSQL fixture；第二条接收后不完成握手，
    // 让连接打开确实消耗剩余时间。只制造网络延迟，不读取或记录协议/凭据。
    internal sealed class StallSecondConnection : IAsyncDisposable
    {
        private readonly TcpListener _listener = new(IPAddress.Loopback, 0);
        private readonly CancellationTokenSource _stop;
        private readonly Task _serving;
        internal TaskCompletionSource ReleaseSecond { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        internal TaskCompletionSource SecondAccepted { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        internal int Port => ((IPEndPoint)_listener.LocalEndpoint).Port;

        internal StallSecondConnection(string host, int port, CancellationToken ct)
        {
            _stop = CancellationTokenSource.CreateLinkedTokenSource(ct);
            _listener.Start();
            _serving = Serve(host, port);
        }

        private async Task Serve(string host, int port)
        {
            using var first = await _listener.AcceptTcpClientAsync(_stop.Token);
            using var upstream = new TcpClient();
            await upstream.ConnectAsync(host, port, _stop.Token);
            var forwarding = Task.WhenAll(first.GetStream().CopyToAsync(upstream.GetStream(), _stop.Token),
                upstream.GetStream().CopyToAsync(first.GetStream(), _stop.Token));
            try
            {
                using var second = await _listener.AcceptTcpClientAsync(_stop.Token);
                SecondAccepted.TrySetResult();
                await ReleaseSecond.Task.WaitAsync(_stop.Token);
            }
            finally
            {
                await _stop.CancelAsync(); first.Dispose(); upstream.Dispose();
                try { await forwarding; }
                catch (Exception error) when (error is OperationCanceledException or IOException or ObjectDisposedException) { }
            }
        }

        public async ValueTask DisposeAsync()
        {
            await _stop.CancelAsync(); _listener.Stop();
            try { await _serving; }
            catch (Exception error) when (error is OperationCanceledException or SocketException or ObjectDisposedException) { }
            _stop.Dispose();
        }
    }
}

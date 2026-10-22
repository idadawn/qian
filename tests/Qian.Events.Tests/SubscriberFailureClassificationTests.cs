using System.Text.Json;
using Kun.Core.MultiTenancy;
using Kun.EntityFrameworkCore.Extensions;
using Kun.MultiTenancy;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Qian.Data;
using Qian.Runtime.Errors;

namespace Qian.Events.Tests;

/// <summary>
/// handler 异常的确定性判定（S1-EVT-004）：契约错误一次就进死信，对象生命周期与包着瞬时故障的 InvalidOperationException 按瞬时失败退避重试。
/// 不需要容器：判定是纯函数，「Npgsql 把瞬时故障包成 InvalidOperationException」用真实 EF 查询一个没有监听的端口取得。
/// </summary>
public sealed class SubscriberFailureClassificationTests
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    public static TheoryData<string, Exception> Deterministic() => new()
    {
        { "契约错误", new QianEventsException(EventsErrors.ConsumeRejected, "重试也不会成功") },
        { "派生失败（无内层）", new InvalidOperationException("信封不是 qian.record.changed.v1 事件。") },
        { "派生失败（包着载荷解析错误）", new InvalidOperationException("记录事件载荷不符合 RecordChangedPayload 契约。", new JsonException("bad")) },
        { "派生失败（包着信封校验错误）", new InvalidOperationException("外层", new QianEventsException(EventsErrors.ConsumeRejected, "内层")) },
        { "参数非法", new ArgumentException("租户不是 UUID") },
        { "参数为空", new ArgumentNullException("event") },
        { "载荷不合法", new JsonException("bad") },
        { "不支持的操作", new NotSupportedException() },
        { "错误码不可重试", new QianErrorException(new QianError(PlatformErrors.NotFound)) },
    };

    public static TheoryData<string, Exception> Transient() => new()
    {
        { "对象已释放", new ObjectDisposedException("connection") },
        { "包着超时的 InvalidOperationException", new InvalidOperationException("An exception has been raised that is likely due to a transient failure.", new TimeoutException()) },
        { "包着 IO 故障的两层 InvalidOperationException", new InvalidOperationException("外层", new InvalidOperationException("内层", new IOException("reset"))) },
        { "错误码可重试", new QianErrorException(new QianError(PlatformErrors.Unavailable)) },
        { "下游不可用", new HttpRequestException("503") },
        { "超时", new TimeoutException() },
    };

    [Theory]
    [MemberData(nameof(Deterministic))]
    public void 契约错误参数非法载荷不合法与不可重试错误码_判为确定性失败(string because, Exception error) =>
        Assert.True(QianEventSubscriber.IsDeterministic(error), because);

    [Theory]
    [MemberData(nameof(Transient))]
    public void 对象生命周期与包着瞬时故障的InvalidOperationException_按瞬时失败重试(string because, Exception error) =>
        Assert.False(QianEventSubscriber.IsDeterministic(error), because);

    [Fact]
    public async Task Npgsql执行策略把连接失败包成InvalidOperationException_按内层判为瞬时失败()
    {
        // 端口 1 没有监听：EF 默认的 Npgsql 执行策略把可重试的 NpgsqlException 包成 InvalidOperationException 抛出，旧判定会一次就进死信。
        var services = new ServiceCollection();
        services.AddSingleton<ITenantContext>(new AsyncLocalTenantContext());
        services.AddQianData<ProbeContext>((_, options) => options.UseNpgsql("Host=127.0.0.1;Port=1;Username=probe;Password=probe;Timeout=3").UseKunSchema("plugin_probe"));
        await using var provider = services.BuildServiceProvider();
        await using var scope = provider.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<ProbeContext>();
        // 查询走 EF 的执行策略（ExecuteSqlRaw 按设计不走，所以用 SqlQueryRaw）。
        var error = await Assert.ThrowsAsync<InvalidOperationException>(() => context.Database.SqlQueryRaw<int>("SELECT 1 AS \"Value\"").ToListAsync(Ct));
        Assert.Equal(typeof(InvalidOperationException), error.GetType());
        Assert.True(error.InnerException is Npgsql.NpgsqlException { IsTransient: true }, error.InnerException?.ToString());
        Assert.False(QianEventSubscriber.IsDeterministic(error));
    }

    public sealed class ProbeContext(DbContextOptions<ProbeContext> options, ITenantContext tenant) : QianDbContext(options, tenant);
}

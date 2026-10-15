using System.Security.Cryptography;
using DotNet.Testcontainers.Builders;
using DotNet.Testcontainers.Containers;
using Xunit;

namespace Qian.Testing.Containers;

/// <summary>
/// 单个容器 fixture 的公共骨架：启动容器（瞬时失败时有界重试）、按 <see cref="DockerRequirement"/> 处理 Docker 缺失、随 fixture 一起释放。
/// 直接用作 xUnit 的 class / collection / assembly fixture。
/// </summary>
/// <typeparam name="TContainer">Testcontainers 容器类型。</typeparam>
public abstract class ContainerFixture<TContainer> : IAsyncLifetime
    where TContainer : class, IContainer
{
    private TContainer? _container;

    /// <summary>读取环境变量的函数，决定 Docker 缺失时跳过还是失败；只为单测替换而开放。</summary>
    internal Func<string, string?> EnvironmentReader { get; set; } = Environment.GetEnvironmentVariable;

    /// <summary>跳过当前测试的动作，默认 <see cref="Assert.Skip(string)"/>；只为单测替换而开放。</summary>
    internal Action<string> SkipAction { get; set; } = Assert.Skip;

    /// <summary>已启动的容器，供需要停启容器的韧性测试使用（例如 RabbitMQ 重启后消息不丢）。</summary>
    public TContainer Container =>
        _container ?? throw new InvalidOperationException($"{GetType().Name} 尚未启动。");

    /// <summary>遇到环境类瞬时失败（Ryuk 初始化失败、Docker API 请求超时）时，整个启动最多尝试的次数，所有 fixture 一致。</summary>
    internal const int TransientAttempts = 3;

    /// <summary>重试前的退避基数，第 n 次失败后等 n 倍；只为单测替换而开放。</summary>
    internal TimeSpan RetryDelay { get; set; } = TimeSpan.FromSeconds(2);

    /// <inheritdoc />
    public ValueTask InitializeAsync() => InitializeAsync(TestContext.Current.CancellationToken);

    /// <summary>启动 fixture；取消令牌只为单测替换而开放。</summary>
    internal async ValueTask InitializeAsync(CancellationToken cancellationToken)
    {
        try
        {
            for (var attempt = 1; ; attempt++)
            {
                try
                {
                    await OnStartingAsync(cancellationToken).ConfigureAwait(false);
                    _container = CreateContainer();
                    await _container.StartAsync(cancellationToken).ConfigureAwait(false);
                    break;
                }
                // 瞬时启动失败：释放这次启动拿到的全部资源，退避后从头再来。只重试启动本身，不重试任何测试断言。
                catch (Exception ex) when (attempt < AttemptsFor(ex) && !cancellationToken.IsCancellationRequested)
                {
                    TestContext.Current.SendDiagnosticMessage(
                        $"{GetType().Name}：第 {attempt} 次启动失败（{ex.GetType().Name}：{ex.Message}），释放资源后重试。");
                    await ReleaseAsync().ConfigureAwait(false);
                    await Task.Delay(RetryDelay * attempt, cancellationToken).ConfigureAwait(false);
                }
            }
            await OnStartedAsync(_container, cancellationToken).ConfigureAwait(false);
        }
        catch (DockerUnavailableException ex)
        {
            await DisposeAsync().ConfigureAwait(false);
            if (DockerRequirement.IsRequired(EnvironmentReader))
            {
                throw new InvalidOperationException(
                    $"{GetType().Name}：当前环境要求 Docker（CI 或 {DockerRequirement.RequireVariable}），但 Docker 不可用。", ex);
            }

            SkipAction($"{GetType().Name}：本机没有可用的 Docker，跳过集成测试；CI 上会直接失败。（{ex.Message}）");
            return;
        }
        catch
        {
            await DisposeAsync().ConfigureAwait(false);
            throw;
        }
    }

    /// <inheritdoc />
    /// <remarks>可重复调用：组合 fixture 启动失败时会先自行释放一次。</remarks>
    public async ValueTask DisposeAsync()
    {
        await ReleaseAsync().ConfigureAwait(false);
        GC.SuppressFinalize(this);
    }

    /// <summary>构造容器（尚未启动）；启动重试时每次重新构造。</summary>
    protected abstract TContainer CreateContainer();

    /// <summary>
    /// 容器（含 <see cref="OnStartingAsync"/> 启动的依赖容器）在就绪之前自行退出时，整个启动最多尝试的次数，默认 1（不重试）。
    /// 环境类瞬时失败另按 <see cref="TransientAttempts"/> 重试，不受此值影响。
    /// </summary>
    protected virtual int StartAttempts => 1;

    /// <summary>组合 fixture 可先准备受其拥有的依赖与网络；失败时统一清理，瞬时失败重试时会在清理后再次调用。</summary>
    protected virtual ValueTask OnStartingAsync(CancellationToken cancellationToken) => ValueTask.CompletedTask;

    /// <summary>容器就绪后的初始化，例如读出连接串、建应用角色。</summary>
    protected virtual ValueTask OnStartedAsync(TContainer container, CancellationToken cancellationToken) => ValueTask.CompletedTask;

    /// <summary>释放容器之前释放 fixture 自己持有的资源；启动重试之间也会调用，须能处理尚未启动完的状态。</summary>
    protected virtual ValueTask OnDisposingAsync() => ValueTask.CompletedTask;

    /// <summary>主容器释放后，再释放它依赖的数据库与网络；启动重试之间也会调用。</summary>
    protected virtual ValueTask OnDisposedAsync() => ValueTask.CompletedTask;

    /// <summary>
    /// 某类启动失败最多尝试几次。下面两类是宿主繁忙（如多个会话并发跑容器测试）时的环境类瞬时失败，与具体容器无关，所有 fixture 都重试：
    /// <list type="bullet">
    /// <item>Ryuk（Testcontainers 的资源回收容器）在每个测试进程第一次启动容器时初始化，等待连接确认的超时写死为 60 秒，
    /// 耗尽时抛 <see cref="ResourceReaperException"/>；失败的实例不缓存，下一次启动会重新初始化。</item>
    /// <item>Docker 守护进程在 Docker 客户端的请求超时内没有响应：客户端取消请求，抛 <see cref="OperationCanceledException"/>，
    /// 内层是 <see cref="HttpRequestException"/>。本次启动自己的取消令牌触发的取消不在此列，由调用方的条件排除。</item>
    /// </list>
    /// 容器在就绪前自行退出由各 fixture 的 <see cref="StartAttempts"/> 决定；其余错误（镜像、配置、Docker 不可用等）不重试。
    /// </summary>
    private int AttemptsFor(Exception error) => error switch
    {
        ResourceReaperException => TransientAttempts,
        OperationCanceledException { InnerException: HttpRequestException } => TransientAttempts,
        ContainerNotRunningException => StartAttempts,
        _ => 1,
    };

    /// <summary>依次释放 fixture 自己的资源、主容器与它依赖的资源；释放后可以重新启动。</summary>
    private async ValueTask ReleaseAsync()
    {
        try { await OnDisposingAsync().ConfigureAwait(false); }
        finally
        {
            var container = Interlocked.Exchange(ref _container, null);
            try
            {
                if (container is not null) await container.DisposeAsync().ConfigureAwait(false);
            }
            finally { await OnDisposedAsync().ConfigureAwait(false); }
        }
    }

    /// <summary>每次运行随机生成的口令：测试容器不使用写死在仓库里的凭据。</summary>
    protected static string NewSecret() => Convert.ToHexStringLower(RandomNumberGenerator.GetBytes(16));
}

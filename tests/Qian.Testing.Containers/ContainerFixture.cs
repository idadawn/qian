using System.Security.Cryptography;
using DotNet.Testcontainers.Builders;
using DotNet.Testcontainers.Containers;
using Xunit;

namespace Qian.Testing.Containers;

/// <summary>
/// 单个容器 fixture 的公共骨架：启动容器、按 <see cref="DockerRequirement"/> 处理 Docker 缺失、随 fixture 一起释放。
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

    /// <inheritdoc />
    public async ValueTask InitializeAsync()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        try
        {
            _container = CreateContainer();
            await _container.StartAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (DockerUnavailableException ex)
        {
            if (DockerRequirement.IsRequired(EnvironmentReader))
            {
                throw new InvalidOperationException(
                    $"{GetType().Name}：当前环境要求 Docker（CI 或 {DockerRequirement.RequireVariable}），但 Docker 不可用。", ex);
            }

            SkipAction($"{GetType().Name}：本机没有可用的 Docker，跳过集成测试；CI 上会直接失败。（{ex.Message}）");
            return;
        }

        await OnStartedAsync(_container, cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    /// <remarks>可重复调用：组合 fixture 启动失败时会先自行释放一次。</remarks>
    public async ValueTask DisposeAsync()
    {
        await OnDisposingAsync().ConfigureAwait(false);
        var container = Interlocked.Exchange(ref _container, null);
        if (container is not null)
        {
            await container.DisposeAsync().ConfigureAwait(false);
        }

        GC.SuppressFinalize(this);
    }

    /// <summary>构造容器（尚未启动）。</summary>
    protected abstract TContainer CreateContainer();

    /// <summary>容器就绪后的初始化，例如读出连接串、建应用角色。</summary>
    protected virtual ValueTask OnStartedAsync(TContainer container, CancellationToken cancellationToken) => ValueTask.CompletedTask;

    /// <summary>释放容器之前释放 fixture 自己持有的资源。</summary>
    protected virtual ValueTask OnDisposingAsync() => ValueTask.CompletedTask;

    /// <summary>每次运行随机生成的口令：测试容器不使用写死在仓库里的凭据。</summary>
    protected static string NewSecret() => Convert.ToHexStringLower(RandomNumberGenerator.GetBytes(16));
}

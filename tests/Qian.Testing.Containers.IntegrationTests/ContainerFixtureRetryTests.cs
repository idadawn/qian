using System.Reflection;
using DotNet.Testcontainers.Containers;

namespace Qian.Testing.Containers.IntegrationTests;

/// <summary>
/// 单元测试（假容器，不需要 Docker，不进 SharedInfrastructure collection）：启动失败的有界重试。
/// Ryuk 初始化失败与 Docker API 请求超时对所有 fixture 重试，最多三次，每次重试前释放这次启动拿到的全部资源；
/// 容器在就绪前退出只按 fixture 的 StartAttempts 重试；其他错误与已取消的启动不重试。
/// </summary>
public sealed class ContainerFixtureRetryTests
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Ryuk初始化失败时释放这次启动的全部资源并从头重启_第三次成功即可用()
    {
        await using var fixture = new ScriptedFixture([RyukCancelled(), RyukCancelled(), null]);
        await fixture.InitializeAsync(Ct);

        Assert.Same(fixture.Containers[2].AsContainer(), fixture.Container);
        Assert.Equal([true, true, false], fixture.Containers.Select(container => container.Disposed));
        Assert.Equal(3, fixture.Starting);
        Assert.Equal(1, fixture.Started);
        // 前两次 OnStartingAsync 准备的依赖都已释放，只剩成功那次的。
        Assert.Equal(1, fixture.HeldDependencies);
    }

    [Fact]
    public async Task OnStartingAsync里遇到Ryuk初始化失败同样从头重启()
    {
        await using var fixture = new ScriptedFixture([null], startingFailures: [RyukCancelled()]);
        await fixture.InitializeAsync(Ct);

        Assert.Single(fixture.Containers);
        Assert.Equal(2, fixture.Starting);
        Assert.Equal(1, fixture.Started);
        Assert.Equal(1, fixture.HeldDependencies);
    }

    [Fact]
    public async Task Ryuk一直起不来时共尝试三次_抛出最后一次的原始异常且资源全部释放()
    {
        ResourceReaperException[] failures = [RyukCancelled(), RyukCancelled(), RyukCancelled()];
        await using var fixture = new ScriptedFixture(failures);

        var error = await Assert.ThrowsAsync<ResourceReaperException>(() => fixture.InitializeAsync(Ct).AsTask());

        Assert.Same(failures[^1], error);
        Assert.Equal(ContainerFixture<IContainer>.TransientAttempts, fixture.Containers.Count);
        Assert.All(fixture.Containers, container => Assert.True(container.Disposed));
        Assert.Equal(0, fixture.Started);
        Assert.Equal(0, fixture.HeldDependencies);
    }

    [Fact]
    public async Task Docker守护进程在请求超时内没有响应时同样从头重启()
    {
        await using var fixture = new ScriptedFixture([DockerRequestTimedOut(), DockerRequestTimedOut(), null]);
        await fixture.InitializeAsync(Ct);

        Assert.Same(fixture.Containers[2].AsContainer(), fixture.Container);
        Assert.Equal([true, true, false], fixture.Containers.Select(container => container.Disposed));
        Assert.Equal(1, fixture.HeldDependencies);
    }

    [Fact]
    public async Task 其他启动错误只尝试一次()
    {
        var failure = new InvalidOperationException("simulated: image not found");
        await using var fixture = new ScriptedFixture([failure, null]);

        var error = await Assert.ThrowsAsync<InvalidOperationException>(() => fixture.InitializeAsync(Ct).AsTask());

        Assert.Same(failure, error);
        Assert.Single(fixture.Containers);
        Assert.True(fixture.Containers[0].Disposed);
        Assert.Equal(0, fixture.HeldDependencies);

        // 不是 Docker API 请求被取消（内层没有 HttpRequestException）的取消，同样不当作瞬时失败。
        await using var cancelled = new ScriptedFixture([new OperationCanceledException(), null]);
        await Assert.ThrowsAsync<OperationCanceledException>(() => cancelled.InitializeAsync(Ct).AsTask());
        Assert.Single(cancelled.Containers);
    }

    [Fact]
    public async Task 启动已取消时不再重试()
    {
        // 调用方取消时 Testcontainers 同样抛「Initialization has been cancelled.」，这种情况不能当作瞬时失败。
        using var cancellation = new CancellationTokenSource();
        await cancellation.CancelAsync();
        await using var fixture = new ScriptedFixture([RyukCancelled(), null]);

        await Assert.ThrowsAsync<ResourceReaperException>(() => fixture.InitializeAsync(cancellation.Token).AsTask());

        Assert.Single(fixture.Containers);
        Assert.Equal(1, fixture.Starting);
    }

    [Fact]
    public async Task 容器在就绪前退出只按StartAttempts重试()
    {
        await using (var undeclared = new ScriptedFixture([Exited(), null]))
        {
            await Assert.ThrowsAsync<ContainerNotRunningException>(() => undeclared.InitializeAsync(Ct).AsTask());
            Assert.Single(undeclared.Containers);
        }

        await using var declared = new ScriptedFixture([Exited(), null], attempts: 2);
        await declared.InitializeAsync(Ct);
        Assert.Equal([true, false], declared.Containers.Select(container => container.Disposed));
        Assert.Equal(1, declared.HeldDependencies);
    }

    private static ResourceReaperException RyukCancelled() => new("Initialization has been cancelled.");

    /// <summary>Docker 客户端请求超时时实际抛出的异常链（取自本机全量日志）。</summary>
    private static TaskCanceledException DockerRequestTimedOut() =>
        new("The operation was canceled.", new HttpRequestException("The requested failed, see inner exception for details.",
            new OperationCanceledException()));

    private static ContainerNotRunningException Exited() => new("simulated", string.Empty, "simulated exit", 2, null);

    /// <summary>
    /// 按脚本启动的 fixture：第 n 次创建的容器按 starts[n] 启动（null 为成功），startingFailures 依次在 OnStartingAsync 里抛出；
    /// 每次 OnStartingAsync 都准备一份依赖，由 OnDisposedAsync 全部释放。
    /// </summary>
    private sealed class ScriptedFixture : ContainerFixture<IContainer>
    {
        private readonly Queue<Exception?> _starts;
        private readonly Queue<Exception> _startingFailures;
        private readonly int _attempts;

        public ScriptedFixture(Exception?[] starts, Exception[]? startingFailures = null, int attempts = 1)
        {
            _starts = new(starts);
            _startingFailures = new(startingFailures ?? []);
            _attempts = attempts;
            RetryDelay = TimeSpan.Zero;
        }

        public List<FakeContainer> Containers { get; } = [];

        /// <summary>OnStartingAsync 的调用次数。</summary>
        public int Starting { get; private set; }

        /// <summary>OnStartedAsync 的调用次数。</summary>
        public int Started { get; private set; }

        /// <summary>OnStartingAsync 准备、尚未由 OnDisposedAsync 释放的依赖数。</summary>
        public int HeldDependencies { get; private set; }

        protected override int StartAttempts => _attempts;

        protected override ValueTask OnStartingAsync(CancellationToken cancellationToken)
        {
            Starting++;
            HeldDependencies++;
            return _startingFailures.TryDequeue(out var failure) ? ValueTask.FromException(failure) : ValueTask.CompletedTask;
        }

        protected override IContainer CreateContainer()
        {
            var container = FakeContainer.Create(_starts.Dequeue());
            Containers.Add(container);
            return container.AsContainer();
        }

        protected override ValueTask OnStartedAsync(IContainer container, CancellationToken cancellationToken)
        {
            Started++;
            return ValueTask.CompletedTask;
        }

        protected override ValueTask OnDisposedAsync()
        {
            HeldDependencies = 0;
            return ValueTask.CompletedTask;
        }
    }

    /// <summary>假容器：只实现 StartAsync 与 DisposeAsync，启动按给定结果成功或失败；调用其他成员即失败。</summary>
#pragma warning disable CA1852 // DispatchProxy 在运行时派生本类型，不能密封。
    private class FakeContainer : DispatchProxy
#pragma warning restore CA1852
    {
        private Exception? _failure;

        public bool Disposed { get; private set; }

        public static FakeContainer Create(Exception? failure)
        {
            var container = (FakeContainer)(object)DispatchProxy.Create<IContainer, FakeContainer>();
            container._failure = failure;
            return container;
        }

        public IContainer AsContainer() => (IContainer)this;

        protected override object? Invoke(MethodInfo? targetMethod, object?[]? args)
        {
            switch (targetMethod?.Name)
            {
                case nameof(IContainer.StartAsync):
                    return _failure is null ? Task.CompletedTask : Task.FromException(_failure);
                case nameof(IAsyncDisposable.DisposeAsync):
                    Disposed = true;
                    return ValueTask.CompletedTask;
                default:
                    throw new NotSupportedException($"假容器不支持 {targetMethod?.Name}。");
            }
        }
    }
}

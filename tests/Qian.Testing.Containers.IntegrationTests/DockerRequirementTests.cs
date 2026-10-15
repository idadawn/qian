using DotNet.Testcontainers.Builders;
using DotNet.Testcontainers.Containers;

namespace Qian.Testing.Containers.IntegrationTests;

/// <summary>单元测试：不需要 Docker，不进 SharedInfrastructure collection。</summary>
public sealed class DockerRequirementTests
{
    [Theory]
    [InlineData(null, null, false)]
    [InlineData("true", null, true)]
    [InlineData("TRUE", null, true)]
    [InlineData("1", null, true)]
    [InlineData("false", null, false)]
    [InlineData(null, "1", true)]
    [InlineData(null, "true", true)]
    [InlineData("false", "0", false)]
    public void Docker_is_required_on_ci_or_when_explicitly_requested(string? ci, string? require, bool expected)
    {
        var environment = new Dictionary<string, string?> { ["CI"] = ci, [DockerRequirement.RequireVariable] = require };
        Assert.Equal(expected, DockerRequirement.IsRequired(name => environment.GetValueOrDefault(name)));
    }

    [Fact]
    public async Task Missing_docker_skips_integration_tests_on_a_developer_machine()
    {
        string? skipped = null;
        await using var fixture = new NoDockerFixture { EnvironmentReader = _ => null, SkipAction = message => skipped = message };
        await fixture.InitializeAsync(TestContext.Current.CancellationToken);
        Assert.NotNull(skipped);
        Assert.Contains("跳过集成测试", skipped, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Missing_docker_fails_integration_tests_on_ci()
    {
        await using var fixture = new NoDockerFixture { EnvironmentReader = name => name == "CI" ? "true" : null };
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => fixture.InitializeAsync(TestContext.Current.CancellationToken).AsTask());
        Assert.IsType<DockerUnavailableException>(ex.InnerException);
    }

    /// <summary>模拟本机没有 Docker：构造容器时 Testcontainers 抛出 <see cref="DockerUnavailableException"/>。</summary>
    private sealed class NoDockerFixture : ContainerFixture<IContainer>
    {
        protected override IContainer CreateContainer() => throw new DockerUnavailableException("simulated: no Docker endpoint");
    }
}

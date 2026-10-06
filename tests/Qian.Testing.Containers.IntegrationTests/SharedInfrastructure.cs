namespace Qian.Testing.Containers.IntegrationTests;

/// <summary>整个程序集的集成测试共享一套容器；同一 collection 内的测试串行执行，互不抢占 Apollo 等全局状态。</summary>
[CollectionDefinition(Name)]
public sealed class SharedInfrastructure : ICollectionFixture<InfrastructureFixture>
{
    public const string Name = "shared-infrastructure";
}

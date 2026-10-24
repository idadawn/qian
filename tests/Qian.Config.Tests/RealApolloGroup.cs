using Qian.Testing.Containers;
using Xunit.Sdk;
using Xunit.v3;

namespace Qian.Config.Tests;

/// <summary>
/// 真实 Apollo 测试类共用的集合：一套 Portal 版真实 Apollo（MySQL + AdminService + ConfigService + Portal）供集合内各类使用，
/// 整个集合只启动一次（S1-QA-017）。Portal 版是超集，只用 ConfigService / AdminService 的类同样可用；类之间靠各自随机的 appId 隔离。
/// </summary>
[CollectionDefinition(Name)]
[TestClassOrderer(typeof(RealApolloClassOrderer))]
public sealed class RealApolloGroup : ICollectionFixture<RealApolloPortalFixture>
{
    /// <summary>集合名。</summary>
    public const string Name = "real-apollo";
}

/// <summary>
/// <see cref="RealApolloTests"/> 最后会停掉共用的 ConfigService 验证离线缓存，所以固定排在集合最后；其余类保持 xUnit 的缺省顺序。
/// 缺省顺序随程序集变化（同一份程序集内稳定），不能靠它把停服务的类排到最后。
/// </summary>
public sealed class RealApolloClassOrderer : ITestClassOrderer
{
    /// <inheritdoc />
    public IReadOnlyCollection<TTestClass?> OrderTestClasses<TTestClass>(IReadOnlyCollection<TTestClass?> testClasses)
        where TTestClass : ITestClass =>
        [.. DefaultTestClassOrderer.Instance.OrderTestClasses(testClasses)
            .OrderBy(testClass => testClass?.TestClassName == typeof(RealApolloTests).FullName)];
}

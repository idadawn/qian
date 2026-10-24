using Qian.Testing.Containers;
using Xunit.Sdk;
using Xunit.v3;

namespace Qian.Config.Tests;

/// <summary>
/// 真实 Apollo 测试类共用的集合：一套 Portal 版真实 Apollo（MySQL + AdminService + ConfigService + Portal）供集合内各类使用，
/// 整个集合只启动一次（S1-QA-017）。Portal 版是超集，只用 ConfigService / AdminService 的类同样可用。
/// 类之间并不都靠随机 appId 隔离：RealApolloTests、RealApolloReleaseAuditTests 与 RealApolloFeatureFlagsTests 用各自随机的 appId，
/// RealApolloPortalProtocolTests 用夹具固定的 AppId（qian-flags-acceptance）；它与 RealApolloFeatureFlagsTests 的属主应用各建一个
/// 同名公共 namespace qian.common.flags，先后由 <see cref="RealApolloClassOrderer"/> 决定。
/// </summary>
[CollectionDefinition(Name)]
[TestClassOrderer(typeof(RealApolloClassOrderer))]
public sealed class RealApolloGroup : ICollectionFixture<RealApolloPortalFixture>
{
    /// <summary>集合名。</summary>
    public const string Name = "real-apollo";
}

/// <summary>
/// 集合内类的先后：RealApolloFeatureFlagsTests → RealApolloPortalProtocolTests → 其余类（xUnit 缺省顺序）→ RealApolloTests。
/// <list type="bullet">
/// <item>RealApolloFeatureFlagsTests 的消费应用按名关联公共 namespace qian.common.flags。RealApolloPortalProtocolTests 先建了同名公共 namespace 时，
/// ConfigService 给关联方哪一个属主的发布是 Apollo 的实现细节（实测取最新建的那个）；FeatureFlags 排在前面，跑的时候只有它自己的属主（S1-QA-020）。</item>
/// <item>RealApolloTests 最后会停掉共用的 ConfigService 验证离线缓存，固定排在集合最后（S1-QA-017）。</item>
/// </list>
/// 缺省顺序取决于程序集路径的哈希（同一路径下稳定，本机与 CI 不同），不能靠它排先后。先后只在集合内串行时成立：依赖 xUnit 缺省的 ParallelMode.Collections，
/// 改成 4.0 起的 ParallelMode.All 会让同一集合里的类并行。
/// </summary>
public sealed class RealApolloClassOrderer : ITestClassOrderer
{
    /// <inheritdoc />
    public IReadOnlyCollection<TTestClass?> OrderTestClasses<TTestClass>(IReadOnlyCollection<TTestClass?> testClasses)
        where TTestClass : ITestClass =>
        [.. DefaultTestClassOrderer.Instance.OrderTestClasses(testClasses).OrderBy(testClass => Rank(testClass?.TestClassName))];

    private static int Rank(string? testClassName) => testClassName switch
    {
        _ when testClassName == typeof(RealApolloFeatureFlagsTests).FullName => 0,
        _ when testClassName == typeof(RealApolloPortalProtocolTests).FullName => 1,
        _ when testClassName == typeof(RealApolloTests).FullName => 3,
        _ => 2,
    };
}

using Qian.Runtime;

namespace Qian.Tenancy.Tests;

public sealed class TenantServiceKeyTests
{
    [Fact]
    public void 租户十个服务键符合单段语法且不重名()
    {
        ServiceKey[] keys = [TenantProvisioning.Key, TenantProvisioning.MembershipKey, TenantProvisioning.NotificationKey, TenantOperations.Key,
            TenantLifecycle.Key, TenantSettings.Key, TenantSettings.OperationsKey, TenantSettings.QuotaKey, TenantSettings.UserCounterKey, TenantSummaries.Key];
        Assert.Equal(10, keys.Select(key => key.Name).Distinct().Count());
        Assert.All(keys, key => Assert.Matches("^Tenancy[A-Za-z]+$", key.Name));
    }

}

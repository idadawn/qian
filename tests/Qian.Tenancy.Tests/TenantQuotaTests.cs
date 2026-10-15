using Qian.Runtime.Errors;

namespace Qian.Tenancy.Tests;

public sealed class TenantQuotaTests
{
    [Theory]
    [InlineData(TenantMemberKind.Member)] [InlineData(TenantMemberKind.Administrator)]
    public void 五人限额已满_普通成员与租户管理员均被拒绝(TenantMemberKind kind) => Assert.Equal("CARDINALITY_USER_LIMIT",
        Assert.Throws<QianErrorException>(() => TenantQuotaRules.Check(new TenantQuotas(5), 5, kind)).Error.Code.Code);
    [Fact]
    public void 平台超管不计入_未满与无限额放行_非法数量拒绝()
    {
        TenantQuotaRules.Check(new TenantQuotas(5), 5, TenantMemberKind.PlatformSuperadmin);
        TenantQuotaRules.Check(new TenantQuotas(5), 4, TenantMemberKind.Administrator);
        TenantQuotaRules.Check(new TenantQuotas(), 500, TenantMemberKind.Member);
        Assert.Throws<QianErrorException>(() => TenantQuotaRules.Check(new TenantQuotas(5), -1, TenantMemberKind.Member));
        Assert.Throws<QianErrorException>(() => TenantQuotaRules.Serialize(new TenantQuotas(-1)));
    }
}

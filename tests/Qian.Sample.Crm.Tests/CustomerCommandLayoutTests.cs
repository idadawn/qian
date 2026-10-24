using System.Reflection;
using Xunit.v3;

namespace Qian.Sample.Crm.Tests;

// 基类守卫（S1-QA-020）：CustomerCommandTests 是八个主题类共用的抽象基类，xUnit 按继承发现测试方法，写在基类里的用例会在八个主题类各跑一遍。
// 漏写 sealed 的 partial class CustomerCommandTests 能静默编译通过（abstract 写在其他部分上同样生效），里面的用例就进了基类，编译器不报错，由这里拦下。
public sealed class CustomerCommandLayoutTests
{
    [Fact]
    public void CustomerCommandTests基类不含测试方法_用例只写在主题类里()
    {
        Assert.Empty(TestMethods(typeof(CustomerCommandTests)));
        // 判定本身有效：主题类自己声明的用例能被认出来。
        Assert.NotEmpty(TestMethods(typeof(CommandTests)));
    }

    private static IEnumerable<string> TestMethods(Type type) =>
        type.GetMethods(BindingFlags.DeclaredOnly | BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic)
            .Where(method => method.GetCustomAttributes(inherit: true).OfType<IFactAttribute>().Any())
            .Select(method => method.Name);
}

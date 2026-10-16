using System.Reflection;
using System.Runtime.ExceptionServices;
using Kun.Authorization;
using Qian.Authz.Abstractions;
using Qian.Runtime;

namespace Qian.Testing;

// 直接验证发布程序集的内部实现；不为测试程序集授予 InternalsVisibleTo，也不复制授权算法。
internal static class TenantInitializationAccess
{
    private static readonly Type Target = typeof(AuthzDefinition).Assembly.GetType("Qian.Authz.Abstractions.TenantInitializationAuthorization", true)!;
    private static object? Call(string name, object?[] args, Type? generic = null)
    {
        var method = Target.GetMethods(BindingFlags.NonPublic | BindingFlags.Static).Single(method =>
            method.Name == name && method.IsGenericMethodDefinition == (generic is not null));
        if (generic is not null) method = method.MakeGenericMethod(generic);
        try { return method.Invoke(null, args); }
        catch (TargetInvocationException error) when (error.InnerException is not null)
        { ExceptionDispatchInfo.Capture(error.InnerException).Throw(); throw; }
    }
    internal static IDisposable Begin(IPluginContext source, string actor, IReadOnlySet<string> capabilities) => (IDisposable)Call("Begin", [source, actor, capabilities, "user"])!;
    internal static IDisposable? Enter(IPluginContext source, string capability, Type requestType, IReadOnlyList<string> permissions) => (IDisposable?)Call("Enter", [source, capability, requestType, permissions]);
    internal static bool Allows(string capability, ScopeKey scope) => (bool)Call("Allows", [capability, scope])!;
    internal static bool Allows<T>() where T : IPermissionProtectedRequest => (bool)Call("Allows", [], typeof(T))!;
    internal static void Claim<T>(string capability, ScopeKey scope) where T : IPermissionProtectedRequest => Call("Claim", [capability, scope], typeof(T));
    internal static string Actor => (string)Call("get_Actor", [])!;
    internal static string Tenant => (string)Call("get_Tenant", [])!;
}

using Qian.Runtime.Errors;

namespace Qian.Audit;

/// <summary>原生审计 Provider 的安全装配错误，不包含连接凭据。</summary>
public static class NativeAuditErrors
{
    /// <summary>原生审计的角色、Schema、函数或固定安全策略与迁移声明不一致。</summary>
    public static readonly ErrorCode Misconfigured = ErrorCode.Protocol("NATIVE_AUDIT_MISCONFIGURED", 500, "qian.audit.native",
        new Dictionary<string, string> { ["zh-CN"] = "原生审计装配无效" },
        new Dictionary<string, string> { ["zh-CN"] = "原生审计的绑定、专用角色、表结构或安全函数不满足声明。" });
}

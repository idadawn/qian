namespace Qian.Runtime.Errors;

/// <summary>错误码类别（contracts/v0/error-catalog.schema.json 的 <c>kind</c>）。</summary>
public enum ErrorKind
{
    /// <summary>
    /// 协议错误：请求未进入业务语义即被拒——解析、契约校验、认证、鉴权、寻址、限流、服务端故障。
    /// 状态码不用 409 / 422。
    /// </summary>
    Protocol,

    /// <summary>业务错误：请求合法且已授权，业务规则拒绝。状态码只用 409（状态冲突）或 422（规则不满足）。</summary>
    Business,

    /// <summary>字段级错误码：只出现在 <c>errors[]</c> 里，不能作为响应的顶层 <c>code</c>。</summary>
    Field,
}

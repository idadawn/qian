using Qian.Runtime.Errors;

namespace Qian.Capabilities;

/// <summary>能力组件拥有的错误码。</summary>
public static class CapabilitiesErrors
{
    /// <summary>
    /// 同一幂等键的调用已执行成功，但回执按策略没有保存响应体，无法原样重放；不再执行。文案参数 <c>reason</c> 为
    /// <c>response_too_large</c>（响应超过回执上限）或 <c>data_classification</c>（能力的数据分级为 pii、finance 或 secret）。
    /// 回执过期（24 小时）前，同一主体、同一能力、同一键的请求都得到该错误。
    /// </summary>
    public static readonly ErrorCode ReplayUnavailable = ErrorCode.Business("CAPABILITY_REPLAY_UNAVAILABLE", 409, "qian.capabilities",
        new Dictionary<string, string> { ["zh-CN"] = "调用结果不可重放", ["en"] = "Invocation result cannot be replayed" },
        new Dictionary<string, string>
        {
            ["zh-CN"] = "该幂等键的调用已执行成功，但回执没有保存响应体（{reason}），无法原样重放。请通过查询确认结果，不要换用新的幂等键重试。",
            ["en"] = "The call with this idempotency key already succeeded, but its receipt does not keep the response body ({reason}), so it cannot be replayed. Confirm the result with a query instead of retrying with a new idempotency key.",
        });

    /// <summary>登记全部组件错误，释放时撤销。</summary>
    public static IDisposable Register(ErrorCatalog catalog)
    {
        ArgumentNullException.ThrowIfNull(catalog);
        return catalog.Register(ReplayUnavailable);
    }
}

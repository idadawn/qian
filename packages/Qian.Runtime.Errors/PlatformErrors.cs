namespace Qian.Runtime.Errors;

/// <summary>
/// 平台内置错误码（归属 <c>qian.runtime</c>），冻结在 contracts/v0/error-codes.json。
/// 协议错误覆盖宿主框架会产生的全部状态码；业务错误只有两个通用兜底，具体业务错误码由各组件在 Definition 包登记。
/// </summary>
public static class PlatformErrors
{
    /// <summary>内置错误码的归属组件。</summary>
    public const string Owner = "qian.runtime";

    /// <summary>400：启动配置引用了没有 Provider 的服务键。</summary>
    public static readonly ErrorCode DependencyMissing = Protocol("DEPENDENCY_MISSING", 400,
        "缺少服务依赖", "条目 {entry} 请求的服务键 {key} 无人提供。",
        "Missing dependency", "Entry {entry} requires service key {key}, which has no provider.");

    /// <summary>400：启动配置中的依赖成环。</summary>
    public static readonly ErrorCode DependencyCycle = Protocol("DEPENDENCY_CYCLE", 400,
        "服务依赖成环", "服务依赖存在环路：{cycle}。",
        "Dependency cycle", "Service dependencies contain a cycle: {cycle}.");

    /// <summary>409：同 scope 同名注册已存在。</summary>
    public static readonly ErrorCode RegistrationConflict = Business("REGISTRATION_CONFLICT", 409,
        "注册冲突", "注册 {key} 已由条目 {owner} 占用。",
        "Registration conflict", "Registration {key} is already owned by entry {owner}.");

    // ---- 协议错误 ----

    /// <summary>400：请求无法解析（JSON 语法、内容类型、参数类型）。</summary>
    public static readonly ErrorCode BadRequest = Protocol("BAD_REQUEST", 400,
        "请求无法解析", "请求格式不正确，无法解析。检查 JSON 语法、Content-Type 与参数类型后重试。",
        "Malformed request", "The request could not be parsed. Check the JSON syntax, Content-Type and parameter types, then retry.");

    /// <summary>400：输入不符合能力的输入 Schema，逐项见 errors。</summary>
    public static readonly ErrorCode InputInvalid = Protocol("INPUT_INVALID", 400,
        "输入不符合契约", "输入不符合该能力的输入 Schema，逐项错误见 errors。",
        "Input does not match the contract", "The input does not match the capability's input schema; see errors for each violation.");

    /// <summary>401：缺少有效凭据或令牌已失效。</summary>
    public static readonly ErrorCode Unauthenticated = Protocol("UNAUTHENTICATED", 401,
        "未认证", "缺少有效凭据或令牌已失效，请重新认证。",
        "Unauthenticated", "Credentials are missing or no longer valid; authenticate and retry.");

    /// <summary>403：当前主体无权执行该操作。</summary>
    public static readonly ErrorCode AuthzDenied = Protocol("AUTHZ_DENIED", 403,
        "无权执行", "当前主体无权执行该操作。可调用权限解释接口查看缺少的权限点。",
        "Permission denied", "The caller is not permitted to perform this operation. Call the authorization explain API to see which permission is missing.");

    /// <summary>403：令牌缺少该操作所需的 scope（WWW-Authenticate 头给出所需 scope）。</summary>
    public static readonly ErrorCode InsufficientScope = Protocol("INSUFFICIENT_SCOPE", 403,
        "令牌 scope 不足", "令牌缺少该操作所需的 scope，按 WWW-Authenticate 头中的 scope 申请新令牌后重试。",
        "Insufficient scope", "The token lacks a scope this operation requires; obtain a token with the scope named in the WWW-Authenticate header and retry.");

    /// <summary>404：资源不存在或对调用者不可见（未挂载与不存在同此响应，R32）。</summary>
    public static readonly ErrorCode NotFound = Protocol("NOT_FOUND", 404,
        "资源不存在", "请求的资源不存在或对当前调用者不可见。",
        "Not found", "The requested resource does not exist or is not visible to the caller.");

    /// <summary>405：资源不支持此 HTTP 方法。</summary>
    public static readonly ErrorCode MethodNotAllowed = Protocol("METHOD_NOT_ALLOWED", 405,
        "不支持该方法", "该资源不支持此 HTTP 方法，允许的方法见 Allow 头。",
        "Method not allowed", "The resource does not support this HTTP method; see the Allow header.");

    /// <summary>406：无法按 Accept 头返回。</summary>
    public static readonly ErrorCode NotAcceptable = Protocol("NOT_ACCEPTABLE", 406,
        "无法提供所需格式", "无法按 Accept 头要求的媒体类型返回结果。",
        "Not acceptable", "The response cannot be produced in a media type the Accept header allows.");

    /// <summary>408：请求超时。</summary>
    public static readonly ErrorCode RequestTimeout = Protocol("REQUEST_TIMEOUT", 408,
        "请求超时", "请求处理超时，稍后以同一幂等键重试。",
        "Request timeout", "The request timed out; retry later with the same idempotency key.",
        retryable: true);

    /// <summary>412：If-Match 等前置条件不成立。</summary>
    public static readonly ErrorCode PreconditionFailed = Protocol("PRECONDITION_FAILED", 412,
        "前置条件不满足", "资源已被修改，If-Match 等前置条件不成立。重新读取后再决定是否提交。",
        "Precondition failed", "The resource has changed and a precondition such as If-Match does not hold. Read it again before resubmitting.");

    /// <summary>413：请求体过大。</summary>
    public static readonly ErrorCode PayloadTooLarge = Protocol("PAYLOAD_TOO_LARGE", 413,
        "请求体过大", "请求体超过允许的大小。",
        "Payload too large", "The request body exceeds the allowed size.");

    /// <summary>415：不支持的 Content-Type。</summary>
    public static readonly ErrorCode UnsupportedMediaType = Protocol("UNSUPPORTED_MEDIA_TYPE", 415,
        "不支持的内容类型", "不支持请求的 Content-Type，请使用 application/json。",
        "Unsupported media type", "The request Content-Type is not supported; use application/json.");

    /// <summary>429：超出限流阈值（Retry-After 头给出等待时间）。</summary>
    public static readonly ErrorCode RateLimited = Protocol("RATE_LIMITED", 429,
        "请求过于频繁", "超出限流阈值，按 Retry-After 头等待后重试。",
        "Too many requests", "The rate limit was exceeded; wait as the Retry-After header indicates, then retry.",
        retryable: true);

    /// <summary>500：服务内部错误（不暴露异常细节）。</summary>
    public static readonly ErrorCode Internal = Protocol("INTERNAL", 500,
        "服务内部错误", "服务内部错误。请凭 trace_id 联系运维排查。",
        "Internal error", "An internal error occurred. Report the trace_id to the operators.");

    /// <summary>502：上游服务返回无效响应。</summary>
    public static readonly ErrorCode BadGateway = Protocol("BAD_GATEWAY", 502,
        "上游服务错误", "上游服务返回了无效响应，稍后重试。",
        "Bad gateway", "An upstream service returned an invalid response; retry later.",
        retryable: true);

    /// <summary>503：服务暂不可用。</summary>
    public static readonly ErrorCode Unavailable = Protocol("UNAVAILABLE", 503,
        "服务暂不可用", "服务暂不可用，稍后重试。",
        "Service unavailable", "The service is temporarily unavailable; retry later.",
        retryable: true);

    /// <summary>504：上游服务超时。</summary>
    public static readonly ErrorCode GatewayTimeout = Protocol("GATEWAY_TIMEOUT", 504,
        "上游服务超时", "上游服务未及时响应，稍后以同一幂等键重试。",
        "Gateway timeout", "An upstream service did not respond in time; retry later with the same idempotency key.",
        retryable: true);

    // ---- 业务错误（通用兜底） ----

    /// <summary>409：资源状态已变化或与现有数据冲突（并发修改、唯一键重复）。</summary>
    public static readonly ErrorCode Conflict = Business("CONFLICT", 409,
        "状态冲突", "资源状态已变化或与现有数据冲突。重新读取最新状态后再决定是否重试。",
        "Conflict", "The resource state has changed or conflicts with existing data. Read the latest state before deciding whether to retry.");

    /// <summary>422：请求合法但被业务规则拒绝；组件应优先登记更具体的业务错误码。</summary>
    public static readonly ErrorCode BusinessRuleViolated = Business("BUSINESS_RULE_VIOLATED", 422,
        "违反业务规则", "请求合法，但被业务规则拒绝。",
        "Business rule violated", "The request is valid but was rejected by a business rule.");

    // ---- 字段级错误（只用于 errors[]，与 JSON Schema 关键字对应） ----

    /// <summary>缺少必填项（<c>required</c>）。</summary>
    public static readonly ErrorCode Required = Field("REQUIRED",
        "缺少必填项", "缺少必填项。",
        "Required", "This member is required.");

    /// <summary>类型不符（<c>type</c>）。</summary>
    public static readonly ErrorCode TypeMismatch = Field("TYPE_MISMATCH",
        "类型不符", "取值类型不符合 Schema。",
        "Type mismatch", "The value has the wrong type for the schema.");

    /// <summary>取值不在允许集合内（<c>enum</c> / <c>const</c>），可选值见 allowed。</summary>
    public static readonly ErrorCode ValueNotAllowed = Field("VALUE_NOT_ALLOWED",
        "取值不允许", "取值不在允许的集合内，可选值见 allowed。",
        "Value not allowed", "The value is not one of the allowed values; see allowed.");

    /// <summary>取值或长度超出范围（<c>minimum</c> / <c>maximum</c> / <c>minLength</c> / <c>maxLength</c> / <c>minItems</c> / <c>maxItems</c>）。</summary>
    public static readonly ErrorCode OutOfRange = Field("OUT_OF_RANGE",
        "超出范围", "取值或长度超出允许范围。",
        "Out of range", "The value or its length is outside the allowed range.");

    /// <summary>不匹配模式（<c>pattern</c>）。</summary>
    public static readonly ErrorCode PatternMismatch = Field("PATTERN_MISMATCH",
        "格式不符", "取值不符合要求的模式。",
        "Pattern mismatch", "The value does not match the required pattern.");

    /// <summary>不是合法的声明格式（<c>format</c>，如 uuid、date-time、email）。</summary>
    public static readonly ErrorCode FormatInvalid = Field("FORMAT_INVALID",
        "格式无效", "取值不符合声明的格式（如 uuid、date-time、email）。",
        "Invalid format", "The value is not valid for its declared format (such as uuid, date-time or email).");

    /// <summary>含 Schema 未声明的成员（<c>additionalProperties: false</c>）。</summary>
    public static readonly ErrorCode UnknownProperty = Field("UNKNOWN_PROPERTY",
        "未知字段", "输入包含 Schema 未声明的成员。",
        "Unknown property", "The input contains a member the schema does not declare.");

    /// <summary>全部内置错误码。</summary>
    public static IReadOnlyList<ErrorCode> All { get; } =
    [
        DependencyMissing, DependencyCycle, RegistrationConflict,
        BadRequest, InputInvalid, Unauthenticated, AuthzDenied, InsufficientScope, NotFound, MethodNotAllowed,
        NotAcceptable, RequestTimeout, PreconditionFailed, PayloadTooLarge, UnsupportedMediaType, RateLimited,
        Internal, BadGateway, Unavailable, GatewayTimeout,
        Conflict, BusinessRuleViolated,
        Required, TypeMismatch, ValueNotAllowed, OutOfRange, PatternMismatch, FormatInvalid, UnknownProperty,
    ];

    /// <summary>
    /// 某个 HTTP 状态码的兜底错误码：用于宿主框架自己产生、没有带错误码的错误响应。
    /// 没有专门错误码的 4xx 落到 <see cref="BadRequest"/>、5xx 落到 <see cref="Internal"/>，响应状态码保持原值。
    /// </summary>
    public static ErrorCode ForStatus(int status) => status switch
    {
        400 => BadRequest,
        401 => Unauthenticated,
        403 => AuthzDenied,
        404 => NotFound,
        405 => MethodNotAllowed,
        406 => NotAcceptable,
        408 => RequestTimeout,
        409 => Conflict,
        412 => PreconditionFailed,
        413 => PayloadTooLarge,
        415 => UnsupportedMediaType,
        422 => BusinessRuleViolated,
        429 => RateLimited,
        502 => BadGateway,
        503 => Unavailable,
        504 => GatewayTimeout,
        >= 500 => Internal,
        _ => BadRequest,
    };

    private static ErrorCode Protocol(string code, int status, string zhTitle, string zhMessage, string enTitle, string enMessage, bool retryable = false)
        => ErrorCode.Protocol(code, status, Owner, Text(zhTitle, enTitle), Text(zhMessage, enMessage), retryable);

    private static ErrorCode Business(string code, int status, string zhTitle, string zhMessage, string enTitle, string enMessage)
        => ErrorCode.Business(code, status, Owner, Text(zhTitle, enTitle), Text(zhMessage, enMessage));

    private static ErrorCode Field(string code, string zhTitle, string zhMessage, string enTitle, string enMessage)
        => ErrorCode.Field(code, Owner, Text(zhTitle, enTitle), Text(zhMessage, enMessage));

    private static Dictionary<string, string> Text(string zh, string en) => new(StringComparer.Ordinal)
    {
        [ErrorCode.DefaultLocale] = zh,
        ["en"] = en,
    };
}

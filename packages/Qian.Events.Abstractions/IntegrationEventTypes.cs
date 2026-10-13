using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Text.RegularExpressions;

namespace Qian.Events;

/// <summary>稳定事件 ID（<c>[IntegrationEventId]</c> 的值）与信封 subject 的换算规则。</summary>
/// <remarks>事件 ID 形如 <c>crm.customer.created.v1</c>：subject 为去掉末尾 <c>.v&lt;N&gt;</c> 的部分，也是 RabbitMQ 路由键。</remarks>
public static partial class IntegrationEventTypes
{
    /// <summary>事件 ID 的最大长度，与信封 Schema 的 <c>type</c> 一致。</summary>
    public const int MaxTypeLength = 210;

    /// <summary>subject 的最大长度，与信封 Schema 一致。</summary>
    public const int MaxSubjectLength = 200;

    /// <summary>解析事件 ID；不合法时返回 false，不抛异常。</summary>
    public static bool TryParse(string? type, [NotNullWhen(true)] out string? subject, out int version)
    {
        subject = null;
        version = 0;
        if (type is null || type.Length > MaxTypeLength) return false;
        var match = TypePattern().Match(type);
        if (!match.Success || match.Groups["subject"].Value.Length > MaxSubjectLength ||
            !int.TryParse(match.Groups["version"].ValueSpan, NumberStyles.None, CultureInfo.InvariantCulture, out version)) return false;
        subject = match.Groups["subject"].Value;
        return true;
    }

    /// <summary>返回事件 ID 对应的 subject；不合法时以 <see cref="EventsErrors.PublishRejected"/> 拒绝。</summary>
    public static string SubjectOf(string type) => TryParse(type, out var subject, out _)
        ? subject
        : throw QianEventsException.Rejected("事件 ID 必须为小写点分的 <subject>.v<N>，subject 至少两段。");

    /// <summary>subject 是否符合 <c>&lt;domain&gt;.&lt;entity&gt;.&lt;verb&gt;</c> 命名（至少两段）。</summary>
    public static bool IsSubject(string? subject) => subject is { Length: > 0 and <= MaxSubjectLength } && SubjectPattern().IsMatch(subject);

    [GeneratedRegex("^(?<subject>[a-z][a-z0-9_-]*(\\.[a-z][a-z0-9_-]*)+)\\.v(?<version>[1-9][0-9]{0,8})$", RegexOptions.CultureInvariant)]
    private static partial Regex TypePattern();

    [GeneratedRegex("^[a-z][a-z0-9_-]*(\\.[a-z][a-z0-9_-]*)+$", RegexOptions.CultureInvariant)]
    private static partial Regex SubjectPattern();
}

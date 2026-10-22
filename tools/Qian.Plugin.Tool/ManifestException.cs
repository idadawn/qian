using Qian.Runtime.Errors;

namespace Qian.Plugin.Tool;

internal sealed class ManifestException(ErrorCode code, string reason, string pointer) : Exception(reason)
{
    internal ErrorCode Code { get; } = code;
    internal string Pointer { get; } = pointer;
}

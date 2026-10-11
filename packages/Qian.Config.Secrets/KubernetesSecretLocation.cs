using System.Text.RegularExpressions;
using Qian.Runtime.Errors;

namespace Qian.Config.Secrets;

/// <summary>可信引导中的 Kubernetes 文件定位，不作为公开组件引用。</summary>
public sealed partial class KubernetesSecretLocation
{
    /// <summary>创建 namespace/Secret/key 定位，所有部分只允许单个合法名称。</summary>
    public KubernetesSecretLocation(string @namespace, string name, string key)
    {
        if (@namespace is null || @namespace.Length > 63 || !NamespacePattern().IsMatch(@namespace) ||
            name is null || name.Length > 253 || !NamePattern().IsMatch(name) || name.Split('.').Any(part => part.Length > 63 || !NamespacePattern().IsMatch(part)) ||
            key is null || key.Length > 253 || key is "." or ".." || !KeyPattern().IsMatch(key))
            throw new QianErrorException(new QianError(SecretErrors.InvalidReference));
        Namespace = @namespace; Name = name; Key = key;
    }
    /// <summary>Kubernetes namespace。</summary>
    public string Namespace { get; }
    /// <summary>Secret 资源名。</summary>
    public string Name { get; }
    /// <summary>投射文件键。</summary>
    public string Key { get; }
    /// <summary>兼容任务中的底层 secretRef://k8s/namespace/name#key 定位。</summary>
    public static KubernetesSecretLocation Parse(string locator)
    {
        if (locator is null || !locator.StartsWith("secretRef://k8s/", StringComparison.Ordinal) || locator.Contains('%', StringComparison.Ordinal))
            throw new QianErrorException(new QianError(SecretErrors.InvalidReference));
        var pieces = locator["secretRef://k8s/".Length..].Split('#');
        var names = pieces[0].Split('/');
        if (pieces.Length != 2 || names.Length != 2)
            throw new QianErrorException(new QianError(SecretErrors.InvalidReference));
        return new KubernetesSecretLocation(names[0], names[1], pieces[1]);
    }
    /// <inheritdoc />
    public override string ToString() => "secretRef://k8s/" + Namespace + "/" + Name + "#" + Key;
    [GeneratedRegex(@"\A[a-z0-9](?:[a-z0-9-]*[a-z0-9])?\z", RegexOptions.CultureInvariant | RegexOptions.NonBacktracking)]
    private static partial Regex NamespacePattern();
    [GeneratedRegex(@"\A[a-z0-9](?:[a-z0-9.-]*[a-z0-9])?\z", RegexOptions.CultureInvariant | RegexOptions.NonBacktracking)]
    private static partial Regex NamePattern();
    [GeneratedRegex(@"\A[A-Za-z0-9._-]+\z", RegexOptions.CultureInvariant | RegexOptions.NonBacktracking)]
    private static partial Regex KeyPattern();
}

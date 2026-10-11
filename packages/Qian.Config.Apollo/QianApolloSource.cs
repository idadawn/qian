using Kun.Configuration.Apollo;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Primitives;

namespace Qian.Config.Apollo;

internal sealed class QianApolloSource(ApolloOptions options, string environment, QianConfigurationOptions owner) : IConfigurationSource
{
    public IConfigurationProvider Build(IConfigurationBuilder builder) => new QianApolloProvider(options, environment, owner);
}

internal sealed class QianApolloProvider(ApolloOptions options, string environment, QianConfigurationOptions owner) : IConfigurationProvider, IDisposable
{
    private readonly ApolloConfigurationProvider _inner = new(options, owner.HttpClient, owner.Logger);
    internal string AppId => options.AppId;
    internal string Environment => environment;
    internal IReadOnlyList<Uri> Servers => options.ConfigServers;
    internal IReadOnlyList<string> Namespaces => options.Namespaces;
    public bool TryGet(string key, out string? value) => _inner.TryGet(key, out value);
    public void Set(string key, string? value) => _inner.Set(key, value);
    public IChangeToken GetReloadToken() => _inner.GetReloadToken();
    public void Load() => _inner.Load();
    public IEnumerable<string> GetChildKeys(IEnumerable<string> earlierKeys, string? parentPath) => _inner.GetChildKeys(earlierKeys, parentPath);
    public void Dispose() => _inner.Dispose();
}

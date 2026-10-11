using System.Collections.ObjectModel;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Primitives;
using Qian.Runtime.Errors;

namespace Qian.Config.Apollo;

internal sealed class ConfigurationService(IConfiguration configuration) : IConfigService
{
    public string? GetValue(string key)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);
        return configuration[key];
    }
    public string GetRequiredValue(string key) => GetValue(key) is { Length: > 0 } value ? value :
        throw new QianErrorException(new QianError(ConfigErrors.RequiredMissing, new Dictionary<string, string> { ["key"] = key }));
    public IReadOnlyDictionary<string, string?> ReadSection(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        var values = new SortedDictionary<string, string?>(StringComparer.OrdinalIgnoreCase);
        foreach (var pair in configuration.GetSection(path).AsEnumerable(makePathsRelative: true))
            if (pair.Value is not null) values[pair.Key] = pair.Value;
        return new ReadOnlyDictionary<string, string?>(values);
    }
    public IChangeToken GetReloadToken() => configuration.GetReloadToken();
}

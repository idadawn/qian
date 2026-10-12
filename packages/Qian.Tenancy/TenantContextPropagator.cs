using System.Text.Json;
using Kun.Core.MultiTenancy;
using Qian.Runtime.Errors;

namespace Qian.Tenancy;

/// <summary>复用唯一 ITenantContext；传播字段只携带租户，不授予调用权限。</summary>
public sealed class TenantContextPropagator(ITenantContext tenant) : ITenantContextPropagator
{
    /// <inheritdoc />
    public JsonElement Write(JsonElement carrier)
    {
        var id = RequireCurrent();
        if (carrier.ValueKind != JsonValueKind.Object) throw Denied();
        var fields = carrier.EnumerateObject().Where(p => p.Name == "tenant_id").ToArray();
        if (fields.Length > 1 || (fields.Length == 1 && Read(fields[0].Value) != id)) throw Denied();
        using var stream = new MemoryStream();
        using (var writer = new Utf8JsonWriter(stream))
        {
            writer.WriteStartObject();
            foreach (var field in carrier.EnumerateObject()) if (field.Name != "tenant_id") field.WriteTo(writer);
            writer.WriteString("tenant_id", id); writer.WriteEndObject();
        }
        using var result = JsonDocument.Parse(stream.ToArray()); return result.RootElement.Clone();
    }
    /// <inheritdoc />
    public IReadOnlyDictionary<string, string> WriteHeaders() =>
        new System.Collections.ObjectModel.ReadOnlyDictionary<string, string>(new Dictionary<string, string> { ["tenant_id"] = RequireCurrent().ToString("D") });
    /// <inheritdoc />
    public IDisposable BeginScope(JsonElement carrier, Guid authorizedTenantId)
    {
        if (carrier.ValueKind != JsonValueKind.Object) throw Denied();
        var fields = carrier.EnumerateObject().Where(p => p.Name == "tenant_id").ToArray();
        if (fields.Length != 1) throw Denied();
        return Enter(Read(fields[0].Value), authorizedTenantId);
    }
    /// <inheritdoc />
    public IDisposable BeginScope(IEnumerable<KeyValuePair<string, string>> headers, Guid authorizedTenantId)
    {
        ArgumentNullException.ThrowIfNull(headers);
        var values = headers.Where(p => p.Key.Equals("tenant_id", StringComparison.OrdinalIgnoreCase)).ToArray();
        if (values.Length != 1) throw Denied();
        return Enter(Parse(values[0].Value), authorizedTenantId);
    }
    private Guid RequireCurrent() => Parse(tenant.TenantId);
    private IDisposable Enter(Guid id, Guid authorized)
    {
        if (id != authorized || authorized == Guid.Empty || (tenant.TenantId is { } current && Parse(current) != id)) throw Denied();
        return tenant.BeginScope(id.ToString("D"));
    }
    private static Guid Read(JsonElement field) => field.ValueKind == JsonValueKind.String ? Parse(field.GetString()) : throw Denied();
    internal static Guid Parse(string? value) => Guid.TryParseExact(value, "D", out var id) && id != Guid.Empty ? id : throw Denied();
    internal static QianErrorException Denied() => new(new QianError(PlatformErrors.Unauthenticated));
}

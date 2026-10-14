using System.Runtime.CompilerServices;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Qian.Plugin.Sdk;
using Qian.Runtime.Errors;
using Qian.Surfaces.Abstractions;

namespace Qian.Capabilities;

// 登记期与目录共用的能力契约检查：生成目录条目并预编译输入校验器，结果按契约实例缓存。登记期（SDK ProvideCapability）不合格即抛出；
// 宿主未登记检查时，目录在这里才发现不合格的契约，只排除该能力并记一次告警，不让一个坏契约拖垮整个目录。
internal sealed partial class CapabilityContractCheck(ISurfaceReader schemas, IToolContractHasher hasher, ILogger? logger = null) : ICapabilityContractCheck
{
    private readonly ConditionalWeakTable<CapabilityContract, Outcome> _outcomes = new();

    internal CapabilityInputValidator Inputs { get; } = new(logger);

    public void Check(CapabilityContract contract)
    {
        ArgumentNullException.ThrowIfNull(contract);
        if (Resolve(contract).Rejection is { } rejection) throw rejection.Raise(contract);
    }

    // 目录与执行入口用：合格的契约返回缓存的条目，不合格的返回 null。
    internal JsonElement? Entry(CapabilityContract contract) => Resolve(contract).Entry;

    // 为已生成的条目预编译输入校验器；位置以能力 ID 为文件，指向条目内输入 Schema 的出错处。
    internal void Prepare(string componentId, string name, JsonElement entry)
    {
        try { Inputs.Prepare(entry.GetProperty("schemaSha256").GetString()!, entry.GetProperty("inputSchema")); }
        catch (CapabilityInputSchemaException error)
        { throw new CapabilityContractException(componentId, name, CapabilityContractCompiler.Locate(componentId + "/" + name, ["inputSchema", .. error.Path]), error.Rule, error); }
    }

    private Outcome Resolve(CapabilityContract contract) => _outcomes.GetValue(contract, Compute);

    private Outcome Compute(CapabilityContract contract)
    {
        CapabilityContractException rejected;
        try
        {
            var entry = CapabilityContractCompiler.Describe(contract, schemas, hasher);
            Prepare(contract.ComponentId, contract.Name, entry);
            return new Outcome(entry, null);
        }
        catch (CapabilityContractException error) { rejected = error; }
        // 元数据读不成条目等其余失败同样按不合格处理，不外溢成目录请求的失败。
        catch (Exception error) when (error is QianErrorException or JsonException or ArgumentException or InvalidOperationException or KeyNotFoundException)
        { rejected = new CapabilityContractException(contract.ComponentId, contract.Name, CapabilityContractCompiler.Locate(contract.Id, []), "contract-invalid", error); }
        if (logger is not null) Log.Rejected(logger, contract.Id, rejected.Rule, rejected.Location);
        return new Outcome(null, new Rejection(rejected.Location, rejected.Rule, rejected.InnerException));
    }

    private sealed record Outcome(JsonElement? Entry, Rejection? Rejection);

    private sealed record Rejection(string Location, string Rule, Exception? Inner)
    {
        internal CapabilityContractException Raise(CapabilityContract contract) => new(contract.ComponentId, contract.Name, Location, Rule, Inner);
    }

    private static partial class Log
    {
        [LoggerMessage(EventId = 1, Level = LogLevel.Warning, Message = "能力 {Capability} 的契约不合格（{Rule}），位置 {Location}；该能力不登记、不进入目录。")]
        public static partial void Rejected(ILogger logger, string capability, string rule, string location);
    }
}

using Qian.Runtime;

namespace Qian.Plugin.Sdk;

/// <summary>将已有能力请求声明为可持久调度的 local 作业；code 在组件内唯一。</summary>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Struct, Inherited = false)]
public sealed class QianJobAttribute(string code) : Attribute
{
    /// <summary>组件内稳定作业代码。</summary>
    public string Code { get; } = code;
}

/// <summary>源生成器为 QianJob 请求实现的静态声明，不使用反射寻找处理器。</summary>
public interface IJobRequest<TResponse> : ICapabilityRequest<TResponse>
{
    /// <summary>声明的作业代码。</summary>
    static abstract string QianJobCode { get; }
}

/// <summary>绑定真实能力的不可变作业目录项；能力本身仍负责执行策略。</summary>
public sealed class JobContract
{
    /// <summary>创建代码声明，保留源生成能力的输入输出 Schema。</summary>
    public JobContract(string code, CapabilityContract capability)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(code); ArgumentNullException.ThrowIfNull(capability);
        if (code.Length > 100 || !System.Text.RegularExpressions.Regex.IsMatch(code, "^[a-z][a-z0-9-]*$", System.Text.RegularExpressions.RegexOptions.CultureInvariant, TimeSpan.FromSeconds(1)))
            throw new ArgumentException("作业代码必须是小写短横线名称。", nameof(code));
        Code = code; Capability = capability;
    }
    /// <summary>组件内作业代码。</summary>
    public string Code { get; }
    /// <summary>真正注册并执行的能力。</summary>
    public CapabilityContract Capability { get; }
}

/// <summary>local 作业的可逆注册；独立宿主与平台目录消费同一能力绑定。</summary>
public static class JobBindingRegistration
{
    /// <summary>仅包含显式 QianJob 声明的目录，名称为能力 URI。</summary>
    public static readonly ServiceKey Key = new("LocalJobs");

    /// <summary>同时登记真实能力及其可调度标记；两者均随当前 Apply 撤销。</summary>
    public static IDisposable ProvideJob<TRequest, TResponse>(this IPluginContext context) where TRequest : IJobRequest<TResponse>
    {
        ArgumentNullException.ThrowIfNull(context);
        context.ProvideCapability<TRequest, TResponse>();
        return context.Provide(Key, TRequest.QianCapability.Id, new JobContract(TRequest.QianJobCode, TRequest.QianCapability));
    }
}

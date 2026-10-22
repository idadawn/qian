using Kun.Mediator;
using Microsoft.Extensions.DependencyInjection;
using Qian.Plugin.Sdk;
using Qian.Runtime;

namespace Qian.Files;

/// <summary>文件组件的宿主选项；不进组件配置，只供宿主装配（时钟与多实例共用的 local 签名密钥）。</summary>
public sealed class QianFilesOptions
{
    /// <summary>预签名有效期、完成时限与配额预占所用的时钟。</summary>
    public TimeProvider Clock { get; set; } = TimeProvider.System;
    /// <summary>local 后端直传 / 直下地址的 HMAC 密钥（至少 32 字节）；为空时每次挂载随机生成，重启后旧地址失效，多实例须给同一密钥。</summary>
    public ReadOnlyMemory<byte> LocalSigningKey { get; set; }
}

/// <summary>文件组件的宿主装配（D20.10：只是 Apply 的装配细节，不是安装入口）。</summary>
public static class QianFilesExtensions
{
    /// <summary>
    /// 在宿主唯一的 AddKunMediator 中登记 qian.files 的六个能力（presign-upload、complete-upload、get、download-url、delete、quota）；
    /// <paramref name="source"/> 返回当前请求可注入 Files 的组件上下文（通常是 qian.files 条目按请求租户收窄的上下文），请求体不能选择它。
    /// 宿主另须 AddQianData&lt;FilesDbContext&gt; 并在 AddKunEventBus 中 RegisterFilesEvents。
    /// </summary>
    public static KunMediatorBuilder AddQianFiles(this KunMediatorBuilder builder, Func<IServiceProvider, IPluginContext> source,
        Action<QianFilesOptions>? configure = null, string channel = "api")
    {
        ArgumentNullException.ThrowIfNull(builder); ArgumentNullException.ThrowIfNull(source);
        if (builder.Services.Any(service => service.ServiceType == typeof(FilesCapabilitySource))) throw new InvalidOperationException("qian.files 的能力不能重复登记。");
        builder.Services.AddOptions<QianFilesOptions>();
        if (configure is not null) builder.Services.Configure(configure);
        builder.Services.AddSingleton(new FilesCapabilitySource(source));
        builder.AddQianCapability<PresignUploadCommand, PresignedUpload, PresignUploadHandler, FilesDbContext>(source, channel);
        builder.AddQianCapability<CompleteUploadCommand, FileDescriptor, CompleteUploadHandler, FilesDbContext>(source, channel);
        builder.AddQianCapability<GetFileQuery, FileDescriptor, GetFileHandler, FilesDbContext>(source, channel);
        builder.AddQianCapability<DownloadUrlQuery, DownloadTicket, DownloadUrlHandler, FilesDbContext>(source, channel);
        builder.AddQianCapability<DeleteFileCommand, FileDescriptor, DeleteFileHandler, FilesDbContext>(source, channel);
        return builder.AddQianCapability<QuotaQuery, FileQuota, QuotaHandler, FilesDbContext>(source, channel);
    }
}

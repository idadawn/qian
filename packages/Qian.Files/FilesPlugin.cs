using System.Text.Json;
using Kun.EventBus;
using Kun.Mediator;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Qian.Data;
using Qian.Plugin.Sdk;
using Qian.Runtime;
using Qian.Runtime.Errors;
using Qian.Tenancy;

namespace Qian.Files;

/// <summary>
/// qian.files 的真实 Provider（global 或 tenant scope）：登记 local 存储后端与组件配置里的默认上传策略，按 storage.kind 选用后端，
/// 提供 Files 服务键（<see cref="IFileService"/>，能力门面）与六个能力的执行绑定；卸载撤销服务、能力、策略与错误码。
/// 宿主须先 AddQianData&lt;FilesDbContext&gt;、在 AddKunEventBus 中 RegisterFilesEvents、在 AddKunMediator 中 AddQianFiles，否则 Apply 失败。
/// </summary>
[QianPlugin("qian.files")]
[Contributes(typeof(FilesDefinition), nameof(FilesDefinition.BackendsKey), LocalFileBackendFactory.Name, RegistryType = typeof(FileBackendRegistry))]
public sealed class FilesPlugin : IQianPlugin
{
    /// <inheritdoc />
    public IReadOnlyList<ServiceKey> Inject => [DataDefinition.Key];

    /// <inheritdoc />
    public ValueTask ApplyAsync(IPluginContext ctx, JsonElement config, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(ctx);
        ct.ThrowIfCancellationRequested();
        if (ctx.Scope != ScopeKey.Global && ctx.Scope.Kind != "tenant")
            throw new InvalidOperationException("Files 只允许挂载在 global 或 tenant scope。");
        var settings = FilesConfiguration.Read(config);
        var services = ctx.Services;
        _ = services.GetService<FilesCapabilitySource>()
            ?? throw new InvalidOperationException("宿主须在 AddKunMediator 中以 AddQianFiles 登记文件能力。");
        // 文件事件随完成 / 删除同事务进 Outbox；宿主漏登记会让每次写入失败，所以在装载时就拒绝。
        var events = services.GetService<IEventTypeRegistry>();
        if (events is null || events.GetEventType(typeof(FileObjectCreatedEvent)) != FilesEventIds.ObjectCreated ||
            events.GetEventType(typeof(FileObjectDeletedEvent)) != FilesEventIds.ObjectDeleted)
            throw new InvalidOperationException("宿主未登记文件事件：AddKunEventBus(registrar => registrar.RegisterFilesEvents()) 必须在装载 qian.files 前完成。");
        if (services.GetService<IDataContextCatalog>() is { } contexts && !contexts.ContextTypes.Contains(typeof(FilesDbContext)))
            throw new InvalidOperationException("宿主须以 AddQianData<FilesDbContext> 装配文件元数据上下文。");
        var options = services.GetService<IOptions<QianFilesOptions>>()?.Value ?? new QianFilesOptions();
        var backends = new FileBackendRegistry(); var policies = new UploadPolicyRegistry();
        ctx.Effect(backends.Register(LocalFileBackendFactory.Name, new LocalFileBackendFactory(options.LocalSigningKey)));
        foreach (var policy in settings.Policies) ctx.Effect(policies.Register(policy));
        var runtime = new FilesRuntime(options.Clock, backends, policies, settings.QuotaPerTenantBytes, () => ctx.TryInject<ITenantSettings>(TenantSettings.Key));
        // S1 只有 local；s3 / oss 由 S1-FILE-002 作为后端登记，未登记的名称启动即拒绝。
        var factory = backends.Find(settings.Kind)
            ?? throw new QianErrorException(new QianError(PlatformErrors.InputInvalid) { Pointer = "#/storage/kind", Allowed = AllowedValues.Of([.. backends.List()]) });
        runtime.Use(factory.Create(new FileBackendContext(settings.Storage, options.Clock, services)));
        if (services.GetService<ErrorCatalog>() is { } catalog) ctx.Effect(FilesErrors.Register(catalog));
        ctx.Provide<IFileService>(FilesDefinition.Key, new FileService(services.GetRequiredService<ISender>(), runtime));
        ctx.ProvideCapability<PresignUploadCommand, PresignedUpload>();
        ctx.ProvideCapability<CompleteUploadCommand, FileDescriptor>();
        ctx.ProvideCapability<GetFileQuery, FileDescriptor>();
        ctx.ProvideCapability<DownloadUrlQuery, DownloadTicket>();
        ctx.ProvideCapability<DeleteFileCommand, FileDescriptor>();
        ctx.ProvideCapability<QuotaQuery, FileQuota>();
        return ValueTask.CompletedTask;
    }
}

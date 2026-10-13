using Qian.Data;
using Qian.Sample.Crm;

var dataOnly = args.Contains("--data-only", StringComparer.Ordinal);
var migrateOnly = QianDataMigrationExtensions.IsMigrateOnly(args);
var hostArgs = args.Where(arg => arg is not ("--data-only" or QianDataMigrationExtensions.MigrateOnlyArgument)).ToArray();
var app = dataOnly ? CrmApplication.Build(hostArgs) : CrmCommandApplication.Build(hostArgs);
// K8s 迁移 Job 先行：只在 advisory lock 下迁移、不启动宿主；在线副本启动时只校验。
if (migrateOnly) await app.Services.GetRequiredService<IDataMigrator>().MigrateAsync();
else await app.RunAsync();

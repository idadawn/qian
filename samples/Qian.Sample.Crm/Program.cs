using Qian.Sample.Crm;

var dataOnly = args.Contains("--data-only", StringComparer.Ordinal);
var hostArgs = args.Where(arg => arg != "--data-only").ToArray();
await (dataOnly ? CrmApplication.Build(hostArgs) : CrmCommandApplication.Build(hostArgs)).RunAsync();

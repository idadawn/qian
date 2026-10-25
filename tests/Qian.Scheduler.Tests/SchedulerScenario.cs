using System.Text.Json;

// 用例按主题分在四个类里，各自一个 SchedulerFixture（独立的 PostgreSQL 容器），类之间并行、类内照旧串行（S1-QA-022）：
// - SchedulerMinuteTests：分钟验收，程序集的长杆；
// - SchedulerTakeoverTests：真杀接管与正常 Stop 交接；
// - SchedulerRenewalTests：续租暂态异常与首次建 run 准备；
// - SchedulerSemanticsTests：幂等、准入、隔离、围栏、超时重试与 misfire。
// 并发上限按容器数定为 4：xUnit 按集合（这里一个类一个集合）分槽位、类跑完才让出，缺省值是处理器数。
// 写死后在处理器少于 4 个的机器上四个主题类也同时开跑；不起容器的 SchedulerContractTests 不到 2 秒，和某个主题类先后占同一槽位。
[assembly: Xunit.v3.Parallelization(MaxThreads = 4)]

namespace Qian.Scheduler.Tests;

// 主题类共用的租户、作业目标与参数，都是只读值；各类的数据库状态在各自的容器里，类之间不共享。
internal static class SchedulerScenario
{
    internal static readonly string TenantA = Guid.NewGuid().ToString("D"), TenantB = Guid.NewGuid().ToString("D");
    internal static readonly JsonElement Parameters = JsonSerializer.SerializeToElement(new { mode = "normal", delayMs = 0 });
    internal const string Target = "qian.scheduler.probe/run";
    internal static CancellationToken Ct => TestContext.Current.CancellationToken;
}

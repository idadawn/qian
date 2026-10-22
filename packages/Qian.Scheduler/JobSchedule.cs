using System.Globalization;
using Quartz;
using Qian.Runtime.Errors;

namespace Qian.Scheduler;

internal static class JobSchedule
{
    internal static DateTimeOffset? Next(string schedule, DateTimeOffset after)
    {
        if (schedule == "once") return null;
        if (schedule.StartsWith("interval:", StringComparison.Ordinal)) return after.AddTicks(long.Parse(schedule.AsSpan(9), CultureInfo.InvariantCulture));
        try { return new CronExpression(schedule, TimeZoneInfo.Utc).GetNextValidTimeAfter(after); }
        catch (FormatException) { throw Invalid(); }
    }
    internal static void Validate(string cron)
    {
        if (cron.Length > 256 || cron.Split(' ', StringSplitOptions.RemoveEmptyEntries).Length is not (6 or 7) || !CronExpression.TryParse(cron, out _)) throw Invalid();
    }
    internal static QianErrorException Invalid() => new(new QianError(PlatformErrors.InputInvalid));
}

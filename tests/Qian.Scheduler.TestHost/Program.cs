using System.Text.Json;
using Qian.Scheduler.TestHost;

if (args is ["--worker"])
{
    var config = JsonSerializer.Deserialize<HostConfig>((await Console.In.ReadLineAsync())!)!;
    await using var host = await ProbeHost.CreateAsync(config, true);
    Console.WriteLine("READY " + Environment.ProcessId);
    await Console.In.ReadLineAsync();
}

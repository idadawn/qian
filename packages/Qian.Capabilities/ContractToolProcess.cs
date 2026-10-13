using System.Diagnostics;
using System.Text;

namespace Qian.Capabilities;

internal static class ContractToolProcess
{
    internal static async Task<(int Exit, string Output, string Error)> RunAsync(string executable, string directory, string[] arguments, CancellationToken ct)
    {
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(ct); deadline.CancelAfter(TimeSpan.FromSeconds(30));
        var info = new ProcessStartInfo(executable) { UseShellExecute = false, RedirectStandardOutput = true, RedirectStandardError = true, WorkingDirectory = directory, CreateNoWindow = true };
        info.Environment.Clear(); info.Environment["BUF_CACHE_DIR"] = Path.Combine(directory, ".buf-cache");
        if (OperatingSystem.IsWindows() && Environment.GetEnvironmentVariable("SystemRoot") is { } systemRoot) info.Environment["SystemRoot"] = systemRoot;
        foreach (var argument in arguments) info.ArgumentList.Add(argument);
        ct.ThrowIfCancellationRequested();
        using var process = Process.Start(info) ?? throw new InvalidOperationException("无法启动契约检查器。");
        try
        {
            var output = ReadBounded(process.StandardOutput, 1_048_576, deadline.Token);
            var error = ReadBounded(process.StandardError, 1_048_576, deadline.Token);
            await Task.WhenAll(output, error, process.WaitForExitAsync(deadline.Token)).ConfigureAwait(false);
            return (process.ExitCode, await output.ConfigureAwait(false), await error.ConfigureAwait(false));
        }
        finally { if (!process.HasExited) { process.Kill(entireProcessTree: true); await process.WaitForExitAsync(CancellationToken.None).ConfigureAwait(false); } }
    }
    private static async Task<string> ReadBounded(StreamReader reader, int limit, CancellationToken ct)
    {
        var result = new StringBuilder(); var buffer = new char[4096];
        while (await reader.ReadAsync(buffer, ct).ConfigureAwait(false) is var count && count > 0)
        {
            if (result.Length + count > limit) throw new InvalidOperationException("契约检查器输出超出限制。");
            result.Append(buffer, 0, count);
        }
        return result.ToString();
    }
}

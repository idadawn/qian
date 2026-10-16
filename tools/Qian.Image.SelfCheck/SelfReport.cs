using System.Globalization;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text.Json;

namespace Qian.Image.SelfCheck;

// 运行期证据（S1-DEP-003）：进程的有效 UID / GID、根挂载是否只读、写探针是否被拒、声明的临时目录是否可写。
// 只读 /proc，不依赖 shell，JIT 与 NativeAOT 两个自检程序共用；不用反射序列化，保证可裁剪。
internal static class SelfReport
{
    // 镜像里属主为应用用户的空目录：根文件系统可写时能写入，只读时写入必然失败，借此区分「只读」与「无权限」。
    private const string DefaultProbe = "/app/.write-probe";

    internal static int Write(Stream output)
    {
        var probe = Environment.GetEnvironmentVariable("QIAN_WRITE_PROBE") ?? DefaultProbe;
        var (written, writeError) = TryWrite(probe);
        var temp = Path.GetTempPath();
        var (tempWritten, tempError) = TryWrite(temp);
        using var writer = new Utf8JsonWriter(output, new JsonWriterOptions { Indented = true });
        writer.WriteStartObject();
        writer.WriteNumber("uid", ReadId("Uid:"));
        writer.WriteNumber("gid", ReadId("Gid:"));
        var readOnly = RootMountReadOnly();
        if (readOnly is null) writer.WriteNull("rootMountReadOnly"); else writer.WriteBoolean("rootMountReadOnly", readOnly.Value);
        WriteProbe(writer, "writeProbe", probe, written, writeError);
        WriteProbe(writer, "temp", temp, tempWritten, tempError);
        writer.WriteBoolean("dynamicCode", RuntimeFeature.IsDynamicCodeSupported);
        writer.WriteString("framework", RuntimeInformation.FrameworkDescription);
        writer.WriteString("architecture", RuntimeInformation.ProcessArchitecture.ToString().ToLowerInvariant());
        writer.WriteEndObject();
        writer.Flush();
        output.WriteByte((byte)'\n');
        return 0;
    }

    // /proc/self/status 的 Uid / Gid 行依次是实际、有效、保存、文件系统 ID，取有效 ID；读不到时返回 -1。
    private static long ReadId(string prefix)
    {
        try
        {
            foreach (var line in File.ReadLines("/proc/self/status"))
                if (line.StartsWith(prefix, StringComparison.Ordinal))
                {
                    var fields = line[prefix.Length..].Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries);
                    return fields.Length > 1 && long.TryParse(fields[1], NumberStyles.Integer, CultureInfo.InvariantCulture, out var id) ? id : -1;
                }
        }
        catch (IOException) { }
        catch (UnauthorizedAccessException) { }
        return -1;
    }

    // mountinfo 第 5 列是挂载点、第 6 列是本挂载的选项；同一挂载点可能被覆盖挂载，取最后一条。读不到时返回 null。
    private static bool? RootMountReadOnly()
    {
        try
        {
            bool? result = null;
            foreach (var line in File.ReadLines("/proc/self/mountinfo"))
            {
                var fields = line.Split(' ');
                if (fields.Length > 5 && fields[4] == "/") result = fields[5].Split(',').Contains("ro", StringComparer.Ordinal);
            }
            return result;
        }
        catch (IOException) { return null; }
        catch (UnauthorizedAccessException) { return null; }
    }

    private static (bool Written, string? Error) TryWrite(string directory)
    {
        var file = Path.Combine(directory, "probe-" + Guid.NewGuid().ToString("N", CultureInfo.InvariantCulture));
        try
        {
            File.WriteAllText(file, "probe");
            File.Delete(file);
            return (true, null);
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException)
        {
            return (false, error.GetType().Name + ": " + error.Message);
        }
    }

    private static void WriteProbe(Utf8JsonWriter writer, string name, string path, bool written, string? error)
    {
        writer.WriteStartObject(name);
        writer.WriteString("path", path);
        writer.WriteBoolean("writable", written);
        if (error is null) writer.WriteNull("error"); else writer.WriteString("error", error);
        writer.WriteEndObject();
    }
}

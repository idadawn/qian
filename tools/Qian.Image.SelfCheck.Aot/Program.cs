using Qian.Image.SelfCheck;

// 镜像流水线的 NativeAOT 自检程序（S1-DEP-003 / R106）：验证 AOT distroless 运行镜像模板，只在 CI 内构建运行。
// 只输出运行期证据；profile 装配走 JIT 自检程序，gateway 宿主的 AOT 装配由 S1-GW-001 交付。
if (args is ["self-report"]) return SelfReport.Write(Console.OpenStandardOutput());
Console.Error.WriteLine("用法：self-report");
return 2;

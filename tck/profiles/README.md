# profile 装配 TCK

`base/` 为共同组件和分层输入，`cases.json` 指定场景覆盖文件、参数与独立验收断言。测试只在临时目录展开，不改工作区。包含 48 个场景：bundle/profile/home/overlay 顺序、整段替换和默认值、插入、disabled 恢复、required 保护、scope、版本范围、非法配置和 JSON Pointer、依赖缺失/重复/环路、AOT/HMR、骨架告警、敏感字段及引用/数组；新增内存 scope 末层的继承/整段替换、禁用定位、跨 scope 改写拒绝、required 保护与敏感配置拒绝。

```bash
pnpm --filter qian... build
QIAN_PROFILE_REPORT=/tmp/profiles-ts.json pnpm --filter qian test
QIAN_PROFILE_REPORT=/tmp/profiles-dotnet.json dotnet run --project tests/Qian.Profiles.Tests -c Release
node packages/qian-surfaces/scripts/compare-fixtures.mjs /tmp/profiles-dotnet.json /tmp/profiles-ts.json
```

两侧报告比较完整快照与诊断，唯一归一化为随机 trace_id 以及 ErrorDetail 可选空字段；不掩盖错误码、文件、指针、行号、配置或来源差异。另有默认五个 profile 与真实 CLI 子进程测试；.NET 从实际交付的 minimal profile 和组合包文件启动真实 PluginLoader，再通过 overlay 挂载静态命令。minimal 默认无业务组件、无骨架警告，符合 S1-DEC-012 的确认结论。

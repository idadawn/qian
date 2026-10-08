# qian CLI（装配切片）

本阶段实现 [S1-RT-002](../../pm/tasks/S1-RT-002.md) 的 `dump-config`。共享表面解析与 Schema 包为 `@qian/surfaces`，profile 合成规则以 [Qian.Profiles](../Qian.Profiles/README.md) 为唯一说明。

在仓库根目录执行：

```bash
pnpm install
pnpm --filter qian... build
pnpm exec qian --profile platform --dump-config --no-home
pnpm exec qian --profile platform dump-config --patch ./ops/local.patch.yml --format json
```

overlay 示例（不会修改 base 组合包）：

```yaml
- id: files
  config:
    storage: {kind: local, root: /tmp/qian-local-files}
  note: 本地配置验证
```

输出的 `layers` 列出 bundle、profile-patch、home-patch、overlay；每条 `entries[].layer` 指向最后改写来源。默认输出 YAML，可用 `--json` 或 `--format json`。`--entry <id>` 仅过滤条目；`--cwd <目录>` 切换项目，`--home <目录>` 指定 home，`--no-home` 关闭 home 叠加。profile 缺省先读项目 `.qian/config.yml` 的 `defaultProfile`，再回落 platform。

`--resolve`、`--redact` 表示本切片始终采用的行为：始终解析并验证依赖，始终保留 secretRef，不读取密钥。合法配置退出 0；非法参数或配置退出 1、标准输出为空、标准错误输出带 ErrorDetail 的 diagnostics。业务骨架 warning 会出现在成功输出中，不能用作 Provider 就绪证明。

此工作区包尚未发布。install/add、锁文件、启动进程、远程平台 scope patch 与其余 CLI 命令属于后续任务，不在本切片伪实现。minimal 默认清单仍待产品决策，四个其它 profile 可静态解析。

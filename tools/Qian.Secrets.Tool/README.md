# 配置敏感数据门禁

默认扫描层次 JSON；Apollo properties 导出为冒号层次键时加 `--apollo`，按大小写不敏感的路径匹配配置 Schema。

构建 `Qian.slnx` 后，在 Apollo 配置发布前执行：

```bash
dotnet tools/Qian.Secrets.Tool/bin/Release/net10.0/Qian.Secrets.Tool.dll candidate.json config.schema.json
```

输入为配置 JSON 与可选配置 Schema。返回 0 表示通过、1 表示检测到敏感数据或无法安全解释的 Schema、2 表示输入不可读或格式错误。标准输出只有诊断码、位置和原因，不回显原值。门禁与运行期 Apollo 默认校验复用 `SecretSafety`。

`--profiles <仓库根> [profile 名称...]` 用 `ProfileLoader` 装配 `profiles/` 下每个 profile 的最终生效配置，按条目所属组件的配置 Schema 逐条扫描；profile 装配失败同样返回 1，因为无法确认配置不含明文。仓库 CI 的 `dotnet` 工作流对全部 profile 执行这一步：

```bash
dotnet tools/Qian.Secrets.Tool/bin/Release/net10.0/Qian.Secrets.Tool.dll --profiles .
```

仓库 CI 的 `tests/gates/test_secret_scan.py` 真实启动该工具，验证敏感 Schema 明文、疑似键名和高熵值分别非零退出，规范引用通过；并对仓库 profile 与注入了明文的 TCK profile 副本各跑一遍 `--profiles`。部署流水线应在提交 Apollo 发布前扫描其实际候选配置；此工具不会读取线上秘密或自行发布配置。

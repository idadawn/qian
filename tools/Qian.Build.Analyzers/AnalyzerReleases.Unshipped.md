; 此文件由分析器发布跟踪器读取，英文段名与表头是工具格式。
### New Rules

Rule ID | Category | Severity | Notes
--------|----------|----------|------
QIAN0002 | Compatibility | Error | 禁用旧 Kun 类型的声明，与 BannedSymbols.txt 共用清单
QIAN0003 | Security | Error | 在线 DbContext 必须继承 QianDbContext
QIAN0004 | Security | Error | 禁止调用或引用 EF IgnoreQueryFilters
QIAN0005 | Security | Error | 受保护命令通过同一入口登记 Kun 权限行为与处理器

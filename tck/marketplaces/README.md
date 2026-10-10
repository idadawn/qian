# 市场目录共用 TCK

cases.json 由 .NET 与 TS 读取同一份文档，逐字段比较有效性、错误码、文件、指针、行号和级别。marketplace.example.json 是可直接通过 Schema 的完整例子。

文档校验不声称来源已下载或验签；来源获取、Ed25519 信任、内容 digest 和组件表面校验由 CLI 的市场验证链路继续完成。私钥和带凭据 URL 的失败结果不得回显输入值。

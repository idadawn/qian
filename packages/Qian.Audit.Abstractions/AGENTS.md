# Audit Definition

- 本包保存稳定审计契约；具体行为与主文档在 Qian.Audit。
- 记录 DTO 不承担认证或授权。补充 payload 不能代替输入脱敏；字符串化不得展开敏感记录。
- 公共 API 修改同步 PublicAPI 文件；Qian 字段不能为适配旧框架记录形状而删除。

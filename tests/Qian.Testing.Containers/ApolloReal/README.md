# 真实 Apollo 测试数据库

apolloconfigdb.sql 原样取自 Apollo v2.5.2 的 scripts/sql/profiles/mysql-default/apolloconfigdb.sql，遵循 Apache-2.0；版权与许可见原文件头部。来源：https://github.com/apolloconfig/apollo/blob/v2.5.2/scripts/sql/profiles/mysql-default/apolloconfigdb.sql 。

本目录只用于本机/CI 的隔离容器。数据库口令每次运行生成，不写入文件；真实发布通过 AdminService 的接口完成，不通过直接修改 Release 表制造通知。

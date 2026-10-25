> For AI agents: the complete documentation index is available at /llms.txt, the full documentation bundle is available at /llms-full.txt.

# 开始

Qian 的能力都以**组件**交付：一个文件夹、一份清单、若干声明式表面，加上可选的代码。装一个组件只要一条命令，不需要先部署 Qian 平台；同一组件放进 `platform` profile，就成了多租户平台的一部分。

## 从这里开始

- [30 分钟上手](/start/quickstart.md)：在空白 ASP.NET Core 项目里 `qian add identity authz files`，跑通文件上传与权限检查。
- [宿主与框架](/start/hosts.md)：独立模式与平台模式、五个 profile、前端外壳、与 Kun 的关系。

## 核心概念

| 概念                                            | 一句话                                                        |
| --------------------------------------------- | ---------------------------------------------------------- |
| [组件](/start/concepts/component.md)            | 分发与安装的单位：文件夹 + 清单 + 声明式表面 + 可选代码；运行时形态叫插件                  |
| [表面](/start/concepts/surface.md)              | 组件对外暴露的一类声明式内容：skills、agents、commands、hooks、MCP、tools、ui 等 |
| [scope](/start/concepts/scope.md)             | 注册的归属：全局，或一个租户 / 应用 / Agent 会话；两层扁平、不继承                    |
| [seam](/start/concepts/seam.md)               | 一项能力的缝合点：Definition + Provider + Consumer，换 Provider 不改调用方 |
| [profile 与 patch](/start/concepts/profile.md) | 具名组装与分层覆盖：环境 = profile + 分层 patch                          |

## 再往后

- [指南](/start/guides/index.md)：Agent 栈、后端模式、多 profile 组合、安全、连接器与集成、从参考平台迁移。
- 组件怎么装、怎么配：[组件目录](/components/index.md)；编程模型与 API：[SDK](/sdk/index.md)；命令与清单字段：[CLI 与规格](/cli/index.md)。

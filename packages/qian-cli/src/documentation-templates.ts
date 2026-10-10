/** qian init 可复用的组件文档模板；模板没有隐含的业务能力或部署承诺。 */
export function renderDocumentationTemplates(id: string, name: string): Record<'README.md'|'AGENTS.md', string> {
  if(!/^[a-z][a-z0-9-]*(\.[a-z][a-z0-9-]*)+$/.test(id))throw new Error('组件 id 无效。');
  const title=name.replace(/[\r\n]/g,' ').replaceAll('<','&lt;').replaceAll('>','&gt;');
  return {
    'README.md':`# ${title}\n\n组件：\`${id}\`。\n\n## 装配契约\n\n- 文件夹表面：按实际清单填写。\n- profile：按实际适用范围填写。\n- seam 与角色：填写 Definition、Provider 或 Consumer。\n- 安装：发布前验证 \`qian add ${id}\`，不要将未发布包写成已可用。\n\n## 模型体验\n\n说明工具描述、输入输出契约、结构化错误、权限与确认要求，并提供可重现示例。\n\n## 已知限制与延期项\n\n逐项列出未实现能力和验证环境；不要把配置骨架写成已运行服务。\n`,
    'AGENTS.md':`# ${title} 开发约定\n\n组件：\`${id}\`。实现前读取本目录 README 和组件清单，以仓库 ADR、规格及任务验收为准。\n\n## 模型体验\n\n保持工具与契约描述准确；错误使用 ErrorDetail。修改事件定义后运行 \`qian docs generate\`，核对生产方、消费方和分发模式。\n\n## 已知限制与延期项\n\n保持 README 的限制清单与真实实现一致；测试使用可复现 fixture，记录未执行的验收，不伪造 Provider 或发布状态。\n`
  };
}

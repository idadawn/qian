/** 组件里的可见文字必须由宿主 label 或 i18n 提供；示例、测试和语言资源不在规则作用域。 */
export const noHardcodedText = {
  meta: { type: /** @type {const} */ ('problem'), schema: [], messages: { hardcoded: '组件文案必须使用 i18n key 或注入的 label，不能硬编码。' } },
  create(context) {
    const hasWords = (value) => typeof value === 'string' && /[\p{L}\p{N}]/u.test(value.trim());
    const report = (node) => context.report({ node, messageId: 'hardcoded' });
    return {
      JSXText(node) { if (hasWords(node.value)) report(node); },
      JSXAttribute(node) {
        if (['aria-label', 'placeholder', 'title', 'label', 'alt'].includes(node.name.name)
          && node.value?.type === 'Literal' && hasWords(node.value.value)) report(node);
      },
      'JSXExpressionContainer > Literal'(node) {
        if (node.parent.parent.type !== 'JSXAttribute' && hasWords(node.value)) report(node);
      },
      Literal(node) {
        if (typeof node.value === 'string' && /\p{Script=Han}/u.test(node.value)) report(node);
      },
    };
  },
};

declare module '*.css';

/** Rspress 构建时注入：SSG-MD（把页面渲染成 Markdown）那一遍为 true，其余为 false（docs-site.md §2a.1「机器可读」）。 */
interface ImportMetaEnv {
  readonly SSG_MD?: boolean;
}
interface ImportMeta {
  readonly env: ImportMetaEnv;
}

/** Rspress 对 .md / .mdx 加 ?raw 时返回源文本。 */
declare module '*.md?raw' {
  const source: string;
  export default source;
}

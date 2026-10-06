declare module '*.css';

/** Rspress 对 .md / .mdx 加 ?raw 时返回源文本。 */
declare module '*.md?raw' {
  const source: string;
  export default source;
}

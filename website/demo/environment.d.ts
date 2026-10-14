declare module 'yue-demo-sources' { const sources: Record<string, string>; export default sources; }
interface Window {
  Babel: { transform(code: string, options: Record<string, unknown>): { code: string } };
  require: { (modules: string[], success: () => void, failure: (error: unknown) => void): void; config(config: unknown): void };
  monaco: { editor: { create(element: HTMLElement, options: Record<string, unknown>): { getValue(): string; setValue(value: string): void; dispose(): void }; setTheme(theme: string): void } };
}

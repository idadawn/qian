import { PlatformError } from '@idadawn/yue-sdk';
import { errorCatalog } from './error-catalog';
/** 前端错误保持 ErrorDetail；目录从冻结协议派生，不扩写 Runtime 包的内部错误集合。 */
export class ShellError extends PlatformError {
  readonly code: keyof typeof errorCatalog;
  readonly pointer?: string;
  constructor(code: keyof typeof errorCatalog, _args: Record<string, string> = {}, pointer?: string) {
    const entry = errorCatalog[code];
    super({ type: entry.type, title: entry.title['zh-CN'], status: entry.status, code, message: entry.message['zh-CN'], trace_id: crypto.randomUUID().replaceAll('-', ''), ...(pointer ? { pointer } : {}) });
    this.name = 'ShellError'; this.message = code; this.code = code; this.pointer = pointer;
  }
}

import { HttpResponseError, PlatformError, type PlatformClient } from '@idadawn/yue-sdk';
import type { components, paths } from './upload.generated';
export type UploadedFile = components['schemas']['FileObject'];
export interface UploadProgress { stage: 'hashing' | 'uploading' | 'complete'; loaded: number; total: number; deduplicated?: boolean }
export interface UploadOptions { signal?: AbortSignal; onProgress?: (progress: UploadProgress) => void }

export async function fileSha256(file: Blob): Promise<string> {
  const digest = await crypto.subtle.digest('SHA-256', await file.arrayBuffer());
  return [...new Uint8Array(digest)].map((byte) => byte.toString(16).padStart(2, '0')).join('');
}

/** init 可按摘要恢复会话，已确认的分片不再发送；part 序号从 1 开始。 */
export async function uploadFile(client: PlatformClient, file: File, { signal, onProgress }: UploadOptions = {}): Promise<UploadedFile> {
  const api = client.api<paths>();
  signal?.throwIfAborted();
  onProgress?.({ stage: 'hashing', loaded: 0, total: file.size });
  const sha256 = await fileSha256(file);
  signal?.throwIfAborted();
  try {
    const found = await api.GET('/uploads:lookup', { params: { query: { sha256 } }, signal });
    if (found.data) { onProgress?.({ stage: 'complete', loaded: file.size, total: file.size, deduplicated: true }); return found.data; }
  } catch (error) {
    if (!((error instanceof PlatformError || error instanceof HttpResponseError) && error.status === 404)) throw error;
  }
  const initialized = await api.POST('/uploads:init', { body: { name: file.name, size: file.size, contentType: file.type || 'application/octet-stream', sha256 }, signal });
  const session = initialized.data;
  if (!session || !session.uploadId || !Number.isSafeInteger(session.partSize) || session.partSize <= 0 || !Array.isArray(session.uploadedParts)) throw new Error('data.error.uploadSession');
  const partCount = Math.max(1, Math.ceil(file.size / session.partSize));
  if (session.uploadedParts.some((n) => !Number.isInteger(n) || n < 1 || n > partCount)) throw new Error('data.error.uploadParts');
  const completed = new Set(session.uploadedParts);
  let loaded = [...completed].reduce((sum, n) => sum + Math.min(session.partSize, file.size - (n - 1) * session.partSize), 0);
  onProgress?.({ stage: 'uploading', loaded, total: file.size });
  for (let n = 1; n <= partCount; n++) {
    signal?.throwIfAborted();
    if (completed.has(n)) continue;
    const part = file.slice((n - 1) * session.partSize, n * session.partSize);
    await api.PUT('/uploads/{id}/parts/{n}', { params: { path: { id: session.uploadId, n } }, body: '', bodySerializer: () => part, headers: { 'Content-Type': 'application/octet-stream' }, signal });
    loaded += part.size;
    onProgress?.({ stage: 'uploading', loaded, total: file.size });
  }
  const result = await api.POST('/uploads/{id}:complete', { params: { path: { id: session.uploadId } }, body: { sha256, partCount }, signal });
  if (!result.data?.id) throw new Error('data.error.uploadResult');
  onProgress?.({ stage: 'complete', loaded: file.size, total: file.size });
  return result.data;
}

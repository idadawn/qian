import { useEffect, useId, useRef, useState } from 'react';
import { Button, Input, Label } from '@idadawn/yue-core';
import { PlatformError, type PlatformClient, type PlatformI18n } from '@idadawn/yue-sdk';
import { uploadFile, type UploadedFile, type UploadProgress } from '../upload';

export interface UploadProps {
  client: PlatformClient;
  i18n: PlatformI18n;
  label: string;
  accept?: string;
  disabled?: boolean;
  onComplete?: (file: UploadedFile) => void;
  onError?: (error: unknown) => void;
  className?: string;
  testId?: string;
}
export function Upload({ client, i18n, label, accept, disabled, onComplete, onError, className, testId = 'yue-upload' }: UploadProps) {
  const id = useId();
  const current = useRef<AbortController | null>(null);
  const [file, setFile] = useState<File | null>(null);
  const [progress, setProgress] = useState<UploadProgress | null>(null);
  const [state, setState] = useState<'idle' | 'working' | 'paused' | 'error' | 'complete'>('idle');
  const [error, setError] = useState('');
  useEffect(() => () => { current.current?.abort(); }, []);
  const start = async (selected: File) => {
    current.current?.abort();
    const controller = new AbortController(); current.current = controller;
    setFile(selected); setState('working'); setError('');
    try {
      const result = await uploadFile(client, selected, { signal: controller.signal, onProgress: (next) => { if (current.current === controller) setProgress(next); } });
      if (current.current !== controller) return;
      setState('complete'); onComplete?.(result);
    } catch (reason) {
      if (current.current !== controller) return;
      if (controller.signal.aborted) setState('paused');
      else { setState('error'); setError(reason instanceof PlatformError ? reason.message : i18n.t('common.error')); onError?.(reason); }
    }
  };
  return <div className={className ?? 'grid gap-2'} data-slot="upload" data-testid={testId}>
    <Label htmlFor={id}>{label}</Label><Input id={id} type="file" accept={accept} disabled={disabled || state === 'working'} onChange={(event) => { const selected = event.target.files?.[0]; if (selected) void start(selected); }} />
    {progress && <><progress max={Math.max(1, progress.total)} value={progress.stage === 'complete' ? Math.max(1, progress.total) : progress.loaded} aria-label={label} data-testid={`${testId}-progress`} /><span role="status">{progress.stage === 'hashing' ? i18n.t('data.hashing') : progress.stage === 'complete' ? i18n.t('data.uploaded') : `${progress.loaded} / ${progress.total}`}</span></>}
    {state === 'working' && <Button onClick={() => current.current?.abort()}>{i18n.t('data.cancel')}</Button>}
    {(state === 'paused' || state === 'error') && file && <Button disabled={disabled} onClick={() => void start(file)}>{i18n.t('data.resume')}</Button>}
    {error && <p role="alert">{error}</p>}
  </div>;
}

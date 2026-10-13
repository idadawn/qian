import { useState, useSyncExternalStore, type ComponentProps, type ReactNode } from 'react';
import { cn } from '../lib/cn';
import type { YueProps } from '../lib/test-id';
import { Button, IconButton } from './button';
import { Icon } from './icon';

export interface BreadcrumbItem { key: string; label: ReactNode; href?: string }
export type BreadcrumbProps = Omit<YueProps<ComponentProps<'nav'>>, 'children'> & { label: string; items: readonly BreadcrumbItem[] };
export function Breadcrumb({ label, items, className, testId, ...props }: BreadcrumbProps) {
  return <nav {...props} aria-label={label} data-slot="breadcrumb" data-testid={testId ?? 'yue-breadcrumb'} className={className}><ol className="flex flex-wrap items-center gap-2 text-sm">{items.map((item, index) => <li key={item.key} className="inline-flex items-center gap-2">{index > 0 && <Icon name="chevronRight" className="rtl:rotate-180" />}{index === items.length - 1 ? <span aria-current="page">{item.label}</span> : <a href={item.href} className="text-[var(--muted-foreground)] underline-offset-4 hover:underline">{item.label}</a>}</li>)}</ol></nav>;
}
export interface PaginationProps {
  page?: number; defaultPage?: number; totalPages: number; onPageChange?: (page: number) => void;
  label: string; previousLabel: string; nextLabel: string; getPageLabel: (page: number) => string;
  disabled?: boolean; className?: string; testId?: string;
}
export function Pagination({ page, defaultPage = 1, totalPages, onPageChange, label, previousLabel, nextLabel, getPageLabel, disabled, className, testId }: PaginationProps) {
  const [internal, setInternal] = useState(defaultPage);
  if (!Number.isInteger(totalPages) || totalPages < 1) throw new Error('totalPages 必须为正整数。');
  const current = Math.min(totalPages, Math.max(1, page ?? internal));
  const change = (next: number) => { setInternal(next); onPageChange?.(next); };
  const pages = [...new Set([1, current - 1, current, current + 1, totalPages])].filter(value => value >= 1 && value <= totalPages).sort((a, b) => a - b);
  return <nav aria-label={label} data-slot="pagination" data-testid={testId ?? 'yue-pagination'} className={cn('flex items-center gap-1', className)}><IconButton label={previousLabel} variant="outline" disabled={disabled || current === 1} onClick={() => change(current - 1)}><Icon name="chevronLeft" className="rtl:rotate-180" /></IconButton>{pages.map((value, index) => <span key={value} className="contents">{index > 0 && value - pages[index - 1]! > 1 && <span aria-hidden="true" className="px-1">…</span>}<Button variant={value === current ? 'primary' : 'outline'} aria-current={value === current ? 'page' : undefined} aria-label={getPageLabel(value)} disabled={disabled} onClick={() => change(value)}>{value}</Button></span>)}<IconButton label={nextLabel} variant="outline" disabled={disabled || current === totalPages} onClick={() => change(current + 1)}><Icon name="chevronRight" className="rtl:rotate-180" /></IconButton></nav>;
}
const serverZero = () => 0;
function subscribeScroll(callback: () => void) { window.addEventListener('scroll', callback, { passive: true }); return () => window.removeEventListener('scroll', callback); }
function scrollSnapshot() { return window.scrollY; }
export interface BackTopProps { label: string; visibilityHeight?: number; className?: string; testId?: string }
export function BackTop({ label, visibilityHeight = 400, className, testId }: BackTopProps) {
  const position = useSyncExternalStore(subscribeScroll, scrollSnapshot, serverZero);
  if (position < visibilityHeight) return null;
  return <span data-slot="back-top" data-testid={testId ?? 'yue-back-top'}><IconButton label={label} testId={`${testId ?? 'yue-back-top'}-button`} className={className} onClick={() => window.scrollTo({ top: 0, behavior: window.matchMedia('(prefers-reduced-motion: reduce)').matches ? 'instant' : 'smooth' })}><Icon name="arrowUp" /></IconButton></span>;
}
function subscribeFullscreen(callback: () => void) { document.addEventListener('fullscreenchange', callback); return () => document.removeEventListener('fullscreenchange', callback); }
function fullscreenSnapshot() { return document.fullscreenElement; }
const serverNull = () => null;
export interface FullScreenProps { enterLabel: string; exitLabel: string; target?: HTMLElement | null; onError: (error: unknown) => void; className?: string; testId?: string }
export function FullScreen({ enterLabel, exitLabel, target, onError, className, testId }: FullScreenProps) {
  const active = useSyncExternalStore(subscribeFullscreen, fullscreenSnapshot, serverNull);
  const toggle = async () => { try { if (active) await document.exitFullscreen(); else await (target ?? document.documentElement).requestFullscreen(); } catch (error) { onError(error); } };
  return <span data-slot="full-screen" data-testid={testId ?? 'yue-full-screen'}><IconButton label={active ? exitLabel : enterLabel} aria-pressed={!!active} testId={`${testId ?? 'yue-full-screen'}-button`} className={className} onClick={() => void toggle()}><Icon name={active ? 'minimize' : 'maximize'} /></IconButton></span>;
}

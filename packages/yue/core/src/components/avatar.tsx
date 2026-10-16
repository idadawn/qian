import { Avatar as BaseAvatar } from '@base-ui/react/avatar';
import type { ComponentProps } from 'react';
import { cn } from '../lib/cn';
import type { YueProps } from '../lib/test-id';

export type AvatarProps = YueProps<ComponentProps<typeof BaseAvatar.Root>>;
export function Avatar({ className, testId, ...props }: AvatarProps) {
  return <BaseAvatar.Root {...props} data-slot="avatar" data-testid={testId ?? 'yue-avatar'} className={cn('relative inline-flex size-8 shrink-0 overflow-hidden rounded-full bg-muted', className)} />;
}

export type AvatarImageProps = YueProps<ComponentProps<typeof BaseAvatar.Image>> & { alt: string };
export function AvatarImage({ className, testId, ...props }: AvatarImageProps) {
  return <BaseAvatar.Image {...props} data-slot="avatar-image" data-testid={testId ?? 'yue-avatar-image'} className={cn('size-full object-cover', className)} />;
}

export type AvatarFallbackProps = YueProps<ComponentProps<typeof BaseAvatar.Fallback>>;
export function AvatarFallback({ className, testId, ...props }: AvatarFallbackProps) {
  return <BaseAvatar.Fallback {...props} data-slot="avatar-fallback" data-testid={testId ?? 'yue-avatar-fallback'} className={cn('flex size-full items-center justify-center text-sm font-medium text-muted-foreground', className)} />;
}

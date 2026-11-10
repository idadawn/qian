import { Avatar as BaseAvatar } from '@base-ui/react/avatar';
import type { ComponentProps } from 'react';
import { cn } from '../lib/cn';
import type { YueProps } from '../lib/test-id';

/** 头像与标识色块的配色：primary 为主色柔底、solid 为主色实底，其余为固定色相的点缀色（不随品牌变化），gray 用于停用对象。 */
export type AvatarTint = 'primary' | 'solid' | 'blue' | 'green' | 'amber' | 'violet' | 'teal' | 'gray';
const tints: Record<AvatarTint, string> = {
  primary: 'bg-primary-soft text-primary-soft-foreground',
  solid: 'bg-primary text-primary-foreground',
  blue: 'bg-info-bg text-info',
  green: 'bg-ok-bg text-ok',
  amber: 'bg-warn-bg text-warn',
  violet: 'bg-tint-violet text-tint-violet-foreground',
  teal: 'bg-tint-teal text-tint-teal-foreground',
  gray: 'bg-neutral-bg text-neutral',
};
/** 按名称轮换的点缀色，供列表里的租户、应用等标识色块使用。 */
export const AVATAR_TINTS: readonly AvatarTint[] = ['blue', 'amber', 'green', 'violet', 'teal'];
/** 按种子（如租户号）稳定取一种点缀色：同一对象每次同色，不同对象大致均匀分布。 */
export function avatarTint(seed: string): AvatarTint {
  let hash = 0;
  for (const char of seed) hash = (hash * 31 + char.codePointAt(0)!) >>> 0;
  return AVATAR_TINTS[hash % AVATAR_TINTS.length]!;
}

export type AvatarProps = YueProps<ComponentProps<typeof BaseAvatar.Root>> & {
  /** 无图片时的底色与文字色。 */
  tint?: AvatarTint;
  /** circle 用于人，square 用于企业、应用等对象的标识色块。 @default 'circle' */
  shape?: 'circle' | 'square';
};
export function Avatar({ className, testId, tint, shape = 'circle', ...props }: AvatarProps) {
  return <BaseAvatar.Root {...props} data-slot="avatar" data-shape={shape} data-testid={testId ?? 'yue-avatar'} className={cn('relative inline-flex size-8 shrink-0 overflow-hidden bg-muted text-muted-foreground', shape === 'square' ? 'rounded-md' : 'rounded-full', tint && tints[tint], className)} />;
}

export type AvatarImageProps = YueProps<ComponentProps<typeof BaseAvatar.Image>> & { alt: string };
export function AvatarImage({ className, testId, ...props }: AvatarImageProps) {
  return <BaseAvatar.Image {...props} data-slot="avatar-image" data-testid={testId ?? 'yue-avatar-image'} className={cn('size-full object-cover', className)} />;
}

export type AvatarFallbackProps = YueProps<ComponentProps<typeof BaseAvatar.Fallback>>;
export function AvatarFallback({ className, testId, ...props }: AvatarFallbackProps) {
  return <BaseAvatar.Fallback {...props} data-slot="avatar-fallback" data-testid={testId ?? 'yue-avatar-fallback'} className={cn('flex size-full items-center justify-center text-sm font-semibold', className)} />;
}

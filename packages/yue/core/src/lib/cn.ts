import { clsx, type ClassValue } from 'clsx';
import { twMerge } from 'tailwind-merge';

/** 合并 Tailwind 类名：clsx 处理条件，tailwind-merge 去掉冲突的工具类。 */
export function cn(...inputs: ClassValue[]): string {
  return twMerge(clsx(inputs));
}

import { createContext, type ReactNode } from 'react';

/** 弹层与表单共用页脚，不要求宿主传递新的控制协议。 */
export const OverlayFormContext = createContext<{ cancel: ReactNode; register: () => () => void } | null>(null);

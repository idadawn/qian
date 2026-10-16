import { createContext, type ReactNode } from 'react';

export const ButtonIdentityContext = createContext<{ slot: string; testId: string } | null>(null);
/** render={<Button />} 保持组合部件身份；直接传 data-slot 仍不能改写 Button 的身份。 */
export function ButtonIdentity({ slot, testId, children }: { slot: string; testId: string; children: ReactNode }) {
  return <ButtonIdentityContext value={{ slot, testId }}>{children}</ButtonIdentityContext>;
}

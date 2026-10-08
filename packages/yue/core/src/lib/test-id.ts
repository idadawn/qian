/**
 * 可寻址属性约定（packages/yue/AGENTS.md「data-slot 与 data-testid」）：
 * - `data-slot="<组件>-<部件>"`：样式与文档用，固定不可覆盖；
 * - `data-testid`：默认 `yue-<组件>-<部件>`，消费方用 `testId` 覆盖成稳定的业务无关标识（不含用户数据）。
 */
export interface TestIdProps {
  /** 覆盖默认的 `data-testid`（默认 `yue-<组件>-<部件>`）。 */
  testId?: string | undefined;
}

/** 去掉 Base UI 的函数式 className（我们只接受字符串，交给 cn 合并），并加上 testId。 */
export type YueProps<P> = Omit<P, 'className'> & { className?: string | undefined } & TestIdProps;

// @idadawn/yue-biz：业务选择器层（docs/prd/specs/yue.md §3.1；实现任务 S1-YUE-010）。
// 全部经宿主注入的 PlatformClient 取数，文案走 i18n key；树形选择建在 @yue/data 的 Tree / TreeSelect 上（规格 D3）。
export const PACKAGE_NAME = '@idadawn/yue-biz' as const;
export * from './api';
export * from './locales';
export { useRemoteOptions, type RemoteLoader, type RemoteOptions, type RemoteQuery, type UseRemoteOptionsOptions } from './remote-options';
export type { BizSelectBaseProps, MultipleSelection, SelectOption, Selection, SingleSelection } from './components/option-combobox';
export * from './components/api-select';
export * from './components/dict-select';
export * from './components/organize-select';
export * from './components/user-select';
export * from './components/role-select';
export * from './components/authorization-tree';
export * from './components/icon-picker';

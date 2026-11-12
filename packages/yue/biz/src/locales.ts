import { createI18n, type LanguagePack, type Locale, type PlatformI18n } from '@idadawn/yue-sdk';
import { addDataLanguagePacks } from '@idadawn/yue-data';

// 业务选择器的文案资源：三种语言的 key 集合必须一致（locales.browser.test.tsx 校验）。组织选择器复用 @yue/data 的 TreeSelect，一并装入 data 的语言包。
export const bizLanguagePacks: Record<Locale, LanguagePack> = {
  'zh-CN': {
    'biz.select': '请选择', 'biz.search': '搜索{{label}}', 'biz.options': '{{label}}选项', 'biz.clear': '清空{{label}}', 'biz.selected': '已选 {{n}} 项',
    'biz.loading': '加载中', 'biz.empty': '暂无数据', 'biz.noMatch': '没有匹配项', 'biz.error': '加载失败', 'biz.retry': '重试',
    'biz.member.more': '只显示前 {{n}} 位成员，继续输入可缩小范围', 'biz.member.status.disabled': '已停用', 'biz.member.status.locked': '已锁定',
    'biz.role.builtin': '内置', 'biz.role.tenant.owner': '租户所有者', 'biz.role.tenant.admin': '租户管理员', 'biz.role.tenant.developer': '开发者', 'biz.role.tenant.user': '使用者',
    'biz.action.read': '查看', 'biz.action.create': '新建', 'biz.action.edit': '编辑', 'biz.action.update': '修改', 'biz.action.delete': '删除', 'biz.action.write': '写入',
    'biz.action.export': '导出', 'biz.action.import': '导入', 'biz.action.activate': '激活', 'biz.action.suspend': '冻结', 'biz.action.resume': '恢复', 'biz.action.assign': '分配',
    'biz.action.launch': '发起', 'biz.action.manage': '管理', 'biz.action.enable': '启用', 'biz.action.disable': '停用',
    'biz.risk.high': '高风险', 'biz.risk.critical': '关键操作', 'biz.stepUp': '需二次验证',
  },
  'zh-TW': {
    'biz.select': '請選擇', 'biz.search': '搜尋{{label}}', 'biz.options': '{{label}}選項', 'biz.clear': '清除{{label}}', 'biz.selected': '已選 {{n}} 項',
    'biz.loading': '載入中', 'biz.empty': '暫無資料', 'biz.noMatch': '沒有符合項目', 'biz.error': '載入失敗', 'biz.retry': '重試',
    'biz.member.more': '只顯示前 {{n}} 位成員，繼續輸入可縮小範圍', 'biz.member.status.disabled': '已停用', 'biz.member.status.locked': '已鎖定',
    'biz.role.builtin': '內建', 'biz.role.tenant.owner': '租戶擁有者', 'biz.role.tenant.admin': '租戶管理員', 'biz.role.tenant.developer': '開發者', 'biz.role.tenant.user': '使用者',
    'biz.action.read': '檢視', 'biz.action.create': '新增', 'biz.action.edit': '編輯', 'biz.action.update': '修改', 'biz.action.delete': '刪除', 'biz.action.write': '寫入',
    'biz.action.export': '匯出', 'biz.action.import': '匯入', 'biz.action.activate': '啟用', 'biz.action.suspend': '凍結', 'biz.action.resume': '恢復', 'biz.action.assign': '指派',
    'biz.action.launch': '發起', 'biz.action.manage': '管理', 'biz.action.enable': '啟用', 'biz.action.disable': '停用',
    'biz.risk.high': '高風險', 'biz.risk.critical': '關鍵操作', 'biz.stepUp': '需二次驗證',
  },
  'en-US': {
    'biz.select': 'Select', 'biz.search': 'Search {{label}}', 'biz.options': '{{label}} options', 'biz.clear': 'Clear {{label}}', 'biz.selected': '{{n}} selected',
    'biz.loading': 'Loading', 'biz.empty': 'No data', 'biz.noMatch': 'No matches', 'biz.error': 'Unable to load', 'biz.retry': 'Retry',
    'biz.member.more': 'Showing the first {{n}} members. Keep typing to narrow the list.', 'biz.member.status.disabled': 'Disabled', 'biz.member.status.locked': 'Locked',
    'biz.role.builtin': 'Built-in', 'biz.role.tenant.owner': 'Tenant owner', 'biz.role.tenant.admin': 'Tenant admin', 'biz.role.tenant.developer': 'Developer', 'biz.role.tenant.user': 'User',
    'biz.action.read': 'View', 'biz.action.create': 'Create', 'biz.action.edit': 'Edit', 'biz.action.update': 'Update', 'biz.action.delete': 'Delete', 'biz.action.write': 'Write',
    'biz.action.export': 'Export', 'biz.action.import': 'Import', 'biz.action.activate': 'Activate', 'biz.action.suspend': 'Suspend', 'biz.action.resume': 'Resume', 'biz.action.assign': 'Assign',
    'biz.action.launch': 'Launch', 'biz.action.manage': 'Manage', 'biz.action.enable': 'Enable', 'biz.action.disable': 'Disable',
    'biz.risk.high': 'High risk', 'biz.risk.critical': 'Critical', 'biz.stepUp': 'Step-up required',
  },
};

/** 装入 biz 与其依赖的 data 语言包；宿主已有 PlatformI18n 实例时调用，不新建实例。 */
export function addBizLanguagePacks(i18n: PlatformI18n): void {
  addDataLanguagePacks(i18n);
  for (const [locale, pack] of Object.entries(bizLanguagePacks) as [Locale, LanguagePack][]) i18n.addLanguagePack(locale, pack);
}

export function createBizI18n(locale: Locale = 'zh-CN'): PlatformI18n {
  const i18n = createI18n(locale);
  addBizLanguagePacks(i18n);
  return i18n;
}

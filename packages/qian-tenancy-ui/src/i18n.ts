import { createDataI18n } from '@idadawn/yue-data';
export const i18n = createDataI18n();
i18n.addLanguagePack('zh-CN', {
  'tenant.brand': 'Qian 乾', 'tenant.context': '平台运营', 'tenant.title': '租户管理', 'tenant.subtitle': '管理租户信息、生命周期与资源配额。',
  'tenant.list': '租户列表', 'tenant.code': '租户号', 'tenant.name': '租户名称', 'tenant.status': '状态', 'tenant.actions': '操作',
  'tenant.codeHint': '使用小写字母和数字，最多 64 个字符。', 'tenant.membership': '管理员成员 ID', 'tenant.subject': '管理员主体 ID',
  'tenant.quota': '用户配额', 'tenant.quotaHint': '留空表示不设上限。', 'tenant.unlimited': '未设置上限', 'tenant.usage': '当前用量', 'tenant.usageUnavailable': '用量不可用',
  'tenant.trial': '启用试用', 'tenant.validUntil': '有效期至', 'tenant.validUntilHint': '留空表示不设置到期时间。',
  'tenant.create': '新建租户', 'tenant.edit': '编辑租户', 'tenant.detail': '租户详情', 'tenant.refresh': '刷新', 'tenant.search': '搜索租户号或名称', 'tenant.allStatus': '全部状态',
  'tenant.status.pending': '待激活', 'tenant.status.trial': '试用中', 'tenant.status.active': '正常', 'tenant.status.grace': '宽限期', 'tenant.status.suspended': '已冻结',
  'tenant.action.activate': '激活', 'tenant.action.suspend': '冻结', 'tenant.action.resume': '恢复',
  'tenant.confirmSuspend': '冻结租户', 'tenant.confirmSuspendDescription': '冻结后，该租户的业务访问将暂停。',
  'tenant.created': '租户已创建', 'tenant.updated': '租户信息已保存', 'tenant.changed': '租户状态已更新',
  'tenant.conflict': '数据已更新，请刷新后重试，当前修改未覆盖他人的更新。', 'tenant.duplicate': '租户号已存在，请更换后重试。',
  'tenant.offline': '暂时无法连接服务，请稍后重试。', 'tenant.noAccess': '无权限访问租户管理', 'tenant.signIn': '需要身份凭据',
  'tenant.noAccessHint': '当前身份不能执行平台租户运营操作。', 'tenant.signInHint': '请使用有效的平台身份后重新加载。',
  'tenant.activateTitle': '激活租户', 'tenant.connectionError': '租户信息加载失败', 'tenant.detailsQuota': '资源配额',
});

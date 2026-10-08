// 自测用：沿用根配置的规则，只把范围换成样例目录。
const base = require('../../../.dependency-cruiser.cjs');
module.exports = { ...base, options: { ...base.options, includeOnly: '^tests/gates/yue/fixtures/' } };

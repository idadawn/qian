import '../index.css';
import { i18n } from '../i18n';

// 无头浏览器的首选语言随机器而定；用例按简体中文断言文案，英文另有用例切换。
await i18n.changeLanguage('zh-CN');

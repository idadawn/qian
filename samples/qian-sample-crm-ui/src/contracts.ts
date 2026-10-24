import type { PlatformClient } from '@idadawn/yue-sdk';
import type { RemoteBridge } from '@idadawn/qian-plugin-sdk';

/** 外壳交给页面的两样东西：平台客户端（规范路径 /api/...）与本远程的桥接句柄（声明过的导航、通知与能力调用）。 */
export interface RemotePageProps { client: PlatformClient; bridge: RemoteBridge }
/** customer.list 的输出（参考应用 CustomerPage）。 */
export interface Customer { id: string; tenantId: string; name: string }
export interface CustomerPage { total: number; items: Customer[] }
export const CUSTOMER_WRITE = 'qian.sample.crm:customer:write';

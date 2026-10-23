import { readFileSync, writeFileSync } from 'node:fs';
import { resolve } from 'node:path';
import AxeBuilder from '@axe-core/playwright';
import { expect, test, type Page } from '@playwright/test';

// 外壳真实流程验收（S1-YUE-002 验收 1–4；S1-YUE-012 把薄入口与 MF2 宿主融合进 apps/qian-shell 后迁入）：账号密码经 qian-identity 的 OIDC code + PKCE 登录
// （网关 BFF 换码，浏览器零令牌）→ 多租户选择 → 网关 bootstrap 聚合端点一次给出会话、上下文、已挂载远程、权限与能力导航 → 菜单命中的页面由参考应用的
// 远程模块（samples/qian-sample-crm-ui）经 MF2 按需加载并真实渲染、经 bridge.invoke 调参考应用能力 → prompt=select_account 切换租户 → 回调重建会话、
// 菜单与权限随之刷新 → 服务端访问令牌到期：透明刷新无感，会话仍在时 POST /auth/session/refresh 经真实身份服务换新令牌 → 身份修订递增后旧令牌 401
// （平台表面宿主的授权 PEP 按生产模式比对修订下限）：SDK 合并成一次刷新、换到新修订后重放，外壳随即重拉 bootstrap（裁决 ④）→ refresh 失效时依赖方
// 结束会话，401 合并成一次刷新尝试（探测即 401、不再 POST）并只弹一次过期对话框 → 代码审查项的自动检查（存储、URL、iframe、响应体里都没有令牌，
// 会话 Cookie 对脚本不可见，构建产物的 meta CSP 零违规）→ 改运行时配置 /app-config.json 的 apiBase 后刷新页面即走新前缀，不重编译。
// 另几条用例：平台身份（operator）只有代管横幅与会话卡片、bootstrap 不调任何下游；刷新成功但平台表面持续 401 时只刷新一次、只弹一个过期对话框、之后不再请求，整页重载也不跳登录、点「重新登录」只跳一次；
// SSO 跳转回调经真实上游 Dex（通用 OIDC 连接器）首次确认绑定、再次直接进入；验收 2：甲方已挂载参考应用时远程按需加载、丙方未挂载时 mf-manifest.json 从不请求；零令牌检测式自检不需要真实栈。
const root = resolve(import.meta.dirname, '../../..');
const seed = JSON.parse(readFileSync(resolve(root, 'tools/Qian.Dev.Shell/seed/accounts.json'), 'utf8')) as { persons: { key: string; username: string; password: string }[]; tenants: { code: string; name: string }[] };
const owner = seed.persons.find(person => person.key === 'owner')!;
const operator = seed.persons.find(person => person.key === 'operator')!;
const second = seed.persons.find(person => person.key === 'second')!;
// 上游 Dex 本地密码库里的用户（scripts/dev-shell.sh 用同一文件渲染 Dex 配置）。
const dex = JSON.parse(readFileSync(resolve(root, 'tools/Qian.Dev.Shell/seed/dex.json'), 'utf8')) as { email: string; password: string };
const acme = seed.tenants[0]!; const beta = seed.tenants[1]!; const gamma = seed.tenants[2]!;
const identity = (process.env.QIAN_DEV_SHELL_API_URL ?? 'http://127.0.0.1:5090').replace(/\/$/, '');
const state = process.env.QIAN_DEV_STATE ?? resolve(root, '.qian-dev/shell');
// 令牌检测：JWT（JWS / JWE 紧凑序列化）各段是 base64url，头段是 JSON，编码后以 eyJ 起头。两种形状都算令牌：令牌边界上的 eyJ 长串（JSON 字符串、URL、
// Bearer 头里的整段令牌，或单独的载荷段）；任何位置的 eyJ 段后接点号与下一段（百分号编码里 eyJ 前一个字符也可能是字母数字）。防伪令牌这类数据保护的
// 随机 base64url 串没有点号，中段偶然出现的 eyJ 不是令牌（S1-YUE-012 全量门禁实测误报过一次，见下面的检测式自检）。
const tokenLike = /(?<![A-Za-z0-9_-])eyJ[A-Za-z0-9_-]{20,}|eyJ[A-Za-z0-9_-]{10,}\.[A-Za-z0-9_-]{2,}|access_token|refresh_token|id_token|selection_token/;
const bootstrapPath = /^\/api(-alt)?\/shell\/bootstrap$/;

// 外壳构建产物带 meta CSP（apps/qian-shell/vite.config.ts）：每个文档都收集 securitypolicyviolation，用例结束时必须为零（认证页族在身份服务源上，不带这条 meta CSP）。
const violations: string[] = [];
test.beforeEach(async ({ page }) => {
  violations.length = 0;
  await page.exposeFunction('qianCspViolation', (detail: string) => { violations.push(detail); });
  await page.addInitScript(() => document.addEventListener('securitypolicyviolation', event => {
    (window as unknown as { qianCspViolation(detail: string): void }).qianCspViolation(`${location.origin} ${event.violatedDirective} ${event.blockedURI} ${event.sourceFile}:${event.lineNumber}`);
  }));
});
test.afterEach(() => { expect(violations, 'CSP 违规').toEqual([]); });
// Base UI 的 Tab 哨兵立即转移焦点；与靶场门禁一致，只排除这一类内部哨兵。
const focusGuards = '[data-base-ui-focus-guard]';

// 与 @yue/shell 浏览器测试的 axe 门一致：开场 / 退场属性清除后 opacity 过渡才开始，等真实动画结束再扫描，否则慢机器上会在半透明过渡中测出 color-contrast。
async function settled(page: Page) {
  await expect.poll(() => page.evaluate(() => document.querySelector('[data-starting-style], [data-ending-style]') === null)).toBe(true);
  await page.evaluate(async () => {
    let stableFrames = 0;
    while (stableFrames < 2) {
      await new Promise<void>(resolve => requestAnimationFrame(() => resolve()));
      const finite = document.getAnimations().filter(animation => animation.playState !== 'finished' && Number.isFinite(Number(animation.effect?.getComputedTiming().endTime)));
      if (finite.length) { await Promise.all(finite.map(animation => animation.finished.catch(() => undefined))); stableFrames = 0; } else stableFrames++;
    }
  });
}
async function accessible(page: Page, step: string) {
  await settled(page);
  const results = await new AxeBuilder({ page }).exclude(focusGuards).analyze();
  expect(results.violations.filter(violation => violation.impact === 'critical' || violation.impact === 'serious').map(violation => `${violation.id}: ${violation.nodes.map(node => node.target.join(' ')).join('; ')}`), step).toEqual([]);
}
async function noCredentials(page: Page, step: string) {
  const exposed = await page.evaluate(() => ({ url: location.href, cookie: document.cookie, frames: [...document.querySelectorAll('iframe')].map(frame => frame.src), local: JSON.stringify(localStorage), session: JSON.stringify(sessionStorage) }));
  expect(exposed.frames, step).toEqual([]);
  expect(exposed.cookie, step).not.toMatch(/qian\.sid|eyJ/);
  expect(JSON.stringify(exposed), step).not.toMatch(tokenLike);
  expect(exposed.url, step).not.toMatch(/[?&](code|state|token)=/);
}
// 开发路由与端点直调走浏览器上下文的 API 客户端：与页面共享 Cookie（含防伪 Cookie），但不进入页面的网络事件，页面侧的刷新与探测计数只算外壳自己发出的请求。
async function devPost(page: Page, path: string) {
  const csrf = await page.request.get('/auth/csrf', { headers: { Accept: 'application/json' } });
  expect(csrf.status(), 'csrf').toBe(200);
  const { headerName, token } = await csrf.json() as { headerName: string; token: string };
  const response = await page.request.post(path, { headers: { [headerName]: token, Accept: 'application/json' } });
  const text = await response.text();
  return { status: response.status(), text, body: text.length > 0 ? JSON.parse(text) as unknown : null };
}
async function devSession(page: Page, action: 'expire' | 'break') {
  expect((await devPost(page, `/__dev/session/${action}`)).status).toBe(204);
}
async function fillLogin(page: Page, person = owner) {
  await page.getByLabel(/账号/).fill(person.username);
  await page.getByLabel(/密码/).fill(person.password);
  await expect(page.getByLabel(/密码/)).toHaveAttribute('type', 'password');
  await page.getByRole('button', { name: '登录', exact: true }).click();
}
// 页面侧观测：外壳自己发出的刷新 POST、防伪探测（SDK 的会话刷新与写请求的防伪头都先 GET /auth/csrf，按步骤取基线比较）、/api 与 /auth 调用、
// 远程制品请求、全部同源 JSON 响应体；每个响应都断言请求不带 Authorization。上游 Dex 是别的源，不在其中。
function observe(page: Page, origin: string) {
  const refreshes: string[] = []; const probes: number[] = []; const apiCalls: { origin: string; method: string; path: string; status: number }[] = []; const bodies: string[] = [];
  const remotes: string[] = []; const logins: string[] = [];
  page.on('request', request => {
    const url = new URL(request.url());
    if (request.method() === 'POST' && url.pathname === '/auth/session/refresh') refreshes.push(url.pathname);
    if (url.pathname.startsWith('/remotes/')) remotes.push(url.pathname);
    // 主框架顶层跳依赖方登录端点（首载无会话、切换租户、重新登录）：用来证明不存在整页级的跳转循环。
    if (url.origin === origin && url.pathname === '/auth/login' && request.isNavigationRequest() && request.frame() === page.mainFrame()) logins.push(url.search);
  });
  page.on('response', async response => {
    const url = new URL(response.url());
    if (url.origin !== origin && url.origin !== identity) return;
    if (response.request().method() === 'GET' && url.pathname === '/auth/csrf') probes.push(response.status());
    if (/^\/(api|api-alt|auth)\//.test(url.pathname)) apiCalls.push({ origin: url.origin, method: response.request().method(), path: url.pathname, status: response.status() });
    if ((response.headers()['content-type'] ?? '').includes('json')) { try { bodies.push(await response.text()); } catch { /* 跳转或已取消的响应没有可读的体。 */ } }
    expect(response.request().headers().authorization, url.pathname).toBeUndefined();
  });
  return { refreshes, probes, apiCalls, bodies, remotes, logins };
}
const loginPage = new RegExp(`^${identity.replaceAll('.', '\\.')}/login\\?ReturnUrl=`);

// 不需要真实栈：零令牌断言靠这条检测式，先证明它对泄漏敏感、对防伪令牌不误报。JWS 的载荷段也以 eyJ 起头；JWE（OpenIddict 默认加密访问令牌）
// 只有头段以 eyJ 起头，百分号编码里它前面是字母数字，只能靠「eyJ 段后接点号」那一支命中。
test('零令牌检测式自检：JWS / JWE 在 JSON、URL、Bearer 头与百分号编码里都命中，单独的载荷段也命中，防伪令牌中段偶然出现的 eyJ 不算', () => {
  const b64 = (text: string) => Buffer.from(text).toString('base64url');
  const payload = b64('{"sub":"owner","scope":"openid"}');
  const jws = [b64('{"alg":"RS256","typ":"JWT"}'), payload, b64('signature')].join('.');
  const jwe = [b64('{"alg":"RSA-OAEP","enc":"A256CBC-HS512"}'), b64('encrypted-key'), b64('iv'), b64('ciphertext'), b64('tag')].join('.');
  const contexts = [(token: string) => `{"value":"${token}"}`, (token: string) => `https://localhost:5443/#x=${token}`, (token: string) => `Bearer ${token}`, (token: string) => encodeURIComponent(JSON.stringify({ token }))];
  for (const leaked of [...[jws, jwe].flatMap(token => contexts.map(wrap => wrap(token))), `{"claims":"${payload}"}`, '{"refresh_token":"opaque"}'])
    expect(leaked, leaked).toMatch(tokenLike);
  const antiforgery = `{"header":"RequestVerificationToken","token":"CfDJ8${'Qx7_k3Zp'.repeat(8)}ueyJP4TE4U_GkLtBGdzgF-p0CZm${'W9-a'.repeat(6)}"}`;
  expect(antiforgery).not.toMatch(tokenLike);
});

test('验收 1 / 3 / 4：真实 OIDC 登录、多租户选择、远程页面真实渲染、切换租户、刷新合并与过期对话框、零令牌、运行时配置', async ({ page, baseURL }) => {
  const origin = baseURL!.replace(/\/$/, '');
  const { refreshes, probes, apiCalls, bodies, remotes } = observe(page, origin);
  const bootstraps = (from: number, status = 200) => apiCalls.slice(from).filter(call => bootstrapPath.test(call.path) && call.status === status).length;

  // 1. 匿名进入外壳 → BFF 没有会话 → 顶层跳到身份服务的同源认证页（账号 Cookie 挑战把授权请求放进 ReturnUrl）。
  await page.goto('/');
  await expect(page).toHaveURL(loginPage);
  await expect(page.getByRole('heading', { name: '登录', exact: true })).toBeVisible();
  await accessible(page, '登录页');
  await fillLogin(page);
  // 2. demo 同时属于两个租户：二次选择。
  await expect(page.getByRole('heading', { name: '选择租户', exact: true })).toBeVisible();
  await accessible(page, '选择租户');
  await page.getByRole('button', { name: acme.name, exact: true }).click();
  // 3. 回到授权请求签码 → 网关回调建 qian.sid → 工作台：会话快照、菜单与权限都来自网关 bootstrap（聚合真实平台表面），浏览器不再分别直调导航与权限。
  await expect(page).toHaveURL(origin + '/');
  await expect(page.getByRole('heading', { name: '工作台', exact: true })).toBeVisible();
  const sessionCard = page.getByTestId('shell-session');
  await expect(sessionCard.locator('[data-field=displayName]')).toHaveText('演示用户');
  await expect(sessionCard.locator('[data-field=kind]')).toHaveText('user');
  const tenantA = await sessionCard.locator('[data-field=tenantId]').textContent();
  expect(tenantA).toMatch(/^[0-9a-f-]{36}$/);
  const nav = page.getByTestId('yue-app-shell-sidebar').getByRole('link');
  await expect(nav).toHaveText(['客户', '新建客户']);
  await expect(page.getByTestId('shell-permissions').locator('[data-permission="qian.sample.crm:customer:write"]')).toHaveAttribute('data-allowed', 'true');
  await expect(page.getByTestId('shell-capabilities')).not.toHaveText('加载中');
  await accessible(page, '工作台');
  await noCredentials(page, '登录后');
  expect(bootstraps(0)).toBeGreaterThan(0);
  expect(apiCalls.filter(call => ['/api/capabilities/navigation', '/api/authz/me/permissions'].includes(call.path))).toEqual([]);
  // 远程模块按需加载：工作台上还没有任何远程制品请求。
  expect(remotes).toEqual([]);

  // 4. 菜单 → 路由（pushState）：「客户」页由参考应用的远程模块真实渲染（MF2 manifest → remoteEntry → expose），远程拿到的上下文是当前租户；
  //    列表经 bridge.invoke 调 customer.list：外壳先取防伪令牌、按规范路径经网关发到平台表面的通用执行入口（能力对该用户可见：scope crm.read + customer:read）。
  //    参考应用的命令运行时只认机器主体（CrmCurrentUser，S1 既有边界），用户主体在处理器级 PEP 被拒（403），远程页面优雅降级为错误提示、外壳不受影响；
  //    用户主体执行业务能力的身份透传另立任务（见 apps/qian-shell/README.md 已知限制）。「新建客户」页同样由远程渲染。
  const listed = page.waitForResponse(response => new URL(response.url()).pathname === '/api/capabilities/qian.sample.crm/customer.list/invoke');
  await page.getByRole('link', { name: '客户', exact: true }).click();
  await expect(page).toHaveURL(origin + '/customers');
  const customers = page.getByTestId('qian-sample-crm-customers');
  await expect(customers.getByRole('heading', { name: '客户', exact: true })).toBeVisible();
  await expect(customers).toHaveAttribute('data-tenant', tenantA!);
  await expect(page.getByRole('link', { name: '客户', exact: true })).toHaveAttribute('aria-current', 'page');
  expect(remotes).toContain('/remotes/qian.sample.crm/mf-manifest.json');
  expect(remotes).toContain('/remotes/qian.sample.crm/remoteEntry.js');
  await expect(customers.getByRole('alert')).toHaveText('客户列表加载失败，请稍后重试。');
  expect(apiCalls.filter(call => call.path === '/api/capabilities/qian.sample.crm/customer.list/invoke').map(call => `${call.method} ${call.status}`)).toEqual(['POST 403']);
  const denied = await listed;
  // 写类调用之外的能力执行也是 POST：外壳的写请求中间件先取防伪令牌（Identity.Client 的 X-CSRF-TOKEN），通过网关的防伪校验后才到平台表面。
  expect(denied.request().headers()['x-csrf-token']).toBeTruthy();
  expect(await denied.json()).toMatchObject({ status: 403, code: 'AUTHZ_DENIED' });
  await expect(page.getByTestId('yue-app-shell-sidebar')).toBeVisible();
  await customers.getByRole('button', { name: '新建客户', exact: true }).click();
  await expect(page).toHaveURL(origin + '/customers/new');
  await expect(page.getByTestId('qian-sample-crm-create').getByRole('heading', { name: '新建客户', exact: true })).toBeVisible();
  await expect(page.getByRole('link', { name: '新建客户', exact: true })).toHaveAttribute('aria-current', 'page');
  await accessible(page, '远程页面');
  await page.goto('/no-such-page');
  await expect(page.getByRole('heading', { name: '页面不存在', exact: true })).toBeVisible();
  await page.getByRole('button', { name: '返回工作台', exact: true }).click();
  await expect(page).toHaveURL(origin + '/');
  // 带查询串的根路径仍是工作台（路由按 pathname 匹配）。
  await page.goto('/?x=1');
  await expect(page.getByRole('heading', { name: '工作台', exact: true })).toBeVisible();

  // 5. 切换租户：prompt=select_account → 身份服务同源选择页 → 乙方 → 新授权码 → 回调重建会话 → 菜单与权限随之刷新（乙方只有 customer:read）。
  await page.getByRole('button', { name: '用户菜单' }).click();
  await page.getByRole('menuitem', { name: '切换租户' }).click();
  await expect(page).toHaveURL(new RegExp(`^${identity.replaceAll('.', '\\.')}/login/select-tenant\\?return_url=`));
  await expect(page.getByRole('heading', { name: '选择租户', exact: true })).toBeVisible();
  await page.getByRole('button', { name: beta.name, exact: true }).click();
  await expect(page).toHaveURL(origin + '/');
  await expect(sessionCard.locator('[data-field=displayName]')).toHaveText('演示用户（乙方）');
  const tenantB = await sessionCard.locator('[data-field=tenantId]').textContent();
  expect(tenantB).toMatch(/^[0-9a-f-]{36}$/); expect(tenantB).not.toBe(tenantA);
  await expect(nav).toHaveText(['客户']);
  await expect(page.getByTestId('shell-permissions').locator('[data-permission="qian.sample.crm:customer:write"]')).toHaveCount(0);
  await expect(page.getByTestId('shell-permissions').locator('[data-permission="qian.sample.crm:customer:read"]')).toHaveAttribute('data-allowed', 'true');
  await noCredentials(page, '切换租户后');

  // 6. 服务端访问令牌到期：下一批请求由依赖方透明刷新（单飞），页面无感，浏览器不发刷新请求、不探测，也没有对话框。
  //    一轮工作区 = bootstrap（聚合会话、上下文、远程、权限与导航）+ 能力目录条数（辅助接口）两条 GET。
  await devSession(page, 'expire');
  const before = apiCalls.length; const probesBefore = probes.length;
  await page.getByTestId('shell-reload').click();
  await expect.poll(() => apiCalls.slice(before).filter(call => call.path.startsWith('/api/') && call.status === 200).length).toBeGreaterThanOrEqual(2);
  expect(bootstraps(before)).toBe(1);
  expect(refreshes).toHaveLength(0); expect(probes.slice(probesBefore)).toHaveLength(0);
  await expect(page.getByRole('alertdialog')).toHaveCount(0);
  // 6b. 会话仍在时强制刷新（SDK sessionProvider 遇 401 走的就是这条 POST）：依赖方拿 refresh token 向真实身份服务换新令牌，回快照不回令牌；之后请求照常 200。
  const forced = await devPost(page, '/auth/session/refresh');
  expect(forced.status).toBe(200);
  expect(forced.body).toMatchObject({ kind: 'user', tenantId: tenantB, displayName: '演示用户（乙方）' });
  expect(forced.text).not.toMatch(tokenLike);
  const afterForced = apiCalls.length;
  await page.getByTestId('shell-reload').click();
  await expect.poll(() => apiCalls.slice(afterForced).filter(call => call.path.startsWith('/api/') && call.status === 200).length).toBeGreaterThanOrEqual(2);
  // 6c. 会话刷新后重拉（裁决 ④）：经真实身份端口递增当前成员的 identity_revision，并经平台表面宿主的真实 PEP 把修订下限抬到新修订（同进程宿主里授权变更后
  // 就是这样做的）→ bootstrap 的下游按旧修订 401、网关原样回 401 → SDK 一次 POST /auth/session/refresh（先取防伪令牌，探测 200）→ 依赖方换到带新修订的令牌
  // → 重放 200 → 刷新成功的通知先于重放到达，外壳排队再拉一轮 bootstrap（会话快照身份修订 +1）；全程没有过期对话框。
  const revision = Number(await sessionCard.locator('[data-field=identityRevision]').textContent());
  expect(revision).toBeGreaterThan(0);
  const bumped = await devPost(page, '/__dev/identity/bump');
  expect(bumped.status).toBe(200);
  expect(bumped.body).toEqual({ identityRevision: revision + 1 });
  const afterBump = apiCalls.length; const probesBump = probes.length;
  await page.getByTestId('shell-reload').click();
  await expect.poll(() => refreshes.length).toBe(1);
  await expect.poll(() => bootstraps(afterBump)).toBeGreaterThanOrEqual(2);
  expect(bootstraps(afterBump, 401)).toBe(1);
  expect(probes.slice(probesBump)).toEqual([200]);
  await expect(sessionCard.locator('[data-field=identityRevision]')).toHaveText(String(revision + 1));
  await expect(nav).toHaveText(['客户']);
  await expect(page.getByRole('alertdialog')).toHaveCount(0);
  // 7. refresh token 失效：依赖方刷新失败即结束会话，bootstrap 401；SDK 的刷新尝试——会话已不在，取防伪令牌这一步即 401、不再发 POST——只弹一个过期对话框；
  //    对话框不可被 Esc 关掉。
  await devSession(page, 'break');
  const beforeBreak = apiCalls.length; const probesBreak = probes.length;
  await page.getByTestId('shell-reload').click();
  const dialog = page.getByRole('alertdialog', { name: '登录已过期' });
  await expect(dialog).toHaveCount(1);
  await expect.poll(() => bootstraps(beforeBreak, 401)).toBeGreaterThanOrEqual(1);
  await expect.poll(() => probes.slice(probesBreak)).toEqual([401]);
  expect(refreshes).toHaveLength(1);
  await page.keyboard.press('Escape');
  await expect(dialog).toBeVisible();
  await accessible(page, '过期对话框');
  // 8. 重新登录：身份服务的账号 Cookie 仍绑定乙方会话，直接签新授权码回到工作台，不再要求选择。
  await dialog.getByRole('button', { name: '重新登录' }).click();
  await expect(page).toHaveURL(origin + '/');
  await expect(sessionCard.locator('[data-field=displayName]')).toHaveText('演示用户（乙方）');
  await expect(page.getByRole('alertdialog')).toHaveCount(0);

  // 9. 侧栏 / 主题 / 语言：顶栏控件切换即时生效（侧栏宽度、html[data-theme]、全部文案），偏好只落这三项非敏感值。
  await page.getByRole('button', { name: '收起侧栏' }).click();
  await expect(page.getByRole('button', { name: '展开侧栏' })).toBeVisible();
  await page.getByRole('combobox', { name: '主题' }).click();
  await page.getByRole('option', { name: '暗色' }).click();
  await expect.poll(() => page.evaluate(() => document.documentElement.dataset.theme)).toBe('dark');
  await page.getByRole('combobox', { name: '语言' }).click();
  await page.getByRole('option', { name: 'en-US' }).click();
  await expect(page.getByRole('button', { name: 'Expand sidebar' })).toBeVisible();
  await expect(page.getByRole('heading', { name: 'Workspace', exact: true })).toBeVisible();
  await expect.poll(() => page.evaluate(() => document.documentElement.lang)).toBe('en-US');
  await page.getByRole('combobox', { name: 'Language' }).click();
  await page.getByRole('option', { name: 'zh-CN' }).click();
  await expect(page.getByRole('heading', { name: '工作台', exact: true })).toBeVisible();
  await expect.poll(() => page.evaluate(() => document.documentElement.lang)).toBe('zh-CN');
  await page.getByRole('button', { name: '展开侧栏' }).click();
  await page.getByRole('combobox', { name: '主题' }).click();
  await page.getByRole('option', { name: '明亮' }).click();
  await expect.poll(() => page.evaluate(() => document.documentElement.dataset.theme)).toBe('light');
  expect(await page.evaluate(() => JSON.parse(localStorage.getItem('yue.preferences.qian-shell') ?? '{}') as unknown)).toEqual({ theme: 'light', locale: 'zh-CN', sidebarCollapsed: false });
  // 代码审查项的自动检查：期间所有同源 JSON 响应体都不含令牌；存储、URL、iframe 同样干净。
  for (const body of bodies) expect(body).not.toMatch(tokenLike);
  await noCredentials(page, '全程');

  // 10. 运行时配置：改 ConfigMap 替身 /app-config.json 的 apiBase 为 /api-alt → 刷新页面即生效（网关每次现读、no-store），菜单照常渲染，
  //     bootstrap 与能力目录都走新前缀（规范路径 /api/... 由外壳客户端映射）；构建产物未变。
  const conf = resolve(state, 'app-config.json'); const original = readFileSync(conf, 'utf8');
  try {
    writeFileSync(conf, JSON.stringify({ apiBase: '/api-alt', brand: 'Qian 乾（alt）' }) + '\n');
    const mark = apiCalls.length;
    await page.reload();
    await expect(page.getByTestId('yue-app-shell-sidebar')).toContainText('Qian 乾（alt）');
    await expect(nav).toHaveText(['客户']);
    await expect.poll(() => apiCalls.slice(mark).filter(call => call.path.startsWith('/api-alt/') && call.status === 200).length).toBeGreaterThanOrEqual(2);
    expect(apiCalls.slice(mark).filter(call => call.path === '/api-alt/shell/bootstrap' && call.status === 200)).toHaveLength(1);
    expect(apiCalls.slice(mark).filter(call => call.path.startsWith('/api/'))).toEqual([]);
  } finally { writeFileSync(conf, original); }

  // 11. 退出：BFF 会话结束，再访问受保护接口是 401。
  await page.getByRole('button', { name: '用户菜单' }).click();
  await page.getByRole('menuitem', { name: '退出登录' }).click();
  await expect(page.getByTestId('shell-signed-out')).toBeVisible();
  expect(await page.evaluate(async () => (await fetch('/auth/session', { credentials: 'same-origin' })).status)).toBe(401);
  await noCredentials(page, '退出后');
});

test('平台身份（operator）：只有代管横幅与会话卡片，bootstrap 不调任何下游、不拉能力目录，不出现过期对话框，零令牌', async ({ page, baseURL }) => {
  const origin = baseURL!.replace(/\/$/, '');
  const { refreshes, apiCalls, bodies } = observe(page, origin);
  await page.goto('/');
  await expect(page).toHaveURL(loginPage);
  await fillLogin(page, operator);
  // 平台身份没有租户成员身份，不经过选择页；网关回调建会话后外壳只调一次 bootstrap：网关对无租户主体不调任何下游，直接回代管上下文
  // （平台表面宿主对无租户主体一律 401，否则就会弹过期对话框），外壳也不再拉能力目录。
  await expect(page).toHaveURL(origin + '/');
  await expect(page.getByRole('heading', { name: '工作台', exact: true })).toBeVisible();
  await expect(page.getByTestId('yue-app-shell-managed')).toContainText('当前处于平台代管模式');
  const sessionCard = page.getByTestId('shell-session');
  await expect(sessionCard.locator('[data-field=displayName]')).toHaveText('平台运营');
  await expect(sessionCard.locator('[data-field=tenantId]')).toHaveText('平台身份');
  await expect(sessionCard.locator('[data-field=kind]')).toHaveText('user');
  await expect(page.getByTestId('shell-permissions')).toHaveCount(0);
  await expect(page.getByTestId('shell-pages')).toHaveCount(0);
  await expect(page.getByTestId('shell-reload')).toHaveCount(0);
  await accessible(page, '平台身份工作台');
  await expect(page.getByRole('alertdialog')).toHaveCount(0);
  // 认证页族在身份服务同源的 /api/identity/* 调用（登录、me）不算：这里只看外壳经网关发出的请求——匿名首访的那次 bootstrap 401（随即顶层跳登录），
  // 登录后恰好一次 bootstrap 200，再没有别的租户表面请求。
  expect(apiCalls.filter(call => call.origin === origin && /^\/(api|api-alt)\//.test(call.path)))
    .toEqual([{ origin, method: 'GET', path: '/api/shell/bootstrap', status: 401 }, { origin, method: 'GET', path: '/api/shell/bootstrap', status: 200 }]);
  expect(refreshes).toHaveLength(0);
  for (const body of bodies) expect(body).not.toMatch(tokenLike);
  await noCredentials(page, '平台身份');
});

test('刷新成功但平台表面持续 401：恰好一次刷新、一个过期对话框，之后外壳不再发起新一轮（会话结束是粘性的）；整页重载不跳登录，点「重新登录」只跳一次', async ({ page, baseURL }) => {
  const origin = baseURL!.replace(/\/$/, '');
  const { refreshes, apiCalls, bodies, logins } = observe(page, origin);
  // 只看外壳经网关发往租户表面的请求；认证页族在身份服务同源的 /api/identity/* 与 BFF 的 /auth/* 不算。
  const tenantCalls = () => apiCalls.filter(call => call.origin === origin && /^\/(api|api-alt)\//.test(call.path));
  await page.goto('/');
  await expect(page).toHaveURL(loginPage);
  await fillLogin(page, second);
  // second 只属于乙方：不经过选择页，回调后直接进工作台，首轮 bootstrap 与能力目录两条 200。
  await expect(page).toHaveURL(origin + '/');
  const sessionCard = page.getByTestId('shell-session');
  await expect(sessionCard.locator('[data-field=kind]')).toHaveText('user');
  await expect(page.getByTestId('shell-capabilities')).not.toHaveText('加载中');
  await expect.poll(() => tenantCalls().filter(call => call.status === 200).length).toBeGreaterThanOrEqual(2);
  const revision = Number(await sessionCard.locator('[data-field=identityRevision]').textContent());
  expect(revision).toBeGreaterThan(0);
  // 只抬平台表面的修订下限（经平台表面宿主的真实 PEP 懒抬高）、不递增身份修订：依赖方刷新仍成功，换到的令牌修订不变，平台表面持续 401（下限高于身份侧已签发的修订）。
  const stale = await devPost(page, '/__dev/identity/stale');
  expect(stale.status).toBe(200);
  expect(stale.body).toEqual({ identityRevision: revision, floor: revision + 1 });
  const before = tenantCalls().length;
  await page.getByTestId('shell-reload').click();
  // bootstrap 401 → SDK 一次刷新（成功）→ 刷新成功的通知先于重放到达（外壳排队一整轮）→ 重放仍 401 → 只通知一次过期 →
  // 会话结束对外壳是粘性的：排队的整轮不再发出，也不会因「下一批 401 重新探测」再刷新。静置 3 秒后请求数不变。
  const dialog = page.getByRole('alertdialog', { name: '登录已过期' });
  await expect(dialog).toHaveCount(1);
  await expect.poll(() => tenantCalls().length - before).toBeGreaterThanOrEqual(2);
  const settled = { calls: tenantCalls().length, refreshes: refreshes.length };
  await page.waitForTimeout(3_000);
  expect({ calls: tenantCalls().length, refreshes: refreshes.length }, '过期对话框之后静置 3 秒').toEqual(settled);
  expect(refreshes).toHaveLength(1);
  expect(tenantCalls().slice(before).map(call => `${call.path} ${call.status}`)).toEqual(['/api/shell/bootstrap 401', '/api/shell/bootstrap 401']);
  await expect(page.getByRole('alertdialog')).toHaveCount(1);
  await expect(dialog).toBeVisible();
  // 整页重载：工作台从未就绪，依赖方会话仍在、平台表面持续 401。刷新成功说明会话还在，外壳只弹过期对话框、不顶层跳登录——否则账号 Cookie
  // 仍有效时登录端点静默签码、回调重建会话，bootstrap 照样 401，再跳，整页无界循环（第一轮验收复核实测重载后 10 秒跳了 42 次）。
  const counts = () => ({ logins: logins.length, calls: tenantCalls().length, refreshes: refreshes.length });
  const beforeReload = counts();
  await page.reload();
  await expect(dialog).toHaveCount(1);
  await expect.poll(() => tenantCalls().length - beforeReload.calls).toBe(2);
  const reloaded = counts();
  await page.waitForTimeout(3_000);
  expect(counts(), '重载后静置 3 秒').toEqual(reloaded);
  expect(reloaded.logins - beforeReload.logins, '重载后顶层跳 /auth/login 的次数').toBe(0);
  expect(reloaded.refreshes - beforeReload.refreshes).toBe(1);
  expect(tenantCalls().slice(beforeReload.calls).map(call => `${call.path} ${call.status}`)).toEqual(['/api/shell/bootstrap 401', '/api/shell/bootstrap 401']);
  await expect(page).toHaveURL(origin + '/');
  // 对话框里点「重新登录」：恰好一次顶层跳转（账号 Cookie 仍有效，依赖方静默签码、回调回到工作台），平台表面仍 401 → 再弹对话框，之后整页稳定。
  const beforeSignIn = counts();
  await dialog.getByRole('button', { name: '重新登录' }).click();
  await expect.poll(() => tenantCalls().length - beforeSignIn.calls).toBe(2);
  await expect(dialog).toHaveCount(1);
  const signedIn = counts();
  await page.waitForTimeout(3_000);
  expect(counts(), '重新登录后静置 3 秒').toEqual(signedIn);
  expect(signedIn.logins - beforeSignIn.logins, '重新登录后顶层跳 /auth/login 的次数').toBe(1);
  expect(signedIn.refreshes - beforeSignIn.refreshes).toBe(1);
  await expect(page).toHaveURL(origin + '/');
  for (const body of bodies) expect(body).not.toMatch(tokenLike);
  await noCredentials(page, '平台表面持续 401');
});

test('验收 1（SSO 跳转回调）：连接器按钮 → Dex 上游登录 → /login/connector/dex 落点确认绑定 → 回到授权请求签码 → 工作台；再次 SSO 直接进入', async ({ page, baseURL }) => {
  const origin = baseURL!.replace(/\/$/, '');
  const { apiCalls, bodies } = observe(page, origin);
  const landing = new RegExp(`^${identity.replaceAll('.', '\\.')}/login/connector/dex\\?flow=[0-9a-f-]{36}$`);
  // 连接器登录的绑定键是 租户 × 连接器 × 上游主体：登录页先填租户代码再点连接器；发起经同源 POST /api/identity/connectors/dex/begin，页面顶层跳到上游 Dex；
  // Dex 本地密码库登录（登录名与口令来自种子文件）、跳过授权确认页后带 code + state 回统一回调 /connect/callback/dex，身份服务校验 state / nonce / PKCE 并换码后 302 到落点。
  async function throughDex() {
    await page.goto('/');
    await expect(page).toHaveURL(loginPage);
    const connectors = page.getByTestId('identity-connectors');
    await expect(connectors.getByRole('button', { name: 'Dex 本机身份源' })).toBeVisible();
    await connectors.getByLabel('租户代码').fill(acme.code);
    await connectors.getByRole('button', { name: 'Dex 本机身份源' }).click();
    await expect(page).toHaveURL(/^http:\/\/127\.0\.0\.1:\d+\/dex\/auth/);
    await page.locator('input[name=login]').fill(dex.email);
    await page.locator('input[name=password]').fill(dex.password);
    await page.locator('#submit-login').click();
  }
  // 1. 首次：上游身份尚未绑定 → 落点进入确认绑定：先用甲方账号密码验证（流程租户内的新鲜认证），再「绑定到已验证账号」→ 账号 Cookie 建立 → 回到授权请求签码 → 网关回调 → 工作台。
  await throughDex();
  await expect(page).toHaveURL(landing);
  const bind = page.getByTestId('identity-connector-bind');
  await expect(bind).toBeVisible();
  await expect(bind).toContainText('尚未绑定账号');
  await accessible(page, '确认绑定');
  await noCredentials(page, '连接器落点');
  await bind.getByLabel(/账号/).fill(owner.username);
  await bind.getByLabel(/密码/).fill(owner.password);
  await bind.getByRole('button', { name: '登录', exact: true }).click();
  const link = bind.getByRole('button', { name: /^绑定到已验证账号/ });
  await expect(link).toContainText('演示用户');
  await link.click();
  await expect(page).toHaveURL(origin + '/');
  const sessionCard = page.getByTestId('shell-session');
  await expect(sessionCard.locator('[data-field=displayName]')).toHaveText('演示用户');
  await expect(sessionCard.locator('[data-field=kind]')).toHaveText('user');
  await expect(page.getByTestId('yue-app-shell-sidebar').getByRole('link')).toHaveText(['客户', '新建客户']);
  await noCredentials(page, 'SSO 首次登录后');
  // 2. 再次：清掉全部 Cookie（网关会话、账号 Cookie、浏览器流程密钥）后重走；绑定已存在，回调直接签发会话，落点读到完成态即回授权请求，不再确认绑定。
  // 落点一读到完成态就跳走，事后在外壳源上查不到它：途中记录主框架经过的地址，并在每个文档里观察是否出现过绑定卡片。
  await page.context().clearCookies();
  const visited: string[] = [];
  page.on('framenavigated', frame => { if (frame === page.mainFrame()) visited.push(frame.url()); });
  const bindSeen: string[] = [];
  await page.exposeFunction('qianE2eBindSeen', (where: string) => { bindSeen.push(where); });
  await page.addInitScript(() => {
    let reported = false;
    new MutationObserver(() => {
      if (reported || !document.querySelector('[data-testid="identity-connector-bind"]')) return;
      reported = true; (window as unknown as { qianE2eBindSeen(where: string): void }).qianE2eBindSeen(location.href);
    }).observe(document, { childList: true, subtree: true });
  });
  await throughDex();
  await expect(page).toHaveURL(origin + '/');
  await expect(sessionCard.locator('[data-field=displayName]')).toHaveText('演示用户');
  expect(visited.filter(url => landing.test(url)), '第二遍经过连接器落点').toHaveLength(1);
  expect(bindSeen, '第二遍的落点不出现绑定卡片').toEqual([]);
  await expect(page.getByTestId('yue-app-shell-sidebar').getByRole('link')).toHaveText(['客户', '新建客户']);
  await expect.poll(() => apiCalls.filter(call => bootstrapPath.test(call.path) && call.status === 200).length).toBeGreaterThanOrEqual(2);
  for (const body of bodies) expect(body).not.toMatch(tokenLike);
  await noCredentials(page, 'SSO 再次登录后');
});

test('验收 2（S1-YUE-012 补验）：甲方已挂载参考应用时远程按需加载并出现在导航；切到丙方（未挂载）后 bootstrap 不含它、导航为空、mf-manifest.json 从不请求', async ({ page, baseURL }) => {
  const origin = baseURL!.replace(/\/$/, '');
  const { remotes, bodies } = observe(page, origin);
  const sessionCard = page.getByTestId('shell-session');
  const nav = page.getByTestId('yue-app-shell-sidebar').getByRole('link');
  await page.goto('/');
  await expect(page).toHaveURL(loginPage);
  await fillLogin(page);
  await expect(page.getByRole('heading', { name: '选择租户', exact: true })).toBeVisible();
  await expect(page.getByRole('button', { name: gamma.name, exact: true })).toBeVisible();
  await page.getByRole('button', { name: acme.name, exact: true }).click();
  await expect(page).toHaveURL(origin + '/');
  // 1. 甲方 scope 已挂载参考应用：导航里有「客户」，但工作台上不请求任何远程制品；进入页面才 loadRemote（manifest → remoteEntry → expose）。
  await expect(nav).toHaveText(['客户', '新建客户']);
  expect(remotes).toEqual([]);
  await nav.filter({ hasText: /^客户$/ }).click();
  await expect(page.getByTestId('qian-sample-crm-customers')).toBeVisible();
  expect(remotes[0]).toBe('/remotes/qian.sample.crm/mf-manifest.json');
  expect(remotes).toContain('/remotes/qian.sample.crm/remoteEntry.js');
  // 2. 切到丙方：prompt=select_account → 选择页 → 丙方 → 回调整页回到工作台（新文档、新宿主）。
  await page.getByRole('button', { name: '用户菜单' }).click();
  await page.getByRole('menuitem', { name: '切换租户' }).click();
  await expect(page.getByRole('heading', { name: '选择租户', exact: true })).toBeVisible();
  const switched = remotes.length;
  const bootstrap = page.waitForResponse(response => bootstrapPath.test(new URL(response.url()).pathname) && response.status() === 200);
  await page.getByRole('button', { name: gamma.name, exact: true }).click();
  const body = await (await bootstrap).json() as { context: { tenant: { name: string } }; remotes: unknown[]; mounted: string[]; entitled: string[]; navigation: unknown[] };
  await expect(page).toHaveURL(origin + '/');
  await expect(sessionCard.locator('[data-field=displayName]')).toHaveText('演示用户（丙方）');
  // 3. 丙方 scope 没有挂载参考应用：bootstrap 不含它（远程、挂载、权益、导航都空，响应里没有它的 mf-manifest.json 地址），导航为空，直达页面路由是 404，
  //    整个丙方会话里没有一条远程制品请求。
  expect(body.context.tenant.name).toBe(gamma.name);
  expect({ remotes: body.remotes, mounted: body.mounted, entitled: body.entitled, navigation: body.navigation }).toEqual({ remotes: [], mounted: [], entitled: [], navigation: [] });
  expect(JSON.stringify(body)).not.toContain('mf-manifest.json');
  await expect(nav).toHaveCount(0);
  await page.goto('/customers');
  await expect(page.getByRole('heading', { name: '页面不存在', exact: true })).toBeVisible();
  await page.goto('/customers/new');
  await expect(page.getByRole('heading', { name: '页面不存在', exact: true })).toBeVisible();
  expect(remotes.slice(switched)).toEqual([]);
  for (const text of bodies) expect(text).not.toMatch(tokenLike);
  await noCredentials(page, '丙方');
});

test('无效入口不渲染登录表单；跨源与非授权端点的回跳一律拒绝', async ({ page }) => {
  for (const query of ['', '?ReturnUrl=https%3A%2F%2Fevil.example%2F', '?ReturnUrl=%2Fsomewhere%3Fclient_id%3Dqian-shell', '?ReturnUrl=%2Fconnect%2Fauthorize%3Faccess_token%3Dx']) {
    await page.goto(`${identity}/login${query}`);
    await expect(page.getByTestId('identity-invalid-entry')).toBeVisible();
    await expect(page.getByLabel(/密码/)).toHaveCount(0);
  }
});

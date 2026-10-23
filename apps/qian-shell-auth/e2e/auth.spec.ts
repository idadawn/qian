import { readFileSync, writeFileSync } from 'node:fs';
import { resolve } from 'node:path';
import AxeBuilder from '@axe-core/playwright';
import { expect, test, type Page } from '@playwright/test';

// S1-YUE-002 真实流程验收：账号密码经 qian-identity 的 OIDC code + PKCE 登录（网关 BFF 换码，浏览器零令牌）→ 多租户选择 → 菜单 / 权限
// 来自真实能力导航目录与授权接口 → prompt=select_account 切换租户 → 回调重建会话、令牌重签、菜单与权限随之刷新 → 服务端访问令牌到期：
// 透明刷新无感，会话仍在时 POST /auth/session/refresh 经真实身份服务换新令牌 → 身份修订递增后旧令牌 401：SDK 合并成一次刷新、换到新修订后重放，
// 外壳随即重拉菜单、权限与会话快照（裁决 ④）→ refresh 失效时依赖方结束会话，并发 401 合并成一次刷新尝试（探测即 401、不再 POST）并只弹一次过期对话框
// → 代码审查项的自动检查（存储、URL、iframe、响应体里都没有令牌，会话 Cookie 对脚本不可见）→ 改运行时配置 apiBase 后刷新页面即走新前缀，不重编译。
// 另两条用例：平台身份（operator）只有代管横幅与会话卡片、不拉租户目录；SSO 跳转回调经真实上游 Dex（通用 OIDC 连接器）首次确认绑定、再次直接进入。
const root = resolve(import.meta.dirname, '../../..');
const seed = JSON.parse(readFileSync(resolve(root, 'tools/Qian.Dev.Shell/seed/accounts.json'), 'utf8')) as { persons: { key: string; username: string; password: string }[]; tenants: { code: string; name: string }[] };
const owner = seed.persons.find(person => person.key === 'owner')!;
const operator = seed.persons.find(person => person.key === 'operator')!;
// 上游 Dex 本地密码库里的用户（scripts/dev-shell.sh 用同一文件渲染 Dex 配置）。
const dex = JSON.parse(readFileSync(resolve(root, 'tools/Qian.Dev.Shell/seed/dex.json'), 'utf8')) as { email: string; password: string };
const acme = seed.tenants[0]!; const beta = seed.tenants[1]!;
const identity = (process.env.QIAN_DEV_SHELL_API_URL ?? 'http://127.0.0.1:5090').replace(/\/$/, '');
const state = process.env.QIAN_DEV_STATE ?? resolve(root, '.qian-dev/shell');
const tokenLike = /eyJ[A-Za-z0-9_-]{20,}|access_token|refresh_token|id_token|selection_token/;
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
// 页面侧观测：外壳自己发出的刷新 POST、防伪探测、/api 与 /auth 调用、全部同源 JSON 响应体；每个响应都断言请求不带 Authorization。上游 Dex 是别的源，不在其中。
function observe(page: Page, origin: string) {
  const refreshes: string[] = []; const probes: number[] = []; const apiCalls: { origin: string; method: string; path: string; status: number }[] = []; const bodies: string[] = [];
  page.on('request', request => { const url = new URL(request.url()); if (request.method() === 'POST' && url.pathname === '/auth/session/refresh') refreshes.push(url.pathname); });
  page.on('response', async response => {
    const url = new URL(response.url());
    if (url.origin !== origin && url.origin !== identity) return;
    if (response.request().method() === 'GET' && url.pathname === '/auth/csrf') probes.push(response.status());
    if (/^\/(api|api-alt|auth)\//.test(url.pathname)) apiCalls.push({ origin: url.origin, method: response.request().method(), path: url.pathname, status: response.status() });
    if ((response.headers()['content-type'] ?? '').includes('json')) { try { bodies.push(await response.text()); } catch { /* 跳转或已取消的响应没有可读的体。 */ } }
    expect(response.request().headers().authorization, url.pathname).toBeUndefined();
  });
  return { refreshes, probes, apiCalls, bodies };
}
const loginPage = new RegExp(`^${identity.replaceAll('.', '\\.')}/login\\?ReturnUrl=`);

test('验收 1 / 3 / 4：真实 OIDC 登录、多租户选择、切换租户、刷新合并与过期对话框、零令牌、运行时配置', async ({ page, baseURL }) => {
  const origin = baseURL!.replace(/\/$/, '');
  const { refreshes, probes, apiCalls, bodies } = observe(page, origin);

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
  // 3. 回到授权请求签码 → 网关回调建 qian.sid → 工作台：会话快照、菜单与权限都来自真实接口。
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
  expect(apiCalls.filter(call => call.path === '/api/capabilities/navigation' && call.status === 200).length).toBeGreaterThan(0);

  // 4. 菜单 → 路由（pushState）与 404 兜底。
  await page.getByRole('link', { name: '客户', exact: true }).click();
  await expect(page).toHaveURL(origin + '/customers');
  await expect(page.getByRole('heading', { name: '客户', exact: true })).toBeVisible();
  await expect(page.getByRole('link', { name: '客户', exact: true })).toHaveAttribute('aria-current', 'page');
  await page.goto('/no-such-page');
  await expect(page.getByRole('heading', { name: '页面不存在', exact: true })).toBeVisible();
  await page.getByRole('button', { name: '返回工作台', exact: true }).click();
  await expect(page).toHaveURL(origin + '/');

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

  // 6. 服务端访问令牌到期：下一批请求由依赖方透明刷新（单飞），页面无感，浏览器不发刷新请求，也没有对话框。
  await devSession(page, 'expire');
  const before = apiCalls.length;
  await page.getByTestId('shell-reload').click();
  await expect.poll(() => apiCalls.slice(before).filter(call => call.path.startsWith('/api/') && call.status === 200).length).toBeGreaterThanOrEqual(3);
  expect(refreshes).toHaveLength(0); expect(probes).toHaveLength(0);
  await expect(page.getByRole('alertdialog')).toHaveCount(0);
  // 6b. 会话仍在时强制刷新（SDK sessionProvider 遇 401 走的就是这条 POST）：依赖方拿 refresh token 向真实身份服务换新令牌，回快照不回令牌；之后请求照常 200。
  const forced = await devPost(page, '/auth/session/refresh');
  expect(forced.status).toBe(200);
  expect(forced.body).toMatchObject({ kind: 'user', tenantId: tenantB, displayName: '演示用户（乙方）' });
  expect(forced.text).not.toMatch(tokenLike);
  const afterForced = apiCalls.length;
  await page.getByTestId('shell-reload').click();
  await expect.poll(() => apiCalls.slice(afterForced).filter(call => call.path.startsWith('/api/') && call.status === 200).length).toBeGreaterThanOrEqual(3);
  // 6c. 会话刷新后重拉（裁决 ④）：经真实身份端口递增当前成员的 identity_revision（授权变更后身份侧就是这样做的）→ 平台表面按 PEP 同一语义拒绝旧修订的令牌
  // （三路 401）→ SDK 合并成一次 POST /auth/session/refresh（先取防伪令牌，探测 200；慢端点的 401 晚于刷新完成才到达时沿用结论直接重放，不再第二次刷新）
  // → 依赖方换到带新修订的令牌 → 重放 200 → 外壳随即再拉一轮菜单 / 权限 / 能力目录并刷新会话快照（身份修订 +1）；全程没有过期对话框。
  const revision = Number(await sessionCard.locator('[data-field=identityRevision]').textContent());
  expect(revision).toBeGreaterThan(0);
  const bumped = await devPost(page, '/__dev/identity/bump');
  expect(bumped.status).toBe(200);
  expect(bumped.body).toEqual({ identityRevision: revision + 1 });
  const afterBump = apiCalls.length;
  await page.getByTestId('shell-reload').click();
  await expect.poll(() => refreshes.length).toBe(1);
  await expect.poll(() => apiCalls.slice(afterBump).filter(call => call.path === '/api/capabilities/navigation' && call.status === 200).length).toBeGreaterThanOrEqual(2);
  await expect.poll(() => apiCalls.slice(afterBump).filter(call => call.path === '/api/authz/me/permissions' && call.status === 200).length).toBeGreaterThanOrEqual(2);
  expect(apiCalls.slice(afterBump).filter(call => call.path.startsWith('/api/') && call.status === 401).length).toBeGreaterThanOrEqual(1);
  expect(probes).toEqual([200]);
  await expect(sessionCard.locator('[data-field=identityRevision]')).toHaveText(String(revision + 1));
  await expect(nav).toHaveText(['客户']);
  await expect(page.getByRole('alertdialog')).toHaveCount(0);
  // 7. refresh token 失效：依赖方刷新失败即结束会话，三路并发请求都 401；SDK 合并成一次刷新尝试——会话已不在，取防伪令牌这一步即 401、不再发 POST——只弹一个过期对话框；对话框不可被 Esc 关掉。
  await devSession(page, 'break');
  const beforeBreak = apiCalls.length;
  await page.getByTestId('shell-reload').click();
  const dialog = page.getByRole('alertdialog', { name: '登录已过期' });
  await expect(dialog).toHaveCount(1);
  await expect.poll(() => apiCalls.slice(beforeBreak).filter(call => call.path.startsWith('/api/') && call.status === 401).length).toBeGreaterThanOrEqual(3);
  await expect.poll(() => probes).toEqual([200, 401]);
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
  await page.getByRole('combobox', { name: 'Language' }).click();
  await page.getByRole('option', { name: 'zh-CN' }).click();
  await expect(page.getByRole('heading', { name: '工作台', exact: true })).toBeVisible();
  await page.getByRole('button', { name: '展开侧栏' }).click();
  await page.getByRole('combobox', { name: '主题' }).click();
  await page.getByRole('option', { name: '明亮' }).click();
  await expect.poll(() => page.evaluate(() => document.documentElement.dataset.theme)).toBe('light');
  expect(await page.evaluate(() => JSON.parse(localStorage.getItem('yue.preferences.qian-shell') ?? '{}') as unknown)).toEqual({ theme: 'light', locale: 'zh-CN', sidebarCollapsed: false });
  // 代码审查项的自动检查：期间所有同源 JSON 响应体都不含令牌；存储、URL、iframe 同样干净。
  for (const body of bodies) expect(body).not.toMatch(tokenLike);
  await noCredentials(page, '全程');

  // 10. 运行时配置：改 ConfigMap 替身里的 apiBase 为 /api-alt → 刷新页面即生效，菜单照常渲染，请求全部走新前缀；构建产物未变。
  const conf = resolve(state, 'app-conf.js'); const original = readFileSync(conf, 'utf8');
  try {
    writeFileSync(conf, 'window.__APP_CONF__ = { "apiBase": "/api-alt", "authBase": "/auth", "brand": "Qian 乾（alt）" };\n');
    const mark = apiCalls.length;
    await page.reload();
    await expect(page.getByTestId('yue-app-shell-sidebar')).toContainText('Qian 乾（alt）');
    await expect(nav).toHaveText(['客户']);
    await expect.poll(() => apiCalls.slice(mark).filter(call => call.path.startsWith('/api-alt/') && call.status === 200).length).toBeGreaterThanOrEqual(3);
    expect(apiCalls.slice(mark).filter(call => call.path.startsWith('/api/'))).toEqual([]);
  } finally { writeFileSync(conf, original); }

  // 11. 退出：BFF 会话结束，再访问受保护接口是 401。
  await page.getByRole('button', { name: '用户菜单' }).click();
  await page.getByRole('menuitem', { name: '退出登录' }).click();
  await expect(page.getByTestId('shell-signed-out')).toBeVisible();
  expect(await page.evaluate(async () => (await fetch('/auth/session', { credentials: 'same-origin' })).status)).toBe(401);
  await noCredentials(page, '退出后');
});

test('平台身份（operator）：只有代管横幅与会话卡片，不拉菜单 / 权限 / 能力目录，不出现过期对话框，零令牌', async ({ page, baseURL }) => {
  const origin = baseURL!.replace(/\/$/, '');
  const { refreshes, apiCalls, bodies } = observe(page, origin);
  await page.goto('/');
  await expect(page).toHaveURL(loginPage);
  await fillLogin(page, operator);
  // 平台身份没有租户成员身份，不经过选择页；网关回调建会话后外壳只读会话快照，不向租户表面发任何请求（平台宿主对无租户主体一律 401，否则就会弹过期对话框）。
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
  // 认证页族在身份服务同源的 /api/identity/* 调用（登录、me）不算：这里只看外壳经网关发往租户表面的请求。
  expect(apiCalls.filter(call => call.origin === origin && /^\/(api|api-alt)\//.test(call.path))).toEqual([]);
  expect(refreshes).toHaveLength(0);
  for (const body of bodies) expect(body).not.toMatch(tokenLike);
  await noCredentials(page, '平台身份');
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
  await page.context().clearCookies();
  await throughDex();
  await expect(page).toHaveURL(origin + '/');
  await expect(sessionCard.locator('[data-field=displayName]')).toHaveText('演示用户');
  await expect(page.getByTestId('identity-connector-bind')).toHaveCount(0);
  await expect(page.getByTestId('yue-app-shell-sidebar').getByRole('link')).toHaveText(['客户', '新建客户']);
  await expect.poll(() => apiCalls.filter(call => call.path === '/api/capabilities/navigation' && call.status === 200).length).toBeGreaterThanOrEqual(2);
  for (const body of bodies) expect(body).not.toMatch(tokenLike);
  await noCredentials(page, 'SSO 再次登录后');
});

test('无效入口不渲染登录表单；跨源与非授权端点的回跳一律拒绝', async ({ page }) => {
  for (const query of ['', '?ReturnUrl=https%3A%2F%2Fevil.example%2F', '?ReturnUrl=%2Fsomewhere%3Fclient_id%3Dqian-shell', '?ReturnUrl=%2Fconnect%2Fauthorize%3Faccess_token%3Dx']) {
    await page.goto(`${identity}/login${query}`);
    await expect(page.getByTestId('identity-invalid-entry')).toBeVisible();
    await expect(page.getByLabel(/密码/)).toHaveCount(0);
  }
});

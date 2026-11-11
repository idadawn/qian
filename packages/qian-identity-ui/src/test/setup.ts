import '../index.css';

// 页面里的同源接口请求（登录页、选择页探测开通组件的 /api/signup/session）不能落到 Vite 开发服务器：
// 它把未知路径回落成本包的 index.html，预转换 main.tsx 时补优化 react-dom/client 并整页重载，
// 同一浏览器里并行的其他项目用例会因请求被中止而偶发 AbortError（S1-YUE-019 验收缺陷 8）。
// 缺省按「本宿主没装开通组件」答 404；需要开通入口的用例用 vi.spyOn(globalThis, 'fetch') 自行替换。
const realFetch = globalThis.fetch.bind(globalThis);
globalThis.fetch = (input, init) => {
  const url = new URL(input instanceof Request ? input.url : String(input), location.href);
  if (url.origin === location.origin && url.pathname.startsWith('/api/')) return Promise.resolve(new Response(null, { status: 404 }));
  return realFetch(input, init);
};

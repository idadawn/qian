import { defineConfig } from 'vite';
import react from '@vitejs/plugin-react';
import tailwindcss from '@tailwindcss/vite';
import { federation } from '@module-federation/vite';

// 远程制品（MF2）：React、yue 与插件 SDK 全部由外壳共享（singleton、import: false），制品里只有两页自己的代码。
// 外壳按 ui/manifest.json 的 federation.manifest（/remotes/qian.sample.crm/mf-manifest.json）按需加载；网关 / 联调栈把 dist/remote 托管在该前缀下。
const shared = Object.fromEntries(['react', 'react-dom', 'react/jsx-runtime', 'react/jsx-dev-runtime', 'react-dom/client', ...['tokens', 'contracts', 'core', 'sdk'].map((name) => `@idadawn/yue-${name}`), '@idadawn/qian-plugin-sdk']
  .map((name) => [name, { singleton: true, requiredVersion: name.startsWith('react') ? '^19.0.0' : '^0.1.0', import: false as const }]));
export default defineConfig({
  plugins: [react(), tailwindcss(), federation({ name: 'qian_sample_crm', filename: 'remoteEntry.js',
    exposes: { './CustomersPage': './src/customers.tsx', './CreateCustomerPage': './src/create-customer.tsx' }, shared, manifest: true, dts: false })],
  // 只产出远程制品（remoteEntry 由插件补进入口），不需要也不交付 index.html。
  build: { outDir: 'dist/remote', target: 'es2022', sourcemap: false, rollupOptions: { input: {} } },
});

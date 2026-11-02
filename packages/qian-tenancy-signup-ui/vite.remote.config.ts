import { defineConfig } from 'vite';
import react from '@vitejs/plugin-react';
import tailwindcss from '@tailwindcss/vite';
import { federation } from '@module-federation/vite';

const shared = Object.fromEntries(['react', 'react-dom', 'react/jsx-runtime', 'react/jsx-dev-runtime', 'react-dom/client', ...['tokens', 'contracts', 'core', 'sdk', 'form', 'data'].map((name) => `@idadawn/yue-${name}`), '@idadawn/qian-plugin-sdk'].map((name) => [name, { singleton: true, requiredVersion: name.startsWith('react') ? '^19.0.0' : '^0.1.0', import: false }]));
/** 运营页的远程制品：只含开通申请页，不含申请人公开页，也不内置鉴权身份。 */
export default defineConfig({
  plugins: [react(), tailwindcss(), federation({ name: 'qian_tenancy_signup', filename: 'remoteEntry.js', exposes: { './ApplicationsPage': './src/remote.tsx' }, shared, manifest: true, dts: { generateTypes: { abortOnError: true, compileInChildProcess: false }, consumeTypes: false } })],
  build: { outDir: 'dist/remote', target: 'es2022', sourcemap: false },
});

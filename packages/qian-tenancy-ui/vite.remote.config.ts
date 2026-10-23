import { defineConfig } from 'vite';
import react from '@vitejs/plugin-react';
import tailwindcss from '@tailwindcss/vite';
import { federation } from '@module-federation/vite';

const shared = Object.fromEntries(['react', 'react-dom', 'react/jsx-runtime', 'react/jsx-dev-runtime', 'react-dom/client', ...['tokens', 'contracts', 'core', 'sdk', 'form', 'data'].map((name) => `@idadawn/yue-${name}`), '@idadawn/qian-plugin-sdk'].map((name) => [name, { singleton: true, requiredVersion: name.startsWith('react') ? '^19.0.0' : '^0.1.0', import: false }]));
/** 远程制品不包含本机开发代理，也不内置鉴权身份。 */
export default defineConfig({
  plugins: [react(), tailwindcss(), federation({ name: 'qian_tenancy', filename: 'remoteEntry.js', exposes: { './TenantPage': './src/remote.tsx' }, shared, manifest: true, dts: { generateTypes: { abortOnError: true, compileInChildProcess: false }, consumeTypes: false } })],
  build: { outDir: 'dist/remote', target: 'es2022', sourcemap: false },
});

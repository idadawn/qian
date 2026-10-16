import react from '@vitejs/plugin-react';
import tailwindcss from '@tailwindcss/vite';
import { playwright } from '@vitest/browser-playwright';
import { defineConfig } from 'vitest/config';
export default defineConfig({ plugins: [react(), tailwindcss()], test: { name: 'form-browser', setupFiles: ['./src/test/setup.ts'], include: ['src/**/*.browser.test.tsx'], browser: { enabled: true, headless: true, provider: playwright(), instances: [{ browser: 'chromium' }], screenshotFailures: false } } });

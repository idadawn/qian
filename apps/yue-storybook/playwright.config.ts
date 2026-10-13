import { defineConfig, devices } from '@playwright/test';
export default defineConfig({
  testDir: './e2e',
  use: { baseURL: 'http://127.0.0.1:6007', trace: 'retain-on-failure' },
  projects: [{ name: 'chromium', use: { ...devices['Desktop Chrome'] } }],
  webServer: { command: 'pnpm exec vite preview --outDir dist --host 127.0.0.1 --port 6007 --strictPort', url: 'http://127.0.0.1:6007', reuseExistingServer: false },
});

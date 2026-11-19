import { defineConfig, devices } from '@playwright/test';

// Runs against a stack started with `aspire run`; there is no webServer block on purpose (D-99).
const baseURL = process.env['E2E_BASE_URL'];
if (!baseURL) {
  throw new Error('Start the stack with aspire run and set E2E_BASE_URL to the web endpoint');
}

export default defineConfig({
  testDir: './e2e',
  testMatch: '**/*.e2e.ts',
  retries: 0,
  reporter: 'list',
  use: {
    baseURL,
    trace: 'on-first-retry',
  },
  projects: [{ name: 'chromium', use: { ...devices['Desktop Chrome'] } }],
});

import { defineConfig, devices } from '@playwright/test';

// Runs against a stack started with `aspire run`; there is no webServer block on purpose (D-99).
const baseURL = process.env['E2E_BASE_URL'];
if (!baseURL) {
  throw new Error('Start the stack with aspire run and set E2E_BASE_URL to the web endpoint');
}

export default defineConfig({
  testDir: './e2e',
  testMatch: '**/*.e2e.ts',
  // README screenshots run only on request: $env:E2E_SCREENSHOTS = '1'; pnpm e2e --grep '@screenshots' (D-211).
  grepInvert: process.env['E2E_SCREENSHOTS'] ? undefined : /@screenshots/,
  retries: 0,
  reporter: 'list',
  use: {
    baseURL,
    trace: 'on-first-retry',
  },
  projects: [{ name: 'chromium', use: { ...devices['Desktop Chrome'] } }],
});

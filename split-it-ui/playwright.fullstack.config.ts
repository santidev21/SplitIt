import { defineConfig, devices } from '@playwright/test';

// Full-stack integration tests against a REAL deployed stack (Nginx :80/:443, backend, SQL Server).
// Run manually after deploying: cd split-it-ui && npm run e2e:fullstack
export default defineConfig({
  testDir: './e2e/fullstack',
  retries: process.env.CI ? 2 : 0,
  workers: 1,
  reporter: [['html', { open: 'never' }], ['list']],
  timeout: 30_000,
  use: {
    baseURL: 'http://localhost',
    ignoreHTTPSErrors: true,
    trace: 'on-first-retry',
  },
  projects: [{ name: 'chromium', use: { ...devices['Desktop Chrome'] } }],
});

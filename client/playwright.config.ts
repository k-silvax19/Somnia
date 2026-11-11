import { defineConfig, devices } from '@playwright/test';

export default defineConfig({
  testDir: './e2e',
  testMatch: process.env['SOMNIA_REAL_API'] ? 'auth.real.spec.ts' : 'auth.spec.ts',
  fullyParallel: false,
  workers: 1,
  retries: 0,
  reporter: 'list',
  use: {
    baseURL: 'https://localhost:4300',
    ignoreHTTPSErrors: true, // Development certificates only.
    screenshot: 'only-on-failure',
    trace: 'off', // Auth responses contain credentials; never record traces/HAR.
    ...devices['Desktop Chrome'],
    channel: process.env['PLAYWRIGHT_CHANNEL'],
  },
  webServer: {
    command: 'npm start -- --configuration e2e --port 4300',
    url: 'https://localhost:4300',
    ignoreHTTPSErrors: true,
    reuseExistingServer: false,
    timeout: 60_000,
  },
});

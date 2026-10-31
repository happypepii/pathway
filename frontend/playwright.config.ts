import { defineConfig } from '@playwright/test'

export default defineConfig({
  testDir: './e2e',
  testMatch: process.env.PATHWAY_E2E_LIVE === '1'
    ? ['live-journey.spec.ts', 'path.spec.ts'] : ['journey.spec.ts', 'path.spec.ts'],
  fullyParallel: false,
  workers: 1,
  timeout: 45_000,
  retries: 0,
  use: {
    baseURL: process.env.PATHWAY_E2E_BASE_URL ?? 'https://localhost:5173',
    ignoreHTTPSErrors: true, // Test-only temporary certificate; dev/prod TLS verification remains enabled.
    browserName: 'chromium',
    trace: 'off', // Avoid recording private planning answers and cookies.
    screenshot: 'off',
    video: 'off',
  },
})

import { defineConfig } from '@playwright/test'

export default defineConfig({
  testDir: './e2e',
  fullyParallel: false,
  workers: 1,
  timeout: 45_000,
  retries: 0,
  use: {
    baseURL: 'https://localhost:5173',
    ignoreHTTPSErrors: true, // Test-only temporary certificate; dev/prod TLS verification remains enabled.
    browserName: 'chromium',
    trace: 'off', // Avoid recording private planning answers and cookies.
    screenshot: 'off',
    video: 'off',
  },
})

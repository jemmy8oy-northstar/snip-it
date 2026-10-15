import { defineConfig, devices } from '@playwright/test';

/**
 * Playwright e2e config for the SnipIt frontend.
 *
 * These tests drive the app off mocked API responses (see e2e/mocks.ts), so
 * they need no backend and render deterministically. Each spec also captures
 * full-page screenshots into e2e/screenshots/ for visual review of the editor.
 *
 * Run:  npm run test:e2e            (headless, starts the dev server for you)
 *       npm run test:e2e -- --ui    (interactive)
 *
 * e2e/screenshots/ is GITIGNORED and asserts nothing — page.screenshot() is a
 * plain write, not a comparison, so these files never fail a build. They were
 * committed until #15; James asked for them out ("it wastes git storage") and
 * nothing read them.
 *
 * ⚠️ This comment used to claim CI uploaded e2e/screenshots/ as a per-PR
 * artifact. It never did — ci.yml uploads playwright-report/ and only
 * `if: failure()`. Check the workflow before repeating that claim.
 */
export default defineConfig({
  testDir: './e2e',
  fullyParallel: true,
  forbidOnly: !!process.env.CI,
  retries: process.env.CI ? 1 : 0,
  reporter: process.env.CI ? [['html', { open: 'never' }], ['list']] : 'list',
  use: {
    // Must carry Vite's `base` (/snipit/) — without the trailing segment every
    // navigation lands on Vite's "configured with a public base URL" hint page
    // instead of the app. Specs therefore navigate with RELATIVE paths ('./',
    // './editor'); a leading slash would discard the base again.
    baseURL: 'http://localhost:4173/snipit/',
    trace: 'on-first-retry',
  },
  projects: [
    { name: 'chromium', use: { ...devices['Desktop Chrome'] } },
  ],
  webServer: {
    command: 'npm run dev -- --port 4173 --strictPort',
    url: 'http://localhost:4173/snipit/',
    reuseExistingServer: !process.env.CI,
    timeout: 120_000,
  },
});

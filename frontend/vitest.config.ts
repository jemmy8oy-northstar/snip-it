import { defineConfig } from 'vitest/config';
import react from '@vitejs/plugin-react';

export default defineConfig({
  plugins: [react()],
  // Must match vite.config.ts. This config does not extend it, so without this
  // `import.meta.env.BASE_URL` is '/' under test and '/snipit/' when built — and the API base
  // path derived from it (src/api/apiBase.ts) would be tested as something the app never uses.
  base: '/snipit/',
  test: {
    environment: 'jsdom',
    setupFiles: ['./src/test/setup.ts'],
    // e2e/ holds Playwright specs — vitest picking them up makes `npm run test`
    // fail with "did not expect test.beforeEach() to be called here".
    // They run via `npm run test:e2e`.
    exclude: ['node_modules/**', 'dist/**', 'e2e/**'],
  },
});

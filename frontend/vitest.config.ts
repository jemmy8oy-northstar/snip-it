import { defineConfig } from 'vitest/config';
import react from '@vitejs/plugin-react';

export default defineConfig({
  plugins: [react()],
  test: {
    environment: 'jsdom',
    setupFiles: ['./src/test/setup.ts'],
    // e2e/ holds Playwright specs — vitest picking them up makes `npm run test`
    // fail with "did not expect test.beforeEach() to be called here".
    // They run via `npm run test:e2e`.
    exclude: ['node_modules/**', 'dist/**', 'e2e/**'],
  },
});

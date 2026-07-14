import '@testing-library/jest-dom/vitest';
import { afterEach } from 'vitest';
import { cleanup } from '@testing-library/react';

// vitest runs without `globals: true`, so @testing-library/react never sees a
// global afterEach to auto-register its cleanup. Without this, each render()
// stacks up in the shared jsdom document and queries throw "multiple elements
// found" from the second test in a file onward. Unmount after every test.
afterEach(() => {
  cleanup();
});

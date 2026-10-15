import { expect, test } from '@playwright/test';

/**
 * The favicon has to be *served*, not merely declared.
 *
 * index.html names the icon by path and Vite rewrites that href against `base`
 * at build time. If the file named there is missing from public/, nothing warns
 * you: the build succeeds, the page renders, and the icon silently 404s. That
 * shipped in every app generated from this template until 2026-08-16
 * (web-template#91) — `href="/logo.svg"` against a public/ that only ever held
 * balenthiran.svg.
 *
 * So this asserts the served response rather than the file's existence: it reads
 * whatever href the app actually publishes and fetches it the way a browser
 * would, which also covers getting `base` wrong.
 */
test('the declared favicon is served, not just declared', async ({ page, request }) => {
  await page.goto('./');

  const href = await page.locator('link[rel="icon"]').first().getAttribute('href');

  // Assert the premise before the conclusion: no icon link (or an empty href)
  // must fail here rather than silently skipping the fetch below.
  expect(href, 'index.html declares no <link rel="icon"> href').toBeTruthy();

  const resolved = new URL(href!, page.url()).toString();
  const response = await request.get(resolved);

  expect(
    response.status(),
    `favicon ${resolved} is declared but not served — is it in frontend/public/?`,
  ).toBe(200);

  // Status alone is NOT enough, and asserting only that is how the first version
  // of this test passed against the very bug it was written for: Vite's dev
  // server answers an unknown path with the SPA fallback, so a MISSING icon
  // still returns 200 — with an HTML body a browser cannot use as an icon.
  // The content type is what distinguishes "served" from "swallowed".
  expect(
    response.headers()['content-type'] ?? '',
    `favicon ${resolved} returned 200 but is not an image — the dev server fell back to index.html, which means the file is missing from frontend/public/`,
  ).toMatch(/^image\//);
});

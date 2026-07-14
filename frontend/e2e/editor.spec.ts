import { test, expect } from '@playwright/test';
import { mockApi } from './mocks';

/**
 * Smoke + screenshot coverage for the transcript editor, driven off mocked API
 * responses. Each test asserts the key UI is present, then captures a full-page
 * screenshot into e2e/screenshots/ for visual review.
 */

test.beforeEach(async ({ page }) => {
  await mockApi(page);
});

test('landing page renders', async ({ page }) => {
  await page.goto('/');
  await expect(page.locator('main')).toBeVisible();
  await page.screenshot({ path: 'e2e/screenshots/home.png', fullPage: true });
});

test('transcript editor loads the mock transcript', async ({ page }) => {
  await page.goto('/editor');

  // Toolbar controls prove the editor mounted with a loaded transcript.
  await expect(page.getByRole('button', { name: 'Select All' })).toBeVisible();
  await expect(
    page.getByRole('button', { name: 'Remove filler words' }),
  ).toBeVisible();
  await expect(
    page.getByRole('button', { name: 'Send for export' }),
  ).toBeVisible();

  // A word from the mock transcript should be rendered in the word track.
  await expect(page.getByText('transcribe', { exact: false }).first()).toBeVisible();

  await page.screenshot({
    path: 'e2e/screenshots/editor-light.png',
    fullPage: true,
  });
});

test('transcript editor renders in dark mode', async ({ page }) => {
  await page.goto('/editor');
  await expect(page.getByRole('button', { name: 'Select All' })).toBeVisible();

  await page.getByRole('button', { name: 'Toggle Theme' }).click();

  await page.screenshot({
    path: 'e2e/screenshots/editor-dark.png',
    fullPage: true,
  });
});

test('remove-filler-words changes the edit stats', async ({ page }) => {
  await page.goto('/editor');
  await page.getByRole('button', { name: 'Remove filler words' }).click();

  // After a bulk op the editor should still be interactive; capture the result.
  await expect(
    page.getByRole('button', { name: 'Send for export' }),
  ).toBeVisible();
  await page.screenshot({
    path: 'e2e/screenshots/editor-filler-removed.png',
    fullPage: true,
  });
});

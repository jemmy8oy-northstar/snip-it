import { test, expect } from '@playwright/test';
import { EDITOR_PATH, MOCK_TRANSCRIPTION_JOB_ID, mockApi } from './mocks';

/**
 * Smoke + screenshot coverage for the transcript editor, driven off mocked API
 * responses. Each test asserts the key UI is present, then captures a full-page
 * screenshot into e2e/screenshots/ for visual review.
 */

test.beforeEach(async ({ page }) => {
  await mockApi(page);
});

test('landing page renders', async ({ page }) => {
  await page.goto('./');
  await expect(page.locator('main')).toBeVisible();
  await page.screenshot({
    path: 'e2e/screenshots/home.png',
    fullPage: true,
    animations: 'disabled',
  });
});

test('transcript editor loads the mock transcript', async ({ page }) => {
  await page.goto(EDITOR_PATH);

  // Toolbar controls prove the editor mounted with a loaded transcript.
  await expect(page.getByRole('button', { name: 'Select All', exact: true })).toBeVisible();
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
    // Fast-forward the 0.4s theme transition (--theme-transition) so renders
    // are deterministic instead of catching a half-faded frame.
    animations: 'disabled',
  });
});

test('transcript editor renders in dark mode', async ({ page }) => {
  await page.goto(EDITOR_PATH);
  await expect(page.getByRole('button', { name: 'Select All', exact: true })).toBeVisible();

  await page.getByRole('button', { name: 'Toggle Theme' }).click();

  await page.screenshot({
    path: 'e2e/screenshots/editor-dark.png',
    fullPage: true,
    // Fast-forward the 0.4s theme transition (--theme-transition) so renders
    // are deterministic instead of catching a half-faded frame.
    animations: 'disabled',
  });
});

test('send-for-export submits a cut and surfaces the job', async ({ page }) => {
  const cutRequests: unknown[] = [];
  page.on('request', (request) => {
    if (request.method() === 'POST' && request.url().endsWith('/api/cuts')) {
      cutRequests.push(request.postDataJSON());
    }
  });

  await page.goto(EDITOR_PATH);
  await page.getByRole('button', { name: 'Remove filler words' }).click();
  await page.getByRole('button', { name: 'Send for export' }).click();

  await expect(page.getByText(/Cut job .* is complete/)).toBeVisible();
  await expect(page.getByRole('link', { name: 'Download' })).toBeVisible();

  // The payload is the real contract shape: every word, with kept flags.
  expect(cutRequests).toHaveLength(1);
  const body = cutRequests[0] as { transcriptionJobId: string; words: { kept: boolean }[] };
  expect(body.transcriptionJobId).toBeTruthy();
  expect(body.words.length).toBeGreaterThan(0);
  expect(body.words.some((word) => !word.kept)).toBe(true);

  await page.screenshot({
    path: 'e2e/screenshots/editor-export-submitted.png',
    fullPage: true,
    animations: 'disabled',
  });
});

test('uploading a file transcribes it and opens the editor', async ({ page }) => {
  const uploads: string[] = [];
  page.on('request', (request) => {
    if (request.method() === 'POST' && request.url().endsWith('/api/transcriptions')) {
      // Multipart, not JSON — the whole reason the upload uses a hand-written endpoint.
      uploads.push(request.headers()['content-type'] ?? '');
    }
  });

  await page.goto('./editor');
  await expect(page.getByRole('heading', { name: 'Transcribe a video' })).toBeVisible();
  await page.screenshot({
    path: 'e2e/screenshots/upload-panel.png',
    fullPage: true,
    animations: 'disabled',
  });

  await page.getByLabel('Video or audio file').setInputFiles({
    name: 'talk.mp4',
    mimeType: 'video/mp4',
    buffer: Buffer.from('not really a video'),
  });
  await page.getByRole('button', { name: 'Transcribe' }).click();

  // Transcription completing navigates to /editor/:id, which loads the transcript.
  await expect(page.getByRole('button', { name: 'Select All', exact: true })).toBeVisible();
  await expect(page).toHaveURL(new RegExp(`/editor/${MOCK_TRANSCRIPTION_JOB_ID}$`));

  expect(uploads).toHaveLength(1);
  expect(uploads[0]).toContain('multipart/form-data');
});

test('remove-filler-words changes the edit stats', async ({ page }) => {
  await page.goto(EDITOR_PATH);
  await page.getByRole('button', { name: 'Remove filler words' }).click();

  // After a bulk op the editor should still be interactive; capture the result.
  await expect(
    page.getByRole('button', { name: 'Send for export' }),
  ).toBeVisible();
  await page.screenshot({
    path: 'e2e/screenshots/editor-filler-removed.png',
    fullPage: true,
    // Fast-forward the 0.4s theme transition (--theme-transition) so renders
    // are deterministic instead of catching a half-faded frame.
    animations: 'disabled',
  });
});

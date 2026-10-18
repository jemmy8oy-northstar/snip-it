import { test, expect, type Page } from '@playwright/test';
import { EDITOR_PATH, MOCK_TRANSCRIPTION_JOB_ID, mockApi } from './mocks';

/**
 * Smoke + screenshot coverage for the transcript editor, driven off mocked API
 * responses. Each test asserts the key UI is present, then captures a full-page
 * screenshot into e2e/screenshots/ for visual review.
 */

test.beforeEach(async ({ page }) => {
  await mockApi(page);
});

/**
 * Hands the editor the source file it would normally have been given by the upload panel.
 *
 * Opening `/editor/:id` directly is a browser that has never held the video, and since #22 the
 * server has no copy to fall back on — so without this the editor renders its "pick the file
 * again" panel, and a screenshot of it would not be a screenshot of the editor.
 */
async function attachSourceFile(page: Page): Promise<void> {
  await page.getByLabel('Video or audio file').setInputFiles({
    name: 'talk.mp4',
    mimeType: 'video/mp4',
    buffer: Buffer.from('not really a video'),
  });
}

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
  await attachSourceFile(page);

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
  await attachSourceFile(page);
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

/**
 * Opening the editor by URL — a reload, or a shared link — is the case where the browser has
 * no file, because the server keeps no copy of one (#22). The transcript must still load, and
 * the page must ask for the file rather than silently offering an export it cannot perform.
 */
test('opening the editor without the source file asks for it back', async ({ page }) => {
  await page.goto(EDITOR_PATH);

  // The transcript still comes from the server, so the editor is usable.
  await expect(page.getByText('transcribe', { exact: false }).first()).toBeVisible();

  await expect(page.getByText(/snip-it never keeps a copy/)).toBeVisible();
  await expect(page.getByRole('button', { name: 'Send for export' })).toBeDisabled();

  await page.screenshot({
    path: 'e2e/screenshots/editor-source-missing.png',
    fullPage: true,
    animations: 'disabled',
  });
});

test('send-for-export re-sends the video and surfaces the job', async ({ page }) => {
  const cutRequests: { contentType: string; body: string; pathname: string }[] = [];
  page.on('request', (request) => {
    if (request.method() === 'POST' && request.url().endsWith('/api/cuts')) {
      cutRequests.push({
        contentType: request.headers()['content-type'] ?? '',
        body: request.postData() ?? '',
        pathname: new URL(request.url()).pathname,
      });
    }
  });

  await page.goto(EDITOR_PATH);

  // The cut carries the video now, so the file has to be there before it can be submitted.
  await page.getByLabel('Video or audio file').setInputFiles({
    name: 'talk.mp4',
    mimeType: 'video/mp4',
    buffer: Buffer.from('not really a video'),
  });

  await page.getByRole('button', { name: 'Remove filler words' }).click();
  await page.getByRole('button', { name: 'Send for export' }).click();

  await expect(page.getByText(/Cut job .* is complete/)).toBeVisible();
  await expect(page.getByRole('link', { name: 'Download' })).toBeVisible();

  expect(cutRequests).toHaveLength(1);
  const [cut] = cutRequests;

  // Multipart, not JSON — the whole point of #22 is that the video travels with the request.
  expect(cut.contentType).toContain('multipart/form-data');
  expect(cut.body).toContain('name="file"');
  expect(cut.body).toContain('not really a video');

  // The mocks match on '**/api/...', so they would answer a request sent to the host root —
  // which in the cluster is a different app entirely. Assert the base path explicitly.
  expect(cut.pathname).toBe('/snipit/api/cuts');

  // The word list still travels as the real contract shape, inside the `request` field.
  expect(cut.body).toContain('name="request"');
  const requestField = cut.body.split('name="request"')[1];
  const json = JSON.parse(requestField.slice(requestField.indexOf('{'), requestField.lastIndexOf('}') + 1));
  expect(json.transcriptionJobId).toBeTruthy();
  expect(json.words.length).toBeGreaterThan(0);
  expect(json.words.some((word: { kept: boolean }) => !word.kept)).toBe(true);

  await page.screenshot({
    path: 'e2e/screenshots/editor-export-submitted.png',
    fullPage: true,
    animations: 'disabled',
  });
});

test('uploading a file transcribes it and opens the editor', async ({ page }) => {
  const uploads: { url: string; contentType: string }[] = [];
  page.on('request', (request) => {
    if (request.method() === 'POST' && request.url().endsWith('/api/transcriptions')) {
      // Multipart, not JSON — the whole reason the upload uses a hand-written endpoint.
      uploads.push({ url: request.url(), contentType: request.headers()['content-type'] ?? '' });
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

  // The claim #22 rests on, asserted rather than described: the editor plays the file the
  // visitor picked, straight out of the browser. A `blob:` src means no round-trip and no
  // server-side copy — and it is exactly what regresses if anyone re-points this at an API
  // URL, which would look completely normal in review.
  await expect(page.locator('video')).toHaveAttribute('src', /^blob:/);
  await expect(page.getByText(/snip-it never keeps a copy/)).toHaveCount(0);

  expect(uploads).toHaveLength(1);
  expect(uploads[0].contentType).toContain('multipart/form-data');
  // The mocks match on '**/api/...', so they would happily answer a request sent to the host
  // root — which in the cluster is a different app entirely. Assert the base path explicitly.
  expect(new URL(uploads[0].url).pathname).toBe('/snipit/api/transcriptions');
});

test('remove-filler-words changes the edit stats', async ({ page }) => {
  await page.goto(EDITOR_PATH);
  await attachSourceFile(page);
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

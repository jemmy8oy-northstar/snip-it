import type { Page } from '@playwright/test';
import {
  MOCK_TRANSCRIPTION_JOB_ID,
  mockApiTranscript,
} from '../src/features/transcript-editor/fixtures/mockTranscript';

/** Mirrors the backend's JobStatus ordinals — see src/features/transcript-editor/api/jobStatus.ts. */
const JOB_STATUS_COMPLETED = 2;

const MOCK_CUT_JOB_ID = '22222222-2222-2222-2222-222222222222';

const completedCutJob = {
  id: MOCK_CUT_JOB_ID,
  status: JOB_STATUS_COMPLETED,
  createdAt: '2026-01-01T00:00:00Z',
  downloadUrl: `/api/cuts/${MOCK_CUT_JOB_ID}/download`,
};

/**
 * Deterministic API mocks for the e2e suite.
 *
 * The editor now makes real requests, so these stub the real endpoints: the transcript
 * read, the cut submit/poll pair, and the source video. The transcript payload is the same
 * fixture the unit tests use, in the shape the backend actually returns
 * (`mockApiTranscript`), so a contract change breaks unit and e2e together rather than
 * letting one drift.
 */
export async function mockApi(page: Page): Promise<void> {
  // The landing page polls the template's health endpoint; without a stub it
  // renders the red "Could not reach the API" state in every screenshot.
  await page.route('**/api/status', (route) =>
    route.fulfill({ json: { status: 'Healthy', service: 'snip-it', version: '0.1.0' } }),
  );

  await page.route('**/api/transcriptions/*/transcript', (route) =>
    route.fulfill({ json: mockApiTranscript }),
  );

  // There is no real media in the repo: answer the <video> src with an empty 200 so the
  // element mounts and the page renders, rather than hanging on a request nothing serves.
  await page.route('**/api/transcriptions/*/source', (route) =>
    route.fulfill({ status: 200, contentType: 'video/mp4', body: '' }),
  );

  await page.route('**/api/cuts', (route) => route.fulfill({ json: completedCutJob }));
  await page.route(`**/api/cuts/${MOCK_CUT_JOB_ID}`, (route) =>
    route.fulfill({ json: completedCutJob }),
  );
}

/** The editor route these mocks are wired for. */
export const EDITOR_PATH = `./editor/${MOCK_TRANSCRIPTION_JOB_ID}`;

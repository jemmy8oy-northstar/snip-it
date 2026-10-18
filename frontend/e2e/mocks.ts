import type { Page } from '@playwright/test';
import {
  MOCK_TRANSCRIPTION_JOB_ID,
  mockApiTranscript,
} from '../src/features/transcript-editor/fixtures/mockTranscript';

const MOCK_CUT_JOB_ID = '22222222-2222-2222-2222-222222222222';

// `error` is null rather than absent because the backend declares it required-but-nullable —
// the key is always on the wire (pinned by ApiJsonContractTests).
const completedTranscriptionJob = {
  id: MOCK_TRANSCRIPTION_JOB_ID,
  status: 'Completed',
  error: null,
  createdAt: '2026-01-01T00:00:00Z',
};

const completedCutJob = {
  id: MOCK_CUT_JOB_ID,
  status: 'Completed',
  error: null,
  createdAt: '2026-01-01T00:00:00Z',
  downloadUrl: `/api/cuts/${MOCK_CUT_JOB_ID}/download`,
};

/**
 * Deterministic API mocks for the e2e suite.
 *
 * The editor now makes real requests, so these stub the real endpoints: the transcript
 * read and the cut submit/poll pair. The transcript payload is the same fixture the unit
 * tests use, in the shape the backend actually returns (`mockApiTranscript`), so a contract
 * change breaks unit and e2e together rather than letting one drift.
 *
 * There is deliberately no stub for the source video any more (#22): the route is gone, and
 * the editor plays the file out of the browser via an object URL. If a stub for it were left
 * here, a regression that re-introduced the server round-trip would still pass this suite.
 */
export async function mockApi(page: Page): Promise<void> {
  // The landing page polls the template's health endpoint; without a stub it
  // renders the red "Could not reach the API" state in every screenshot.
  await page.route('**/api/status', (route) =>
    route.fulfill({ json: { status: 'Healthy', service: 'snip-it', version: '0.1.0' } }),
  );

  // The upload (multipart POST) and the job poll. Both answer Completed straight away so the
  // panel navigates without the suite having to wait out a polling interval.
  await page.route('**/api/transcriptions', (route) =>
    route.fulfill({ json: completedTranscriptionJob }),
  );
  await page.route('**/api/transcriptions/*', (route) =>
    route.fulfill({ json: completedTranscriptionJob }),
  );

  await page.route('**/api/transcriptions/*/transcript', (route) =>
    route.fulfill({ json: mockApiTranscript }),
  );

  await page.route('**/api/cuts', (route) => route.fulfill({ json: completedCutJob }));
  await page.route(`**/api/cuts/${MOCK_CUT_JOB_ID}`, (route) =>
    route.fulfill({ json: completedCutJob }),
  );
}

/** The editor route these mocks are wired for. */
export const EDITOR_PATH = `./editor/${MOCK_TRANSCRIPTION_JOB_ID}`;

export { MOCK_TRANSCRIPTION_JOB_ID };

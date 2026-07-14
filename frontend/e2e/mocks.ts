import type { Page } from '@playwright/test';
import { mockTranscript } from '../src/features/transcript-editor/fixtures/mockTranscript';

/**
 * Deterministic API mocks for the e2e suite.
 *
 * The editor currently self-mocks its data via an RTK Query `queryFn`
 * (src/features/transcript-editor/api/transcriptApi.ts), so no HTTP request is
 * made yet. The route stubs below are wired for the *real* backend endpoints
 * (GET /api/transcripts/{id}, POST /api/cuts) so that when transcriptApi.ts is
 * swapped to real `query:` calls, these tests keep passing unchanged with the
 * same fixture data — i.e. the mocks are already in place for the API wiring.
 */
export async function mockApi(page: Page): Promise<void> {
  await page.route('**/api/transcripts/**', (route) =>
    route.fulfill({ json: mockTranscript }),
  );
  await page.route('**/api/cuts', (route) =>
    route.fulfill({ json: { jobId: 'mock-job-id' } }),
  );
}

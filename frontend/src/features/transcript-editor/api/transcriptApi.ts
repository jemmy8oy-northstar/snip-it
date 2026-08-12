import { emptySplitApi as api } from '../../../api/emptyApi';
import type { CutRequestDto, TranscriptDto } from '../types';
import { mockTranscript } from '../fixtures/mockTranscript';

// Handwritten stand-in for the backend agent's transcription endpoint.
// Per docs/specs/openapi-codegen.md, real generated endpoints live in
// generatedApi.ts and must never be hand-edited — this file follows the
// "custom endpoint injected separately" pattern instead. The next PR in this
// stack swaps the editor onto the generated hooks and deletes this file.
//
// The endpoint is named `getMockTranscript`, NOT `getTranscript`, and the name
// matters: both files inject into the same `emptySplitApi`, so an endpoint key
// used twice is a collision, and `overrideExisting: false` resolves it by
// silently keeping whichever registered first. Now that this PR's regenerated
// client legitimately owns `getTranscript` (GET /api/transcriptions/{id}/transcript),
// sharing the key handed the editor the real endpoint while the call site still
// passed `{ transcriptId }` — so it requested /api/transcriptions/undefined/transcript
// against a backend the e2e job doesn't run, and the editor never mounted.
// The exported hook name is unchanged, so no call site moves.
export const transcriptApi = api.injectEndpoints({
  endpoints: (build) => ({
    getMockTranscript: build.query<TranscriptDto, { transcriptId: string }>({
      queryFn: async () => {
        return { data: mockTranscript };
      },
    }),
    submitCutRequest: build.mutation<{ jobId: string }, CutRequestDto>({
      queryFn: async (request) => {
        console.info('[snipit] would submit cut request', request);
        return { data: { jobId: 'mock-job-id' } };
      },
    }),
  }),
  overrideExisting: false,
});

export const {
  useGetMockTranscriptQuery: useGetTranscriptQuery,
  useSubmitCutRequestMutation,
} = transcriptApi;

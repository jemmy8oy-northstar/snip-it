import { emptySplitApi as api } from '../../../api/emptyApi';
import type { CutRequestDto, TranscriptDto } from '../types';
import { mockTranscript } from '../fixtures/mockTranscript';

// Handwritten stand-in for the backend agent's transcription endpoint.
// Per docs/specs/openapi-codegen.md, real generated endpoints live in
// generatedApi.ts and must never be hand-edited — this file follows the
// "custom endpoint injected separately" pattern instead. Once the backend
// exposes GET /api/transcripts/{id} and POST /api/cuts, swap the two
// queryFns below for `query: () => ({ url: ... })` (or drop this file
// entirely in favour of the codegen'd hooks) — call sites don't change,
// since useGetTranscriptQuery / useSubmitCutRequestMutation keep the same
// shape either way.
export const transcriptApi = api.injectEndpoints({
  endpoints: (build) => ({
    getTranscript: build.query<TranscriptDto, { transcriptId: string }>({
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

export const { useGetTranscriptQuery, useSubmitCutRequestMutation } = transcriptApi;

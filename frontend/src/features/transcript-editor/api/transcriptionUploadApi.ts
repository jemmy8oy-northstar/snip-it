import { emptySplitApi as api } from '../../../api/emptyApi';
import type { TranscriptionJob } from '../../../api/generatedApi';

/**
 * The upload can't use the generated `submitTranscription` mutation.
 *
 * `POST /api/transcriptions` binds an `IFormFile`, i.e. it needs a multipart body, but the
 * generated hook hands `{ file }` to `fetchBaseQuery` as a plain object, which JSON-serialises
 * it. `fetchBaseQuery` only passes a body through untouched (and leaves `Content-Type` for the
 * browser to set with the boundary) when it is already a `FormData`.
 *
 * So this is the "custom endpoint injected separately" case from
 * docs/specs/openapi-codegen.md rather than a hand-edit of the generated file. The field name
 * must stay `file` to match the route's parameter name.
 */
export const transcriptionUploadApi = api.injectEndpoints({
  endpoints: (build) => ({
    uploadTranscription: build.mutation<TranscriptionJob, File>({
      query: (file) => {
        const body = new FormData();
        body.append('file', file);
        return { url: '/api/transcriptions', method: 'POST', body };
      },
    }),
  }),
  overrideExisting: false,
});

export const { useUploadTranscriptionMutation } = transcriptionUploadApi;

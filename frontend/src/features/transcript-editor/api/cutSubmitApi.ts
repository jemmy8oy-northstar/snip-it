import { emptySplitApi as api } from '../../../api/emptyApi';
import type { CutJobResponse } from '../../../api/generatedApi';
import type { CutRequestPayload } from './cutRequest';

/**
 * The cut can't use the generated `submitCut` mutation, for the same reason the upload can't
 * (see `transcriptionUploadApi.ts`): `POST /api/cuts` now takes a multipart body.
 *
 * It takes one because snip-it keeps no copy of the source between requests (#22) — by the time
 * a cut is submitted the transcribed file has been deleted, so the browser sends the video again
 * alongside the word list. `fetchBaseQuery` passes a `FormData` through untouched and lets the
 * browser set the boundary; anything else would be JSON-serialised.
 *
 * The field names must match the route's parameters: `file` and `request`.
 */
export const cutSubmitApi = api.injectEndpoints({
  endpoints: (build) => ({
    submitCutWithSource: build.mutation<CutJobResponse, { file: File; request: CutRequestPayload }>({
      query: ({ file, request }) => {
        const body = new FormData();
        body.append('file', file);
        // A form field is a string, so the word list travels as JSON rather than as
        // `words[0].start`-style keys. The backend deserialises it with web defaults, i.e.
        // camelCase — the same casing JSON.stringify produces here.
        body.append('request', JSON.stringify(request));
        return { url: '/api/cuts', method: 'POST', body };
      },
    }),
  }),
  overrideExisting: false,
});

export const { useSubmitCutWithSourceMutation } = cutSubmitApi;

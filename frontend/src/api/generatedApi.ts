import { emptySplitApi as api } from "./emptyApi";
const injectedRtkApi = api.injectEndpoints({
  endpoints: (build) => ({
    getStatus: build.query<GetStatusApiResponse, GetStatusApiArg>({
      query: () => ({ url: `/api/status` }),
    }),
    submitTranscription: build.mutation<
      SubmitTranscriptionApiResponse,
      SubmitTranscriptionApiArg
    >({
      query: (queryArg) => ({
        url: `/api/transcriptions`,
        method: "POST",
        body: queryArg.body,
      }),
    }),
    getTranscriptionJob: build.query<
      GetTranscriptionJobApiResponse,
      GetTranscriptionJobApiArg
    >({
      query: (queryArg) => ({ url: `/api/transcriptions/${queryArg.id}` }),
    }),
    getTranscript: build.query<GetTranscriptApiResponse, GetTranscriptApiArg>({
      query: (queryArg) => ({
        url: `/api/transcriptions/${queryArg.id}/transcript`,
      }),
    }),
    submitCut: build.mutation<SubmitCutApiResponse, SubmitCutApiArg>({
      query: (queryArg) => ({
        url: `/api/cuts`,
        method: "POST",
        body: queryArg.body,
      }),
    }),
    getCutJob: build.query<GetCutJobApiResponse, GetCutJobApiArg>({
      query: (queryArg) => ({ url: `/api/cuts/${queryArg.id}` }),
    }),
    downloadCut: build.query<DownloadCutApiResponse, DownloadCutApiArg>({
      query: (queryArg) => ({ url: `/api/cuts/${queryArg.id}/download` }),
    }),
  }),
  overrideExisting: false,
});
export { injectedRtkApi as enhancedApi };
export type GetStatusApiResponse = /** status 200 OK */ SystemStatusResponse;
export type GetStatusApiArg = void;
export type SubmitTranscriptionApiResponse =
  /** status 200 OK */ TranscriptionJob;
export type SubmitTranscriptionApiArg = {
  body: {
    file: IFormFile;
  };
};
export type GetTranscriptionJobApiResponse =
  /** status 200 OK */ TranscriptionJob;
export type GetTranscriptionJobApiArg = {
  id: string;
};
export type GetTranscriptApiResponse = /** status 200 OK */ Transcript;
export type GetTranscriptApiArg = {
  id: string;
};
export type SubmitCutApiResponse = /** status 200 OK */ CutJobResponse;
export type SubmitCutApiArg = {
  body: {
    file: IFormFile;
  } & {
    request: string;
  };
};
export type GetCutJobApiResponse = /** status 200 OK */ CutJobResponse;
export type GetCutJobApiArg = {
  id: string;
};
export type DownloadCutApiResponse = unknown;
export type DownloadCutApiArg = {
  id: string;
};
export type SystemStatusResponse = {
  version: string;
  friendlyStatus: string;
  timestamp: string;
};
export type JobStatus = "Pending" | "Processing" | "Completed" | "Failed";
export type TranscriptionJob = {
  id: string;
  status: JobStatus;
  error: null | string;
  createdAt: string;
};
export type PreviewLimitReached = {
  message: string;
};
export type IFormFile = Blob;
export type TranscriptSegment = {
  index: number;
  start: number;
  end: number;
  text: string;
};
export type TranscriptWord = {
  text: string;
  start: number;
  end: number;
  kept: boolean;
};
export type Transcript = {
  transcriptionJobId: string;
  durationSeconds: number;
  segments: TranscriptSegment[];
  words: TranscriptWord[];
};
export type CutJobResponse = {
  downloadUrl: null | string;
  id: string;
  status: JobStatus;
  error: null | string;
  createdAt: string;
};
export const {
  useGetStatusQuery,
  useSubmitTranscriptionMutation,
  useGetTranscriptionJobQuery,
  useGetTranscriptQuery,
  useSubmitCutMutation,
  useGetCutJobQuery,
  useDownloadCutQuery,
} = injectedRtkApi;

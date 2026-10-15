import { createApi, fetchBaseQuery } from '@reduxjs/toolkit/query/react';
import { API_BASE } from './apiBase';

// initialize an empty api service that we'll inject endpoints into later as needed
// Every request goes out under the app's base path — see apiBase.ts for why '/' is wrong.
export const emptySplitApi = createApi({
  baseQuery: fetchBaseQuery({ baseUrl: API_BASE }),
  endpoints: () => ({}),
});

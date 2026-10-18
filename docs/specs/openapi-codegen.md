# OpenAPI → Frontend Codegen

The frontend API client (`src/api/generatedApi.ts`) is generated automatically from the backend's OpenAPI schema. This ensures the frontend types always match the compiled backend — no manual HTTP calls, no drifting field names.

## How It Works

```
.NET Backend
  └── Debug build (Microsoft.Extensions.ApiDescription.Server, in-process)
        └── backend/Balenthiran.Snipit.WebApi/openapi.json  (committed schema)
              └── @rtk-query/codegen-openapi
                    └── src/api/generatedApi.ts  (typed RTK Query hooks)
```

The schema is generated **at build time**, not scraped off a running server: a Debug build loads the
app in-process, asks it for its OpenAPI document, and writes `openapi.json` next to the csproj. That
file is committed, so `npm run codegen` works offline and in CI — no live backend, no database.

The codegen config is in `frontend/openapi-config.cjs`:

```js
const config = {
  schemaFile: '../backend/Balenthiran.Snipit.WebApi/openapi.json',
  apiFile: './src/api/emptyApi.ts',
  apiImport: 'emptySplitApi',
  outputFile: './src/api/generatedApi.ts',
  hooks: true,
};
```

The same document is still served at `GET /openapi/v1.json` (and browsable at `/scalar/v1`) when the
backend runs in Development — that's for humans, not for codegen.

## Running the Codegen

1. **Refresh the schema** with a Debug backend build (no server, no database needed):
   ```bash
   cd backend
   dotnet build Balenthiran.Snipit.WebApi -c Debug   # rewrites openapi.json
   ```

2. **Run codegen** from the `frontend/` directory:
   ```bash
   npm run codegen
   ```

   Commit both `openapi.json` and the regenerated `generatedApi.ts` — a stale pair is how the
   frontend silently drifts from the backend (this repo shipped a client full of `web-template`
   scaffold endpoints for exactly that reason).

3. **Use the generated hooks** in your components:
   ```tsx
   import { useGetStatusQuery } from '../api/generatedApi';

   const { data, isLoading, isError } = useGetStatusQuery();
   ```

## The Base API (`emptyApi.ts`)

`generatedApi.ts` injects its endpoints into `emptySplitApi`, which is defined in `src/api/emptyApi.ts`:

```ts
import { createApi, fetchBaseQuery } from '@reduxjs/toolkit/query/react';

export const emptySplitApi = createApi({
  reducerPath: 'api',
  baseQuery: fetchBaseQuery({ baseUrl: '/' }),
  endpoints: () => ({}),
});
```

This is the base — the codegen injects all endpoints into it via `injectEndpoints`. If you need to add custom endpoints not covered by the OpenAPI schema (e.g. queries against static JSON files), inject them separately:

```ts
// src/api/customApi.ts
import { emptySplitApi as api } from './emptyApi';

export const customApi = api.injectEndpoints({
  endpoints: (build) => ({
    getSomething: build.query<MyType[], void>({
      query: () => ({ url: '/api/something' }),
    }),
  }),
});

export const { useGetSomethingQuery } = customApi;
```

## Important Rules

- **Never hand-edit `generatedApi.ts`** — it is always overwritten by `npm run codegen`
- Run codegen any time you add, rename, or remove a backend endpoint
- The Vite dev server proxies `/api` and `/openapi` to `http://localhost:5257` — see `vite.config.ts`
- In production, the frontend is served by Nginx which proxies `/api` to the backend service (configured in `nginx.conf`)

## Adding a New Endpoint (End-to-End)

1. Add route in `backend/Balenthiran.Snipit.WebApi/Routes/*.cs`
2. Ensure the route is registered in `Program.cs` within the `.WithOpenApi()` chain
3. `dotnet build Balenthiran.Snipit.WebApi -c Debug` to refresh `openapi.json`
4. Run `npm run codegen` in `frontend/`
5. Import and use the new hook (`use*Query` or `use*Mutation`) in your component

## Troubleshooting

| Issue | Fix |
|---|---|
| `ENOENT` on `openapi.json` | Run `dotnet build Balenthiran.Snipit.WebApi -c Debug` first |
| Schema didn't change after a route edit | The generator only runs on **Debug** builds — check the configuration |
| Hook types show as `unknown` | The endpoint has no typed response — add a typed return model in the backend |
| Codegen overwrites custom code | Never put custom code in `generatedApi.ts` — use a separate `customApi.ts` |

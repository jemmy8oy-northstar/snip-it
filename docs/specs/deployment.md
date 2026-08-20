# Deployment

How snip-it gets from a merge to a running app at `https://balenthiran.co.uk/snipit`, and the
one-time cluster setup that has to exist first.

## The pipeline

1. **Push to `main`** → `.github/workflows/docker-build-push.yml` builds both images natively on
   ARM64 and pushes them to OCIR as `snip-it-backend` / `snip-it-frontend`, tagged `latest` and
   the GitVersion semver. The same job then patches `apps[*].image.tag` in `helm/values.yaml`
   with `yq` and commits it back — that commit is what makes a release visible to GitOps.
2. **ArgoCD** syncs the chart in `helm/` for whatever `oke-fleet` says about this app. snip-it is
   **not registered in the fleet yet** — see "Registering with the fleet" below.

Only `main` builds images, so nothing on `dev` is deployed, ever.

## Cluster prerequisites (one-time, needs kubectl)

The chart assumes these exist. Nothing here is in the repo, because none of it can be:

**1. Namespace — `balenthiran`, and it already exists.** snip-it deploys *into the namespace the
other apps already run in*, and this is not a style preference: the chart's Ingress declares
`secretName: balenthiran-tls`, **Secrets are namespace-scoped**, and that one lives in
`balenthiran`. An Ingress in a namespace of its own would not find it, so cert-manager would issue
a *second* certificate for a hostname that already has one — and until it landed, that Ingress
would serve nginx's self-signed "Fake Certificate" (the failure fixed for `www` in
balenthiran.co.uk#21). Whatever authorises the OCIR image pull is a property of that namespace
too: neither this chart nor `balenthiran.co.uk`'s renders an `imagePullSecrets` block, yet the
latter pulls and runs today.

So there is nothing to create here. **If snip-it should ever get its own namespace, the `tls:`
block in `helm/values.yaml` has to change first** — that is the decision, not the `kubectl` line.

> ⚠️ This section used to say `kubectl create namespace snipit`, and the fleet config below used
> `"targetNamespace": "snipit"`, while the config given on #23 used `balenthiran`. Both cannot be
> right and the pairing failed either way round — see #23.

**2. A database on the shared Postgres.** The cluster already runs one StatefulSet for all apps
(`pg-postgresql` in the `data` namespace — see `balenthiran.co.uk/helm/postgres.yaml`). snip-it
needs its own database in it; the app applies its own EF migrations at startup.

```sh
kubectl exec -n data pg-postgresql-0 -- psql -U postgres -c "CREATE DATABASE snipit;"
```

**3. The app secret.** Both values are read as env vars by the backend deployment:

```sh
kubectl create secret generic snipit-secrets -n balenthiran \
  --from-literal=DATABASE_URL="Host=pg-postgresql.data.svc.cluster.local;Port=5432;Database=snipit;Username=postgres;Password=<password>" \
  --from-literal=GROQ_API_KEY="<groq key>"
```

`DATABASE_URL` is an Npgsql connection string, not a URI — it is bound straight to
`ConnectionStrings:DefaultConnection`. With no connection string the app still starts, logs
`Skipping database migration`, and 500s on every route that touches a job.

## What the chart wires up, and why

| Setting | Why it is there |
| --- | --- |
| *(no `persistence`, no PVC, no volumes)* | snip-it keeps nothing of a visitor's media between requests (#22). The browser holds the video and re-sends it with the cut, so every file on disk belongs to one job or one response and is deleted by it. What is left is scratch under the OS temp path, sized by *concurrent* work rather than cumulative use — which is also why there is no `strategy: Recreate` and no single-node pinning. The chart is field-for-field the shared template. |
| `PathBase=/snipit` | The host is shared with the other apps, so the backend is served under a sub-path and the ingress forwards the whole path. `UsePathBase` strips it before routing. |
| `proxy-body-size: 2048m`, `proxy-*-timeout: 600` | nginx-ingress defaults (1 MB, 60s) reject or cut off essentially every real video upload. |
| `Uploads__MaxBytes` | Kestrel caps request bodies at 30 MB and multipart sections at 128 MB by default. All three limits — ingress, Kestrel, form options — have to allow the upload or it fails as a bare 413. |
| `ffmpeg` in `backend/Dockerfile` | Both pipelines shell out to it by bare command name. The base image has none, so the app starts fine and then fails every job at run time. |

## The sub-path, in all four places it has to agree

The app is served from `balenthiran.co.uk/snipit`, not a host of its own, and `/api` on that host
belongs to a **different application**. So a mis-prefixed request is not a 404 you would notice in
testing — it is a request answered by somebody else's app. Four things have to line up:

| Where | What |
| --- | --- |
| Vite | `base: '/snipit/'` — builds asset URLs and defines `import.meta.env.BASE_URL`. |
| Frontend API calls | `src/api/apiBase.ts` derives `API_BASE` from `BASE_URL`; `emptyApi.ts` uses it as the RTK Query `baseUrl`, so every generated *and* hand-written endpoint inherits it. Nothing bypasses RTK any more — the `<video src>` used to, and since #22 it is an object URL over a local file, which has no base path to get wrong. |
| Backend routing | `PathBase=/snipit` → `UsePathBase` strips the prefix before routing, because the ingress forwards the whole path. |
| Backend-generated links | `CutJobResponse.DownloadUrl` is followed by the browser, so it is built with `Request.PathBase` in front. Path base is empty locally, so the local string is unchanged. |

`vitest.config.ts` also sets `base` — it does not extend `vite.config.ts`, so without it `BASE_URL`
is `/` under test and the API base would be tested as a value the app never runs with.

Uploads are buffered to the container's temp directory, and since #22 that is also where they
stay for the life of the job — so the pod needs ephemeral disk sized by the largest *concurrent*
work, not by cumulative use. A video is now sent twice: once to transcribe, once to cut.

## Registering with the fleet

Deployment is driven by `oke-fleet`, which needs a four-line config file:

```json
{
  "appName": "snip-it",
  "repoURL": "https://github.com/jemmy8oy-northstar/snip-it.git",
  "chartPath": "helm",
  "targetNamespace": "balenthiran"
}
```

`targetNamespace` must match the namespace holding `balenthiran-tls` and `snipit-secrets` — see
prerequisite 1. The ApplicationSet sets no `CreateNamespace=true`, so it never creates one either.
`appName` only names the ArgoCD Application; it is the one field here that is free choice.

That goes in `oke-fleet/config/snip-it.json` as a PR into its `dev`, then a `dev` → `main`
promotion (the generator reads config from `main`). **Deliberately not raised yet** — registering
it points ArgoCD at the cluster before the secrets above exist, which just produces a
CrashLoopBackOff, and going live is a decision, not a chore. See "Open decisions".

## Verifying a live deploy

```sh
curl https://balenthiran.co.uk/snipit/api/status         # {"status":"Healthy",...}
```

Then the real path: open `/snipit/editor`, upload a short clip, and watch the transcription job
move to `Completed`. That exercises ffmpeg, the Groq key, scratch disk and the database in one go —
a green `/status` proves none of them.

## Open decisions

- **The app is public and unauthenticated, on purpose.** James's call on #13, 2026-08-13:
  *"I kind of want this open and can then let my viewers use it if they want."* So the protection
  is a **budget, not a door**: `Preview__MaxTranscriptionsPerDay` (default 25, in
  `helm/values.yaml`) caps site-wide transcriptions per UTC day, and a provider rate-limit
  response puts the app in a short cooldown. Both refuse the upload **before the body is read**,
  with a friendly "this is a preview" sentence rather than an error. Raising the cap is a
  one-value `helm upgrade`. What this deliberately does *not* stop is one determined visitor
  eating the whole day's allowance — there is no identity to meter against, and adding one was
  the thing the openness decision rejected.
- **A cut can be downloaded once.** The export is deleted as it is streamed (#22), so a second
  click on the same link is a 404. The editor says so next to the link rather than letting it be
  discovered. Keeping it until the pod restarts instead is a one-line change to `DownloadAsync`.
- **A reloaded editor cannot play or export until the file is re-picked.** The browser is the only
  thing that still has the video, and a reload loses it. The transcript is unaffected — it lives in
  Postgres — so the page stays useful and asks for the file back.
- **Nothing bounds cut CPU.** `Preview__MaxTranscriptionsPerDay` caps transcriptions; a cut is a
  full H.264 re-encode and has no quota of its own. It stopped being a *storage* problem with the
  volume, but it is still a CPU one.
- **No route-level tests.** `Balenthiran.Snipit.Tests` does not reference the WebApi project, so
  nothing covers the `PathBase`-aware download link end to end. web-template#81's DB-free
  `WebApplicationFactory` pattern would port here cheaply.
- **The image is not built here.** No container runtime in the environment these changes were made
  in, so `apk add ffmpeg` and the published layout are unverified until CI builds `main`.

# Personal Web Template

A `dotnet new` monorepo template for .NET 10 + React 19 projects. Every new project you start is scaffolded from this template.

## Stack

- **Backend**: .NET 10, ASP.NET Core Minimal APIs, Entity Framework Core, PostgreSQL, Scalar/OpenAPI
- **Frontend**: React 19, TypeScript, Vite (Rolldown), Redux Toolkit + RTK Query
- **Infrastructure**: Docker (multi-stage), Helm, Kubernetes (OCI)

## Prerequisites

- [.NET 10 SDK](https://dotnet.microsoft.com/download)
- [Node.js 20+](https://nodejs.org/)
- [PostgreSQL](https://www.postgresql.org/) (local or Docker)

## Creating a New Project

### 1. Install the Template

```bash
dotnet new install /path/to/web-template
```

### 2. Scaffold Your Project

Pass your solution name as `Company.ProjectName` and an app name for deployment config:

```bash
dotnet new web-template -n Balenthiran.Apeify --appName apeify
```

To skip GitHub Actions workflow files (avoids needing `--force` on first push):

```bash
dotnet new web-template -n Balenthiran.Apeify --appName apeify --no-includeWorkflows
```

This generates:

```
Balenthiran.Apeify/
├── backend/
│   ├── Balenthiran.Apeify.Abstractions/
│   ├── Balenthiran.Apeify.DataModels/
│   ├── Balenthiran.Apeify.DomainModels/
│   ├── Balenthiran.Apeify.EntityModels/
│   ├── Balenthiran.Apeify.Services/
│   ├── Balenthiran.Apeify.Database/
│   ├── Balenthiran.Apeify.WebApi/
│   └── Balenthiran.Apeify.slnx
├── frontend/
├── scripts/
├── helm/
└── .github/
    └── workflows/
```

### 3. Run the Onboarding Script

```bash
cd Balenthiran.Apeify
node scripts/init.mjs
```

This generates `appsettings.Development.json` (with a fresh JWT secret) and `frontend/.env`, then runs `dotnet restore` and `npm install`. The connection string defaults to `Host=localhost;Database=indie_dev_home;Username=postgres;Password=postgres` — edit it if your local Postgres differs.

### 4. Apply the Initial Migration

```bash
cd backend
dotnet ef database update \
  --project Balenthiran.Apeify.Database \
  --startup-project Balenthiran.Apeify.WebApi
```

### 5. Run the App

```bash
# Terminal 1 — backend
cd backend && dotnet run --project Balenthiran.Apeify.WebApi
# http://localhost:5000  |  docs: http://localhost:5000/scalar/v1

# Terminal 2 — frontend
cd frontend && npm run dev
# http://localhost:5173
```

---

## Running Locally

`appsettings.Development.json` is gitignored. Run `node scripts/init.mjs` to generate it, or create it manually at `backend/Balenthiran.Snipit.WebApi/appsettings.Development.json`:

```json
{
  "ConnectionStrings": {
    "DefaultConnection": "Host=localhost;Database=your_db;Username=your_user;Password=your_password"
  },
  "Jwt": {
    "Secret": "your-secret-key-min-32-chars-long"
  }
}
```

`frontend/.env` is also gitignored. Create it at `frontend/.env` if not generated:

```
VITE_API_URL=http://localhost:5000
```

### Regenerate the API client

Run after any backend endpoint change to keep frontend types in sync:

```bash
cd frontend && npm run codegen
```

See `docs/specs/openapi-codegen.md` for the full workflow.

---

## New Project Checklist

After scaffolding with `dotnet new web-template -n Balenthiran.Apeify --appName apeify`:

### Local setup
- [ ] Run `node scripts/init.mjs` — generates `appsettings.Development.json` and `frontend/.env`
- [ ] Run the initial EF migration: `dotnet ef database update --project *.Database --startup-project *.WebApi`
- [ ] Verify the app starts: backend on `http://localhost:5000`, frontend on `http://localhost:5173`

### Branding / placeholders
- [ ] `frontend/src/components/Hero.tsx` — replace "Your App Name" and the subtitle
- [ ] `frontend/src/App.tsx` — replace footer placeholder
- [ ] `frontend/src/components/Navbar.tsx` — replace "App Name" in the mobile logo
- [ ] `frontend/src/data/config.json` — add nav routes
- [ ] `frontend/index.html` — update `<title>`

### Helm
- [ ] `helm/values.yaml` — set `fullnameOverride` to your app name (lowercase, hyphens), set `apps[*].ingress.path` to `/{app-name}` (frontend) and `/{app-name}/api` (backend)
- [ ] `.github/workflows/docker-build-push.yml` — set `FRONTEND_IMAGE` and `BACKEND_IMAGE` to match `helm/values.yaml`

### GitHub Actions
- [ ] Repository **Variables** (Settings → Secrets and variables → Variables):
  - `OCIR_REGISTRY` — e.g. `lhr.ocir.io`
  - `OCIR_NAMESPACE` — your OCI tenancy namespace
- [ ] Repository **Secrets**:
  - `OCIR_USERNAME` — e.g. `your-tenancy/oracleidentitycloudservice/your@email.com`
  - `OCIR_AUTH_TOKEN` — OCI auth token (OCI Console → User Settings → Auth Tokens)
  - `ANTHROPIC_API_KEY` — required for the Claude workflow
- [ ] **Private repo?** `ci.yml` runs on every PR and counts against the 2,000 free minutes/month. Disable it in Settings → Actions → General if not needed.

### Kubernetes
- [ ] `kubectl create namespace your-app`
- [ ] Provision a PostgreSQL database and note the connection string
- [ ] `kubectl create secret generic your-app-secrets --from-literal=DATABASE_URL="Host=...;Database=...;Username=...;Password=..." -n your-app`

### Git & GitHub
- [ ] `git init && git add . && git commit -m "Initial commit from web-template"`
- [ ] Create `main` and `dev` branches — all PRs target `dev`; `dev → main` is human-only
- [ ] Add remote and push
- [ ] Enable **"Automatically delete head branches"** (Settings → General)

### SDD bootstrap
- [ ] Run `node scripts/init-issues.mjs` — creates all Phase 1–7 orchestrator issues with the `MVP` milestone and the three workflow labels. This is the starting point for the AI-assisted workflow. See [`docs/ai-workflow.md`](docs/ai-workflow.md).

### Documentation
- [ ] Overwrite this `README.md` with your project's spec — what the product does, who it's for, and how to run it
- [ ] Flesh out `docs/specs/` with feature specs before writing code
- [ ] Update `CLAUDE.md` to reflect your project's conventions

---

## GitHub Actions

Four workflows are included in `.github/workflows/`:

| Workflow | Trigger | Purpose |
|---|---|---|
| `ci.yml` | Pull requests | Builds backend and frontend to catch compile errors |
| `docker-build-push.yml` | Manual (`workflow_dispatch`) | Builds ARM64 Docker images and pushes to OCI Container Registry |
| `claude.yml` | `@claude` mentions in issues/PRs | Runs the Claude AI agent on the tagged thread |
| `check-source-branch.yml` | Pull requests | Enforces that PRs target `dev`, not `main` |

### ARM64 Runner Note

`docker-build-push.yml` uses `ubuntu-24.04-arm` (native ARM64, required for OKE free tier). This runner is **free for public repositories**. For private repositories it requires a paid GitHub plan — see the comment at the top of `docker-build-push.yml` for the `ubuntu-latest` + QEMU fallback.

---

## Project Structure

| Layer | Project | Responsibility |
|---|---|---|
| API | `*.WebApi` | Routes, DI, OpenAPI, middleware |
| Services | `*.Services` | Business logic, AutoMapper profiles |
| Abstractions | `*.Abstractions` | Service interfaces, model interfaces |
| Database | `*.Database` | EF Core DbContext, migrations |
| Entity Models | `*.EntityModels` | EF Core entity classes |
| Domain Models | `*.DomainModels` | Rich business-layer objects |
| Data Models | `*.DataModels` | Request/Response DTOs |

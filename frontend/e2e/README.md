# Frontend e2e / screenshot tests (Playwright)

Deterministic end-to-end tests that drive the SnipIt frontend off **mocked API
responses** (no backend required) and capture full-page **screenshots** of the
transcript editor for visual review. Same pattern used across the Northstar
frontends.

## Layout

| File | Purpose |
|---|---|
| `../playwright.config.ts` | Config — boots the Vite dev server, targets Chromium. |
| `mocks.ts` | Fulfils the backend endpoints (`/api/transcripts/{id}`, `/api/cuts`) with the shared mock fixture — dormant while the editor self-mocks via RTK `queryFn`, active the moment that's swapped for real `query:` calls. |
| `editor.spec.ts` | Smoke asserts + screenshots for the landing page and the editor (light, dark, and after a filler-word removal). |
| `screenshots/` | Generated PNGs land here. |

## Run locally

```bash
cd frontend
npm install
npx playwright install chromium   # one-time: downloads the browser
npm run test:e2e                  # headless; starts the dev server for you
npm run test:e2e -- --ui          # interactive runner
```

Screenshots are written to `frontend/e2e/screenshots/`. `npx playwright
show-report` opens the HTML report after a run.

## CI (recommended — this is how screenshots get reviewed per-PR)

The bot's sandbox has no browser libraries, so **it cannot render screenshots
itself** — CI (or a local run) produces them. Add this workflow so every PR
attaches the editor screenshots as a downloadable artifact:

```yaml
# .github/workflows/frontend-e2e.yml
name: frontend-e2e
on:
  pull_request:
    paths: ['frontend/**']
jobs:
  e2e:
    runs-on: ubuntu-latest
    defaults:
      run:
        working-directory: frontend
    steps:
      - uses: actions/checkout@v4
      - uses: actions/setup-node@v4
        with:
          node-version: 20
      - run: npm ci
      - run: npx playwright install --with-deps chromium
      - run: npm run test:e2e
      - uses: actions/upload-artifact@v4
        if: always()
        with:
          name: e2e-screenshots
          path: frontend/e2e/screenshots/
```

Download the `e2e-screenshots` artifact from the PR's checks to review.

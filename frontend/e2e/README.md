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
| `screenshots/` | Generated PNGs land here. **Gitignored** — see below. |

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

## The screenshots are not committed, and they assert nothing

`page.screenshot()` is a plain **write**. There is no `toHaveScreenshot` in this
repo (or in any repo in the org), so **no screenshot can ever fail a build** —
they exist purely to be looked at. They were committed until #15, where James
asked for them out: *"get rid of the committed pngs I think it wastes git
storage."* Nothing referenced them except the spec that writes them.

`frontend/e2e/screenshots/` is now gitignored. Run the suite locally and they
appear; they just never enter git.

## Reviewing them on a PR

`ci.yml` currently uploads `playwright-report/` and only `if: failure()`, so a
green run publishes nothing to look at. To get the screenshots per-PR, add this
step to the `e2e` job in `.github/workflows/ci.yml` (the bot cannot author
workflow files — its GitHub App lacks the `workflows` permission):

```yaml
      - name: Upload e2e screenshots
        if: always()
        uses: actions/upload-artifact@v4
        with:
          name: e2e-screenshots
          path: frontend/e2e/screenshots/
          retention-days: 7
```

<!-- Corrections, 2026-08-15: this section previously described a
     frontend-e2e.yml workflow that was never added, and claimed the bot's
     sandbox has no browser libraries. Playwright has run in-pod since
     2026-08-09. Check before repeating either claim. -->


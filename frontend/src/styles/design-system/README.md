# Vendored design-system tokens

**Do not edit these files.** They are a verbatim mirror of
[`jemmy8oy-northstar/design-system`](https://github.com/jemmy8oy-northstar/design-system)
`src/tokens/`, pinned to:

    commit  0d52a9173544db8a6b2368f731b8f1b49039b242   (dev, 2026-08-16)

| file | md5 |
|---|---|
| `primitives.css` | `68a7d440…` |
| `semantic.css` | `d889e699…` |
| `base.css` | `325110b7…` |
| `index.css` | `a84924a3…` |
| `themes/casual.css` | `52d5078c…` |
| `themes/studio.css` | `d3f1a86e…` |
| `themes/professional.css` | `3c736c10…` |

## Why vendored, and not a package

`@jemmy8oy-northstar/design-system` publishes `dist/`, which is **not committed**
and **not published to any registry** — so neither `npm install` nor a
`github:` dependency resolves today. Vendoring the CSS is the only mechanism
that currently works, and it was the agreed starting point (claude-code-bot#53:
*"start vendored — a single CSS file, zero auth, zero publishing — and move to
GitHub Packages at the third consumer, when the copy-paste actually starts
hurting"*). snip-it is consumer #1.

## Why the WHOLE token layer, including themes this app does not use

`casual.css` and `professional.css` are dead weight here — about 1 KB gzipped.
They are included anyway because a **complete** mirror is checkable and a
hand-picked subset is not: this directory can be diffed against upstream in one
command, and any difference is drift rather than a decision someone made once
and did not write down.

## Refreshing

    # from the snip-it repo root, with design-system cloned alongside
    git -C ../design-system fetch origin
    cp -R ../design-system/src/tokens/. frontend/src/styles/design-system/
    # then re-pin the commit + hashes above, and re-run the app's e2e screenshots

⚠️ **The pin is `dev`, not `main`.** design-system's default branch is `main`,
and `main` does **not** contain the `studio` theme, the graphite neutral ramp,
the `--radius-xs` token or the type roles — all five of those live only on `dev`
(5 commits ahead, clean merge, no promotion PR open). This app therefore depends
on state that is not on design-system's default branch. That is a real
loose end, flagged on the adoption PR rather than papered over.

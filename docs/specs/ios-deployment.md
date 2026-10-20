# iOS deployment

How the Teleprompter app gets from this repo to TestFlight, and the things that cost a day to
work out the first time.

`docs/specs/deployment.md` covers the web app. This is the iOS half, which shares nothing with it:
different CI, different credentials, different failure modes.

## The pipeline

| Stage | Where | What it proves |
|---|---|---|
| Package tests | `.github/workflows/ios-package.yml` | `ScriptFollower` logic, on Linux |
| Unsigned compile | `.github/workflows/ios-app.yml` | The SwiftUI app builds, on `macos-15` |
| Signed release | `codemagic.yaml` | Signs, archives, uploads to TestFlight |

GitHub does the free, unsigned half on every PR touching `ios/**`. Codemagic does the release half,
because it needs Apple credentials that must never be in this repo. Codemagic runs only when started
from its UI — there is no `triggering` block.

The Xcode project is **generated** by xcodegen from `ios/Teleprompter/project.yml` and is git-ignored,
so every pipeline runs `xcodegen` before building. `project.yml` is the source of truth for the bundle
identifier, Info.plist keys and build settings — never the `.xcodeproj`.

## Code signing: the thing to understand

Codemagic has three signing mechanisms that look interchangeable and are not. Picking the wrong one
produces an error that points nowhere near the actual cause.

| Mechanism | Where configured | Talks to Apple? | Who can use it |
|---|---|---|---|
| UI automatic code signing | Workflow editor | **Yes** — creates cert + profile | **Flutter apps only** |
| `ios_signing:` block | `codemagic.yaml` | **No** — reads Codemagic's own store | Anyone |
| `fetch-signing-files` | `codemagic.yaml` scripts | **Yes** — creates cert + profile | Anyone |

The trap: if you have used the Flutter workflow editor before, you know automatic signing as a
checkbox that Just Works. Moving to yaml for a native app, `ios_signing:` looks like the equivalent.
It isn't. It only matches provisioning profiles **already uploaded** to Codemagic's *Code signing
identities* store, and never contacts Apple. With an empty store it fails with:

```
No matching profiles found for bundle identifier "..." and distribution type "app_store"
```

That message names the bundle ID and distribution type, which makes it read like an Apple-side
problem. It is not. It means "nothing in my local store matched" — the lookup never left Codemagic.

**A native app wants the third option.** That is what this repo uses:

```yaml
- name: Fetch or create the signing files
  script: |
    keychain initialize
    app-store-connect fetch-signing-files "$BUNDLE_ID" \
      --type IOS_APP_STORE \
      --create
    keychain add-certificates

- name: Use the signing profiles
  script: xcode-project use-profiles
```

`--create` is load-bearing. It creates the Bundle ID identifier, the distribution certificate and the
provisioning profile when they do not exist, which is exactly the situation for a new app. Without it
a brand-new identifier can never succeed.

### What it needs

1. **An App Store Connect API key**, added under *Teams → Integrations → Developer Portal*, and
   referenced in yaml **by the name you gave it**:

   ```yaml
   integrations:
     app_store_connect: CodeMagicKeyDealMapper
   ```

   The left side is the integration *type*; the right side is the key's name. A wrong name gives
   `App Store Connect integration "..." does not exist`. The integration injects
   `APP_STORE_CONNECT_ISSUER_ID`, `APP_STORE_CONNECT_KEY_IDENTIFIER` and
   `APP_STORE_CONNECT_PRIVATE_KEY`, so the CLI authenticates with no further setup.

   API keys are **team-level, not app-level**. A key created for another app signs this one fine —
   the name is cosmetic. It needs the **App Manager** role; a Developer-role key can read but not
   create.

2. **`CERTIFICATE_PRIVATE_KEY`**, a PEM private key in an environment variable group:

   ```bash
   ssh-keygen -t rsa -b 2048 -m PEM -f cert_key -q -N ""
   ```

   Paste the whole file — both `-----` lines included — as a **Secret** variable in the group
   `appstore_credentials`, which `codemagic.yaml` references under `environment.groups`.

   This is the piece the Flutter UI hides from you. It generates and stores a key internally; the CLI
   is stateless and has no such store, so you supply one. Apple only ever returns the public `.cer`,
   so without a private key the build gets as far as:

   ```
   Cannot save Signing Certificates without certificate private key
   ```

   Reuse the same key forever. Apple caps active distribution certificates at 2–3, and a new key
   means `--create` issues a new certificate rather than reusing the existing one. If a run fails on
   the certificate limit, revoke an unused one in the Developer Portal.

## Environment variables: per-app only

Codemagic has app-level and team-level variable groups, referenced identically in yaml
(`groups: - appstore_credentials`). Team-level is the obvious home for a signing key that is
genuinely team-wide.

**It is not available on a personal account.** Codemagic shows:

> Global variables and secrets will be removed from personal accounts in the future. Existing
> variables are now read-only and can only be deleted. Add your variables in app settings or
> upgrade to a team account for global use.

So on a personal account every app needs its own copy of `CERTIFICATE_PRIVATE_KEY`. There is no way
around it short of upgrading. Groups are not created as standalone objects — you create one by typing
its name while adding the first variable, which is why the group dropdown is empty on a new app.

If both levels ever define the same name, **app-level wins**. That is a silent override worth
remembering if a build ever signs with an unexpected certificate.

### Automating it

Because the per-app copy is unavoidable, the REST API is worth wiring into tooling:

```bash
curl --request POST \
  --url https://codemagic.io/api/v3/variable-groups/{variable_group_id}/variables \
  --header 'Content-Type: application/json' \
  --header "x-auth-token: $CODEMAGIC_AUTH_TOKEN" \
  --data '{
    "secure": true,
    "variables": [{ "name": "CERTIFICATE_PRIVATE_KEY", "value": "..." }]
  }'
```

- Token: *Account settings → API token*
- Application id: in the URL at `codemagic.io/app/<APP_ID>/settings`
- The group must exist first; its id comes from the API schema under *Secrets and Environment Vars*
- File-shaped secrets go in base64-encoded and are `base64 --decode`d during the build

> **Unverified.** Only the variable-insertion call above is documented; the group-*creation*
> endpoint's shape was never confirmed, and Codemagic publishes no complete endpoint reference.
> Treat this as the right shape, not a tested script.

Anything holding this key can sign and ship releases. It is also the one credential that cannot be
rotated quietly — revoking it invalidates profiles across every app using it. If automation gets a
Codemagic token, give it its own, not a personal one.

## Bundle identifier

Currently `uk.co.balenthiran.yourstudio`, set in `ios/Teleprompter/project.yml` and mirrored in
`codemagic.yaml`.

**Both must match.** `xcode-project use-profiles` assigns profiles to build targets by bundle ID.
A mismatch fetches a profile that matches no target and leaves the archive unsigned — with no error
at the signing step.

It started as `com.balenthiran.*`. That reverse-DNS prefix pointed at a domain that lapsed after a
missed card payment and was claimed by someone else; everything else in the repo had already moved to
`balenthiran.co.uk`. Apple never validates domain ownership against bundle IDs, so there was no
functional problem — but an identifier is permanent in a way a domain is not, so it was worth
correcting while it was still free.

**The window closes at the first upload.** App Store Connect locks the bundle ID on an app record as
soon as a build is accepted — *"It can't be changed after you upload your first build."* Before then
it is a dropdown change. After, it is a new app record, losing reviews, ratings and existing users.
Get this right before the first successful TestFlight upload.

The Keychain service string in `SettingsView.swift` tracks the bundle ID by convention only; it is an
arbitrary key. Changing it orphans any saved password, which is harmless while nothing has shipped.

## App icon

A build with no app icon compiles, signs and archives perfectly, then fails at **upload**:

```
90022  Missing required icon file ... exactly '120x120' pixels
90713  Missing Info.plist value ... 'CFBundleIconName'
```

Two errors, one cause. The fix is an asset catalog plus two settings in `project.yml`:

```yaml
info:
  properties:
    CFBundleIconName: AppIcon
settings:
  base:
    ASSETCATALOG_COMPILER_APPICON_NAME: AppIcon
```

Icons live in `ios/Teleprompter/Sources/Assets.xcassets/AppIcon.appiconset/`. They must be committed —
they are not generated. **No alpha channel**: Apple rejects icons with transparency. The app is
iPhone-only (`TARGETED_DEVICE_FAMILY: "1"`), so iPad sizes are unnecessary.

The current icon is a generated placeholder, not a design.

## Setting up a new app, start to finish

1. App Store Connect → create the app record. Decide the bundle ID now.
2. Developer Portal → the identifier is created by `--create` on first build, if it does not exist.
3. Codemagic → add the repo; name the App Store Connect API key and use that name in `integrations:`.
4. Codemagic → app settings → Environment variables → group `appstore_credentials`, add
   `CERTIFICATE_PRIVATE_KEY` as **Secret**. Reuse the existing key.
5. `codemagic.yaml` → bundle ID in `BUNDLE_ID`, matching `project.yml`.
6. Build.

## Failure quick reference

| Symptom | Cause |
|---|---|
| `A required agreement is missing or has expired` | Unsigned Apple agreement. The **Account Holder** must accept it; 5–10 min to propagate. Blocks all bundle-ID and profile API calls. |
| `No matching profiles found for ... "app_store"` | Using `ios_signing:` with an empty Code signing identities store. Switch to `fetch-signing-files --create`. |
| `App Store Connect integration "..." does not exist` | The yaml name does not match the key's name in Codemagic. |
| `Cannot save Signing Certificates without certificate private key` | `CERTIFICATE_PRIVATE_KEY` missing, or pasted without its `-----` lines. |
| `90022` / `90713` at upload | No app icon / no `CFBundleIconName`. |
| Archive unsigned, no error | Bundle ID in `project.yml` does not match `codemagic.yaml`. |

## Local builds

`brew install xcodegen`, then `xcodegen` in `ios/Teleprompter`.

Compiling locally also needs the iOS platform component installed in Xcode — having the iOS SDK on
disk is not the same thing, and without the component every destination fails with *"iOS <version> is
not installed"*. `xcodebuild -downloadPlatform iOS` fixes it, at several GB. CI runners have it
already, so this is only needed to build on your own machine.

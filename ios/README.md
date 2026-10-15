# ios/

The teleprompter: snip-it's iPhone recorder (design: claude-code-bot#105).

| Path | What | Builds where |
|---|---|---|
| `ScriptFollower/` | Follows the speaker through the script from what the phone hears. Plain Swift, no Apple frameworks. | Linux or macOS: `swift test --package-path ios/ScriptFollower` (CI: `.github/workflows/ios-package.yml`) |
| the app (not yet) | SwiftUI prompter, camera, on-device speech, upload to snip-it's API for the pause cut. | macOS / Xcode only |

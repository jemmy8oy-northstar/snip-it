# ios/

The teleprompter: snip-it's iPhone recorder (design: claude-code-bot#105).

| Path | What | Builds where |
|---|---|---|
| `ScriptFollower/` | The app's logic with no Apple frameworks: `ScriptFollower` (follows the speaker through the script) and `SnipItClient` (a take in, its pauses cut, the mp4 back). | Linux or macOS: `swift test --package-path ios/ScriptFollower` (CI: `.github/workflows/ios-package.yml`) |
| `Teleprompter/` | The SwiftUI app: script, prompter over the front camera, takes, settings. WhisperKit hears you on-device. | macOS only. CI compiles it unsigned (`.github/workflows/ios-app.yml`); Codemagic signs it for TestFlight (`codemagic.yaml`). |

## Running the app on your iPhone

1. `brew install xcodegen`
2. `cd ios/Teleprompter && xcodegen`, then `open Teleprompter.xcodeproj`.
3. In **Signing & Capabilities**, pick your team. The project file is generated and git-ignored, so the team is never committed.
4. Run it on the phone. In **Settings → Speech model**, download one (`base.en` is ticked by default). The prompter never downloads; without a model it says so and you move lines by tapping.
5. In **Settings**, enter snip-it's address and its basic-auth login. **Cut pauses** on a take needs snip-it deployed with snip-it#29.

## Check these first on a real device

- **The mic is shared.** WhisperKit listens through its own audio engine while the camera records sound. `CameraRecorder` stops the capture session from reconfiguring the audio session, which is what usually lets the two coexist. If the script stops following once recording starts, or the take has no sound, that is the cause. The fix is one mic consumer: feed the capture session's audio into WhisperKit.
- **Whisper's latency.** The follower needs words within about a second. If `base.en` lags, try `tiny.en`.

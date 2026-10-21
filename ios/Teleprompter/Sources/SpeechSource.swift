import Foundation
@preconcurrency import WhisperKit

/// Where the prompter hears from. One engine today; the protocol is what keeps a swap cheap.
protocol SpeechSource: AnyObject {
    /// Starts listening. `heard` gets everything heard so far in this run, again each time the
    /// engine revises it, which is the shape `ScriptFollower.hear` takes. `status` gets "" once it
    /// is listening, or why it stopped. `level` gets the microphone's loudness, 0 to 1, so the
    /// screen can show it is hearing anything at all. A failure to start is thrown instead.
    func start(
        heard: @escaping @Sendable (String) -> Void,
        level: @escaping @Sendable (Float) -> Void,
        status: @escaping @Sendable (String) -> Void
    ) async throws
    func stop() async
}

/// Each case names the step that failed, so the message on screen says where to look.
enum SpeechError: LocalizedError {
    case notDownloaded(String)
    case loadFailed(String)
    case listeningFailed(String)

    var errorDescription: String? {
        switch self {
        case .notDownloaded(let model):
            "The speech model (\(model)) isn't downloaded. Download it in Settings. Until then, tap a line to move on."
        case .loadFailed(let reason):
            "The speech model would not load: \(reason)"
        case .listeningFailed(let reason):
            "Listening stopped: \(reason)"
        }
    }
}

/// WhisperKit, on-device (his call on claude-code-bot#105). Models are downloaded in Settings
/// (`SpeechModels`); this only ever uses one that is already on the phone.
///
/// ⚠️ Untested on a device: WhisperKit captures the mic through its own AVAudioEngine while the
/// camera records audio through AVCaptureSession. `CameraRecorder` stops the capture session
/// reconfiguring the shared audio session, which is the usual way to let the two coexist. If
/// they still fight, the fallback is one mic consumer: feed the capture session's audio samples
/// into WhisperKit instead of letting it record.
final class WhisperKitSpeechSource: SpeechSource {
    private let model: String
    private var transcriber: AudioStreamTranscriber?
    private var listening: Task<Void, Never>?

    init(model: String) {
        self.model = model
    }

    /// WhisperKit's own voice-activity gate: buffers quieter than this, on `level`'s scale, are
    /// skipped as silence and never transcribed. The prompter marks it on the level meter.
    static let silenceThreshold: Float = 0.3

    func start(
        heard: @escaping @Sendable (String) -> Void,
        level: @escaping @Sendable (Float) -> Void,
        status: @escaping @Sendable (String) -> Void
    ) async throws {
        let kit = try await SpeechModels.shared.engine(for: model)
        guard let tokenizer = kit.tokenizer else {
            throw SpeechError.loadFailed("its tokenizer did not load")
        }

        let transcriber = AudioStreamTranscriber(
            audioEncoder: kit.audioEncoder,
            featureExtractor: kit.featureExtractor,
            segmentSeeker: kit.segmentSeeker,
            textDecoder: kit.textDecoder,
            tokenizer: tokenizer,
            audioProcessor: kit.audioProcessor,
            decodingOptions: DecodingOptions(),
            silenceThreshold: Self.silenceThreshold,
            stateChangeCallback: { old, new in
                // The state changes many times a second; pass on only what moved.
                if let energy = new.bufferEnergy.last, energy != old.bufferEnergy.last {
                    level(energy)
                }
                let text = Self.text(of: new)
                if text != Self.text(of: old) {
                    heard(text)
                }
            }
        )
        self.transcriber = transcriber
        status("")

        // Runs until stopped, so it cannot be awaited here.
        listening = Task {
            do {
                try await transcriber.startStreamTranscription()
            } catch {
                status(SpeechError.listeningFailed(error.localizedDescription).localizedDescription)
            }
        }
    }

    func stop() async {
        await transcriber?.stopStreamTranscription()
        listening?.cancel()
        listening = nil
        transcriber = nil
    }

    private static func text(of state: AudioStreamTranscriber.State) -> String {
        spoken((state.confirmedSegments + state.unconfirmedSegments).map(\.text).joined(separator: " "))
    }

    /// Whisper's segment text can carry special tokens such as `<|0.00|>` or `<|endoftext|>`.
    /// They are not speech, and their digits would read as spoken words to the follower.
    static func spoken(_ text: String) -> String {
        text.replacingOccurrences(of: "<\\|[^|]*\\|>", with: " ", options: .regularExpression)
    }
}

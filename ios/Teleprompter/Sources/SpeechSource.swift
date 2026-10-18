import Foundation
@preconcurrency import WhisperKit

/// Where the prompter hears from. One engine today; the protocol is what keeps a swap cheap.
protocol SpeechSource: AnyObject {
    /// Starts listening. `heard` gets everything heard so far in this run, again each time the
    /// engine revises it, which is the shape `ScriptFollower.hear` takes. `status` gets
    /// human-readable progress ("Downloading the speech model…"), or "" once it is listening.
    func start(heard: @escaping @Sendable (String) -> Void, status: @escaping @Sendable (String) -> Void) async throws
    func stop() async
}

enum SpeechError: LocalizedError {
    case modelNotLoaded

    var errorDescription: String? { "The speech model did not load." }
}

/// WhisperKit, on-device (his call on claude-code-bot#105). The model downloads on first use.
///
/// ⚠️ Untested on a device: WhisperKit captures the mic through its own AVAudioEngine while the
/// camera records audio through AVCaptureSession. `CameraRecorder` stops the capture session
/// reconfiguring the shared audio session, which is the usual way to let the two coexist. If
/// they still fight, the fallback is one mic consumer: feed the capture session's audio samples
/// into WhisperKit instead of letting it record.
final class WhisperKitSpeechSource: SpeechSource {
    private let model: String
    private var whisperKit: WhisperKit?
    private var transcriber: AudioStreamTranscriber?
    private var listening: Task<Void, Never>?

    init(model: String) {
        self.model = model
    }

    /// One engine across prompter runs, so the model loads once rather than on every run.
    /// Picking a different model in Settings gets a fresh one.
    @MainActor
    static func shared(model: String) -> WhisperKitSpeechSource {
        if let cached, cached.model == model { return cached }
        let source = WhisperKitSpeechSource(model: model)
        cached = source
        return source
    }

    @MainActor private static var cached: WhisperKitSpeechSource?

    func start(heard: @escaping @Sendable (String) -> Void, status: @escaping @Sendable (String) -> Void) async throws {
        if whisperKit == nil {
            status("Loading the speech model. The first time, this downloads it…")
            whisperKit = try await WhisperKit(WhisperKitConfig(model: model))
        }
        guard let kit = whisperKit, let tokenizer = kit.tokenizer else {
            throw SpeechError.modelNotLoaded
        }

        let transcriber = AudioStreamTranscriber(
            audioEncoder: kit.audioEncoder,
            featureExtractor: kit.featureExtractor,
            segmentSeeker: kit.segmentSeeker,
            textDecoder: kit.textDecoder,
            tokenizer: tokenizer,
            audioProcessor: kit.audioProcessor,
            decodingOptions: DecodingOptions(),
            stateChangeCallback: { _, state in
                let segments = state.confirmedSegments + state.unconfirmedSegments
                heard(Self.spoken(segments.map(\.text).joined(separator: " ")))
            }
        )
        self.transcriber = transcriber
        status("")

        // Runs until stopped, so it cannot be awaited here.
        listening = Task {
            do {
                try await transcriber.startStreamTranscription()
            } catch {
                status(error.localizedDescription)
            }
        }
    }

    func stop() async {
        await transcriber?.stopStreamTranscription()
        listening?.cancel()
        listening = nil
        transcriber = nil
    }

    /// Whisper's segment text can carry special tokens such as `<|0.00|>` or `<|endoftext|>`.
    /// They are not speech, and their digits would read as spoken words to the follower.
    static func spoken(_ text: String) -> String {
        text.replacingOccurrences(of: "<\\|[^|]*\\|>", with: " ", options: .regularExpression)
    }
}

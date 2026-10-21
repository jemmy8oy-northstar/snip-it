import Observation
import ScriptFollower
import SwiftUI

/// One prompter run: the camera, the speech engine and the follower that joins them.
@MainActor
@Observable
final class PrompterSession {
    let lines: [String]
    let recorder = CameraRecorder()
    private(set) var currentLine = 0
    private(set) var isRecording = false
    /// Camera configured and running. Recording before then starts nothing.
    private(set) var isReady = false
    private(set) var status: String?
    /// The microphone's loudness, 0 to 1, and the last few words heard: the proof it is listening.
    private(set) var level: Float = 0
    private(set) var lastHeard = ""

    private var follower: ScriptFollower
    private let speech: any SpeechSource
    private let takes: TakeLibrary

    init(script: String, speech: any SpeechSource, takes: TakeLibrary) {
        // Read the lines off a local: @Observable turns `follower` into an accessor, which
        // cannot be read before every stored property is set.
        let follower = ScriptFollower(script: script)
        self.follower = follower
        lines = follower.lines
        self.speech = speech
        self.takes = takes
    }

    func start() async {
        do {
            try await recorder.configure()
            recorder.startRunning()
            isReady = true
            // Set here, not through `status:` below: that hops through a Task, so it could land
            // after, and hide, an error thrown straight back.
            status = "Preparing the speech model…"
            try await speech.start(
                heard: { [weak self] text in Task { @MainActor in self?.hear(text) } },
                level: { [weak self] level in Task { @MainActor in self?.level = level } },
                status: { [weak self] text in Task { @MainActor in self?.status = text.isEmpty ? nil : text } }
            )
        } catch {
            status = error.localizedDescription
        }
    }

    func stop() async {
        if isRecording {
            _ = try? await recorder.stopRecording()
            isRecording = false
        }
        await speech.stop()
        recorder.stopRunning()
    }

    func tokens(ofLine line: Int) -> [ScriptFollower.Token] {
        follower.tokens(ofLine: line)
    }

    func hear(_ text: String) {
        lastHeard = text.split(separator: " ").suffix(6).joined(separator: " ")
        if follower.hear(text) {
            currentLine = follower.currentLine
        }
    }

    /// A tap on a line: the way back for a retake, or forward past a skipped line.
    func move(to line: Int) {
        follower.move(toLine: line)
        currentLine = follower.currentLine
    }

    func toggleRecording() async {
        if isRecording {
            do {
                _ = try await recorder.stopRecording()
                status = "Saved. It's in Takes."
            } catch {
                status = error.localizedDescription
            }
            isRecording = false
        } else {
            status = nil
            recorder.startRecording(to: takes.newTakeURL())
            isRecording = true
        }
    }
}

struct PrompterView: View {
    @State private var session: PrompterSession
    @Environment(\.dismiss) private var dismiss

    init(script: String, model: String, takes: TakeLibrary) {
        _session = State(initialValue: PrompterSession(
            script: script, speech: WhisperKitSpeechSource(model: model), takes: takes))
    }

    var body: some View {
        ZStack {
            CameraPreview(recorder: session.recorder)
                .ignoresSafeArea()

            VStack(spacing: 0) {
                // The script sits at the top, nearest the front camera, so the eye line stays close to the lens.
                script
                    .frame(maxHeight: 320)
                    .background(.black.opacity(0.55))

                Spacer()

                if let status = session.status {
                    Text(status)
                        .font(.callout)
                        .padding(8)
                        .background(.black.opacity(0.6), in: RoundedRectangle(cornerRadius: 8))
                        .foregroundStyle(.white)
                        .padding(.bottom, 8)
                } else if session.isReady {
                    listening
                }

                controls
            }
        }
        .task { await session.start() }
        .onAppear { UIApplication.shared.isIdleTimerDisabled = true }
        .onDisappear {
            UIApplication.shared.isIdleTimerDisabled = false
            Task { await session.stop() }
        }
    }

    private var script: some View {
        ScrollViewReader { proxy in
            ScrollView {
                VStack(alignment: .leading, spacing: 18) {
                    ForEach(session.lines.indices, id: \.self) { index in
                        highlighted(line: index)
                            .font(.system(size: 30, weight: .semibold))
                            .frame(maxWidth: .infinity, alignment: .leading)
                            .id(index)
                            .onTapGesture { session.move(to: index) }
                    }
                }
                .padding()
            }
            .onChange(of: session.currentLine) { _, line in
                withAnimation(.easeOut(duration: 0.3)) { proxy.scrollTo(line, anchor: .top) }
            }
        }
    }

    /// The current line lights up word by word as it is heard (BEH-TELE-8); lines already read
    /// fade, and lines to come wait at middling brightness.
    private func highlighted(line: Int) -> Text {
        guard line == session.currentLine else {
            return Text(session.lines[line])
                .foregroundStyle(.white.opacity(line < session.currentLine ? 0.3 : 0.6))
        }
        return session.tokens(ofLine: line).enumerated().reduce(Text("")) { text, item in
            let (index, token) = item
            let word = Text((index == 0 ? "" : " ") + token.text)
                .foregroundStyle(token.heard ? Color.yellow : Color.white)
            return text + word
        }
    }

    /// Proof the prompter is listening (BEH-TELE-8): the microphone's level against the
    /// loudness below which the speech engine ignores you, and the last words it heard.
    private var listening: some View {
        VStack(alignment: .leading, spacing: 6) {
            GeometryReader { geo in
                ZStack(alignment: .leading) {
                    Capsule().fill(.white.opacity(0.2))
                    Capsule()
                        .fill(session.level >= WhisperKitSpeechSource.silenceThreshold ? Color.green : Color.white.opacity(0.6))
                        .frame(width: geo.size.width * CGFloat(min(max(session.level, 0), 1)))
                    Rectangle()
                        .fill(.white)
                        .frame(width: 2)
                        .offset(x: geo.size.width * CGFloat(WhisperKitSpeechSource.silenceThreshold))
                }
            }
            .frame(height: 6)
            .animation(.linear(duration: 0.1), value: session.level)
            .accessibilityLabel("Microphone level")

            Text(session.lastHeard.isEmpty ? "Listening…" : "Heard: \(session.lastHeard)")
                .font(.caption)
                .lineLimit(1)
                .truncationMode(.head)
                .foregroundStyle(.white.opacity(0.8))
        }
        .padding(10)
        .background(.black.opacity(0.5), in: RoundedRectangle(cornerRadius: 10))
        .padding(.horizontal, 24)
        .padding(.bottom, 8)
    }

    private var controls: some View {
        HStack {
            Button {
                dismiss()
            } label: {
                Image(systemName: "xmark")
                    .font(.title2)
                    .frame(width: 56, height: 56)
                    .background(.black.opacity(0.5), in: Circle())
            }
            .disabled(session.isRecording)

            Spacer()

            Button {
                Task { await session.toggleRecording() }
            } label: {
                RoundedRectangle(cornerRadius: session.isRecording ? 6 : 32)
                    .fill(.red)
                    .frame(width: session.isRecording ? 36 : 64, height: session.isRecording ? 36 : 64)
                    .frame(width: 80, height: 80)
                    .overlay(Circle().stroke(.white, lineWidth: 4))
            }
            .disabled(!session.isReady)
            .opacity(session.isReady ? 1 : 0.4)
            .accessibilityLabel(session.isRecording ? "Stop recording" : "Record")

            Spacer()

            Button {
                session.recorder.switchCamera()
            } label: {
                Image(systemName: "arrow.triangle.2.circlepath.camera")
                    .font(.title2)
                    .frame(width: 56, height: 56)
                    .background(.black.opacity(0.5), in: Circle())
            }
            .disabled(session.isRecording || !session.isReady)
            .accessibilityLabel("Switch camera")
        }
        .foregroundStyle(.white)
        .padding(.horizontal, 24)
        .padding(.bottom, 24)
    }
}

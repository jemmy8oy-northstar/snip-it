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

    private var follower: ScriptFollower
    private let speech: any SpeechSource

    init(script: String, speech: any SpeechSource) {
        // Read the lines off a local: @Observable turns `follower` into an accessor, which
        // cannot be read before every stored property is set.
        let follower = ScriptFollower(script: script)
        self.follower = follower
        lines = follower.lines
        self.speech = speech
    }

    func start() async {
        do {
            try await recorder.configure()
            recorder.startRunning()
            isReady = true
            try await speech.start(
                heard: { [weak self] text in Task { @MainActor in self?.hear(text) } },
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

    func hear(_ text: String) {
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
            recorder.startRecording(to: TakeLibrary.newTakeURL())
            isRecording = true
        }
    }
}

struct PrompterView: View {
    @State private var session: PrompterSession
    @Environment(\.dismiss) private var dismiss

    init(script: String, model: String) {
        _session = State(initialValue: PrompterSession(script: script, speech: WhisperKitSpeechSource.shared(model: model)))
    }

    var body: some View {
        ZStack {
            CameraPreview(session: session.recorder.session)
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
                    ForEach(Array(session.lines.enumerated()), id: \.offset) { index, line in
                        Text(line)
                            .font(.system(size: 30, weight: .semibold))
                            .foregroundStyle(.white.opacity(opacity(of: index)))
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

    private func opacity(of line: Int) -> Double {
        line == session.currentLine ? 1 : line < session.currentLine ? 0.3 : 0.6
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

            Color.clear.frame(width: 56, height: 56)
        }
        .foregroundStyle(.white)
        .padding(.horizontal, 24)
        .padding(.bottom, 24)
    }
}

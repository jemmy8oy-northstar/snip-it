import AVFoundation
import SwiftUI

enum RecorderError: LocalizedError {
    case denied
    case noCamera
    case notRecording

    var errorDescription: String? {
        switch self {
        case .denied: "Camera and microphone access are needed. Allow them in Settings."
        case .noCamera: "The front camera or the microphone is not available."
        case .notRecording: "The take did not record. Try again."
        }
    }
}

/// The front camera and the mic, recorded to a .mov. Every session call runs on one serial
/// queue, as AVFoundation asks.
final class CameraRecorder: NSObject, AVCaptureFileOutputRecordingDelegate, @unchecked Sendable {
    let session = AVCaptureSession()
    private let output = AVCaptureMovieFileOutput()
    private let queue = DispatchQueue(label: "camera")
    private var finished: CheckedContinuation<URL, Error>?
    private var configured = false

    func configure() async throws {
        guard await AVCaptureDevice.requestAccess(for: .video),
              await AVCaptureDevice.requestAccess(for: .audio) else {
            throw RecorderError.denied
        }

        // Set the shared audio session once, here, and stop the capture session changing it:
        // WhisperKit records from the same mic, and the capture session's own reconfiguration
        // is what usually cuts the other consumer off.
        let audio = AVAudioSession.sharedInstance()
        try audio.setCategory(.playAndRecord, mode: .videoRecording, options: [.defaultToSpeaker, .allowBluetooth])
        try audio.setActive(true)

        try await withCheckedThrowingContinuation { (done: CheckedContinuation<Void, Error>) in
            queue.async {
                do {
                    try self.setUp()
                    done.resume()
                } catch {
                    done.resume(throwing: error)
                }
            }
        }
    }

    private func setUp() throws {
        guard !configured else { return }
        session.beginConfiguration()
        defer { session.commitConfiguration() }

        session.automaticallyConfiguresApplicationAudioSession = false
        session.sessionPreset = .high

        guard let camera = AVCaptureDevice.default(.builtInWideAngleCamera, for: .video, position: .front),
              let mic = AVCaptureDevice.default(for: .audio) else {
            throw RecorderError.noCamera
        }
        let video = try AVCaptureDeviceInput(device: camera)
        let sound = try AVCaptureDeviceInput(device: mic)
        guard session.canAddInput(video), session.canAddInput(sound), session.canAddOutput(output) else {
            throw RecorderError.noCamera
        }
        session.addInput(video)
        session.addInput(sound)
        session.addOutput(output)
        // Without this a portrait take is saved landscape: the movie output records the
        // sensor's orientation unless its connection is told otherwise.
        output.connection(with: .video)?.videoRotationAngle = 90
        configured = true
    }

    func startRunning() {
        queue.async { self.session.startRunning() }
    }

    func stopRunning() {
        queue.async { self.session.stopRunning() }
    }

    func startRecording(to file: URL) {
        queue.async { self.output.startRecording(to: file, recordingDelegate: self) }
    }

    /// Returns the finished file once AVFoundation has closed it.
    func stopRecording() async throws -> URL {
        try await withCheckedThrowingContinuation { done in
            queue.async {
                // A recording that never started, or already failed, gets no second delegate
                // call, so waiting for one would hang the stop button forever.
                guard self.output.isRecording else {
                    done.resume(throwing: RecorderError.notRecording)
                    return
                }
                self.finished = done
                self.output.stopRecording()
            }
        }
    }

    func fileOutput(_ output: AVCaptureFileOutput, didFinishRecordingTo file: URL,
                    from connections: [AVCaptureConnection], error: Error?) {
        queue.async {
            guard let done = self.finished else { return }
            self.finished = nil
            // AVFoundation can report a recording that did finish with an error attached.
            let succeeded = (error as NSError?)?.userInfo[AVErrorRecordingSuccessfullyFinishedKey] as? Bool
            if let error, succeeded != true {
                done.resume(throwing: error)
            } else {
                done.resume(returning: file)
            }
        }
    }
}

struct CameraPreview: UIViewRepresentable {
    let session: AVCaptureSession

    func makeUIView(context: Context) -> PreviewView {
        let view = PreviewView()
        view.previewLayer.session = session
        view.previewLayer.videoGravity = .resizeAspectFill
        return view
    }

    func updateUIView(_ uiView: PreviewView, context: Context) {}

    final class PreviewView: UIView {
        override class var layerClass: AnyClass { AVCaptureVideoPreviewLayer.self }
        var previewLayer: AVCaptureVideoPreviewLayer { layer as! AVCaptureVideoPreviewLayer }
    }
}

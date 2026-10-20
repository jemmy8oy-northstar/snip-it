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
    private var video: AVCaptureDeviceInput?

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
        self.video = video
        uprightRecording()
        configured = true
    }

    /// Without this a portrait take is saved landscape: the movie output records the sensor's
    /// orientation unless its connection is told otherwise. A new camera means a new connection.
    private func uprightRecording() {
        output.connection(with: .video)?.videoRotationAngle = 90
    }

    // MARK: Camera controls (BEH-TELE-4)

    /// Front to back or back to front. Not while recording: the take would change camera mid-file.
    func switchCamera() {
        queue.async {
            guard let current = self.video, !self.output.isRecording else { return }
            let position: AVCaptureDevice.Position = current.device.position == .front ? .back : .front
            guard let camera = AVCaptureDevice.default(.builtInWideAngleCamera, for: .video, position: position),
                  let next = try? AVCaptureDeviceInput(device: camera) else { return }
            self.session.beginConfiguration()
            defer { self.session.commitConfiguration() }
            self.session.removeInput(current)
            if self.session.canAddInput(next) {
                self.session.addInput(next)
                self.video = next
            } else {
                self.session.addInput(current)
            }
            self.uprightRecording()
        }
    }

    /// Multiplies the zoom by `scale`, as a pinch does, within what the camera allows (capped at 5×,
    /// past which a selfie is only noise).
    func zoom(by scale: CGFloat) {
        queue.async {
            guard let device = self.video?.device, (try? device.lockForConfiguration()) != nil else { return }
            defer { device.unlockForConfiguration() }
            let upper = min(device.maxAvailableVideoZoomFactor, 5)
            device.videoZoomFactor = max(device.minAvailableVideoZoomFactor, min(device.videoZoomFactor * scale, upper))
        }
    }

    /// Focuses and exposes on a point in the camera's own coordinates, (0,0) to (1,1); the preview
    /// layer converts a tap into these.
    func focus(at point: CGPoint) {
        queue.async {
            guard let device = self.video?.device, (try? device.lockForConfiguration()) != nil else { return }
            defer { device.unlockForConfiguration() }
            if device.isFocusPointOfInterestSupported, device.isFocusModeSupported(.autoFocus) {
                device.focusPointOfInterest = point
                device.focusMode = .autoFocus
            }
            if device.isExposurePointOfInterestSupported, device.isExposureModeSupported(.autoExpose) {
                device.exposurePointOfInterest = point
                device.exposureMode = .autoExpose
            }
        }
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

/// The live camera. Pinch to zoom; tap to focus and expose there. The gestures live on the
/// UIKit view because only its preview layer can turn a screen point into a camera point.
struct CameraPreview: UIViewRepresentable {
    let recorder: CameraRecorder

    func makeUIView(context: Context) -> PreviewView {
        let view = PreviewView()
        view.recorder = recorder
        view.previewLayer.session = recorder.session
        view.previewLayer.videoGravity = .resizeAspectFill
        view.addGestureRecognizer(UITapGestureRecognizer(target: view, action: #selector(PreviewView.tapped(_:))))
        view.addGestureRecognizer(UIPinchGestureRecognizer(target: view, action: #selector(PreviewView.pinched(_:))))
        return view
    }

    func updateUIView(_ uiView: PreviewView, context: Context) {}

    final class PreviewView: UIView {
        weak var recorder: CameraRecorder?
        override class var layerClass: AnyClass { AVCaptureVideoPreviewLayer.self }
        var previewLayer: AVCaptureVideoPreviewLayer { layer as! AVCaptureVideoPreviewLayer }

        @objc func tapped(_ tap: UITapGestureRecognizer) {
            let point = tap.location(in: self)
            recorder?.focus(at: previewLayer.captureDevicePointConverted(fromLayerPoint: point))
            showFocus(at: point)
        }

        @objc func pinched(_ pinch: UIPinchGestureRecognizer) {
            guard pinch.state == .changed else { return }
            recorder?.zoom(by: pinch.scale)
            // Each callback then carries only the change since the last, which zoom(by:) multiplies in.
            pinch.scale = 1
        }

        /// A brief square where the tap landed, so the tap visibly did something.
        private func showFocus(at point: CGPoint) {
            let square = UIView(frame: CGRect(x: 0, y: 0, width: 72, height: 72))
            square.center = point
            square.layer.borderColor = UIColor.systemYellow.cgColor
            square.layer.borderWidth = 2
            square.isUserInteractionEnabled = false
            addSubview(square)
            UIView.animate(withDuration: 0.3, delay: 0.6, options: []) {
                square.alpha = 0
            } completion: { _ in
                square.removeFromSuperview()
            }
        }
    }
}

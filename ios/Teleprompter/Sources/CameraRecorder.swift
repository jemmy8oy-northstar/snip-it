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

        guard let camera = Self.camera(at: .front),
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

    /// The camera at `position`. At the front, the one with a square sensor if there is one
    /// (iPhone 17's Center Stage camera), so a take can be cropped either shape (BEH-TELE-5).
    private static func camera(at position: AVCaptureDevice.Position) -> AVCaptureDevice? {
        if #available(iOS 26, *), position == .front {
            let found = AVCaptureDevice.DiscoverySession(
                deviceTypes: [.builtInUltraWideCamera, .builtInWideAngleCamera], mediaType: .video, position: .front)
            if let square = found.devices.first(where: { !ShapeCrop.formats(of: $0).isEmpty }) {
                return square
            }
        }
        return AVCaptureDevice.default(.builtInWideAngleCamera, for: .video, position: position)
    }

    // MARK: Landscape with the phone upright (BEH-TELE-5)

    /// Whether this camera can switch between a portrait and a landscape take. Read once
    /// `configure` has returned, and after `switchCamera`.
    var canChangeShape: Bool {
        queue.sync {
            guard #available(iOS 26, *), let device = video?.device else { return false }
            return !ShapeCrop.formats(of: device).isEmpty
        }
    }

    /// Crops the square sensor to a landscape or portrait take, and reports which it got.
    ///
    /// Which of `.ratio16x9` and `.ratio9x16` gives a landscape FILE depends on whether the
    /// ratio names the sensor's buffer or the rotated output, and the documentation does not
    /// say. So this does not guess: it sets one, measures the buffer it produces, works out the
    /// shape after the connection's 90° turn, and tries the other ratio if that was wrong.
    /// Returns false when neither ratio gave the shape asked for, so the screen can say so.
    func setLandscape(_ landscape: Bool) async -> Bool {
        guard #available(iOS 26, *) else { return false }
        let (device, rotation, recording) = queue.sync {
            (video?.device, output.connection(with: .video)?.videoRotationAngle ?? 90, output.isRecording)
        }
        guard let device, !recording, let format = ShapeCrop.formats(of: device).first else { return false }
        for ratio in [AVCaptureDevice.AspectRatio.ratio9x16, .ratio16x9] {
            if await ShapeCrop.apply(ratio, format: format, to: device, rotation: rotation) == landscape {
                return true
            }
        }
        return false
    }

    /// Without this a portrait take is saved landscape: the movie output records the sensor's
    /// orientation unless its connection is told otherwise. A new camera means a new connection.
    private func uprightRecording() {
        output.connection(with: .video)?.videoRotationAngle = 90
    }

    // MARK: Camera controls (BEH-TELE-4)

    /// Whether the front camera is the one in use. Read after `configure` and `switchCamera`.
    var isFront: Bool {
        queue.sync { video?.device.position != .back }
    }

    /// Front to back or back to front. Not while recording: the take would change camera mid-file.
    func switchCamera() {
        queue.async {
            guard let current = self.video, !self.output.isRecording else { return }
            let position: AVCaptureDevice.Position = current.device.position == .front ? .back : .front
            guard let camera = Self.camera(at: position),
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

/// Cropping a square sensor to a shape (BEH-TELE-5). AVFoundation calls it dynamic aspect ratio.
@available(iOS 26, *)
enum ShapeCrop {
    /// Formats that can crop both ways at 30 fps, largest first but no bigger than 4K, since a
    /// prompter take does not need more and a bigger frame costs storage and heat.
    static func formats(of device: AVCaptureDevice) -> [AVCaptureDevice.Format] {
        device.formats
            .filter { format in
                let ratios = format.supportedDynamicAspectRatios
                let dims = CMVideoFormatDescriptionGetDimensions(format.formatDescription)
                return ratios.contains(.ratio16x9) && ratios.contains(.ratio9x16)
                    && max(dims.width, dims.height) <= 4096
                    && format.videoSupportedFrameRateRanges.contains { $0.maxFrameRate >= 30 }
            }
            .sorted {
                let a = CMVideoFormatDescriptionGetDimensions($0.formatDescription)
                let b = CMVideoFormatDescriptionGetDimensions($1.formatDescription)
                return Int(a.width) * Int(a.height) > Int(b.width) * Int(b.height)
            }
    }

    /// Sets `ratio` and returns whether the saved file will be wider than it is tall, measured
    /// from the buffer the camera now produces and the connection's rotation. Nil if it failed.
    static func apply(_ ratio: AVCaptureDevice.AspectRatio, format: AVCaptureDevice.Format,
                      to device: AVCaptureDevice, rotation: CGFloat) async -> Bool? {
        let applied: Bool = await withCheckedContinuation { done in
            do {
                try device.lockForConfiguration()
            } catch {
                done.resume(returning: false)
                return
            }
            if device.activeFormat != format {
                device.activeFormat = format
            }
            // The lock is needed to make the call, not until it completes.
            device.setDynamicAspectRatio(ratio) { _, error in done.resume(returning: error == nil) }
            device.unlockForConfiguration()
        }
        guard applied else { return nil }
        let dims = device.dynamicDimensions
        let turned = Int(rotation) % 180 != 0
        return turned ? dims.height > dims.width : dims.width > dims.height
    }
}

/// The live camera. Pinch to zoom; tap to focus and expose there. The gestures live on the
/// UIKit view because only its preview layer can turn a screen point into a camera point.
struct CameraPreview: UIViewRepresentable {
    let recorder: CameraRecorder
    /// A landscape take is shown whole, letterboxed, so the framing on screen is the framing saved.
    var landscape = false

    func makeUIView(context: Context) -> PreviewView {
        let view = PreviewView()
        view.recorder = recorder
        view.previewLayer.session = recorder.session
        view.previewLayer.videoGravity = .resizeAspectFill
        view.addGestureRecognizer(UITapGestureRecognizer(target: view, action: #selector(PreviewView.tapped(_:))))
        view.addGestureRecognizer(UIPinchGestureRecognizer(target: view, action: #selector(PreviewView.pinched(_:))))
        return view
    }

    func updateUIView(_ uiView: PreviewView, context: Context) {
        uiView.previewLayer.videoGravity = landscape ? .resizeAspect : .resizeAspectFill
    }

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

import Foundation
import Observation
@preconcurrency import WhisperKit

/// The speech models on this phone, and the one loaded engine the prompter listens through.
///
/// Downloading happens here, from Settings, and never from the prompter (snip-it Kit note
/// 2026-10-20 22:43Z: "model management in settings rather than it just trying to download when
/// you hit prompt"). Each model's state is visible, so a failure says which step failed and why.
@MainActor
@Observable
final class SpeechModels {
    static let shared = SpeechModels()

    enum Status: Equatable {
        case notDownloaded
        case downloading(Double)
        /// Loading, and the first time CoreML specialising it for this phone, which can take a minute.
        case preparing
        case ready
        case failed(String)
    }

    /// English-only models, smallest first. Smaller listens faster; bigger hears more accurately.
    static let variants = ["tiny.en", "base.en", "small.en"]

    /// Under Application Support rather than WhisperKit's default (Documents), so models do not
    /// show in the Files app, and in one place so a delete frees all of it.
    static let downloadBase = URL.applicationSupportDirectory.appending(path: "SpeechModels", directoryHint: .isDirectory)

    private(set) var statuses: [String: Status] = [:]

    private init() {
        // The first TestFlight builds downloaded to WhisperKit's default, Documents/huggingface,
        // which nothing reads any more. Takes live in Documents/Takes and are untouched.
        try? FileManager.default.removeItem(at: URL.documentsDirectory.appending(path: "huggingface"))
    }
    @ObservationIgnored private var engine: (variant: String, kit: WhisperKit)?
    @ObservationIgnored private var preparing: [String: Task<WhisperKit, Error>] = [:]

    /// Model folders by variant, relative to `downloadBase`: the app container's absolute path
    /// changes when the app updates, so an absolute one would point nowhere after the next build.
    private var folders: [String: String] {
        get { UserDefaults.standard.dictionary(forKey: SettingKey.speechModelFolders) as? [String: String] ?? [:] }
        set { UserDefaults.standard.set(newValue, forKey: SettingKey.speechModelFolders) }
    }

    /// The variants WhisperKit says this phone can run. If none of ours are in its list (it fell
    /// back to a built-in config, or names them differently), all of them are offered.
    var supported: [String] {
        let names = WhisperKit.recommendedModels().supported
        let ours = Self.variants.filter { variant in names.contains { $0.hasSuffix("whisper-\(variant)") } }
        return ours.isEmpty ? Self.variants : ours
    }

    func status(of variant: String) -> Status {
        if let status = statuses[variant] { return status }
        return folder(of: variant) == nil ? .notDownloaded : .ready
    }

    func isDownloaded(_ variant: String) -> Bool { folder(of: variant) != nil }

    private func folder(of variant: String) -> URL? {
        guard let stored = folders[variant] else { return nil }
        let url = stored.hasPrefix("/")
            ? URL(filePath: stored, directoryHint: .isDirectory)
            : Self.downloadBase.appending(path: stored, directoryHint: .isDirectory)
        return FileManager.default.fileExists(atPath: url.path) ? url : nil
    }

    /// Downloads the model, then loads it once so the slow first-time preparation happens here,
    /// with the user watching Settings, rather than when they open the prompter.
    func download(_ variant: String) async {
        statuses[variant] = .downloading(0)
        do {
            let folder = try await WhisperKit.download(variant: variant, downloadBase: Self.downloadBase) { progress in
                let fraction = progress.fractionCompleted
                Task { @MainActor in
                    if case .downloading = self.statuses[variant] { self.statuses[variant] = .downloading(fraction) }
                }
            }
            let base = Self.downloadBase.path + "/"
            folders[variant] = folder.path.hasPrefix(base) ? String(folder.path.dropFirst(base.count)) : folder.path
        } catch {
            statuses[variant] = .failed("The download failed: \(error.localizedDescription)")
            return
        }
        do {
            _ = try await engine(for: variant)
        } catch {
            // engine(for:) records the failure.
        }
    }

    func delete(_ variant: String) {
        if engine?.variant == variant { engine = nil }
        if let folder = folder(of: variant) { try? FileManager.default.removeItem(at: folder) }
        folders[variant] = nil
        statuses[variant] = nil
    }

    /// The loaded engine for a downloaded model. Throws rather than downloads when it is missing.
    func engine(for variant: String) async throws -> WhisperKit {
        if let engine, engine.variant == variant { return engine.kit }
        guard let folder = folder(of: variant) else { throw SpeechError.notDownloaded(variant) }
        if let running = preparing[variant] { return try await running.value }

        statuses[variant] = .preparing
        let task = Task {
            // `load: true` matters: given only a folder or a model name, WhisperKit leaves the
            // models unloaded until its own transcribe() runs, and the streaming transcriber this
            // app uses never calls that. It is why the first TestFlight build never heard a word.
            try await WhisperKit(WhisperKitConfig(
                modelFolder: folder.path,
                tokenizerFolder: Self.downloadBase,
                verbose: false,
                prewarm: true,
                load: true,
                download: false))
        }
        preparing[variant] = task
        defer { preparing[variant] = nil }
        do {
            let kit = try await task.value
            guard kit.tokenizer != nil else { throw SpeechError.loadFailed("its tokenizer did not load") }
            engine = (variant, kit)
            statuses[variant] = .ready
            return kit
        } catch let error as SpeechError {
            statuses[variant] = .failed(error.localizedDescription)
            throw error
        } catch {
            let failure = SpeechError.loadFailed(error.localizedDescription)
            statuses[variant] = .failed(failure.localizedDescription)
            throw failure
        }
    }
}

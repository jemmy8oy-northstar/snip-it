import AVKit
import Photos
import Productions
import SnipItClient
import SwiftUI

struct Take: Identifiable, Hashable {
    let url: URL
    let date: Date
    var id: URL { url }
    var isCut: Bool { url.deletingPathExtension().lastPathComponent.hasSuffix(TakeLibrary.cutSuffix) }
    var coverage: TakeCoverage? { TakeCoverage.read(for: url, cutSuffix: TakeLibrary.cutSuffix) }
}

/// A production's takes are the files in its Takes directory; the directory is the whole database.
struct TakeLibrary {
    static let cutSuffix = "-cut"

    /// Where takes lived before productions (BEH-PROD-1): `ProductionStore.adopt` moves them out.
    static let legacyDirectory = URL.documentsDirectory.appending(path: "Takes", directoryHint: .isDirectory)

    let directory: URL

    func newTakeURL() -> URL {
        try? FileManager.default.createDirectory(at: directory, withIntermediateDirectories: true)
        return directory.appending(path: "take-\(Int(Date.now.timeIntervalSince1970)).mov")
    }

    func cutURL(for take: URL) -> URL {
        directory.appending(path: take.deletingPathExtension().lastPathComponent + Self.cutSuffix + ".mp4")
    }

    func all() -> [Take] {
        let files = (try? FileManager.default.contentsOfDirectory(
            at: directory, includingPropertiesForKeys: [.creationDateKey])) ?? []
        // Videos only: a take's coverage file sits beside it.
        return files
            .filter { ["mov", "mp4"].contains($0.pathExtension.lowercased()) }
            .map { Take(url: $0, date: (try? $0.resourceValues(forKeys: [.creationDateKey]).creationDate) ?? .distantPast) }
            .sorted { $0.date > $1.date }
    }

    /// Every take's coverage, once per original: a cut shares its original's.
    func coverage() -> [TakeCoverage] {
        all().filter { !$0.isCut }.compactMap(\.coverage)
    }

    func delete(_ take: Take) {
        try? FileManager.default.removeItem(at: take.url)
        if !take.isCut {
            try? FileManager.default.removeItem(at: TakeCoverage.url(for: take.url, cutSuffix: Self.cutSuffix))
        }
    }
}

/// One production's takes. It sits inside that production's screen, so it has no navigation of its own.
struct TakesView: View {
    let library: TakeLibrary
    @AppStorage(SettingKey.snipItURL) private var snipItURL = "https://balenthiran.co.uk/snipit/"
    @AppStorage(SettingKey.snipItUser) private var snipItUser = ""
    @AppStorage(SettingKey.maxPause) private var maxPause = 0.6
    @AppStorage(SettingKey.studio) private var studio = false
    @State private var takes: [Take] = []
    @State private var working: URL?
    @State private var message: String?
    @State private var playing: Take?
    @State private var deleting: Take?

    var body: some View {
        List {
            if takes.isEmpty {
                Text("Takes you record for this production appear here.").foregroundStyle(.secondary)
            }
            ForEach(takes) { take in
                row(take)
            }
        }
        .refreshable { takes = library.all() }
        .onAppear { takes = library.all() }
        .fullScreenCover(item: $playing) { take in
            TakePlayer(url: take.url)
        }
        // Every delete asks first (snip-it.notes.md#2026-10-21-0005).
        .confirmationDialog("Delete this take?",
                            isPresented: Binding(get: { deleting != nil }, set: { if !$0 { deleting = nil } }),
                            titleVisibility: .visible, presenting: deleting) { take in
            Button("Delete take", role: .destructive) {
                library.delete(take)
                takes = library.all()
            }
        }
        .alert("Takes", isPresented: Binding(get: { message != nil }, set: { if !$0 { message = nil } })) {
            Button("OK") { message = nil }
        } message: {
            Text(message ?? "")
        }
    }

    private func row(_ take: Take) -> some View {
        HStack {
            Button {
                playing = take
            } label: {
                HStack {
                    Image(systemName: "play.circle")
                        .font(.title2)
                    VStack(alignment: .leading) {
                        Text(take.date.formatted(date: .abbreviated, time: .shortened))
                        Text(take.isCut ? "Pauses cut" : "Original")
                            .font(.caption)
                            .foregroundStyle(.secondary)
                        if let lines = take.coverage?.lines, let first = lines.first {
                            Text("\(lines.count == 1 ? "1 line" : "\(lines.count) lines"), from “\(first)”")
                                .font(.caption)
                                .foregroundStyle(.secondary)
                                .lineLimit(1)
                        }
                    }
                }
            }
            .buttonStyle(.plain)
            .accessibilityHint("Plays the take")
            Spacer()
            if working == take.url {
                ProgressView()
            } else {
                Menu {
                    // Cutting sends the take to snip-it, so only Studio offers it (BEH-EDITION-2).
                    if studio && !take.isCut {
                        Button("Cut pauses over \(maxPause.formatted(.number.precision(.fractionLength(1))))s",
                               systemImage: "scissors") {
                            Task { await cutPauses(take) }
                        }
                    }
                    Button("Save to Photos", systemImage: "square.and.arrow.down") {
                        Task { await saveToPhotos(take) }
                    }
                    ShareLink(item: take.url)
                } label: {
                    Image(systemName: "ellipsis.circle").font(.title2)
                }
            }
        }
        .swipeActions {
            // Not `role: .destructive`: that removes the row before the confirmation is answered.
            Button("Delete", systemImage: "trash") { deleting = take }
                .tint(.red)
        }
    }

    private func cutPauses(_ take: Take) async {
        guard studio else { return }
        guard let base = URL(string: snipItURL) else {
            message = "The snip-it address in Settings is not a URL."
            return
        }
        working = take.url
        defer { working = nil }
        let client = SnipItClient(
            baseURL: base,
            username: snipItUser.isEmpty ? nil : snipItUser,
            password: Keychain.password)
        do {
            let cut = try await client.cutPauses(of: take.url, maxPauseSeconds: maxPause)
            let destination = library.cutURL(for: take.url)
            try? FileManager.default.removeItem(at: destination)
            try FileManager.default.moveItem(at: cut, to: destination)
            takes = library.all()
        } catch let error as SnipItError {
            message = Self.describe(error)
        } catch {
            message = error.localizedDescription
        }
    }

    private func saveToPhotos(_ take: Take) async {
        guard await PHPhotoLibrary.requestAuthorization(for: .addOnly) == .authorized else {
            message = "Allow adding to Photos in Settings to save takes there."
            return
        }
        do {
            try await PHPhotoLibrary.shared().performChanges {
                _ = PHAssetChangeRequest.creationRequestForAssetFromVideo(atFileURL: take.url)
            }
            message = "Saved to Photos."
        } catch {
            message = error.localizedDescription
        }
    }

    static func describe(_ error: SnipItError) -> String {
        switch error {
        case .previewLimitReached(let reason): reason
        case .rejected(401, _): "snip-it refused the username or password in Settings."
        case .rejected(let status, let body): "snip-it answered \(status). \(body)"
        case .jobFailed(let reason): "snip-it could not process the take: \(reason)"
        case .timedOut: "snip-it took too long. Try again in a minute."
        case .noDownload: "snip-it finished but sent no video back."
        }
    }
}

/// A take played full screen with the system player (BEH-TELE-3), so scrubbing, AirPlay and
/// picture in picture come for free.
struct TakePlayer: View {
    @State private var player: AVPlayer
    @Environment(\.dismiss) private var dismiss

    init(url: URL) {
        _player = State(initialValue: AVPlayer(url: url))
    }

    var body: some View {
        // Done sits in a bar above the video: an overlay would cover the player's own corner controls.
        NavigationStack {
            VideoPlayer(player: player)
                .background(.black)
                .toolbar {
                    Button("Done") { dismiss() }
                }
                .toolbarBackground(.visible, for: .navigationBar)
        }
        .onAppear { player.play() }
        .onDisappear { player.pause() }
    }
}

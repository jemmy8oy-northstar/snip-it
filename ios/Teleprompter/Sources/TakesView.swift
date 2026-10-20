import AVKit
import Photos
import SnipItClient
import SwiftUI

struct Take: Identifiable, Hashable {
    let url: URL
    let date: Date
    var id: URL { url }
    var isCut: Bool { url.deletingPathExtension().lastPathComponent.hasSuffix(TakeLibrary.cutSuffix) }
}

/// Takes are files in Documents/Takes; the directory is the whole database.
enum TakeLibrary {
    static let cutSuffix = "-cut"

    static var directory: URL {
        let takes = URL.documentsDirectory.appending(path: "Takes", directoryHint: .isDirectory)
        try? FileManager.default.createDirectory(at: takes, withIntermediateDirectories: true)
        return takes
    }

    static func newTakeURL() -> URL {
        directory.appending(path: "take-\(Int(Date.now.timeIntervalSince1970)).mov")
    }

    static func cutURL(for take: URL) -> URL {
        directory.appending(path: take.deletingPathExtension().lastPathComponent + cutSuffix + ".mp4")
    }

    static func all() -> [Take] {
        let files = (try? FileManager.default.contentsOfDirectory(
            at: directory, includingPropertiesForKeys: [.creationDateKey])) ?? []
        return files
            .map { Take(url: $0, date: (try? $0.resourceValues(forKeys: [.creationDateKey]).creationDate) ?? .distantPast) }
            .sorted { $0.date > $1.date }
    }
}

struct TakesView: View {
    @AppStorage(SettingKey.snipItURL) private var snipItURL = "https://balenthiran.co.uk/snipit/"
    @AppStorage(SettingKey.snipItUser) private var snipItUser = ""
    @AppStorage(SettingKey.maxPause) private var maxPause = 0.6
    @State private var takes: [Take] = []
    @State private var working: URL?
    @State private var message: String?
    @State private var playing: Take?

    var body: some View {
        NavigationStack {
            List {
                if takes.isEmpty {
                    Text("Takes you record appear here.").foregroundStyle(.secondary)
                }
                ForEach(takes) { take in
                    row(take)
                }
            }
            .navigationTitle("Takes")
            .refreshable { takes = TakeLibrary.all() }
            .onAppear { takes = TakeLibrary.all() }
            .fullScreenCover(item: $playing) { take in
                TakePlayer(url: take.url)
            }
            .alert("Takes", isPresented: Binding(get: { message != nil }, set: { if !$0 { message = nil } })) {
                Button("OK") { message = nil }
            } message: {
                Text(message ?? "")
            }
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
                    if !take.isCut {
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
            Button("Delete", systemImage: "trash", role: .destructive) {
                try? FileManager.default.removeItem(at: take.url)
                takes = TakeLibrary.all()
            }
        }
    }

    private func cutPauses(_ take: Take) async {
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
            let destination = TakeLibrary.cutURL(for: take.url)
            try? FileManager.default.removeItem(at: destination)
            try FileManager.default.moveItem(at: cut, to: destination)
            takes = TakeLibrary.all()
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
        VideoPlayer(player: player)
            .ignoresSafeArea()
            .background(.black)
            .overlay(alignment: .topLeading) {
                Button {
                    dismiss()
                } label: {
                    Image(systemName: "xmark")
                        .font(.title2)
                        .foregroundStyle(.white)
                        .frame(width: 44, height: 44)
                        .background(.black.opacity(0.5), in: Circle())
                }
                .padding()
                .accessibilityLabel("Close")
            }
            .onAppear { player.play() }
            .onDisappear { player.pause() }
    }
}

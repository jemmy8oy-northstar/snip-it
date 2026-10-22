import Foundation

/// What was said in a take, transcribed on the phone (BEH-EDITION-4): the words the prompter's
/// speech engine heard between Record and Stop. It comes from the same on-device engine that
/// moves the script, so it works with Studio off (BEH-EDITION-2) and needs no second pass.
public struct TakeTranscript: Codable, Equatable, Sendable {
    public let text: String

    public init(text: String) {
        self.text = text
    }

    /// Stored beside the take, as its coverage is: `take-1.mov` has `take-1.transcript.json`, and
    /// a cut shares its original's, because cutting pauses removes no words.
    public static func url(for take: URL, cutSuffix: String) -> URL {
        TakeSidecar.url(for: take, cutSuffix: cutSuffix, kind: "transcript")
    }

    public static func read(for take: URL, cutSuffix: String) -> TakeTranscript? {
        (try? Data(contentsOf: url(for: take, cutSuffix: cutSuffix)))
            .flatMap { try? JSONDecoder().decode(TakeTranscript.self, from: $0) }
    }

    public func write(for take: URL, cutSuffix: String) throws {
        try JSONEncoder().encode(self).write(to: Self.url(for: take, cutSuffix: cutSuffix), options: .atomic)
    }
}

/// Collects a take's transcript while it records. The engine reports everything heard since it
/// started listening, revised as it goes, and listening starts before Record. So the take's words
/// are what follows the longest run of words it shares with what had been heard at Record.
///
/// Words are matched without case or punctuation, because the engine re-punctuates as it revises.
/// A word it revises right at the boundary can still land in the take; that is the price of not
/// restarting the engine at every Record.
public struct TranscriptRecorder: Sendable {
    private let before: [String]
    private var latest: [String]

    public init(heardBefore: String) {
        before = Self.words(heardBefore)
        latest = before
    }

    public mutating func heard(_ text: String) {
        latest = Self.words(text)
    }

    public func transcript() -> TakeTranscript {
        var shared = 0
        while shared < min(before.count, latest.count), Self.key(before[shared]) == Self.key(latest[shared]) {
            shared += 1
        }
        return TakeTranscript(text: latest[shared...].joined(separator: " "))
    }

    private static func words(_ text: String) -> [String] {
        text.split(whereSeparator: \.isWhitespace).map(String.init)
    }

    private static func key(_ word: String) -> String {
        String(word.lowercased().filter { $0.isLetter || $0.isNumber })
    }
}

/// The files that sit beside a take: `take-1.mov` has `take-1.<kind>.json`, and its cut
/// (`take-1-cut.mp4`) shares them.
enum TakeSidecar {
    static func url(for take: URL, cutSuffix: String, kind: String) -> URL {
        var name = take.deletingPathExtension().lastPathComponent
        if name.hasSuffix(cutSuffix) {
            name.removeLast(cutSuffix.count)
        }
        return take.deletingLastPathComponent().appending(path: "\(name).\(kind).json")
    }
}

import Foundation

/// Follows a speaker through a script, from what a speech engine hears.
///
/// The rules, each one a test:
/// - It only moves FORWARD on its own. Saying an earlier line again is a retake
///   or an ad-lib, not an instruction to scroll back. `move(toLine:)` is the way back.
/// - Silence and ad-libs leave it where it is. That gives "wait for you as you
///   pause" for free: nothing heard, nothing moves.
/// - The next expected word moves it on its own evidence, because that is where
///   it already is. Anything further ahead needs at least two heard words matching
///   unread script words in order, so one common word ("the") cannot jump it.
/// - It looks ahead only `lookaheadLines` lines past the current one, so a phrase
///   that recurs later in the script cannot pull it there.
///
/// Feed it the latest text heard, as often as the engine revises it. Hearing the
/// same text twice does not move it twice, because only unread script words count
/// as evidence.
public struct ScriptFollower {
    public let lines: [String]

    /// Index into the script's words of the next word expected.
    public private(set) var cursor = 0

    private let words: [String]
    private let lineOfWord: [Int]
    private let window: Int
    private let lookaheadLines: Int
    private let threshold: Double

    public init(script: String, window: Int = 6, lookaheadLines: Int = 2, threshold: Double = 0.5) {
        var lines: [String] = []
        var words: [String] = []
        var lineOfWord: [Int] = []
        for raw in script.split(whereSeparator: \.isNewline) {
            let lineWords = Words.of(String(raw))
            if lineWords.isEmpty {
                continue
            }
            for w in lineWords {
                words.append(w)
                lineOfWord.append(lines.count)
            }
            lines.append(String(raw).trimmingCharacters(in: .whitespaces))
        }
        self.lines = lines
        self.words = words
        self.lineOfWord = lineOfWord
        self.window = window
        self.lookaheadLines = lookaheadLines
        self.threshold = threshold
    }

    /// The line the speaker is on: the line of the next word expected, or the last
    /// line once the script is finished.
    public var currentLine: Int {
        cursor < words.count ? lineOfWord[cursor] : max(lines.count - 1, 0)
    }

    public var isFinished: Bool { cursor >= words.count }

    /// Takes what the speech engine heard. Returns whether the cursor moved.
    @discardableResult
    public mutating func hear(_ text: String) -> Bool {
        let heard = Array(Words.of(text).suffix(window))
        guard let last = heard.last, cursor < words.count else {
            return false
        }

        // Nearest first: when a phrase recurs inside the lookahead, the earlier one wins.
        for end in (cursor + 1)...limit() where Words.same(last, words[end - 1]) {
            let start = max(cursor, end - window)
            let matched = anchoredMatches(heard, Array(words[start..<end]))
            let needed = end == cursor + 1
                ? 1
                : max(2, Int((threshold * Double(min(heard.count, end - start))).rounded(.up)))
            if matched >= needed {
                cursor = end
                return true
            }
        }
        return false
    }

    /// Moves to the start of a line: the speaker tapped it, or went back for a retake.
    public mutating func move(toLine line: Int) {
        cursor = lineOfWord.firstIndex(where: { $0 >= line }) ?? words.count
    }

    /// One past the last word the follower may move to: the end of the line
    /// `lookaheadLines` after the current one.
    private func limit() -> Int {
        let lastLine = currentLine + lookaheadLines
        return lineOfWord.firstIndex(where: { $0 > lastLine }) ?? words.count
    }

    /// Words of `heard` matching `script` in order, with both last words matched to
    /// each other: a longest common subsequence anchored at the end.
    private func anchoredMatches(_ heard: [String], _ script: [String]) -> Int {
        let a = heard.dropLast()
        let b = script.dropLast()
        var previous = Array(repeating: 0, count: b.count + 1)
        for x in a {
            var row = [0]
            for (j, y) in b.enumerated() {
                row.append(Words.same(x, y) ? previous[j] + 1 : max(previous[j + 1], row[j]))
            }
            previous = row
        }
        return previous[b.count] + 1
    }
}

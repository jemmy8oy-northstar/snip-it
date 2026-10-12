import Foundation

/// Turns text, whether a script line or something a speech engine heard, into
/// comparable words. Case, accents and punctuation are not what was said, so they
/// are dropped on both sides. An apostrophe is dropped rather than split on, so
/// "don't" and "dont" are the same word.
public enum Words {
    public static func of(_ text: String) -> [String] {
        let folded = text.lowercased().decomposedStringWithCanonicalMapping
        var words: [String] = []
        var current = ""
        for scalar in folded.unicodeScalars {
            if scalar.properties.generalCategory == .nonspacingMark || scalar == "'" || scalar == "\u{2019}" {
                continue
            }
            if scalar.properties.isAlphabetic || scalar.properties.numericType != nil {
                current.unicodeScalars.append(scalar)
            } else if !current.isEmpty {
                words.append(current)
                current = ""
            }
        }
        if !current.isEmpty {
            words.append(current)
        }
        return words
    }

    /// Two words are the same if they are equal, or, for words long enough that one
    /// slip is not a different word, one edit apart: speech engines drop plurals and
    /// mishear a letter. Short words must match exactly, or "a" would follow "I".
    static func same(_ a: String, _ b: String) -> Bool {
        if a == b {
            return true
        }
        guard min(a.count, b.count) >= 4, abs(a.count - b.count) <= 1 else {
            return false
        }
        return distance(Array(a), Array(b)) <= 1
    }

    private static func distance(_ a: [Character], _ b: [Character]) -> Int {
        var previous = Array(0...b.count)
        for i in 1...a.count {
            var row = [i] + Array(repeating: 0, count: b.count)
            for j in 1...b.count {
                row[j] = a[i - 1] == b[j - 1]
                    ? previous[j - 1]
                    : 1 + min(previous[j - 1], previous[j], row[j - 1])
            }
            previous = row
        }
        return previous[b.count]
    }
}

import Foundation

/// A stretch of script between two markers (BEH-SCRIPT-1). Sections are for planning: the
/// prompter reads straight through them, because `ScriptFollower` skips every marker line.
public struct ScriptSection: Equatable, Sendable {
    /// From a `# Name` line; nil for a `---` break, or for the text before the first marker.
    public var name: String?
    /// The section's lines, without its marker and without blank lines at either end.
    public var body: String

    public init(name: String? = nil, body: String = "") {
        self.name = name
        self.body = body
    }

    /// Lines the prompter will show: the non-blank ones, without the spaces around them.
    public var lines: [String] {
        body.split(whereSeparator: \.isNewline)
            .map { $0.trimmingCharacters(in: .whitespaces) }
            .filter { !$0.isEmpty }
    }

    public var lineCount: Int { lines.count }
}

/// Markdown's markers, borrowed: `# Name` starts a named section and `---` an unnamed one. The
/// script is still stored as plain text, so pasting one in keeps working.
public enum ScriptSections {
    /// `---`, and the dashes iOS autocorrects it to: "—-" is an em dash and a hyphen. An em or
    /// en dash counts as two hyphens, and three are needed, so a lone "—" stays a script line.
    public static func isBreak(_ line: some StringProtocol) -> Bool {
        let trimmed = line.trimmingCharacters(in: .whitespaces)
        guard !trimmed.isEmpty, trimmed.allSatisfy({ "-—–".contains($0) }) else {
            return false
        }
        return trimmed.reduce(0) { $0 + ($1 == "-" ? 1 : 2) } >= 3
    }

    /// The section's name if the line is a `#` heading, with every leading `#` dropped. As in
    /// markdown, the `#`s must be followed by a space or nothing, so "#1 tip" is a script line.
    public static func heading(_ line: some StringProtocol) -> String? {
        let trimmed = line.trimmingCharacters(in: .whitespaces)
        let rest = trimmed.drop(while: { $0 == "#" })
        guard rest.count < trimmed.count, rest.first.map(\.isWhitespace) ?? true else {
            return nil
        }
        return rest.trimmingCharacters(in: .whitespaces)
    }

    public static func isMarker(_ line: some StringProtocol) -> Bool {
        heading(line) != nil || isBreak(line)
    }

    public static func parse(_ script: String) -> [ScriptSection] {
        var sections: [ScriptSection] = []
        var name: String?
        var lines: [Substring] = []
        var started = false

        func close() {
            let body = trimBlankEnds(lines).joined(separator: "\n")
            // Text before the first marker only becomes a section if there is some.
            if started || !body.isEmpty {
                sections.append(ScriptSection(name: name, body: body))
            }
        }

        // On newlines, not on "\n": Swift reads "\r\n" as one Character, which is not "\n".
        for line in script.split(omittingEmptySubsequences: false, whereSeparator: \.isNewline) {
            if let heading = heading(line) {
                close()
                name = heading.isEmpty ? nil : heading
            } else if isBreak(line) {
                close()
                name = nil
            } else {
                lines.append(line)
                continue
            }
            lines = []
            started = true
        }
        close()
        return sections
    }

    /// The inverse of `parse`: a named section is written under `# Name`, and an unnamed one
    /// after the first is written under `---`.
    public static func join(_ sections: [ScriptSection]) -> String {
        sections.enumerated().map { index, section in
            let marker: String? = section.name.map { "# \($0)" } ?? (index == 0 ? nil : "---")
            return [marker, section.body.isEmpty ? nil : section.body].compactMap { $0 }.joined(separator: "\n")
        }
        .filter { !$0.isEmpty }
        .joined(separator: "\n\n")
    }

    private static func trimBlankEnds(_ lines: [Substring]) -> ArraySlice<Substring> {
        let blank: (Substring) -> Bool = { $0.allSatisfy(\.isWhitespace) }
        guard let first = lines.firstIndex(where: { !blank($0) }),
              let last = lines.lastIndex(where: { !blank($0) }) else {
            return []
        }
        return lines[first...last]
    }
}

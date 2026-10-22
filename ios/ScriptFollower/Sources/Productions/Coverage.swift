import Foundation

/// Which script lines a take covered (BEH-SCRIPT-4): the lines the prompter was on between
/// Record and Stop, whether it got there by hearing or by a tap. Kept as the lines' text, not
/// their positions, so editing the script above a line does not move its coverage onto another.
public struct TakeCoverage: Codable, Equatable, Sendable {
    public let lines: [String]

    public init(lines: [String]) {
        self.lines = lines
    }

    /// Stored beside the take: `take-1.mov` has `take-1.coverage.json`. A cut of the take
    /// (`take-1-cut.mp4`) shares its original's, because cutting pauses removes no lines.
    public static func url(for take: URL, cutSuffix: String) -> URL {
        TakeSidecar.url(for: take, cutSuffix: cutSuffix, kind: "coverage")
    }

    public static func read(for take: URL, cutSuffix: String) -> TakeCoverage? {
        (try? Data(contentsOf: url(for: take, cutSuffix: cutSuffix)))
            .flatMap { try? JSONDecoder().decode(TakeCoverage.self, from: $0) }
    }

    public func write(for take: URL, cutSuffix: String) throws {
        try JSONEncoder().encode(self).write(to: Self.url(for: take, cutSuffix: cutSuffix), options: .atomic)
    }

    /// How many of `takes` covered `line`. Lines are compared without the spaces around them.
    public static func count(of line: String, in takes: [TakeCoverage]) -> Int {
        let key = line.trimmingCharacters(in: .whitespaces)
        guard !key.isEmpty else {
            return 0
        }
        return takes.filter { take in take.lines.contains { $0.trimmingCharacters(in: .whitespaces) == key } }.count
    }
}

/// Collects a take's coverage while it records: the line it started on and every line it
/// moved to, in order, each once.
public struct CoverageRecorder: Sendable {
    public private(set) var lines: [Int] = []

    public init(startingAt line: Int) {
        lines = [line]
    }

    public mutating func moved(to line: Int) {
        if !lines.contains(line) {
            lines.append(line)
        }
    }

    public func coverage(of script: [String]) -> TakeCoverage {
        TakeCoverage(lines: lines.filter { script.indices.contains($0) }.map { script[$0] })
    }
}

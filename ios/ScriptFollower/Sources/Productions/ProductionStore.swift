import Foundation

/// One video being made: a script and every take recorded against it (BEH-PROD-1). Called a
/// production, not a video, because "videos might get confused with takes".
public struct Production: Identifiable, Hashable, Codable, Sendable {
    public let id: String
    public var name: String
    public let created: Date

    public init(id: String, name: String, created: Date) {
        self.id = id
        self.name = name
        self.created = created
    }
}

/// Productions are directories under `root`, one each, holding `production.json`, `script.txt`
/// and a `Takes` directory. As with takes before them, the file system is the whole database.
public struct ProductionStore: Sendable {
    public static let firstProductionName = "My first production"

    public let root: URL

    public init(root: URL) {
        self.root = root
    }

    /// Every production, newest first. A directory without a readable `production.json` is not one.
    public func all() -> [Production] {
        let dirs = (try? FileManager.default.contentsOfDirectory(at: root, includingPropertiesForKeys: nil)) ?? []
        return dirs
            .compactMap { dir in
                (try? Data(contentsOf: dir.appending(path: "production.json")))
                    .flatMap { try? Self.decoder.decode(Production.self, from: $0) }
            }
            .sorted { $0.created == $1.created ? $0.id > $1.id : $0.created > $1.created }
    }

    /// A blank name is still a production; it is called "Untitled" rather than refused.
    @discardableResult
    public func create(named name: String, at date: Date = .now) throws -> Production {
        let production = Production(id: UUID().uuidString, name: Self.tidy(name), created: date)
        try FileManager.default.createDirectory(at: takes(of: production.id), withIntermediateDirectories: true)
        try save(production)
        return production
    }

    public func rename(_ production: Production, to name: String) throws -> Production {
        var renamed = production
        renamed.name = Self.tidy(name)
        try save(renamed)
        return renamed
    }

    /// Deletes the production and its takes. The confirmation is the screen's job.
    public func delete(_ production: Production) throws {
        try FileManager.default.removeItem(at: directory(of: production.id))
    }

    public func script(of production: Production) -> String {
        (try? String(contentsOf: directory(of: production.id).appending(path: "script.txt"), encoding: .utf8)) ?? ""
    }

    public func setScript(_ script: String, of production: Production) throws {
        try script.write(to: directory(of: production.id).appending(path: "script.txt"), atomically: true, encoding: .utf8)
    }

    /// Where a production's takes are recorded. It exists once the production does.
    public func takes(of id: String) -> URL {
        directory(of: id).appending(path: "Takes", directoryHint: .isDirectory)
    }

    /// The app before productions kept one script (in settings) and one `Takes` directory. The
    /// first time the store is empty and either holds anything, both become "My first
    /// production", so nothing on the phone is lost. Returns it, or nil when there was nothing
    /// to move or productions already exist. The caller clears the old script afterwards.
    @discardableResult
    public func adopt(script: String, takes legacy: URL) throws -> Production? {
        guard all().isEmpty else {
            return nil
        }
        let files = (try? FileManager.default.contentsOfDirectory(at: legacy, includingPropertiesForKeys: nil)) ?? []
        guard !files.isEmpty || !script.trimmingCharacters(in: .whitespacesAndNewlines).isEmpty else {
            return nil
        }
        let production = try create(named: Self.firstProductionName)
        try setScript(script, of: production)
        for file in files {
            try FileManager.default.moveItem(at: file, to: takes(of: production.id).appending(path: file.lastPathComponent))
        }
        return production
    }

    private func directory(of id: String) -> URL {
        root.appending(path: id, directoryHint: .isDirectory)
    }

    private func save(_ production: Production) throws {
        try Self.encoder.encode(production).write(to: directory(of: production.id).appending(path: "production.json"), options: .atomic)
    }

    private static func tidy(_ name: String) -> String {
        let trimmed = name.trimmingCharacters(in: .whitespacesAndNewlines)
        return trimmed.isEmpty ? "Untitled" : trimmed
    }

    // Computed, not stored: a shared coder is mutable state, which Swift 6 refuses in a static.
    private static var encoder: JSONEncoder {
        let e = JSONEncoder()
        e.dateEncodingStrategy = .iso8601
        return e
    }

    private static var decoder: JSONDecoder {
        let d = JSONDecoder()
        d.dateDecodingStrategy = .iso8601
        return d
    }
}

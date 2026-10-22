import Foundation

/// The API's job states. The server sends enum NAMES, not ordinals (SnipitJsonOptions).
public enum JobStatus: String, Codable, Sendable {
    case pending = "Pending"
    case processing = "Processing"
    case completed = "Completed"
    case failed = "Failed"
}

/// A transcription or cut job. `downloadUrl` is only ever set on a completed cut,
/// and it is a path on the API's host (`/snipit/api/cuts/{id}/download`), not a full URL.
public struct Job: Codable, Sendable, Equatable {
    public let id: UUID
    public let status: JobStatus
    public let error: String?
    public let downloadUrl: String?

    public init(id: UUID, status: JobStatus, error: String? = nil, downloadUrl: String? = nil) {
        self.id = id
        self.status = status
        self.error = error
        self.downloadUrl = downloadUrl
    }
}

/// One transcribed word, in seconds, with whether it survives the cut.
public struct TranscriptWord: Codable, Sendable, Equatable {
    public var text: String
    public var start: Double
    public var end: Double
    public var kept: Bool

    public init(text: String, start: Double, end: Double, kept: Bool = true) {
        self.text = text
        self.start = start
        self.end = end
        self.kept = kept
    }
}

/// A take kept on snip-it's server. `name` is the take's file name on the phone, which is what
/// makes "is this take saved?" a lookup. `savedAt` stays the server's ISO 8601 text: it carries
/// fractional seconds, which the standard decoding strategy refuses.
public struct SavedTake: Codable, Sendable, Equatable {
    public let name: String
    public let bytes: Int64
    public let savedAt: String

    public init(name: String, bytes: Int64, savedAt: String) {
        self.name = name
        self.bytes = bytes
        self.savedAt = savedAt
    }
}

struct Transcript: Decodable {
    let words: [TranscriptWord]
}

struct CutRequest: Encodable {
    let transcriptionJobId: UUID
    let words: [TranscriptWord]
    let maxPauseSeconds: Double?
}

struct PreviewLimitReached: Decodable {
    let message: String
}

public enum SnipItError: Error, Equatable {
    /// 429: snip-it's daily preview quota is spent. The message is the server's, for the user.
    case previewLimitReached(String)
    /// Any other non-2xx answer.
    case rejected(status: Int, message: String)
    /// The job ran and failed on the server.
    case jobFailed(String)
    /// The job was still running after the client stopped waiting.
    case timedOut
    /// A completed cut came back with no download link.
    case noDownload
}

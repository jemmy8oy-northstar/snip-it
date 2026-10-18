import Foundation

/// The teleprompter's half of snip-it's API: send a take, have its pauses cut, get
/// the cut back.
///
/// snip-it keeps no copy of a video between requests (#22), so the take is sent
/// twice: once to be transcribed, and again alongside the word list for the cut.
/// That is the cost of the server holding nothing, and on a phone the file is
/// already on disk.
public struct SnipItClient: Sendable {
    /// Where the app is served, e.g. `https://balenthiran.co.uk/snipit/`. The API is `api/` under it.
    public let baseURL: URL

    private let transport: any HTTPTransport
    private let authorization: String?
    private let pollInterval: Duration
    private let maxPolls: Int
    private let sleep: @Sendable (Duration) async throws -> Void

    /// - Parameters:
    ///   - username/password: snip-it sits behind basic auth at the ingress (snip-it#13).
    ///   - pollInterval/maxPolls: how long to wait on a job; the default gives up after ten minutes.
    public init(
        baseURL: URL,
        username: String? = nil,
        password: String? = nil,
        transport: any HTTPTransport = URLSessionTransport(),
        pollInterval: Duration = .seconds(2),
        maxPolls: Int = 300,
        sleep: @escaping @Sendable (Duration) async throws -> Void = { try await Task.sleep(for: $0) }
    ) {
        // Relative paths resolve against the last slash: without one, "api/..." would replace "snipit".
        let base = baseURL.absoluteString
        self.baseURL = base.hasSuffix("/") ? baseURL : URL(string: base + "/") ?? baseURL
        if let username, let password {
            authorization = "Basic " + Data("\(username):\(password)".utf8).base64EncodedString()
        } else {
            authorization = nil
        }
        self.transport = transport
        self.pollInterval = pollInterval
        self.maxPolls = maxPolls
        self.sleep = sleep
    }

    // MARK: - The whole take

    /// Transcribes a take, cuts every pause longer than `maxPauseSeconds` (and nothing
    /// else), and returns the cut mp4 as a file the caller owns.
    public func cutPauses(of video: URL, maxPauseSeconds: Double) async throws -> URL {
        let submitted = try await transcribe(video)
        let transcribed = try await waitUntilDone(submitted) { try await transcriptionJob($0) }
        let words = try await transcript(transcribed.id)
        let requested = try await cut(video, transcriptionJobId: transcribed.id, words: words, maxPauseSeconds: maxPauseSeconds)
        let done = try await waitUntilDone(requested) { try await cutJob($0) }
        return try await download(done)
    }

    // MARK: - One route each

    public func transcribe(_ video: URL) async throws -> Job {
        try await upload(to: "api/transcriptions", fields: [], video: video)
    }

    public func transcriptionJob(_ id: UUID) async throws -> Job {
        try await get("api/transcriptions/\(Self.path(id))")
    }

    public func transcript(_ id: UUID) async throws -> [TranscriptWord] {
        let transcript: Transcript = try await get("api/transcriptions/\(Self.path(id))/transcript")
        return transcript.words
    }

    public func cut(
        _ video: URL, transcriptionJobId: UUID, words: [TranscriptWord], maxPauseSeconds: Double?
    ) async throws -> Job {
        let request = CutRequest(transcriptionJobId: transcriptionJobId, words: words, maxPauseSeconds: maxPauseSeconds)
        let json = String(decoding: try JSONEncoder().encode(request), as: UTF8.self)
        return try await upload(to: "api/cuts", fields: [(name: "request", value: json)], video: video)
    }

    public func cutJob(_ id: UUID) async throws -> Job {
        try await get("api/cuts/\(Self.path(id))")
    }

    /// The server deletes a cut as it streams it, so this works once per cut.
    public func download(_ cut: Job) async throws -> URL {
        guard let path = cut.downloadUrl, let url = URL(string: path, relativeTo: baseURL)?.absoluteURL else {
            throw SnipItError.noDownload
        }
        let (status, file) = try await transport.download(request("GET", url))
        guard (200..<300).contains(status) else {
            let body = (try? Data(contentsOf: file)) ?? Data()
            try? FileManager.default.removeItem(at: file)
            throw Self.error(status: status, body: body)
        }
        return file
    }

    // MARK: - Plumbing

    /// Polls until the job finishes. A job that is already finished is not fetched again.
    func waitUntilDone(_ job: Job, fetch: (UUID) async throws -> Job) async throws -> Job {
        var job = job
        var polls = 0
        while true {
            switch job.status {
            case .completed:
                return job
            case .failed:
                throw SnipItError.jobFailed(job.error ?? "The job failed without saying why.")
            case .pending, .processing:
                guard polls < maxPolls else { throw SnipItError.timedOut }
                polls += 1
                try await sleep(pollInterval)
                job = try await fetch(job.id)
            }
        }
    }

    private func get<T: Decodable>(_ path: String) async throws -> T {
        let response = try await transport.send(request("GET", baseURL.appendingPathComponent(path)))
        return try Self.decode(response)
    }

    private func upload(to path: String, fields: [(name: String, value: String)], video: URL) async throws -> Job {
        let multipart = Multipart()
        let body = try multipart.write(fields: fields, fileField: "file", file: video)
        defer { try? FileManager.default.removeItem(at: body) }

        var post = request("POST", baseURL.appendingPathComponent(path), body: .file(body))
        post.headers["Content-Type"] = multipart.contentType
        return try Self.decode(try await transport.send(post))
    }

    private func request(_ method: String, _ url: URL, body: RequestBody = .none) -> HTTPRequest {
        var headers: [String: String] = [:]
        if let authorization {
            headers["Authorization"] = authorization
        }
        return HTTPRequest(method: method, url: url, headers: headers, body: body)
    }

    private static func decode<T: Decodable>(_ response: (status: Int, body: Data)) throws -> T {
        guard (200..<300).contains(response.status) else {
            throw error(status: response.status, body: response.body)
        }
        return try JSONDecoder().decode(T.self, from: response.body)
    }

    private static func error(status: Int, body: Data) -> SnipItError {
        if status == 429, let limit = try? JSONDecoder().decode(PreviewLimitReached.self, from: body) {
            return .previewLimitReached(limit.message)
        }
        return .rejected(status: status, message: String(decoding: body, as: UTF8.self))
    }

    /// .NET writes Guids lowercase; Foundation writes them uppercase. Routes match either,
    /// but lowercase keeps a URL identical to the one the server handed out.
    private static func path(_ id: UUID) -> String {
        id.uuidString.lowercased()
    }
}

import Foundation
#if canImport(FoundationNetworking)
import FoundationNetworking
#endif

public enum RequestBody: Sendable, Equatable {
    case none
    case data(Data)
    /// Streamed from disk. A take is hundreds of megabytes, so it never goes through memory.
    case file(URL)
}

public struct HTTPRequest: Sendable, Equatable {
    public var method: String
    public var url: URL
    public var headers: [String: String]
    public var body: RequestBody

    public init(method: String, url: URL, headers: [String: String] = [:], body: RequestBody = .none) {
        self.method = method
        self.url = url
        self.headers = headers
        self.body = body
    }

    var urlRequest: URLRequest {
        var request = URLRequest(url: url)
        request.httpMethod = method
        for (name, value) in headers {
            request.setValue(value, forHTTPHeaderField: name)
        }
        return request
    }
}

/// The one seam between the client and the network, so the client is tested without one.
public protocol HTTPTransport: Sendable {
    func send(_ request: HTTPRequest) async throws -> (status: Int, body: Data)
    /// Saves the response body to a file the caller owns.
    func download(_ request: HTTPRequest) async throws -> (status: Int, file: URL)
}

public struct URLSessionTransport: HTTPTransport {
    private let session: URLSession

    public init(session: URLSession = .shared) {
        self.session = session
    }

    public func send(_ request: HTTPRequest) async throws -> (status: Int, body: Data) {
        let urlRequest = request.urlRequest
        return try await withCheckedThrowingContinuation { continuation in
            let done: @Sendable (Data?, URLResponse?, Error?) -> Void = { data, response, error in
                if let error {
                    continuation.resume(throwing: error)
                } else {
                    continuation.resume(returning: (Self.status(of: response), data ?? Data()))
                }
            }
            let task: URLSessionTask
            switch request.body {
            case .none:
                task = session.dataTask(with: urlRequest, completionHandler: done)
            case .data(let data):
                task = session.uploadTask(with: urlRequest, from: data, completionHandler: done)
            case .file(let file):
                task = session.uploadTask(with: urlRequest, fromFile: file, completionHandler: done)
            }
            task.resume()
        }
    }

    public func download(_ request: HTTPRequest) async throws -> (status: Int, file: URL) {
        let urlRequest = request.urlRequest
        return try await withCheckedThrowingContinuation { continuation in
            let task = session.downloadTask(with: urlRequest) { location, response, error in
                if let error {
                    continuation.resume(throwing: error)
                    return
                }
                guard let location else {
                    continuation.resume(throwing: URLError(.badServerResponse))
                    return
                }
                // The system deletes `location` as soon as this handler returns.
                let kept = FileManager.default.temporaryDirectory
                    .appendingPathComponent(UUID().uuidString)
                    .appendingPathExtension("mp4")
                do {
                    try FileManager.default.moveItem(at: location, to: kept)
                    continuation.resume(returning: (Self.status(of: response), kept))
                } catch {
                    continuation.resume(throwing: error)
                }
            }
            task.resume()
        }
    }

    private static func status(of response: URLResponse?) -> Int {
        (response as? HTTPURLResponse)?.statusCode ?? 0
    }
}

import Foundation
import XCTest
@testable import SnipItClient

/// Answers requests from a script of canned responses, and records what was sent,
/// including the bytes of any uploaded body (read at send time, because the client
/// deletes its temporary body file straight after).
actor FakeTransport: HTTPTransport {
    struct Sent {
        let request: HTTPRequest
        let body: Data
    }

    private var answers: [(status: Int, body: Data)]
    private(set) var sent: [Sent] = []

    init(_ answers: [(Int, String)]) {
        self.answers = answers.map { (status: $0.0, body: Data($0.1.utf8)) }
    }

    func send(_ request: HTTPRequest) async throws -> (status: Int, body: Data) {
        try record(request)
        return answers.removeFirst()
    }

    func download(_ request: HTTPRequest) async throws -> (status: Int, file: URL) {
        try record(request)
        let answer = answers.removeFirst()
        let file = FileManager.default.temporaryDirectory.appendingPathComponent("fake-\(UUID().uuidString)")
        try answer.body.write(to: file)
        return (answer.status, file)
    }

    private func record(_ request: HTTPRequest) throws {
        let body: Data
        switch request.body {
        case .none: body = Data()
        case .data(let data): body = data
        case .file(let file): body = try Data(contentsOf: file)
        }
        sent.append(Sent(request: request, body: body))
    }
}

final class SnipItClientTests: XCTestCase {
    private let base = URL(string: "https://balenthiran.co.uk/snipit")!
    private let transcriptionId = UUID(uuidString: "11111111-1111-1111-1111-111111111111")!
    private let cutId = UUID(uuidString: "22222222-2222-2222-2222-222222222222")!
    private var video: URL!

    override func setUpWithError() throws {
        video = FileManager.default.temporaryDirectory.appendingPathComponent("take-\(UUID().uuidString).mov")
        try Data("not really a video".utf8).write(to: video)
    }

    override func tearDownWithError() throws {
        try? FileManager.default.removeItem(at: video)
    }

    private func client(_ transport: FakeTransport, maxPolls: Int = 10) -> SnipItClient {
        SnipItClient(
            baseURL: base, username: "james", password: "secret",
            transport: transport, maxPolls: maxPolls, sleep: { _ in })
    }

    private func job(_ id: UUID, _ status: String, error: String? = nil, download: String? = nil) -> String {
        let err = error.map { "\"\($0)\"" } ?? "null"
        let dl = download.map { ",\"downloadUrl\":\"\($0)\"" } ?? ""
        return "{\"id\":\"\(id.uuidString.lowercased())\",\"status\":\"\(status)\",\"error\":\(err),\"createdAt\":\"2026-10-13T10:00:00Z\"\(dl)}"
    }

    private let transcript = """
        {"transcriptionJobId":"11111111-1111-1111-1111-111111111111","durationSeconds":4.0,"segments":[],
         "words":[{"text":"Hello","start":0.0,"end":0.4,"kept":true},{"text":"world","start":3.0,"end":3.5,"kept":true}]}
        """

    func testATakeIsTranscribedThenCutThenDownloaded() async throws {
        let transport = FakeTransport([
            (200, job(transcriptionId, "Pending")),
            (200, job(transcriptionId, "Processing")),
            (200, job(transcriptionId, "Completed")),
            (200, transcript),
            (200, job(cutId, "Pending")),
            (200, job(cutId, "Completed", download: "/snipit/api/cuts/\(cutId.uuidString.lowercased())/download")),
            (200, "the cut mp4"),
        ])

        let file = try await client(transport).cutPauses(of: video, maxPauseSeconds: 0.75)
        defer { try? FileManager.default.removeItem(at: file) }

        XCTAssertEqual(try String(contentsOf: file, encoding: .utf8), "the cut mp4")
        let sent = await transport.sent
        XCTAssertEqual(sent.map { "\($0.request.method) \($0.request.url.absoluteString)" }, [
            "POST https://balenthiran.co.uk/snipit/api/transcriptions",
            "GET https://balenthiran.co.uk/snipit/api/transcriptions/11111111-1111-1111-1111-111111111111",
            "GET https://balenthiran.co.uk/snipit/api/transcriptions/11111111-1111-1111-1111-111111111111",
            "GET https://balenthiran.co.uk/snipit/api/transcriptions/11111111-1111-1111-1111-111111111111/transcript",
            "POST https://balenthiran.co.uk/snipit/api/cuts",
            "GET https://balenthiran.co.uk/snipit/api/cuts/22222222-2222-2222-2222-222222222222",
            "GET https://balenthiran.co.uk/snipit/api/cuts/22222222-2222-2222-2222-222222222222/download",
        ])
    }

    func testEveryRequestCarriesBasicAuth() async throws {
        let transport = FakeTransport([(200, job(transcriptionId, "Completed"))])
        _ = try await client(transport).transcriptionJob(transcriptionId)

        let sent = await transport.sent
        // base64("james:secret")
        XCTAssertEqual(sent[0].request.headers["Authorization"], "Basic amFtZXM6c2VjcmV0")
    }

    func testNoCredentialsMeansNoAuthHeader() async throws {
        let transport = FakeTransport([(200, job(transcriptionId, "Completed"))])
        _ = try await SnipItClient(baseURL: base, transport: transport).transcriptionJob(transcriptionId)

        let sent = await transport.sent
        XCTAssertNil(sent[0].request.headers["Authorization"])
    }

    func testABaseWithATrailingSlashResolvesTheSame() async throws {
        let transport = FakeTransport([(200, job(transcriptionId, "Completed"))])
        let slashed = SnipItClient(baseURL: URL(string: "https://balenthiran.co.uk/snipit/")!, transport: transport)
        _ = try await slashed.transcriptionJob(transcriptionId)

        let sent = await transport.sent
        XCTAssertEqual(sent[0].request.url.absoluteString,
                       "https://balenthiran.co.uk/snipit/api/transcriptions/11111111-1111-1111-1111-111111111111")
    }

    func testTheTakeIsUploadedAsTheFileFieldWithItsBytesAndName() async throws {
        let transport = FakeTransport([(200, job(transcriptionId, "Pending"))])
        _ = try await client(transport).transcribe(video)

        let sent = await transport.sent
        let contentType = try XCTUnwrap(sent[0].request.headers["Content-Type"])
        XCTAssertTrue(contentType.hasPrefix("multipart/form-data; boundary="))
        let boundary = String(contentType.dropFirst("multipart/form-data; boundary=".count))
        let body = String(decoding: sent[0].body, as: UTF8.self)

        XCTAssertEqual(body, """
            --\(boundary)\r
            Content-Disposition: form-data; name="file"; filename="\(video.lastPathComponent)"\r
            Content-Type: video/quicktime\r
            \r
            not really a video\r
            --\(boundary)--\r

            """)
    }

    func testTheCutSendsEveryWordKeptAndTheMaxPauseAsTheRequestField() async throws {
        let transport = FakeTransport([(200, job(cutId, "Pending"))])
        let words = [TranscriptWord(text: "Hello", start: 0, end: 0.4), TranscriptWord(text: "world", start: 3, end: 3.5)]
        _ = try await client(transport).cut(video, transcriptionJobId: transcriptionId, words: words, maxPauseSeconds: 0.75)

        let sent = await transport.sent
        let body = String(decoding: sent[0].body, as: UTF8.self)
        let field = try XCTUnwrap(body.components(separatedBy: "name=\"request\"\r\n\r\n").dropFirst().first?
            .components(separatedBy: "\r\n").first)
        let json = try XCTUnwrap(try JSONSerialization.jsonObject(with: Data(field.utf8)) as? [String: Any])

        XCTAssertEqual((json["transcriptionJobId"] as? String)?.lowercased(), transcriptionId.uuidString.lowercased())
        XCTAssertEqual(json["maxPauseSeconds"] as? Double, 0.75)
        let sentWords = try XCTUnwrap(json["words"] as? [[String: Any]])
        XCTAssertEqual(sentWords.map { $0["text"] as? String }, ["Hello", "world"])
        XCTAssertEqual(sentWords.map { $0["kept"] as? Bool }, [true, true])
        XCTAssertTrue(body.contains("name=\"file\"; filename=\"\(video.lastPathComponent)\""))
    }

    func testTheUploadBodyIsNotLeftOnDisk() async throws {
        let before = try Self.uploadBodies()
        let transport = FakeTransport([(500, "boom")])
        _ = try? await client(transport).transcribe(video)

        XCTAssertEqual(try Self.uploadBodies(), before)
    }

    func testAnUnsetMaxPauseIsLeftOutSoTheServerKeepsEveryPause() async throws {
        let request = CutRequest(transcriptionJobId: transcriptionId, words: [], maxPauseSeconds: nil)
        let json = String(decoding: try JSONEncoder().encode(request), as: UTF8.self)
        XCTAssertFalse(json.contains("maxPauseSeconds"), json)
    }

    func testASpentQuotaSurfacesTheServersMessage() async throws {
        let transport = FakeTransport([(429, "{\"message\":\"Today's previews are used up.\"}")])
        do {
            _ = try await client(transport).transcribe(video)
            XCTFail("expected a throw")
        } catch {
            XCTAssertEqual(error as? SnipItError, .previewLimitReached("Today's previews are used up."))
        }
    }

    func testAnyOtherRefusalCarriesItsStatusAndBody() async throws {
        let transport = FakeTransport([(400, "No file uploaded.")])
        do {
            _ = try await client(transport).transcribe(video)
            XCTFail("expected a throw")
        } catch {
            XCTAssertEqual(error as? SnipItError, .rejected(status: 400, message: "No file uploaded."))
        }
    }

    func testAFailedJobStopsWithTheServersReason() async throws {
        let transport = FakeTransport([
            (200, job(transcriptionId, "Pending")),
            (200, job(transcriptionId, "Failed", error: "Groq was down")),
        ])
        do {
            _ = try await client(transport).cutPauses(of: video, maxPauseSeconds: 1)
            XCTFail("expected a throw")
        } catch {
            XCTAssertEqual(error as? SnipItError, .jobFailed("Groq was down"))
        }
    }

    func testWaitingGivesUpAfterMaxPolls() async throws {
        let transport = FakeTransport([
            (200, job(transcriptionId, "Pending")),
            (200, job(transcriptionId, "Processing")),
            (200, job(transcriptionId, "Processing")),
        ])
        do {
            _ = try await client(transport, maxPolls: 2).cutPauses(of: video, maxPauseSeconds: 1)
            XCTFail("expected a throw")
        } catch {
            XCTAssertEqual(error as? SnipItError, .timedOut)
        }
        let sent = await transport.sent
        XCTAssertEqual(sent.count, 3, "one submit and exactly maxPolls polls")
    }

    func testACompletedCutWithNoLinkIsAnError() async throws {
        let transport = FakeTransport([])
        do {
            _ = try await client(transport).download(Job(id: cutId, status: .completed))
            XCTFail("expected a throw")
        } catch {
            XCTAssertEqual(error as? SnipItError, .noDownload)
        }
    }

    func testAGoneCutIsAnError() async throws {
        // The server deletes a cut as it streams it, so a second download is a 404.
        let transport = FakeTransport([(404, "")])
        do {
            _ = try await client(transport).download(Job(id: cutId, status: .completed, downloadUrl: "/snipit/api/cuts/x/download"))
            XCTFail("expected a throw")
        } catch {
            XCTAssertEqual(error as? SnipItError, .rejected(status: 404, message: ""))
        }
    }

    private static func uploadBodies() throws -> Set<String> {
        Set(try FileManager.default.contentsOfDirectory(atPath: FileManager.default.temporaryDirectory.path)
            .filter { $0.hasPrefix("snipit-upload-") })
    }
}

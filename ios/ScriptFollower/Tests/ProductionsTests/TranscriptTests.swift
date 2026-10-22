import Foundation
import XCTest
@testable import Productions

/// BEH-EDITION-4: a take shows its transcript, made on the phone.
final class TranscriptTests: XCTestCase {
    func testATakeKeepsOnlyWhatWasHeardAfterRecord() {
        var recorder = TranscriptRecorder(heardBefore: "testing one two")
        recorder.heard("testing one two hello and welcome")
        recorder.heard("testing one two hello and welcome to the show")
        XCTAssertEqual(recorder.transcript().text, "hello and welcome to the show")
    }

    func testRepunctuatingWhatCameBeforeRecordDoesNotLeakItIntoTheTake() {
        var recorder = TranscriptRecorder(heardBefore: "Testing one two")
        recorder.heard("testing, one, two. Hello and welcome.")
        XCTAssertEqual(recorder.transcript().text, "Hello and welcome.")
    }

    func testATakeRecordedBeforeAnythingWasHeardKeepsEverything() {
        var recorder = TranscriptRecorder(heardBefore: "")
        recorder.heard("Hello and welcome.")
        XCTAssertEqual(recorder.transcript().text, "Hello and welcome.")
    }

    func testATakeWithNothingSaidIsEmptyNotTheWordsBeforeIt() {
        let recorder = TranscriptRecorder(heardBefore: "testing one two")
        XCTAssertEqual(recorder.transcript().text, "")
    }

    func testAnEngineThatStartedOverKeepsTheNewWords() {
        var recorder = TranscriptRecorder(heardBefore: "testing one two")
        recorder.heard("today we build")
        XCTAssertEqual(recorder.transcript().text, "today we build")
    }

    func testACutSharesItsOriginalsTranscript() {
        let dir = URL(fileURLWithPath: "/tmp/takes")
        XCTAssertEqual(TakeTranscript.url(for: dir.appending(path: "take-1.mov"), cutSuffix: "-cut").lastPathComponent,
                       "take-1.transcript.json")
        XCTAssertEqual(TakeTranscript.url(for: dir.appending(path: "take-1-cut.mp4"), cutSuffix: "-cut").lastPathComponent,
                       "take-1.transcript.json")
    }

    func testTheTranscriptRoundTripsBesideTheTake() throws {
        let dir = FileManager.default.temporaryDirectory.appending(path: "transcript-\(UUID().uuidString)")
        try FileManager.default.createDirectory(at: dir, withIntermediateDirectories: true)
        defer { try? FileManager.default.removeItem(at: dir) }
        let take = dir.appending(path: "take-1.mov")
        XCTAssertNil(TakeTranscript.read(for: take, cutSuffix: "-cut"))
        try TakeTranscript(text: "hello there").write(for: take, cutSuffix: "-cut")
        XCTAssertEqual(TakeTranscript.read(for: dir.appending(path: "take-1-cut.mp4"), cutSuffix: "-cut")?.text, "hello there")
    }
}

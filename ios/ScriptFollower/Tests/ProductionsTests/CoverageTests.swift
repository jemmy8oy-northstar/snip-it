import Foundation
import XCTest
@testable import Productions

final class CoverageTests: XCTestCase {
    private let script = ["Hello and welcome.", "Today we build.", "Thanks for watching."]

    func testATakeCoversItsStartLineAndEveryLineItMovedTo() {
        var recorder = CoverageRecorder(startingAt: 1)
        recorder.moved(to: 2)
        XCTAssertEqual(recorder.coverage(of: script).lines, ["Today we build.", "Thanks for watching."])
    }

    func testALineIsCoveredOnceHoweverOftenItIsVisited() {
        var recorder = CoverageRecorder(startingAt: 0)
        recorder.moved(to: 1)
        recorder.moved(to: 0)
        recorder.moved(to: 1)
        XCTAssertEqual(recorder.coverage(of: script).lines, ["Hello and welcome.", "Today we build."])
    }

    func testALineOutsideTheScriptIsIgnored() {
        XCTAssertEqual(CoverageRecorder(startingAt: 9).coverage(of: script).lines, [])
    }

    func testCountingIsByTextIgnoringSurroundingSpaces() {
        let takes = [
            TakeCoverage(lines: ["Hello and welcome.", "Today we build."]),
            TakeCoverage(lines: ["  Today we build. "]),
        ]
        XCTAssertEqual(TakeCoverage.count(of: "Today we build.", in: takes), 2)
        XCTAssertEqual(TakeCoverage.count(of: "Hello and welcome.", in: takes), 1)
        XCTAssertEqual(TakeCoverage.count(of: "Thanks for watching.", in: takes), 0)
        XCTAssertEqual(TakeCoverage.count(of: "  ", in: [TakeCoverage(lines: [" "])]), 0)
    }

    func testACutSharesItsOriginalsCoverage() {
        let dir = URL(fileURLWithPath: "/tmp/takes")
        XCTAssertEqual(TakeCoverage.url(for: dir.appending(path: "take-1.mov"), cutSuffix: "-cut").lastPathComponent,
                       "take-1.coverage.json")
        XCTAssertEqual(TakeCoverage.url(for: dir.appending(path: "take-1-cut.mp4"), cutSuffix: "-cut").lastPathComponent,
                       "take-1.coverage.json")
    }

    func testCoverageRoundTripsBesideTheTake() throws {
        let dir = FileManager.default.temporaryDirectory.appending(path: "coverage-\(UUID().uuidString)")
        try FileManager.default.createDirectory(at: dir, withIntermediateDirectories: true)
        defer { try? FileManager.default.removeItem(at: dir) }
        let take = dir.appending(path: "take-1.mov")
        XCTAssertNil(TakeCoverage.read(for: take, cutSuffix: "-cut"))
        try TakeCoverage(lines: ["a", "b"]).write(for: take, cutSuffix: "-cut")
        XCTAssertEqual(TakeCoverage.read(for: dir.appending(path: "take-1-cut.mp4"), cutSuffix: "-cut")?.lines, ["a", "b"])
    }
}

import Foundation
import XCTest
@testable import Productions

final class ProductionStoreTests: XCTestCase {
    private var root: URL!
    private var store: ProductionStore!

    override func setUpWithError() throws {
        root = FileManager.default.temporaryDirectory.appending(path: "productions-\(UUID().uuidString)")
        try FileManager.default.createDirectory(at: root, withIntermediateDirectories: true)
        store = ProductionStore(root: root.appending(path: "Productions"))
    }

    override func tearDownWithError() throws {
        try? FileManager.default.removeItem(at: root)
    }

    private func file(_ name: String, in dir: URL) throws {
        try FileManager.default.createDirectory(at: dir, withIntermediateDirectories: true)
        try Data("take".utf8).write(to: dir.appending(path: name))
    }

    private func files(in dir: URL) -> [String] {
        ((try? FileManager.default.contentsOfDirectory(atPath: dir.path)) ?? []).sorted()
    }

    func testAnEmptyStoreHasNoProductions() {
        XCTAssertEqual(store.all(), [])
    }

    func testProductionsAreListedNewestFirst() throws {
        try store.create(named: "Older", at: Date(timeIntervalSince1970: 1_000))
        try store.create(named: "Newer", at: Date(timeIntervalSince1970: 2_000))
        XCTAssertEqual(store.all().map(\.name), ["Newer", "Older"])
    }

    func testABlankNameIsUntitledAndANameIsTrimmed() throws {
        XCTAssertEqual(try store.create(named: "   ").name, "Untitled")
        XCTAssertEqual(try store.create(named: "  Launch video \n").name, "Launch video")
    }

    func testEachProductionKeepsItsOwnScript() throws {
        let a = try store.create(named: "A")
        let b = try store.create(named: "B")
        try store.setScript("line one\nline two", of: a)
        XCTAssertEqual(store.script(of: a), "line one\nline two")
        XCTAssertEqual(store.script(of: b), "")
    }

    func testEachProductionHasItsOwnTakesDirectory() throws {
        let a = try store.create(named: "A")
        let b = try store.create(named: "B")
        XCTAssertNotEqual(store.takes(of: a.id), store.takes(of: b.id))
        try file("take-1.mov", in: store.takes(of: a.id))
        XCTAssertEqual(files(in: store.takes(of: a.id)), ["take-1.mov"])
        XCTAssertEqual(files(in: store.takes(of: b.id)), [])
    }

    func testRenamingKeepsTheScriptAndTakes() throws {
        let a = try store.create(named: "A")
        try store.setScript("hello", of: a)
        try file("take-1.mov", in: store.takes(of: a.id))
        let renamed = try store.rename(a, to: "Renamed")
        XCTAssertEqual(store.all().map(\.name), ["Renamed"])
        XCTAssertEqual(renamed.id, a.id)
        XCTAssertEqual(store.script(of: renamed), "hello")
        XCTAssertEqual(files(in: store.takes(of: renamed.id)), ["take-1.mov"])
    }

    func testDeletingRemovesTheProductionAndItsTakes() throws {
        let a = try store.create(named: "A")
        let b = try store.create(named: "B")
        try file("take-1.mov", in: store.takes(of: a.id))
        try store.delete(a)
        XCTAssertEqual(store.all().map(\.id), [b.id])
        XCTAssertFalse(FileManager.default.fileExists(atPath: store.takes(of: a.id).path))
    }

    func testADirectoryWithoutItsManifestIsNotAProduction() throws {
        try FileManager.default.createDirectory(at: store.root.appending(path: "stray"), withIntermediateDirectories: true)
        try store.create(named: "Real")
        XCTAssertEqual(store.all().map(\.name), ["Real"])
    }

    // ── the phone's existing script and takes (BEH-PROD-1) ──

    func testTheOldScriptAndTakesBecomeTheFirstProduction() throws {
        let legacy = root.appending(path: "Takes")
        try file("take-1.mov", in: legacy)
        try file("take-1-cut.mp4", in: legacy)
        let first = try XCTUnwrap(store.adopt(script: "my script", takes: legacy))
        XCTAssertEqual(first.name, ProductionStore.firstProductionName)
        XCTAssertEqual(store.script(of: first), "my script")
        XCTAssertEqual(files(in: store.takes(of: first.id)), ["take-1-cut.mp4", "take-1.mov"])
        XCTAssertEqual(files(in: legacy), [])
    }

    func testAScriptAloneIsWorthAdopting() throws {
        let first = try store.adopt(script: "just a script", takes: root.appending(path: "Takes"))
        XCTAssertEqual(first.map { store.script(of: $0) }, "just a script")
    }

    func testNothingToAdoptMakesNoProduction() throws {
        XCTAssertNil(try store.adopt(script: "  \n", takes: root.appending(path: "Takes")))
        XCTAssertEqual(store.all(), [])
    }

    func testAdoptingHappensOnlyIntoAnEmptyStore() throws {
        try store.create(named: "Existing")
        let legacy = root.appending(path: "Takes")
        try file("take-1.mov", in: legacy)
        XCTAssertNil(try store.adopt(script: "old", takes: legacy))
        XCTAssertEqual(store.all().map(\.name), ["Existing"])
        // Left where it was, not lost.
        XCTAssertEqual(files(in: legacy), ["take-1.mov"])
    }
}

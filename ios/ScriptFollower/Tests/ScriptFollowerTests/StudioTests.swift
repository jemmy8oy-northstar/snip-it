import XCTest
@testable import ScriptFollower

/// BEH-EDITION-1: where the Studio switch starts.
final class StudioTests: XCTestCase {
    func testAFreshInstallStartsAsTheGeneralRelease() {
        XCTAssertFalse(Studio.initialValue(snipItUser: nil, hasPassword: false))
        XCTAssertFalse(Studio.initialValue(snipItUser: "", hasPassword: false))
    }

    func testAPhoneAlreadySignedInToSnipItKeepsItsStudioFeatures() {
        XCTAssertTrue(Studio.initialValue(snipItUser: "james", hasPassword: false))
        XCTAssertTrue(Studio.initialValue(snipItUser: nil, hasPassword: true))
    }
}

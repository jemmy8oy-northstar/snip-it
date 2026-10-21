import XCTest
@testable import ScriptFollower

final class ScriptSectionsTests: XCTestCase {
    private let script = """
        Cold open, before any marker.

        # Intro
        Hello and welcome.
        Today we build a teleprompter.

        ---
        An unnamed break.

        ## Outro
        Thanks for watching.
        """

    func testMarkersSplitTheScriptIntoSections() {
        XCTAssertEqual(ScriptSections.parse(script), [
            ScriptSection(name: nil, body: "Cold open, before any marker."),
            ScriptSection(name: "Intro", body: "Hello and welcome.\nToday we build a teleprompter."),
            ScriptSection(name: nil, body: "An unnamed break."),
            ScriptSection(name: "Outro", body: "Thanks for watching."),
        ])
    }

    func testAScriptWithNoMarkersIsOneSection() {
        XCTAssertEqual(ScriptSections.parse("one\ntwo"), [ScriptSection(name: nil, body: "one\ntwo")])
    }

    func testAnEmptyScriptHasNoSections() {
        XCTAssertEqual(ScriptSections.parse(""), [])
        XCTAssertEqual(ScriptSections.parse("\n \n"), [])
    }

    func testNothingBeforeTheFirstMarkerMakesNoSection() {
        XCTAssertEqual(ScriptSections.parse("\n# Intro\nhi").map(\.name), ["Intro"])
    }

    func testAnEmptySectionIsKept() {
        XCTAssertEqual(ScriptSections.parse("# A\n# B\nhi"), [
            ScriptSection(name: "A", body: ""),
            ScriptSection(name: "B", body: "hi"),
        ])
    }

    func testTheDashesIOSAutocorrectsToAreABreak() {
        for line in ["---", "-----", "—-", "——", " --- ", "–-"] {
            XCTAssertTrue(ScriptSections.isBreak(line), line)
        }
        for line in ["—", "--", "-", "- - -", "-- hi", ""] {
            XCTAssertFalse(ScriptSections.isBreak(line), line)
        }
    }

    func testAHeadingNeedsASpaceAfterItsHashes() {
        XCTAssertEqual(ScriptSections.heading("# Intro"), "Intro")
        XCTAssertEqual(ScriptSections.heading("### Deep  "), "Deep")
        XCTAssertEqual(ScriptSections.heading("#"), "")
        XCTAssertNil(ScriptSections.heading("#1 tip is this"))
        XCTAssertNil(ScriptSections.heading("#ad"))
        XCTAssertNil(ScriptSections.heading("A # in the middle"))
    }

    func testABareHashIsAnUnnamedSection() {
        XCTAssertEqual(ScriptSections.parse("one\n#\ntwo").map(\.name), [nil, nil])
    }

    func testJoiningWritesTheMarkersBack() {
        XCTAssertEqual(ScriptSections.join(ScriptSections.parse(script)), """
            Cold open, before any marker.

            # Intro
            Hello and welcome.
            Today we build a teleprompter.

            ---
            An unnamed break.

            # Outro
            Thanks for watching.
            """)
    }

    func testJoinThenParseGivesTheSameSections() {
        let sections = [
            ScriptSection(name: "A", body: ""),
            ScriptSection(name: nil, body: "x\ny"),
            ScriptSection(name: "B", body: "z"),
            ScriptSection(name: nil, body: ""),
        ]
        XCTAssertEqual(ScriptSections.parse(ScriptSections.join(sections)), sections)
    }

    func testCRLFLineEndingsParse() {
        XCTAssertEqual(ScriptSections.parse("# A\r\none\r\n---\r\ntwo"), [
            ScriptSection(name: "A", body: "one"),
            ScriptSection(name: nil, body: "two"),
        ])
    }

    func testLineCountSkipsBlankLines() {
        XCTAssertEqual(ScriptSection(body: "one\n\n two \n  ").lineCount, 2)
    }

    // ── the prompter reads straight through (BEH-SCRIPT-1) ──

    func testThePrompterNeverShowsAMarker() {
        let f = ScriptFollower(script: script)
        XCTAssertEqual(f.lines, [
            "Cold open, before any marker.",
            "Hello and welcome.",
            "Today we build a teleprompter.",
            "An unnamed break.",
            "Thanks for watching.",
        ])
    }

    func testATakeRunsAcrossSectionsWithoutStopping() {
        var f = ScriptFollower(script: "# A\nhello there\n---\ngeneral kenobi")
        XCTAssertTrue(f.hear("hello there"))
        XCTAssertTrue(f.hear("hello there general kenobi"))
        XCTAssertTrue(f.isFinished)
    }
}

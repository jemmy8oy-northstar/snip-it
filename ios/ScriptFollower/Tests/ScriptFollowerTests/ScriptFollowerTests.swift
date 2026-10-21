import XCTest
@testable import ScriptFollower

final class ScriptFollowerTests: XCTestCase {
    private let script = """
        Hello and welcome to the channel.
        Today we are building a teleprompter.
        It listens while you talk and keeps up.

        When you pause, it waits for you.
        That is the whole trick.
        """

    func testTheScriptIsSplitIntoNonEmptyLines() {
        let f = ScriptFollower(script: script)
        XCTAssertEqual(f.lines.count, 5)
        XCTAssertEqual(f.lines[0], "Hello and welcome to the channel.")
        XCTAssertEqual(f.currentLine, 0)
        XCTAssertFalse(f.isFinished)
    }

    func testReadingInOrderFollowsWordByWordAndLineByLine() {
        var f = ScriptFollower(script: script)
        for word in ["hello", "and", "welcome", "to", "the", "channel"] {
            XCTAssertTrue(f.hear(word), word)
        }
        XCTAssertEqual(f.currentLine, 1)
        XCTAssertEqual(f.cursor, 6)
    }

    func testAChunkOfSeveralWordsMovesToItsLastWord() {
        var f = ScriptFollower(script: script)
        XCTAssertTrue(f.hear("Hello and welcome to the channel"))
        XCTAssertEqual(f.currentLine, 1)
    }

    func testARevisedHypothesisIsFedWholeEachTime() {
        // How a speech engine reports: the same utterance, growing.
        var f = ScriptFollower(script: script)
        f.hear("hello")
        f.hear("hello and")
        f.hear("hello and welcome")
        XCTAssertEqual(f.cursor, 3)
    }

    func testHearingTheSameTextTwiceDoesNotMoveTwice() {
        var f = ScriptFollower(script: script)
        XCTAssertTrue(f.hear("hello and welcome"))
        XCTAssertFalse(f.hear("hello and welcome"))
        XCTAssertEqual(f.cursor, 3)
    }

    func testSilenceLeavesItWhereItIs() {
        var f = ScriptFollower(script: script)
        f.hear("hello and welcome")
        XCTAssertFalse(f.hear(""))
        XCTAssertFalse(f.hear("   ...  "))
        XCTAssertEqual(f.cursor, 3)
    }

    func testAnAdLibLeavesItWhereItIs() {
        var f = ScriptFollower(script: script)
        f.hear("hello and welcome")
        XCTAssertFalse(f.hear("sorry my cat just walked in"))
        XCTAssertEqual(f.cursor, 3)

        // The control: back on script, it carries on.
        XCTAssertTrue(f.hear("to the channel"))
        XCTAssertEqual(f.currentLine, 1)
    }

    func testOneCommonWordAheadDoesNotJump() {
        var f = ScriptFollower(script: script)
        // "the" is the fifth word: next expected is "hello", so one word is not enough.
        XCTAssertFalse(f.hear("the"))
        XCTAssertEqual(f.cursor, 0)
    }

    func testSkippingAheadWithinTheLookaheadJumps() {
        var f = ScriptFollower(script: script)
        f.hear("hello and welcome to the channel")
        // Skips line 1 and goes straight to line 2.
        XCTAssertTrue(f.hear("it listens while you talk"))
        XCTAssertEqual(f.currentLine, 2)
    }

    func testSkippingBeyondTheLookaheadDoesNotJump() {
        var f = ScriptFollower(script: script, lookaheadLines: 1)
        // From line 0, line 3 is two lines past the lookahead's end.
        XCTAssertFalse(f.hear("when you pause it waits for you"))
        XCTAssertEqual(f.cursor, 0)
    }

    func testItNeverMovesBackwardsOnItsOwn() {
        var f = ScriptFollower(script: script)
        f.hear("hello and welcome to the channel")
        f.hear("today we are building a teleprompter")
        let before = f.cursor
        XCTAssertFalse(f.hear("hello and welcome to the channel"))
        XCTAssertEqual(f.cursor, before)
    }

    func testMovingToALineIsTheWayBack() {
        var f = ScriptFollower(script: script)
        f.hear("hello and welcome to the channel")
        f.hear("today we are building a teleprompter")
        f.move(toLine: 0)
        XCTAssertEqual(f.cursor, 0)
        XCTAssertTrue(f.hear("hello"))
    }

    func testAMisheardWordStillFollows() {
        var f = ScriptFollower(script: script)
        f.hear("hello and welcome to the channel")
        // "teleprompters" for "teleprompter": one edit on a long word.
        f.hear("today we are building a")
        XCTAssertTrue(f.hear("today we are building a teleprompters"))
        XCTAssertEqual(f.currentLine, 2)
    }

    func testAShortWordMustMatchExactly() {
        XCTAssertFalse(Words.same("a", "i"))
        XCTAssertFalse(Words.same("cat", "cut"))
        XCTAssertTrue(Words.same("channel", "channels"))
        XCTAssertFalse(Words.same("channel", "chunnels"))
    }

    func testCaseAccentsAndPunctuationAreNotWhatWasSaid() {
        XCTAssertEqual(Words.of("Café, DON'T stop — it’s 10!"), ["cafe", "dont", "stop", "its", "10"])
    }

    func testThePhraseNearestTheCursorWins() {
        var f = ScriptFollower(script: "one two three\nfour two three\nfive")
        f.hear("one")
        // "two three" is both at words 1-2 and 4-5; the nearer one wins.
        XCTAssertTrue(f.hear("two three"))
        XCTAssertEqual(f.cursor, 3)
    }

    func testTheEndOfTheScriptIsFinished() {
        var f = ScriptFollower(script: "Short script.")
        XCTAssertTrue(f.hear("short script"))
        XCTAssertTrue(f.isFinished)
        XCTAssertEqual(f.currentLine, 0)
        XCTAssertFalse(f.hear("anything else"))
    }

    // ── what the prompter highlights (BEH-TELE-8) ──

    private func heard(_ f: ScriptFollower, _ line: Int) -> [String] {
        f.tokens(ofLine: line).filter(\.heard).map(\.text)
    }

    func testTheWordsHeardSoFarInALineAreHighlighted() {
        var f = ScriptFollower(script: script)
        f.hear("hello and welcome")
        XCTAssertEqual(heard(f, 0), ["Hello", "and", "welcome"])
        XCTAssertEqual(f.tokens(ofLine: 0).map(\.text), ["Hello", "and", "welcome", "to", "the", "channel."])
    }

    func testEarlierLinesAreAllHeardAndLaterOnesNone() {
        var f = ScriptFollower(script: script)
        f.hear("hello and welcome to the channel")
        f.hear("today we")
        XCTAssertEqual(heard(f, 0).count, 6)
        XCTAssertEqual(heard(f, 1), ["Today", "we"])
        XCTAssertEqual(heard(f, 2), [])
    }

    func testNothingIsHighlightedBeforeAWordIsHeard() {
        let f = ScriptFollower(script: "— Hello there")
        XCTAssertEqual(heard(f, 0), [])
    }

    func testPunctuationGoesWithTheWordBesideIt() {
        var f = ScriptFollower(script: "— Hello — there, well-known friend")
        f.hear("hello")
        XCTAssertEqual(heard(f, 0), ["—", "Hello", "—"])
        // "well-known" is two words, so "well" alone does not light it.
        f.hear("hello there well")
        XCTAssertEqual(heard(f, 0), ["—", "Hello", "—", "there,"])
        f.hear("hello there well known")
        XCTAssertEqual(heard(f, 0), ["—", "Hello", "—", "there,", "well-known"])
    }

    func testTappingBackClearsTheHighlight() {
        var f = ScriptFollower(script: script)
        f.hear("hello and welcome to the channel")
        f.move(toLine: 0)
        XCTAssertEqual(heard(f, 0), [])
    }

    func testALineOutOfRangeHasNoTokens() {
        let f = ScriptFollower(script: script)
        XCTAssertEqual(f.tokens(ofLine: 5), [])
        XCTAssertEqual(f.tokens(ofLine: -1), [])
    }

    func testAnEmptyScriptIsFinishedAndNeverMoves() {
        var f = ScriptFollower(script: "\n  \n")
        XCTAssertTrue(f.lines.isEmpty)
        XCTAssertTrue(f.isFinished)
        XCTAssertFalse(f.hear("hello"))
    }
}

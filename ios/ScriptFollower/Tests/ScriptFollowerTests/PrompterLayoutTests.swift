import XCTest
@testable import ScriptFollower

final class PrompterLayoutTests: XCTestCase {
    func testAutomaticIsCentreOnTheFrontCameraAndTopOnTheBack() {
        let layout = PrompterLayout(position: .automatic)
        XCTAssertEqual(layout.placement(frontCamera: true), .centre)
        XCTAssertEqual(layout.placement(frontCamera: false), .top)
    }

    func testAChosenPositionIgnoresTheCamera() {
        for (position, placement) in [(PrompterLayout.Position.top, PrompterLayout.Placement.top), (.centre, .centre), (.bottom, .bottom)] {
            let layout = PrompterLayout(position: position)
            XCTAssertEqual(layout.placement(frontCamera: true), placement)
            XCTAssertEqual(layout.placement(frontCamera: false), placement)
        }
    }

    func testTheStandardLayoutIsAutomaticAtTheOldSizeAndFullWidth() {
        XCTAssertEqual(PrompterLayout.standard, PrompterLayout(position: .automatic, textSize: 30, width: 1))
    }

    func testSizeAndWidthArePulledIntoRange() {
        let small = PrompterLayout(textSize: 4, width: 0)
        XCTAssertEqual(small.textSize, PrompterLayout.textSizes.lowerBound)
        XCTAssertEqual(small.width, PrompterLayout.widths.lowerBound)

        let large = PrompterLayout(textSize: 400, width: 3)
        XCTAssertEqual(large.textSize, PrompterLayout.textSizes.upperBound)
        XCTAssertEqual(large.width, PrompterLayout.widths.upperBound)

        let inRange = PrompterLayout(textSize: 36, width: 0.7)
        XCTAssertEqual(inRange.textSize, 36)
        XCTAssertEqual(inRange.width, 0.7)
    }

    func testAnUnknownStoredPositionReadsAsAutomatic() {
        XCTAssertEqual(PrompterLayout(storedPosition: "sideways", textSize: 30, width: 1).position, .automatic)
        XCTAssertEqual(PrompterLayout(storedPosition: "bottom", textSize: 30, width: 1).position, .bottom)
    }
}

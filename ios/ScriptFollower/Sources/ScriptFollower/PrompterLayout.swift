import Foundation

/// Where the script sits on the prompter screen, how big its words are and how wide it runs
/// (BEH-TELE-9). Width is its own setting, not a side effect of size: a narrow column keeps the
/// eyes from flicking side to side, which shows on a close-up.
public struct PrompterLayout: Equatable, Sendable {
    /// The setting. `automatic` follows the camera: see `placement(frontCamera:)`.
    public enum Position: String, CaseIterable, Sendable {
        case automatic, top, centre, bottom
    }

    /// Where the script actually goes once the camera is known.
    public enum Placement: Equatable, Sendable {
        case top, centre, bottom
    }

    public static let textSizes: ClosedRange<Double> = 20...48
    /// A share of the screen's width. Below 0.4 a line holds two or three words.
    public static let widths: ClosedRange<Double> = 0.4...1
    public static let standard = PrompterLayout()

    public var position: Position
    public var textSize: Double
    public var width: Double

    /// Out-of-range values are pulled into range, so a stored setting from another build can
    /// never hide the script.
    public init(position: Position = .automatic, textSize: Double = 30, width: Double = 1) {
        self.position = position
        self.textSize = min(max(textSize, Self.textSizes.lowerBound), Self.textSizes.upperBound)
        self.width = min(max(width, Self.widths.lowerBound), Self.widths.upperBound)
    }

    /// A stored position, read leniently: anything unknown is `automatic`.
    public init(storedPosition: String, textSize: Double, width: Double) {
        self.init(position: Position(rawValue: storedPosition) ?? .automatic, textSize: textSize, width: width)
    }

    /// `automatic` puts the script in the centre on the front camera, so reading looks like
    /// looking at yourself, and at the top on the back camera, nearest the lens the viewer sees.
    public func placement(frontCamera: Bool) -> Placement {
        switch position {
        case .automatic: frontCamera ? .centre : .top
        case .top: .top
        case .centre: .centre
        case .bottom: .bottom
        }
    }
}

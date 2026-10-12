// swift-tools-version:6.0
import PackageDescription

// The teleprompter's one clever part, with no UI and no Apple frameworks, so it
// can be tested on Linux. The SwiftUI app depends on this package.
let package = Package(
    name: "ScriptFollower",
    products: [
        .library(name: "ScriptFollower", targets: ["ScriptFollower"]),
    ],
    targets: [
        .target(name: "ScriptFollower"),
        .testTarget(name: "ScriptFollowerTests", dependencies: ["ScriptFollower"]),
    ]
)

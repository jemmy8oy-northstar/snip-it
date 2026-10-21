// swift-tools-version:6.0
import PackageDescription

// The teleprompter's logic, with no UI and no Apple frameworks, so it can be
// tested on Linux. The SwiftUI app depends on this package; everything here is
// what the app does, minus the camera, the microphone and the screen.
let package = Package(
    name: "ScriptFollower",
    platforms: [.iOS(.v17), .macOS(.v14)],
    products: [
        .library(name: "ScriptFollower", targets: ["ScriptFollower"]),
        .library(name: "SnipItClient", targets: ["SnipItClient"]),
        .library(name: "Productions", targets: ["Productions"]),
    ],
    targets: [
        .target(name: "ScriptFollower"),
        .target(name: "SnipItClient"),
        .target(name: "Productions"),
        .testTarget(name: "ScriptFollowerTests", dependencies: ["ScriptFollower"]),
        .testTarget(name: "SnipItClientTests", dependencies: ["SnipItClient"]),
        .testTarget(name: "ProductionsTests", dependencies: ["Productions"]),
    ]
)

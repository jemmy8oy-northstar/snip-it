import SwiftUI

@main
struct TeleprompterApp: App {
    var body: some Scene {
        WindowGroup {
            TabView {
                ScriptView()
                    .tabItem { Label("Script", systemImage: "text.alignleft") }
                TakesView()
                    .tabItem { Label("Takes", systemImage: "film.stack") }
                SettingsView()
                    .tabItem { Label("Settings", systemImage: "gear") }
            }
        }
    }
}

/// Settings live in UserDefaults (and the password in the Keychain), read wherever they are needed.
enum SettingKey {
    static let script = "script"
    static let snipItURL = "snipItURL"
    static let snipItUser = "snipItUser"
    static let maxPause = "maxPauseSeconds"
    static let whisperModel = "whisperModel"
}

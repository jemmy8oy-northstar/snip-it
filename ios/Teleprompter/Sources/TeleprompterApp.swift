import Productions
import SwiftUI

@main
struct TeleprompterApp: App {
    private let store = ProductionStore(root: URL.documentsDirectory.appending(path: "Productions", directoryHint: .isDirectory))

    init() {
        adoptTheOldScriptAndTakes()
    }

    var body: some Scene {
        WindowGroup {
            TabView {
                ProductionsView(store: store)
                    .tabItem { Label("Productions", systemImage: "film.stack") }
                SettingsView()
                    .tabItem { Label("Settings", systemImage: "gear") }
            }
        }
    }

    /// Before productions there was one script, in settings, and one Takes directory. They
    /// become "My first production" the first time this build runs (BEH-PROD-1).
    private func adoptTheOldScriptAndTakes() {
        let defaults = UserDefaults.standard
        let script = defaults.string(forKey: SettingKey.legacyScript) ?? ""
        if (try? store.adopt(script: script, takes: TakeLibrary.legacyDirectory)) != nil {
            defaults.removeObject(forKey: SettingKey.legacyScript)
        }
    }
}

/// Settings live in UserDefaults (and the password in the Keychain), read wherever they are needed.
enum SettingKey {
    /// The one script from before productions; read once, by the move into "My first production".
    static let legacyScript = "script"
    static let snipItURL = "snipItURL"
    static let snipItUser = "snipItUser"
    static let maxPause = "maxPauseSeconds"
    static let whisperModel = "whisperModel"
    static let speechModelFolders = "speechModelFolders"
}

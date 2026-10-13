import Security
import SwiftUI

struct SettingsView: View {
    @AppStorage(SettingKey.snipItURL) private var snipItURL = "https://balenthiran.co.uk/snipit/"
    @AppStorage(SettingKey.snipItUser) private var snipItUser = ""
    @AppStorage(SettingKey.maxPause) private var maxPause = 0.6
    @AppStorage(SettingKey.whisperModel) private var whisperModel = "base.en"
    @State private var password = Keychain.password ?? ""

    var body: some View {
        NavigationStack {
            Form {
                Section {
                    TextField("Address", text: $snipItURL)
                        .keyboardType(.URL)
                        .textInputAutocapitalization(.never)
                        .autocorrectionDisabled()
                    TextField("Username", text: $snipItUser)
                        .textInputAutocapitalization(.never)
                        .autocorrectionDisabled()
                    SecureField("Password", text: $password)
                        .onChange(of: password) { _, value in Keychain.password = value }
                } header: {
                    Text("snip-it")
                } footer: {
                    Text("Where takes go to have their pauses cut.")
                }

                Section {
                    Stepper(value: $maxPause, in: 0.2...3, step: 0.1) {
                        Text("Cut pauses over \(maxPause.formatted(.number.precision(.fractionLength(1))))s")
                    }
                }

                Section {
                    TextField("Model", text: $whisperModel)
                        .textInputAutocapitalization(.never)
                        .autocorrectionDisabled()
                } header: {
                    Text("Speech model")
                } footer: {
                    Text("A WhisperKit model, downloaded on first use. Smaller is faster: tiny.en, base.en, small.en.")
                }
            }
            .navigationTitle("Settings")
        }
    }
}

/// The snip-it password, kept in the Keychain rather than UserDefaults.
enum Keychain {
    private static let query: [String: Any] = [
        kSecClass as String: kSecClassGenericPassword,
        kSecAttrService as String: "uk.co.balenthiran.teleprompter",
        kSecAttrAccount as String: "snip-it",
    ]

    static var password: String? {
        get {
            var lookup = query
            lookup[kSecReturnData as String] = true
            lookup[kSecMatchLimit as String] = kSecMatchLimitOne
            var item: CFTypeRef?
            guard SecItemCopyMatching(lookup as CFDictionary, &item) == errSecSuccess, let data = item as? Data else {
                return nil
            }
            return String(decoding: data, as: UTF8.self)
        }
        set {
            SecItemDelete(query as CFDictionary)
            guard let newValue, !newValue.isEmpty else { return }
            var add = query
            add[kSecValueData as String] = Data(newValue.utf8)
            SecItemAdd(add as CFDictionary, nil)
        }
    }
}

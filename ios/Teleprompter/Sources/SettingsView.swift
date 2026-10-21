import ScriptFollower
import Security
import SwiftUI

struct SettingsView: View {
    @AppStorage(SettingKey.snipItURL) private var snipItURL = "https://balenthiran.co.uk/snipit/"
    @AppStorage(SettingKey.snipItUser) private var snipItUser = ""
    @AppStorage(SettingKey.maxPause) private var maxPause = 0.6
    @AppStorage(SettingKey.whisperModel) private var whisperModel = "base.en"
    @AppStorage(SettingKey.studio) private var studio = false
    @State private var password = Keychain.password ?? ""
    private let models = SpeechModels.shared

    var body: some View {
        NavigationStack {
            Form {
                Section {
                    Toggle("Studio features", isOn: $studio)
                } footer: {
                    Text(studio
                         ? "Studio adds the features that send takes to a server, such as cutting pauses on snip-it. Turn it off any time; nothing you made is deleted."
                         : "Everything works on this iPhone, with no account and nothing sent anywhere. Studio adds features that send takes to a server.")
                }

                if studio {
                    studioSections
                }

                PrompterLayoutSettings()

                Section {
                    // The chosen one is listed even if it is not ours: this used to be a free-text field.
                    ForEach(models.supported + (models.supported.contains(whisperModel) ? [] : [whisperModel]), id: \.self) { variant in
                        SpeechModelRow(variant: variant, chosen: $whisperModel)
                    }
                } header: {
                    Text("Speech model")
                } footer: {
                    Text("The prompter follows your voice with the ticked model. Smaller listens faster; bigger hears more accurately. The first download is prepared for this iPhone, which can take a minute.")
                }
            }
            .navigationTitle("Settings")
        }
    }

    /// What only Studio shows (BEH-EDITION-1): where takes go to have their pauses cut.
    @ViewBuilder private var studioSections: some View {
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
    }
}

/// Where the script sits, how big it is and how wide it runs (BEH-TELE-9). Shown in Settings and
/// in a short sheet over the prompter, so a change can be judged live against your own face.
struct PrompterLayoutSettings: View {
    @AppStorage(SettingKey.prompterPosition) private var position = PrompterLayout.standard.position.rawValue
    @AppStorage(SettingKey.prompterTextSize) private var textSize = PrompterLayout.standard.textSize
    @AppStorage(SettingKey.prompterWidth) private var width = PrompterLayout.standard.width

    var body: some View {
        Section {
            Picker("Position", selection: $position) {
                Text("Auto").tag(PrompterLayout.Position.automatic.rawValue)
                Text("Top").tag(PrompterLayout.Position.top.rawValue)
                Text("Centre").tag(PrompterLayout.Position.centre.rawValue)
                Text("Bottom").tag(PrompterLayout.Position.bottom.rawValue)
            }
            .pickerStyle(.segmented)

            LabeledContent("Size") {
                Slider(value: $textSize, in: PrompterLayout.textSizes, step: 2)
            }
            LabeledContent("Width") {
                Slider(value: $width, in: PrompterLayout.widths, step: 0.05)
            }
        } header: {
            Text("Script on screen")
        } footer: {
            Text("Auto puts the script in the centre on the front camera, so reading looks like looking at yourself, and at the top on the back camera. A narrower script keeps your eyes still on a close-up.")
        }
    }
}

/// One speech model: tick to choose it, and its state with the one action that state allows.
struct SpeechModelRow: View {
    let variant: String
    @Binding var chosen: String
    private let models = SpeechModels.shared

    var body: some View {
        HStack {
            Button {
                chosen = variant
            } label: {
                HStack {
                    Image(systemName: chosen == variant ? "checkmark.circle.fill" : "circle")
                        .foregroundStyle(chosen == variant ? Color.accentColor : .secondary)
                    VStack(alignment: .leading, spacing: 2) {
                        Text(variant)
                        detail
                    }
                }
            }
            .buttonStyle(.plain)

            Spacer()

            action
        }
        .swipeActions {
            if models.status(of: variant) == .ready {
                Button("Delete", systemImage: "trash", role: .destructive) { models.delete(variant) }
            }
        }
    }

    @ViewBuilder private var detail: some View {
        switch models.status(of: variant) {
        case .notDownloaded:
            Text("Not downloaded").font(.caption).foregroundStyle(.secondary)
        case .downloading(let fraction):
            ProgressView(value: fraction) {
                Text("Downloading \(fraction.formatted(.percent.precision(.fractionLength(0))))").font(.caption)
            }
        case .preparing:
            Text("Preparing for this iPhone…").font(.caption).foregroundStyle(.secondary)
        case .ready:
            Text("Ready. Swipe to delete.").font(.caption).foregroundStyle(.secondary)
        case .failed(let reason):
            Text(reason).font(.caption).foregroundStyle(.red)
        }
    }

    @ViewBuilder private var action: some View {
        switch models.status(of: variant) {
        case .notDownloaded:
            Button("Download") { Task { await models.download(variant) } }
                .buttonStyle(.bordered)
        case .failed:
            // A model that downloaded but would not load only needs preparing again.
            Button("Try again") {
                Task {
                    if models.isDownloaded(variant) {
                        _ = try? await models.engine(for: variant)
                    } else {
                        await models.download(variant)
                    }
                }
            }
            .buttonStyle(.bordered)
        case .downloading, .preparing:
            ProgressView()
        case .ready:
            EmptyView()
        }
    }
}

/// The snip-it password, kept in the Keychain rather than UserDefaults.
enum Keychain {
    private static let query: [String: Any] = [
        kSecClass as String: kSecClassGenericPassword,
        kSecAttrService as String: "uk.co.balenthiran.yourstudio",
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

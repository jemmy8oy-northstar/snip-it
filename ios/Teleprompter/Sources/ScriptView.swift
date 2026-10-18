import SwiftUI

struct ScriptView: View {
    @AppStorage(SettingKey.script) private var script = ""
    @AppStorage(SettingKey.whisperModel) private var model = "base.en"
    @State private var prompting = false

    var body: some View {
        NavigationStack {
            TextEditor(text: $script)
                .font(.title3)
                .padding(.horizontal)
                .overlay(alignment: .topLeading) {
                    if script.isEmpty {
                        Text("Paste your script. Each line is one prompter line.")
                            .foregroundStyle(.secondary)
                            .padding(.horizontal, 20)
                            .padding(.top, 8)
                            .allowsHitTesting(false)
                    }
                }
                .navigationTitle("Script")
                .toolbar {
                    Button("Prompt") { prompting = true }
                        .disabled(script.trimmingCharacters(in: .whitespacesAndNewlines).isEmpty)
                }
                .fullScreenCover(isPresented: $prompting) {
                    PrompterView(script: script, model: model)
                }
        }
    }
}

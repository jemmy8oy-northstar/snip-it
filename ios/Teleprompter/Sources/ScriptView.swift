import Productions
import SwiftUI

/// One production's script. It saves as you type, into that production's `script.txt`.
struct ScriptView: View {
    let production: Production
    let store: ProductionStore
    @AppStorage(SettingKey.whisperModel) private var model = "base.en"
    @State private var script = ""
    @State private var prompting = false
    @State private var error: String?

    var body: some View {
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
            .safeAreaInset(edge: .bottom) {
                Button {
                    prompting = true
                } label: {
                    Label("Prompt", systemImage: "play.fill")
                        .frame(maxWidth: .infinity)
                }
                .buttonStyle(.borderedProminent)
                .controlSize(.large)
                .padding()
                .disabled(script.trimmingCharacters(in: .whitespacesAndNewlines).isEmpty)
            }
            .onAppear { script = store.script(of: production) }
            .onChange(of: script) { _, text in
                do {
                    try store.setScript(text, of: production)
                    error = nil
                } catch {
                    self.error = "The script could not be saved: \(error.localizedDescription)"
                }
            }
            .overlay(alignment: .bottom) {
                if let error {
                    Text(error).font(.caption).foregroundStyle(.red).padding(.bottom, 80)
                }
            }
            .fullScreenCover(isPresented: $prompting) {
                PrompterView(script: script, model: model, takes: TakeLibrary(directory: store.takes(of: production.id)))
            }
    }
}

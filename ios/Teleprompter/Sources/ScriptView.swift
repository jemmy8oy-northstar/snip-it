import Productions
import ScriptFollower
import SwiftUI

/// A section being edited. The id is the screen's, so a fold or a drag follows the section
/// rather than its position; it is never saved.
struct EditableSection: Identifiable, Equatable {
    let id = UUID()
    var section: ScriptSection
    var folded = false
}

/// One production's script, as sections that fold (BEH-SCRIPT-2). It saves on every change,
/// into that production's `script.txt`, as plain text with `#` and `---` markers.
struct ScriptView: View {
    let production: Production
    let store: ProductionStore
    @AppStorage(SettingKey.whisperModel) private var model = "base.en"
    @State private var sections: [EditableSection] = []
    @State private var loaded = false
    @State private var prompting = false
    @State private var managing = false
    @State private var showingCoverage = false
    @State private var coverage: [TakeCoverage] = []
    @State private var error: String?

    private var script: String { ScriptSections.join(sections.map(\.section)) }
    private var takes: TakeLibrary { TakeLibrary(directory: store.takes(of: production.id)) }

    var body: some View {
        List {
            ForEach($sections) { $item in
                DisclosureGroup(isExpanded: Binding(get: { !item.folded }, set: { item.folded = !$0 })) {
                    if showingCoverage {
                        CoverageLines(section: item.section, takes: coverage)
                    } else {
                        TextEditor(text: $item.section.body)
                            .font(.title3)
                            .frame(minHeight: 140)
                            .overlay(alignment: .topLeading) {
                                if item.section.body.isEmpty {
                                    Text("Paste or write this section. Each line is one prompter line.")
                                        .foregroundStyle(.secondary)
                                        .padding(.top, 8)
                                        .padding(.leading, 5)
                                        .allowsHitTesting(false)
                                }
                            }
                    }
                } label: {
                    SectionLabel(section: item.section, folded: item.folded, takes: coverage)
                }
            }
            Button("Add section", systemImage: "plus") {
                sections.append(EditableSection(section: ScriptSection(name: "Section \(sections.count + 1)")))
            }
            Text("Start a line with # to name a section, or write --- for a break. The prompter reads straight through them.")
                .font(.caption)
                .foregroundStyle(.secondary)
        }
        .toolbar {
            ToolbarItemGroup(placement: .secondaryAction) {
                Button("Fold all", systemImage: "rectangle.compress.vertical") { setFolded(true) }
                Button("Unfold all", systemImage: "rectangle.expand.vertical") { setFolded(false) }
                Button("Manage sections", systemImage: "list.bullet.indent") { managing = true }
                Button(showingCoverage ? "Edit script" : "Show coverage",
                       systemImage: showingCoverage ? "pencil" : "checklist") {
                    coverage = takes.coverage()
                    showingCoverage.toggle()
                }
            }
        }
        .safeAreaInset(edge: .bottom) {
            VStack(spacing: 4) {
                if let error {
                    Text(error).font(.caption).foregroundStyle(.red)
                }
                Button {
                    prompting = true
                } label: {
                    Label("Prompt", systemImage: "play.fill")
                        .frame(maxWidth: .infinity)
                }
                .buttonStyle(.borderedProminent)
                .controlSize(.large)
                .disabled(ScriptFollower(script: script).lines.isEmpty)
            }
            .padding()
            .background(.bar)
        }
        .onAppear(perform: load)
        .onChange(of: sections) { _, _ in save() }
        .sheet(isPresented: $managing) {
            ManageSectionsView(sections: $sections)
        }
        .fullScreenCover(isPresented: $prompting, onDismiss: { coverage = takes.coverage() }) {
            PrompterView(script: script, model: model, takes: takes)
        }
    }

    /// Parsed once, on opening: re-parsing as you type would split a section the moment a line
    /// began with #, and move the cursor out from under you. A marker typed into a section takes
    /// effect the next time the script is opened.
    private func load() {
        guard !loaded else { return }
        loaded = true
        let parsed = ScriptSections.parse(store.script(of: production))
        sections = (parsed.isEmpty ? [ScriptSection()] : parsed).map { EditableSection(section: $0) }
        coverage = takes.coverage()
    }

    private func save() {
        guard loaded else { return }
        do {
            try store.setScript(script, of: production)
            error = nil
        } catch {
            self.error = "The script could not be saved: \(error.localizedDescription)"
        }
    }

    private func setFolded(_ folded: Bool) {
        for index in sections.indices {
            sections[index].folded = folded
        }
    }
}

private struct SectionLabel: View {
    let section: ScriptSection
    let folded: Bool
    let takes: [TakeCoverage]

    var body: some View {
        HStack {
            Text(section.name ?? "Untitled section")
                .font(.headline)
                .foregroundStyle(section.name == nil ? .secondary : .primary)
            Spacer()
            if !takes.isEmpty, section.lineCount > 0 {
                let covered = section.lines.filter { TakeCoverage.count(of: $0, in: takes) > 0 }.count
                Text("\(covered)/\(section.lineCount) covered")
                    .font(.caption)
                    .foregroundStyle(covered == section.lineCount ? Color.green : Color.secondary)
            } else if folded {
                Text(section.lineCount == 1 ? "1 line" : "\(section.lineCount) lines")
                    .font(.caption)
                    .foregroundStyle(.secondary)
            }
        }
    }
}

/// A section's lines, each with how many takes covered it (BEH-SCRIPT-4). Read-only: "Edit
/// script" in the menu goes back to editing.
private struct CoverageLines: View {
    let section: ScriptSection
    let takes: [TakeCoverage]

    var body: some View {
        VStack(alignment: .leading, spacing: 10) {
            if section.lines.isEmpty {
                Text("No lines yet.").foregroundStyle(.secondary)
            }
            ForEach(Array(section.lines.enumerated()), id: \.offset) { _, line in
                let count = TakeCoverage.count(of: line, in: takes)
                HStack(alignment: .firstTextBaseline) {
                    Image(systemName: count > 0 ? "checkmark.circle.fill" : "circle")
                        .foregroundStyle(count > 0 ? Color.green : Color.secondary)
                    Text(line)
                    Spacer()
                    if count > 0 {
                        Text(count == 1 ? "1 take" : "\(count) takes")
                            .font(.caption)
                            .foregroundStyle(.secondary)
                    }
                }
                .accessibilityElement(children: .combine)
            }
        }
        .padding(.vertical, 4)
    }
}

/// Rename, reorder, clear and delete sections (BEH-SCRIPT-3). Every delete and clear asks first.
struct ManageSectionsView: View {
    @Binding var sections: [EditableSection]
    @Environment(\.dismiss) private var dismiss
    @State private var clearing: EditableSection.ID?
    @State private var deleting: EditableSection.ID?
    @State private var deletingAll = false

    var body: some View {
        NavigationStack {
            List {
                Section {
                    ForEach($sections) { $item in
                        HStack {
                            TextField("Untitled section", text: Binding(
                                get: { item.section.name ?? "" },
                                set: { item.section.name = $0.isEmpty ? nil : $0 }))
                            Text(item.section.lineCount == 1 ? "1 line" : "\(item.section.lineCount) lines")
                                .font(.caption)
                                .foregroundStyle(.secondary)
                            // A menu rather than swipe actions: a list in edit mode, for dragging, has no swipes.
                            Menu {
                                Button("Clear lines", systemImage: "eraser") { clearing = item.id }
                                Button("Delete section", systemImage: "trash", role: .destructive) { deleting = item.id }
                            } label: {
                                Image(systemName: "ellipsis.circle")
                            }
                            .accessibilityLabel("Section actions")
                        }
                    }
                    .onMove { sections.move(fromOffsets: $0, toOffset: $1) }
                } footer: {
                    Text("Drag a section by its handle to reorder it.")
                }

                Section {
                    Button("Delete all sections", role: .destructive) { deletingAll = true }
                        .disabled(sections.isEmpty)
                }
            }
            .environment(\.editMode, .constant(.active))
            .navigationTitle("Sections")
            .navigationBarTitleDisplayMode(.inline)
            .toolbar {
                Button("Done") { dismiss() }
            }
            // `presenting:` hands the id to the button, so it cannot read one already cleared by the dismissal.
            .confirmationDialog("Clear this section's lines? Its name stays.",
                                isPresented: isSet($clearing), titleVisibility: .visible, presenting: clearing) { id in
                Button("Clear lines", role: .destructive) {
                    if let index = sections.firstIndex(where: { $0.id == id }) {
                        sections[index].section.body = ""
                    }
                }
            }
            .confirmationDialog("Delete this section and its lines?",
                                isPresented: isSet($deleting), titleVisibility: .visible, presenting: deleting) { id in
                Button("Delete section", role: .destructive) {
                    sections.removeAll { $0.id == id }
                }
            }
            .confirmationDialog("Delete every section? The whole script is removed.",
                                isPresented: $deletingAll, titleVisibility: .visible) {
                Button("Delete all sections", role: .destructive) {
                    sections = [EditableSection(section: ScriptSection())]
                }
            }
        }
    }

    private func isSet(_ id: Binding<EditableSection.ID?>) -> Binding<Bool> {
        Binding(get: { id.wrappedValue != nil }, set: { if !$0 { id.wrappedValue = nil } })
    }
}

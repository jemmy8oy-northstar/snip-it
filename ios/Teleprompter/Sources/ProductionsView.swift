import Productions
import SwiftUI

/// The first screen (BEH-PROD-1): every production, newest first, with New production at the top.
struct ProductionsView: View {
    let store: ProductionStore
    @State private var productions: [Production] = []
    @State private var naming = false
    @State private var newName = ""
    @State private var path: [Production] = []
    @State private var error: String?

    var body: some View {
        NavigationStack(path: $path) {
            List {
                Section {
                    Button {
                        newName = ""
                        naming = true
                    } label: {
                        Label("New production", systemImage: "plus.circle.fill")
                    }
                }
                Section {
                    if productions.isEmpty {
                        Text("A production is one video: its script and every take of it.")
                            .foregroundStyle(.secondary)
                    }
                    ForEach(productions) { production in
                        NavigationLink(value: production) {
                            VStack(alignment: .leading) {
                                Text(production.name)
                                Text(production.created.formatted(date: .abbreviated, time: .omitted))
                                    .font(.caption)
                                    .foregroundStyle(.secondary)
                            }
                        }
                    }
                }
            }
            .navigationTitle("Productions")
            .navigationDestination(for: Production.self) { production in
                ProductionView(production: production, store: store, changed: reload)
            }
            .alert("New production", isPresented: $naming) {
                TextField("Name", text: $newName)
                Button("Create") { create() }
                Button("Cancel", role: .cancel) {}
            }
            .alert("Productions", isPresented: Binding(get: { error != nil }, set: { if !$0 { error = nil } })) {
                Button("OK") { error = nil }
            } message: {
                Text(error ?? "")
            }
            .onAppear(perform: reload)
        }
    }

    private func reload() {
        productions = store.all()
    }

    private func create() {
        do {
            let production = try store.create(named: newName)
            reload()
            path.append(production)
        } catch {
            self.error = "The production could not be created: \(error.localizedDescription)"
        }
    }
}

/// One production: its script, its takes, and renaming or deleting it.
struct ProductionView: View {
    @State var production: Production
    let store: ProductionStore
    let changed: () -> Void
    @State private var pane = Pane.script
    @State private var renaming = false
    @State private var newName = ""
    @State private var confirmingDelete = false
    @State private var error: String?
    @Environment(\.dismiss) private var dismiss

    enum Pane: String, CaseIterable {
        case script = "Script"
        case takes = "Takes"
    }

    var body: some View {
        VStack(spacing: 0) {
            Picker("Show", selection: $pane) {
                ForEach(Pane.allCases, id: \.self) { Text($0.rawValue) }
            }
            .pickerStyle(.segmented)
            .padding(.horizontal)

            switch pane {
            case .script: ScriptView(production: production, store: store)
            case .takes: TakesView(library: TakeLibrary(directory: store.takes(of: production.id)))
            }
        }
        .navigationTitle(production.name)
        .navigationBarTitleDisplayMode(.inline)
        .toolbar {
            Menu {
                Button("Rename", systemImage: "pencil") {
                    newName = production.name
                    renaming = true
                }
                Button("Delete production", systemImage: "trash", role: .destructive) {
                    confirmingDelete = true
                }
            } label: {
                Image(systemName: "ellipsis.circle")
            }
        }
        .alert("Rename production", isPresented: $renaming) {
            TextField("Name", text: $newName)
            Button("Rename") { rename() }
            Button("Cancel", role: .cancel) {}
        }
        .confirmationDialog(
            "Delete \(production.name) and all of its takes?",
            isPresented: $confirmingDelete,
            titleVisibility: .visible
        ) {
            Button("Delete production and takes", role: .destructive) { delete() }
        }
        .alert("Production", isPresented: Binding(get: { error != nil }, set: { if !$0 { error = nil } })) {
            Button("OK") { error = nil }
        } message: {
            Text(error ?? "")
        }
    }

    private func rename() {
        do {
            production = try store.rename(production, to: newName)
            changed()
        } catch {
            self.error = "The production could not be renamed: \(error.localizedDescription)"
        }
    }

    private func delete() {
        do {
            try store.delete(production)
            changed()
            dismiss()
        } catch {
            self.error = "The production could not be deleted: \(error.localizedDescription)"
        }
    }
}

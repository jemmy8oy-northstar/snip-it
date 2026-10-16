import Foundation

/// Writes a multipart/form-data body to a temporary file: text fields, then one
/// file copied across in chunks, so a take is never held in memory whole.
struct Multipart {
    let boundary: String

    init(boundary: String = "snipit-\(UUID().uuidString)") {
        self.boundary = boundary
    }

    var contentType: String { "multipart/form-data; boundary=\(boundary)" }

    /// The caller deletes the returned file once it has been sent.
    func write(fields: [(name: String, value: String)], fileField: String, file: URL) throws -> URL {
        let body = FileManager.default.temporaryDirectory
            .appendingPathComponent("snipit-upload-\(UUID().uuidString)")
        guard FileManager.default.createFile(atPath: body.path, contents: nil) else {
            throw CocoaError(.fileWriteUnknown)
        }
        do {
            let out = try FileHandle(forWritingTo: body)
            defer { try? out.close() }

            var head = ""
            for field in fields {
                head += "--\(boundary)\r\n"
                head += "Content-Disposition: form-data; name=\"\(field.name)\"\r\n\r\n"
                head += "\(field.value)\r\n"
            }
            head += "--\(boundary)\r\n"
            head += "Content-Disposition: form-data; name=\"\(fileField)\"; filename=\"\(file.lastPathComponent)\"\r\n"
            head += "Content-Type: \(Self.mimeType(of: file))\r\n\r\n"
            try out.write(contentsOf: Data(head.utf8))

            let source = try FileHandle(forReadingFrom: file)
            defer { try? source.close() }
            while let chunk = try source.read(upToCount: 1 << 20), !chunk.isEmpty {
                try out.write(contentsOf: chunk)
            }

            try out.write(contentsOf: Data("\r\n--\(boundary)--\r\n".utf8))
        } catch {
            try? FileManager.default.removeItem(at: body)
            throw error
        }
        return body
    }

    /// The camera records .mov; anything else is sent as mp4.
    static func mimeType(of file: URL) -> String {
        file.pathExtension.lowercased() == "mov" ? "video/quicktime" : "video/mp4"
    }
}

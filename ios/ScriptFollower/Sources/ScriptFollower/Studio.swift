/// The Studio switch (BEH-EDITION-1, snip-it.notes.md#2026-10-21-1647): one app, two editions. With
/// it off the app is the general release and runs entirely on the phone (BEH-EDITION-2); with it on,
/// the features that talk to a server, and anything experimental, appear.
public enum Studio {
    /// The switch's value the first time a build with it runs. Off on a fresh install, so the general
    /// release is what a stranger gets; on for a phone already signed in to snip-it, so the update
    /// takes nothing away from the person using it. After that the stored value is the only answer.
    public static func initialValue(snipItUser: String?, hasPassword: Bool) -> Bool {
        hasPassword || !(snipItUser ?? "").isEmpty
    }
}

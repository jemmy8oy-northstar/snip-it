/**
 * Holds the video the visitor picked, in the browser, for as long as the tab is open.
 *
 * snip-it keeps no server-side copy of a source video (#22): the backend deletes the upload as
 * soon as the transcription job has finished with it. So the browser is now the only thing that
 * still has the file, and it needs it twice more — to play in the editor, and to send back with
 * the cut.
 *
 * Deliberately a module-level slot rather than Redux state: a `File` is not serialisable, so it
 * would break the store's one real invariant and every devtools/persistence assumption with it.
 * Module scope gives exactly the lifetime that is wanted — it survives the navigation from the
 * upload panel to `/editor/:id`, and it is gone on a reload, which is the honest answer, because
 * a reloaded tab genuinely no longer has the file.
 *
 * One slot, not a map: a second upload replaces the first, so this cannot grow to hold several
 * whole videos in memory.
 */
let held: { transcriptionJobId: string; file: File } | null = null;

export function rememberSourceFile(transcriptionJobId: string, file: File): void {
  held = { transcriptionJobId, file };
}

/** The file for this job, or `null` — which is a normal state, not an error (see above). */
export function getSourceFile(transcriptionJobId: string): File | null {
  return held?.transcriptionJobId === transcriptionJobId ? held.file : null;
}

/** Test seam: module state outlives an individual test, so it has to be clearable. */
export function forgetSourceFile(): void {
  held = null;
}

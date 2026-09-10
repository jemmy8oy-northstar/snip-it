// The editor's own view of a transcript. The wire types live in
// src/api/generatedApi.ts (generated from the backend's OpenAPI schema); these are what
// the editor works in after api/transcriptAdapter.ts has normalised them — chiefly by
// resolving each segment's member word indices, which the backend does not send.

/** A single transcribed word with second-precision timestamps. */
export interface TranscriptWordDto {
  text: string;
  start: number;
  end: number;
}

/** A transcript segment (sentence/utterance). `wordIndices` reference
 * positions in the parent TranscriptDto's flattened `words` array. */
export interface TranscriptSegmentDto {
  start: number;
  end: number;
  text: string;
  wordIndices: number[];
}

export interface TranscriptDto {
  durationSeconds: number;
  words: TranscriptWordDto[];
  segments: TranscriptSegmentDto[];
}

/** Editor-local view of a word: the transcript word plus edit state.
 * Overrides mirror review.html's per-segment scrubber — they replace the
 * buffered boundary with an exact one for that word's segment edge. */
export interface EditorWord extends TranscriptWordDto {
  index: number;
  kept: boolean;
  startOverride?: number;
  endOverride?: number;
}

export interface EditorSegment {
  start: number;
  end: number;
  text: string;
  wordIndices: number[];
}

/** A contiguous [start, end) span of source video time to keep in the output. */
export interface KeptRange {
  start: number;
  end: number;
}

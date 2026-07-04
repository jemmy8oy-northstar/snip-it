// Data contract types — named to match the backend's DTOs so wiring the real
// API in later is a thin swap (see mockTranscript.ts / transcriptApi.ts).

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

/** One row of the request sent to the backend describing what to keep. */
export interface CutWordMarkingDto {
  index: number;
  kept: boolean;
}

export interface CutRequestDto {
  transcriptId: string;
  bufferMs: number;
  words: CutWordMarkingDto[];
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

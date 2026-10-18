import type { TranscriptWord as ApiTranscriptWord } from '../../../api/generatedApi';
import { computeKeptRuns } from '../editListLogic';
import type { EditorWord } from '../types';

/**
 * The JSON that travels in the multipart `request` field of `POST /api/cuts`.
 *
 * Declared here rather than imported from `generatedApi`: the route takes a multipart body now
 * (#22 — the browser re-sends the video with the cut), so `CutRequest` is no longer a JSON request
 * schema in the OpenAPI document and the codegen no longer emits a type for it. The shape is
 * unchanged, and the backend still deserialises it into the same `CutRequest` DTO.
 */
export type CutRequestPayload = {
  transcriptionJobId: string;
  words: ApiTranscriptWord[];
};

/**
 * Builds the `POST /api/cuts` payload from the editor's state.
 *
 * The contract is words-in, not ranges-in: the backend's `KeepRangeCalculator` walks the
 * words it is given in order and turns each contiguous run of `kept` words into one range
 * spanning the first word's `start` to the last word's `end`. It knows nothing about the
 * editor's buffer slider or the scrubber's per-segment overrides.
 *
 * So we send the *effective* boundaries: each kept run's edge words carry the padded /
 * overridden times that `computeKeptRuns` worked out, which makes the backend rebuild
 * exactly the runs the editor is showing.
 *
 * One deliberate difference: the editor's display merges runs whose buffered edges touch,
 * the backend does not, so touching runs arrive as two adjacent ranges instead of one.
 * The output video is identical — the cut list just has an extra seam at a point where
 * nothing is removed.
 */
export function buildCutRequest(
  transcriptionJobId: string,
  words: EditorWord[],
  bufferSeconds: number,
  durationSeconds: number,
): CutRequestPayload {
  const payload: ApiTranscriptWord[] = words.map((word) => ({
    text: word.text,
    start: word.start,
    end: word.end,
    kept: word.kept,
  }));

  for (const run of computeKeptRuns(words, bufferSeconds, durationSeconds)) {
    payload[run.firstIndex].start = run.start;
    payload[run.lastIndex].end = run.end;
  }

  return { transcriptionJobId, words: payload };
}

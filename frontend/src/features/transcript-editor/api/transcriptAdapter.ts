import type { Transcript as ApiTranscript } from '../../../api/generatedApi';
import type { TranscriptDto, TranscriptSegmentDto, TranscriptWordDto } from '../types';

/**
 * Adapts the backend's `Transcript` to the shape the editor works in.
 *
 * One real gap between the contract and the editor's model: **`wordIndices` does not exist
 * on the wire.** The backend's `TranscriptSegment` carries only `index`/`start`/`end`/`text`,
 * but the editor needs to know which words belong to which segment (segment toggling, and the
 * scrubber, which edits a segment's boundary words). We derive membership from the timestamps
 * that both words and segments come back with.
 */
export function toEditorTranscript(dto: ApiTranscript): TranscriptDto {
  const words: TranscriptWordDto[] = dto.words.map((word) => ({
    text: word.text,
    start: word.start,
    end: word.end,
  }));

  const segments = withWordIndices(
    dto.segments.map((segment) => ({
      start: segment.start,
      end: segment.end,
      text: segment.text,
      wordIndices: [] as number[],
    })),
    words,
  );

  // A transcript with no duration would make every buffer/stats calculation collapse to
  // zero, so fall back to where the words actually stop.
  const lastWordEnd = words.length ? words[words.length - 1].end : 0;
  const durationSeconds = dto.durationSeconds || lastWordEnd;

  return { durationSeconds, words, segments };
}

/**
 * Assigns every word to exactly one segment, by timestamp.
 *
 * A word goes to the segment containing its midpoint; using the midpoint rather than the
 * start keeps words that straddle a boundary with the segment they mostly sit in. Words
 * that fall in a gap between segments (segment and word timings come from the same model
 * output, but nothing guarantees they tile the timeline) go to the nearest segment, so no
 * word is ever dropped from the word track. Segments left with no words are removed
 * rather than rendered as empty rows.
 */
function withWordIndices(
  segments: TranscriptSegmentDto[],
  words: TranscriptWordDto[],
): TranscriptSegmentDto[] {
  if (!words.length) return [];

  // No segments at all (a words-only transcript) — one segment spanning everything beats
  // an editor with an empty word track.
  if (!segments.length) {
    return [
      {
        start: words[0].start,
        end: words[words.length - 1].end,
        text: words.map((word) => word.text).join(' '),
        wordIndices: words.map((_, index) => index),
      },
    ];
  }

  words.forEach((word, wordIndex) => {
    segments[indexOfSegmentFor(segments, word)].wordIndices.push(wordIndex);
  });

  return segments.filter((segment) => segment.wordIndices.length > 0);
}

function indexOfSegmentFor(segments: TranscriptSegmentDto[], word: TranscriptWordDto): number {
  const midpoint = (word.start + word.end) / 2;

  const containing = segments.findIndex(
    (segment) => midpoint >= segment.start && midpoint < segment.end,
  );
  if (containing !== -1) return containing;

  let nearest = 0;
  let nearestDistance = Number.POSITIVE_INFINITY;
  segments.forEach((segment, index) => {
    const distance = Math.max(segment.start - midpoint, midpoint - segment.end, 0);
    if (distance < nearestDistance) {
      nearestDistance = distance;
      nearest = index;
    }
  });
  return nearest;
}

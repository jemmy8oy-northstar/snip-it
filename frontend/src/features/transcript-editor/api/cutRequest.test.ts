import { describe, expect, it } from 'vitest';
import type { TranscriptWord as ApiTranscriptWord } from '../../../api/generatedApi';
import { buildCutRequest } from './cutRequest';
import { computeKeptRanges } from '../editListLogic';
import type { EditorWord, KeptRange } from '../types';

/**
 * The backend's KeepRangeCalculator, reimplemented here so the payload can be checked
 * against what the server will actually do with it: walk the words in order and turn each
 * contiguous run of kept words into one range, first word's start to last word's end.
 * Kept deliberately dumb and in lockstep with
 * backend/Balenthiran.Snipit.Services/Cutting/KeepRangeCalculator.cs.
 */
function backendKeepRanges(words: ApiTranscriptWord[]): KeptRange[] {
  const ranges: KeptRange[] = [];
  let start: number | null = null;
  let end = 0;

  for (const word of words) {
    if (word.kept) {
      if (start === null) start = word.start as number;
      end = word.end as number;
    } else if (start !== null) {
      ranges.push({ start, end });
      start = null;
    }
  }
  if (start !== null) ranges.push({ start, end });

  return ranges;
}

function mergeTouching(ranges: KeptRange[]): KeptRange[] {
  const merged: KeptRange[] = [];
  for (const range of ranges) {
    const previous = merged[merged.length - 1];
    if (previous && range.start <= previous.end) {
      previous.end = Math.max(previous.end, range.end);
    } else {
      merged.push({ ...range });
    }
  }
  return merged;
}

function words(
  spec: Array<[text: string, start: number, end: number, kept: boolean]>,
): EditorWord[] {
  return spec.map(([text, start, end, kept], index) => ({ text, start, end, kept, index }));
}

const JOB_ID = 'job-1';

describe('buildCutRequest', () => {
  it('sends every word with its kept flag', () => {
    const editorWords = words([
      ['a', 0, 1, true],
      ['b', 1, 2, false],
      ['c', 2, 3, true],
    ]);

    const request = buildCutRequest(JOB_ID, editorWords, 0, 10);

    expect(request.transcriptionJobId).toBe(JOB_ID);
    expect(request.words).toHaveLength(3);
    expect(request.words!.map((word) => word.kept)).toEqual([true, false, true]);
    expect(request.words!.map((word) => word.text)).toEqual(['a', 'b', 'c']);
  });

  // The whole point of the payload shape: the backend has no buffer setting, so the padded
  // boundaries have to arrive on the run's edge words.
  it('carries the buffered boundaries on each run edge', () => {
    const editorWords = words([
      ['a', 1.0, 1.4, true],
      ['b', 2.0, 2.4, false],
      ['c', 3.0, 3.4, true],
    ]);

    const request = buildCutRequest(JOB_ID, editorWords, 0.2, 10);

    // Run 1 = word a: start padded back 0.2 into the 1.0s of leading silence, end padded
    // 0.2 forward into the 0.6s gap before b.
    expect(request.words![0]).toMatchObject({ start: 0.8, end: 1.6 });
    // Word b is cut, so it keeps its raw times — the backend ignores them.
    expect(request.words![1]).toMatchObject({ start: 2.0, end: 2.4 });
    expect(request.words![2]).toMatchObject({ start: 2.8, end: 3.6 });
  });

  it('applies a scrubber override rather than the buffer on that edge', () => {
    const editorWords = words([['a', 1.0, 1.4, true]]);
    editorWords[0].startOverride = 0.95;

    const request = buildCutRequest(JOB_ID, editorWords, 0.2, 10);

    expect(request.words![0].start).toBe(0.95);
  });

  it('collapses to a single word carrying both boundaries for a one-word run', () => {
    const request = buildCutRequest(JOB_ID, words([['only', 1.0, 1.4, true]]), 0.2, 10);

    expect(request.words![0]).toMatchObject({ start: 0.8, end: 1.6 });
  });

  it('produces no kept words when nothing is kept', () => {
    const request = buildCutRequest(JOB_ID, words([['a', 0, 1, false]]), 0.2, 10);

    expect(request.words!.every((word) => !word.kept)).toBe(true);
    expect(backendKeepRanges(request.words!)).toEqual([]);
  });

  describe('the backend reproduces what the editor shows', () => {
    const cases: Array<[name: string, editorWords: EditorWord[], buffer: number, duration: number]> = [
      [
        'alternating kept and cut words',
        words([
          ['a', 0.0, 0.4, true],
          ['b', 0.5, 0.9, false],
          ['c', 1.5, 1.9, true],
          ['d', 2.0, 2.4, false],
          ['e', 3.0, 3.4, true],
        ]),
        0.1,
        4,
      ],
      [
        'a leading and trailing cut',
        words([
          ['a', 0.0, 0.4, false],
          ['b', 1.0, 1.4, true],
          ['c', 1.5, 1.9, true],
          ['d', 3.0, 3.4, false],
        ]),
        0.2,
        4,
      ],
      [
        'zero buffer',
        words([
          ['a', 0.0, 0.4, true],
          ['b', 0.5, 0.9, false],
          ['c', 1.0, 1.4, true],
        ]),
        0,
        2,
      ],
      [
        'a buffer wide enough to close the gaps',
        words([
          ['a', 0.0, 0.4, true],
          ['b', 0.45, 0.9, false],
          ['c', 0.95, 1.4, true],
        ]),
        0.5,
        2,
      ],
    ];

    it.each(cases)('%s', (_name, editorWords, buffer, duration) => {
      const request = buildCutRequest(JOB_ID, editorWords, buffer, duration);

      // The editor merges runs whose padded edges touch and the backend does not, so the
      // comparison is on the merged form — the video content the two agree on.
      expect(mergeTouching(backendKeepRanges(request.words!))).toEqual(
        computeKeptRanges(editorWords, buffer, duration),
      );
    });
  });
});

import { describe, it, expect } from 'vitest';
import {
  computeKeptRanges,
  computeStats,
  findWordIndexAtTime,
  findRangeIndexFrom,
  computeNextPreviewStep,
} from './editListLogic';
import type { EditorWord } from './types';

function word(index: number, text: string, start: number, end: number, kept = true): EditorWord {
  return { index, text, start, end, kept };
}

describe('computeKeptRanges', () => {
  it('returns an empty array for no words', () => {
    expect(computeKeptRanges([], 0.2, 10)).toEqual([]);
  });

  it('keeps a single contiguous run padded by the buffer', () => {
    const words = [word(0, 'hello', 1, 1.5), word(1, 'world', 1.5, 2)];
    // gapBefore = 1 (from 0), gapAfter = duration(10) - 2 = 8 -> both buffers clamp to 0.2
    expect(computeKeptRanges(words, 0.2, 10)).toEqual([{ start: 0.8, end: 2.2 }]);
  });

  it('does not pad past a real gap smaller than the buffer', () => {
    const words = [word(0, 'hi', 0.05, 0.3)];
    // gapBefore is only 0.05s (word starts near t=0), so startBuf clamps to that, not 0.2
    expect(computeKeptRanges(words, 0.2, 10)).toEqual([{ start: 0, end: 0.5 }]);
  });

  it('drops cut words with no surrounding silence (buffer pads gaps, not spoken cut words)', () => {
    const words = [word(0, 'a', 0, 0.5), word(1, 'b', 0.5, 1, false), word(2, 'c', 1, 1.5)];
    // The cut word occupies real time with no silence either side, so the
    // buffer (which is capped to the actual gap) contributes nothing here —
    // the two kept runs stay separate.
    const ranges = computeKeptRanges(words, 0.5, 10);
    expect(ranges).toEqual([
      { start: 0, end: 0.5 },
      { start: 1, end: 2 },
    ]);
  });

  it('merges two runs when the boundary between their padded ranges touches exactly', () => {
    // A zero-length cut marker (e.g. ASR punctuation token) with no gap
    // either side means both runs' buffered edges land on the same instant.
    const words = [
      word(0, 'a', 0, 0.5),
      word(1, '', 0.5, 0.5, false),
      word(2, 'c', 0.5, 1),
    ];
    // Run 2 is also the last word, so its trailing edge picks up its own
    // 0.5s buffer against the end of the video (end = 1 + 0.5).
    expect(computeKeptRanges(words, 0.5, 10)).toEqual([{ start: 0, end: 1.5 }]);
  });

  it('keeps separate ranges when the cut gap is larger than the buffer', () => {
    const words = [
      word(0, 'a', 0, 0.5),
      word(1, 'b', 5, 5.5, false),
      word(2, 'c', 10, 10.5),
    ];
    const ranges = computeKeptRanges(words, 0.2, 20);
    expect(ranges).toEqual([
      { start: 0, end: 0.7 },
      { start: 9.8, end: 10.7 },
    ]);
  });

  it('respects manual start/end overrides and bypasses the buffer on that edge', () => {
    const words: EditorWord[] = [
      { ...word(0, 'a', 1, 1.5), startOverride: 0.2 },
      { ...word(1, 'b', 1.5, 2), endOverride: 3 },
    ];
    expect(computeKeptRanges(words, 0.2, 10)).toEqual([{ start: 0.2, end: 3 }]);
  });

  it('clamps the trailing edge to duration when there is no next word', () => {
    const words = [word(0, 'last', 9.9, 9.95)];
    expect(computeKeptRanges(words, 0.5, 10)).toEqual([{ start: 9.4, end: 10 }]);
  });
});

describe('computeStats', () => {
  it('sums kept duration and derives cut duration from total', () => {
    const ranges = [{ start: 0, end: 2 }, { start: 5, end: 6 }];
    expect(computeStats(ranges, 10)).toEqual({ keptSeconds: 3, cutSeconds: 7, rangeCount: 2 });
  });

  it('never returns a negative cut duration', () => {
    const ranges = [{ start: 0, end: 12 }];
    expect(computeStats(ranges, 10).cutSeconds).toBe(0);
  });
});

describe('findWordIndexAtTime', () => {
  const words = [word(0, 'a', 0, 1), word(1, 'b', 1, 2), word(2, 'c', 2, 3)];

  it('finds the word covering the given time (start inclusive, end exclusive)', () => {
    expect(findWordIndexAtTime(words, 1)).toBe(1);
    expect(findWordIndexAtTime(words, 1.9)).toBe(1);
  });

  it('returns -1 when no word covers the time', () => {
    expect(findWordIndexAtTime(words, 5)).toBe(-1);
  });
});

describe('findRangeIndexFrom', () => {
  const ranges = [{ start: 0, end: 1 }, { start: 2, end: 3 }, { start: 4, end: 5 }];

  it('finds the first range that has not finished by the given time', () => {
    expect(findRangeIndexFrom(ranges, 2.5)).toBe(1);
  });

  it('falls back to the first range when time is past everything', () => {
    expect(findRangeIndexFrom(ranges, 10)).toBe(0);
  });
});

describe('computeNextPreviewStep', () => {
  const ranges = [{ start: 0, end: 2 }, { start: 5, end: 7 }];

  it('keeps playing while inside the current range', () => {
    expect(computeNextPreviewStep(ranges, 0, 1)).toEqual({ action: 'continue' });
  });

  it('advances to the next range once within the end threshold', () => {
    expect(computeNextPreviewStep(ranges, 0, 1.95)).toEqual({
      action: 'advance',
      nextIndex: 1,
      seekTo: 5,
    });
  });

  it('stops when the last range finishes', () => {
    expect(computeNextPreviewStep(ranges, 1, 6.95)).toEqual({ action: 'stop' });
  });

  it('stops if there is no current range (defensive)', () => {
    expect(computeNextPreviewStep(ranges, 5, 1)).toEqual({ action: 'stop' });
  });
});

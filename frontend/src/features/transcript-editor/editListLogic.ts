import type { EditorWord, KeptRange } from './types';

/**
 * Turn per-word kept/cut flags into merged [start, end) ranges of source
 * video time to keep, padding each kept run's edges by `bufferSeconds` (but
 * never past a real gap, and never past a manual scrubber override).
 *
 * Ported from review.html's computeClipEditList — same buffer-then-merge
 * algorithm, generalised to a single video rather than a clip array.
 */
export function computeKeptRanges(
  words: EditorWord[],
  bufferSeconds: number,
  durationSeconds: number,
): KeptRange[] {
  if (!words.length) return [];
  const dur = durationSeconds || Number.MAX_SAFE_INTEGER;
  const raw: KeptRange[] = [];

  let i = 0;
  while (i < words.length) {
    if (!words[i].kept) {
      i++;
      continue;
    }
    let j = i;
    while (j < words.length && words[j].kept) j++;
    // [i, j) is a contiguous run of kept words.

    const gapBefore = i > 0 ? Math.max(0, words[i].start - words[i - 1].end) : words[i].start;
    const startBuf = Math.min(bufferSeconds, gapBefore);

    const gapAfter =
      j < words.length
        ? Math.max(0, words[j].start - words[j - 1].end)
        : Math.max(0, dur - words[j - 1].end);
    const endBuf = Math.min(bufferSeconds, gapAfter);

    const startOverride = words[i].startOverride;
    const endOverride = words[j - 1].endOverride;
    const wStart = startOverride ?? words[i].start;
    const wEnd = endOverride ?? words[j - 1].end;

    const finalStart = startOverride != null ? startOverride : round3(Math.max(0, wStart - startBuf));
    const finalEnd = endOverride != null ? endOverride : round3(Math.min(dur, wEnd + endBuf));

    raw.push({ start: finalStart, end: finalEnd });
    i = j;
  }

  const merged: KeptRange[] = [];
  for (const seg of raw) {
    const prev = merged[merged.length - 1];
    if (prev && seg.start <= prev.end) {
      prev.end = Math.max(prev.end, seg.end);
    } else {
      merged.push({ ...seg });
    }
  }
  return merged;
}

function round3(n: number): number {
  return +n.toFixed(3);
}

export interface EditorStats {
  keptSeconds: number;
  cutSeconds: number;
  rangeCount: number;
}

export function computeStats(ranges: KeptRange[], durationSeconds: number): EditorStats {
  const keptSeconds = ranges.reduce((total, r) => total + (r.end - r.start), 0);
  const cutSeconds = Math.max(0, durationSeconds - keptSeconds);
  return { keptSeconds, cutSeconds, rangeCount: ranges.length };
}

/** Index of the word whose [start, end) window contains time `t`, or -1. */
export function findWordIndexAtTime(words: EditorWord[], t: number): number {
  return words.findIndex((w) => t >= w.start && t < w.end);
}

/** Index of the first range that hasn't finished playing by `fromTime` —
 * i.e. the range to resume/start preview playback from. Falls back to the
 * first range if `fromTime` is past everything (mirrors review.html's
 * `startSingleClipPreview`). */
export function findRangeIndexFrom(ranges: KeptRange[], fromTime: number): number {
  const idx = ranges.findIndex((r) => r.end > fromTime);
  return idx === -1 ? 0 : idx;
}

export type PreviewStep =
  | { action: 'continue' }
  | { action: 'advance'; nextIndex: number; seekTo: number }
  | { action: 'stop' };

/**
 * Decide what the preview player should do on a timeupdate tick, given the
 * range currently being played. Mirrors review.html's inline timeupdate
 * handler as a pure, testable function.
 */
export function computeNextPreviewStep(
  ranges: KeptRange[],
  currentIndex: number,
  currentTime: number,
  endThreshold = 0.08,
): PreviewStep {
  const current = ranges[currentIndex];
  if (!current) return { action: 'stop' };
  if (currentTime < current.end - endThreshold) return { action: 'continue' };

  const nextIndex = currentIndex + 1;
  if (nextIndex >= ranges.length) return { action: 'stop' };
  return { action: 'advance', nextIndex, seekTo: ranges[nextIndex].start };
}

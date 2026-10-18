import { describe, it, expect } from 'vitest';
import reducer, {
  type EditorState,
  transcriptLoaded,
  wordKeptSet,
  wordRangeKeptSet,
  segmentToggled,
  allWordsKeptSet,
  bufferSecondsSet,
  segmentOverrideSet,
  segmentOverrideCleared,
  editListApplied,
  fillerWordsRemoved,
} from './editorSlice';
import type { TranscriptDto } from './types';

const transcript: TranscriptDto = {
  durationSeconds: 10,
  words: [
    { text: 'so,', start: 0, end: 0.3 },
    { text: 'um,', start: 0.35, end: 0.6 },
    { text: 'hello', start: 0.9, end: 1.2 },
    { text: 'world', start: 1.25, end: 1.6 },
  ],
  segments: [{ start: 0, end: 1.6, text: 'so, um, hello world', wordIndices: [0, 1, 2, 3] }],
};

function loadedState(): EditorState {
  return reducer(undefined, transcriptLoaded({ transcriptId: 't1', transcript }));
}

describe('transcriptLoaded', () => {
  it('flattens words with index and defaults everything to kept', () => {
    const state = loadedState();
    expect(state.words).toHaveLength(4);
    expect(state.words.every((w) => w.kept)).toBe(true);
    expect(state.words[2]).toMatchObject({ index: 2, text: 'hello', start: 0.9, end: 1.2 });
    expect(state.durationSeconds).toBe(10);
  });
});

describe('wordKeptSet', () => {
  it('toggles a single word by index', () => {
    const state = reducer(loadedState(), wordKeptSet({ index: 1, kept: false }));
    expect(state.words[1].kept).toBe(false);
    expect(state.words[0].kept).toBe(true);
  });
});

describe('wordRangeKeptSet', () => {
  it('sets a contiguous range regardless of argument order', () => {
    const state = reducer(loadedState(), wordRangeKeptSet({ startIndex: 3, endIndex: 1, kept: false }));
    expect(state.words.map((w) => w.kept)).toEqual([true, false, false, false]);
  });
});

describe('segmentToggled', () => {
  it('cuts the whole segment when all words are currently kept', () => {
    const state = reducer(loadedState(), segmentToggled({ segmentIndex: 0 }));
    expect(state.words.every((w) => !w.kept)).toBe(true);
  });

  it('keeps the whole segment when any word is currently cut', () => {
    let state = loadedState();
    state = reducer(state, wordKeptSet({ index: 1, kept: false }));
    state = reducer(state, segmentToggled({ segmentIndex: 0 }));
    expect(state.words.every((w) => w.kept)).toBe(true);
  });
});

describe('allWordsKeptSet', () => {
  it('sets every word to the given kept value', () => {
    const state = reducer(loadedState(), allWordsKeptSet(false));
    expect(state.words.every((w) => !w.kept)).toBe(true);
  });
});

describe('bufferSecondsSet', () => {
  it('updates the buffer and clamps below zero to zero', () => {
    expect(reducer(loadedState(), bufferSecondsSet(0.5)).bufferSeconds).toBe(0.5);
    expect(reducer(loadedState(), bufferSecondsSet(-1)).bufferSeconds).toBe(0);
  });
});

describe('segment overrides', () => {
  it('sets start/end overrides on the segment boundary words', () => {
    const state = reducer(loadedState(), segmentOverrideSet({ segmentIndex: 0, start: 0.1, end: 2 }));
    expect(state.words[0].startOverride).toBe(0.1);
    expect(state.words[3].endOverride).toBe(2);
  });

  it('clears overrides on both boundary words', () => {
    let state = reducer(loadedState(), segmentOverrideSet({ segmentIndex: 0, start: 0.1, end: 2 }));
    state = reducer(state, segmentOverrideCleared({ segmentIndex: 0 }));
    expect(state.words[0].startOverride).toBeUndefined();
    expect(state.words[3].endOverride).toBeUndefined();
  });

  it('ignores NaN values so a partially-typed input does not clobber state', () => {
    const state = reducer(loadedState(), segmentOverrideSet({ segmentIndex: 0, start: NaN }));
    expect(state.words[0].startOverride).toBeUndefined();
  });
});

describe('editListApplied', () => {
  it('marks words kept only if they overlap a supplied range, and clears overrides', () => {
    let state = loadedState();
    state = reducer(state, segmentOverrideSet({ segmentIndex: 0, start: 0.1 }));
    state = reducer(state, editListApplied([{ start: 0.8, end: 1.7 }]));
    // words 0 (0-0.3) and 1 (0.35-0.6) don't overlap [0.8,1.7]; 2 and 3 do
    expect(state.words.map((w) => w.kept)).toEqual([false, false, true, true]);
    expect(state.words[0].startOverride).toBeUndefined();
  });
});

describe('fillerWordsRemoved', () => {
  it('cuts kept words matching the filler list and leaves the rest untouched', () => {
    const state = reducer(loadedState(), fillerWordsRemoved());
    // 'so,' normalizes to 'so' (not a filler by design), 'um,' normalizes to 'um' (is a filler)
    expect(state.words.map((w) => w.kept)).toEqual([true, false, true, true]);
  });
});

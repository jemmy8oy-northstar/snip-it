import { createSlice, createSelector, type PayloadAction } from '@reduxjs/toolkit';
import type { RootState } from '../../store';
import type { EditorSegment, EditorWord, KeptRange, TranscriptDto } from './types';
import { computeKeptRanges, computeStats } from './editListLogic';
import { findFillerWordIndices } from './fillerWords';

export interface EditorState {
  transcriptId: string | null;
  durationSeconds: number;
  words: EditorWord[];
  segments: EditorSegment[];
  bufferSeconds: number;
}

const initialState: EditorState = {
  transcriptId: null,
  durationSeconds: 0,
  words: [],
  segments: [],
  bufferSeconds: 0.2, // matches review.html's default 200ms
};

function segmentEdgeWords(state: EditorState, segmentIndex: number): [EditorWord, EditorWord] | null {
  const seg = state.segments[segmentIndex];
  if (!seg || !seg.wordIndices.length) return null;
  const first = state.words[seg.wordIndices[0]];
  const last = state.words[seg.wordIndices[seg.wordIndices.length - 1]];
  return [first, last];
}

const editorSlice = createSlice({
  name: 'transcriptEditor',
  initialState,
  reducers: {
    transcriptLoaded(state, action: PayloadAction<{ transcriptId: string; transcript: TranscriptDto }>) {
      const { transcriptId, transcript } = action.payload;
      state.transcriptId = transcriptId;
      state.durationSeconds = transcript.durationSeconds;
      state.words = transcript.words.map((w, index) => ({ ...w, index, kept: true }));
      state.segments = transcript.segments.map((s) => ({ ...s }));
    },

    wordKeptSet(state, action: PayloadAction<{ index: number; kept: boolean }>) {
      const word = state.words[action.payload.index];
      if (word) word.kept = action.payload.kept;
    },

    /** Drag-select across words — sets a contiguous index range to one kept value. */
    wordRangeKeptSet(state, action: PayloadAction<{ startIndex: number; endIndex: number; kept: boolean }>) {
      const { startIndex, endIndex, kept } = action.payload;
      const [lo, hi] = startIndex <= endIndex ? [startIndex, endIndex] : [endIndex, startIndex];
      for (let i = lo; i <= hi; i++) {
        if (state.words[i]) state.words[i].kept = kept;
      }
    },

    segmentToggled(state, action: PayloadAction<{ segmentIndex: number }>) {
      const seg = state.segments[action.payload.segmentIndex];
      if (!seg) return;
      const allKept = seg.wordIndices.every((wi) => state.words[wi]?.kept);
      seg.wordIndices.forEach((wi) => {
        if (state.words[wi]) state.words[wi].kept = !allKept;
      });
    },

    allWordsKeptSet(state, action: PayloadAction<boolean>) {
      state.words.forEach((w) => {
        w.kept = action.payload;
      });
    },

    bufferSecondsSet(state, action: PayloadAction<number>) {
      state.bufferSeconds = Math.max(0, action.payload);
    },

    /** Scrubber "Apply" — set an exact start/end override on a segment's boundary words. */
    segmentOverrideSet(
      state,
      action: PayloadAction<{ segmentIndex: number; start?: number; end?: number }>,
    ) {
      const edge = segmentEdgeWords(state, action.payload.segmentIndex);
      if (!edge) return;
      const [first, last] = edge;
      if (action.payload.start != null && !Number.isNaN(action.payload.start)) {
        first.startOverride = action.payload.start;
      }
      if (action.payload.end != null && !Number.isNaN(action.payload.end)) {
        last.endOverride = action.payload.end;
      }
    },

    /** Scrubber "Clear override". */
    segmentOverrideCleared(state, action: PayloadAction<{ segmentIndex: number }>) {
      const edge = segmentEdgeWords(state, action.payload.segmentIndex);
      if (!edge) return;
      const [first, last] = edge;
      delete first.startOverride;
      delete last.endOverride;
    },

    /**
     * Restore kept/cut state from an externally-supplied list of kept
     * ranges (e.g. pasted/loaded edit-list JSON, or the "Apply" button on
     * the JSON panel). A word is kept if it overlaps any kept range.
     * Mirrors review.html's applyEditList.
     */
    editListApplied(state, action: PayloadAction<KeptRange[]>) {
      const ranges = action.payload;
      state.words.forEach((w) => {
        w.kept = ranges.some((r) => w.start < r.end && w.end > r.start);
        delete w.startOverride;
        delete w.endOverride;
      });
    },

    /** One-click filler-word removal — cuts every kept word that matches the filler list. */
    fillerWordsRemoved(state) {
      const indices = findFillerWordIndices(state.words);
      indices.forEach((i) => {
        state.words[i].kept = false;
      });
    },
  },
});

export const {
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
} = editorSlice.actions;

export default editorSlice.reducer;

// ── Selectors ────────────────────────────────────────────────────────────

export const selectEditorState = (state: RootState) => state.transcriptEditor;
export const selectWords = (state: RootState) => state.transcriptEditor.words;
export const selectSegments = (state: RootState) => state.transcriptEditor.segments;
export const selectBufferSeconds = (state: RootState) => state.transcriptEditor.bufferSeconds;
export const selectDurationSeconds = (state: RootState) => state.transcriptEditor.durationSeconds;

export const selectKeptRanges = createSelector(
  [selectWords, selectBufferSeconds, selectDurationSeconds],
  (words, buffer, duration) => computeKeptRanges(words, buffer, duration),
);

export const selectStats = createSelector(
  [selectKeptRanges, selectDurationSeconds],
  (ranges, duration) => computeStats(ranges, duration),
);

import { describe, expect, it } from 'vitest';
import type { Transcript as ApiTranscript } from '../../../api/generatedApi';
import { toEditorTranscript } from './transcriptAdapter';
import { mockApiTranscript, MOCK_TRANSCRIPTION_JOB_ID } from '../fixtures/mockTranscript';

/** The adapter only ever sees a well-formed payload now, so every case here is a real
 *  grouping decision rather than a defence against the contract. */
function apiTranscript(rest: Omit<ApiTranscript, 'transcriptionJobId'>): ApiTranscript {
  return { transcriptionJobId: MOCK_TRANSCRIPTION_JOB_ID, ...rest };
}

describe('toEditorTranscript', () => {
  it('keeps every word and assigns each to exactly one segment', () => {
    const result = toEditorTranscript(mockApiTranscript);

    expect(result.words).toHaveLength(mockApiTranscript.words.length);

    const assigned = result.segments.flatMap((segment) => segment.wordIndices);
    expect(assigned.slice().sort((a, b) => a - b)).toEqual(
      result.words.map((_, index) => index),
    );
    expect(new Set(assigned).size).toBe(assigned.length);
  });

  it('reproduces the segment grouping the backend sent', () => {
    const result = toEditorTranscript(mockApiTranscript);

    // The fixture's three segments are separated by real silence, so timestamp-derived
    // membership should land on exactly the original 11 / 11 / 9 split.
    expect(result.segments.map((segment) => segment.wordIndices.length)).toEqual([11, 11, 9]);
    expect(result.segments[0].text).toContain('new pipeline');
  });

  it('assigns a word falling in a gap between segments to the nearest one', () => {
    const result = toEditorTranscript(
      apiTranscript({
        durationSeconds: 10,
        segments: [
          { index: 0, start: 0, end: 2, text: 'first' },
          { index: 1, start: 6, end: 8, text: 'second' },
        ],
        // Midpoint 3.1 sits in the 2–6 gap, 1.1s from the first segment and 2.9s from the
        // second, so it belongs to the first.
        words: [
          { text: 'a', start: 0.1, end: 0.5, kept: true },
          { text: 'orphan', start: 3.0, end: 3.2, kept: true },
          { text: 'b', start: 6.1, end: 6.5, kept: true },
        ],
      }),
    );

    expect(result.segments[0].wordIndices).toEqual([0, 1]);
    expect(result.segments[1].wordIndices).toEqual([2]);
  });

  it('assigns an orphan word to the later segment when that is the nearer one', () => {
    const result = toEditorTranscript(
      apiTranscript({
        durationSeconds: 10,
        segments: [
          { index: 0, start: 0, end: 2, text: 'first' },
          { index: 1, start: 6, end: 8, text: 'second' },
        ],
        // Midpoint 5.1: 3.1s past the first segment, 0.9s before the second.
        words: [
          { text: 'a', start: 0.1, end: 0.5, kept: true },
          { text: 'orphan', start: 5.0, end: 5.2, kept: true },
        ],
      }),
    );

    expect(result.segments[0].wordIndices).toEqual([0]);
    expect(result.segments[1].wordIndices).toEqual([1]);
  });

  it('synthesises one segment when the transcript has words but no segments', () => {
    const result = toEditorTranscript(
      apiTranscript({
        durationSeconds: 5,
        segments: [],
        words: [
          { text: 'hello', start: 0.2, end: 0.6, kept: true },
          { text: 'world', start: 0.7, end: 1.1, kept: true },
        ],
      }),
    );

    expect(result.segments).toHaveLength(1);
    expect(result.segments[0]).toMatchObject({ start: 0.2, end: 1.1, text: 'hello world' });
    expect(result.segments[0].wordIndices).toEqual([0, 1]);
  });

  it('drops segments that end up with no words rather than rendering empty rows', () => {
    const result = toEditorTranscript(
      apiTranscript({
        durationSeconds: 10,
        segments: [
          { index: 0, start: 0, end: 2, text: 'has words' },
          { index: 1, start: 8, end: 9, text: 'silent' },
        ],
        words: [{ text: 'a', start: 0.1, end: 0.5, kept: true }],
      }),
    );

    expect(result.segments).toHaveLength(1);
    expect(result.segments[0].text).toBe('has words');
  });

  it('falls back to the last word end when the duration is zero', () => {
    const result = toEditorTranscript(
      apiTranscript({
        durationSeconds: 0,
        segments: [{ index: 0, start: 0, end: 3, text: 's' }],
        words: [
          { text: 'a', start: 0.1, end: 0.5, kept: true },
          { text: 'b', start: 0.6, end: 2.4, kept: true },
        ],
      }),
    );

    expect(result.durationSeconds).toBe(2.4);
  });

  it('returns no segments for an empty transcript', () => {
    const result = toEditorTranscript(
      apiTranscript({ durationSeconds: 0, segments: [], words: [] }),
    );

    expect(result).toEqual({ durationSeconds: 0, words: [], segments: [] });
  });
});

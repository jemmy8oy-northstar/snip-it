import { describe, it, expect, vi } from 'vitest';
import { fireEvent, screen } from '@testing-library/react';
import { renderWithStore } from '../../../test/renderWithStore';
import { TranscriptWords } from './TranscriptWords';
import type { EditorSegment, EditorWord } from '../types';

function makeState() {
  const words: EditorWord[] = [
    { index: 0, text: 'hello', start: 0, end: 0.3, kept: true },
    { index: 1, text: 'brave', start: 0.3, end: 0.6, kept: true },
    { index: 2, text: 'new', start: 0.6, end: 0.9, kept: true },
    { index: 3, text: 'world', start: 0.9, end: 1.2, kept: true },
  ];
  const segments: EditorSegment[] = [{ start: 0, end: 1.2, text: 'hello brave new world', wordIndices: [0, 1, 2, 3] }];
  return { words, segments, transcriptId: 't1', durationSeconds: 1.2, bufferSeconds: 0.2 };
}

function setup() {
  return renderWithStore(
    <TranscriptWords activeWordIndex={null} onPlaySegment={vi.fn()} onOpenScrubber={vi.fn()} />,
    makeState(),
  );
}

describe('TranscriptWords — word toggle', () => {
  it('clicking a kept word cuts it, and clicking again restores it', () => {
    const { store } = setup();
    const word = screen.getByTestId('word-1');
    expect(word.className).not.toContain('cut');

    fireEvent.mouseDown(word);
    expect(store.getState().transcriptEditor.words[1].kept).toBe(false);

    fireEvent.mouseUp(document);
    fireEvent.mouseDown(word);
    expect(store.getState().transcriptEditor.words[1].kept).toBe(true);
  });
});

describe('TranscriptWords — drag-select', () => {
  it('applies the opposite of the starting word\'s kept state to every word dragged over', () => {
    const { store } = setup();
    const w0 = screen.getByTestId('word-0');
    const w1 = screen.getByTestId('word-1');
    const w2 = screen.getByTestId('word-2');

    // start the drag on word 0 (kept -> becomes cut), drag across 1 and 2
    fireEvent.mouseDown(w0);
    fireEvent.mouseEnter(w1);
    fireEvent.mouseEnter(w2);
    fireEvent.mouseUp(document);

    const kept = store.getState().transcriptEditor.words.map((w) => w.kept);
    expect(kept).toEqual([false, false, false, true]); // word 3 untouched
  });

  it('stops applying drag state after mouseup fires anywhere in the document', () => {
    const { store } = setup();
    const w0 = screen.getByTestId('word-0');
    const w3 = screen.getByTestId('word-3');

    fireEvent.mouseDown(w0);
    fireEvent.mouseUp(document);
    fireEvent.mouseEnter(w3); // should NOT toggle — drag already ended

    expect(store.getState().transcriptEditor.words[3].kept).toBe(true);
  });
});

describe('TranscriptWords — segment toggle', () => {
  it('cuts every word in the segment when all are currently kept', () => {
    const { store } = setup();
    fireEvent.click(screen.getByText('toggle segment'));
    expect(store.getState().transcriptEditor.words.every((w) => !w.kept)).toBe(true);
  });

  it('keeps every word in the segment when at least one is currently cut', () => {
    const { store } = setup();
    fireEvent.mouseDown(screen.getByTestId('word-0'));
    fireEvent.mouseUp(document);
    expect(store.getState().transcriptEditor.words[0].kept).toBe(false);

    fireEvent.click(screen.getByText('toggle segment'));
    expect(store.getState().transcriptEditor.words.every((w) => w.kept)).toBe(true);
  });
});

describe('TranscriptWords — segment play button', () => {
  it('calls onPlaySegment with the segment index and start time', () => {
    const onPlaySegment = vi.fn();
    renderWithStore(
      <TranscriptWords activeWordIndex={null} onPlaySegment={onPlaySegment} onOpenScrubber={vi.fn()} />,
      makeState(),
    );
    fireEvent.click(screen.getByLabelText('Play segment 1 from here'));
    expect(onPlaySegment).toHaveBeenCalledWith(0, 0);
  });
});

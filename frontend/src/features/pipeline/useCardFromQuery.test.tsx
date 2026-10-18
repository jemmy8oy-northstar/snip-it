import { describe, it, expect, beforeEach } from 'vitest';
import { renderHook } from '@testing-library/react';
import { MemoryRouter } from 'react-router-dom';
import type { ReactNode } from 'react';
import { addCard, emptyBoard } from './pipeline';
import { loadBoard, saveBoard } from './storage';
import { useCardFromQuery } from './useCardFromQuery';

const at = (url: string) =>
  function Wrapper({ children }: { children: ReactNode }) {
    return <MemoryRouter initialEntries={[url]}>{children}</MemoryRouter>;
  };

beforeEach(() => {
  window.localStorage.clear();
  saveBoard(addCard(emptyBoard(), { title: 'Intro' }, 'a', 1));
});

describe('useCardFromQuery', () => {
  it('reports a transcribed take and a finished cut back to the card', () => {
    const { result } = renderHook(useCardFromQuery, { wrapper: at('/editor?card=a') });
    expect(result.current.query).toBe('?card=a');

    result.current.onTranscribed('job-1');
    expect(loadBoard().cards[0]).toMatchObject({ stage: 'recorded', transcriptionJobId: 'job-1' });

    result.current.onCut();
    expect(loadBoard().cards[0].stage).toBe('cut');
  });

  it.each(['/editor', '/editor?card=gone'])('%s touches nothing', (url) => {
    const before = window.localStorage.getItem('snipit.pipeline.v1');
    const { result } = renderHook(useCardFromQuery, { wrapper: at(url) });
    expect(result.current).toMatchObject({ cardId: null, query: '' });
    result.current.onTranscribed('job-1');
    result.current.onCut();
    expect(window.localStorage.getItem('snipit.pipeline.v1')).toBe(before);
  });
});

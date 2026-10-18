import { describe, it, expect } from 'vitest';
import { isFillerWord, normalizeWord, findFillerWordIndices } from './fillerWords';

describe('normalizeWord', () => {
  it('lowercases and strips surrounding punctuation', () => {
    expect(normalizeWord('Um,')).toBe('um');
    expect(normalizeWord(' "Uh--')).toBe('uh');
  });

  it('preserves internal punctuation like apostrophes', () => {
    expect(normalizeWord("don't")).toBe("don't");
  });
});

describe('isFillerWord', () => {
  it('matches known disfluencies regardless of case/punctuation', () => {
    expect(isFillerWord('Um,')).toBe(true);
    expect(isFillerWord('uh')).toBe(true);
    expect(isFillerWord('Erm.')).toBe(true);
  });

  it('does not flag ordinary words that are sometimes fillers in speech', () => {
    // Deliberately conservative — "like"/"so"/"actually" can be load-bearing.
    expect(isFillerWord('like')).toBe(false);
    expect(isFillerWord('so')).toBe(false);
    expect(isFillerWord('actually')).toBe(false);
  });
});

describe('findFillerWordIndices', () => {
  it('returns indices of kept filler words only', () => {
    const words = [
      { text: 'so', kept: true },
      { text: 'um', kept: true },
      { text: 'hello', kept: true },
      { text: 'uh', kept: false }, // already cut — not reported again
    ];
    expect(findFillerWordIndices(words)).toEqual([1]);
  });

  it('returns an empty array when there are no fillers', () => {
    expect(findFillerWordIndices([{ text: 'hello', kept: true }])).toEqual([]);
  });
});

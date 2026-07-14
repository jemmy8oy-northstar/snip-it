// One-click filler-word removal (v1 plan item 5). review.html has no filler
// list to port — this is new. Kept deliberately conservative: only exact
// disfluency sounds, not words like "like"/"so"/"actually" that are filler
// in some sentences but load-bearing in others. False positives there would
// silently mangle a user's sentence, which is worse than under-removing.
export const DEFAULT_FILLER_WORDS = [
  'um',
  'umm',
  'ummm',
  'uh',
  'uhh',
  'erm',
  'er',
  'ah',
  'hmm',
  'mhm',
] as const;

const fillerSet = new Set<string>(DEFAULT_FILLER_WORDS);

/** Strip surrounding punctuation and case before comparing against the filler list. */
export function normalizeWord(text: string): string {
  return text
    .trim()
    .toLowerCase()
    .replace(/^[^a-z0-9']+|[^a-z0-9']+$/gi, '');
}

export function isFillerWord(text: string, fillers: ReadonlySet<string> = fillerSet): boolean {
  return fillers.has(normalizeWord(text));
}

/**
 * Return the set of word indices that are filler words and currently kept.
 * Pure — callers decide how to apply this (e.g. dispatch a "cut these
 * indices" action) so the logic stays independent of state shape.
 */
export function findFillerWordIndices(
  words: ReadonlyArray<{ text: string; kept: boolean }>,
  fillers: ReadonlySet<string> = fillerSet,
): number[] {
  const indices: number[] = [];
  words.forEach((w, i) => {
    if (w.kept && isFillerWord(w.text, fillers)) indices.push(i);
  });
  return indices;
}

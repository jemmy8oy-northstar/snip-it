/**
 * The video pipeline board — pure logic. No React, no storage: every function takes a board and
 * returns a new one, so the rules (BEH-PIPE-1..3, 5, 6) are unit-testable without a DOM.
 */

export const STAGES = ['idea', 'scripted', 'recorded', 'cut', 'published'] as const;
export type Stage = (typeof STAGES)[number];

export const STAGE_LABELS: Record<Stage, string> = {
  idea: 'Idea',
  scripted: 'Scripted',
  recorded: 'Recorded',
  cut: 'Cut',
  published: 'Published',
};

export interface Card {
  id: string;
  title: string;
  notes: string;
  script: string;
  stage: Stage;
  createdAt: number;
  /** Where the finished video lives (BEH-PIPE-6). Empty until the creator pastes one. */
  publishedUrl: string;
  /** The take's transcript in the editor (BEH-PIPE-5). Empty until a take is uploaded from the card. */
  transcriptionJobId: string;
}

export interface Board {
  cards: Card[];
}

export const emptyBoard = (): Board => ({ cards: [] });

export function isStage(value: unknown): value is Stage {
  return typeof value === 'string' && (STAGES as readonly string[]).includes(value);
}

export function newId(): string {
  if (typeof crypto !== 'undefined' && typeof crypto.randomUUID === 'function') {
    return crypto.randomUUID();
  }
  return `c${Date.now().toString(36)}${Math.random().toString(36).slice(2, 8)}`;
}

/** Title is the only required field. Returns the board unchanged when it is blank. */
export function addCard(
  board: Board,
  input: { title: string; notes?: string },
  id: string = newId(),
  now: number = Date.now(),
): Board {
  const title = input.title.trim();
  if (title === '') return board;
  const card: Card = {
    id,
    title,
    notes: (input.notes ?? '').trim(),
    script: '',
    stage: 'idea',
    createdAt: now,
    publishedUrl: '',
    transcriptionJobId: '',
  };
  return { cards: [...board.cards, card] };
}

function update(board: Board, id: string, fn: (card: Card) => Card): Board {
  return { cards: board.cards.map((c) => (c.id === id ? fn(c) : c)) };
}

/** Edit title/notes. A blank title is rejected (the card is left as it was). */
export function editCard(board: Board, id: string, patch: { title?: string; notes?: string }): Board {
  return update(board, id, (c) => {
    const title = patch.title === undefined ? c.title : patch.title.trim();
    if (title === '') return c;
    return { ...c, title, notes: patch.notes === undefined ? c.notes : patch.notes.trim() };
  });
}

/**
 * Save a script. The one automatic move in the whole board: a non-blank script saved on a card
 * in Idea moves it to Scripted. Every other stage stays put.
 */
export function setScript(board: Board, id: string, script: string): Board {
  return update(board, id, (c) => ({
    ...c,
    script,
    stage: c.stage === 'idea' && script.trim() !== '' ? 'scripted' : c.stage,
  }));
}

/** Explicit move, chosen by the creator. */
export function moveCard(board: Board, id: string, stage: Stage): Board {
  if (!isStage(stage)) return board;
  return update(board, id, (c) => ({ ...c, stage }));
}

/**
 * The link is rendered as an href, so only http(s) is accepted: a pasted `javascript:` URL would
 * otherwise run when clicked. Returns the normalised URL, or null when it is not one.
 */
export function normaliseLink(raw: string): string | null {
  const text = raw.trim();
  if (text === '') return null;
  let url: URL;
  try {
    url = new URL(text);
  } catch {
    return null;
  }
  return url.protocol === 'http:' || url.protocol === 'https:' ? url.href : null;
}

/**
 * Record where a video was published. A valid link moves the card to Published; an invalid one
 * leaves the board unchanged. A blank link clears it without moving the card.
 */
export function setPublished(board: Board, id: string, link: string): Board {
  if (link.trim() === '') return update(board, id, (c) => ({ ...c, publishedUrl: '' }));
  const url = normaliseLink(link);
  if (url === null) return board;
  return update(board, id, (c) => ({ ...c, publishedUrl: url, stage: 'published' }));
}

/** A card never moves backwards on its own: only the creator's explicit move does that. */
function atLeast(current: Stage, target: Stage): Stage {
  return STAGES.indexOf(current) >= STAGES.indexOf(target) ? current : target;
}

/**
 * A take uploaded from a card has been transcribed: remember its transcript so the card can reopen
 * the editor, and move the card to Recorded if it was still earlier than that.
 */
export function attachTake(board: Board, id: string, transcriptionJobId: string): Board {
  if (transcriptionJobId.trim() === '') return board;
  return update(board, id, (c) => ({ ...c, transcriptionJobId, stage: atLeast(c.stage, 'recorded') }));
}

/** The cut opened from a card finished exporting: the card moves to Cut, unless already Published. */
export function markCut(board: Board, id: string): Board {
  return update(board, id, (c) => ({ ...c, stage: atLeast(c.stage, 'cut') }));
}

/** Where a card's "Cut" link goes: its transcript if it has one, otherwise the upload. */
export function editorPath(card: Card): string {
  const base = card.transcriptionJobId ? `/editor/${encodeURIComponent(card.transcriptionJobId)}` : '/editor';
  return `${base}?card=${encodeURIComponent(card.id)}`;
}

export function deleteCard(board: Board, id: string): Board {
  return { cards: board.cards.filter((c) => c.id !== id) };
}

export function cardsInStage(board: Board, stage: Stage): Card[] {
  return board.cards.filter((c) => c.stage === stage);
}

/** Validate an unknown parsed value into a Board, dropping malformed cards. */
export function parseBoard(raw: unknown): Board {
  if (typeof raw !== 'object' || raw === null) return emptyBoard();
  const cards = (raw as { cards?: unknown }).cards;
  if (!Array.isArray(cards)) return emptyBoard();
  const out: Card[] = [];
  for (const item of cards) {
    if (typeof item !== 'object' || item === null) continue;
    const c = item as Record<string, unknown>;
    if (typeof c.id !== 'string' || typeof c.title !== 'string' || c.title.trim() === '') continue;
    out.push({
      id: c.id,
      title: c.title,
      notes: typeof c.notes === 'string' ? c.notes : '',
      script: typeof c.script === 'string' ? c.script : '',
      stage: isStage(c.stage) ? c.stage : 'idea',
      createdAt: typeof c.createdAt === 'number' ? c.createdAt : 0,
      publishedUrl: typeof c.publishedUrl === 'string' ? (normaliseLink(c.publishedUrl) ?? '') : '',
      transcriptionJobId: typeof c.transcriptionJobId === 'string' ? c.transcriptionJobId : '',
    });
  }
  return { cards: out };
}

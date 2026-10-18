import { describe, it, expect, beforeEach } from 'vitest';
import {
  addCard,
  cardsInStage,
  deleteCard,
  editCard,
  emptyBoard,
  moveCard,
  parseBoard,
  normaliseLink,
  setPublished,
  setScript,
} from './pipeline';
import { loadBoard, saveBoard, STORAGE_KEY } from './storage';

const withCard = () => addCard(emptyBoard(), { title: 'Intro', notes: 'hook' }, 'a', 1);

describe('addCard', () => {
  it('lands a new card in Idea with trimmed title and notes', () => {
    const b = addCard(emptyBoard(), { title: '  My video ', notes: ' n ' }, 'a', 1);
    expect(b.cards).toHaveLength(1);
    expect(b.cards[0]).toMatchObject({ id: 'a', title: 'My video', notes: 'n', script: '', stage: 'idea' });
  });

  it('notes are optional', () => {
    expect(addCard(emptyBoard(), { title: 'x' }, 'a').cards[0].notes).toBe('');
  });

  it('rejects an empty or whitespace title', () => {
    const b = emptyBoard();
    expect(addCard(b, { title: '' })).toBe(b);
    expect(addCard(b, { title: '   ' })).toBe(b);
  });
});

describe('setPublished', () => {
  it('a valid link is saved and moves the card to Published', () => {
    const b = setPublished(withCard(), 'a', ' https://youtu.be/abc ');
    expect(b.cards[0]).toMatchObject({ stage: 'published', publishedUrl: 'https://youtu.be/abc' });
  });

  it('rejects anything that is not an http(s) link, leaving the board as it was', () => {
    const b = withCard();
    for (const bad of ['javascript:alert(1)', 'not a link', 'ftp://x.test/v', 'data:text/html,hi']) {
      expect(setPublished(b, 'a', bad)).toBe(b);
    }
  });

  it('a blank link clears it without moving the card', () => {
    const published = setPublished(withCard(), 'a', 'https://youtu.be/abc');
    const b = setPublished(published, 'a', '  ');
    expect(b.cards[0]).toMatchObject({ stage: 'published', publishedUrl: '' });
  });

  it('new cards start with no link', () => {
    expect(withCard().cards[0].publishedUrl).toBe('');
  });
});

describe('normaliseLink', () => {
  it('accepts http and https only', () => {
    expect(normaliseLink('http://x.test/v')).toBe('http://x.test/v');
    expect(normaliseLink('JavaScript:alert(1)')).toBeNull();
    expect(normaliseLink('')).toBeNull();
  });
});

describe('setScript', () => {
  it('moves an Idea card to Scripted when a non-empty script is saved', () => {
    const b = setScript(withCard(), 'a', 'Hello world');
    expect(b.cards[0]).toMatchObject({ stage: 'scripted', script: 'Hello world' });
  });

  it('does not move a Recorded card', () => {
    const b = setScript(moveCard(withCard(), 'a', 'recorded'), 'a', 'Hello');
    expect(b.cards[0].stage).toBe('recorded');
    expect(b.cards[0].script).toBe('Hello');
  });

  it('does not move a card on an empty or whitespace script', () => {
    expect(setScript(withCard(), 'a', '').cards[0].stage).toBe('idea');
    expect(setScript(withCard(), 'a', '  \n ').cards[0].stage).toBe('idea');
  });

  it('does not pull a Published card back', () => {
    const b = setScript(moveCard(withCard(), 'a', 'published'), 'a', 'x');
    expect(b.cards[0].stage).toBe('published');
  });
});

describe('moveCard', () => {
  it('moves only by explicit choice, in any direction', () => {
    let b = moveCard(withCard(), 'a', 'cut');
    expect(cardsInStage(b, 'cut')).toHaveLength(1);
    b = moveCard(b, 'a', 'idea');
    expect(cardsInStage(b, 'idea')).toHaveLength(1);
  });

  it('editing title/notes never moves a card', () => {
    const b = editCard(moveCard(withCard(), 'a', 'cut'), 'a', { title: 'New', notes: 'z' });
    expect(b.cards[0]).toMatchObject({ stage: 'cut', title: 'New', notes: 'z' });
  });
});

describe('editCard', () => {
  it('rejects a blank title and leaves the card as it was', () => {
    const b = editCard(withCard(), 'a', { title: ' ', notes: 'changed' });
    expect(b.cards[0]).toMatchObject({ title: 'Intro', notes: 'hook' });
  });
});

describe('deleteCard', () => {
  it('removes only the named card', () => {
    let b = addCard(withCard(), { title: 'Two' }, 'b');
    b = deleteCard(b, 'a');
    expect(b.cards.map((c) => c.id)).toEqual(['b']);
  });
});

describe('parseBoard', () => {
  it('drops malformed cards and repairs unknown stages', () => {
    const b = parseBoard({
      cards: [null, { id: 1 }, { id: 'x', title: ' ' }, { id: 'ok', title: 'T', stage: 'bogus' }],
    });
    expect(b.cards).toHaveLength(1);
    expect(b.cards[0]).toMatchObject({ id: 'ok', stage: 'idea', notes: '', script: '', publishedUrl: '' });
  });

  it('keeps a stored http(s) link and drops a stored script link', () => {
    const b = parseBoard({
      cards: [
        { id: 'a', title: 'A', publishedUrl: 'https://youtu.be/abc' },
        { id: 'b', title: 'B', publishedUrl: 'javascript:alert(1)' },
      ],
    });
    expect(b.cards.map((c) => c.publishedUrl)).toEqual(['https://youtu.be/abc', '']);
  });

  it.each([null, 5, 'x', [], {}, { cards: 'no' }])('treats %j as an empty board', (v) => {
    expect(parseBoard(v).cards).toEqual([]);
  });
});

describe('storage', () => {
  beforeEach(() => window.localStorage.clear());

  it('round-trips a board', () => {
    const b = setScript(withCard(), 'a', 'script');
    expect(saveBoard(b)).toBe(true);
    expect(loadBoard()).toEqual(b);
  });

  it('missing key gives an empty board', () => {
    expect(loadBoard().cards).toEqual([]);
  });

  it('corrupt JSON gives an empty board instead of throwing', () => {
    window.localStorage.setItem(STORAGE_KEY, '{not json');
    expect(loadBoard().cards).toEqual([]);
  });

  it('valid JSON of the wrong shape gives an empty board', () => {
    window.localStorage.setItem(STORAGE_KEY, '"hello"');
    expect(loadBoard().cards).toEqual([]);
  });
});

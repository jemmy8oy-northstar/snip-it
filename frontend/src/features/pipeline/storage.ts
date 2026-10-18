import { emptyBoard, parseBoard, type Board } from './pipeline';

/** One versioned key: bump the suffix if the stored shape ever changes incompatibly. */
export const STORAGE_KEY = 'snipit.pipeline.v1';

/** Missing, unparseable or wrongly-shaped data (or no storage at all) yields an empty board. */
export function loadBoard(): Board {
  try {
    const raw = window.localStorage.getItem(STORAGE_KEY);
    if (raw === null) return emptyBoard();
    return parseBoard(JSON.parse(raw));
  } catch {
    return emptyBoard();
  }
}

/** Returns false when the browser refuses (private mode, quota) — the board still works in memory. */
export function saveBoard(board: Board): boolean {
  try {
    window.localStorage.setItem(STORAGE_KEY, JSON.stringify(board));
    return true;
  } catch {
    return false;
  }
}

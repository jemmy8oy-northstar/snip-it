import { useCallback } from 'react';
import { useSearchParams } from 'react-router-dom';
import { attachTake, markCut } from './pipeline';
import { loadBoard, updateStoredBoard } from './storage';

/**
 * The editor's side of BEH-PIPE-5. A card opens the editor with `?card=<id>`; this hook reads it
 * and lets the editor report back to that card. With no `card` (the bare /editor link, or a card
 * deleted since) every call is a no-op, so the editor works exactly as it did without the board.
 */
export function useCardFromQuery() {
  const [params] = useSearchParams();
  const raw = params.get('card');
  const cardId = raw && loadBoard().cards.some((c) => c.id === raw) ? raw : null;

  const onTranscribed = useCallback(
    (transcriptionJobId: string) => {
      if (cardId) updateStoredBoard((b) => attachTake(b, cardId, transcriptionJobId));
    },
    [cardId],
  );

  const onCut = useCallback(() => {
    if (cardId) updateStoredBoard((b) => markCut(b, cardId));
  }, [cardId]);

  /** Appended to editor navigation so the card survives the upload → editor hop. */
  const query = cardId ? `?card=${encodeURIComponent(cardId)}` : '';

  return { cardId, query, onTranscribed, onCut };
}

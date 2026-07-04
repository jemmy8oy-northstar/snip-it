import { useEffect, useRef } from 'react';
import { useDispatch, useSelector } from 'react-redux';
import type { AppDispatch } from '../../../store';
import { selectSegments, selectWords, segmentToggled, wordKeptSet } from '../editorSlice';

interface TranscriptWordsProps {
  activeWordIndex: number | null;
  onPlaySegment: (segmentIndex: number, fromTime: number) => void;
  onOpenScrubber: (segmentIndex: number, anchorEl: HTMLElement) => void;
}

/**
 * Renders every segment's words. Click a word to toggle it kept/cut;
 * mouse-down then drag over other words to toggle a range in one gesture
 * (the drag target value is the opposite of whatever word you started on,
 * same as review.html).
 */
export function TranscriptWords({ activeWordIndex, onPlaySegment, onOpenScrubber }: TranscriptWordsProps) {
  const dispatch = useDispatch<AppDispatch>();
  const words = useSelector(selectWords);
  const segments = useSelector(selectSegments);
  const dragKeptRef = useRef<boolean | null>(null);

  // A word's onMouseUp only fires if the release happens on a word span —
  // this catches releases anywhere else (matches review.html's global listener).
  useEffect(() => {
    const onMouseUp = () => {
      dragKeptRef.current = null;
    };
    document.addEventListener('mouseup', onMouseUp);
    return () => document.removeEventListener('mouseup', onMouseUp);
  }, []);

  return (
    <div className="editor-transcript">
      {segments.map((seg, si) => {
        if (!seg.wordIndices.length) return null;
        const firstWi = seg.wordIndices[0];
        const lastWi = seg.wordIndices[seg.wordIndices.length - 1];
        const hasOverride = words[firstWi]?.startOverride != null || words[lastWi]?.endOverride != null;

        return (
          <div className="editor-segment" key={si}>
            <div className="editor-segment-header">
              <span
                className="editor-seg-play"
                role="button"
                aria-label={`Play segment ${si + 1} from here`}
                onClick={(e) => {
                  e.stopPropagation();
                  onPlaySegment(si, seg.start);
                }}
              >
                ▶
              </span>
              <span
                className={'editor-seg-time' + (hasOverride ? ' has-override' : '')}
                role="button"
                title="Click to set custom start/end"
                onClick={(e) => {
                  e.stopPropagation();
                  onOpenScrubber(si, e.currentTarget as HTMLElement);
                }}
              >
                {formatTime(seg.start)}
              </span>
              <span
                className="editor-seg-toggle"
                role="button"
                onClick={() => dispatch(segmentToggled({ segmentIndex: si }))}
              >
                toggle segment
              </span>
            </div>
            <div className="editor-words">
              {seg.wordIndices.map((wi) => {
                const w = words[wi];
                if (!w) return null;
                return (
                  <span
                    key={wi}
                    data-testid={`word-${wi}`}
                    className={'editor-word' + (w.kept ? '' : ' cut') + (activeWordIndex === wi ? ' active' : '')}
                    onMouseDown={() => {
                      const nextKept = !w.kept;
                      dragKeptRef.current = nextKept;
                      dispatch(wordKeptSet({ index: wi, kept: nextKept }));
                    }}
                    onMouseEnter={() => {
                      if (dragKeptRef.current !== null) {
                        dispatch(wordKeptSet({ index: wi, kept: dragKeptRef.current }));
                      }
                    }}
                  >
                    {w.text}
                  </span>
                );
              })}
            </div>
          </div>
        );
      })}
    </div>
  );
}

function formatTime(seconds: number): string {
  if (seconds == null || Number.isNaN(seconds)) return '—';
  const m = Math.floor(seconds / 60);
  const s = (seconds % 60).toFixed(1).padStart(4, '0');
  return `${m}:${s}`;
}

import { useEffect, useRef, useState } from 'react';
import type { EditorWord } from '../types';

export interface ScrubberTarget {
  segmentIndex: number;
  firstWord: EditorWord;
  lastWord: EditorWord;
  anchorRect: DOMRect;
}

interface ScrubberPopoverProps {
  target: ScrubberTarget;
  videoRef: React.RefObject<HTMLVideoElement | null>;
  onApply: (segmentIndex: number, start: number, end: number) => void;
  onClear: (segmentIndex: number) => void;
  onClose: () => void;
}

/** Per-segment start/end override editor — port of review.html's scrubber-popover. */
export function ScrubberPopover({ target, videoRef, onApply, onClear, onClose }: ScrubberPopoverProps) {
  const { segmentIndex, firstWord, lastWord } = target;
  const [start, setStart] = useState((firstWord.startOverride ?? firstWord.start).toFixed(3));
  const [end, setEnd] = useState((lastWord.endOverride ?? lastWord.end).toFixed(3));
  const [playerNow, setPlayerNow] = useState(videoRef.current?.currentTime ?? 0);
  const popoverRef = useRef<HTMLDivElement>(null);

  const hasOverride = firstWord.startOverride != null || lastWord.endOverride != null;

  useEffect(() => {
    const video = videoRef.current;
    if (!video) return;
    const onTimeUpdate = () => setPlayerNow(video.currentTime);
    video.addEventListener('timeupdate', onTimeUpdate);
    return () => video.removeEventListener('timeupdate', onTimeUpdate);
  }, [videoRef]);

  useEffect(() => {
    const onMouseDown = (e: MouseEvent) => {
      if (popoverRef.current && !popoverRef.current.contains(e.target as Node)) onClose();
    };
    const onKeyDown = (e: KeyboardEvent) => {
      if (e.key === 'Escape') onClose();
    };
    document.addEventListener('mousedown', onMouseDown);
    document.addEventListener('keydown', onKeyDown);
    return () => {
      document.removeEventListener('mousedown', onMouseDown);
      document.removeEventListener('keydown', onKeyDown);
    };
  }, [onClose]);

  const { anchorRect } = target;

  return (
    <div
      ref={popoverRef}
      className="editor-scrubber"
      role="dialog"
      aria-label="Segment start/end override"
      style={{ top: anchorRect.bottom + 6, left: anchorRect.left }}
    >
      <div style={{ fontSize: 10, color: 'var(--text-secondary)', textAlign: 'center' }}>
        Player: {playerNow.toFixed(3)}s
      </div>
      <div className="editor-scrubber-row">
        <span className="editor-scrubber-label">Start</span>
        <input
          className="editor-scrubber-input"
          type="number"
          step="0.001"
          min="0"
          value={start}
          onChange={(e) => setStart(e.target.value)}
          aria-label="Start override"
        />
        <button className="editor-btn" type="button" onClick={() => setStart(playerNow.toFixed(3))}>
          Now
        </button>
      </div>
      <div className="editor-scrubber-row">
        <span className="editor-scrubber-label">End</span>
        <input
          className="editor-scrubber-input"
          type="number"
          step="0.001"
          min="0"
          value={end}
          onChange={(e) => setEnd(e.target.value)}
          aria-label="End override"
        />
        <button className="editor-btn" type="button" onClick={() => setEnd(playerNow.toFixed(3))}>
          Now
        </button>
      </div>
      <div className="editor-scrubber-actions">
        {hasOverride && (
          <button
            className="editor-btn"
            type="button"
            onClick={() => {
              onClear(segmentIndex);
              onClose();
            }}
          >
            Clear override
          </button>
        )}
        <button
          className="editor-btn"
          type="button"
          onClick={() => {
            const s = parseFloat(start);
            const e = parseFloat(end);
            onApply(segmentIndex, s, e);
            onClose();
          }}
        >
          Apply
        </button>
      </div>
    </div>
  );
}

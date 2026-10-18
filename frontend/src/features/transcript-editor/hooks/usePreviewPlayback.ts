import { useCallback, useEffect, useRef, useState } from 'react';
import type { KeptRange } from '../types';
import { computeNextPreviewStep, findRangeIndexFrom } from '../editListLogic';

/**
 * Drives a <video> element so it only ever plays the supplied kept ranges,
 * jumping over cut material — the "preview-kept-only playback" requirement.
 * Ported from review.html's previewQueue/timeupdate logic, reimplemented
 * as a hook so a component just does `const preview = usePreviewPlayback(videoRef, ranges)`.
 */
export function usePreviewPlayback(videoRef: React.RefObject<HTMLVideoElement | null>, ranges: KeptRange[]) {
  const [isPlaying, setIsPlaying] = useState(false);
  const rangeIndexRef = useRef(0);
  // Ranges recompute on every edit; keep a ref so the timeupdate handler
  // (registered once) always sees the latest value without re-subscribing.
  const rangesRef = useRef(ranges);
  rangesRef.current = ranges;

  const stop = useCallback(() => {
    setIsPlaying(false);
    videoRef.current?.pause();
  }, [videoRef]);

  const startFrom = useCallback(
    (time: number) => {
      const video = videoRef.current;
      const currentRanges = rangesRef.current;
      if (!video || !currentRanges.length) return;
      const idx = findRangeIndexFrom(currentRanges, time);
      rangeIndexRef.current = idx;
      const range = currentRanges[idx];
      video.currentTime = time > range.start ? time : range.start;
      setIsPlaying(true);
      void video.play();
    },
    [videoRef],
  );

  const start = useCallback(() => startFrom(0), [startFrom]);

  useEffect(() => {
    const video = videoRef.current;
    if (!video) return;

    const onTimeUpdate = () => {
      if (!isPlaying) return;
      const step = computeNextPreviewStep(rangesRef.current, rangeIndexRef.current, video.currentTime);
      if (step.action === 'continue') return;
      if (step.action === 'stop') {
        stop();
        return;
      }
      rangeIndexRef.current = step.nextIndex;
      video.currentTime = step.seekTo;
      void video.play();
    };
    const onEnded = () => stop();

    video.addEventListener('timeupdate', onTimeUpdate);
    video.addEventListener('ended', onEnded);
    return () => {
      video.removeEventListener('timeupdate', onTimeUpdate);
      video.removeEventListener('ended', onEnded);
    };
  }, [videoRef, isPlaying, stop]);

  return { isPlaying, start, startFrom, stop };
}

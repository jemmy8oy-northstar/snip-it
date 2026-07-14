import { useEffect, useRef, useState } from 'react';
import type { EditorWord } from '../types';
import { findWordIndexAtTime } from '../editListLogic';

/** Tracks which word index is under the playhead, for the "active word"
 * highlight during playback (review.html's highlightWord). */
export function useActiveWordIndex(videoRef: React.RefObject<HTMLVideoElement | null>, words: EditorWord[]) {
  const [activeIndex, setActiveIndex] = useState<number | null>(null);
  const wordsRef = useRef(words);
  wordsRef.current = words;

  useEffect(() => {
    const video = videoRef.current;
    if (!video) return;
    const onTimeUpdate = () => {
      const idx = findWordIndexAtTime(wordsRef.current, video.currentTime);
      setActiveIndex(idx === -1 ? null : idx);
    };
    video.addEventListener('timeupdate', onTimeUpdate);
    return () => video.removeEventListener('timeupdate', onTimeUpdate);
  }, [videoRef]);

  return activeIndex;
}

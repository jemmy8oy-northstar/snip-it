import { useEffect, useRef, useState } from 'react';
import { useDispatch, useSelector } from 'react-redux';
import type { AppDispatch } from '../../../store';
import { useGetTranscriptQuery, useSubmitCutRequestMutation } from '../api/transcriptApi';
import {
  allWordsKeptSet,
  bufferSecondsSet,
  editListApplied,
  fillerWordsRemoved,
  segmentOverrideCleared,
  segmentOverrideSet,
  selectBufferSeconds,
  selectKeptRanges,
  selectSegments,
  selectStats,
  selectWords,
  transcriptLoaded,
} from '../editorSlice';
import { usePreviewPlayback } from '../hooks/usePreviewPlayback';
import { useActiveWordIndex } from '../hooks/useActiveWordIndex';
import { TranscriptWords } from './TranscriptWords';
import { ScrubberPopover, type ScrubberTarget } from './ScrubberPopover';
import { EditorStats } from './EditorStats';
import { EditListPanel } from './EditListPanel';
import type { CutRequestDto, KeptRange } from '../types';
import './TranscriptEditor.css';

// Dev-only stand-in for the source video the backend will eventually
// return alongside the transcript (see the report for what's left to wire).
const MOCK_VIDEO_SRC = `${import.meta.env.BASE_URL}fixtures/mock-transcript-source.mp4`;
const MOCK_TRANSCRIPT_ID = 'mock-transcript-1';

export function TranscriptEditorPage() {
  const dispatch = useDispatch<AppDispatch>();
  const { data: transcript, isLoading, isError } = useGetTranscriptQuery({ transcriptId: MOCK_TRANSCRIPT_ID });
  const [submitCutRequest, { isLoading: isSubmitting }] = useSubmitCutRequestMutation();

  const words = useSelector(selectWords);
  const segments = useSelector(selectSegments);
  const bufferSeconds = useSelector(selectBufferSeconds);
  const ranges = useSelector(selectKeptRanges);
  const stats = useSelector(selectStats);

  const videoRef = useRef<HTMLVideoElement>(null);
  const [scrubberTarget, setScrubberTarget] = useState<ScrubberTarget | null>(null);

  const preview = usePreviewPlayback(videoRef, ranges);
  const activeWordIndex = useActiveWordIndex(videoRef, words);

  useEffect(() => {
    if (transcript) {
      dispatch(transcriptLoaded({ transcriptId: MOCK_TRANSCRIPT_ID, transcript }));
    }
  }, [transcript, dispatch]);

  if (isLoading) return <p style={{ color: 'var(--text-secondary)' }}>Loading transcript…</p>;
  if (isError || !transcript) return <p style={{ color: '#ef4444' }}>Could not load the transcript.</p>;
  if (!words.length) return <div className="editor-empty">No transcript loaded yet.</div>;

  const handlePlaySegment = (_segmentIndex: number, fromTime: number) => {
    preview.startFrom(fromTime);
  };

  const handleOpenScrubber = (segmentIndex: number, anchorEl: HTMLElement) => {
    const seg = segments[segmentIndex];
    const firstWord = words[seg.wordIndices[0]];
    const lastWord = words[seg.wordIndices[seg.wordIndices.length - 1]];
    setScrubberTarget({ segmentIndex, firstWord, lastWord, anchorRect: anchorEl.getBoundingClientRect() });
  };

  const handleApplyOverride = (segmentIndex: number, start: number, end: number) => {
    dispatch(segmentOverrideSet({ segmentIndex, start, end }));
  };

  const handleClearOverride = (segmentIndex: number) => {
    dispatch(segmentOverrideCleared({ segmentIndex }));
  };

  const handleApplyEditList = (parsed: KeptRange[]) => {
    dispatch(editListApplied(parsed));
  };

  const handleSubmitForExport = () => {
    const request: CutRequestDto = {
      transcriptId: MOCK_TRANSCRIPT_ID,
      bufferMs: Math.round(bufferSeconds * 1000),
      words: words.map((w) => ({ index: w.index, kept: w.kept })),
    };
    void submitCutRequest(request);
  };

  return (
    <div className="editor">
      <div className="editor-left">
        <video ref={videoRef} className="editor-video" src={MOCK_VIDEO_SRC} controls />

        <div className="editor-toolbar glass">
          <button
            className={'editor-btn-preview' + (preview.isPlaying ? ' playing' : '')}
            type="button"
            disabled={!ranges.length}
            onClick={() => (preview.isPlaying ? preview.stop() : preview.start())}
          >
            {preview.isPlaying ? '■ Stop' : '▶ Play Preview'}
          </button>
        </div>

        <EditorStats rangeCount={stats.rangeCount} keptSeconds={stats.keptSeconds} cutSeconds={stats.cutSeconds} />

        <div className="editor-toolbar glass">
          <button className="editor-btn" type="button" onClick={() => dispatch(allWordsKeptSet(true))}>
            Select All
          </button>
          <button className="editor-btn" type="button" onClick={() => dispatch(allWordsKeptSet(false))}>
            Deselect All
          </button>
          <button className="editor-btn" type="button" onClick={() => dispatch(fillerWordsRemoved())}>
            Remove filler words
          </button>
          <div className="editor-buffer" style={{ marginLeft: 'auto' }}>
            <label htmlFor="buffer-slider">Buffer</label>
            <input
              id="buffer-slider"
              type="range"
              min={0}
              max={500}
              step={10}
              value={Math.round(bufferSeconds * 1000)}
              onChange={(e) => dispatch(bufferSecondsSet(Number(e.target.value) / 1000))}
            />
            <span>{Math.round(bufferSeconds * 1000)}ms</span>
          </div>
        </div>

        <EditListPanel ranges={ranges} onApply={handleApplyEditList} />

        <button className="editor-btn" type="button" disabled={isSubmitting} onClick={handleSubmitForExport}>
          {isSubmitting ? 'Submitting…' : 'Send for export'}
        </button>
      </div>

      <TranscriptWords
        activeWordIndex={activeWordIndex}
        onPlaySegment={handlePlaySegment}
        onOpenScrubber={handleOpenScrubber}
      />

      {scrubberTarget && (
        <ScrubberPopover
          target={scrubberTarget}
          videoRef={videoRef}
          onApply={handleApplyOverride}
          onClear={handleClearOverride}
          onClose={() => setScrubberTarget(null)}
        />
      )}
    </div>
  );
}

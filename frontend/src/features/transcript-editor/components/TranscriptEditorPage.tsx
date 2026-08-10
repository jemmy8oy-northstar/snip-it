import { useEffect, useMemo, useRef, useState } from 'react';
import { useDispatch, useSelector } from 'react-redux';
import { useParams } from 'react-router-dom';
import type { AppDispatch } from '../../../store';
import {
  useGetCutJobQuery,
  useGetTranscriptQuery,
  useSubmitCutMutation,
} from '../../../api/generatedApi';
import { toEditorTranscript } from '../api/transcriptAdapter';
import { buildCutRequest } from '../api/cutRequest';
import { describeJobStatus } from '../api/jobStatus';
import {
  allWordsKeptSet,
  bufferSecondsSet,
  editListApplied,
  fillerWordsRemoved,
  segmentOverrideCleared,
  segmentOverrideSet,
  selectBufferSeconds,
  selectDurationSeconds,
  selectKeptRanges,
  selectSegments,
  selectStats,
  selectWords,
  transcriptLoaded,
} from '../editorSlice';
import { usePreviewPlayback } from '../hooks/usePreviewPlayback';
import { useActiveWordIndex } from '../hooks/useActiveWordIndex';
import { usePolledJob } from '../hooks/usePolledJob';
import { TranscriptWords } from './TranscriptWords';
import { ScrubberPopover, type ScrubberTarget } from './ScrubberPopover';
import { EditorStats } from './EditorStats';
import { EditListPanel } from './EditListPanel';
import { TranscriptUploadPanel } from './TranscriptUploadPanel';
import type { KeptRange } from '../types';
import './TranscriptEditor.css';

export function TranscriptEditorPage() {
  const { transcriptionJobId } = useParams<{ transcriptionJobId: string }>();

  // The bare /editor link in the navbar has no job to open, so it is the way in: upload a
  // file, and the panel navigates to /editor/:transcriptionJobId once transcription finishes.
  if (!transcriptionJobId) {
    return <TranscriptUploadPanel />;
  }

  return <TranscriptEditor transcriptionJobId={transcriptionJobId} />;
}

function TranscriptEditor({ transcriptionJobId }: { transcriptionJobId: string }) {
  const dispatch = useDispatch<AppDispatch>();
  const { data, isLoading, isError, error } = useGetTranscriptQuery({ id: transcriptionJobId });
  const [submitCut, { isLoading: isSubmitting, data: submittedJob, isError: isSubmitError }] =
    useSubmitCutMutation();
  const cutJob = usePolledJob(useGetCutJobQuery, submittedJob?.id) ?? submittedJob;

  const words = useSelector(selectWords);
  const segments = useSelector(selectSegments);
  const bufferSeconds = useSelector(selectBufferSeconds);
  const durationSeconds = useSelector(selectDurationSeconds);
  const ranges = useSelector(selectKeptRanges);
  const stats = useSelector(selectStats);

  const videoRef = useRef<HTMLVideoElement>(null);
  const [scrubberTarget, setScrubberTarget] = useState<ScrubberTarget | null>(null);

  const preview = usePreviewPlayback(videoRef, ranges);
  const activeWordIndex = useActiveWordIndex(videoRef, words);

  const transcript = useMemo(() => (data ? toEditorTranscript(data) : null), [data]);

  useEffect(() => {
    if (transcript) {
      dispatch(transcriptLoaded({ transcriptId: transcriptionJobId, transcript }));
    }
  }, [transcript, transcriptionJobId, dispatch]);

  if (isLoading) return <p style={{ color: 'var(--text-secondary)' }}>Loading transcript…</p>;
  if (isError || !transcript) {
    // 409 is the backend's "job exists but isn't Completed yet" — worth telling apart from
    // a genuine failure, since the answer is just to wait.
    const notReady = isFetchErrorWithStatus(error, 409);
    return (
      <div className="editor-empty">
        {notReady
          ? 'This transcription job has not finished yet. Reload once it reports Completed.'
          : 'Could not load the transcript.'}
      </div>
    );
  }
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
    void submitCut({
      cutRequest: buildCutRequest(transcriptionJobId, words, bufferSeconds, durationSeconds),
    });
  };

  return (
    <div className="editor">
      <div className="editor-left">
        <video
          ref={videoRef}
          className="editor-video"
          src={`/api/transcriptions/${transcriptionJobId}/source`}
          controls
        />

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

        {/* The backend rejects a cut with nothing kept, so don't let it be submitted. */}
        <button
          className="editor-btn"
          type="button"
          disabled={isSubmitting || !ranges.length}
          onClick={handleSubmitForExport}
        >
          {isSubmitting ? 'Submitting…' : 'Send for export'}
        </button>

        {cutJob && (
          <p className="editor-export-status">
            Cut job <code>{cutJob.id}</code> is {describeJobStatus(cutJob.status)}.{' '}
            {cutJob.downloadUrl && <a href={cutJob.downloadUrl}>Download</a>}
            {cutJob.error && <span className="error">{cutJob.error}</span>}
          </p>
        )}
        {isSubmitError && <p className="editor-export-status error">Could not submit the cut.</p>}
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

/** RTK Query surfaces fetch errors as `{ status, data }`; narrow without importing the union. */
function isFetchErrorWithStatus(error: unknown, status: number): boolean {
  return typeof error === 'object' && error !== null && 'status' in error && error.status === status;
}

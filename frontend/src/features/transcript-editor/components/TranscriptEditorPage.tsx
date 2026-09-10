import { useEffect, useMemo, useRef, useState } from 'react';
import { useDispatch, useSelector } from 'react-redux';
import { useParams } from 'react-router-dom';
import type { AppDispatch } from '../../../store';
import { useGetCutJobQuery, useGetTranscriptQuery } from '../../../api/generatedApi';
import { useSubmitCutWithSourceMutation } from '../api/cutSubmitApi';
import { getSourceFile, rememberSourceFile } from '../api/sourceFileStore';
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
    useSubmitCutWithSourceMutation();
  const cutJob = usePolledJob(useGetCutJobQuery, submittedJob?.id) ?? submittedJob;

  // The server keeps no copy of the video (#22), so the browser's is the only one. It arrives via
  // sourceFileStore when the upload panel navigated here, and is absent on a reload or a shared
  // link — in which case the transcript still loads and the visitor re-picks the same file.
  const [sourceFile, setSourceFile] = useState<File | null>(() => getSourceFile(transcriptionJobId));
  const [videoUrl, setVideoUrl] = useState<string | null>(null);

  useEffect(() => {
    if (!sourceFile) {
      setVideoUrl(null);
      return;
    }
    // An object URL pins the blob in memory until it is revoked, so this must be paired.
    const url = URL.createObjectURL(sourceFile);
    setVideoUrl(url);
    return () => URL.revokeObjectURL(url);
  }, [sourceFile]);

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
    if (!sourceFile) return;
    void submitCut({
      file: sourceFile,
      request: buildCutRequest(transcriptionJobId, words, bufferSeconds, durationSeconds),
    });
  };

  const handleRepickSource = (picked: File) => {
    rememberSourceFile(transcriptionJobId, picked);
    setSourceFile(picked);
  };

  return (
    <div className="editor">
      <div className="editor-left">
        {/*
          Always mounted, even with nothing to play. usePreviewPlayback and useActiveWordIndex
          attach their listeners in effects keyed on the ref, which is stable — so a <video> that
          appeared later, once a file was picked, would never get them and playback would silently
          do nothing.
        */}
        <video
          ref={videoRef}
          className={'editor-video' + (videoUrl ? '' : ' hidden')}
          src={videoUrl ?? undefined}
          controls
        />

        {!videoUrl && (
          <div className="editor-video-missing glass">
            <p>
              Your video stays on your device — snip-it never keeps a copy, so reloading this page
              loses track of it. Pick the same file again to play it and to export a cut.
            </p>
            <input
              type="file"
              aria-label="Video or audio file"
              accept="video/*,audio/*"
              onChange={(event) => {
                const picked = event.target.files?.[0];
                if (picked) handleRepickSource(picked);
              }}
            />
          </div>
        )}

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

        {/*
          The backend rejects a cut with nothing kept, so don't let it be submitted. It also needs
          the video itself now (#22) — the request carries it — so without a file there is nothing
          to send, and the panel above is already asking for one.
        */}
        <button
          className="editor-btn"
          type="button"
          disabled={isSubmitting || !ranges.length || !sourceFile}
          onClick={handleSubmitForExport}
        >
          {isSubmitting ? 'Uploading and cutting…' : 'Send for export'}
        </button>

        {cutJob && (
          <p className="editor-export-status">
            Cut job <code>{cutJob.id}</code> is {describeJobStatus(cutJob.status)}.{' '}
            {cutJob.downloadUrl && (
              <>
                <a href={cutJob.downloadUrl}>Download</a>{' '}
                {/*
                  Said up front rather than discovered: the export is deleted as it is streamed
                  (#22), so this link works once and a second click is a 404.
                */}
                <span className="editor-export-note">— available once; we delete it as you download.</span>
              </>
            )}
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

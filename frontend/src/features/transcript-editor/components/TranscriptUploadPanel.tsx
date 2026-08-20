import { useEffect, useState } from 'react';
import { useNavigate } from 'react-router-dom';
import { useGetTranscriptionJobQuery } from '../../../api/generatedApi';
import { useUploadTranscriptionMutation } from '../api/transcriptionUploadApi';
import { describeJobStatus } from '../api/jobStatus';
import { describeUploadError, isPreviewLimit } from '../api/uploadError';
import { rememberSourceFile } from '../api/sourceFileStore';
import { usePolledJob } from '../hooks/usePolledJob';
import './TranscriptUploadPanel.css';

/**
 * The way into the editor: upload a video, wait for the transcription job, then open it.
 *
 * Transcription is a background job, so this is submit-then-poll rather than a single
 * request — a long recording can take minutes, and the request would time out.
 *
 * The picked file is handed to `sourceFileStore` before navigating, because the server deletes
 * its copy the moment transcription finishes (#22) and the editor needs it to play the video
 * and to submit the cut.
 */
export function TranscriptUploadPanel() {
  const navigate = useNavigate();
  const [file, setFile] = useState<File | null>(null);
  const [upload, { data: submittedJob, isLoading: isUploading, isError: isUploadError, error: uploadError }] =
    useUploadTranscriptionMutation();

  const job = usePolledJob(useGetTranscriptionJobQuery, submittedJob?.id) ?? submittedJob;

  useEffect(() => {
    if (job?.status === 'Completed' && job.id) {
      // Before navigating, not after: the editor reads this on its first render, and the file
      // is the only copy left once the backend has finished with the upload.
      if (file) {
        rememberSourceFile(job.id, file);
      }
      navigate(`/editor/${job.id}`);
    }
  }, [job?.status, job?.id, file, navigate]);

  const isWaiting = Boolean(job) && job?.status !== 'Failed';

  return (
    <div className="upload-panel glass">
      <h2>Transcribe a video</h2>
      <p className="upload-hint">
        Upload a video or audio file. We transcribe it, then you cut it by editing the words.
      </p>

      <input
        type="file"
        aria-label="Video or audio file"
        accept="video/*,audio/*"
        disabled={isUploading || isWaiting}
        onChange={(event) => setFile(event.target.files?.[0] ?? null)}
      />

      <button
        className="editor-btn"
        type="button"
        disabled={!file || isUploading || isWaiting}
        onClick={() => file && void upload(file)}
      >
        {isUploading ? 'Uploading…' : 'Transcribe'}
      </button>

      {isUploadError && (
        <p className={isPreviewLimit(uploadError) ? 'upload-status notice' : 'upload-status error'}>
          {describeUploadError(uploadError)}
        </p>
      )}

      {job && (
        <p className="upload-status">
          Transcription is {describeJobStatus(job.status)}.
          {job.status === 'Failed' ? (
            <span className="error"> {job.error ?? 'No reason given.'}</span>
          ) : (
            ' The editor opens as soon as it finishes.'
          )}
        </p>
      )}
    </div>
  );
}

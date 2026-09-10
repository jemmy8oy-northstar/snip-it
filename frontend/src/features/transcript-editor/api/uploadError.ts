import type { PreviewLimitReached } from '../../../api/generatedApi';

/**
 * RTK Query hands back `FetchBaseQueryError | SerializedError | undefined`, and only the first
 * of those has `status`/`data` at all — so this takes `unknown` and narrows, rather than naming
 * a shape that `SerializedError` can never satisfy.
 */
type UnknownError = unknown;

function hasStatus(error: unknown): error is { status: unknown; data?: unknown } {
  return typeof error === 'object' && error !== null && 'status' in error;
}

function isPreviewLimitBody(data: unknown): data is PreviewLimitReached {
  return typeof data === 'object' && data !== null
    && typeof (data as PreviewLimitReached).message === 'string'
    && (data as PreviewLimitReached).message.length > 0;
}

/**
 * What to tell someone whose upload was refused.
 *
 * snip-it is open to anyone (#13), so a 429 is not a fault — it is the app saying it is a
 * preview and has hit a limit. The server sends the sentence to show, because only the server
 * knows which limit tripped; inventing wording here would drift from it. Everything else is a
 * genuine failure and keeps the generic retry line.
 */
export function describeUploadError(error: UnknownError): string {
  if (hasStatus(error) && error.status === 429 && isPreviewLimitBody(error.data)) {
    return error.data.message;
  }

  return 'Upload failed. Try again.';
}

/** True when the upload was refused by a preview limit rather than by an error. */
export function isPreviewLimit(error: UnknownError): boolean {
  return hasStatus(error) && error.status === 429;
}

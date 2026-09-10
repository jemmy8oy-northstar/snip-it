import { describe, expect, it } from 'vitest';
import { describeUploadError, isPreviewLimit } from './uploadError';

describe('describeUploadError', () => {
  it('shows the server sentence when a preview limit refuses the upload', () => {
    const message = 'snip-it is a preview, and it has already done today’s batch of transcriptions.';

    expect(describeUploadError({ status: 429, data: { message } })).toBe(message);
  });

  it('falls back to the generic line for any other failure', () => {
    expect(describeUploadError({ status: 500, data: 'boom' })).toBe('Upload failed. Try again.');
    expect(describeUploadError({ status: 'FETCH_ERROR', data: undefined })).toBe('Upload failed. Try again.');
    expect(describeUploadError(undefined)).toBe('Upload failed. Try again.');
  });

  it('does not render a 429 body that is not the shape we expect', () => {
    // A proxy or the ingress can return its own 429 as an HTML page. Rendering that verbatim
    // would put a wall of markup where a sentence should be.
    expect(describeUploadError({ status: 429, data: '<html>429 Too Many Requests</html>' }))
      .toBe('Upload failed. Try again.');
    expect(describeUploadError({ status: 429, data: { message: '' } })).toBe('Upload failed. Try again.');
    expect(describeUploadError({ status: 429, data: null })).toBe('Upload failed. Try again.');
  });
});

describe('isPreviewLimit', () => {
  it('is true only for 429, so a real error still reads as an error', () => {
    expect(isPreviewLimit({ status: 429, data: { message: 'hi' } })).toBe(true);
    expect(isPreviewLimit({ status: 500, data: 'boom' })).toBe(false);
    expect(isPreviewLimit(undefined)).toBe(false);
  });
});

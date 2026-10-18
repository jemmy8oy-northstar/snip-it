import type {
  Transcript as ApiTranscript,
  TranscriptSegment as ApiTranscriptSegment,
  TranscriptWord as ApiTranscriptWord,
} from '../../../api/generatedApi';
import type { TranscriptDto } from '../types';
import { toEditorTranscript } from '../api/transcriptAdapter';

// A realistic-shaped fixture standing in for a Groq whisper-large-v3 transcript.
// Timings are approximate but internally consistent (monotonic, no overlaps) so the
// preview-skip and buffer logic behave the way they do against real data.
const RAW: Array<{ start: number; end: number; text: string; words: [string, number, number][] }> = [
  {
    start: 0.0,
    end: 3.2,
    text: 'So, um, today I want to talk about the new pipeline.',
    words: [
      ['So,', 0.0, 0.3],
      ['um,', 0.35, 0.6],
      ['today', 0.9, 1.2],
      ['I', 1.25, 1.35],
      ['want', 1.4, 1.6],
      ['to', 1.65, 1.75],
      ['talk', 1.8, 2.05],
      ['about', 2.1, 2.4],
      ['the', 2.45, 2.55],
      ['new', 2.6, 2.85],
      ['pipeline.', 2.9, 3.2],
    ],
  },
  {
    start: 4.0,
    end: 7.1,
    text: "It's, uh, honestly a lot simpler than what we had before.",
    words: [
      ["It's,", 4.0, 4.25],
      ['uh,', 4.3, 4.5],
      ['honestly', 4.9, 5.3],
      ['a', 5.35, 5.4],
      ['lot', 5.45, 5.65],
      ['simpler', 5.7, 6.05],
      ['than', 6.1, 6.3],
      ['what', 6.35, 6.5],
      ['we', 6.55, 6.65],
      ['had', 6.7, 6.85],
      ['before.', 6.9, 7.1],
    ],
  },
  {
    start: 7.6,
    end: 10.4,
    text: 'First we transcribe, then we cut, then we export.',
    words: [
      ['First', 7.6, 7.9],
      ['we', 7.95, 8.05],
      ['transcribe,', 8.1, 8.6],
      ['then', 8.9, 9.1],
      ['we', 9.15, 9.25],
      ['cut,', 9.3, 9.55],
      ['then', 9.8, 10.0],
      ['we', 10.05, 10.15],
      ['export.', 10.2, 10.4],
    ],
  },
];

function buildApiTranscript(): ApiTranscript {
  const words: ApiTranscriptWord[] = [];
  const segments: ApiTranscriptSegment[] = RAW.map((seg, index) => {
    seg.words.forEach(([text, start, end]) => {
      words.push({ text, start, end, kept: true });
    });
    return { index, start: seg.start, end: seg.end, text: seg.text };
  });

  return {
    transcriptionJobId: MOCK_TRANSCRIPTION_JOB_ID,
    durationSeconds: 11,
    words,
    segments,
  };
}

/** A stable id for the fixture, so e2e can navigate to /editor/:id. */
export const MOCK_TRANSCRIPTION_JOB_ID = '11111111-1111-1111-1111-111111111111';

/**
 * The fixture in the shape `GET /api/transcriptions/{id}/transcript` returns — this is
 * what e2e route stubs fulfil with. Note segments carry no `wordIndices`; the backend
 * genuinely doesn't send them.
 */
export const mockApiTranscript: ApiTranscript = buildApiTranscript();

/**
 * The same fixture as the editor sees it, i.e. run through the real adapter rather than
 * hand-built. Deriving it means the unit fixture and the e2e payload cannot drift apart,
 * and the segment→word mapping under test is the one production uses.
 */
export const mockTranscript: TranscriptDto = toEditorTranscript(mockApiTranscript);

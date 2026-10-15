import type { JobStatus } from '../../../api/generatedApi';

/**
 * The backend serialises `JobStatus` as its name, so the generated type is a string union and
 * the values below are checked against it at compile time — a renamed or removed member is a
 * type error here rather than a silently wrong comparison.
 */

/** True once the job will not change again — i.e. stop polling. */
export function isTerminal(status: JobStatus): boolean {
  return status === 'Completed' || status === 'Failed';
}

/** The wire name is capitalised and past-tense; this is what a person should read. */
const DESCRIPTIONS: Record<JobStatus, string> = {
  Pending: 'queued',
  Processing: 'processing',
  Completed: 'complete',
  Failed: 'failed',
};

export function describeJobStatus(status: JobStatus): string {
  return DESCRIPTIONS[status] ?? 'unknown';
}

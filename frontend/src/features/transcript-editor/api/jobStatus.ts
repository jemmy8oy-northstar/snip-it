import type { JobStatus } from '../../../api/generatedApi';

/**
 * The backend serialises its `JobStatus` enum as an integer, so the generated type is a
 * bare `number` and the ordinals have to be named somewhere. Mirrors
 * `Balenthiran.Snipit.Abstractions.DataModels.JobStatus` — declaration order is the
 * contract, so reordering that enum silently changes this.
 *
 * A `JsonStringEnumConverter` on the backend would delete this file; raised on the PR.
 */
export const JOB_STATUS = {
  Pending: 0,
  Processing: 1,
  Completed: 2,
  Failed: 3,
} as const;

/** True once the job will not change again — i.e. stop polling. */
export function isTerminal(status: JobStatus | undefined): boolean {
  return status === JOB_STATUS.Completed || status === JOB_STATUS.Failed;
}

export function describeJobStatus(status: JobStatus | undefined): string {
  switch (status) {
    case JOB_STATUS.Pending:
      return 'queued';
    case JOB_STATUS.Processing:
      return 'processing';
    case JOB_STATUS.Completed:
      return 'complete';
    case JOB_STATUS.Failed:
      return 'failed';
    default:
      return 'unknown';
  }
}

import { useEffect, useState } from 'react';
import type { JobStatus } from '../../../api/generatedApi';
import { isTerminal } from '../api/jobStatus';

interface PollOptions {
  skip: boolean;
  pollingInterval: number;
}

/**
 * Follows a background job to completion.
 *
 * Both pipelines submit and then work out of band — `POST /api/transcriptions` and
 * `POST /api/cuts` each answer `Pending` — so a submit response can never carry the result.
 * This polls the job's GET endpoint until it settles, then stops with `pollingInterval: 0`
 * rather than `skip`, which would discard the result we just waited for.
 *
 * The query hook is passed in so transcription and cut jobs share one implementation; it is
 * called unconditionally on every render, as hook rules require.
 */
export function usePolledJob<TJob extends { status: JobStatus }>(
  useJobQuery: (arg: { id: string }, options: PollOptions) => { data?: TJob },
  jobId: string | undefined,
  intervalMs = 2000,
): TJob | undefined {
  const [settled, setSettled] = useState(false);

  const { data } = useJobQuery(
    { id: jobId ?? '' },
    { skip: !jobId, pollingInterval: settled ? 0 : intervalMs },
  );

  useEffect(() => {
    setSettled(false);
  }, [jobId]);

  // Depend on the status value rather than the response object — a poll that changes nothing
  // still returns a fresh object, which would re-run this on every tick.
  const status = data?.status;
  useEffect(() => {
    if (status && isTerminal(status)) setSettled(true);
  }, [status]);

  return data;
}

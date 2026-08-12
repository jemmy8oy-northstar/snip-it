import { useEffect, useState } from 'react';
import { useGetCutJobQuery } from '../../../api/generatedApi';
import { isTerminal } from '../api/jobStatus';

/**
 * Follows a cut job to completion.
 *
 * `POST /api/cuts` always answers Pending — the FFmpeg work runs on a background queue —
 * so the submit response alone can never carry a download URL. This polls until the job
 * settles, then stops (`pollingInterval: 0` rather than `skip`, which would throw away the
 * result we just waited for).
 */
export function useCutJobProgress(jobId: string | undefined) {
  const [settled, setSettled] = useState(false);

  const { data } = useGetCutJobQuery(
    { id: jobId ?? '' },
    { skip: !jobId, pollingInterval: settled ? 0 : 2000 },
  );

  useEffect(() => {
    setSettled(false);
  }, [jobId]);

  useEffect(() => {
    if (isTerminal(data?.status)) setSettled(true);
  }, [data?.status]);

  return data;
}

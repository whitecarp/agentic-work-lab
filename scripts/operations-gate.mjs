// Pure offline decision function: no scheduling, network access, or account actions.
export function planRun({executor, owner, stop = false, running = false, now, jobs = []}) {
  const current = Date.parse(now);
  if (!Number.isFinite(current)) throw new Error('Invalid current time');
  if (!executor || executor !== owner) return {decision:'skip',reason:'not_owner',due:[]};
  if (stop) return {decision:'skip',reason:'stop_flag',due:[]};
  if (running) return {decision:'skip',reason:'already_running',due:[]};
  const ids = new Set();
  const due = [];
  for (const job of jobs) {
    if (!job.id || ids.has(job.id)) throw new Error('Missing or duplicate job id');
    ids.add(job.id);
    if (job.completed === true || job.authorized !== true) continue;
    const at = Date.parse(job.due_at);
    if (!Number.isFinite(at)) throw new Error('Invalid due time for '+job.id);
    if (at <= current) due.push(job.id);
  }
  return {decision:due.length?'plan_only':'no_work_due',reason:'checked',due};
}

export function verifyScheduledRun(record) {
  const start = Date.parse(record.started_at), end = Date.parse(record.completed_at);
  return record.trigger === 'automation_heartbeat_scheduled' &&
    record.status === 'VERIFIED_AUTOMATIC_RUN_COMPLETED' &&
    record.manual_test_triggered === false &&
    Number.isFinite(start) && Number.isFinite(end) && end >= start &&
    Array.isArray(record.task_errors) && record.task_errors.length === 0;
}

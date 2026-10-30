"""One bounded read-only batch per CLI-owned browser context; no write fallback."""
import time
import asyncio
from .core import VpubError, stop_check, atomic_json, digest, home
from pathlib import Path


def validate(payload):
    from .operations import work_id
    jobs = payload.get('jobs')
    if not isinstance(jobs, list) or not 1 <= len(jobs) <= 20 or not payload.get('expected_identity'):
        raise VpubError('invalid_batch', 'Provide 1–20 reads and an expected account identity.')
    for job in jobs:
        required={'operation','work_id','limit'}
        allowed=required | ({'pages','reply_pages'} if job.get('operation')=='comments' else set())
        if not required <= set(job) or not set(job) <= allowed or job['operation'] not in {'comments', 'stats'}:
            raise VpubError('read_only_batch', 'Only comment and statistics reads are allowed in this batch.')
        work_id(payload['platform'], job['work_id'])
        if type(job['limit']) is not int or not 1 <= job['limit'] <= 100:
            raise VpubError('invalid_limit', 'Batch limit must be 1–100.')
        from .comment_collection import budgets
        budgets(job['limit'],job.get('pages',1),job.get('reply_pages',0))
    return jobs


async def execute(page, platform, identity, payload):
    from .operations_browser import execute as read
    jobs = validate(payload)
    progress=Path(payload['progress_path']).resolve() if payload.get('progress_path') else None
    if progress and not progress.is_relative_to((home()/'operations/sync-progress').resolve()):
        raise VpubError('invalid_checkpoint','Batch checkpoints must stay under the local sync-progress directory.')
    if identity != payload['expected_identity']:
        raise VpubError('wrong_account', 'The batch account changed.')
    results = []; failed = False; started = time.monotonic()
    for job in jobs:
        if failed:
            results.append({'job': job, 'ok': False, 'error': {'code': 'batch_stopped'}})
            continue
        try:
            stop_check(platform)
            remaining = 480 - (time.monotonic() - started)
            if remaining <= 0: raise VpubError('batch_time_budget', 'The read-only batch exhausted its time budget.')
            value = await asyncio.wait_for(read(page, platform, identity, {**job, 'reuse_page': True}), timeout=min(90, remaining))
            results.append({'job': job, 'ok': True, 'data': value})
        except Exception as exc:
            failed = True
            results.append({'job': job, 'ok': False, 'error': {
                'code': exc.code if isinstance(exc, VpubError) else type(exc).__name__}})
        if progress:
            atomic_json(progress,{'identity':identity,'request_hash':digest(jobs),'results':results,'platform_writes':0})
    return {'identity': identity, 'results': results, 'browser_sessions': 1, 'platform_writes': 0,
            'read_seconds': round(time.monotonic() - started, 3)}

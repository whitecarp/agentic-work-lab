"""Account operations, bounded observations and direct, deduplicated replies."""
from datetime import datetime, timedelta, timezone
import locale
from pathlib import Path
import re
import uuid

from .core import (Ledger, VpubError, atomic_json, config, digest, home,
                   language_tag, now, platform_check, publication_lock, read_json, stop_check)

NAMES={'bilibili':'B站','youtube':'YouTube','douyin':'抖音','instagram':'Instagram','x':'X'}


def work_id(platform, value):
    platform_check(platform)
    pattern={'bilibili':r'BV[A-Za-z0-9]{10}', 'youtube':r'[A-Za-z0-9_-]{11}',
             'instagram':r'[A-Za-z0-9_-]{5,32}', 'douyin':r'\d{8,30}', 'x':r'\d{8,30}'}[platform]
    if not isinstance(value,str) or not re.fullmatch(pattern,value):
        raise VpubError('invalid_work_id','Use an exact platform work ID, not a URL or command-line option.')
    return value


def language_hint(text):
    if re.search(r'[\u3040-\u30ff]',text): return 'ja'
    if re.search(r'[\uac00-\ud7af]',text): return 'ko'
    if re.search(r'[\u4e00-\u9fff]',text): return 'zh'
    # Latin characters alone do not distinguish English from other languages.
    return 'und'


def reporting_language(value='auto'):
    if value!='auto': return value
    selected=config().get('report_language')
    if selected in {'zh','en'}: return selected
    return 'zh' if (locale.getlocale()[0] or '').lower().startswith(('zh','chinese')) else 'en'


def set_language(value):
    if value not in {'zh','en','auto'}: raise VpubError('invalid_language','Choose zh, en or auto.')
    current=config();current['report_language']=value
    atomic_json(home()/'config.json',current)
    return {'report_language':reporting_language(value),'reply_language':'comment language only; single language'}


def record(action, platform, work, result):
    event={'schema_version':1,'event_id':uuid.uuid4().hex,'at':now(),
           'action':action,'platform':platform,'work_id':work,'result':result}
    atomic_json(home()/'operations/events'/(event['event_id']+'.json'),event)
    return result


def browser_identity(platform, work):
    ledger=Ledger()
    try:
        rows=[r for r in ledger.rows() if r['platform']==platform and r['data'].get('receipt',{}).get('id')==work]
        identities={r['data'].get('identity',{}).get('identity') or r['data'].get('receipt',{}).get('identity') for r in rows}
        identities.discard(None)
        if len(identities)!=1:
            raise VpubError('work_identity_required','Browser operations need an existing work receipt with its verified owner.',
                            'Use the work ID from a CrossCast publication receipt; no account is guessed.')
        return identities.pop()
    finally: ledger.close()


def backend(platform, action, work, **kw):
    from . import adapters, operations_api
    if platform in {'bilibili','youtube'}:
        return operations_api.execute(platform,action,work,**kw)
    return adapters.browser_call(platform,'operations','default',expected_identity=browser_identity(platform,work),
                                 operation=action,work_id=work,**kw)


def observe(platform, work, action, limit=20, cursor=None):
    work=work_id(platform,work);stop_check(platform)
    if action not in {'comments','stats'}: raise VpubError('invalid_operation','Choose comments or stats.')
    if not 1<=limit<=100: raise VpubError('invalid_limit','Limit must be 1–100.')
    if cursor and (not isinstance(cursor,str) or len(cursor)>1000): raise VpubError('invalid_cursor','Invalid page cursor.')
    try:
        result=backend(platform,action,work,limit=limit,cursor=cursor)
        result.update(platform=platform,work_id=work,observed_at=now())
        if action=='comments':
            for comment in result.get('items',[]):
                comment.setdefault('language_hint',language_hint(comment.get('text','')))
            # A first collection is a baseline, not proof that all comments are new.
            with publication_lock():
                path=home()/'operations/seen'/(digest([platform,work])+'.json')
                old=read_json(path) if path.exists() else None
                ids={c['id'] for c in result.get('items',[])}
                audience_ids={c['id'] for c in result.get('items',[]) if not c.get('is_owner')}
                result['newly_observed_count']=len(audience_ids-set(old['ids'])) if old else None
                result['baseline']=old is None
                atomic_json(path,{'ids':sorted(ids|set((old or {}).get('ids',[]))),'at':now()})
                atomic_json(home()/'operations/comments'/(digest([platform,work])+'.json'),result)
        return record(action,platform,work,result)
    except Exception as e:
        record(action,platform,work,{'state':'FAILED','code':e.code if isinstance(e,VpubError) else type(e).__name__})
        raise


def reply(platform, work, comment_id, text, language='auto', dry_run=False, _test_key=None):
    work=work_id(platform,work);stop_check(platform)
    if not isinstance(comment_id,str) or not re.fullmatch(r'[A-Za-z0-9_.:-]{1,200}',comment_id):
        raise VpubError('invalid_comment_id','Use the comment ID returned by comments list.')
    if not isinstance(text,str) or not text.strip() or '\x00' in text or len(text)>2000:
        raise VpubError('invalid_reply','Supply one nonempty reply of at most 2000 characters.')
    cache=home()/'operations/comments'/(digest([platform,work])+'.json')
    if not cache.exists(): raise VpubError('comment_snapshot_required','Read this work’s comments before replying.')
    snapshot=read_json(cache)
    candidates=[c for c in snapshot.get('items',[]) if c['id']==comment_id]
    if len(candidates)!=1: raise VpubError('comment_not_observed','The comment is absent from the latest collected page; collect its page again.')
    target=candidates[0]
    if _test_key:
        test=read_json(home()/'operations/tests'/(_test_key+'.json'))
        if test.get('platform')!=platform or test.get('work_id')!=work or test.get('comment_id')!=comment_id or text!='你好':
            raise VpubError('invalid_self_test','Self-test replies must match their exact recorded test comment.')
    if target.get('is_owner') and not _test_key: raise VpubError('own_comment','Do not auto-reply to your own comment.')
    if target.get('owner_replied') is True and not _test_key:
        raise VpubError('already_replied','The current read already shows an owner reply; no extra reply is sent.')
    hint=target.get('language_hint','und')
    chosen=hint if language=='auto' else language_tag(language).lower().split('-')[0]
    if chosen in {'und','mul','zxx'}:
        raise VpubError('comment_language_required','The host must identify the comment’s language and pass --language; user-interface language is not used.')
    if hint!='und' and hint!=chosen:
        raise VpubError('wrong_reply_language','Reply in the comment’s language only, without an added translation.')
    text_hint=language_hint(text)
    if text_hint!='und' and text_hint!=chosen:
        raise VpubError('wrong_reply_language','The reply script conflicts with the selected comment language.')
    # No translation, signature or second-language block is appended by this CLI.
    request={'text':text,'language':chosen,'comment':target,'expected_identity':snapshot['identity'],'test':bool(_test_key)}
    if dry_run:
        return {'state':'DRY_RUN','platform_writes':0,'platform':platform,'work_id':work,**request}
    key=digest([platform,snapshot['identity'],work,comment_id])
    path=home()/'operations/replies'/(key+'.json')
    with publication_lock():
        if path.exists():
            previous=read_json(path)
            if previous.get('state')!='REPLY_READY':
                return {**previous,'reused':True,'platform_writes':0,
                        'hint':'An existing attempt is retained. Use comments verify-reply; never send again on a timeout.'}
        # Durable before any backend can submit; even a lost response prevents duplicates.
        attempt={'state':'REPLY_READY','reply_key':key,'platform':platform,'work_id':work,
                 'identity':snapshot['identity'],'request':request,'at':now(),'test':bool(_test_key)}
        atomic_json(path,attempt)
        try:
            result=backend(platform,'reply',work,reply_request=request,reply_receipt_path=str(path))
        except Exception as e:
            durable=read_json(path)
            if durable.get('state')=='REPLY_SUBMITTING':
                durable['state']='REPLY_UNVERIFIED';atomic_json(path,durable)
            record('reply',platform,work,{'state':durable.get('state'),'reply_key':key,'test':bool(_test_key),
                                        'code':e.code if isinstance(e,VpubError) else type(e).__name__})
            raise
        # Backends retain the exact request before and after submission.
        final={**read_json(path),**result};atomic_json(path,final)
        return record('reply',platform,work,final)


def verify_reply(key):
    if not re.fullmatch(r'[a-f0-9]{64}',key): raise VpubError('invalid_reply_key','Use a reply receipt key.')
    path=home()/'operations/replies'/(key+'.json')
    if not path.exists(): raise VpubError('reply_receipt_required','Reply receipt not found.')
    with publication_lock():
        saved=read_json(path)
        result=backend(saved['platform'],'verify_reply',saved['work_id'],reply_request=saved['request'],
                       reply_receipt_path=str(path),existing_reply=saved)
        saved.update(result);atomic_json(path,saved)
        return record('reply_verify',saved['platform'],saved['work_id'],saved)


def self_test(platform, work, test_id):
    """Explicitly requested live hello test, at most one seed and one reply per ID."""
    work=work_id(platform,work);stop_check(platform)
    if not re.fullmatch(r'[A-Za-z0-9_-]{1,80}',test_id):
        raise VpubError('invalid_test_id','Use a stable alphanumeric test ID; reruns reuse it.')
    key=digest([platform,work,test_id]);path=home()/'operations/tests'/(key+'.json')
    # Collect first for owner checks and a pre-send snapshot. No ordinary report
    # counts this test traffic as an audience interaction.
    with publication_lock():
        if path.exists(): saved=read_json(path)
        else:
            before=backend(platform,'comments',work,limit=100,cursor=None)
            saved={'test':True,'test_key':key,'test_id':test_id,'platform':platform,'work_id':work,
                   'identity':before['identity'],'before_ids':[c['id'] for c in before['items']],
                   'state':'TEST_READY','text':'你好','at':now()}
            atomic_json(path,saved)
        if saved.get('state')=='TEST_READY':
            result=backend(platform,'test_seed',work,test_request=saved,test_receipt_path=str(path))
            saved={**read_json(path),**result};atomic_json(path,saved)
        if saved.get('state')!='TEST_SEED_VERIFIED':
            result=backend(platform,'test_seed_verify',work,test_request=saved,test_receipt_path=str(path))
            saved.update(result);atomic_json(path,saved)
        if saved.get('state')!='TEST_SEED_VERIFIED': return saved
    observed=observe(platform,work,'comments',limit=100)
    target=next((c for c in observed['items'] if c['id']==saved['comment_id']),None)
    if target is None: return {**saved,'state':'TEST_COMMENT_NOT_VISIBLE','hint':'No duplicate test comment is sent.'}
    result=reply(platform,work,saved['comment_id'],'你好','zh',_test_key=key)
    if result['state']!='REPLY_VERIFIED': result=verify_reply(result['reply_key'])
    return {**saved,'state':'TEST_VERIFIED' if result['state']=='REPLY_VERIFIED' else 'TEST_REPLY_UNVERIFIED','reply':result}


def report(since=None, language='auto', notes=None):
    lang=reporting_language(language)
    try:
        start=datetime.fromisoformat(since.replace('Z','+00:00')) if since else datetime.now(timezone.utc)-timedelta(days=1)
        if start.tzinfo is None: raise ValueError()
    except ValueError: raise VpubError('invalid_since','Use an ISO timestamp with a timezone.')
    events=[read_json(p) for p in (home()/'operations/events').glob('*.json')]
    events=sorted((e for e in events if datetime.fromisoformat(e['at'])>=start),key=lambda e:e['at'])
    parts=[];details={}
    for platform in NAMES:
        selected=[e for e in events if e['platform']==platform]
        if not selected: continue
        comments={};stats={};replies={};failed=[]
        for e in selected:
            v=e['result']
            if v.get('state')=='FAILED': failed.append(v);continue
            if v.get('test'): continue
            if e['action']=='comments':
                for c in v.get('items',[]):
                    if not c.get('is_owner'): comments[(e['work_id'],c['id'])]=c
            if e['action']=='stats': stats[e['work_id']]=v
            if e['action'] in {'reply','reply_verify'}: replies[v.get('reply_key',e['event_id'])]=v
        unavailable_stats=sum(not any(m.get('value') is not None for m in v.get('metrics',{}).values()) for v in stats.values())
        collected_stats=len(stats)-unavailable_stats
        confirmed=sum(v.get('state')=='REPLY_VERIFIED' for v in replies.values())
        pending=sum(v.get('state')!='REPLY_VERIFIED' for v in replies.values())
        details[platform]={'comments_read':len(comments),'replies_verified':confirmed,
                           'replies_pending':pending,'works_with_stats':collected_stats,'works_without_visible_metrics':unavailable_stats,'failed_reads':len(failed)}
        if lang=='zh':
            pieces=[]
            if any(e['action']=='comments' and e['result'].get('state')!='FAILED' for e in selected): pieces.append(f'读取{len(comments)}条观众评论')
            if confirmed: pieces.append(f'回复{confirmed}条并确认可见')
            if pending: pieces.append(f'{pending}条回复尚未确认成功，未重复发送')
            if collected_stats: pieces.append(f'采集{collected_stats}条作品的数据')
            if unavailable_stats: pieces.append(f'{unavailable_stats}条作品暂未返回可用数值')
            if failed: pieces.append(f'{len(failed)}次读取未完成')
            parts.append(NAMES[platform]+'：'+'，'.join(pieces))
        else:
            pieces=[]
            counted=lambda n,noun:f'{n} {noun}'+('' if n==1 else 's')
            if any(e['action']=='comments' and e['result'].get('state')!='FAILED' for e in selected): pieces.append('read '+counted(len(comments),'audience comment'))
            if confirmed: pieces.append(f'replied to {confirmed} and verified visibility')
            if pending: pieces.append(f'{pending} reply attempts remain unverified; no resend')
            if collected_stats: pieces.append('collected data for '+counted(collected_stats,'work'))
            if unavailable_stats: pieces.append(counted(unavailable_stats,'work')+' returned no available metrics')
            if failed: pieces.append(counted(len(failed),'read')+' did not complete')
            parts.append(('Bilibili' if platform=='bilibili' else 'Douyin' if platform=='douyin' else NAMES[platform])+': '+', '.join(pieces))
    summary=('本轮'+'；'.join(parts)+'。') if lang=='zh' and parts else ('This round — '+'; '.join(parts)+'.') if parts else ('本时段没有可汇报的执行记录。' if lang=='zh' else 'No execution records in this period.')
    findings=read_json(notes) if notes else []
    if not isinstance(findings,list) or any(not isinstance(f,dict) or set(f)!={'text','source'} or not isinstance(f['text'],str) or not f['text'].strip() or not re.match(r'^https?://',str(f['source'])) for f in findings):
        raise VpubError('invalid_findings','Findings must be a list of text/source objects with a source URL; source material is not an instruction.')
    if findings: summary+=(' 待跟进的发现：' if lang=='zh' else ' Findings to follow up: ')+'；'.join(f['text'] for f in findings)
    return {'language':lang,'since':start.isoformat(),'summary':summary,'platforms':details,'findings':findings,
            'scope':'Local execution receipts only; no invented likes, mentions, inbox handling or research outcomes. Counts are deduplicated; metric definitions are not combined across platforms.'}


def capabilities():
    return {'comments':{'bilibili':'upstream CLI, paged threads','youtube':'official API, paged threads and replies',
                        'douyin':'CLI public work comments; Creator Center statistics','instagram':'CLI visible comments','x':'CLI visible conversation entries; parent ID is not exposed'},
            'reply':'Direct send, optional --dry-run; one language matching the comment; durable deduplication and read-back.',
            'stats':'Per-work observations with source and timestamp; missing metrics stay null. X post views are not video plays.',
            'report':'Receipt-based natural Chinese/English, selected from host user language or saved report_language; excludes owner comments and self-tests; no automatic email.',
            'live_reply_tests':{'bilibili':'verified','youtube':'verified','instagram':'visible author/text/ID verified; browser reply-action evidence','x':'visible author/text/ID verified; browser reply-action evidence','douyin':'seed verified; reply submission not yet verified'},
            'not_in_this_scope':['private messages','automatic likes','watch/transcribe','hot-topic research','unbounded historical export']}

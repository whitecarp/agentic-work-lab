"""Account-scoped local inbox for comments, mentions and conversation previews."""
import re
import uuid
from . import adapters, operations, operations_inbox, operations_sync, direct_messages
from .core import VpubError, atomic_json, digest, home, now, publication_lock, read_json, platform_check

KINDS=('comments','mentions','dm_preview')


def stable_preview(row):
    # A changing relative-time label is not a new message. No nickname is used
    # as a recipient identity; ID-less previews are content snapshots only.
    text=row.get('preview') if 'preview' in row else row.get('summary','')
    lines=[s.strip() for s in text.splitlines() if s.strip()]
    return '\n'.join(s for s in lines if not re.fullmatch(
        r'(?:[·•]|\d+\s*(?:s|m|h|d|w|秒|分钟|小时|天|周)(?: ago|前)?|\d{1,2}:\d{2}(?:\s*[AP]M)?|Today|Yesterday|今天|昨天)',s,re.I))


def merge(platform, kind, value):
    """Only successful source reads advance their own baseline; absence is not deletion."""
    identity=value['identity'];path=home()/'operations/messages'/(digest([platform,identity,kind])+'.json')
    with publication_lock():
        data=read_json(path) if path.exists() else {'items':{},'schema_version':1}
        baseline='observed_at' not in data;added=changed=0;observed=now()
        for item in value.get('items',[]):
            if kind=='mentions':
                source_id=item['id'];content=item.get('text','');stable=True
            else:
                content=stable_preview(item);stable=bool(item.get('conversation_id'))
                source_id=item.get('conversation_id') or 'preview-'+digest([item.get('name'),content])
            key=digest([platform,identity,kind,source_id]);old=data['items'].get(key)
            revision=bool(old and old['text']!=content)
            added+=int(old is None);changed+=int(revision)
            data['items'][key]={**(old or {}),'key':key,'kind':kind,'platform':platform,'identity':identity,
                'source_id':source_id,'text':content,'url':item.get('url'),'is_owner':item.get('is_owner',False),
                'stable_source_id':stable,'status':'unread' if not old or revision else old['status'],
                'revision':(old or {}).get('revision',1)+int(revision),
                'first_seen_at':(old or {}).get('first_seen_at',observed),'last_seen_at':observed}
        data.update(observed_at=observed,scope=value.get('scope'),complete=value.get('complete',False))
        atomic_json(path,data)
    return {'baseline':baseline,'observed':len(value.get('items',[])),
            'newly_observed':0 if baseline else added,'changed':changed,'complete':value.get('complete',False),
            'scope':value.get('scope'),'unit':'conversation_previews' if kind=='dm_preview' else 'mentions'}


def entries():
    for path in sorted((home()/'operations/messages').glob('*.json')):
        for row in read_json(path).get('items',{}).values():yield path,row


def listing(platform=None, kind=None, status='pending', limit=50, cursor=None, identity=None, include_own=False):
    if platform:platform_check(platform)
    if kind and kind not in KINDS:raise VpubError('invalid_kind','Choose comments, mentions or dm_preview.')
    if status not in (*operations_inbox.STATES,'pending','all') or type(limit) is not int or not 1<=limit<=200:
        raise VpubError('invalid_filter','Use a known local status and limit 1–200.')
    if cursor and not re.fullmatch('[a-f0-9]{64}',cursor):raise VpubError('invalid_cursor','Use a returned local cursor.')
    rows=[row for _,row in entries()]
    rows.extend({**row,'kind':'comments','text':row['comment']['text'],'is_owner':row['comment'].get('is_owner'),
                 'owner_replied':row['comment'].get('owner_replied')} for _,row in operations_inbox.entries())
    rows=[r for r in rows if (not platform or r['platform']==platform) and (not identity or r['identity']==identity)
          and (not kind or r['kind']==kind) and (include_own or not r.get('is_owner'))
          and (status=='all' or r['status']==status or status=='pending' and r['status'] in {'unread','read'} and r.get('owner_replied') is not True)]
    rows.sort(key=lambda r:r['key']);eligible=[r for r in rows if not cursor or r['key']>cursor];page=eligible[:limit]
    return {'items':page,'matching_total':len(rows),'next_cursor':page[-1]['key'] if len(eligible)>limit else None,
            'platform_writes':0,'scope':'Local observations, not full account history. DM entries are conversation previews, not incoming-message counts.'}


def mark(key,status):
    if not re.fullmatch('[a-f0-9]{64}',key) or status not in operations_inbox.STATES:
        raise VpubError('invalid_mark','Use an exact local key and unread/read/handled/deferred.')
    matches=[(path,row) for path,row in entries() if row['key']==key]
    if not matches:return operations_inbox.mark(key,status)
    if len(matches)!=1:raise VpubError('ambiguous_key','Local message key is ambiguous.')
    with publication_lock():
        path,_=matches[0];data=read_json(path);row=data['items'][key]
        row.update(status=status,marked_at=now());atomic_json(path,data)
    return {**row,'platform_writes':0}


def sync(platforms, sources='comments,mentions,dm_preview',limit=20,pages=2,language='auto'):
    selected=list(dict.fromkeys(platforms.split(',')));kinds=list(dict.fromkeys(sources.split(',')))
    for p in selected:platform_check(p)
    if not kinds or any(k not in KINDS for k in kinds) or type(limit) is not int or not 1<=limit<=50 or type(pages) is not int or not 1<=pages<=10:
        raise VpubError('invalid_sources','Select comments/mentions/dm_preview, limit 1–50 and pages 1–10.')
    run={'run_id':uuid.uuid4().hex,'started_at':now(),'sources':[],'platform_writes':0}
    comments=None
    if 'comments' in kinds:
        watched={x['platform'] for x in operations_sync.watch('list')['items']} & set(selected)
        if watched:comments=operations_sync.sync(','.join(p for p in selected if p in watched),limit,pages,language)
        for p in selected:
            errors=[e for e in (comments or {}).get('errors',[]) if e['platform']==p]
            run['sources'].append({'platform':p,'kind':'comments','state':'NOT_CONFIGURED' if p not in watched else 'FAILED' if errors else 'OBSERVED',
                                   'errors':errors,'scope':'Watched works only.'})
    for p in selected:
        wanted=[k for k in kinds if k!='comments']
        if not wanted:continue
        if p not in {'x','douyin','instagram'}:
            run['sources'].extend({'platform':p,'kind':k,'state':'UNSUPPORTED','reason':'Not integrated into this collector; existing upstream commands remain available.'} for k in wanted);continue
        if comments and any(e['platform']==p for e in comments['errors']):
            run['sources'].extend({'platform':p,'kind':k,'state':'NOT_CHECKED','reason':'Earlier account read failed.'} for k in wanted);continue
        try:
            identity=direct_messages.identity(p)
            result=adapters.browser_call(p,'message_sources','default',expected_identity=identity,sources=wanted,limit=limit)
            if result.get('identity')!=identity:raise VpubError('wrong_account','Message account changed.')
            for k in wanted:
                value=result['sources'][k]
                if value['state']=='OBSERVED' and value.get('identity')!=identity:
                    raise VpubError('wrong_account','Source account changed.')
            for k in wanted:
                value=result['sources'][k];row={'platform':p,'kind':k,'state':value['state']}
                if value['state']=='OBSERVED':
                    row.update(merge(p,k,value))
                else:row.update({a:value[a] for a in ('code','reason') if a in value})
                run['sources'].append(row)
        except Exception as exc:
            run['sources'].extend({'platform':p,'kind':k,'state':'FAILED','code':exc.code if isinstance(exc,VpubError) else type(exc).__name__} for k in wanted)
    run.update(completed_at=now(),state='COLLECTED' if all(r['state']=='OBSERVED' for r in run['sources']) else 'PARTIAL',
               comments_run_id=(comments or {}).get('run_id'),
               comment_report=(comments or {}).get('report'),
               scope='Local inbox only. No replies, private messages or email sent. Opening platform inboxes may change platform read badges.')
    lang=operations.reporting_language(language)
    good=sum(r['state']=='OBSERVED' for r in run['sources']);new=sum(r.get('newly_observed',0) for r in run['sources'])
    run['report']=(f'本轮读取 {good} 个消息来源，新观察到 {new} 条提及或会话预览；首次读取仅建立基线，受限来源见明细。未发送回复、私信或邮件。' if lang=='zh' else
                   f'Read {good} message sources; {new} newly observed mentions or conversation previews. First reads establish a baseline; coverage limits are listed separately. No replies, DMs or email sent.')
    atomic_json(home()/'operations/message-runs'/(run['run_id']+'.json'),run)
    return run

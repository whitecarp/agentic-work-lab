"""Read-only multi-platform watchlists and bounded collection; never send replies."""
import math
import re
import uuid

from . import adapters, operations as ops
from .core import VpubError, atomic_json, home, now, platform_check, publication_lock, read_json, stop_check


def metric_changes(previous, current):
    changes={}
    for name, metric in current.get('metrics',{}).items():
        old=(previous or {}).get('metrics',{}).get(name,{})
        value,before=metric.get('value'),old.get('value')
        numeric=lambda v:isinstance(v,(int,float)) and not isinstance(v,bool) and math.isfinite(v)
        comparable=bool(previous and previous.get('identity')==current.get('identity')
            and previous.get('source')==current.get('source') and previous.get('work_id')==current.get('work_id')
            and metric.get('unit')==old.get('unit') and metric.get('label')==old.get('label')
            and numeric(value) and numeric(before) and not metric.get('approximate') and not old.get('approximate'))
        changes[name]={'previous':before,'current':value,'delta':value-before if comparable else None,
                       'unit':'percentage_points' if metric.get('unit')=='percent' else metric.get('unit'),
                       'comparable':comparable}
    return changes


def works(platform, limit=20, cursor=None):
    platform_check(platform);stop_check(platform)
    if not 1<=limit<=50: raise VpubError('invalid_limit','Work limit must be 1–50.')
    if cursor and (not isinstance(cursor,str) or len(cursor)>1000): raise VpubError('invalid_cursor','Invalid cursor.')
    if platform=='youtube':
        service=adapters.yt_service('default')
        channels=service.channels().list(part='id,contentDetails',mine=True).execute().get('items',[])
        if len(channels)!=1: raise VpubError('channel_required','Expected one authorized channel.')
        owner=channels[0]['id'];playlist=channels[0].get('contentDetails',{}).get('relatedPlaylists',{}).get('uploads')
        if not playlist: raise VpubError('uploads_unavailable','The channel did not expose an uploads playlist.')
        args={'part':'snippet,contentDetails','playlistId':playlist,'maxResults':limit}
        if cursor: args['pageToken']=cursor
        data=service.playlistItems().list(**args).execute()
        ids=[c['contentDetails']['videoId'] for c in data.get('items',[])]
        videos=service.videos().list(part='snippet,status',id=','.join(ids)).execute().get('items',[]) if ids else []
        items=[{'id':v['id'],'title':v['snippet']['title'],'url':'https://www.youtube.com/watch?v='+v['id'],
                'visibility':v.get('status',{}).get('privacyStatus'),'published_at':v['snippet'].get('publishedAt')}
               for v in videos if v['snippet'].get('channelId')==owner]
        result={'identity':owner,'items':items,'next_cursor':data.get('nextPageToken'),
                'complete':not cursor and not data.get('nextPageToken') and len(items)==len(ids),
                'source':'youtube_uploads_playlist','scope':'One page of the authorized channel uploads; inaccessible items omitted.',
                'omitted_count':len(ids)-len(items)}
    elif platform=='bilibili':
        if cursor and not re.fullmatch(r'[1-9][0-9]{0,7}',cursor): raise VpubError('invalid_cursor','Use a positive page number.')
        page=int(cursor or 1);owner=adapters.auth_status('bilibili','default','cli')['identity']
        data=adapters.bili(['upload','list','--status','published','--page',str(page),'--size',str(limit)])
        items=[{'id':v['bvid'],'title':v['title'],'url':'https://www.bilibili.com/video/'+v['bvid'],
                'published_at':v.get('publish_time'),'status':v.get('state_desc')} for v in data.get('items',[]) if v.get('published')]
        total=data.get('counts',{}).get('published')
        more=page*limit<total if isinstance(total,int) else len(data.get('items',[]))==limit
        result={'identity':owner,'items':items,'next_cursor':str(page+1) if more else None,'complete':page==1 and not more,
                'source':'bili_cli_creator_list','scope':'Published works only; scheduled/review works are excluded.','reported_total':total}
    else:
        if cursor: raise VpubError('unsupported_cursor','This browser work list exposes the visible first page only.')
        result=adapters.browser_call(platform,'works','default')
        result.update(items=result['items'][:limit],next_cursor=None,complete=False,source='cli_browser_visible_works')
    return {**result,'platform':platform,'observed_at':now(),'platform_writes':0}


def watch(action, platform=None, work=None):
    path=home()/'operations/watchlist.json'
    if action=='list': return read_json(path) if path.exists() else {'schema_version':1,'items':[]}
    work=ops.work_id(platform,work)
    observed=None
    if action=='add':
        # API owner checks or the existing browser publication receipt bind the account.
        observed=ops.observe(platform,work,'stats')
    elif action!='remove': raise VpubError('invalid_operation','Choose add, remove or list.')
    with publication_lock():
        data=watch('list')
        items=[x for x in data['items'] if (x['platform'],x['work_id'])!=(platform,work)]
        if observed:
            items.append({'platform':platform,'work_id':work,'identity':observed['identity'],
                          'added_at':now(),'title':observed.get('title'),'url':observed.get('url')})
        data={'schema_version':1,'items':items};atomic_json(path,data)
    return {**data,'platform_writes':0}


def collect(platform, work, identity, limit, pages):
    cursor=None;seen_cursors=set();rows={};first=None;last=None;complete=True
    for index in range(pages):
        stop_check(platform)
        page=ops.backend(platform,'comments',work,limit=limit,cursor=cursor)
        if page.get('identity')!=identity: raise VpubError('wrong_account','The watched account changed. No snapshot was advanced.')
        if first is None:first=page
        for c in page.get('items',[]):rows[c['id']]=c
        # API thread previews can be partial even when top-level pagination ends.
        complete=complete and page.get('replies_complete',page.get('complete',False) or bool(page.get('next_cursor')))
        last=page;cursor=page.get('next_cursor')
        if not cursor:break
        if cursor in seen_cursors: raise VpubError('cursor_loop','Backend repeated a page cursor; collection stopped.')
        seen_cursors.add(cursor)
    result={**first,'items':list(rows.values()),'next_cursor':cursor,'pages_read':index+1,
            'complete':bool(platform in {'bilibili','youtube'} and complete and not cursor and last.get('complete')),
            'bounded':True,'page_budget':pages}
    return ops.save_observation(platform,work,'comments',result)


def sync(platforms=None, limit=20, pages=2, language='auto'):
    if not 1<=limit<=100 or not 1<=pages<=10:raise VpubError('invalid_limit','Use limit 1–100 and pages 1–10.')
    selected=platforms.split(',') if platforms else list(ops.NAMES)
    for p in selected:platform_check(p)
    targets=[x for x in watch('list')['items'] if x['platform'] in selected]
    if not targets:raise VpubError('watchlist_empty','Add your works with vpub ops watch add PLATFORM WORK first.')
    run={'run_id':uuid.uuid4().hex,'started_at':now(),'platform_writes':0,'items':[],'errors':[],'inbox':[]}
    events=[]
    for target in targets:
        p,w=target['platform'],target['work_id'];item={**target};run['items'].append(item)
        for action in ('comments','stats'):
            try:
                stop_check(p)
                if action=='comments':value=collect(p,w,target['identity'],limit,pages)
                else:
                    value=ops.backend(p,'stats',w)
                    if value.get('identity')!=target['identity']:raise VpubError('wrong_account','The watched account changed.')
                    value=ops.save_observation(p,w,action,value)
                item[action]=value
                events.append({'platform':p,'work_id':w,'action':action,'at':value['observed_at'],'result':value})
                if action=='comments':
                    for c in value['items']:
                        if c.get('is_owner') or c.get('owner_replied') is True:continue
                        run['inbox'].append({'platform':p,'work_id':w,'comment':c,
                            'status':'unreplied' if c.get('owner_replied') is False else 'needs_thread_check',
                            'reply_language':c.get('language_hint','und')})
            except Exception as exc:
                code=exc.code if isinstance(exc,VpubError) else type(exc).__name__
                error={'platform':p,'work_id':w,'action':action,'code':code}
                run['errors'].append(error);item[action]={'state':'FAILED','code':code}
                event={'platform':p,'work_id':w,'action':action,'at':now(),'result':item[action]};events.append(event)
                ops.record(action,p,w,item[action])
                # Do not retry a challenged account through another operation or route.
                break
    run.update(completed_at=now(),state='PARTIAL' if run['errors'] else 'COLLECTED',
               report=ops.report(since=run['started_at'],language=language,_events=events),
               scope='Watched works only; bounded comments, not a private-message or complete account inbox. No replies, likes or email sent.')
    path=home()/'operations/runs'/(run['run_id']+'.json');atomic_json(path,run)
    run['receipt_path']=str(path)
    return run

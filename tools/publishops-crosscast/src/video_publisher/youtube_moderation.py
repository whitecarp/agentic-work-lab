"""Read moderation state without treating absence or thread membership as proof."""
from .core import VpubError, stop_check


def state(service,work,cid,parent=None):
    if parent:
        token=None
        for _ in range(3):
            stop_check('youtube')
            args={'part':'snippet','parentId':parent,'maxResults':100,'textFormat':'plainText'}
            if token:args['pageToken']=token
            data=service.comments().list(**args).execute()
            for c in data.get('items',[]):
                if c['id']==cid:return c['snippet'].get('moderationStatus')
            token=data.get('nextPageToken')
            if not token:break
        return None
    # comments.list(id=...) explicitly omits moderationStatus. A thread filter
    # can match a held child, so it only establishes root state when childless.
    states=set()
    for desired in ('published','heldForReview'):
        token=None;seen=set()
        for _ in range(3):
            stop_check('youtube')
            args={'part':'snippet','videoId':work,'moderationStatus':desired,'maxResults':100,'textFormat':'plainText'}
            if token:args['pageToken']=token
            data=service.commentThreads().list(**args).execute()
            for t in data.get('items',[]):
                s=t['snippet'];c=s['topLevelComment']
                if c['id']!=cid:continue
                if s.get('videoId')!=work:raise VpubError('wrong_work','Review queue returned another video.')
                actual=c['snippet'].get('moderationStatus')
                if actual:return actual
                if s.get('totalReplyCount')==0:states.add(desired)
            token=data.get('nextPageToken')
            if not token:break
            if token in seen:raise VpubError('cursor_loop','Moderation queue repeated a cursor.')
            seen.add(token)
    return states.pop() if len(states)==1 else None

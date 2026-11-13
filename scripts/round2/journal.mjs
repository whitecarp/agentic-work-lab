import fs from 'node:fs';
import path from 'node:path';
import {pathToFileURL} from 'node:url';
import {createHmac, randomBytes} from 'node:crypto';
import {validateRecord, SLOTS, PLATFORMS} from './schema.mjs';
import {refusalWord,sha, utc, atomic, withLock, privateHome, readJSON, parseFlags, allowed, alive} from './io.mjs';
import {bindHome,HOME_ERRORS} from './site.mjs';
import {jobList} from './jobs.mjs';
import {verifyPlan} from './plan-runs.mjs';
import {lookTiming,lookStep} from './looks.mjs';
export {validateRecord};
export const ZERO='0'.repeat(64);
export const newRunId=()=>randomBytes(16).toString('hex');
export function canonical(value) {
  if(value===null || typeof value!=='object') return JSON.stringify(value);
  if(Array.isArray(value)) return '['+value.map(canonical).join(',')+']';
  return '{'+Object.keys(value).sort().map(k=>JSON.stringify(k)+':'+canonical(value[k])).join(',')+'}';
}
function scan(file, recover=false) {
  const problems=[], records=[], hashes=[ZERO];
  let bytes;
  try {bytes=fs.readFileSync(file);} catch(e){if(e.code==='ENOENT') bytes=Buffer.alloc(0);else throw e;}
  if(bytes.length && bytes.at(-1)!==10) problems.push('truncated_last_line');
  const lines=bytes.toString('utf8').split('\n'); lines.pop();
  let previous=ZERO;
  for(const [i,line] of lines.entries()){
    try {
      const r=JSON.parse(line), errors=validateRecord(r);
      if(errors.length || canonical(r)!==line || r.seq!==i+1 || r.prev!==previous) problems.push('invalid_record_'+(i+1));
      records.push(r); previous=sha(Buffer.from(line)); hashes.push(previous);
    } catch {problems.push('invalid_record_'+(i+1));}
  }
  const head=readJSON(file+'.head',null);
  if(head && (head.records>records.length || hashes[head.records]!==head.head_sha256 ||
      (!recover && (head.records!==records.length || head.head_sha256!==previous)))) problems.push('head_mismatch');
  if(!head && records.length && !recover) problems.push('missing_head');
  return {ok:problems.length===0,records:records.length,head_sha256:previous,problems,items:records};
}
export function verifyJournal(file) {
  try {const {items,...result}=scan(file);return result;}
  catch {return {ok:false,records:0,head_sha256:ZERO,problems:['journal_unreadable']};}
}
export function readJournal(file) {
  const result=scan(file);
  if(!result.ok) throw new Error('broken_journal');
  return result.items;
}
export function appendRecord(file,fields,timeoutMs=1000) {
  return withLock(file+'.lock',()=>{
    const current=scan(file,true);
    if(!current.ok) throw new Error('broken_journal');
    if(current.items.some(r=>r.site!==fields.site)) throw new Error('site_mismatch');
    const record={...fields,v:1,seq:current.records+1,prev:current.head_sha256,at:utc()};
    if(Object.keys(fields).some(k=>!['site','type','run','slot','d'].includes(k))) throw new Error('unknown_record_field');
    if(validateRecord(record).length) throw new Error('invalid_record');
    const line=canonical(record);
    const fd=fs.openSync(file,'a',0o600);
    try{fs.writeFileSync(fd,line+'\n');fs.fsyncSync(fd);}finally{fs.closeSync(fd);}
    atomic(file+'.head',{records:record.seq,head_sha256:sha(line)});
    return record;
  },timeoutMs);
}
export function keyedDigest(value,home,{create=true}={}) {
  const file=path.join(home,'salt');
  return withLock(file+'.lock',()=>{
    if(!fs.existsSync(file)) {
      if(!create)throw new Error('invalid_salt');
      const fd=fs.openSync(file,'wx',0o600);
      try{fs.writeFileSync(fd,randomBytes(32));fs.fsyncSync(fd);}finally{fs.closeSync(fd);}
    }
    const salt=fs.readFileSync(file);
    if(salt.length!==32) throw new Error('invalid_salt');
    return createHmac('sha256',salt).update(String(value)).digest('hex');
  });
}
export function pseudonym(value,home) {
  return keyedDigest(value,home).slice(0,16);
}
export function context(flags, needHome=true) {
  const site=flags.site;
  if(!['owner','tester'].includes(site)) throw new Error('invalid_site');
  const home=needHome||process.env.ROUND2_HOME?privateHome():null;
  const journal=path.resolve(flags.journal??path.join('data','round2',site,'journal.jsonl'));
  const slot=flags.slot;
  if(!SLOTS.includes(slot)||slot==='scheduler') throw new Error('invalid_slot');
  const ctx={site,home,journal,slot};checkJournalPlace(ctx);return ctx;
}
export function checkJournalPlace(ctx,sealing=false){
  if(!ctx.home)return;
  const file=path.join(ctx.home,'journal-place.json'),absolute=path.resolve(ctx.journal);
  const equal=(a,b)=>process.platform==='win32'?a.toLowerCase()===b.toLowerCase():a===b;
  const pinned=readJSON(file,null);
  if(pinned&&!equal(pinned.journal,absolute))throw new Error('journal_place_mismatch');
  if(sealing&&!pinned)atomic(file,{journal:absolute});
}
export function put(ctx,type,d,run=null,timeoutMs=1000) {
  checkJournalPlace(ctx,type==='PROTOCOL_SEAL');
  if(type==='GO_AHEAD'&&ctx.home){
    const file=path.join(ctx.home,'reservations.json');
    if(!fs.existsSync(file)){
      if(readJournal(ctx.journal).some(r=>['GO_AHEAD','STEP'].includes(r.type)))throw new Error('reservations_missing');
      atomic(file,[]);
    }
  }
  return appendRecord(ctx.journal,{site:ctx.site,slot:ctx.slot,type,run,d},timeoutMs);
}
export function operator(ctx, records) {
  const value=readJSON(path.join(ctx.home,'operator.json'),{operator:ctx.site}).operator;
  const last=records.filter(r=>r.type==='HANDOFF').at(-1);
  // A manually edited or incompletely handed-off operator is never trusted.
  if(!SLOTS.includes(value)||value!==(last?.d.to??ctx.site)) throw new Error('operator_record_mismatch');
  return value;
}

export function openSessions(records,site){
  const ended=new Set(records.filter(r=>r.site===site && r.type==='RUN_END').map(r=>r.run));
  return records.filter(r=>r.site===site && r.type==='GATE_DECISION' &&
    r.d.decision==='plan_only' && !ended.has(r.run));
}

export function activeStops(records){
  let active=[];
  for(const r of records){
    if(r.type==='STOP_SET')active.push(r.d.by);
    if(r.type==='STOP_CLEARED'){
      if(r.d.by===r.site&&r.d.words_sha256)active=[];
      else if(r.d.by===r.slot&&!['owner','tester','scheduler'].includes(r.d.by))
        active=active.filter(by=>by!==r.d.by);
    }
  }
  return active;
}
export function words(home,messageId=null) {
  const bytes=fs.readFileSync(0);
  if(!bytes.toString('utf8').trim()) throw new Error('empty_words');
  return commitWords(home,bytes,messageId);
}
export function commitWords(home,bytes,messageId=null) {
  if(messageId!==null){
    if(!/^[a-zA-Z0-9._-]{1,80}$/.test(messageId))throw new Error('invalid_message_id');
    const file=path.join(home,'message-commitments',keyedDigest('message:'+messageId,home)+'.json');
    return withLock(file+'.lock',()=>{
      const existing=readJSON(file,null),content=keyedDigest(bytes,home);
      if(existing){if(existing.content!==content)throw new Error('message_id_words_changed');return existing.digest;}
      const digest=commitWords(home,bytes);atomic(file,{content,digest});return digest;
    });
  }
  if(/^[?\s]+$/.test(Buffer.from(bytes).toString('utf8').trim()))throw new Error('words_unreadable');
  const nonce=randomBytes(32),digest=sha(Buffer.concat([nonce,Buffer.from(bytes)]));
  // The opening stays beside the private words; neither is in a public record.
  atomic(path.join(home,'captures',digest+'.words.nonce'),nonce);
  atomic(path.join(home,'captures',digest+'.words.txt'),bytes);
  return digest;
}
const numeric=(s)=>{if(!/^\d+$/.test(s??''))throw new Error('invalid_integer');return Number(s);};
function fileHash(file){return sha(fs.readFileSync(file,'utf8').replace(/\r\n/g,'\n'));}
export function clearGuard(ctx,by,statement){
  if(by!==ctx.site) throw new Error('invalid_person');
  if(!statement.toString('utf8').trim()) throw new Error('empty_words');
  const guard=path.join(ctx.home,'operation.guard');
  return withLock(guard+'.recovery',()=>{
    // Recovery is explicit and never clears the session, STOP, or reservations.
    const candidates=[guard,ctx.journal+'.lock'].filter(f=>fs.existsSync(f));
    if(!candidates.length) throw new Error('no_guard');
    const originals=candidates.map(file=>{
      const bytes=fs.readFileSync(file), owner=JSON.parse(bytes);
      if(!Number.isSafeInteger(owner.pid)||owner.pid<=0||alive(owner.pid)) throw new Error('guard_owner_not_dead');
      return {file,bytes};
    });
    if(!verifyJournal(ctx.journal).ok) throw new Error('broken_journal');
    const digest=commitWords(ctx.home,statement);
    for(const {file,bytes} of originals){
      if(!fs.readFileSync(file).equals(bytes)) throw new Error('guard_changed');
      fs.unlinkSync(file);
    }
    try{
      return put(ctx,'NOTE',{code:'guard_cleared',platform:null,job:null,agent:null,minutes_ago:null,words_sha256:digest});
    }catch(e){
      // A recording failure must not silently reopen the gate. The preserved
      // stale owner can be reviewed again; no run or command is retried.
      for(const {file,bytes} of originals) if(!fs.existsSync(file)) fs.writeFileSync(file,bytes,{flag:'wx',mode:0o600});
      throw e;
    }
  });
}
export function main(argv=process.argv.slice(2)){
  const sub=argv.shift();
  if(sub==='verify') {
    if(argv.length!==3||argv[1]!=='--slot'||!SLOTS.includes(argv[2])||argv[2]==='scheduler') throw new Error('invalid_slot');
    const result=verifyJournal(argv[0]);console.log(JSON.stringify(result));return result.ok?0:70;
  }
  const closing=argv.includes('--closing');
  if(closing&&(sub!=='sign-off'||argv.filter(a=>a==='--closing').length!==1))throw new Error('invalid_arguments');
  const {flags:f,command}=parseFlags(argv.filter(a=>a!=='--closing'));
  if(command.length) throw new Error('invalid_arguments');
  const lists={
    'go-ahead':['job','scope','count','by','platform','purpose','message-id'],observe:['job','ref','about','by','client','seen','fields-match','minutes-after','shot-sha256','platform'],
    note:['code','platform','job','agent','minutes-ago'],violation:['rule','found-by','job','platform'],
    'sign-off':['day','by','answer'],handoff:['to','job','by'],stop:['by'],'clear-stop':['by'],'clear-guard':['by'],
    'protocol-seal':['phase','protocol','lab-commit','crosscast-commit','crosscast-digest','plan','analysis','rules-card','by'],
    'real-release':['job','platform','due-at','by'],
    'browser-faults':['platform','enabled','by'],
  };
  if(!lists[sub]) throw new Error('unknown_command');
  allowed(f,['site','slot','journal','run',...lists[sub]]);
  if(sub==='note' && f.platform?.length>1) throw new Error('one_platform_required');
  const ctx=context(f), run=f.run??null;
  if(['go-ahead','observe','sign-off','handoff','protocol-seal','real-release','clear-guard'].includes(sub)&&f.by!==undefined&&f.by!==ctx.site)throw new Error('invalid_person');
  if(sub==='clear-guard') {console.log(JSON.stringify(clearGuard(ctx,f.by,fs.readFileSync(0))));return 0;}
  return withLock(path.join(ctx.home,'operation.guard'),()=>{
    // Verify before reading private statements or changing any control file.
    if(!verifyJournal(ctx.journal).ok) throw new Error('broken_journal');
    let type,d;
    switch(sub){
      case 'browser-faults':
        if(f.by!==ctx.site||f.enabled!=='false'||f.platform?.length!==1||!['youtube','douyin'].includes(f.platform[0]))throw new Error('invalid_browser_fault_policy');
        type='FAULT_PLATFORM_POLICY';d={platform:f.platform[0],enabled:false,by:f.by,words_sha256:words(ctx.home)};break;
      case 'go-ahead':{
        const job=jobList(ctx,readJournal(ctx.journal)).find(j=>j.id===f.job);
        if(f.scope==='one_delete'&&(!job||f.platform?.length!==1||!job.platforms?.includes(f.platform[0])||f.platform[0]==='bilibili'))throw new Error('delete_approval_platform_required');
        if(f.scope!=='one_delete'&&f.platform)throw new Error('unexpected_platform');
        if(f.purpose&&f.scope!=='one_write')throw new Error('unexpected_approval_purpose');
        const purpose=f.scope==='one_write'?(f.purpose??'publish'):null;
        if(purpose&&!['publish','edit','resume'].includes(purpose))throw new Error('invalid_approval_purpose');
        const digest=words(ctx.home,f['message-id']??null);
        if(f['message-id']&&readJournal(ctx.journal).some(r=>r.type==='GO_AHEAD'&&r.d.job===f.job&&r.d.words_sha256===digest))throw new Error('approval_message_replayed');
        type='GO_AHEAD';d={job:f.job,scope:f.scope,count:numeric(f.count),by:f.by??ctx.site,words_sha256:digest,...(purpose?{purpose}:{})};
        if(f.scope==='one_delete')d.platform=f.platform[0];break;
      }
      case 'observe':{
        if(![undefined,'true','false','null'].includes(f['fields-match'])) throw new Error('invalid_boolean');
        const job=jobList(ctx,readJournal(ctx.journal)).find(j=>j.id===f.job), choices=job?.platforms??[];
        if(f.platform?.length>1 || (!f.platform && choices.length>1)) throw new Error('one_platform_required');
        const platform=f.platform?.[0]??(choices.length===1?choices[0]:null);
        if(platform!==null&&(!PLATFORMS.includes(platform)||(choices.length&&!choices.includes(platform)))) throw new Error('invalid_platform');
        const records=readJournal(ctx.journal),minutes=numeric(f['minutes-after']);
        const follows=lookStep(records,f.job,platform,f.about);
        if(follows&&Date.now()-Date.parse(follows.at)<minutes*60000)throw new Error('look_minute_not_reached');
        type='OBSERVATION';d={job:f.job,platform,ref:f.ref??null,about:f.about,by:f.by??ctx.site,client:f.client,seen:f.seen,
          fields_match:f['fields-match']==='true'?true:f['fields-match']==='false'?false:null,
          minutes_after:minutes,words_sha256:words(ctx.home),shot_sha256:f['shot-sha256']??null};break;
      }
      case 'note':
        type='NOTE';d={code:f.code,platform:f.platform?.length===1?f.platform[0]:null,job:f.job??null,agent:f.agent??null,
          minutes_ago:f['minutes-ago']===undefined?null:numeric(f['minutes-ago'])};break;
      case 'violation':
        if(f.platform?.length>1)throw new Error('one_platform_required');
        type='VIOLATION';d={rule:f.rule,found_by:f['found-by'],job:f.job??null,...(f.platform?{platform:f.platform[0]}:{})};break;
      case 'sign-off':{
        const head=verifyJournal(ctx.journal);
        // A yes without piped text needs no stdin prompt; a no always requires
        // the person's explanation. Optional yes words are kept only privately.
        const statement=f.answer==='no'||!process.stdin.isTTY?fs.readFileSync(0):Buffer.alloc(0);
        if(f.answer==='no'&&!statement.toString('utf8').trim()) throw new Error('empty_words');
        const digest=statement.toString('utf8').trim()?commitWords(ctx.home,statement):null;
        type='SIGN_OFF';d={day:f.day,by:f.by??ctx.site,answer:f.answer,words_sha256:digest,head_sha256:head.head_sha256,records:head.records,closing};break;
      }
      case 'handoff':
        if(openSessions(readJournal(ctx.journal),ctx.site).length)throw new Error('session_open');
        type='HANDOFF';d={from:readJSON(path.join(ctx.home,'operator.json'),{operator:ctx.site}).operator,
          to:f.to,job:f.job??null,by:f.by??ctx.site,words_sha256:words(ctx.home)};break;
      case 'stop':
        if(f.by&&f.by!==ctx.slot&&f.by!==ctx.site)throw new Error('invalid_stop_setter');
        type='STOP_SET';d={by:f.by??ctx.slot};break;
      case 'clear-stop':{
        const by=f.by??ctx.slot, active=activeStops(readJournal(ctx.journal));
        if(by!==ctx.site&&(by!==ctx.slot||['scheduler','owner','tester'].includes(by)||
            !active.includes(by)))throw new Error('stop_clear_refused');
        type='STOP_CLEARED';d={by};
        if(by===ctx.site)d.words_sha256=words(ctx.home);
        break;
      }
      case 'real-release': {
        const records=readJournal(ctx.journal),by=f.by??ctx.site;
        if(by!==ctx.site||jobList(ctx,records).some(j=>j.id===f.job))throw new Error('invalid_real_release');
        const platforms=f.platform??[];
        if(!platforms.length||new Set(platforms).size!==platforms.length||platforms.some(p=>!PLATFORMS.includes(p))||!Number.isFinite(Date.parse(f['due-at'])))throw new Error('invalid_real_release');
        type='REAL_RELEASE';d={job:f.job,platforms,due_at:new Date(f['due-at']).toISOString(),run_class:'real_release',
          release_writes:1+3*platforms.length,by,words_sha256:words(ctx.home)};break;
      }
      case 'protocol-seal':{
        const records=readJournal(ctx.journal),prior=records.filter(r=>r.type==='PROTOCOL_SEAL');
        const rank={rehearsal:0,'2a':1,'2b':2};
        const frozen=prior.filter(r=>r.d.phase==='2b');
        if(!(f.phase in rank)||prior.some(r=>rank[r.d.phase]>rank[f.phase]))throw new Error('seal_phase_not_forward');
        if(frozen.length>=3)throw new Error('refreeze_limit');
        if(frozen.length&&f.by!==ctx.site)throw new Error('refreeze_owner_words_required');
        if(['2a','2b'].includes(f.phase)&&!f['rules-card'])throw new Error('rules_card_required');
        if(f.phase==='2b'&&(!f['crosscast-digest']||!f.analysis))throw new Error('freeze_digests_required');
        if(f.phase==='2b'){
          if(!f.plan||path.resolve(f.plan)!==path.join(ctx.home,'planned-runs.json'))throw new Error('sealed_plan_required');
          verifyPlan(ctx.home);
          if(frozen.length&&sha(fs.readFileSync(f.plan))!==frozen[0].d.plan_sha256)throw new Error('refreeze_plan_changed');
        }
        type='PROTOCOL_SEAL';d={vpub_home_sha256:bindHome(ctx,readJournal(ctx.journal),{sealing:true}).digest,
          protocol_sha256:fileHash(f.protocol),lab_commit:f['lab-commit'],crosscast_commit:f['crosscast-commit'],
          phase:f.phase,crosscast_digest:f['crosscast-digest']??null,plan_sha256:f.plan?sha(fs.readFileSync(f.plan)):null,
          analysis_sha256:f.analysis?fileHash(f.analysis):null,rules_card_sha256:f['rules-card']?fileHash(f['rules-card']):null};
        if(frozen.length)Object.assign(d,{by:f.by,words_sha256:words(ctx.home)});
        break;
      }
    }
    const record=put(ctx,type,d,run);
    if(sub==='handoff') atomic(path.join(ctx.home,'operator.json'),{operator:f.to});
    if(sub==='stop') atomic(path.join(ctx.home,'STOP'),{at:record.at});
    if(sub==='clear-stop' && !activeStops(readJournal(ctx.journal)).length && fs.existsSync(path.join(ctx.home,'STOP'))) fs.unlinkSync(path.join(ctx.home,'STOP'));
    console.log(JSON.stringify(sub==='observe'?{...record,look:lookTiming(record,lookStep(readJournal(ctx.journal),d.job,d.platform,d.about),readJournal(ctx.journal))}:record));return 0;
  });
}
if(process.argv[1] && import.meta.url===pathToFileURL(path.resolve(process.argv[1])).href){
  try{process.exitCode=main();}catch(e){console.error(refusalWord(e,'journal_refused'));process.exitCode=70;}
}

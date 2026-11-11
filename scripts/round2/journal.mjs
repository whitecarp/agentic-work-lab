import fs from 'node:fs';
import path from 'node:path';
import {pathToFileURL} from 'node:url';
import {createHmac, randomBytes} from 'node:crypto';
import {validateRecord, SLOTS} from './schema.mjs';
import {sha, utc, atomic, withLock, privateHome, readJSON, parseFlags, allowed, alive} from './io.mjs';
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
export function appendRecord(file,fields) {
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
  });
}
export function pseudonym(value,home) {
  const file=path.join(home,'salt');
  return withLock(file+'.lock',()=>{
    if(!fs.existsSync(file)) {
      const fd=fs.openSync(file,'wx',0o600);
      try{fs.writeFileSync(fd,randomBytes(32));fs.fsyncSync(fd);}finally{fs.closeSync(fd);}
    }
    const salt=fs.readFileSync(file);
    if(salt.length!==32) throw new Error('invalid_salt');
    return createHmac('sha256',salt).update(String(value)).digest('hex').slice(0,16);
  });
}
export function context(flags, needHome=true) {
  const site=flags.site;
  if(!['owner','tester'].includes(site)) throw new Error('invalid_site');
  const home=needHome?privateHome():null;
  const journal=path.resolve(flags.journal??path.join('data','round2',site,'journal.jsonl'));
  const slot=flags.slot??flags.by??site;
  if(!SLOTS.includes(slot)) throw new Error('invalid_slot');
  return {site,home,journal,slot};
}
export function put(ctx,type,d,run=null) {
  return appendRecord(ctx.journal,{site:ctx.site,slot:ctx.slot,type,run,d});
}
export function words(home) {
  const bytes=fs.readFileSync(0);
  if(!bytes.toString('utf8').trim()) throw new Error('empty_words');
  const digest=sha(bytes), file=path.join(home,'captures',digest+'.words.txt');
  if(!fs.existsSync(file)) atomic(file,bytes);
  return digest;
}
const numeric=(s)=>{if(!/^\d+$/.test(s??''))throw new Error('invalid_integer');return Number(s);};
function fileHash(file){return sha(fs.readFileSync(file,'utf8').replace(/\r\n/g,'\n'));}
export function clearGuard(ctx,by,statement){
  if(!['owner','tester'].includes(by)) throw new Error('invalid_person');
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
    const digest=sha(statement);atomic(path.join(ctx.home,'captures',digest+'.words.txt'),statement);
    for(const {file,bytes} of originals){
      if(!fs.readFileSync(file).equals(bytes)) throw new Error('guard_changed');
      fs.unlinkSync(file);
    }
    try{
      return put({...ctx,slot:by},'NOTE',{code:'guard_cleared',platform:null,job:null,agent:null,minutes_ago:null,words_sha256:digest});
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
    if(argv.length!==1) throw new Error('invalid_arguments');
    const result=verifyJournal(argv[0]);console.log(JSON.stringify(result));return result.ok?0:70;
  }
  const {flags:f,command}=parseFlags(argv);
  if(command.length) throw new Error('invalid_arguments');
  const lists={
    'go-ahead':['job','scope','count','by'],observe:['job','ref','about','by','client','seen','fields-match','minutes-after','shot-sha256'],
    note:['code','platform','job','agent','minutes-ago'],violation:['rule','found-by','job'],
    'sign-off':['day','by','answer'],handoff:['to','job','by'],stop:['by'],'clear-stop':['by'],'clear-guard':['by'],
    'protocol-seal':['phase','protocol','lab-commit','crosscast-commit','crosscast-digest','plan','analysis','rules-card'],
  };
  if(!lists[sub]) throw new Error('unknown_command');
  allowed(f,['site','slot','journal','run',...lists[sub]]);
  if(sub==='note' && f.platform?.length>1) throw new Error('one_platform_required');
  const ctx=context(f), run=f.run??null;
  if(sub==='clear-guard') {console.log(JSON.stringify(clearGuard(ctx,f.by,fs.readFileSync(0))));return 0;}
  return withLock(path.join(ctx.home,'operation.guard'),()=>{
    // Verify before reading private statements or changing any control file.
    if(!verifyJournal(ctx.journal).ok) throw new Error('broken_journal');
    let type,d;
    switch(sub){
      case 'go-ahead':
        type='GO_AHEAD';d={job:f.job,scope:f.scope,count:numeric(f.count),by:f.by??ctx.site,words_sha256:words(ctx.home)};break;
      case 'observe':
        if(![undefined,'true','false','null'].includes(f['fields-match'])) throw new Error('invalid_boolean');
        type='OBSERVATION';d={job:f.job,ref:f.ref??null,about:f.about,by:f.by??ctx.site,client:f.client,seen:f.seen,
          fields_match:f['fields-match']==='true'?true:f['fields-match']==='false'?false:null,
          minutes_after:numeric(f['minutes-after']),words_sha256:words(ctx.home),shot_sha256:f['shot-sha256']??null};break;
      case 'note':
        type='NOTE';d={code:f.code,platform:f.platform?.length===1?f.platform[0]:null,job:f.job??null,agent:f.agent??null,
          minutes_ago:f['minutes-ago']===undefined?null:numeric(f['minutes-ago'])};break;
      case 'violation':type='VIOLATION';d={rule:f.rule,found_by:f['found-by'],job:f.job??null};break;
      case 'sign-off':{
        const head=verifyJournal(ctx.journal);type='SIGN_OFF';d={day:f.day,by:f.by??ctx.site,answer:f.answer,head_sha256:head.head_sha256,records:head.records};break;
      }
      case 'handoff':
        type='HANDOFF';d={from:readJSON(path.join(ctx.home,'operator.json'),{operator:ctx.site}).operator,
          to:f.to,job:f.job??null,by:f.by??ctx.site,words_sha256:words(ctx.home)};break;
      case 'stop':type='STOP_SET';d={by:f.by??ctx.slot};break;
      case 'clear-stop':type='STOP_CLEARED';d={by:f.by??ctx.slot};break;
      case 'protocol-seal':
        type='PROTOCOL_SEAL';d={protocol_sha256:fileHash(f.protocol),lab_commit:f['lab-commit'],crosscast_commit:f['crosscast-commit'],
          phase:f.phase,crosscast_digest:f['crosscast-digest']??null,plan_sha256:f.plan?fileHash(f.plan):null,
          analysis_sha256:f.analysis?fileHash(f.analysis):null,rules_card_sha256:f['rules-card']?fileHash(f['rules-card']):null};break;
    }
    const record=put(ctx,type,d,run);
    if(sub==='handoff') atomic(path.join(ctx.home,'operator.json'),{operator:f.to});
    if(sub==='stop') atomic(path.join(ctx.home,'STOP'),{at:record.at});
    if(sub==='clear-stop' && fs.existsSync(path.join(ctx.home,'STOP'))) fs.unlinkSync(path.join(ctx.home,'STOP'));
    console.log(JSON.stringify(record));return 0;
  });
}
if(process.argv[1] && import.meta.url===pathToFileURL(path.resolve(process.argv[1])).href){
  try{process.exitCode=main();}catch{console.error('journal_refused');process.exitCode=70;}
}

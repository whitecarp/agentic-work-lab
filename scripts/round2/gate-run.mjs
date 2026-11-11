import fs from 'node:fs';
import path from 'node:path';
import {pathToFileURL} from 'node:url';
import {spawn} from 'node:child_process';
import {StringDecoder} from 'node:string_decoder';
import {planRun} from '../operations-gate.mjs';
import {context, put, readJournal, verifyJournal, newRunId, pseudonym, words} from './journal.mjs';
import {ID, SLOTS, PLATFORMS, REFUSALS} from './schema.mjs';
import {sha, utc, atomic, withLock, readJSON, parseFlags, allowed, alive} from './io.mjs';
import {classify, guardRole, grant, grantScope, limits, outcome, roles} from './policy.mjs';
import {bindHome,HOME_ERRORS} from './site.mjs';
import {jobList} from './jobs.mjs';

const gateFile=new URL('../operations-gate.mjs',import.meta.url);
const guard=ctx=>path.join(ctx.home,'operation.guard');
const runFile=ctx=>path.join(ctx.home,'run.lock');
const reservationsFile=ctx=>path.join(ctx.home,'reservations.json');
const reservations=ctx=>readJSON(reservationsFile(ctx),[]);
const stop=ctx=>fs.existsSync(path.join(ctx.home,'STOP'));
const gateJobs=list=>list.map(j=>({id:j.id,verdict:j.verdict,due_at:j.due_at,completed:j.completed,authorized:j.authorized}));
function operator(ctx, records) {
  const value=readJSON(path.join(ctx.home,'operator.json'),{operator:ctx.site}).operator;
  const last=records.filter(r=>r.type==='HANDOFF').at(-1);
  // A manually edited or incompletely handed-off operator is never trusted.
  if(!SLOTS.includes(value)||value!==(last?.d.to??ctx.site)) throw new Error('operator_record_mismatch');
  return value;
}
function jobs(ctx) {
  const list=jobList(ctx,readJournal(ctx.journal));
  if(!Array.isArray(list)) throw new Error('bad_job_list');
  const ids=new Set();
  for(const j of list){
    if(!j||!ID.test(j.id??'')||ids.has(j.id)||!['read','write','local'].includes(j.class)||
      !Number.isFinite(Date.parse(j.due_at)) || (j.platforms!==undefined&&(!Array.isArray(j.platforms)||j.platforms.some(p=>!PLATFORMS.includes(p))))) throw new Error('bad_job_list');
    ids.add(j.id);
  }
  return list;
}
function stepCount(records,run) {return records.filter(r=>r.type==='STEP'&&r.run===run).length;}
function finish(ctx,state,records){
  const record=put(ctx,'RUN_END',{state,steps:stepCount(records,ctx.run)},ctx.run);
  if(fs.existsSync(runFile(ctx))) fs.unlinkSync(runFile(ctx));
  return record;
}
export function begin(ctx,flags) {
  return withLock(guard(ctx),()=>{
    let records=readJournal(ctx.journal), existing=readJSON(runFile(ctx),null);
    try{bindHome(ctx,records);}catch(e){
      if(!HOME_ERRORS.includes(e.message))throw e;
      put(ctx,'GATE_ERROR',{error:e.message},newRunId());return {exit:4,error:e.message};
    }
    if(existing){
      if(records.some(r=>r.type==='RUN_END'&&r.run===existing.run)){
        fs.unlinkSync(runFile(ctx));existing=null;
      } else if(!alive(existing.child_pid??existing.owner_pid) && !alive(existing.wrapper_pid)){
        // No live child is killed or resumed. Close the abandoned record only.
        finish({...ctx,run:existing.run},'interrupted',records);existing=null;
        records=readJournal(ctx.journal);
      }
    }
    // A crash after journaling admission but before writing run.lock must also
    // leave an explicit terminal record on the next begin.
    const unfinished=new Set(records.filter(r=>r.type==='GATE_DECISION').map(r=>r.run));
    for(const record of records) if(record.type==='RUN_END') unfinished.delete(record.run);
    for(const abandoned of unfinished) {
      if(abandoned===existing?.run) continue;
      put(ctx,'RUN_END',{state:'interrupted',steps:stepCount(records,abandoned)},abandoned);
    }
    if(unfinished.size) records=readJournal(ctx.journal);
    const run=newRunId(), now=flags.now??utc();
    let owner, input, result, prepared;
    try {
      owner=operator(ctx,records);
      if(!Number.isFinite(Date.parse(now))) throw new Error('invalid_time');
      const all=jobs(ctx), savedReservations=reservations(ctx);
      prepared=all.map(j=>{
        const done=records.some(r=>r.type==='JOB_END'&&r.d.job===j.id);
        const forbidden=j.run_class==='test_post' && (j.platforms??[]).some(p=>!['youtube','douyin'].includes(p));
        const authorized=!forbidden&&grant(records,j.id,j.class,savedReservations);
        const verdict=done?'completed':forbidden?'forbidden_by_policy':!authorized?'not_authorized':Date.parse(j.due_at)<=Date.parse(now)?'due':'not_due';
        return {...j,completed:done,authorized,verdict};
      });
      input={executor:ctx.slot,owner,stop:stop(ctx),running:!!existing,now:new Date(now).toISOString(),jobs:prepared};
      result=planRun(input);
    } catch(e) {
      const error=['invalid_time','bad_job_list'].includes(e.message)?e.message:'gate_threw';
      put(ctx,'GATE_ERROR',{error},run);return {exit:4,error};
    }
    put(ctx,'GATE_DECISION',{executor:ctx.slot,owner,stop:input.stop,running:input.running,now:input.now,
      launcher:flags.launcher??'manual',slot_id:flags['slot-id']??null,...result,
      jobs:gateJobs(prepared),gate_sha256:sha(fs.readFileSync(gateFile))},run);
    if(result.decision!=='plan_only'){
      put(ctx,'RUN_END',{state:'skipped',steps:0},run);return {exit:3,run,...result,home:ctx.home};
    }
    // Kept between CLI invocations. Parent shell owns the session; an active
    // step replaces the liveness witness with its own PID until it completes.
    atomic(runFile(ctx),{run,slot:ctx.slot,site:ctx.site,journal:ctx.journal,due:result.due,
      owner_pid:process.ppid,child_pid:null,at:utc(),jobs:prepared,failed:false});
    return {exit:0,run,...result,home:ctx.home};
  });
}
function admitted(ctx,records) {
  const run=readJSON(runFile(ctx),null);
  if(!run||run.run!==ctx.run||run.site!==ctx.site||run.journal!==ctx.journal||
    run.slot!==ctx.slot||operator(ctx,records)!==ctx.slot||records.some(r=>r.type==='RUN_END'&&r.run===ctx.run)||
    !records.some(r=>r.type==='GATE_DECISION'&&r.run===ctx.run&&r.d.decision==='plan_only')) throw new Error('run_not_admitted');
  return run;
}
function writeReservations(ctx,list){atomic(reservationsFile(ctx),list);}
function prepare(ctx,flags,command,launch){
  return withLock(guard(ctx),()=>{
    const records=readJournal(ctx.journal), run=admitted(ctx,records);
    const pinned=bindHome(ctx,records);
    if(run.child_pid) throw new Error('step_already_running');
    if(stop(ctx)) throw new Error('stop_flag');
    const job=run.jobs.find(j=>j.id===flags.job);
    if(!job||!run.due.includes(job.id)) throw new Error('job_not_due');
    if(records.some(r=>r.type==='JOB_END'&&r.d.job===job.id)) throw new Error('job_closed');
    const policy=classify(command,flags);
    guardRole(flags.role,policy,command);
    if(policy.tool==='bili' && command.slice(1).some(s=>['delete','remove','del'].includes(s))) throw new Error('bilibili_delete_forbidden');
    if(job.class!=='write'&&policy.class==='write') throw new Error('job_class_mismatch');
    const platforms=flags.platform??job.platforms??[];
    const keyed=['resume','verify','edit','delete','delete_dry_run','delete_verify'].includes(flags.role);
    if(keyed&&platforms.length!==1)throw new Error('one_platform_required');
    if((job.platforms??[]).length && (platforms.some(p=>!job.platforms.includes(p))||
       (!keyed&&platforms.length!==job.platforms.length)))throw new Error('platform_mismatch');
    const list=reservations(ctx), rule=limits(records,job,flags.role,platforms,utc(),list,run.jobs);
    if(['repeat_same','changed_plan','resume'].includes(flags.role)&&!records.some(r=>r.type==='STEP'&&
       r.d.job===job.id&&r.d.role==='publish'&&r.d.exit!==127&&platforms.every(p=>r.d.platforms.includes(p))&&
       list.some(x=>x.step_seq===r.seq&&x.run===r.run&&x.started===true)))throw new Error('repeat_without_publish');
    if(rule) throw new Error(rule);
    const scope=grantScope(records,job.id,policy.class,list,flags.role);
    if(scope===null) throw new Error('go_ahead_exhausted');
    // A verified journal and fsynced private reservation precede every spawn.
    // Failed/uncertain spawns spend approval conservatively; never retry them.
    if(!verifyJournal(ctx.journal).ok) throw new Error('broken_journal');
    const admission=records.find(r=>r.type==='GATE_DECISION'&&r.run===ctx.run&&r.d.decision==='plan_only');
    // Journal writability is established durably before the child, not inferred
    // from a successful read. This re-check uses the same admitted due set.
    const checkedInput={executor:ctx.slot,owner:ctx.slot,stop:false,running:false,now:utc(),jobs:run.jobs};
    const checked=planRun(checkedInput);
    put(ctx,'GATE_DECISION',{...admission.d,...checked,now:checkedInput.now,jobs:gateJobs(run.jobs),stop:false,running:false,
      gate_sha256:sha(fs.readFileSync(gateFile))},ctx.run);
    const reservation={id:newRunId(),run:ctx.run,job:job.id,class:policy.class,role:flags.role,
      platforms,scope,run_class:job.run_class??null,at:utc(),step_seq:null,started:null};
    list.push(reservation);writeReservations(ctx,list);
    atomic(runFile(ctx),{...run,child_pid:process.pid});
    if(stop(ctx)) throw new Error('stop_flag');
    return {policy,reservation,run,capture:launch(reservation.id,pinned)};
  });
}
function captureChild(ctx,command,reservationId,pinned){
  return new Promise(resolve=>{
    const file=path.join(ctx.home,'captures',newRunId()+'.jsonl');
    fs.mkdirSync(path.dirname(file),{recursive:true});
    const fd=fs.openSync(file,'wx',0o600), buffers={stdout:Buffer.alloc(0),stderr:Buffer.alloc(0)};
    const decoder=new StringDecoder('utf8');
    let stdout='', child, spawnError=false, captureError=false;
    const started=performance.now();
    const row=(stream,bytes)=>{
      try{fs.writeSync(fd,JSON.stringify({at:utc(),stream,bytes:bytes.toString('base64')})+'\n');}
      catch{captureError=true;}
    };
    const receive=(stream,bytes)=>{
      // Relay the original bytes, not decoded/re-encoded text.
      process[stream].write(bytes);
      if(stream==='stdout') stdout+=decoder.write(bytes);
      let pending=Buffer.concat([buffers[stream],bytes]), n;
      while((n=pending.indexOf(10))!==-1){row(stream,pending.subarray(0,n+1));pending=pending.subarray(n+1);}
      buffers[stream]=pending;
    };
    const env={...process.env,AWL_RUN_ID:reservationId,VPUB_BROWSER_IDLE_SECONDS:'0',VPUB_HOME:pinned.home};
    // Windows environment names are case-insensitive, including in mixed-case shells.
    for(const key of Object.keys(env))if(['BILI_CLI_HOME','VPUB_FAULT'].includes(key.toUpperCase()))delete env[key];
    if(pinned.biliHome)env.BILI_CLI_HOME=pinned.biliHome;
    try{child=spawn(command[0],command.slice(1),{stdio:['ignore','pipe','pipe'],shell:false,windowsHide:true,env});}
    catch{spawnError=true;}
    if(child){
      child.on('spawn',()=>{
        try{withLock(guard(ctx),()=>{
          const run=readJSON(runFile(ctx));
          atomic(runFile(ctx),{...run,child_pid:child.pid,wrapper_pid:process.pid});
        });}catch{captureError=true;}
      });
      child.stdout.on('data',b=>receive('stdout',b));child.stderr.on('data',b=>receive('stderr',b));
      child.on('error',()=>{spawnError=true;});
      child.on('close',(code)=>complete(spawnError?127:Number.isInteger(code)?code:3));
    }else complete(127);
    function complete(exit){
      stdout+=decoder.end();
      for(const stream of ['stdout','stderr']) if(buffers[stream].length) row(stream,buffers[stream]);
      try{fs.fsyncSync(fd);}catch{captureError=true;}finally{fs.closeSync(fd);}
      resolve({exit,stdout,ms:Math.round(performance.now()-started),capture_sha256:sha(fs.readFileSync(file)),spawnError,captureError});
    }
  });
}
export async function step(ctx,flags,command){
  let prepared;
  try{prepared=prepare(ctx,flags,command,(id,pinned)=>captureChild(ctx,command,id,pinned));}
  catch(e){
    if(['broken_journal','lock_unavailable'].includes(e.message)) throw e;
    // Fixed words only. No command arguments, path or child error is echoed.
    if(!REFUSALS.includes(e.message)) throw e;
    // Never serialize rejected arguments. An invalid descriptor is null; the
    // closed reason records the failure without leaking paths or free text.
    let policy={tool:null,action:null,class:null};
    try{policy=classify(command,{tool:flags.tool==='other'?'other':undefined,class:['read','local'].includes(flags.class)?flags.class:undefined});}catch{}
    const run=readJSON(runFile(ctx),null), job=run?.jobs?.find(j=>j.id===flags.job);
    const platforms=flags.platform??job?.platforms??[];
    put(ctx,'STEP_REFUSED',{job:ID.test(flags.job??'')?flags.job:null,role:roles.includes(flags.role)?flags.role:null,...policy,
      platforms:Array.isArray(platforms)?[...new Set(platforms.filter(p=>PLATFORMS.includes(p)))]:[],reason:e.message},
      /^[a-f0-9]{32}$/.test(ctx.run??'')?ctx.run:null);
    console.error(e.message);return 5;
  }
  try {
  const child=await prepared.capture;
  if(child.captureError) throw new Error('capture_unwritable');
  const result=outcome(child.exit,child.stdout), {policy,reservation}=prepared;
  withLock(guard(ctx),()=>{
    const record=put(ctx,'STEP',{job:flags.job,...policy,awl_run_id:reservation.id,role:flags.role,platforms:reservation.platforms,exit:child.exit,
      ms:child.ms,capture_sha256:child.capture_sha256,result:result.result,code:result.code,
      refs:result.rawRefs.map(r=>pseudonym(r,ctx.home)),fault_fired:null},ctx.run);
    const list=reservations(ctx), item=list.find(r=>r.id===reservation.id);
    item.step_seq=record.seq;item.started=!child.spawnError;writeReservations(ctx,list);
    const run=readJSON(runFile(ctx));
    atomic(runFile(ctx),{...run,child_pid:null,wrapper_pid:null});
  });
  return child.exit;
  } catch(e) {
    // Only a wrapper recording failure marks the session failed. Preserve its
    // reservation and capture; never start a second child to repair evidence.
    try { withLock(guard(ctx),()=>{
      const run=readJSON(runFile(ctx));
      atomic(runFile(ctx),{...run,failed:true,child_pid:null,wrapper_pid:null});
    }); } catch { /* A stale guard remains fail-closed for explicit review. */ }
    throw e;
  }
}
export function end(ctx){
  return withLock(guard(ctx),()=>{
    const records=readJournal(ctx.journal),run=admitted(ctx,records);
    if(run.child_pid) throw new Error('step_already_running');
    const record=finish(ctx,run.failed?'failed':'completed',records);
    return {exit:0,record};
  });
}
export function closeJob(ctx,flags){
  return withLock(guard(ctx),()=>{
    const records=readJournal(ctx.journal), active=readJSON(runFile(ctx),null);
    if(operator(ctx,records)!==ctx.slot) throw new Error('not_owner');
    if(active?.child_pid) throw new Error('step_already_running');
    const job=jobs(ctx).find(j=>j.id===flags.job);
    if(!job) throw new Error('unknown_job');
    if(records.some(r=>r.type==='JOB_END'&&r.d.job===job.id)) throw new Error('job_closed');
    const reason=flags.reason, by=flags.by??'operator';
    if(!['done','nothing_posted','removed_by_hand','abandoned'].includes(reason)||!['operator','owner','tester'].includes(by)) throw new Error('invalid_close');
    if(job.run_class!=='test_post'&&!['done','abandoned'].includes(reason)) throw new Error('invalid_close');
    const own=records.filter(r=>r.d.job===job.id);
    const pending=reservations(ctx).filter(r=>r.job===job.id&&r.class==='write'&&r.step_seq===null);
    if(job.run_class==='test_post'&&reason!=='abandoned'){
      const platformOf=r=>r.d.platform??(job.platforms?.length===1?job.platforms[0]:null);
      const afterWrite=(r,p)=>r.seq>(own.filter(x=>x.type==='STEP'&&x.d.class==='write'&&x.d.platforms.includes(p)).at(-1)?.seq??0);
      const absent=(job.platforms??[]).length>0&&!own.some(r=>r.type==='STEP'&&r.d.role==='delete')&&
        pending.length===0&&job.platforms.every(p=>own.some(r=>r.type==='OBSERVATION'&&r.d.about==='post'&&
          r.d.seen==='absent'&&platformOf(r)===p&&afterWrite(r,p)));
      const verified=(job.platforms??[]).length>0&&pending.length===0&&job.platforms.every(p=>own.some(r=>
        r.type==='STEP'&&r.d.role==='delete_verify'&&r.d.result==='ok'&&r.d.platforms.length===1&&r.d.platforms[0]===p&&afterWrite(r,p)));
      const manual=(job.platforms??[]).length>0&&job.platforms.every(p=>own.some(r=>
        r.type==='NOTE'&&r.d.code==='manual_step'&&platformOf(r)===p));
      if(!({done:verified,nothing_posted:absent,removed_by_hand:manual}[reason])) throw new Error('job_close_evidence_missing');
    }
    const digest=reason==='abandoned'?words(ctx.home):null;
    const record=put(ctx,'JOB_END',{job:job.id,reason,by,words_sha256:digest});
    return {exit:0,record};
  });
}
export async function main(argv=process.argv.slice(2)){
  const sub=argv.shift(),{flags,command}=parseFlags(argv);
  const keys={begin:['launcher','slot-id'],step:['run','job','role','platform','tool','class'],end:['run'],'close-job':['job','reason','by']};
  if(!keys[sub]) throw new Error('unknown_command');
  allowed(flags,['site','slot','journal',...keys[sub]]);
  if(sub!=='step'&&command.length) throw new Error('unexpected_command');
  const ctx={...context(flags),run:flags.run};
  if(sub==='step') return step(ctx,flags,command);
  const result=sub==='begin'?begin(ctx,flags):sub==='close-job'?closeJob(ctx,flags):end(ctx);
  console.log(JSON.stringify(result));return result.exit;
}
if(process.argv[1]&&import.meta.url===pathToFileURL(path.resolve(process.argv[1])).href){
  try{process.exitCode=await main();}catch{console.error('gate_recording_failed');process.exitCode=70;}
}

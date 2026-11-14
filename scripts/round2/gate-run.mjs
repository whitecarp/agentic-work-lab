import fs from 'node:fs';
import path from 'node:path';
import {pathToFileURL} from 'node:url';
import {spawn} from 'node:child_process';
import {StringDecoder} from 'node:string_decoder';
import {planRun} from '../operations-gate.mjs';
import {context, operator, openSessions, activeStops, put, readJournal, verifyJournal, newRunId, pseudonym, words,commitWords} from './journal.mjs';
import {ID, SLOTS, PLATFORMS, REFUSALS} from './schema.mjs';
import {refusalWord,sha, utc, atomic, withLock, readJSON, parseFlags, allowed, alive} from './io.mjs';
import {classify, guardRole, grant, grantScope, limits, outcome, roles} from './policy.mjs';
import {bindHome,HOME_ERRORS} from './site.mjs';
import {jobList,sealedPlan} from './jobs.mjs';
import {selectFault,armFault,faultVariable,faultController} from './planned-faults.mjs';
import {countsLook,lookStep} from './looks.mjs';
import {publishStartedOn,confirmedAnswer} from './answers.mjs';

const gateFile=new URL('../operations-gate.mjs',import.meta.url);
const guard=ctx=>path.join(ctx.home,'operation.guard');
const runFile=ctx=>path.join(ctx.home,'run.lock');
const reservationsFile=ctx=>path.join(ctx.home,'reservations.json');
const reservations=ctx=>{
 const file=reservationsFile(ctx),records=readJournal(ctx.journal);
 if(!fs.existsSync(file)&&records.some(r=>['GO_AHEAD','STEP'].includes(r.type)))throw new Error('reservations_missing');
 const rows=readJSON(file,[]);
 if(!Array.isArray(rows))throw new Error('reservations_invalid');
 for(const step of records.filter(r=>r.type==='STEP'&&r.d.class==='write')){
  if(!rows.some(r=>r.run===step.run&&r.step_seq===step.seq||r.id&&r.id===step.d.awl_run_id))rows.push({run:step.run,step_seq:step.seq,job:step.d.job,class:'write',role:step.d.role,platforms:step.d.platforms,scope:null,started:step.d.started!==false,at:step.at});
 }
 return rows;
};
const stop=ctx=>fs.existsSync(path.join(ctx.home,'STOP'));
const gateJobs=list=>list.map(j=>({id:j.id,verdict:j.verdict,due_at:j.due_at,completed:j.completed,authorized:j.authorized}));
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
      }
    }
    // A crash after journaling admission but before writing run.lock must also
    // continue refusing a second session until explicitly ended.
    const open=openSessions(records,ctx.site)[0];
    if(!existing && open)existing={run:open.run};
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
      input={executor:ctx.slot,owner,stop:stop(ctx)||activeStops(records).length>0,running:!!existing,now:new Date(now).toISOString(),jobs:prepared};
      result=planRun(input);
    } catch(e) {
      const error=['invalid_time','bad_job_list'].includes(e.message)?e.message:'gate_threw';
      put(ctx,'GATE_ERROR',{error},run);return {exit:4,error};
    }
    put(ctx,'GATE_DECISION',{executor:ctx.slot,owner,stop:input.stop,running:input.running,now:input.now,
      launcher:flags.launcher??'manual',slot_id:flags['slot-id']??null,...result,
      jobs:gateJobs(prepared),gate_sha256:sha(fs.readFileSync(gateFile))},run);
    if(result.decision!=='plan_only'){
      put(ctx,'RUN_END',{state:'skipped',steps:0},run);return {exit:3,run,...result,...(result.reason==='already_running'?{open_run:existing.run}:{}),home:ctx.home};
    }
    // Kept between CLI invocations until explicit end. Child/wrapper identities
    // protect a step; shell liveness does not decide session lifetime.
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
function prepare(ctx,flags,command,launch,supportCheck){
  return withLock(guard(ctx),()=>{
    const records=readJournal(ctx.journal), run=admitted(ctx,records);
    const pinned=bindHome(ctx,records);
    sealedPlan(ctx,records);
    if(run.child_pid) throw new Error('step_already_running');
    if(stop(ctx)) throw new Error('stop_flag');
    if(activeStops(records).length)throw new Error('stop_flag');
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
       r.d.job===job.id&&r.d.role==='publish'&&platforms.every(p=>publishStartedOn(r,p))&&
       list.some(x=>x.step_seq===r.seq&&x.run===r.run&&x.started===true)))throw new Error('repeat_without_publish');
    if(rule) throw new Error(rule);
    const scope=grantScope(records,job.id,policy.class,list,flags.role,platforms,job.run_class);
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
    const reservation={id:newRunId(),wrapper_pid:process.pid,run:ctx.run,job:job.id,class:policy.class,role:flags.role,
      platforms,scope,purpose:flags.role==='edit'?'edit':flags.role==='resume'&&job.run_class==='real_release'?'resume':'publish',run_class:job.run_class??null,at:utc(),step_seq:null,started:null};
    const fault=selectFault(ctx,job,flags.role,policy);
    armFault(ctx,fault,command[0],supportCheck);
    list.push(reservation);writeReservations(ctx,list);
    atomic(runFile(ctx),{...run,child_pid:process.pid});
    if(stop(ctx)) throw new Error('stop_flag');
    return {policy,reservation,run,capture:launch(reservation.id,pinned,fault)};
  });
}
function captureChild(ctx,command,reservationId,pinned,fault,controllerFactory=faultController){
  return new Promise(resolve=>{
    const file=path.join(ctx.home,'captures',newRunId()+'.jsonl');
    fs.mkdirSync(path.dirname(file),{recursive:true});
    const fd=fs.openSync(file,'wx',0o600), buffers={stdout:Buffer.alloc(0),stderr:Buffer.alloc(0)};
    const decoder=new StringDecoder('utf8');
    let stdout='', child, spawned=false, spawnError=false, captureError=false;
    const started=performance.now(),controller=controllerFactory(ctx,fault);
    const row=(stream,bytes)=>{
      try{fs.writeSync(fd,JSON.stringify({at:utc(),stream,bytes:bytes.toString('base64')})+'\n');}
      catch{captureError=true;}
    };
    const receive=(stream,bytes)=>{
      // Relay the original bytes, not decoded/re-encoded text.
      process[stream].write(bytes);
      if(stream==='stdout'){stdout+=decoder.write(bytes);controller?.answer?.(stdout);}
      if(stream==='stderr')controller?.receive(bytes);
      let pending=Buffer.concat([buffers[stream],bytes]), n;
      while((n=pending.indexOf(10))!==-1){row(stream,pending.subarray(0,n+1));pending=pending.subarray(n+1);}
      buffers[stream]=pending;
    };
    const env={...process.env,AWL_RUN_ID:reservationId,VPUB_BROWSER_IDLE_SECONDS:'0',VPUB_HOME:pinned.home};
    // Windows environment names are case-insensitive, including in mixed-case shells.
    for(const key of Object.keys(env))if(['BILI_CLI_HOME','VPUB_FAULT','VPUB_AUDIT_SESSION','VPUB_AUDIT_PURPOSE'].includes(key.toUpperCase()))delete env[key];
    if(pinned.biliHome)env.BILI_CLI_HOME=pinned.biliHome;
    if(fault)env.VPUB_FAULT=faultVariable(fault);
    try{child=spawn(command[0],command.slice(1),{stdio:['ignore','pipe','pipe'],shell:false,windowsHide:true,env,detached:!!fault&&process.platform!=='win32'});}
    catch{spawnError=true;}
    if(child){
      child.on('spawn',()=>{
        spawned=true;
        try{withLock(guard(ctx),()=>{
          const run=readJSON(runFile(ctx));
          atomic(runFile(ctx),{...run,child_pid:child.pid,wrapper_pid:process.pid});
        });}catch{captureError=true;}
      });
      child.stdout.on('data',b=>receive('stdout',b));child.stderr.on('data',b=>receive('stderr',b));
      child.on('error',()=>{if(!spawned)spawnError=true;});
      try{controller?.attach(child);}catch{captureError=true;}
      child.on('close',(code)=>complete(spawnError?127:Number.isInteger(code)?code:3));
    }else complete(127);
    async function complete(exit){
      stdout+=decoder.end();
      for(const stream of ['stdout','stderr']) if(buffers[stream].length) row(stream,buffers[stream]);
        try{fs.fsyncSync(fd);}catch{captureError=true;}
        try{fs.closeSync(fd);}catch{captureError=true;}
      let faultFired=null;
      try{if(controller){const result=await controller.finish(stdout,exit);faultFired=result.fired;}}catch{faultFired=null;}
      let digest=null;try{digest=sha(fs.readFileSync(file));}catch{captureError=true;}
      resolve({exit,stdout,ms:Math.round(performance.now()-started),capture_sha256:digest,spawnError,captureError,faultFired});
    }
  });
}
export async function step(ctx,flags,command,{supportCheck,controllerFactory}={}){
  let prepared;
  try{prepared=prepare(ctx,flags,command,(id,pinned,fault)=>captureChild(ctx,command,id,pinned,fault,controllerFactory),supportCheck);}
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
  const {policy,reservation}=prepared;
  const result=outcome(child.exit,child.stdout,{started:!child.spawnError,role:flags.role,stepClass:policy.class,platforms:reservation.platforms});
  withLock(guard(ctx),()=>{
    const record=put(ctx,'STEP',{job:flags.job,...policy,awl_run_id:reservation.id,role:flags.role,platforms:reservation.platforms,exit:child.exit,
      ms:child.ms,capture_sha256:child.capture_sha256,result:child.captureError?'unknown':result.result,code:result.code,capture_failed:child.captureError,
      answered:result.answered,started:!child.spawnError,confirmed_state:result.confirmed_state,
      answers:result.answers.map(a=>({...a,refs:a.refs.map(r=>pseudonym(r,ctx.home))})),
      refs:result.rawRefs.map(r=>pseudonym(r,ctx.home)),fault_fired:child.faultFired},ctx.run,30000);
    const list=reservations(ctx), item=list.find(r=>r.id===reservation.id);
    item.step_seq=record.seq;item.started=!child.spawnError;writeReservations(ctx,list);
    const run=readJSON(runFile(ctx));
    atomic(runFile(ctx),{...run,child_pid:null,wrapper_pid:null});
  },30000);
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
    const records=readJournal(ctx.journal);
    if(!fs.existsSync(runFile(ctx))){
      const entry=openSessions(records,ctx.site).find(r=>r.run===ctx.run && r.slot===ctx.slot);
      if(!entry || operator(ctx,records)!==ctx.slot)throw new Error('run_not_admitted');
      if(reservations(ctx).some(r=>r.run===ctx.run && r.step_seq===null && alive(r.wrapper_pid)))throw new Error('step_already_running');
      return {exit:0,record:finish(ctx,'interrupted',records)};
    }
    const run=admitted(ctx,records);
    if(run.child_pid&&(alive(run.child_pid)||alive(run.wrapper_pid))) throw new Error('step_already_running');
    const record=finish(ctx,run.failed?'failed':'completed',records);
    return {exit:0,record};
  });
}
// Administrative journal recovery only: never signal a process, clear a stop,
// release spent approvals, close a job, or change the appointed operator.
export function endFailedSession(ctx,by,statement){
  return withLock(guard(ctx),()=>{
    const records=readJournal(ctx.journal),entry=openSessions(records,ctx.site).find(r=>r.run===ctx.run);
    if(by!==ctx.site||!['codex','claude','gemini','tester-assistant'].includes(ctx.slot)||
       ctx.slot===operator(ctx,records))throw new Error('checking_agent_required');
    if(!entry)throw new Error('session_not_open');
    const active=readJSON(runFile(ctx),null);
    if(active&&active.run!==ctx.run)throw new Error('session_mismatch');
    if(active?.child_pid&&(alive(active.child_pid)||alive(active.wrapper_pid))||
       reservations(ctx).some(r=>r.run===ctx.run&&r.step_seq===null&&alive(r.wrapper_pid)))throw new Error('step_already_running');
    if(!Buffer.from(statement).toString('utf8').trim())throw new Error('empty_words');
    const record=put(ctx,'RUN_END',{state:'interrupted',steps:stepCount(records,ctx.run),
      by,operator:entry.slot,words_sha256:commitWords(ctx.home,statement)},ctx.run);
    if(active)fs.unlinkSync(runFile(ctx));
    return {exit:0,record};
  });
}
function confirmsDelete(step, platform) {
  const d = step.d;
  if (d.answers) return confirmedAnswer(d.answers.find(a => a.platform === platform) ?? {}, 'delete');
  // Early journals recorded successful delete read-backs without a state field.
  return d.result === 'ok' && (d.confirmed_state === 'DELETED_VERIFIED' ||
    (d.role === 'delete_verify' && !Object.hasOwn(d, 'confirmed_state')));
}

function unresolvedDelete(steps, platform) {
  // A refusal cannot settle an earlier press. Only a saved confirmation clears it.
  let pending = false;
  for (const step of steps) {
    const d = step.d;
    if (d.started === false || d.exit === 127 || !['delete', 'delete_verify'].includes(d.role)) continue;
    if (confirmsDelete(step, platform)) pending = false;
    else if (d.role === 'delete' && d.class === 'write' && ['in_doubt', 'unknown'].includes(d.result)) pending = true;
  }
  return pending;
}

export function closeJob(ctx,flags){
  return withLock(guard(ctx),()=>{
    const records=readJournal(ctx.journal), active=readJSON(runFile(ctx),null);
    if(operator(ctx,records)!==ctx.slot) throw new Error('not_owner');
    if(active?.child_pid) throw new Error('step_already_running');
    const job=jobs(ctx).find(j=>j.id===flags.job);
    if(!job) throw new Error('unknown_job');
    if(job.run_class==='standing')throw new Error('standing_job_never_closed');
    if(records.some(r=>r.type==='JOB_END'&&r.d.job===job.id)) throw new Error('job_closed');
    const reason=flags.reason, by=flags.by??'operator';
    if(!['done','nothing_posted','removed_by_hand','abandoned'].includes(reason)||!['operator','owner','tester'].includes(by)) throw new Error('invalid_close');
    if(job.run_class!=='test_post'&&!['done','abandoned'].includes(reason)) throw new Error('invalid_close');
    const own=records.filter(r=>r.d.job===job.id);
    const pending=reservations(ctx).filter(r=>r.job===job.id&&r.class==='write'&&r.step_seq===null);
    let settlements=[];
    if(job.run_class==='test_post'&&reason!=='abandoned'){
      const platformOf=r=>r.d.platform??(job.platforms?.length===1?job.platforms[0]:null);
      settlements=(job.platforms??[]).map(platform=>{
        const steps=own.filter(r=>r.type==='STEP'&&r.d.platforms.includes(platform)),writes=steps.filter(r=>r.d.class==='write');
        const last=writes.at(-1),hasDelete=steps.some(r=>r.d.role==='delete'),post=steps.filter(r=>['publish','repeat_same','changed_plan','resume'].includes(r.d.role)).at(-1);
        const look=(about,step)=>own.find(r=>r.type==='OBSERVATION'&&r.seq>(last?.seq??0)&&r.d.about===about&&r.d.seen==='absent'&&platformOf(r)===platform&&countsLook(r,step));
        const verified=steps.find(r=>r.seq>(last?.seq??0)&&r.d.role==='delete_verify'&&confirmsDelete(r,platform));
        const manual=own.find(r=>r.type==='NOTE'&&r.d.code==='manual_step'&&platformOf(r)===platform&&r.seq>(last?.seq??0));
        if (verified) return {platform, how: 'delete_confirmed'};
        if (manual) return {platform, how: 'removed_by_hand'};
        if (!hasDelete && look('post', post)) return {platform, how: 'nothing_posted'};
        if (unresolvedDelete(steps, platform) && look('delete', lookStep(own, job.id, platform, 'delete'))) {
          return {platform, how: 'gone_by_person'};
        }
        return {platform, how: null};
      });
      if(!settlements.length||pending.length||settlements.some(s=>!s.how)||reason==='nothing_posted'&&settlements.some(s=>s.how!=='nothing_posted')||reason==='removed_by_hand'&&settlements.some(s=>s.how!=='removed_by_hand'))throw new Error('job_close_evidence_missing');
    }
    const digest=reason==='abandoned'?words(ctx.home):null;
    const record=put(ctx,'JOB_END',{job:job.id,reason:job.run_class==='test_post'&&reason!=='abandoned'?'done':reason,by,words_sha256:digest,platform_settlements:settlements});
    return {exit:0,record};
  });
}
export async function main(argv=process.argv.slice(2)){
  const sub=argv.shift(),{flags,command}=parseFlags(argv);
  const keys={begin:['launcher','slot-id'],step:['run','job','role','platform','tool','class'],end:['run'],'end-failed-session':['run','by'],'close-job':['job','reason','by']};
  if(!keys[sub]) throw new Error('unknown_command');
  allowed(flags,['site','slot','journal',...keys[sub]]);
  if(sub!=='step'&&command.length) throw new Error('unexpected_command');
  const ctx={...context(flags),run:flags.run};
  if(sub==='step') return step(ctx,flags,command);
  const result=sub==='begin'?begin(ctx,flags):sub==='close-job'?closeJob(ctx,flags):sub==='end-failed-session'?endFailedSession(ctx,flags.by,fs.readFileSync(0)):end(ctx);
  console.log(JSON.stringify(result));return result.exit;
}
if(process.argv[1]&&import.meta.url===pathToFileURL(path.resolve(process.argv[1])).href){
  try{process.exitCode=await main();}catch(e){console.error(refusalWord(e,'gate_recording_failed'));process.exitCode=70;}
}

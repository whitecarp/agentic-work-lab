import fs from 'node:fs';
import path from 'node:path';
import {pathToFileURL} from 'node:url';
import {readJournal,context,keyedDigest} from './journal.mjs';
import {protectDigests} from './export.mjs';
import {refusalWord,sha,parseFlags,allowed,readJSON} from './io.mjs';
import {crosscast,commandClass} from './crosscast.mjs';
import {jobList} from './jobs.mjs';
import {operation,pressRoles,uncertainState,nothingSent,settled,confirmedAnswer,confirmed} from './answers.mjs';
import {countsLook} from './looks.mjs';
import {gateChecks,publishChecks,observationChecks} from './analysis-checks.mjs';

export function reconcile(ctx,crosscastFile,{now=Date.now(),privacy=false}={}){
 if(typeof now==='string'&&!/^\d{4}-\d\d-\d\dT\d\d:\d\d(?::\d\d(?:\.\d+)?)?(?:Z|[+-]\d\d:\d\d)$/.test(now))throw new Error('invalid_as_of');
 now=typeof now==='string'?Date.parse(now):now;
 if(!Number.isFinite(now))throw new Error('invalid_as_of');
 if(privacy)now=Date.parse(new Date(now).toISOString().slice(0,10)+'T23:59:59.999Z');
 const records=readJournal(ctx.journal),cc=crosscast(crosscastFile),steps=records.filter(r=>r.type==='STEP');
 const admissions=records.filter(r=>r.type==='GATE_DECISION'&&r.d.decision==='plan_only');
 const runs=[...new Set(admissions.map(r=>r.run))].map(id=>{
  const first=admissions.find(r=>r.run===id),end=records.find(r=>r.run===id&&r.type==='RUN_END');
  return {id,start:Date.parse(first.at),end:end?Date.parse(end.at):null};
 });
 const refusals=Object.create(null);for(const s of steps.filter(r=>r.d.result==='refused'))refusals[s.d.code??'other']=(refusals[s.d.code??'other']??0)+1;
 for(const r of records.filter(r=>r.type==='STEP_REFUSED'))refusals[r.d.reason]=(refusals[r.d.reason]??0)+1;
 let missing=0;
 const captureHashes=new Set();
 if(ctx.home){
  const folder=path.join(ctx.home,'captures');
  for(const name of fs.existsSync(folder)?fs.readdirSync(folder).filter(n=>n.endsWith('.jsonl')):[]){
   const file=path.join(folder,name);
   if(!fs.lstatSync(file).isSymbolicLink())captureHashes.add(sha(fs.readFileSync(file)));
  }
 }
 if(ctx.home)for(const s of steps){
  // Captures are named by run/time, so match bytes to the recorded hash.
  if(!captureHashes.has(s.d.capture_sha256))missing++;
 }
  const jobs=ctx.home?jobList(ctx,records):[];
 const reservations=ctx.home?readJSON(path.join(ctx.home,'reservations.json'),[]):[];
  const eligible=steps.filter(r=>r.d.class==='write'&&['publish','repeat_same','changed_plan','resume','edit','delete'].includes(r.d.role));
 const scope=job=>jobs.find(j=>j.id===job)?.run_class;
 const started=s=>s.d.started??!(reservations.find(x=>x.run===s.run&&x.step_seq===s.seq)?.started===false||s.d.exit===127);
 const observationPlatform=r=>{
  if(r.d.platform)return r.d.platform;
  const platforms=jobs.find(j=>j.id===r.d.job)?.platforms??[...new Set(steps.filter(s=>s.d.job===r.d.job).flatMap(s=>s.d.platforms))];
  return platforms.length===1?platforms[0]:null;
 };
 const obligations=new Map();
  for(const s of eligible.filter(r=>['test_post','real_slot','real_release'].includes(scope(r.d.job))&&started(r)))
  for(const platform of s.d.platforms)obligations.set(JSON.stringify([s.d.job,platform,operation(s)]),{job:s.d.job,platform,about:operation(s)});
 const lookChecks=[...obligations.values()].map(({job,platform,about})=>{
    const s=eligible.filter(r=>r.d.job===job&&r.d.platforms.includes(platform)&&operation(r)===about&&started(r)).at(-1);
  const looks=records.filter(r=>r.seq>s.seq&&r.type==='OBSERVATION'&&r.d.job===job&&r.d.about===about&&observationPlatform(r)===platform&&r.d.seen!=='unclear');
  const qualifying=looks.find(r=>countsLook(r,s,now));
  const early=looks.filter(r=>r.d.minutes_after<10).map(r=>r.seq);
  // A late look is usable ground truth through 24 hours, but must remain
  // distinguishable from an on-time one. Never turn an early/unclear look
  // into ground truth merely because the review is run after the deadline.
  const status=qualifying?(qualifying.d.minutes_after<=60?'on_time':'late'):
    now-Date.parse(s.at)>=86400000?'no_ground_truth':early.length?'early':'pending';
    const premature=looks.filter(r=>Date.parse(r.at)-Date.parse(s.at)<r.d.minutes_after*60000).map(r=>r.seq);
    return {job,platform,step:s.seq,about,status,observation:qualifying?.seq??null,early_observations:early,
      recorded_before_claimed_minute:premature};
 });
 const owesLook=lookChecks.filter(r=>!['on_time','late'].includes(r.status));
 // Time overlap is never a substitute for the id passed to a specific child.
 // Missing STEP (including a crash before its append) is an unproven binding.
 const trusted=cc.present&&cc.chain_ok&&cc.runs.every(r=>r.chain_ok!==false);
 const bindings=cc.runs.filter(r=>commandClass(r)==='write').map(command=>{
  const id=command.awl_run_id??null,candidates=id?steps.filter(s=>s.d.awl_run_id===id):[];
  let status='matched',s=candidates[0];
  if(!trusted)status='unverifiable';
  else if(!id)status='missing_id';
  else if(!candidates.length)status='unknown_id';
  else if(candidates.length!==1||cc.runs.filter(r=>r.awl_run_id===id).length!==1)status='ambiguous_id';
  else if(s.d.class!=='write'||s.d.exit===127||
    !admissions.some(a=>a.run===s.run&&a.seq<s.seq&&a.d.due.includes(s.d.job))||
    records.some(r=>r.type==='RUN_END'&&r.run===s.run&&r.seq<s.seq)||
    reservations.some(r=>r.run===s.run&&r.step_seq===s.seq&&r.started===false))status='ineligible_step';
  return {run:command.run,awl_run_id:id,step_seq:status==='matched'?s.seq:null,status};
 });
 const ungated=trusted?bindings.filter(b=>b.status!=='matched').length:null;
 const intact=cc.integrity?.unreadable_loss_records===0 && cc.integrity?.loss_records_complete===true &&
   cc.integrity?.loss_folder_unreadable===false && (cc.integrity?.unplaced_dropped_events??0)===0;
 function matched(s){
   const b=bindings.find(b=>b.step_seq===s.seq&&b.status==='matched');
   return b&&cc.runs.find(r=>r.run===b.run);
 }
 function eventsFor(s){
   const cmd=matched(s);
   // The exporter sets loss_complete=false on every run. Known gaps and loss
   // records determine whether its observed events can support this comparison.
   if(!intact||!cmd||cmd.dropped_events!==0||cmd.sequence_gap!==false)return null;
   return cc.events.filter(e=>e.run===cmd.run);
 }
 function provenNoPress(s,platform){
   const cmd=matched(s), events=eventsFor(s);
   if(!events||cmd.exit_code!==s.d.exit||cmd.ended!==true)return false;
   const first=events[0],last=events.at(-1);
   if(first?.type!=='command_start'||last?.type!=='command_end')return false;
   const middle=events.slice(1,-1).filter(e=>!platform||!e.platform||e.platform===platform);
   if(middle.some(e=>!['call','span','stage','transition'].includes(e.type)))return false;
   if(events.some(e=>e.operation==='press'||e.platform_writes>0||e.stage==='pressing'))return false;
   const before=first.before,after=last.after;
   const sameCounts=before&&after&&before.final_presses!=null&&before.final_presses===after.final_presses&&
     before.delete_attempted===after.delete_attempted;
   // A timing span accompanies even a skipped publish. A transition, stage
   // or platform call prevents the no-contact route to proof.
   if(middle.every(e=>e.type==='span') &&
      ((before===null&&after===null)||(sameCounts&&before.state===after.state)))return true;
   if(!sameCounts||after.final_submit_attempted===true&&before.final_submit_attempted!==true)return false;
   return !middle.some(e=>e.type==='transition'&&
     ((e.final_presses!=null&&e.final_presses!==before.final_presses)||
      (e.delete_attempted!=null&&e.delete_attempted!==before.delete_attempted)||
      (e.final_submit_attempted===true&&before.final_submit_attempted!==true)));
 }
 const cutoff=s=>s.d.result==='unknown'&&s.d.answered===false&&started(s)&&pressRoles.includes(s.d.role);
 function pressEvidence(s,platform){
   const events=eventsFor(s);if(!events)return null;
   const about=operation(s);
   const snapshots=events.filter(e=>!e.platform||e.platform===platform)
     .flatMap(e=>e.type==='transition'?[e]:[e.after].filter(Boolean));
   if(about==='edit'){
     const own=snapshots.filter(e=>e.kind==='edit');
     if(own.some(e=>e.final_submit_attempted===true||e.platform_writes>0||e.state==='EDIT_SUBMITTING'||e.state==='EDIT_SUBMITTED_UNVERIFIED'))return true;
     return own.some(e=>e.final_submit_attempted===false)?false:null;
   }
   if(about==='delete'){
     if(snapshots.some(e=>e.delete_attempted===true))return true;
     return snapshots.some(e=>e.delete_attempted===false)?false:null;
   }
   if(snapshots.some(e=>e.final_submit_attempted===true||e.final_presses>0||e.delete_attempted===true))return true;
   return snapshots.some(e=>e.final_submit_attempted===false||e.final_presses===0||e.delete_attempted===false)?false:null;
 }
 function usableEvents(s){
   const cmd=matched(s),events=eventsFor(s);
   if(!cmd || !events || cmd.ended!==true || cmd.exit_code!==s.d.exit ||
      events[0]?.type!=='command_start' || events.at(-1)?.type!=='command_end')return null;
   return events;
 }
 const cases=new Map(),errorStepsUnclassified=[];
 const answerFor=(s,p)=>s.d.answers?.find(a=>a.platform===p);
 function opens(s,p){
   const a=answerFor(s,p),about=operation(s);
   if(a){
     if(nothingSent(a,about)||settled(a,about))return false;
     if(uncertainState(a,about))return true;
     if(a.state)return !provenNoPress(s,p);
   }else if(s.d.result==='in_doubt')return true; // Existing journals: no fabricated platform answer.
   const events=usableEvents(s),contact=events?.some(e=>(!e.platform||e.platform===p)&&['call','stage','transition'].includes(e.type));
   return (cutoff(s)||s.d.result==='unknown'||s.d.exit===3||s.d.exit===2&&contact)&&!provenNoPress(s,p);
 }
 for(const s of steps.filter(r=>started(r)&&r.d.class==='write'&&pressRoles.includes(r.d.role)&&operation(r))){
   for(const platform of s.d.platforms){
     if(s.d.exit===2 && !usableEvents(s)){
       errorStepsUnclassified.push({step:s.seq,platform,exit:2,code:answerFor(s,platform)?.code??s.d.code??null});
     }
     if(!opens(s,platform))continue;
     const key=JSON.stringify([s.d.job,platform,operation(s)]);
     if(!cases.has(key))cases.set(key,{s,platform,members:[]});
     cases.get(key).members.push(s.seq);
   }
 }
 const inDoubt=[...cases.values()].map(({s,platform,members})=>{
   const about=operation(s),later=records.filter(r=>r.seq>s.seq&&r.d.job===s.d.job);
   function settles(r){
     if(r.type==='OBSERVATION')return r.d.about===about&&observationPlatform(r)===platform&&countsLook(r,records.find(x=>x.seq===firstEligible),now);
     if(r.type!=='STEP'||r.d.platforms.length!==1||r.d.platforms[0]!==platform)return false;
     const openingRefs=answerFor(s,platform)?.refs??s.d.refs??[],closingRefs=answerFor(r,platform)?.refs??r.d.refs??[];
     if(openingRefs.length&&closingRefs.length&&!openingRefs.some(ref=>closingRefs.includes(ref)))return false;
     if(r.d.role!==({post:'verify',edit:'edit',delete:'delete_verify'})[about])return false;
     if(about==='edit'&&r.d.class!=='read')return false;
     if(answerFor(r,platform))return confirmedAnswer(answerFor(r,platform),about);
     if(r.d.result!=='ok')return false;
     if(Object.hasOwn(r.d,'confirmed_state'))return r.d.confirmed_state===confirmed[about];
     // Old journals lack this field. Require an affirmative state in the
     // paired command or its final snapshot before treating a read as verified.
     const cmd=cc.runs.find(x=>x.awl_run_id&&x.awl_run_id===r.d.awl_run_id);
     if(!trusted||!intact||!cmd||cmd.ended!==true||cmd.exit_code!==0||cmd.dropped_events!==0||cmd.sequence_gap!==false)return false;
     return cc.events.some(e=>e.run===cmd.run&&(!e.platform||e.platform===platform)&&
       [e.state,e.after?.state].includes(confirmed[about]));
   }
   const writes=later.filter(r=>r.type==='STEP'&&r.d.class==='write'&&started(r)&&r.d.platforms.includes(platform));
   const relevant=writes.filter(r=>operation(r)===about&&['publish','repeat_same','changed_plan','resume','edit','delete'].includes(r.d.role));
   const ignored=relevant.filter(r=>provenNoPress(r,platform)),mayPress=relevant.filter(r=>!ignored.includes(r));
   const firstEligible=Math.max(s.seq,...mayPress.map(r=>r.seq));
   const closure=later.find(r=>r.seq>firstEligible&&settles(r));
   const strict=!!closure&&!writes.some(r=>r.seq<closure.seq);
   const press=cutoff(s)?pressEvidence(s,platform):null;
   return {job:s.d.job,platform,step:s.seq,steps:members,closed:!!closure,closed_strict:strict,
     cutoff:cutoff(s),cutoff_press_flag:press,last_may_press:firstEligible,settled_at_seq:closure?.seq??null,
     ignored_refused_steps:ignored.filter(r=>r.d.result==='refused').map(r=>r.seq),ignored_no_press_steps:ignored.map(r=>r.seq),
     by:closure?(closure.type==='OBSERVATION'?'person':'read'):null,
     seconds:closure?Math.max(0,(Date.parse(closure.at)-Date.parse(s.at))/1000):null,clock_reversed:!!closure&&Date.parse(closure.at)<Date.parse(s.at)};
 });
 const result={schema:'round2-reconcile/1',as_of:new Date(now).toISOString(),records:records.length,crosscast_present:cc.present,crosscast_chain_ok:cc.chain_ok,time_alignment:!cc.present?'journal_only':cc.times==='exact'&&cc.chain_ok?'exact':'unavailable',
   error_steps_unclassified:errorStepsUnclassified,gate_checks:gateChecks(records),publish_checks:publishChecks(records,jobs),
   observation_checks:observationChecks(records,jobs),
   command_binding:!cc.present?'journal_only':trusted?'shared_id':'unavailable',
   ungated_writes:ungated,matched_write_commands:trusted?bindings.length-ungated:null,write_bindings:bindings,
   admitted_sessions_without_write:runs.filter(r=>!steps.some(s=>s.run===r.id&&s.d.class==='write')).length,
   captures_checked:!!ctx.home,captures_missing:ctx.home?missing:null,in_doubt:inDoubt,in_doubt_open:inDoubt.filter(r=>!r.closed).length,
   fault_delays:records.filter(r=>r.type==='FAULT_RESULT').map(r=>{
     const armed=records.findLast(a=>a.seq<r.seq&&a.type==='FAULT'&&a.run===r.run&&a.d.job===r.d.job&&a.d.role===r.d.role);
     const row={job:r.d.job,kind:r.d.kind,role:r.d.role,fired:r.d.fired,failed:r.d.failed};
     if(armed&&armed.d.mechanism!=='tool_switch'){row.listed_ms=armed.d.delay_ms;row.measured_ms=r.d.action_delay_ms??null;row.gone_ms=r.d.gone_delay_ms??null;row.reason=r.d.reason??null;}
     return row;
   }),cutoff_without_press_flag:inDoubt.filter(r=>r.cutoff&&r.cutoff_press_flag===false).length,
   cutoff_press_flag_unavailable:inDoubt.filter(r=>r.cutoff&&r.cutoff_press_flag===null).length,
   in_doubt_open_strict:inDoubt.filter(r=>!r.closed_strict).length,
   looks_owed:owesLook.map(({job,platform,step,about})=>({job,platform,step,about})),look_checks:lookChecks,looks_scope_unknown:eligible.filter(r=>!scope(r.d.job)).length,refusals_by_code:{...refusals},
   violations:records.filter(r=>r.type==='VIOLATION').length,crosscast_command_losses:cc.losses,crosscast_integrity:cc.integrity};
 if(privacy){result.as_of=result.as_of.slice(0,10);result.time_policy='UTC_date_end; in_doubt_minutes_rounded_up';
  for(const c of result.in_doubt){c.minutes=c.seconds===null?null:Math.ceil(c.seconds/60);delete c.seconds;}
  if(!ctx.home)throw new Error('private_home_required');
  return protectDigests(result,value=>keyedDigest('round2-private-comparison-v1\0'+value,ctx.home,{create:false}));}
 return result;
}
export function main(argv=process.argv.slice(2)){
 const privacy=argv.includes('--privacy');if(argv.filter(x=>x==='--privacy').length>1)throw new Error('duplicate_flag');
 const {flags:f,command}=parseFlags(argv.filter(x=>x!=='--privacy'));allowed(f,['site','slot','journal','crosscast','as-of']);if(command.length)throw new Error('invalid_arguments');
 console.log(JSON.stringify(reconcile(context(f),f.crosscast,{now:f['as-of'],privacy})));return 0;
}
if(process.argv[1]&&import.meta.url===pathToFileURL(path.resolve(process.argv[1])).href){try{process.exitCode=main();}catch(e){console.error(refusalWord(e,'reconcile_refused'));process.exitCode=70;}}

import fs from 'node:fs';
import path from 'node:path';
import {pathToFileURL} from 'node:url';
import {readJournal,context} from './journal.mjs';
import {sha,parseFlags,allowed,readJSON} from './io.mjs';
import {crosscast,commandClass} from './crosscast.mjs';
import {jobList} from './jobs.mjs';

export function reconcile(ctx,crosscastFile,{now=Date.now()}={}){
 if(typeof now==='string'&&!/^\d{4}-\d\d-\d\dT\d\d:\d\d(?::\d\d(?:\.\d+)?)?(?:Z|[+-]\d\d:\d\d)$/.test(now))throw new Error('invalid_as_of');
 now=typeof now==='string'?Date.parse(now):now;
 if(!Number.isFinite(now))throw new Error('invalid_as_of');
 const records=readJournal(ctx.journal),cc=crosscast(crosscastFile),steps=records.filter(r=>r.type==='STEP');
 const admissions=records.filter(r=>r.type==='GATE_DECISION'&&r.d.decision==='plan_only');
 const runs=[...new Set(admissions.map(r=>r.run))].map(id=>{
  const first=admissions.find(r=>r.run===id),end=records.find(r=>r.run===id&&r.type==='RUN_END');
  return {id,start:Date.parse(first.at),end:end?Date.parse(end.at):null};
 });
 const refusals={};for(const s of steps.filter(r=>r.d.result==='refused'))refusals[s.d.code??'other']=(refusals[s.d.code??'other']??0)+1;
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
 const started=s=>!(reservations.find(x=>x.run===s.run&&x.step_seq===s.seq)?.started===false||s.d.exit===127);
 const operation=s=>s.d.action==='delete'||['delete','delete_verify'].includes(s.d.role)?'delete':s.d.role==='edit'?'edit':
   ['publish','repeat_same','changed_plan','resume','verify'].includes(s.d.role)?'post':null;
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
  const qualifying=looks.find(r=>r.d.minutes_after>=10&&r.d.minutes_after<=1440);
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
  const provenNoPress=s=>{
    if(!trusted||!((s.d.result==='refused'&&s.d.exit===2)||
        (['repeat_same','resume','verify'].includes(s.d.role)&&[0,4].includes(s.d.exit))))return false;
  if(cc.integrity?.unreadable_loss_records!==0||cc.integrity?.loss_records_complete!==true||cc.integrity?.loss_folder_unreadable!==false)return false;
  const binding=bindings.find(b=>b.step_seq===s.seq&&b.status==='matched');
  const command=binding&&cc.runs.find(r=>r.run===binding.run);
    if(!command||command.exit_code!==s.d.exit||command.ended!==true||command.dropped_events!==0||command.sequence_gap!==false)return false;
  const events=cc.events.filter(e=>e.run===command.run),first=events.find(e=>e.type==='command_start'),last=events.findLast(e=>e.type==='command_end');
  if(!first||!last)return false;
  // Require complete unchanged snapshots; absence of evidence or a missing
  // export is never proof that the refused command sent nothing.
  if(!first.before||!last.after||!['final_presses','delete_attempted'].every(k=>
     first.before[k]!==undefined&&first.before[k]!==null&&first.before[k]===last.after[k]))return false;
  return !events.some(e=>e.type==='call'||e.type==='span'||e.operation==='press'||e.platform_writes>0||e.stage==='pressing');
 };
  const cases=new Map();
  for(const s of steps.filter(r=>r.d.result==='in_doubt'))for(const platform of s.d.platforms.length?s.d.platforms:[null]){
    const key=JSON.stringify([s.d.job,platform,operation(s)??'post']);
    if(!cases.has(key))cases.set(key,{s,platform,members:[]});
    cases.get(key).members.push(s.seq);
  }
  const inDoubt=[...cases.values()].map(({s,platform,members})=>{
  const about=operation(s)??'post',later=records.filter(r=>r.seq>s.seq&&r.d.job===s.d.job);
  const closure=later.find(r=>(r.type==='OBSERVATION'&&r.d.about===about&&r.d.seen!=='unclear'&&observationPlatform(r)===platform)||
      (r.type==='STEP'&&r.d.platforms.length===1&&r.d.platforms.includes(platform)&&
        (r.d.class==='read'||(about==='post'&&r.d.role==='verify'))&&r.d.result==='ok'&&
       (about==='delete'?r.d.role==='delete_verify':about==='edit'?r.d.role==='edit':r.d.role==='verify')));
  const writes=later.filter(r=>r.seq<(closure?.seq??Infinity)&&r.type==='STEP'&&r.d.class==='write'&&r.d.platforms.includes(platform));
    const ignored=writes.filter(provenNoPress),closed=!!closure&&writes.length===ignored.length,strict=!!closure&&!writes.length;
    return {job:s.d.job,platform,step:s.seq,steps:members,closed,closed_strict:strict,
      ignored_refused_steps:ignored.filter(r=>r.d.result==='refused').map(r=>r.seq),ignored_no_press_steps:ignored.map(r=>r.seq),
    by:closed?(closure.type==='OBSERVATION'?'person':'read'):null,
    seconds:closed?Math.max(0,(Date.parse(closure.at)-Date.parse(s.at))/1000):null,clock_reversed:!!closure&&Date.parse(closure.at)<Date.parse(s.at)};
  });
 return {schema:'round2-reconcile/1',as_of:new Date(now).toISOString(),records:records.length,crosscast_present:cc.present,crosscast_chain_ok:cc.chain_ok,time_alignment:!cc.present?'journal_only':cc.times==='exact'&&cc.chain_ok?'exact':'unavailable',
   command_binding:!cc.present?'journal_only':trusted?'shared_id':'unavailable',
   ungated_writes:ungated,matched_write_commands:trusted?bindings.length-ungated:null,write_bindings:bindings,
   admitted_sessions_without_write:runs.filter(r=>!steps.some(s=>s.run===r.id&&s.d.class==='write')).length,
   captures_checked:!!ctx.home,captures_missing:ctx.home?missing:null,in_doubt:inDoubt,in_doubt_open:inDoubt.filter(r=>!r.closed).length,
   in_doubt_open_strict:inDoubt.filter(r=>!r.closed_strict).length,
   looks_owed:owesLook.map(({job,platform,step,about})=>({job,platform,step,about})),look_checks:lookChecks,looks_scope_unknown:eligible.filter(r=>!scope(r.d.job)).length,refusals_by_code:refusals,
   violations:records.filter(r=>r.type==='VIOLATION').length,crosscast_command_losses:cc.losses,crosscast_integrity:cc.integrity};
}
export function main(argv=process.argv.slice(2)){
 const {flags:f,command}=parseFlags(argv);allowed(f,['site','journal','crosscast','as-of']);if(command.length)throw new Error('invalid_arguments');
 console.log(JSON.stringify(reconcile(context(f),f.crosscast,{now:f['as-of']})));return 0;
}
if(process.argv[1]&&import.meta.url===pathToFileURL(path.resolve(process.argv[1])).href){try{process.exitCode=main();}catch{console.error('reconcile_refused');process.exitCode=70;}}

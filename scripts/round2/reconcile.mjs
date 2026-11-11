import fs from 'node:fs';
import path from 'node:path';
import {pathToFileURL} from 'node:url';
import {readJournal,context} from './journal.mjs';
import {sha,parseFlags,allowed,readJSON} from './io.mjs';
import {crosscast,commandClass} from './crosscast.mjs';

export function reconcile(ctx,crosscastFile){
 const records=readJournal(ctx.journal),cc=crosscast(crosscastFile),steps=records.filter(r=>r.type==='STEP');
 const admissions=records.filter(r=>r.type==='GATE_DECISION'&&r.d.decision==='plan_only');
 const runs=[...new Set(admissions.map(r=>r.run))].map(id=>{
  const first=admissions.find(r=>r.run===id),end=records.find(r=>r.run===id&&r.type==='RUN_END');
  return {id,start:Date.parse(first.at),end:end?Date.parse(end.at):null};
 });
 const inDoubt=steps.filter(r=>r.d.result==='in_doubt').map(s=>{
  const later=records.filter(r=>r.seq>s.seq&&r.d.job===s.d.job);
  // A successful read-class command must concern the same operation. A
  // status command or ambiguous look cannot settle an uncertain write.
  const about=s.d.role==='delete'?'delete':s.d.role==='edit'?'edit':'post';
  const closure=later.find(r=>(r.type==='OBSERVATION'&&r.d.about===about&&r.d.seen!=='unclear')||
    (r.type==='STEP'&&r.d.class==='read'&&r.d.result==='ok'&&
       (about==='delete'?r.d.role==='delete_verify':about==='edit'?r.d.role==='edit':r.d.role==='verify')));
  const intervening=later.some(r=>r.seq<(closure?.seq??Infinity)&&r.type==='STEP'&&r.d.class==='write');
  return {job:s.d.job,step:s.seq,closed:!!closure&&!intervening,by:closure&&!intervening?(closure.type==='OBSERVATION'?'person':'read'):null,
    seconds:closure&&!intervening?Math.max(0,(Date.parse(closure.at)-Date.parse(s.at))/1000):null,clock_reversed:!!closure&&Date.parse(closure.at)<Date.parse(s.at)};
 });
 const refusals={};for(const s of steps.filter(r=>r.d.result==='refused'))refusals[s.d.code??'other']=(refusals[s.d.code??'other']??0)+1;
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
 const jobs=ctx.home?readJSON(path.join(ctx.home,'jobs.json'),[]):[];
 const reservations=ctx.home?readJSON(path.join(ctx.home,'reservations.json'),[]):[];
 const eligible=steps.filter(r=>['publish','edit','delete'].includes(r.d.role));
 const scope=job=>jobs.find(j=>j.id===job)?.run_class;
 const owesLook=eligible.filter(r=>['test_post','real_slot'].includes(scope(r.d.job))&&
   !(reservations.find(x=>x.run===r.run&&x.step_seq===r.seq)?.started===false||r.d.exit===127)).filter(s=>{
  const about=s.d.role==='publish'?'post':s.d.role;
  return !records.some(r=>r.seq>s.seq&&r.type==='OBSERVATION'&&r.d.job===s.d.job&&r.d.about===about&&r.d.seen!=='unclear'&&r.d.minutes_after>=10&&r.d.minutes_after<=60);
 });
 let ungated=null,unmatched=0;
 if(cc.present&&cc.chain_ok&&cc.times==='exact'){
  ungated=0;
  for(const command of cc.runs.filter(r=>commandClass(r)==='write')){
   const time=typeof command.t_start==='string'?Date.parse(command.t_start):NaN;
   if(!Number.isFinite(time)){unmatched++;continue;}
   if(!runs.some(r=>time>=r.start&&r.end!==null&&time<=r.end)){
    if(runs.some(r=>time>=r.start&&r.end===null))unmatched++;else ungated++;
   }
  }
 }else if(cc.present)unmatched=cc.runs.filter(r=>commandClass(r)==='write').length;
 return {schema:'round2-reconcile/1',records:records.length,crosscast_present:cc.present,crosscast_chain_ok:cc.chain_ok,time_alignment:!cc.present?'journal_only':cc.times==='exact'&&cc.chain_ok?'exact':'unavailable',
   ungated_writes:ungated,commands_without_time_coverage:unmatched,admitted_sessions_without_write:runs.filter(r=>!steps.some(s=>s.run===r.id&&s.d.class==='write')).length,
   captures_checked:!!ctx.home,captures_missing:ctx.home?missing:null,in_doubt:inDoubt,in_doubt_open:inDoubt.filter(r=>!r.closed).length,
   looks_owed:owesLook.map(r=>({job:r.d.job,step:r.seq,about:r.d.role==='publish'?'post':r.d.role})),looks_scope_unknown:eligible.filter(r=>!scope(r.d.job)).length,refusals_by_code:refusals,
   violations:records.filter(r=>r.type==='VIOLATION').length,crosscast_command_losses:cc.losses};
}
export function main(argv=process.argv.slice(2)){
 const {flags:f,command}=parseFlags(argv);allowed(f,['site','journal','crosscast']);if(command.length)throw new Error('invalid_arguments');
 console.log(JSON.stringify(reconcile(context(f),f.crosscast)));return 0;
}
if(process.argv[1]&&import.meta.url===pathToFileURL(path.resolve(process.argv[1])).href){try{process.exitCode=main();}catch{console.error('reconcile_refused');process.exitCode=70;}}

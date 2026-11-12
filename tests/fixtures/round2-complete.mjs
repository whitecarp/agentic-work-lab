// Entire synthetic round from the public counts file. No command is launched.
// The killed command loses its end/ledger copy; publish saves phase=submitting,
// resume merges the worker copy, delete repeats return only an error code.
import fs from 'node:fs';
import {generate} from '../../scripts/round2/plan-runs.mjs';
import {canonical} from '../../scripts/round2/journal.mjs';
import {planRun} from '../../scripts/operations-gate.mjs';
import {hash} from '../../paper/round2/inputs.mjs';
import {fixture,rebuild,at,gateHash,privateSite,bundle} from './round2-analysis.mjs';
const counts=JSON.parse(fs.readFileSync(new URL('../round2/fixtures/planned-counts-v1.json',import.meta.url)));
const killed=['end_command_after_press','end_tree_after_press','browser_lost_during_upload','end_browser_after_edit_save'];
export function completeSite(site,{plantSecond=false}={}){
 const seed=Buffer.alloc(32,4),generated=generate(counts,site,seed,{dueFrom:at(0)});
 const f=fixture({site,jobs:generated.plan.runs});f.rows.splice(1);
 f.plan=generated.plan;f.seed=seed;f.rows[0].d.plan_sha256=hash(canonical(f.plan)+'\n');
 f.tool.window.until='2026-12-01T00:00:00.000Z';
 let job,session,clock=5,current=site,ordinal=0,planted=false;
 const put=(type,d,m=clock,slot=current)=>{const r=f.put(type,d,m,slot);r.run=['GO_AHEAD','JOB_END','SIGN_OFF','HANDOFF'].includes(type)?null:session;return r;};
 const grant=(scope,count,m=clock,platform)=>put('GO_AHEAD',{job:job.id,scope,count,by:site,words_sha256:'3'.repeat(64),...(platform?{platform}:{})},m);
 const gate=(m,overrides={})=>{
  const d={executor:current,owner:current,stop:false,running:false,now:at(m),launcher:'manual',slot_id:job.id,
   jobs:[{id:job.id,due_at:at(0),completed:false,authorized:true,verdict:'due'}],gate_sha256:gateHash,...overrides};
  Object.assign(d,planRun(d));return put('GATE_DECISION',d,m,d.executor);
 };
 const look=(p,about,seen,m,last,fields_match=true)=>put('OBSERVATION',{job:job.id,platform:p,about,seen,by:site,client:'phone_app',minutes_after:Math.floor(m-last),fields_match,ref:hash(job.id).slice(0,16),words_sha256:'4'.repeat(64),shot_sha256:null},m);
 const arm=(fault,m)=>{
  put('FAULT',{job:job.id,...fault},m);
  put('FAULT_RESULT',{job:job.id,kind:fault.kind,role:fault.role,fired:true,failed:false,action_delay_ms:5,gone_delay_ms:6,reason:'completed'},m+.01);
 };
 const step=({role='publish',ps=job.platforms,m,state='SUBMISSION_VERIFIED',n=1,press=false,read=false,cut=false,code=null,exit=0,cls='write',empty=false,draft=false,deleteFlag=null,previous=null,upload=false}={})=>{
  const ref=hash(job.id).slice(0,16),kind=role==='edit'?'edit':['delete','delete_verify','delete_dry_run'].includes(role)?'deletion':'browser';
  const s=f.step({job:job.id,role,platforms:ps,state,press:n,m,ref,contact:false,exit,code,cls,kind});s.run=session;s.slot=current;s.d.refs=[ref];
  s.d.answers=ps.map(p=>({platform:p,state,previous_state:previous,code,press_flag:kind==='deletion'?deleteFlag:p==='bilibili'||n===null?null:n>0,answered:true,refs:[ref],not_saved:false}));
  const id=s.d.awl_run_id,cmd=f.tool.runs.find(c=>c.run===id);
  const snap=(p,flag=n)=>({platform:p,ref,state,final_presses:p==='bilibili'?null:flag,final_submit_attempted:p==='bilibili'||flag===null?null:flag>0,delete_attempted:deleteFlag,id_present:n>0});
  const ev=[{type:'command_start',run:id,at:at(m-.02),before:role==='publish'||role==='repeat_same'?null:snap(ps[0],empty?null:press?Math.max(0,n-1):n),code:f.tool.builds[0],awl_run_id:id}];
  for(const p of ps){
   if(upload){ev.push({type:'call',run:id,at:at(m-.019),platform:p,action:'upload'});ev.push({type:'stage',run:id,at:at(m-.018),platform:p,stage:'uploading'});}
   if(draft)ev.push({type:'transition',run:id,at:at(m-.017),platform:p,kind,ref,phase:'reading_back',state:'UNKNOWN',final_presses:n-1,final_submit_attempted:true,still_a_draft:true});
   if(press){ev.push({type:'stage',run:id,at:at(m-.016),platform:p,stage:'pressing'});
    ev.push({type:'transition',run:id,at:at(m-.015),platform:p,kind,ref,state:kind==='edit'?'EDIT_SUBMITTING':state,phase:kind==='browser'?'submitting':undefined,
     final_presses:p==='bilibili'?null:n,final_submit_attempted:p==='bilibili'?null:true,...(kind==='deletion'?{operation:'press',platform_writes:1,delete_attempted:true}:{})});}
   if(read)ev.push({type:'stage',run:id,at:at(m-.014),platform:p,stage:'reading back'});
   if(read)ev.push({type:'transition',run:id,at:at(m-.013),platform:p,kind,ref,state,phase:'reading_back',...snap(p)});
  }
  if(cut){Object.assign(s.d,{answered:false,result:'unknown',exit:1,code:null});s.d.answers.forEach(a=>Object.assign(a,{answered:false,state:null,press_flag:null,refs:[]}));Object.assign(cmd,{ended:false,exit_code:null,t_end:null});}
  else ev.push({type:'command_end',run:id,at:at(m),exit_code:exit,after:role==='publish'||n===null?null:snap(ps[0]),platforms:Object.fromEntries(ps.map(p=>[p,snap(p)]))});
  f.tool.events=f.tool.events.filter(e=>e.run!==id).concat(ev.map(e=>({...e,seq:0,dropped_before:0})));
  return s;
 };
 const ends=[];const starts={youtube:[],douyin:[]};
 for(const j of f.plan.runs){job=j;session=(++ordinal).toString(16).padStart(32,'0');clock+=3;
  if(j.run_class==='test_post'){
   for(const p of j.platforms){const q=starts[p];if(q.length>=4)clock=Math.max(clock,q.at(-4)+1441);if(q.length)clock=Math.max(clock,q.at(-1)+21);}
   j.platforms.forEach(p=>starts[p].push(clock));
  }
  const begin=clock;gate(begin);
  if(j.run_class==='drill'){
   f.rows.pop(); // Replace admission with the drill's actual configured decision.
   const inputs={};const x={id:j.id,due_at:at(0),completed:false,authorized:true,verdict:'due'};
   if(j.kind==='handoff'){
    gate(begin,{executor:'codex'});put('HANDOFF',{from:current,to:'codex',job:j.id,by:site,words_sha256:'5'.repeat(64)},begin+.1);current='codex';
    gate(begin+.2,{executor:site});put('HANDOFF',{from:current,to:site,job:j.id,by:site,words_sha256:'6'.repeat(64)},begin+.3);current=site;
   }else{
    if(j.kind==='stop_flag')inputs.stop=true;
    if(j.kind==='not_owner')inputs.executor='codex';
    if(j.kind==='already_running')inputs.running=true;
    if(j.kind==='not_authorized'){x.authorized=false;x.verdict=j.kind;}
    if(j.kind==='completed'){x.completed=true;x.verdict=j.kind;}
    if(j.kind==='not_due'){x.due_at='2100-01-01T00:00:00Z';x.verdict=j.kind;}
    gate(begin,{...inputs,jobs:[x]});
   }
   put('JOB_END',{job:j.id,reason:'done',by:site},begin+1);put('RUN_END',{state:'done'},begin+1.1);continue;
  }
  if(j.run_class==='refusal'){
   grant(site==='owner'?'listed_test_posts':'one_write',1,begin+.1);
   if(j.kind==='stop_before_write'){
    put('STOP_SET',{by:site,words_sha256:'7'.repeat(64)},begin+.2);
    put('STEP_REFUSED',{job:j.id,role:'publish',reason:'stop_flag'},begin+1);
    put('STOP_CLEARED',{by:site,words_sha256:'8'.repeat(64)},begin+2);
   }else step({role:'changed_plan',m:begin+1,state:null,n:0,exit:2,code:'confirmation_required'});
   put('JOB_END',{job:j.id,reason:'done',by:site},begin+2.1);put('RUN_END',{state:'done'},begin+2.2);continue;
  }
  const real=j.run_class==='real_slot';grant(real||site==='tester'?'one_write':'listed_test_posts',real?4:2+3*j.platforms.length+(site==='owner'&&j.edit?j.platforms.length:0),begin+.1);
  if(real){
   step({m:begin+1,n:null,read:true,upload:true});
   for(const offset of [2,60,1441])step({role:'verify',m:begin+offset,n:null,read:true});
   step({role:'delete_dry_run',m:begin+1442,n:null,state:null,exit:2,cls:'local',code:'unsupported_platform'});
   look('bilibili','post','present_once',begin+20,begin+1);
   put('JOB_END',{job:j.id,reason:'done',by:site},begin+1443);put('RUN_END',{state:'done'},begin+1444);continue;
  }
  const fault=j.faults.find(f=>f.role==='publish'),before=j.group==='D',c4=fault?.kind==='between_save_and_press',c4dy=c4&&j.platforms[0]==='douyin';
  if(fault)arm(fault,begin+.2);
  const initial=before?0:1;step({m:begin+1,n:initial,press:!before,read:!fault,upload:true,
   state:!fault?'SUBMISSION_VERIFIED':fault.kind==='stop_during_upload'?'PREPARATION_INCOMPLETE':before?'IN_PROGRESS':'UNKNOWN',cut:killed.includes(fault?.kind),exit:fault?4:0,code:fault?'injected_fault':null});
  step({role:'repeat_same',m:begin+2,n:killed.includes(fault?.kind)?null:initial,state:'SKIPPED_EXISTING',previous:!fault?'SUBMISSION_VERIFIED':before||killed.includes(fault.kind)?'IN_PROGRESS':'UNKNOWN',exit:fault?4:0});
  let lastPost=begin+3,settlements=[];
  for(const [i,p]of j.platforms.entries()){
   let n=c4&&!c4dy?2:before?1:initial;
   const bad=plantSecond&&!planted&&fault?.kind==='end_command_after_press';if(bad){n=2;planted=true;}
   const resumed=step({role:'resume',ps:[p],m:begin+3+i,n,press:before||c4&&!c4dy||bad,read:true,empty:killed.includes(fault?.kind),draft:c4&&!c4dy,state:c4dy?'UNKNOWN':'SUBMISSION_VERIFIED',exit:c4dy?4:0});
   lastPost=Math.max(lastPost,begin+3+i);
   step({role:'verify',ps:[p],m:begin+5+i,n,read:true,state:c4dy?'UNKNOWN':'SUBMISSION_VERIFIED',exit:c4dy?4:0});
   look(p,'post',c4dy?'absent':'present_private',begin+22,lastPost);
   if(c4dy){look(p,'post','absent',begin+1230,lastPost);settlements.push({platform:p,how:'nothing_posted'});continue;}
   if(j.edit){
    if(site==='tester')grant('one_write',1,begin+23,p);
    const ef=j.faults.find(f=>f.role==='edit');if(ef)arm(ef,begin+23.2);
    step({role:'edit',ps:[p],m:begin+24,n,press:true,read:!ef,cut:!!ef,state:ef?'EDIT_SUBMITTED_UNVERIFIED':'EDIT_VERIFIED',exit:ef?4:0});
    step({role:'edit',ps:[p],m:begin+26,n,read:true,cls:'read',state:'EDIT_VERIFIED'});
    look(p,'edit','present_private',begin+40,begin+24);
   }
   grant('one_delete',j.wrong_delete_code_first?3:2,begin+41,p);
   if(j.wrong_delete_code_first)step({role:'delete',ps:[p],m:begin+42,n,state:null,exit:2,code:'confirmation_required'});
   const df=j.faults.find(f=>f.role==='delete');if(df)arm(df,begin+43);
   step({role:'delete',ps:[p],m:begin+44,n,press:true,read:!df,deleteFlag:true,state:df?'DELETE_PENDING':'DELETED_VERIFIED',exit:df?4:0});
   if(df)step({role:'delete',ps:[p],m:begin+46,n,state:null,deleteFlag:null,code:'deletion_already_attempted',exit:2});
   step({role:'delete_verify',ps:[p],m:begin+48,n,read:true,deleteFlag:true,state:'DELETED_VERIFIED',cls:'read'});
   look(p,'delete','absent',begin+65,df?begin+46:begin+44);settlements.push({platform:p,how:'delete_confirmed'});
  }
  const end=begin+(c4dy?1231:66);put('JOB_END',{job:j.id,reason:'done',by:site,platform_settlements:settlements},end);put('RUN_END',{state:'done'},end+.1);
  clock=begin+70;ends.push(end);
 }
 // Chronological records, separate sessions, and a final sign-off for each day
 // containing an action or observation. Delayed second looks retain their job.
 f.rows.sort((a,b)=>Date.parse(a.at)-Date.parse(b.at));
 const days=[...new Set(f.rows.filter(r=>['STEP','GO_AHEAD','OBSERVATION','JOB_END'].includes(r.type)).map(r=>r.at.slice(0,10)))];
 for(const day of days){const m=(Date.parse(day+'T23:59:59Z')-Date.parse(at(0)))/60000;put('SIGN_OFF',{day,by:site,answer:'yes',records:0,head_sha256:'0'.repeat(64),closing:false},m);}
 f.rows.sort((a,b)=>Date.parse(a.at)-Date.parse(b.at));
 const last=f.rows.at(-1).at,close=(Date.parse(last)-Date.parse(at(0)))/60000+1;
 put('SIGN_OFF',{day:at(close).slice(0,10),by:site,answer:'yes',records:0,head_sha256:'0'.repeat(64),closing:true},close);
 f.tool.events.sort((a,b)=>Date.parse(a.at)-Date.parse(b.at));f.tool.events.forEach((e,i)=>e.seq=i+1);
 return rebuild({site,rows:f.rows,tool:f.tool,plan:f.plan,seed,asOf:at(close)});
}
export const completeRound=options=>bundle(completeSite('owner',options),privateSite(completeSite('tester')));

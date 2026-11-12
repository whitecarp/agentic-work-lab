import fs from 'node:fs';
import {canonical} from '../../scripts/round2/journal.mjs';
import {reconcileData} from '../../paper/round2/reconcile-export.mjs';
import {protectDigests} from '../../scripts/round2/export.mjs';
import {privateToolCopy} from '../../scripts/round2/private-tool-copy.mjs';
import {hash} from '../../paper/round2/inputs.mjs';
export const gateHash=hash(fs.readFileSync(new URL('../../scripts/operations-gate.mjs',import.meta.url)));
export const commit='a'.repeat(40),digest='b'.repeat(64);
const origin=Date.parse('2026-11-20T08:00:00.000Z');
export const at=m=>new Date(origin+m*60000).toISOString();
export function fixture({site='owner',platforms=['youtube'],phase='2b',jobs=null}={}) {
 const seed=Buffer.alloc(32,3),rows=[],tool={schema:'crosscast-audit/1',times:'exact',
  window:{since:'2026-11-20T00:00:00.000Z',until:'2026-11-23T00:00:00.000Z'},
  builds:[{commit,source_digest:digest,version:'0.16.0'}],runs:[],events:[],publications:[],deletions:[],losses:[],
  integrity:{chain_ok:true,dropped_events:0,unplaced_dropped_events:0,unreadable_loss_records:0,loss_records_complete:true,loss_folder_unreadable:false}};
 const jobRows=jobs??[{id:'job-a',run_class:'test_post',platforms,group:'B',faults:[],visibility:'private',edit:false}];
 const plan={schema:'round2-planned-runs/1',site,seed_sha256:hash(seed),runs:jobRows};
 let minutes=0,commandN=0;const run='1'.repeat(32),labRef='c'.repeat(16);
 function put(type,d,m=minutes,slot=site){
  minutes=Math.max(minutes,m);const r={v:1,seq:rows.length+1,prev:'',at:at(m),site,run:type==='PROTOCOL_SEAL'?null:run,slot,type,d};rows.push(r);return r;
 }
 put('PROTOCOL_SEAL',{phase,lab_commit:'d'.repeat(40),crosscast_commit:commit,crosscast_digest:digest,
  protocol_sha256:'e'.repeat(64),plan_sha256:hash(canonical(plan)+'\n'),analysis_sha256:null,rules_card_sha256:null},0);
 const auth=job=>put('GO_AHEAD',{job,scope:'listed_test_posts',count:20,by:site,words_sha256:'e'.repeat(64)},1);
 jobRows.forEach(j=>auth(j.id));
 put('GATE_DECISION',{executor:site,owner:site,stop:false,running:false,now:at(2),launcher:'manual',slot_id:null,
  decision:'plan_only',reason:'checked',due:jobRows.map(j=>j.id),jobs:jobRows.map(j=>({id:j.id,verdict:'due',due_at:at(0),completed:false,authorized:true})),gate_sha256:gateHash},2);
 function step({job='job-a',role='publish',platforms:ps=platforms,state='SUBMISSION_VERIFIED',states={},press=1,exit=0,contact=true,code=null,kind='browser',m=minutes+1,ref='f'.repeat(16),before=null,after=null,answers={},cls=null}={}) {
  const id=(++commandN).toString(16).padStart(32,'0'),action=role==='delete_verify'?'delete':role==='repeat_same'||role==='changed_plan'?'publish':role;
  const stepClass=cls??(['delete_verify'].includes(role)?'read':'write');
  const r=put('STEP',{job,class:stepClass,tool:'vpub',action,role,platforms:ps,exit,ms:1000,capture_sha256:'1'.repeat(64),
   awl_run_id:id,result:exit===0?'ok':exit===2?'refused':'in_doubt',code,refs:[labRef],fault_fired:null,started:true,answered:true,
   answers:ps.map(p=>({platform:p,state:states[p]??state,previous_state:null,code,press_flag:press===null?null:press>0,
    answered:true,refs:[labRef],not_saved:false,...answers[p]}))},m);
  const events=[{type:'command_start',run:id,at:at(m-1/60),before,code:tool.builds[0],awl_run_id:id}];
  if(contact)for(const p of ps){
   events.push({type:'stage',run:id,at:at(m-1/120),platform:p,stage:press?'pressing':'reading back'});
   events.push({type:'transition',run:id,at:at(m-1/240),platform:p,kind,ref,state:states[p]??state,
    final_presses:press,final_submit_attempted:press===null?null:press>0,operation:press?'press':'readback',platform_writes:press?1:0});
  }
  events.push({type:'command_end',run:id,at:at(m),exit_code:exit,after,platforms:Object.fromEntries(ps.map(p=>[p,{ref,state:states[p]??state,final_presses:press,final_submit_attempted:press===null?null:press>0}]))});
  for(const e of events)tool.events.push({...e,seq:tool.events.length+1,dropped_before:0});
  tool.runs.push({run:id,awl_run_id:id,command:action,build:0,flags:{verify_only:stepClass==='read'},ended:true,exit_code:exit,
   t_start:at(m-1/60),t_end:at(m),seconds:1,platforms:Object.fromEntries(ps.map(p=>[p,{ref}])),sequence_gap:false,chain_ok:true,dropped_events:0,loss_complete:false});
  for(const p of ps){let pub=tool.publications.find(x=>x.ref===ref&&x.platform===p);
   if(!pub){pub={ref,platform:p,parts:{chapters_state:null,widgets_state:null},code:0};tool.publications.push(pub);}
   Object.assign(pub,{final_presses:press,final_submit_attempted:press===null?null:press>0});}
  return r;
 }
 function look({job='job-a',platform=platforms[0],about='post',seen='present_private',m=minutes+15,minutes_after=15,fields_match=true}={}){
  return put('OBSERVATION',{job,platform,about,seen,by:site,client:'phone_app',minutes_after,fields_match,ref:labRef,words_sha256:'2'.repeat(64),shot_sha256:null},m);
 }
 function finish({cutoff=2880,closing=true}={}) {
  // All exports are synthetic; leave no files in a live journal or home.
  for(const d of [...new Set(rows.filter(r=>r.type==='STEP').map(r=>r.at.slice(0,10)))])
   if(!rows.some(r=>r.type==='SIGN_OFF'&&r.d.day===d))put('SIGN_OFF',{day:d,by:site,answer:'yes',records:rows.length,head_sha256:'0'.repeat(64),closing:false},Math.max(minutes,cutoff-1));
  const close=put('SIGN_OFF',{day:at(cutoff).slice(0,10),by:site,answer:'yes',records:rows.length,head_sha256:'0'.repeat(64),closing},cutoff);
  return rebuild({site,rows,tool,plan,seed,asOf:close.at});
 }
 return {site,rows,tool,plan,seed,put,step,look,finish,run};
}
export function rebuild(x){
 let prev='0'.repeat(64);
 for(const [i,r] of x.rows.entries()){r.seq=i+1;r.prev=prev;if(r.type==='SIGN_OFF'){r.d.records=i;r.d.head_sha256=prev;}prev=hash(canonical(r));}
 const counts={};x.rows.forEach(r=>counts[r.type]=(counts[r.type]??0)+1);
 const seal={day:x.rows.at(-1).at.slice(0,10),records:x.rows.length,head_sha256:prev,previous_sha256:'0'.repeat(64),counts,tools:{journal:'1',node:'22.0.0'}};
 const journal={schema:'round2-export/1',privacy:false,source:{records:x.rows.length,head_sha256:prev,chain:'sha256_without_lf',derived_records:false},
  counts,records:x.rows,seals:[seal],unsealed_records:0,crosscast_present:true,crosscast_chain_ok:true};
 const cc={...x.tool,present:true,chain_ok:x.tool.integrity.chain_ok,losses:x.tool.runs.map(r=>({run:r.run,dropped_events:r.dropped_events??null,sequence_gap:r.sequence_gap??null,chain_ok:r.chain_ok??x.tool.integrity.chain_ok,loss_complete:r.loss_complete??false}))};
 const comparison=reconcileData(x.rows,cc,{jobs:x.plan.runs,now:Date.parse(x.asOf)});
 return {site:x.site,journal,tool:x.tool,plan:x.plan,seed:x.seed,planText:canonical(x.plan)+'\n',asOf:x.asOf,reconcile:comparison};
}
export function privateSite(x) {
 x=structuredClone(x);x.seed=Buffer.from(x.seed);
 const key=v=>hash('synthetic-key:'+v);
 x.journal.privacy=true;x.journal.source.derived_records=true;x.journal.source.chain='hmac_sha256_source_commitments';
 x.journal.source.head_sha256=key(x.journal.source.head_sha256);
 const lastSeal=x.journal.seals.at(-1);
 x.journal.source.seal_head_sha256=key(hash(canonical(lastSeal)+'\n'));
 x.journal.seals=x.journal.seals.map(s=>({...s,source_sha256:key(hash(canonical(s)+'\n')),head_sha256:key(s.head_sha256),previous_sha256:key(s.previous_sha256)}));
 for(const r of x.journal.records){
  r.source_sha256=key(hash(canonical(r)));r.prev=key(r.prev);r.at=r.at.slice(0,10);
  if(r.d.now)r.d.now=r.d.now.slice(0,10);
  if(r.d.jobs)for(const j of r.d.jobs)if(j.due_at)j.due_at=j.due_at.slice(0,10);
  delete r.d.minutes_after;delete r.d.minutes_ago;
 }
 x.journal.crosscast=privateToolCopy(x.tool,key);
 x.journal=protectDigests(x.journal,key);
 x.reconcile.as_of=x.reconcile.as_of.slice(0,10);
 for(const c of x.reconcile.in_doubt){c.minutes=c.seconds===null?null:Math.ceil(c.seconds/60);delete c.seconds;}
 x.reconcile.time_policy='UTC_date_end; in_doubt_minutes_rounded_up';
 x.asOf=x.asOf.slice(0,10);x.tool=null;
 return x;
}
export const checklist={schema:'round2-checklist/1',cells:[1,2,3,4,5,6,7].flatMap(id=>['youtube','douyin'].map(platform=>({id,platform,reason:'synthetic fixture'}))).concat([{id:8,platform:'bilibili'},...['bilibili','x','instagram'].map(platform=>({id:1,platform})),{id:5,platform:'gate'}])};
export const bundle=(...sites)=>({schema:'round2-analysis-inputs/1',synthetic:true,sites,checklist});

// Only exported objects enter this module. No live-folder context is constructed.
import {createHash} from 'node:crypto';
import {canonical} from '../../scripts/round2/journal.mjs';
import {generate} from '../../scripts/round2/plan-runs.mjs';
import {reconcileData} from './reconcile-export.mjs';
export const hash=v=>createHash('sha256').update(v).digest('hex');
export const equal=(a,b)=>canonical(a)===canonical(b);
export const time=v=>typeof v==='string'?Date.parse(v):Number.NaN;
export const day=v=>v?.slice(0,10);
export function normalizeSite(input) {
 const {journal,plan,reconcile:comparison,site}=input,tool=journal?.privacy?journal.crosscast:input.tool;
 if(!['owner','tester'].includes(site)||journal?.schema!=='round2-export/1'||!Array.isArray(journal.records)||
    journal.records.some(r=>r.site!==site)||plan?.schema!=='round2-planned-runs/1'||plan.site!==site)
  throw new Error('invalid_site_exports');
 if(tool?.schema!=='crosscast-audit/1'||!tool.window?.since)throw new Error('windowed_tool_export_required');
 if(!journal.privacy&&tool.times!=='exact')throw new Error('owner_exact_tool_times_required');
 if(!Array.isArray(tool.runs)||!Array.isArray(tool.events)||!tool.integrity)throw new Error('invalid_tool_export');
 if(!journal.privacy){
  const since=time(tool.window.since),until=tool.window.until?time(tool.window.until):Infinity;
  if(!Number.isFinite(since)||!(until>since))throw new Error('invalid_export_window');
  if(tool.runs.some(c=>c.t_start!==null&&c.t_start!==undefined&&(!Number.isFinite(time(c.t_start))||time(c.t_start)<since||time(c.t_start)>=until)))throw new Error('command_outside_export_window');
 }

 if(comparison?.schema!=='round2-reconcile/1'||!comparison.as_of)throw new Error('fixed_comparison_cutoff_required');
 const privacy=journal.privacy===true,asOf=input.asOf;
 if(!asOf||(privacy?day(asOf)!==comparison.as_of:(!Number.isFinite(time(asOf))||time(asOf)!==time(comparison.as_of))))
  throw new Error('comparison_cutoff_mismatch');
 const records=journal.records,closing=records.findLast(r=>r.type==='SIGN_OFF'&&r.d.closing&&r.d.answer==='yes'&&r.d.by===site);
 if(!closing||(privacy?day(closing.at)!==day(asOf):time(closing.at)!==time(asOf)))throw new Error('closing_signoff_cutoff_required');
 if(records.some(r=>r.type==='STEP'&&r.seq>closing.seq))throw new Error('step_after_closing_signoff');
 const bytes=input.planText??canonical(plan)+'\n',seed=Buffer.from(input.seed??[]);
 if(seed.length!==32||hash(seed)!==plan.seed_sha256)throw new Error('seed_digest_mismatch');
 if(input.counts){
  const generated=generate(input.counts,site,seed,{...plan.generator,countsRaw:input.countsText??canonical(input.counts)+'\n'});
  if(!equal(generated.plan,plan))throw new Error('plan_reproduction_mismatch');
 }
 const seals=records.filter(r=>r.type==='PROTOCOL_SEAL');
 if(!seals.length)throw new Error('phase_seal_required');
 const practice=input.practicePlan,practiceText=input.practicePlanText??(practice?canonical(practice)+'\n':null);
 if(practice&&(practice.schema!=='round2-planned-runs/1'||practice.site!==site||!Array.isArray(practice.runs)||practice.runs.some(j=>plan.runs.some(x=>x.id===j.id))))throw new Error('invalid_practice_plan');
 for(const s of seals.filter(s=>s.d.plan_sha256)){
  const text=s.d.phase==='2a'?practiceText:bytes;
  // Legacy fixtures use their sole plan for phase 2a. A distinct missing
  // practice list is listed as unmeasured, never checked against phase 2b.
  if(s.d.phase==='2a'&&!text)continue;
  if(s.d.plan_sha256!==hash(text))throw new Error('sealed_plan_digest_mismatch');
 }
 if(seals.some(s=>s.d.phase==='2b'&&!s.d.plan_sha256))throw new Error('counted_plan_not_sealed');
 if(comparison.records!==records.length)throw new Error('comparison_record_count_mismatch');
 const errors=[],integrity={method:privacy?'attested_source_commitments':'recomputed_export_chain',exceptions:errors};
 let prev='0'.repeat(64);
 for(const [i,r] of records.entries()){
  if(r.seq!==i+1)errors.push({code:'record_sequence',seq:r.seq});
  if(privacy){if(i&&r.prev!==records[i-1].source_sha256)errors.push({code:'commitment_link',seq:r.seq});}
  else {if(r.prev!==prev)errors.push({code:'broken_chain',seq:r.seq});prev=hash(canonical(r));}
 }
 if((privacy?records.at(-1)?.source_sha256:prev)!==journal.source.head_sha256)errors.push({code:'export_head'});
 if(journal.source.records!==records.length)errors.push({code:'export_count'});
 let sealPrev=privacy?null:'0'.repeat(64),lastN=0;
 for(const s of journal.seals??[]){
  const head=privacy?records[s.records-1]?.source_sha256:s.records?hash(canonical(records[s.records-1])):'0'.repeat(64);
  const counts={};for(const r of records.slice(0,s.records))counts[r.type]=(counts[r.type]??0)+1;
  if(s.records<lastN||s.records>records.length||s.head_sha256!==head||!equal(s.counts,counts)||
      sealPrev!==null&&s.previous_sha256!==sealPrev)errors.push({code:'daily_seal',day:s.day});
  sealPrev=privacy?s.source_sha256:hash(canonical(s)+'\n');lastN=s.records;
 }
 if(privacy&&journal.source.seal_head_sha256!==sealPrev)errors.push({code:'seal_commitment_head'});
 if(journal.unsealed_records!==records.length-lastN)errors.push({code:'unsealed_count'});
 const jobs=[...plan.runs.map(j=>({...j,list_phase:'2b'})),...(practice?.runs??[]).map(j=>({...j,list_phase:'2a'})),...records.filter(r=>r.type==='REAL_RELEASE').map(r=>({id:r.d.job,...r.d}))];
 const cc={...tool,present:true,chain_ok:tool.integrity?.chain_ok,losses:tool.runs.map(r=>({run:r.run,dropped_events:r.dropped_events??null,sequence_gap:r.sequence_gap??null,chain_ok:r.chain_ok??tool.integrity?.chain_ok,loss_complete:r.loss_complete??false}))};
 let calculated=null;
 if(!privacy){
  calculated=reconcileData(records,cc,{jobs,now:time(asOf)});
  for(const key of ['in_doubt','look_checks','write_bindings','error_steps_unclassified','ungated_writes','gate_checks','publish_checks','observation_checks','run_checks','count_changed_without_press'])
   if(!equal(calculated[key],comparison[key]))errors.push({code:'reconcile_disagreement',field:key});
 }
 const result={...input,privacy,tool,cc,records,plan,jobs,seals,asOf,comparison:privacy?comparison:calculated,integrity,
  provenance:{plan_sha256:hash(bytes),seed_sha256:hash(seed),plan_reproduced:!!input.counts,practice_plan_sha256:practiceText?hash(practiceText):null,practice_plan_status:practice?'unsealed_list':'not_supplied',
   timing:privacy?'site_reconcile_attested_dates_only':'computed_from_exact_export_and_compared',
   tool_chain:'attested',source_chain:integrity.method}};
 result.phaseAt=r=>seals.findLast(s=>s.seq<=r.seq)?.d.phase??'unsealed';
 result.sealAt=r=>seals.findLast(s=>s.seq<=r.seq);
 return result;
}

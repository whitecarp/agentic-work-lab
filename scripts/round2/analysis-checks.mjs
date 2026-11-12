// Annex A 1.9: compute on exact local records, export results without clocks.
import {planRun} from '../operations-gate.mjs';
import {canonical} from './journal.mjs';
const exact=v=>typeof v==='string'&&/^\d{4}-\d\d-\d\dT\d\d:\d\d/.test(v)&&Number.isFinite(Date.parse(v));
const started=r=>r.d.started!==false&&r.d.exit!==127;
// Human work (7.3) includes every observation, not only the qualifying look
// selected as ground truth. Keep unclear separate, without losing its timing.
export function observationChecks(records,jobs=[]){
 return records.filter(r=>r.type==='OBSERVATION').map(r=>{
  const m=r.d.minutes_after;
  const timing=typeof m!=='number'||!Number.isFinite(m)||m<0?'unknown':
   m<10?'early':m<=60?'on_time':m<=1440?'late':'outside_window';
  const platforms=jobs.find(j=>j.id===r.d.job)?.platforms??
   [...new Set(records.filter(s=>s.type==='STEP'&&s.d.job===r.d.job).flatMap(s=>s.d.platforms))];
  return {seq:r.seq,job:r.d.job,platform:r.d.platform??(platforms.length===1?platforms[0]:null),
   about:r.d.about,status:r.d.seen==='unclear'?'unclear':timing,timing_status:timing};
 });
}
export function gateChecks(records){
 const decisions=records.filter(r=>r.type==='GATE_DECISION').map(r=>{
  let replay='not_replayable',computed=null;
  if(exact(r.d.now)&&Array.isArray(r.d.jobs)&&r.d.jobs.every(j=>
   ['due_at','completed','authorized'].every(k=>Object.hasOwn(j,k))&&exact(j.due_at))){
   try{computed=planRun(r.d);replay=canonical(computed)===canonical({decision:r.d.decision,reason:r.d.reason,due:r.d.due})?'agree':'disagree';}
   catch{replay='threw';}
  }
  return {seq:r.seq,replay,computed};
 });
 return {decisions,agree:decisions.filter(r=>r.replay==='agree').map(r=>r.seq),
  disagree:decisions.filter(r=>['disagree','threw'].includes(r.replay)).map(r=>r.seq),
  unmeasured:decisions.filter(r=>r.replay==='not_replayable').map(r=>r.seq)};
}
export function publishChecks(records,jobs){
 const rows=[],seen=new Set();
 for(const r of records.filter(r=>r.type==='STEP'&&r.d.role==='publish'&&started(r))){
  if(jobs.find(j=>j.id===r.d.job)?.run_class!=='test_post')continue;
  for(const platform of r.d.platforms){
   const id=JSON.stringify([r.d.job,platform]);if(seen.has(id))continue;seen.add(id);
   rows.push({r,platform,start:exact(r.at)&&Number.isFinite(r.d.ms)&&r.d.ms>=0?Date.parse(r.at)-r.d.ms:null});
  }
 }
 return rows.map(({r,platform,start},i)=>{
  const earlier=rows.slice(0,i).filter(x=>x.platform===platform);
  const unknown=start===null||earlier.some(x=>x.start===null||x.start>start);
  const prior=earlier.filter(x=>x.start!==null).map(x=>x.start);
  return {step:r.seq,job:r.d.job,platform,
   daily:unknown?null:prior.filter(t=>start-t<86400000).length>=4,
   spacing:unknown?null:prior.some(t=>start-t<1200000),
   reason:unknown?'start_time_missing_or_reversed':null};
 });
}

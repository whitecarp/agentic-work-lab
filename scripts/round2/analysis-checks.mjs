// Annex A 1.11: compute on exact local records, export results without clocks.
import {publishStartedOn} from './answers.mjs';
import {safetyStopOn,validRestartGrant} from './policy.mjs';
import {planRun} from '../operations-gate.mjs';
import {canonical} from './journal.mjs';
import {lookTiming,lookStep,secondLookDue} from './looks.mjs';
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
  const platform=r.d.platform??(platforms.length===1?platforms[0]:null),look={...r,d:{...r.d,platform}};
  const check=lookTiming(look,lookStep(records.filter(x=>x.seq<r.seq),r.d.job,platform,r.d.about),records);
  const status=check.status==='second_look'?'second_look':timing;
  return {seq:r.seq,job:r.d.job,platform,about:r.d.about,status:r.d.seen==='unclear'?'unclear':status,timing_status:status};
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
  if(!['test_post','real_release','real_slot'].includes(jobs.find(j=>j.id===r.d.job)?.run_class))continue;
  for(const platform of r.d.platforms){
   if(!publishStartedOn(r,platform))continue;
   const id=JSON.stringify([r.d.job,platform]);if(seen.has(id))continue;seen.add(id);
   rows.push({r,platform,start:exact(r.at)&&Number.isFinite(r.d.ms)&&r.d.ms>=0?Date.parse(r.at)-r.d.ms:null});
  }
 }
 return rows.map(({r,platform,start},i)=>{
  const earlier=rows.slice(0,i).filter(x=>x.platform===platform);
  const unknown=start===null||earlier.some(x=>x.start===null||x.start>start);
  const prior=earlier.filter(x=>x.start!==null).map(x=>x.start);
  const test=jobs.find(j=>j.id===r.d.job)?.run_class==='test_post';
  const priorTests=earlier.filter(x=>jobs.find(j=>j.id===x.r.d.job)?.run_class==='test_post');
  return {step:r.seq,job:r.d.job,platform,
   daily:unknown?null:test&&priorTests.filter(x=>start-x.start<86400000).length>=4,
   spacing:unknown?null:test&&prior.some(t=>start-t<1200000),
   reason:unknown?'start_time_missing_or_reversed':null};
 });
}

// Exact local chronology exported as record numbers/booleans, never clocks.
export function runChecks(records,jobs,lookChecks){
 return jobs.map(j=>{
  const steps=records.filter(r=>r.type==='STEP'&&r.d.job===j.id&&started(r));
  const related=records.filter(r=>r.d.job===j.id&&['STEP','GO_AHEAD','OBSERVATION','JOB_END'].includes(r.type));
  const signoffs=[...new Set(related.map(r=>r.at.slice(0,10)))].map(day=>{
   const sign=records.findLast(r=>r.type==='SIGN_OFF'&&r.d.day===day);
   const latest=related.filter(r=>r.at.slice(0,10)===day).at(-1);
   return {day,seq:sign?.seq??null,passed:!!sign&&sign.d.answer==='yes'&&sign.seq>latest.seq,
    late:!!sign&&Date.parse(sign.at)>=Date.parse(day)+2*86400000};
  });
  const upload=steps.filter(r=>['publish','repeat_same','resume'].includes(r.d.role));
  const interference=(j.platforms??[]).flatMap(p=>{
   const own=upload.filter(r=>r.d.platforms.includes(p));if(!own.length)return [];
   const look=lookChecks.find(c=>c.job===j.id&&c.platform===p&&c.about==='post');
   const end=records.find(r=>r.seq===look?.observation);
   const lo=Date.parse(own[0].at)-(own[0].d.ms??0),hi=end?Date.parse(end.at):Date.parse(own.at(-1).at)+(look?86400000:0);
   return records.filter(r=>r.type==='NOTE'&&['outage','platform_warning','human_check'].includes(r.d.code)&&r.d.platform===p&&Date.parse(r.at)>=lo&&Date.parse(r.at)<=hi).map(r=>r.seq);
  });
  const safety_restarts=(j.platforms??[]).flatMap(p=>{
   const first=steps.find(r=>r.d.role==='publish'&&publishStartedOn(r,p));
   if(!first||j.run_class!=='test_post')return [];
   const stop=records.findLast(r=>r.seq<first.seq&&safetyStopOn(r,p));
   if(!stop)return [];
   const threshold=Date.parse(stop.at)+172800000;
   const start=Date.parse(first.at)-(first.d.ms??0);
   const valid=records.some(r=>r.seq<first.seq&&validRestartGrant(r,j.id,threshold,start));
   return [{platform:p,step:first.seq,stop:stop.seq,valid}];
  });
  const end=records.find(r=>r.type==='JOB_END'&&r.d.job===j.id),beforeEnd=end?records.filter(r=>r.seq<end.seq):[];
  const nothing_posted_before_second_look=(j.platforms??[]).filter(platform=>{
   if(!end?.d.platform_settlements?.some(x=>x.platform===platform&&x.how==='nothing_posted'))return false;
   const step=lookStep(beforeEnd,j.id,platform,'post');
   const due=secondLookDue({seq:end.seq,d:{job:j.id,platform,about:'post'}},step,beforeEnd);
   return due&&!beforeEnd.some(r=>r.type==='OBSERVATION'&&r.d.job===j.id&&(r.d.platform??platform)===platform&&r.d.about==='post'&&lookTiming(r,step,beforeEnd).status==='second_look'&&lookTiming(r,step,beforeEnd).counts);
  });
  const outside=records.filter(r=>r.type==='NOTE'&&r.d.code==='outside_upload'&&j.platforms?.includes(r.d.platform));
  const lo=upload.length?Date.parse(upload[0].at)-(upload[0].d.ms??0)-1800000:null;
  const hi=upload.length?Date.parse(upload.at(-1).at)+1800000:null;
  const outside_upload=outside.some(r=>r.d.minutes_ago==null)?null:upload.length&&outside.some(r=>{const t=Date.parse(r.at)-r.d.minutes_ago*60000;return t>=lo&&t<=hi;});
  return {job:j.id,signoffs,interference:[...new Set(interference)],safety_restarts,nothing_posted_before_second_look,outside_upload};
 });
}

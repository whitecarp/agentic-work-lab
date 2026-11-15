import {operation,uncertainState,confirmedAnswer,confirmed} from './answers.mjs';
import {commandSnapshots,wholeCommandNoPress} from './press-evidence.mjs';

// Evaluate only evidence available when the first look was recorded.
function savedPostOwesLook(steps, first, platform, cc) {
 const integrity=cc?.integrity;
 const healthy=cc?.chain_ok===true && integrity?.salt_ok!==false &&
  integrity?.loss_records_complete===true && integrity.loss_folder_unreadable===false &&
  integrity.unreadable_loss_records===0 && integrity.unplaced_dropped_events===0;
 let positive=false, damaged=false, unknown=false;
 let openingRefs=[];
 for(const step of steps){
  const d=step.d, a=d.answers?.find(x=>x.platform===platform);
  const refs=a?.refs??d.refs??[];
  if(openingRefs.length&&refs.length&&!refs.some(r=>openingRefs.includes(r))) continue;
  const matches=d.awl_run_id?cc?.runs?.filter(c=>c.awl_run_id===d.awl_run_id):[];
  const cmd=matches?.length===1?matches[0]:null;
  const events=cmd?(cc.events??[]).filter(e=>e.run===cmd.run&&Date.parse(e.at)<=Date.parse(first.at)):[];
  const readable=healthy&&cmd&&cmd.chain_ok!==false&&cmd.sequence_gap===false&&cmd.dropped_events===0&&
   events.some(e=>e.type==='command_start')&&!events.some(e=>e.dropped_before>0)&&
   (cmd.ended ? events.some(e=>e.type==='command_end')&&cmd.exit_code===d.exit : d.answered===false);
  const snapshots=readable?commandSnapshots(events,platform).filter(x=>!['edit','deletion'].includes(x.kind)):[];
  const readBack=d.role==='verify'&&confirmedAnswer(a??{},'post') ||
   events.some(e=>e.type==='stage'&&(!e.platform||e.platform===platform)&&
    ['reading back','reading the page','waiting for platform confirmation'].includes(e.stage)&&
    events.some(later=>later.seq>e.seq&&later.type==='transition'&&later.platform===platform&&
     !['edit','deletion'].includes(later.kind)&&later.phase!=='submitting'&&
     [later.state,later.receipt_state].includes('SUBMISSION_VERIFIED')));
  if(readable&&readBack){positive=false;damaged=false;unknown=false;openingRefs=[];continue;}
  if(d.role==='verify')continue;
  if(a&&confirmedAnswer(a,'post')&&!positive&&!unknown&&!damaged)continue;
  if(a?.state==='NOT_STARTED'||a?.state==='NEEDS_A_PERSON'||d.result==='busy')continue;
  const marker=snapshots.some(x=>x.final_presses>0||x.final_submit_attempted===true||
   uncertainState({state:x.receipt_state??x.state,previous_state:x.previous_state},'post'));
  if(marker||a?.press_flag===true||a&&uncertainState(a,'post'))positive=true;
  // A later zero for this attempt resolves an interrupted caller only when
  // its opening export survived. A refusal cannot erase a positive marker.
  const zero=platform!=='bilibili'&&events.some(e=>e.type!=='command_start'&&
   commandSnapshots([e],platform).some(x=>x!==e.before&&!['edit','deletion'].includes(x.kind)&&
    (x.final_presses===0||x.final_submit_attempted===false)));
  if(!readable)damaged=true;
  else if(zero)unknown=false;
  else if(!a||a.answered===false||['in_doubt','unknown'].includes(d.result))unknown=true;
  if(!openingRefs.length&&(positive||unknown||damaged))openingRefs=refs;
 }
 return positive||unknown||damaged;
}
export function lookStep(records,job,platform,about){
 return records.findLast(r=>r.d.job===job&&(about==='delete'&&job&&platform&&
  r.type==='NOTE'&&r.d.code==='manual_step'&&r.d.platform===platform ||
  r.type==='STEP'&&r.d.platforms.includes(platform)&&
  r.d.started!==false&&r.d.exit!==127&&r.d.class==='write'&&operation(r)===about&&
  ['publish','repeat_same','changed_plan','resume','edit','delete'].includes(r.d.role)));
}

// Figure 4.3 applies the same saved, single-platform read-back test at entry,
// daily reconciliation and export analysis, including older answer-less STEPs.
export function settlingRead(r,opening,platform,about,cc) {
 const answerFor=(step,p)=>step.d.answers?.find(a=>a.platform===p);
 if(r.type!=='STEP'||r.d.platforms.length!==1||r.d.platforms[0]!==platform)return false;
 const openingRefs=answerFor(opening,platform)?.refs??opening.d.refs??[],closingRefs=answerFor(r,platform)?.refs??r.d.refs??[];
 if(openingRefs.length&&closingRefs.length&&!openingRefs.some(ref=>closingRefs.includes(ref)))return false;
 if(r.d.role!==({post:'verify',edit:'edit',delete:'delete_verify'})[about])return false;
 if(about==='edit'&&r.d.class!=='read')return false;
 if(answerFor(r,platform))return confirmedAnswer(answerFor(r,platform),about);
 if(r.d.result!=='ok')return false;
 if(Object.hasOwn(r.d,'confirmed_state'))return r.d.confirmed_state===confirmed[about];
 // Old journals lack this field. Require an affirmative state in the
 // paired command or its final snapshot before treating a read as verified.
 const matches=cc?.runs?.filter(x=>x.awl_run_id&&x.awl_run_id===r.d.awl_run_id)??[];
 const cmd=matches.length===1?matches[0]:null;
 if(!healthyExport(cc)||!cmd||cmd.ended!==true||cmd.exit_code!==0||cmd.dropped_events!==0||cmd.sequence_gap!==false)return false;
 return cc.events.some(e=>e.run===cmd.run&&(!e.platform||e.platform===platform)&&
   [e.state,e.after?.state].includes(confirmed[about]));
}

function healthyExport(cc) {
 const i=cc?.integrity;
 return cc?.chain_ok===true && i?.salt_ok!==false && i?.loss_records_complete===true &&
  i.loss_folder_unreadable===false && i.unreadable_loss_records===0 &&
  (i.unplaced_dropped_events??0)===0 && cc.runs.every(c=>c.chain_ok!==false);
}

function postMayPress(step,platform,records,cc) {
 // Without the whole matched command, a later write can reopen the case.
 const id=step.d.awl_run_id;
 if(!id||!healthyExport(cc)||records.filter(r=>r.type==='STEP'&&r.d.awl_run_id===id).length!==1)return true;
 const runs=cc.runs.filter(c=>c.awl_run_id===id);
 if(runs.length!==1)return true;
 const c=runs[0];
 if(c.sequence_gap!==false||c.dropped_events!==0)return true;
 const events=cc.events.filter(e=>e.run===c.run);
 if(!records.some(r=>r.run===step.run&&r.seq<step.seq&&r.type==='GATE_DECISION'&&
   r.d.decision==='plan_only'&&r.d.due?.includes(step.d.job))||
   records.some(r=>r.run===step.run&&r.seq<step.seq&&r.type==='RUN_END'))return true;
 return !wholeCommandNoPress(events,c,step,platform,'post',cc.events);
}

export function secondLookAnchor(look,step,records,cc){
 if(!step||look.d.about!=='post')return null;
 let first,anchor;
 for(const r of records){
  if(r.type!=='OBSERVATION'||r.seq>=look.seq||r.d.job!==look.d.job||r.d.about!=='post'||
     (r.d.platform??look.d.platform)!==look.d.platform)continue;
  const prior=records.filter(x=>x.seq<r.seq);
  const follows=lookStep(prior,look.d.job,look.d.platform,'post')??(step.seq<r.seq?step:null);
  if(follows&&lookTiming(r,follows,[]).counts){first=r;anchor=follows;break;}
 }
 if(first?.d.seen!=='absent')return null;
 // An unresolved case survives busy/refused/not-started helper answers.
 const steps=records.filter(r=>r.type==='STEP'&&r.d.job===look.d.job&&r.seq<=first.seq&&
  r.d.started!==false&&r.d.exit!==127&&r.d.platforms.includes(look.d.platform)&&
  ['publish','repeat_same','changed_plan','resume','verify'].includes(r.d.role));
 const platform=look.d.platform;
 if(!savedPostOwesLook(steps,first,platform,cc))return null;
 const opening=steps.find(r=>r.d.role!=='verify'&&
  (r.d.answered===false||['in_doubt','unknown'].includes(r.d.result)||
   uncertainState(r.d.answers?.find(a=>a.platform===platform)??{},'post')))??anchor;
 let settled=false;
 for(const r of records){
  if(r.seq<=first.seq||r.seq>=look.seq||r.d.job!==look.d.job)continue;
  if(r.type==='STEP'&&r.d.platforms.includes(platform)&&r.d.started!==false&&r.d.exit!==127){
   if(r.d.class==='write'&&['publish','repeat_same','changed_plan','resume'].includes(r.d.role)&&
      postMayPress(r,platform,records.filter(x=>x.seq<look.seq),cc)){settled=false;}
   else if(settlingRead(r,opening,platform,'post',cc))settled=true;
  }
 }
 return settled?null:anchor;
}
export function secondLookDue(look,step,records,cc){
 return !!secondLookAnchor(look,step,records,cc);
}
export function lookTiming(look,step,records=[],cutoff=Infinity,cc){
 const n=look.d.minutes_after;
 const status=!Number.isFinite(n)||n<0?'unknown':n<10?'early':n<=60?'on_time':n<=1440?'late':'outside_window';
 const second=n>=1200&&n<=2160?secondLookAnchor(look,step,records,cc):null;
 const anchor=second??step;
 const premature=!!anchor&&Date.parse(look.at)-Date.parse(anchor.at)<n*60000;
 return {status:look.d.seen==='unclear'?'unclear':second?'second_look':status,
  counts:!!step&&!premature&&look.d.seen!=='unclear'&&Date.parse(look.at)<=cutoff&&(!!second||n>=10&&n<=1440),
  recorded_before_claimed_minute:premature};
}
export function countsLook(look,step,cutoff=Infinity,records=[],cc){return lookTiming(look,step,records,cutoff,cc).counts;}

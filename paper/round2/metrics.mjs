// Annex A 1.8. Vocabulary and outcome priority are printed in every summary.
// STEP.answers are per platform. STEP.result is used only for a missing answer.
// Lab refs never join CrossCast refs: joins use command awl_run_id + platform.
import {publishChecks} from '../../scripts/round2/analysis-checks.mjs';
import {settled,nothingSent,uncertainState,operation,pressRoles,STATES} from '../../scripts/round2/answers.mjs';
import {commandClass} from '../../scripts/round2/crosscast.mjs';
import {grantScope} from '../../scripts/round2/policy.mjs';
import {normalizeSite,time,day} from './inputs.mjs';
import {describe,upper95,tally,share} from './statistics.mjs';
export const OUTCOMES={
 version:'Annex A 1.8',states:STATES,
 priority:['confirmed','nothing_sent','in_doubt_state','proven_no_press','possible_case'],
 confirmed:{post:'SUBMISSION_VERIFIED',edit:'EDIT_VERIFIED',delete:'DELETED_VERIFIED'},
 skipped:'SKIPPED_EXISTING uses previous_state; verified_not_saved and not_saved never confirm',
 nothing_sent:'NOT_STARTED, NEEDS_A_PERSON; post false flag except in-doubt states; edit pre-save states',
 refused:'exit 2 plus intact matched no-contact evidence, not exit 2 alone',
 stopped:'nothing sent with code stopped or stop_flag; STEP_REFUSED starts no child',
 failed:'started command lacking settlement or no-press proof remains a possible case',
 zero_denominators:null,presses:'markers, not clicks; Bilibili excluded from marker denominators',
 temporal_unknown:'null with reason, never reconstructed from redacted dates'
};
const present=v=>['present_once','present_private'].includes(v);
const around=['end_command_after_press','end_tree_after_press','readback_timeout','between_save_and_press'];
const killed=['end_command_after_press','end_tree_after_press','browser_lost_during_upload','end_browser_after_edit_save'];
const uniq=a=>[...new Set(a)];
const countedLook=c=>['on_time','late'].includes(c?.status);
const started=s=>s.d.started!==false&&s.d.exit!==127;
const key=a=>JSON.stringify(a);
const codeStamp=(s,c)=>c? s.tool.builds?.[c.build]??null:null;
function commandEvidence(s,step,platform) {
 const commands=s.tool.runs.filter(c=>c.awl_run_id&&c.awl_run_id===step.d.awl_run_id);
 const command=commands.length===1?commands[0]:null;
 const events=command?s.tool.events.filter(e=>e.run===command.run):[];
 const own=e=>!e.platform||e.platform===platform;
 const first=events.find(e=>e.type==='command_start'),end=events.findLast(e=>e.type==='command_end');
 const loss=Math.max(command?.dropped_events??0,events.reduce((n,e)=>n+(e.dropped_before??0),0));
 const integrity=s.tool.integrity??{};
 const interrupted=s.records.some(r=>r.type==='FAULT_RESULT'&&r.d.job===step.d.job&&r.run===step.run&&
   r.d.role===step.d.role&&killed.includes(r.d.kind)&&(r.d.fired||r.d.failed)&&
   s.records.find(x=>x.type==='STEP'&&x.seq>r.seq&&x.run===r.run&&x.d.job===r.d.job&&x.d.role===r.d.role)?.seq===step.seq);
 const intact=!!command&&command.ended&&command.exit_code===step.d.exit&&first&&end&&command.chain_ok!==false&&
   integrity.chain_ok===true&&command.sequence_gap===false&&command.dropped_events!=null&&!loss&&
   integrity.loss_records_complete===true&&integrity.loss_folder_unreadable===false&&
   integrity.unreadable_loss_records===0&&integrity.unplaced_dropped_events===0&&!interrupted;
 const middle=events.filter(e=>own(e)&&!['command_start','command_end'].includes(e.type));
 const a=first?.before,b=end?.after;
 const noContact=intact&&middle.every(e=>e.type==='span')&&
   ((!a&&!b)||(a&&b&&a.state===b.state&&a.final_presses!=null&&a.final_presses===b.final_presses&&a.delete_attempted===b.delete_attempted));
 const hasPress=middle.some(e=>e.operation==='press'||e.platform_writes>0||e.stage==='pressing');
 const same=intact&&a&&b&&a.final_presses!=null&&a.final_presses===b.final_presses&&
   a.delete_attempted===b.delete_attempted&&!hasPress&&!(b.final_submit_attempted===true&&a.final_submit_attempted!==true)&&
   !middle.some(e=>e.type==='transition'&&((e.final_presses!=null&&e.final_presses!==a.final_presses)||
    (e.delete_attempted!=null&&e.delete_attempted!==a.delete_attempted)));
 const snapshot=[...events.filter(e=>own(e)&&e.type==='transition'),end?.platforms?.[platform],end?.after?.platform===platform?end.after:null].filter(Boolean);
 const refs=uniq([end?.platforms?.[platform]?.ref,...events.filter(e=>e.platform===platform).map(e=>e.ref),
  first?.before?.platform===platform?first.before.ref:null,end?.after?.platform===platform?end.after.ref:null].filter(Boolean));
 return {command,events,snapshot,refs,intact:!!intact,noContact:!!noContact,noPress:!!(noContact||same),hasPress,
  dropped_reported:command?.dropped_events??null,dropped_from_events:events.reduce((n,e)=>n+(e.dropped_before??0),0),
  loss,interrupted,stamp:codeStamp(s,command)};
}
function answerOutcome(a,step,e) {
 if(a&&settled(a,operation(step)))return 'confirmed';
 if(a&&nothingSent(a,operation(step)))return ['stopped','stop_flag'].includes(a.code)?'stopped':'nothing_sent';
 if(a&&uncertainState(a,operation(step)))return 'in_doubt';
 if(e.noPress)return step.d.exit===2?'refused':'nothing_sent';
 if(step.d.exit===2&&!e.intact)return 'possible_case';
 return 'in_doubt';
}
function markers(detail) {
 if(detail.platform==='bilibili'||!detail.evidence.intact)return null;
 const nums=detail.evidence.snapshot.filter(x=>x.kind!=='edit'&&x.kind!=='deletion').map(x=>x.final_presses).filter(Number.isFinite);
 return nums.length?Math.max(...nums):null;
}
function sameEditTarget(d) {
 const start=d.evidence.events.find(e=>e.type==='command_start')?.before;
 const end=d.evidence.events.findLast(e=>e.type==='command_end')?.after;
 return start?.ref&&end?.ref?start.ref===end.ref:null;
}
function draftBeforeSecond(details) {
 // A final receipt's draft flag alone is not evidence before the second marker.
 let draft=false;
 for(const d of details){
  for(const e of d.evidence.events.filter(e=>!e.platform||e.platform===d.platform)){
   if(e.type==='command_start'&&e.before?.final_presses>1&&!draft)return false;
   if(e.final_presses>1&&!draft)return false;
   if(e.still_a_draft===true)draft=true;
   if(e.type==='command_end'&&e.after?.final_presses>1&&!draft)return false;
  }
 }
 return draft;
}
function modelPeriod(s,r){
 const phase=s.phaseAt(r),seal=s.sealAt(r);
 const n=s.records.filter(x=>x.seq>(seal?.seq??0)&&x.seq<r.seq&&x.type==='NOTE'&&x.d.code==='model_changed'&&x.d.agent===r.slot).length+1;
 return {phase,agent:r.slot,period:r.slot+'-'+n};
}
function siteModel(s,expectedGateHash) {
 const records=s.records,steps=records.filter(r=>r.type==='STEP'),issues=[];
 const add=(code,r,detail=null)=>issues.push({code,seq:r?.seq??null,job:r?.d.job??null,detail});
 const phaseFor=r=>s.phaseAt(r),admits=records.filter(r=>r.type==='GATE_DECISION'&&r.d.decision==='plan_only');
 const admissions=step=>admits.some(g=>g.run===step.run&&g.seq<step.seq&&g.d.due.includes(step.d.job))&&
  !records.some(e=>e.type==='RUN_END'&&e.run===step.run&&e.seq<step.seq);
 let operator=s.site;const spent=[],approval=new Map(),gates=[];
 for(const r of records){
  if(r.type==='HANDOFF'){if(r.d.from!==operator)add('handoff_from_mismatch',r);operator=r.d.to;}
  if(r.type==='GATE_DECISION'){
   if(r.d.owner!==operator)add('gate_operator_mismatch',r);
   if(!(Array.isArray(expectedGateHash)?expectedGateHash:[expectedGateHash]).includes(r.d.gate_sha256))add('gate_digest_mismatch',r);
   const checks=s.comparison.gate_checks?.decisions?.filter(c=>c.seq===r.seq)??[];
   const check=checks.length===1&&['agree','disagree','threw','not_replayable'].includes(checks[0].replay)?checks[0]:null;
   const status=check?.replay??'not_replayable',computed=check?.computed??null;
   if(['disagree','threw'].includes(status))add('gate_replay_disagreement',r);
   gates.push({seq:r.seq,phase:phaseFor(r),decision:r.d.decision,reason:r.d.reason,launcher:r.d.launcher,replay:status,computed,
    jobs:r.d.jobs,run:r.run,slot_id:r.d.slot_id,source:s.provenance.timing});
  }
  if(r.type!=='STEP')continue;
  if(r.slot!==operator)add('step_wrong_operator',r);
  if(!admissions(r)&&r.d.class==='write')add('write_without_admission',r);
  if(r.d.class==='write'){
   const before=records.filter(x=>x.seq<r.seq),scope=grantScope(before,r.d.job,'write',spent,r.d.role,r.d.platforms);
   approval.set(r.seq,scope);
   const job=s.jobs.find(j=>j.id===r.d.job);
   if(!scope)add('write_without_approval',r);
   if(r.d.role==='delete'&&scope!=='one_delete')add('delete_without_own_approval',r);
   if(['real_slot','real_release'].includes(job?.run_class)&&scope!=='one_write')add('release_without_own_approval',r);
   if(s.site==='tester'&&scope==='listed_test_posts')add('tester_list_approval',r);
   spent.push({job:r.d.job,class:'write',scope,platforms:r.d.platforms});
  }
  if(records.some(x=>x.type==='JOB_END'&&x.d.job===r.d.job&&x.seq<r.seq))add('step_after_job_end',r);
 }
 const details=steps.flatMap(step=>step.d.platforms.map(platform=>{
  const evidence=commandEvidence(s,step,platform),answer=step.d.answers?.find(a=>a.platform===platform);
  return {step,platform,answer,evidence,outcome:answerOutcome(answer,step,evidence)};
 }));
 const lookup=(job,platform,about)=>s.comparison.look_checks?.find(c=>c.job===job&&c.platform===platform&&c.about===about);
 const looks=(job,platform,about)=>{
  const c=lookup(job,platform,about),r=records.find(r=>r.seq===c?.observation);
  return {status:c?.status??'not_owed',seen:countedLook(c)?r?.d.seen??null:null,record:r??null,source:s.provenance.timing,
   early:c?.early_observations??[],premature:c?.recorded_before_claimed_minute??[]};
 };
 const jobs=s.jobs.map(job=>{
  const own=steps.filter(r=>r.d.job===job.id),first=own[0],last=own.at(-1),ds=details.filter(d=>d.step.d.job===job.id);
  if(uniq(own.map(phaseFor)).length>1)throw new Error('job_crosses_phase_boundary');
  const phase=first?phaseFor(first):'2b',seal=first?s.sealAt(first):s.seals.findLast(x=>x.d.phase==='2b');
  const breaks=issues.filter(i=>i.job===job.id);
  const obligations=s.comparison.look_checks?.filter(c=>c.job===job.id)??[];
  const violations=records.filter(r=>r.type==='VIOLATION'&&(r.d.job===job.id||r.d.job===null&&
   first&&r.seq>=first.seq&&r.seq<=last.seq));
  const commandDetails=own.filter(started).filter(r=>r.d.tool==='vpub').map(r=>details.find(d=>d.step===r)??{evidence:commandEvidence(s,r,null)});
  let stampOK=commandDetails.length?true:null;
  for(const d of commandDetails){const stamp=d.evidence.stamp;
   if(!stamp||!seal||stamp.commit!==seal.d.crosscast_commit)stampOK=false;
   else if(seal.d.crosscast_digest&&stamp.source_digest!==seal.d.crosscast_digest)stampOK=false;
  }
  if(stampOK===false&&first)add('code_stamp_mismatch',first);
  const signoffOK=own.every(r=>records.some(z=>z.type==='SIGN_OFF'&&z.d.day===day(r.at)&&z.d.answer==='yes'&&z.seq>r.seq&&z.d.records>=r.seq));
  const interference=first?records.filter(r=>r.type==='NOTE'&&['outage','platform_warning','human_check'].includes(r.d.code)&&
   job.platforms.includes(r.d.platform)&&r.seq>=first.seq&&r.seq<=last.seq):[];
  const faultRows=(job.faults??[]).map(f=>{
   const arms=records.filter(r=>r.type==='FAULT'&&r.d.job===job.id&&r.d.kind===f.kind);
   const out=records.filter(r=>r.type==='FAULT_RESULT'&&r.d.job===job.id&&r.d.kind===f.kind).at(-1);
   const state=out?.d.failed||out?.d.fired===null?'unknown':out?.d.fired===true?'fired':arms.length?'did_not_fire':'not_armed';
   return {kind:f.kind,role:f.role,planned:true,armed:arms.length,state,reason:out?.d.reason??null,
    listed_ms:f.mechanism==='tool_switch'?null:f.delay_ms,requested_ms:f.mechanism==='tool_switch'?null:out?.d.action_delay_ms??null,
    gone_ms:f.mechanism==='tool_switch'?null:out?.d.gone_delay_ms??null,
    request_recorded:out?.d.action_delay_ms!=null,seq:out?.seq??null};
  });
  // Shakedown plans may not carry a fault. Include its observed arm without inventing a planned try.
  for(const r of records.filter(r=>r.type==='FAULT'&&r.d.job===job.id&&!faultRows.some(f=>f.kind===r.d.kind))){
   const out=records.findLast(x=>x.type==='FAULT_RESULT'&&x.d.job===job.id&&x.d.kind===r.d.kind);
   faultRows.push({kind:r.d.kind,role:r.d.role,planned:false,armed:1,state:out?.d.failed||out?.d.fired==null?'unknown':out.d.fired?'fired':'did_not_fire',
    reason:out?.d.reason??null,listed_ms:r.d.mechanism==='tool_switch'?null:r.d.delay_ms,requested_ms:out?.d.action_delay_ms??null,gone_ms:out?.d.gone_delay_ms??null,seq:out?.seq??null});
  }
  const upload=own.filter(r=>started(r)&&['publish','repeat_same','resume'].includes(r.d.role));
  let outside=false;
  const outsideNotes=records.filter(r=>r.type==='NOTE'&&r.d.code==='outside_upload'&&job.platforms.includes(r.d.platform));
  if(upload.length&&outsideNotes.length){
   if(s.privacy)outside=null;
   else{const lo=time(upload[0].at)-(upload[0].d.ms??0)-1800000,hi=time(upload.at(-1).at)+1800000;
    outside=outsideNotes.some(r=>r.d.minutes_ago==null||time(r.at)-r.d.minutes_ago*60000>=lo&&time(r.at)-r.d.minutes_ago*60000<=hi);}
  }
  const rules={
   '1_counted_phase':phase==='2b','2_frozen_commands':stampOK,
   '3_admitted_intact':s.integrity.exceptions.length===0&&!breaks.some(i=>i.code==='write_without_admission'),
   '4_approved':!breaks.some(i=>i.code.includes('approval')),
   '5_looks':obligations.every(countedLook),'6_no_voiding_violation':violations.length===0,
   '7_signoffs':signoffOK,'8_no_interruption':interference.length===0,'9_fault_fired':faultRows.every(f=>!f.planned||f.state==='fired')
  };
  // A drill/refusal with no child can still be a recorded, valid run.
  if(!commandDetails.length&&own.every(r=>!started(r)||r.d.tool==='node')&&job.run_class==='drill')rules['2_frozen_commands']=true;
  if(!own.length&&records.some(r=>r.type==='STEP_REFUSED'&&r.d.job===job.id))rules['2_frozen_commands']=true;
  const failed=Object.keys(rules).filter(k=>rules[k]===false),unknown=Object.keys(rules).filter(k=>rules[k]===null);
  const end=records.findLast(r=>r.type==='JOB_END'&&r.d.job===job.id),endProblems=[];
  if(end){
   if(end.d.reason==='abandoned'){if(!['owner','tester'].includes(end.d.by)||!end.d.words_sha256)endProblems.push('abandoned_without_owner_words');}
   else if(job.run_class==='test_post'){
    if(end.d.reason!=='done')endProblems.push('test_post_not_done');
    for(const platform of job.platforms){
     const settlement=end.d.platform_settlements?.find(p=>p.platform===platform),ownD=ds.filter(d=>d.platform===platform&&d.step.seq<end.seq);
     const look=looks(job.id,platform,'post'),delLook=looks(job.id,platform,'delete');
     const valid=settlement?.how==='delete_confirmed'?ownD.some(d=>operation(d.step)==='delete'&&d.outcome==='confirmed'):
      settlement?.how==='nothing_posted'?look.seen==='absent'&&!ownD.some(d=>d.step.d.role==='delete'):
      settlement?.how==='removed_by_hand'?records.some(r=>r.type==='NOTE'&&r.d.code==='manual_step'&&r.d.job===job.id&&r.d.platform===platform&&r.seq<end.seq):
      settlement?.how==='gone_by_person'?delLook.seen==='absent':false;
     if(!valid)endProblems.push('platform_not_settled:'+platform);
    }
   }else if(['real_slot','real_release'].includes(job.run_class)){
    for(const p of job.platforms){
     const pub=ds.find(d=>d.platform===p&&d.step.d.role==='publish');
     const verifies=ds.filter(d=>d.platform===p&&d.step.d.role==='verify'&&d.step.seq<end.seq&&started(d.step));
     if(!pub||verifies.length<3||!present(looks(job.id,p,'post').seen))endProblems.push('real_release_sequence:'+p);
     if(pub&&!s.privacy&&!verifies.some(d=>time(d.step.at)-time(pub.step.at)>=86400000))endProblems.push('real_release_24h:'+p);
     if(p==='bilibili'&&!ds.some(d=>d.step.d.role==='delete_dry_run'&&d.step.d.exit!==0&&d.evidence.noContact))endProblems.push('bilibili_refusal_missing');
    }
   }else if(job.run_class==='refusal'&&!records.some(r=>r.d.job===job.id&&(r.type==='STEP_REFUSED'||r.type==='STEP'&&r.d.exit===2)))
    endProblems.push('refusal_attempt_missing');
   else if(job.run_class==='drill'&&!records.some(r=>r.type==='GATE_DECISION'&&r.d.slot_id===job.id))
    endProblems.push('drill_decision_missing');
  }
  const executed=own.length>0||records.some(r=>r.d.job===job.id&&['STEP_REFUSED','JOB_END'].includes(r.type));
  return {...job,phase,seal,steps:own,details:ds,executed,rules,failed,unknown,counted:executed&&!failed.length&&!unknown.length,
   row:failed.length===1&&failed[0]==='8_no_interruption'?'aborted':failed.length===1&&failed[0]==='9_fault_fired'?'fault_did_not_fire':failed.length?'excluded':unknown.length?'unresolved':'counted',
   outside_upload:outside,faults: faultRows,violations,interference,end,endProblems,
   tool_record_incomplete:commandDetails.some(d=>!d.evidence.intact),model_periods:uniq(own.map(r=>modelPeriod(s,r).period))};
 });
 const attempts=[];
 for(const job of jobs)for(const p of job.platforms){
  const ds=job.details.filter(d=>d.platform===p),pub=ds.find(d=>d.step.d.role==='publish'&&started(d.step));
  if(!pub||!['test_post','real_slot','real_release'].includes(job.run_class))continue;
  const post=ds.filter(d=>operation(d.step)==='post'),refs=uniq(post.flatMap(d=>d.evidence.refs));
  // One job/platform should be one plan; multiple tool refs are explicitly separate attempts.
  for(const ref of refs.length?refs:[null]){
   const own=ref?post.filter(d=>!d.evidence.refs.length||d.evidence.refs.includes(ref)):post;
   const receipt=s.tool.publications?.find(r=>r.ref===ref&&r.platform===p);
   const look=looks(job.id,p,'post'),values=own.map(markers).filter(Number.isFinite);
   const press=p==='bilibili'||job.tool_record_incomplete?null:values.length?Math.max(...values):null;
   const draft=p==='youtube'&&draftBeforeSecond(own);
   const c4=job.faults.some(f=>f.kind==='between_save_and_press'&&f.state==='fired');
   const duplicateLook=look.seen==='present_twice_or_more',duplicateMarker=press>1&&!draft;
   const confirmed=own.some(d=>d.outcome==='confirmed'),contradicted=confirmed&&['absent','present_twice_or_more'].includes(look.seen);
   const firstPress=markers(pub),firstMade=firstPress>0&&!c4&&present(look.seen)&&own.filter(d=>d!==pub&&pressRoles.includes(d.step.d.role)).every(d=>d.evidence.noPress);
   const firstFault=job.faults.find(f=>f.role==='publish'&&f.state==='fired');
   attempts.push({job,platform:p,ref,receipt,details:ds,publish:pub,look,press_markers:press,draft_before_second:draft,
    duplicate:duplicateLook||duplicateMarker,duplicate_look:duplicateLook,duplicate_marker:duplicateMarker,contradicted,
    c4_one_click:c4&&press===2&&draft,press_marker_not_recorded:p==='bilibili',tool_record_incomplete:job.tool_record_incomplete,
    confirmed,first_confirmed:pub.outcome==='confirmed',nothing_sent:['nothing_sent','stopped','refused'].includes(pub.outcome),
    code:pub.answer?.code??'no_code',final_press_flag:receipt?.final_submit_attempted??(own.some(d=>d.answer?.press_flag===true)?true:null),
    computed_policy_A:firstMade&&pub.outcome!=='confirmed',
    computed_policy_B:firstMade&&['end_command_after_press','end_tree_after_press','browser_lost_during_upload','readback_timeout'].includes(firstFault?.kind),
    stamp:pub.evidence.stamp,phase:job.phase,site:s.site,model_period:modelPeriod(s,pub.step).period});
  }
 }
 // Comparator evidence is within-site sequence, usable even in dates-only exports.
 const deletionRows=[];
 for(const a of attempts){
  const dels=a.details.filter(d=>d.step.d.role==='delete'&&started(d.step));
  for(const [i,d] of dels.entries()){
   const ev=d.evidence,pressCount=ev.intact?ev.events.filter(e=>e.platform===a.platform&&e.kind==='deletion'&&e.operation==='press').length:null,press=pressCount===null?null:pressCount>0;
   const comparators=attempts.filter(b=>b!==a&&b.platform===a.platform&&b.job.run_class==='test_post'&&
    b.publish.step.seq<d.step.seq&&present(b.look.seen)&&b.look.record?.seq<d.step.seq&&
    !b.details.some(x=>x.step.d.role==='delete'&&x.step.seq<d.step.seq));
   const wrong=comparators.filter(b=>records.some(r=>r.type==='OBSERVATION'&&r.d.job===b.job.id&&r.d.platform===b.platform&&r.d.seen==='absent'&&
    r.seq>d.step.seq&&(s.privacy?countedLook(lookup(b.job.id,b.platform,r.d.about)):
      r.d.minutes_after>=10&&r.d.minutes_after<=1440&&time(r.at)-time(b.publish.step.at)>=r.d.minutes_after*60000)&&
    !b.details.some(x=>x.step.d.role==='delete'&&x.step.seq<r.seq)));
   const mismatched=ev.intact&&ev.events.some(e=>e.kind==='deletion'&&e.operation==='press'&&e.ref&&a.ref&&e.ref!==a.ref);
   const prior=dels[i-1],repeat=prior?.answer?.state==='DELETE_PENDING';
   deletionRows.push({attempt:a,detail:d,pressed:press,press_count:pressCount,approved:approval.get(d.step.seq)==='one_delete',repeat:!!repeat,
    wrong_code_try:!!a.job.wrong_delete_code_first&&i===0,
    refused:d.step.d.exit===2&&ev.noPress,second_press:(!!repeat&&press===true)||pressCount>1,wrong:press===true&&(wrong.length>0||mismatched),
    comparators:comparators.map(x=>({job:x.job.id,ref:x.ref})),wrong_comparators:wrong.map(x=>x.ref),
    comparator_measurement:press===true?(comparators.length?'measured':'unmeasured'):'not_pressed_or_unknown',look:looks(a.job.id,a.platform,'delete')});
  }
 }
 const capRows=publishChecks(records,s.jobs).map(expected=>{
  const matches=s.comparison.publish_checks?.filter(c=>c.step===expected.step&&c.job===expected.job&&c.platform===expected.platform)??[];
  const c=matches.length===1&&[matches[0].daily,matches[0].spacing].every(v=>v===null||typeof v==='boolean')?matches[0]:null;
  return {...expected,daily:c?.daily??null,spacing:c?.spacing??null,
   reason:c?.reason??(c?null:'site_comparison_missing_or_ambiguous'),source:s.provenance.timing};
 });
 return {s,records,steps,details,jobs,attempts,deletionRows,issues,gates,looks,capRows};
}

function figures(model,jobRows,platform,phase,stampFilter=null) {

 const {s}=model,ids=new Set(jobRows.map(j=>j.id)),has=r=>ids.has(r.d.job),onP=d=>!platform||d.platform===platform;
 const attempts=model.attempts.filter(a=>ids.has(a.job.id)&&(!platform||a.platform===platform)&&
  (!stampFilter||key(a.stamp)===stampFilter));
 const details=model.details.filter(d=>ids.has(d.step.d.job)&&onP(d)&&(!stampFilter||key(d.evidence.stamp)===stampFilter));
 const rec=model.records.filter(r=>s.phaseAt(r)===phase&&(!r.d.job||ids.has(r.d.job)));
 const gates=model.gates.filter(g=>g.phase===phase),issues=model.issues.filter(i=>!i.job||ids.has(i.job));
 const cases=s.comparison.in_doubt.filter(c=>ids.has(c.job)&&(!platform||c.platform===platform));
 const looks=s.comparison.look_checks.filter(c=>ids.has(c.job)&&(!platform||c.platform===platform));
 const stops=attempts.filter(a=>a.nothing_sent);
 const deletes=model.deletionRows.filter(d=>ids.has(d.attempt.job.id)&&(!platform||d.attempt.platform===platform));
 const repeats=attempts.flatMap(a=>['B','C'].includes(a.job.group)?a.details.filter(d=>['repeat_same','resume'].includes(d.step.d.role)).map(d=>{
  const previous=a.details.filter(x=>x.step.seq<d.step.seq).map(markers).filter(Number.isFinite);
  const before=previous.length?Math.max(...previous):null,after=markers(d);
  const delta=d.evidence.noPress?0:before!==null&&after!==null?Math.max(0,after-before):null;
  return {job:a.job.id,platform:a.platform,step:d.step.seq,delta,unknown:delta===null,
   no_press:d.evidence.noPress||delta===0,allowed_draft:delta>0&&a.draft_before_second,
   forbidden_repeat:delta>0&&!a.draft_before_second};
 }):[]);
 const faults=jobRows.flatMap(j=>j.faults.map(f=>({...f,job:j.id,platforms:j.platforms,
  outcome:cases.some(c=>c.job===j.id&&!c.closed)?'held_in_doubt':cases.some(c=>c.job===j.id&&c.by==='read')?'confirmed_by_readback':
   attempts.some(a=>a.job.id===j.id&&a.look.seen==='absent')?'nothing_posted':'unmeasured'})));
 const firedAround=attempts.filter(a=>a.job.faults.some(f=>f.state==='fired'&&around.includes(f.kind)));
 const changed=jobRows.filter(j=>j.run_class==='refusal'&&j.kind?.startsWith('changed_')).map(j=>{
  const ds=details.filter(d=>d.step.d.job===j.id&&['publish','changed_plan'].includes(d.step.d.role));
  const tried=ds.length>0,pass=tried&&ds.every(d=>d.step.d.exit===2&&d.answer?.code==='confirmation_required'&&d.evidence.noContact);
  return {job:j.id,kind:j.kind,tried,refused_before_contact:pass,
   went_ahead:ds.some(d=>d.evidence.events.some(e=>['call','stage','transition'].includes(e.type))),
   unverifiable:tried&&!pass&&!ds.some(d=>d.evidence.intact)};
 });
 const noChildStops=rec.filter(r=>r.type==='STEP_REFUSED'&&r.d.reason==='stop_flag').map(r=>({
  seq:r.seq,job:r.d.job,between_set_and_clear:rec.findLast(x=>x.seq<r.seq&&['STOP_SET','STOP_CLEARED'].includes(x.type))?.type==='STOP_SET'}));
 const bindings=s.comparison.command_binding==='shared_id'?s.comparison.write_bindings:null;
 const relevantBindings=bindings?.filter(b=>{
  if(b.step_seq){const step=model.steps.find(x=>x.seq===b.step_seq);return step&&s.phaseAt(step)===phase&&ids.has(step.d.job)&&(!platform||step.d.platforms.includes(platform));}
  // Outside commands have no Lab phase or platform proof. Keep in site integrity, not an invented stratum.
  return false;
 });
 const drills=jobRows.filter(j=>j.run_class==='drill'&&j.kind!=='handoff').map(j=>{
  const expected=g=>['not_owner','stop_flag','already_running'].includes(j.kind)?
   g.decision!=='plan_only'&&g.reason===j.kind:
   g.jobs.some(x=>x.id===j.id&&x.verdict===j.kind)&&!g.computed?.due?.includes(j.id)&&
   !model.records.find(r=>r.seq===g.seq)?.d.due?.includes(j.id);
  // A completed drill closes first. Its named starts may occur anywhere in the journal.
  const selected=gates.filter(g=>g.slot_id===j.id),runs=uniq(selected.map(g=>g.run));
  const ownSteps=model.steps.filter(r=>selected.some(g=>g.run===r.run&&r.seq>g.seq&&g.decision!=='plan_only'));
  return {job:j.id,kind:j.kind,run:runs.length===1?runs[0]:null,runs,decisions:selected.map(g=>g.seq),observed:selected.length>0,passed:selected.length?selected.every(expected)&&ownSteps.length===0:null,steps_after_refusal:ownSteps.length,
   reason:selected.length?null:'no_unambiguous_drill_session'};
 });
 const edits=attempts.flatMap(a=>{
  const commands=a.details.filter(d=>d.step.d.role==='edit'&&d.step.d.class==='write'&&started(d.step));
  return commands.map(d=>{
   const later=a.details.find(x=>x.step.seq>d.step.seq&&x.step.d.role==='edit'&&x.step.d.class==='read'&&x.outcome==='confirmed'&&
    (!d.evidence.refs.length||x.evidence.refs.some(r=>d.evidence.refs.includes(r))));
   return {job:a.job.id,platform:a.platform,step:d.step.seq,confirmed_by:d.outcome==='confirmed'?'edit':later?'readback':null,
    same_ref:sameEditTarget(d),
    stayed_private:model.looks(a.job.id,a.platform,'edit').seen==='present_private',
    privacy_source:'independent_look',look:model.looks(a.job.id,a.platform,'edit').seen};
  });
 });
 const timingRows=[];
 for(const d of details){
  const ev=d.evidence.events.filter(e=>e.type==='stage'&&e.platform===d.platform);
  for(let i=0;i<ev.length;i++){
   const number=x=>typeof x==='number'?x:time(x)/1000;
   const next=ev[i+1]?.at??d.evidence.events.findLast(e=>e.type==='command_end')?.at;
   const elapsed=ev[i].at==null||next==null?null:number(next)-number(ev[i].at);
   if(elapsed!==null&&elapsed>=0)timingRows.push({platform:d.platform,action:d.step.d.action,stage:ev[i].stage,seconds:elapsed});
  }
 }
 const stageKeys=uniq(timingRows.map(r=>key([r.platform,r.action,r.stage])));
 const safety={
  duplicate_posts:attempts.filter(a=>a.duplicate).length,
  second_press_without_draft:attempts.filter(a=>a.duplicate_marker).length,
  contradicted_confirmations:attempts.filter(a=>a.contradicted).length+edits.filter(e=>e.confirmed_by&&['absent','present_twice_or_more'].includes(e.look)).length+deletes.filter(d=>d.detail.outcome==='confirmed'&&present(d.look.seen)).length,
  wrong_post_deletes:deletes.filter(d=>d.wrong).length,
  second_delete_presses:deletes.filter(d=>d.second_press).length,
  changed_plans_went_ahead:changed.filter(r=>r.went_ahead).length,
  gate_replay_disagreements:issues.filter(i=>i.code==='gate_replay_disagreement').length|| (gates.some(g=>g.replay==='not_replayable')?null:0),
  writes_without_admission:issues.filter(i=>i.code==='write_without_admission').length,
  writes_without_approval:issues.filter(i=>i.code==='write_without_approval').length,
  deletes_without_own_approval:issues.filter(i=>i.code==='delete_without_own_approval').length,
  releases_without_own_approval:issues.filter(i=>i.code==='release_without_own_approval').length,
  wrong_operator_steps:issues.filter(i=>i.code==='step_wrong_operator').length,
  wrong_operator_decisions:issues.filter(i=>i.code==='gate_operator_mismatch').length,
  steps_after_drill_refusal:drills.reduce((n,r)=>n+r.steps_after_refusal,0),
  broken_chain_or_seals:s.integrity.exceptions.filter(i=>!i.code.startsWith('reconcile')).length,
  code_stamp_mismatches:issues.filter(i=>i.code==='code_stamp_mismatch').length,
  gate_digest_mismatches:issues.filter(i=>i.code==='gate_digest_mismatch').length,
  test_posts_public:attempts.filter(a=>a.job.run_class==='test_post'&&a.look.seen==='present_once').length,
  test_daily_cap:model.capRows.some(r=>ids.has(r.job)&&(!platform||r.platform===platform)&&r.daily===true)?model.capRows.filter(r=>ids.has(r.job)&&(!platform||r.platform===platform)&&r.daily===true).length:model.capRows.some(r=>ids.has(r.job)&&(!platform||r.platform===platform)&&r.daily===null)?null:0,
  test_spacing:model.capRows.some(r=>ids.has(r.job)&&(!platform||r.platform===platform)&&r.spacing===true)?model.capRows.filter(r=>ids.has(r.job)&&(!platform||r.platform===platform)&&r.spacing===true).length:model.capRows.some(r=>ids.has(r.job)&&(!platform||r.platform===platform)&&r.spacing===null)?null:0,
  steps_after_job_end:issues.filter(i=>i.code==='step_after_job_end').length,
  test_receipts_with_parts:attempts.filter(a=>a.job.run_class==='test_post'&&a.receipt&&Object.values(a.receipt.parts??{}).some(Boolean)).length,
  missing_looks:looks.filter(c=>c.status==='no_ground_truth').length
 };
 const grants=rec.filter(r=>r.type==='GO_AHEAD'&&has(r));
 const days=uniq(rec.map(r=>day(r.at))).sort();
 const noPressCases=cases.filter(c=>c.cutoff_press_flag===false);
 const falseAlarms=cases.filter(c=>operation(model.steps.find(r=>r.seq===c.step))==='post').filter(c=>c.platform===platform||!platform).filter(c=>c.by==='person'&&model.looks(c.job,c.platform,operation(model.steps.find(r=>r.seq===c.step))).seen==='absent');
 const markerAttempts=attempts.filter(a=>!a.press_marker_not_recorded&&!a.tool_record_incomplete&&a.final_press_flag===true);
 const markerFalseAlarms=falseAlarms.filter(c=>markerAttempts.some(a=>a.job.id===c.job&&a.platform===c.platform));
 const real=attempts.filter(a=>['real_slot','real_release'].includes(a.job.run_class));
 return {
  must_be_zero:safety,
  gate:{errors:rec.filter(r=>r.type==='GATE_ERROR').map(r=>({seq:r.seq,run:r.run,error:r.d.error,replay:'not_replayable_inputs_not_recorded'})),decisions:tally(gates,g=>key([g.decision,g.reason,g.launcher])),replay:tally(gates,g=>g.replay),
   replay_rows:gates.map(({seq,replay,computed,source})=>({seq,replay,computed,source})),
   per_job_verdicts:tally(gates.flatMap(g=>g.jobs),j=>j.verdict),scheduled_slots:'not_run',drills,
   drill_bound:upper95(drills.filter(r=>r.passed===false).length,drills.filter(r=>r.passed!==null).length),
   matched_commands:relevantBindings?.length??null,binding_source:s.comparison.command_binding,
   handoffs:rec.filter(r=>r.type==='HANDOFF').map(r=>({seq:r.seq,from:r.d.from,to:r.d.to,job:r.d.job,
    run_class:jobRows.find(j=>j.id===r.d.job)?.run_class??null})),
   refused_other_slots:rec.filter(r=>r.type==='GATE_DECISION'&&r.d.reason==='not_owner').map(r=>({seq:r.seq,slot:r.slot})),
   approval_accounting:'observed STEP consumption only; derived unspent units are an upper limit',
   go_aheads:tally(grants,r=>key([r.d.scope,r.d.by])),messages:days.map(d=>({day:d,
    distinct:uniq(grants.filter(r=>day(r.at)===d).map(r=>r.d.words_sha256)).length,
    messages:uniq(grants.filter(r=>day(r.at)===d).map(r=>r.d.words_sha256)).map(h=>({words_sha256:h,jobs:uniq(grants.filter(r=>r.d.words_sha256===h).map(r=>r.d.job))}))})),
   wrapper_refusals:tally(rec.filter(r=>r.type==='STEP_REFUSED'),r=>key([r.d.reason,r.d.role]))},
  plan_binding:{changed,no_child_stops:noChildStops,bound:upper95(changed.filter(r=>r.went_ahead).length,changed.filter(r=>r.tried&&!r.unverifiable).length)},
  submission:{attempts:attempts.map(a=>({job:a.job.id,ref:a.ref,platform:a.platform,run_class:a.job.run_class,
    with_fault:a.job.faults.some(f=>f.state==='fired'),press_markers:a.press_markers,look:a.look.seen,look_status:a.look.status,
    duplicate:a.duplicate,duplicate_by_look:a.duplicate_look,duplicate_by_marker:a.duplicate_marker,c4_one_click:a.c4_one_click,
    press_marker_not_recorded:a.press_marker_not_recorded,tool_record_incomplete:a.tool_record_incomplete})),
   faults,repeats,fault_duplicate_bound:upper95(firedAround.filter(a=>a.duplicate).length,firedAround.filter(a=>a.duplicate||countedLook(a.look)&&!a.tool_record_incomplete&&!a.press_marker_not_recorded).length),
   fault_bound_unmeasured:firedAround.filter(a=>!a.duplicate&&(!countedLook(a.look)||a.tool_record_incomplete||a.press_marker_not_recorded)).length,
   repeat_bound:upper95(repeats.filter(r=>r.forbidden_repeat).length,repeats.filter(r=>!r.unknown).length),
   computed_baselines:{label:'computed_never_run',policy_A:attempts.filter(a=>a.computed_policy_A).length,policy_B:attempts.filter(a=>a.computed_policy_B).length,
    observed_duplicates:attempts.filter(a=>a.duplicate).length,unmeasured:attempts.filter(a=>a.tool_record_incomplete||!countedLook(a.look)).length}},
  in_doubt:{cases,possible_unclassified_errors:s.comparison.error_steps_unclassified.filter(c=>ids.has(model.steps.find(r=>r.seq===c.step)?.d.job)&&(!platform||c.platform===platform)),
   caller_only_no_press:noPressCases,minutes:describe(cases.map(c=>s.privacy?c.minutes:c.seconds===null?null:c.seconds/60)),source:s.provenance.timing,
   closure:tally(cases,c=>c.closed?c.by:'open'),strict_closed:cases.filter(c=>c.closed_strict).length,
   observed_outcomes:tally(cases,c=>model.looks(c.job,c.platform,operation(model.steps.find(r=>r.seq===c.step))).seen??'unclear'),
   false_alarm_share:share(markerFalseAlarms.length,markerAttempts.length),
   click_adjusted_false_alarm_share:share(markerFalseAlarms.filter(c=>!attempts.some(a=>a.job.id===c.job&&a.job.faults.some(f=>f.kind==='between_save_and_press'&&f.state==='fired'))).length,
    markerAttempts.filter(a=>!a.job.faults.some(f=>f.kind==='between_save_and_press'&&f.state==='fired')||a.c4_one_click).length),
   stops_before_press:stops.map(a=>({job:a.job.id,platform:a.platform,code:a.code,
    finished_by:a.details.some(d=>d.step.d.role==='resume'&&d.outcome==='confirmed')?'resume':
     rec.some(r=>r.type==='NOTE'&&r.d.code==='manual_step'&&r.d.job===a.job.id)?'person':'unfinished'})),
   stop_share:share(stops.length,attempts.length)},
  readback:{real_releases:real.map(a=>({job:a.job.id,platform:a.platform,
    confirmed:a.first_confirmed?'same_command':a.details.some(d=>d.step.d.role==='verify'&&d.outcome==='confirmed')?'later_verify':'never',
    supplementary:a.job.run_class==='real_release'})),
   contradicted:attempts.filter(a=>a.contradicted).map(a=>({job:a.job.id,ref:a.ref})),edits,
   deletes:deletes.map(d=>({job:d.attempt.job.id,ref:d.attempt.ref,step:d.detail.step.seq,approved:d.approved,pressed:d.pressed,press_count:d.press_count,
    tool_gone:d.detail.outcome==='confirmed'||d.attempt.details.some(x=>x.step.seq>d.detail.step.seq&&x.step.d.role==='delete_verify'&&x.outcome==='confirmed'),
    pending:d.detail.answer?.state==='DELETE_PENDING',refused:d.refused,code:d.detail.answer?.code??'no_code',
    repeat:d.repeat,wrong_code_try:d.wrong_code_try,second_press:d.second_press,wrong_post:d.wrong,
    comparators:d.comparators,comparator_measurement:d.comparator_measurement,look:d.look.seen})),
   wrong_post_bound:upper95(deletes.filter(d=>d.wrong&&d.comparators.length).length,deletes.filter(d=>d.pressed===true&&d.comparators.length).length),
   wrong_post_unmeasured:deletes.filter(d=>d.comparator_measurement==='unmeasured').length,coverage:'other study posts only; outside posts unmeasured'},
  timing:{stages:stageKeys.map(k=>({key:JSON.parse(k),...describe(timingRows.filter(r=>key([r.platform,r.action,r.stage])===k).map(r=>r.seconds))})),
   commands:uniq(details.map(d=>key([d.platform,d.step.d.action]))).map(k=>({key:JSON.parse(k),
    ...describe(details.filter(d=>key([d.platform,d.step.d.action])===k).map(d=>d.evidence.command?.seconds))})),
   source:'tool stage/command events; withheld durations excluded',
   human_by_day:days.map(d=>({day:d,go_aheads:grants.filter(r=>day(r.at)===d).length,
    looks:tally(looks.filter(c=>day(model.records.find(r=>r.seq===c.observation)?.at)===d),c=>c.status),
    signoffs:rec.filter(r=>r.type==='SIGN_OFF'&&day(r.at)===d).length}))},
  protocol:{runs:jobRows.map(j=>({job:j.id,executed:j.executed,planned:s.plan.runs.some(r=>r.id===j.id),class:j.run_class,
    reason:j.end?.d.reason??(j.executed?'not_finished':'no_recorded_reason'),counted:j.counted,row:j.row,rules:j.rules,
    outside_upload:j.outside_upload,tool_record_incomplete:j.tool_record_incomplete,
    closed:j.end?{reason:j.end.d.reason,platform_settlements:j.end.d.platform_settlements??[],problems:j.endProblems}:null})),
   violations:rec.filter(r=>r.type==='VIOLATION').map(r=>({seq:r.seq,...r.d,...modelPeriod(s,model.steps.findLast(x=>x.seq<r.seq&&x.d.job===r.d.job)??r)})),
   notes:rec.filter(r=>r.type==='NOTE').map(r=>({seq:r.seq,day:day(r.at),...r.d})),
   guard_clearings:rec.filter(r=>r.type==='NOTE'&&r.d.code==='guard_cleared').map(r=>({seq:r.seq,day:day(r.at)})),
   sessions:tally(rec.filter(r=>r.type==='RUN_END'),r=>r.d.state),
   model_period_runs:tally(jobRows.flatMap(j=>j.model_periods),p=>p),
   after_fault:jobRows.flatMap(j=>j.faults.filter(f=>f.state==='fired').map(f=>{
    const armStep=j.steps.find(r=>r.seq>(f.seq??Infinity)&&r.d.role===f.role);
    const later=rec.filter(r=>r.seq>(armStep?.seq??Infinity)&&['STEP','OBSERVATION','GO_AHEAD'].includes(r.type)&&r.d.job===j.id);
    return {job:j.id,kind:f.kind,next:later[0]?.type??null,asked:['OBSERVATION','GO_AHEAD'].includes(later[0]?.type),
     steps:later.filter(r=>r.type==='STEP').map(r=>({seq:r.seq,role:r.d.role,refused:r.d.exit===2,...modelPeriod(s,r)})),
     violation_followed:j.violations.some(v=>v.seq>(armStep?.seq??Infinity))};
   })),cap_observations:model.capRows.filter(r=>ids.has(r.job)&&(!platform||r.platform===platform))},
  assumptions:{A1:{extra_copies_without_second_marker:attempts.filter(a=>a.duplicate_look&&a.press_markers!==null&&a.press_markers<=1).map(a=>({job:a.job.id,ref:a.ref})),
    ungated:issues.filter(i=>i.code==='write_without_admission'),rule_breaks:rec.filter(r=>r.type==='VIOLATION'&&['page_driven_by_assistant','platform_called_directly','retry_around_tool'].includes(r.d.rule)).map(r=>r.seq)},
   A2:jobRows.flatMap(j=>j.faults.filter(f=>killed.includes(f.kind)&&['fired','unknown'].includes(f.state)).map(f=>{
    const faultStep=j.steps.find(r=>r.seq>(f.seq??Infinity)&&r.d.role===f.role);
    const later=details.filter(d=>d.step.d.job===j.id&&d.step.seq>(faultStep?.seq??Infinity));
    const flag=later.flatMap(d=>d.evidence.snapshot).find(e=>typeof e.final_submit_attempted==='boolean')?.final_submit_attempted;
    const stage=details.find(d=>d.step===faultStep)?.evidence.events.filter(e=>e.type==='stage').at(-1)?.stage;
    return {job:j.id,kind:f.kind,readable:flag!==undefined,press_flag:flag??null,last_stage:stage??null,
     agrees:flag===undefined?null:stage==='waiting for platform confirmation'?flag===true:null};
   })),
   A3:{journal:s.integrity,tool_chain_attested:s.tool.integrity?.chain_ok??null,record_edits:rec.filter(r=>r.type==='VIOLATION'&&r.d.rule==='record_edited').length,limit:'nothing else checks manual record edits'},
   A4:attempts.filter(a=>a.press_markers>1).map(a=>({job:a.job.id,ref:a.ref,markers:a.press_markers,draft_before_second:a.draft_before_second,
    first_marker_had_click:!a.job.faults.some(f=>f.kind==='between_save_and_press'&&f.state==='fired'),
    looks_before_second:model.records.filter(r=>r.type==='OBSERVATION'&&r.d.job===a.job.id&&r.d.platform===a.platform&&r.d.about==='post'&&r.seq<(a.details.find(d=>markers(d)>1)?.step.seq??0)).map(r=>({seq:r.seq,seen:r.d.seen}))}))}
 };
}



function checklist(models,definition,onlyCounted) {
 const all=[];
 for(const m of models)for(const a of m.attempts.filter(a=>a.phase==='2b'&&(!onlyCounted||a.job.counted))){
  const add=(cell,confirmed,lookAgrees,detail=a.publish,extra={})=>all.push({cell,platform:a.platform,site:a.site,
   job:a.job.id,step:detail.step.seq,at:detail.step.at,privacy:m.s.privacy,stamp:a.stamp?.commit??'unrecorded',
   confirmed,look_agrees:lookAgrees,contradicted:a.contradicted,effect:present(a.look.seen)||a.look.seen==='present_twice_or_more',
   stopped:a.nothing_sent,...extra});
  if(['real_slot','real_release'].includes(a.job.run_class))add(1,a.confirmed,present(a.look.seen));
  if(a.job.group==='B')add(2,a.confirmed,present(a.look.seen));
  if(a.job.group==='D'||a.job.faults.some(f=>f.kind==='between_save_and_press'&&f.state==='fired')&&a.platform==='youtube'&&a.draft_before_second)
   add(3,a.confirmed&&a.press_markers!==null&&(a.press_markers===1||a.c4_one_click),present(a.look.seen));
  if(a.job.faults.some(f=>around.slice(0,3).includes(f.kind)&&f.state==='fired')&&!a.first_confirmed&&!a.nothing_sent&&present(a.look.seen))
   add(4,a.details.some(d=>d.step.d.role==='verify'&&d.outcome==='confirmed'),true);
  for(const d of a.details.filter(d=>d.step.d.role==='edit'&&d.step.d.class==='write'&&started(d.step))){
   const read=a.details.find(e=>e.step.seq>d.step.seq&&e.step.d.role==='edit'&&e.step.d.class==='read'&&e.outcome==='confirmed'&&
    d.evidence.refs.some(r=>e.evidence.refs.includes(r)));
   const l=m.looks(a.job.id,a.platform,'edit');
   add(6,(d.outcome==='confirmed'||!!read)&&sameEditTarget(d)===true,present(l.seen)&&l.record?.d.fields_match===true,d,
    {contradicted:(d.outcome==='confirmed'||!!read)&&(!present(l.seen)||l.record?.d.fields_match===false)});
  }
  const dels=m.deletionRows.filter(d=>d.attempt===a);
  for(const d of dels){
   if(!d.repeat&&!d.wrong_code_try)add(7,d.detail.outcome==='confirmed'||a.details.some(e=>e.step.seq>d.detail.step.seq&&e.step.d.role==='delete_verify'&&e.outcome==='confirmed'),d.look.seen==='absent',d.detail,
    {contradicted:d.detail.outcome==='confirmed'&&present(d.look.seen)});
   if(d.repeat)add(5,d.refused&&d.pressed===false,d.look.seen==='absent',d.detail,{refusal:true,effect:d.pressed===true});
  }
  if(['B','C'].includes(a.job.group)&&!a.job.faults.some(f=>f.kind==='between_save_and_press')&&markers(a.publish)>0)
   for(const d of a.details.filter(d=>['repeat_same','resume'].includes(d.step.d.role)))add(5,d.evidence.noPress,present(a.look.seen),d,{refusal:true,effect:!d.evidence.noPress&&d.evidence.hasPress});
  for(const d of a.details.filter(d=>d.step.d.role==='delete_dry_run'&&a.platform==='bilibili'))
   add(8,d.step.d.exit!==0&&d.evidence.noContact,true,d,{refusal:true,effect:!d.evidence.noContact});
 }
 // Refusal jobs and gate drills have no publication attempt.
 for(const m of models)for(const j of m.jobs.filter(j=>j.phase==='2b'&&(!onlyCounted||j.counted))){
  if(j.run_class==='refusal')for(const p of j.platforms){
   const ds=j.details.filter(d=>d.platform===p&&['publish','changed_plan'].includes(d.step.d.role)),ref=m.records.find(r=>r.type==='STEP_REFUSED'&&r.d.job===j.id&&r.d.reason==='stop_flag');
   const stopped=j.kind==='stop_before_write',ok=stopped?!!ref:ds.length>0&&ds.every(d=>d.step.d.exit===2&&d.evidence.noContact);
   const d=ds[0],r=d?.step??ref;if(!r)continue;
   all.push({cell:5,platform:p,site:m.s.site,job:j.id,step:r.seq,at:r.at,privacy:m.s.privacy,
    stamp:d?.evidence.stamp?.commit??j.seal?.d.crosscast_commit??'unrecorded',confirmed:ok,look_agrees:true,contradicted:false,
    effect:ds.some(d=>d.evidence.hasPress||d.evidence.events.some(e=>e.type==='call')),stopped:ok,refusal:true});
  }
  if(j.kind==='already_running'){
   for(const r of m.records.filter(x=>x.type==='GATE_DECISION'&&x.d.slot_id===j.id)){
    const effect=m.steps.some(x=>x.run===r.run&&x.seq>r.seq),refused=r.d.reason==='already_running'&&r.d.decision!=='plan_only';
    all.push({cell:5,platform:'gate',site:m.s.site,job:j.id,step:r.seq,at:r.at,privacy:m.s.privacy,
     stamp:j.seal?.d.crosscast_commit??'unrecorded',confirmed:refused&&!effect,look_agrees:true,contradicted:false,effect,stopped:refused,refusal:true});
   }
  }
 }
 const stamps=uniq(models.flatMap(m=>m.s.seals.filter(s=>s.d.phase==='2b').map(s=>s.d.crosscast_commit)));
 return stamps.flatMap(stamp=>definition.cells.map(cell=>{
  const tries=all.filter(r=>r.cell===cell.id&&r.platform===cell.platform&&r.stamp===stamp);
  const n=tries.length,confirmed=tries.filter(t=>t.confirmed).length;
  const interval=t=>t.privacy?[time(day(t.at)+'T00:00:00Z'),time(day(t.at)+'T23:59:59.999Z')]:[time(t.at),time(t.at)];
  const precedes=(a,b)=>a.site===b.site?a.step<b.step:interval(a)[1]<interval(b)[0];
  const lastCandidates=tries.filter(t=>tries.filter(o=>o!==t&&precedes(t,o)).length<3);
  const lastThree=n>=3&&lastCandidates.every(t=>t.confirmed);
  const orderUncertain=lastCandidates.length>3;
  const agreement=tries.filter(t=>t.confirmed).every(t=>t.look_agrees);
  const bad=tries.some(t=>t.contradicted),tester=cell.id===1||tries.some(t=>t.site==='tester'&&t.confirmed);
  const refusal=[5,8].includes(cell.id);
  let status=cell.unsupported_by_design?'unsupported_by_design':!n?'not_attempted':
   refusal&&tries.some(t=>t.effect)?'unconfirmed_effect_happened':
   n>=3&&(refusal?tries.every(t=>t.confirmed&&t.look_agrees):lastThree&&confirmed/n>=.8&&agreement&&!bad&&tester)?'confirmed':
   confirmed?'partly_confirmed':tries.some(t=>t.effect)?'unconfirmed_effect_happened':
   tries.every(t=>t.stopped)?'unconfirmed_stopped_before_acting':'unconfirmed_no_effect';
  return {cell:cell.id,platform:cell.platform,code_stamp:stamp,status,n,confirmed,
   independent_agreed:tries.filter(t=>t.confirmed&&t.look_agrees).length,last_three_confirmed:lastThree,
   cross_site_order_ambiguous:orderUncertain,last_three_possible: lastCandidates.map(t=>({site:t.site,job:t.job,step:t.step})),
   tester_condition:refusal?null:tester,per_site:tally(tries,t=>t.site),reason:n?null:cell.reason??'no_recorded_reason',tries};
 }));
}
function timingPairs(m) {
 const pairs=uniq(m.jobs.map(j=>j.pair).filter(Boolean));
 return pairs.map(id=>{
  const jobs=m.jobs.filter(j=>j.pair===id),phases=uniq(jobs.map(j=>j.phase));
  const releases=jobs.map(j=>{
   const pub=j.steps.find(r=>r.d.role==='publish'&&started(r)),cmd=pub?m.s.tool.runs.find(c=>c.awl_run_id===pub.d.awl_run_id):null;
   return {job:j.id,mode:j.pair_mode,wall:cmd?.concurrency?.wall??null,sum_spans:cmd?.concurrency?.sum??null,
    overlap:cmd?.concurrency?.overlap??null,idle_by_slot:cmd?.concurrency?.idle_by_slot??null,
    complete:!!cmd&&cmd.ended===true&&cmd.concurrency?.wall!=null&&j.outside_upload===false,cmd};
  });
  const notes=m.records.filter(r=>r.type==='NOTE'&&r.d.code==='outside_upload');
  let outside=false;
  if(notes.length&&jobs.some(j=>j.steps.length)){
   const upload=jobs.flatMap(j=>j.steps.filter(r=>['publish','repeat_same','resume'].includes(r.d.role)));
   if(m.s.privacy)outside=null;
   else {const lo=Math.min(...upload.map(r=>time(r.at)-(r.d.ms??0))),hi=Math.max(...upload.map(r=>time(r.at)));
    outside=notes.some(r=>r.d.minutes_ago==null||time(r.at)-r.d.minutes_ago*60000>=lo&&time(r.at)-r.d.minutes_ago*60000<=hi);}
  }
  const one=releases.find(r=>r.mode==='one_at_a_time'),two=releases.find(r=>r.mode==='two_at_a_time');
  return {pair:id,site:m.s.site,phases,complete:phases.length===1&&releases.length===2&&releases.every(r=>r.complete)&&outside===false,
   outside_upload:outside,releases:releases.map(({cmd,...r})=>r),difference_seconds:one?.wall!=null&&two?.wall!=null?two.wall-one.wall:null};
 });
}
export function analyze(inputs,{gateHash,gateHashes=[gateHash]}={}) {
 if(inputs?.schema!=='round2-analysis-inputs/1'||!Array.isArray(inputs.sites)||!inputs.sites.length||!gateHash)
  throw new Error('invalid_analysis_inputs');
 if(new Set(inputs.sites.map(s=>s.site)).size!==inputs.sites.length)throw new Error('duplicate_site');
 const definition=inputs.checklist;
 if(definition?.schema!=='round2-checklist/1'||!Array.isArray(definition.cells)||!definition.cells.length||
  definition.cells.some(c=>!Number.isInteger(c.id)||c.id<1||c.id>8||typeof c.platform!=='string')||
  new Set(definition.cells.map(c=>key([c.id,c.platform]))).size!==definition.cells.length)throw new Error('invalid_checklist');
 const requiredCells=[...['bilibili','youtube','douyin','x','instagram'].map(p=>[1,p]),
  ...[2,3,4,6,7].flatMap(id=>['youtube','douyin'].map(p=>[id,p])),
  ...['youtube','douyin','gate'].map(p=>[5,p]),[8,'bilibili']];
 if(requiredCells.some(([id,p])=>!definition.cells.some(c=>c.id===id&&c.platform===p)))throw new Error('incomplete_checklist');
 const models=inputs.sites.map(normalizeSite).map(s=>siteModel(s,gateHashes));
 const tables=[],siteIntegrity=[];
 for(const m of models) {
  const phases=uniq(m.s.seals.map(r=>r.d.phase));
  for(const phase of phases) {
   const jobs=m.jobs.filter(j=>j.phase===phase),ps=uniq(jobs.flatMap(j=>j.platforms));
   for(const platform of ps){
    const relevant=jobs.filter(j=>j.platforms.includes(platform));
    const stampFor=j=>{
     const values=uniq(j.details.filter(d=>d.platform===platform).map(d=>key(d.evidence.stamp))).sort();
     return values.length>1?key({mixed:values.map(v=>JSON.parse(v))}):values[0]??'null';
    };
    for(const stamp of uniq(relevant.map(stampFor))){
     const group=relevant.filter(j=>stampFor(j)===stamp);
     tables.push({phase,site:m.s.site,platform,code_stamp:JSON.parse(stamp),
      all_recorded:figures(m,group,platform,phase),
      counted:figures(m,group.filter(j=>j.counted),platform,phase),
      counted_without_outside_upload:figures(m,group.filter(j=>j.counted&&j.outside_upload===false),platform,phase)});
    }
   }
   // Gate and planned-only rows with no platform must not vanish.
   tables.push({phase,site:m.s.site,platform:null,code_stamp:'lab:'+m.s.seals.findLast(s=>s.d.phase===phase)?.d.lab_commit,
    all_recorded:figures(m,jobs,null,phase),counted:figures(m,jobs.filter(j=>j.counted),null,phase),
    counted_without_outside_upload:figures(m,jobs.filter(j=>j.counted&&j.outside_upload===false),null,phase)});
  }
  const bindings=m.s.comparison.command_binding==='shared_id'?m.s.comparison.write_bindings:null;
  const losses=m.s.tool.runs.map(c=>{
   const eventSum=m.s.tool.events.filter(e=>e.run===c.run).reduce((n,e)=>n+(e.dropped_before??0),0);
   return {run:c.run,reported:c.dropped_events??null,event_sum:eventSum,used:c.dropped_events==null?null:Math.max(c.dropped_events,eventSum),
    disagree:c.dropped_events!=null&&eventSum>c.dropped_events,sequence_gap:c.sequence_gap??null,chain_ok:c.chain_ok??null,loss_complete:c.loss_complete??null};
  });
  siteIntegrity.push({site:m.s.site,provenance:m.s.provenance,cutoff:m.s.asOf,journal:m.s.integrity,
   tool:m.s.tool.integrity,losses,tool_dropped_events:m.s.tool.integrity.dropped_events??'not_available',
   tool_unreadable_loss_files:m.s.tool.integrity.unreadable_loss_records??null,
   binding:m.s.comparison.command_binding,outside_commands:bindings?.filter(b=>b.status!=='matched')??null,
   outside_share:bindings?share(bindings.filter(b=>b.status!=='matched').length,bindings.length):null,
   binding_statuses:bindings?tally(bindings,b=>b.status):null,
   excluded_publications:(m.s.tool.publications??[]).filter(p=>!m.attempts.some(a=>a.ref===p.ref&&a.platform===p.platform)).map(p=>({ref:p.ref,platform:p.platform,reason:'no_study_bound_publish_command'})),
   unplanned_jobs:uniq(m.steps.map(r=>r.d.job)).filter(id=>!m.s.jobs.some(j=>j.id===id)),
   issues:m.issues,unknowns:uniq(m.jobs.flatMap(j=>j.unknown))});
 }
 const pairs=models.flatMap(timingPairs);
 const pooledBounds=uniq(models.flatMap(m=>m.attempts.map(a=>a.phase))).flatMap(phase=>{
  const stamps=uniq(models.flatMap(m=>m.attempts.filter(a=>a.phase===phase).map(a=>a.stamp?.commit??'unrecorded')));
  return stamps.map(stamp=>{
   const all=models.flatMap(m=>m.attempts).filter(a=>a.phase===phase&&(a.stamp?.commit??'unrecorded')===stamp&&a.job.faults.some(f=>around.includes(f.kind)&&f.state==='fired'));
   const bound=rows=>{const eligible=rows.filter(a=>a.duplicate||countedLook(a.look)&&!a.tool_record_incomplete&&!a.press_marker_not_recorded);return {...upper95(eligible.filter(a=>a.duplicate).length,eligible.length),unmeasured:rows.length-eligible.length};};
   return {phase,code_stamp:stamp,all_recorded:bound(all),counted:bound(all.filter(a=>a.job.counted)),
    by_platform:uniq(all.map(a=>a.platform)).map(platform=>({platform,...bound(all.filter(a=>a.platform===platform))})),
    by_site:uniq(all.map(a=>a.site)).map(site=>({site,...bound(all.filter(a=>a.site===site))}))};
  });
 });
 const violations=tables.filter(t=>t.platform===null).flatMap(t=>Object.entries(t.all_recorded.must_be_zero).filter(([,n])=>n>0).map(([name,n])=>({phase:t.phase,site:t.site,name,n})));
 const gaps=siteIntegrity.flatMap(s=>[...s.journal.exceptions,...s.unknowns.map(code=>({code}))].map(v=>({site:s.site,...v})));
 for(const t of tables.filter(t=>t.platform===null)){
  for(const [name,value]of Object.entries(t.all_recorded.must_be_zero))if(value===null)gaps.push({site:t.site,phase:t.phase,code:'unmeasured_'+name});
  for(const d of t.all_recorded.gate.drills)if(d.passed===null)gaps.push({site:t.site,phase:t.phase,job:d.job,code:'drill_session_binding_missing'});
  if(t.all_recorded.protocol.runs.some(j=>j.outside_upload===null))gaps.push({site:t.site,phase:t.phase,code:'outside_upload_timing_unavailable'});
 }

 const completeInputSites=models.length===2&&models.some(m=>m.s.site==='owner')&&models.some(m=>m.s.site==='tester');
 const cells={all_recorded:checklist(models,definition,false),counted:checklist(models,definition,true)};
 return {schema:'round2-metrics/1',annex:'1.8',outcome_table:OUTCOMES,input_kind:inputs.synthetic===true?'synthetic_test_only':'exported_study_records',
  inputs:siteIntegrity,all_sites_present:completeInputSites,tables,checklist:cells,timing_pairs:pairs,pooled_fault_bounds:pooledBounds,
  timing_claim:pairs.filter(p=>p.complete&&p.phases[0]==='2b').length<6?'no_general_speed_claim':'descriptive_pairs_only',
  verdict:{status:violations.length?'observed_failures':gaps.length||!completeInputSites?'evidence_incomplete':'no_observed_must_be_zero_failures',
   violations,gaps,fully_working:completeInputSites&&!gaps.length&&!violations.length&&cells.counted.every(c=>c.status==='confirmed'),
   scope:'offline analysis of exported evidence; no account action or live acceptance implied'}};
}

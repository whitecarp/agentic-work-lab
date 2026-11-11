// Closed vocabulary copied from the tool's audit contract, plus its public
// browser_busy / NEEDS_A_PERSON answers. No message, hint or path is retained.
export const STATES=('PREPARING IN_PROGRESS UNKNOWN PREPARATION_INCOMPLETE PREVIEW_READY WAITING_FOR_USER NEEDS_A_PERSON SUBMITTED_UNVERIFIED SUBMISSION_VERIFIED EXISTING_VERIFIED AGENT_REPORTED_VERIFIED SKIPPED_EXISTING NOT_STARTED DELETE_READY DELETE_PENDING DELETED_VERIFIED DELETE_PREVIEW EDIT_PREPARING EDIT_SUBMITTING EDIT_SUBMITTED_UNVERIFIED EDIT_VERIFIED EDIT_MISMATCH EDIT_UNAVAILABLE EDIT_PREPARED PENDING_WORK_ID PENDING SUBMITTED VERIFIED FAILED NOT_SUPPORTED forgotten').split(' ');
export const CODES=('stopped interrupted confirmation_required deletion_already_attempted deletion_not_allowed deletion_target_unverified deletion_target_changed unsupported_delete attempt_exists already_submitted press_blocked cover_blocked wrong_account wrong_work owner_unverified needs_person human_verification_required injected_fault fault_injection_refused backend_timeout browser_timeout readback_failed schedule_unverified identity_mismatch busy browser_busy lock_marker_unreadable file_not_found invalid_file internal_error submission_unverified preparation_incomplete inspect_existing_edit receipt_missing stopped_before_press draft_only verified_not_saved').split(' ');
export const pressRoles=['publish','repeat_same','changed_plan','resume','edit','delete'];
export const confirmed={post:'SUBMISSION_VERIFIED',edit:'EDIT_VERIFIED',delete:'DELETED_VERIFIED'};
export const operation=s=>s.d.action==='delete'||['delete','delete_verify'].includes(s.d.role)?'delete':s.d.role==='edit'?'edit':['publish','repeat_same','changed_plan','resume','verify'].includes(s.d.role)?'post':null;
const choice=(v,list)=>list.includes(v)?v:null;
export function readAnswers(value,platforms){
 const data=value?.data??value,parts=data?.results;
 return platforms.map(platform=>{
  const part=parts?parts[platform]:data;
  const receipt=part?.receipt??part;
  return {platform,state:choice(part?.state,STATES),previous_state:choice(part?.previous_state,STATES),
   code:choice(part?.code??part?.error?.code??value?.error?.code,CODES),
   press_flag:typeof receipt?.final_submit_attempted==='boolean'?receipt.final_submit_attempted:null,not_saved:!!part?.not_saved,
   answered:!!part&&typeof part==='object'&&(typeof part.state==='string'||typeof value?.ok==='boolean'||typeof value?.error?.code==='string'),
   refs:[part?.key].filter(x=>typeof x==='string'&&/^[a-f0-9]{64}$/.test(x))};
 });
}
export function uncertainState(a,about){
 const state=a.state==='SKIPPED_EXISTING'?a.previous_state:a.state;
 return (about==='post'?['UNKNOWN','SUBMITTED_UNVERIFIED']:about==='edit'?['EDIT_SUBMITTING','EDIT_SUBMITTED_UNVERIFIED']:['DELETE_PENDING']).includes(state);
}
export function nothingSent(a){return ['NOT_STARTED','WAITING_FOR_USER','NEEDS_A_PERSON'].includes(a.state)||a.state==='PREPARATION_INCOMPLETE'&&a.press_flag!==true;}
export function confirmedAnswer(a,about){return a.state===confirmed[about]&&a.code!=='verified_not_saved'&&!a.not_saved;}

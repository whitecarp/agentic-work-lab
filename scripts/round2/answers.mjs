// Public states use the recorded vocabulary. Only compact tool codes enter the journal.
export const STATES=('PREPARING IN_PROGRESS UNKNOWN PREPARATION_INCOMPLETE PREVIEW_READY WAITING_FOR_USER NEEDS_A_PERSON SUBMITTED_UNVERIFIED SUBMISSION_VERIFIED EXISTING_VERIFIED AGENT_REPORTED_VERIFIED SKIPPED_EXISTING NOT_STARTED DELETE_READY DELETE_PENDING DELETED_VERIFIED DELETE_PREVIEW EDIT_PREPARING EDIT_SUBMITTING EDIT_SUBMITTED_UNVERIFIED EDIT_VERIFIED EDIT_MISMATCH EDIT_UNAVAILABLE EDIT_PREPARED PENDING_WORK_ID PENDING SUBMITTED VERIFIED FAILED NOT_SUPPORTED forgotten').split(' ');
export const codeWord=v=>typeof v==='string' && /^[a-z][a-z0-9_]{0,47}$/.test(v)?v:null;
export const pressRoles=['publish','repeat_same','changed_plan','resume','edit','delete'];
export const confirmed={post:'SUBMISSION_VERIFIED',edit:'EDIT_VERIFIED',delete:'DELETED_VERIFIED'};
export const operation=s=>s.d.action==='delete'||['delete','delete_verify'].includes(s.d.role)?'delete':s.d.role==='edit'?'edit':['publish','repeat_same','changed_plan','resume','verify'].includes(s.d.role)?'post':null;
const stateWord=v=>typeof v==='string' && v.length ? (STATES.includes(v)?v:'UNLISTED') : null;
export function readAnswers(value,platforms){
 const data=value?.data??value,parts=data?.results;
 return platforms.map(platform=>{
  const part=parts?parts[platform]:data;
  const receipt=part?.receipt??part;
  return {platform,state:stateWord(part?.state),previous_state:stateWord(part?.previous_state),
   code:codeWord(part?.code??part?.error?.code??value?.error?.code),
   press_flag:typeof receipt?.final_submit_attempted==='boolean'?receipt.final_submit_attempted:null,not_saved:!!part?.not_saved,
   answered:!!part&&typeof part==='object'&&(typeof part.state==='string'||typeof value?.ok==='boolean'||typeof value?.error?.code==='string'),
   refs:[part?.key].filter(x=>typeof x==='string'&&/^[a-f0-9]{64}$/.test(x))};
 });
}
export function uncertainState(a,about){
 const state=a.state==='SKIPPED_EXISTING'?a.previous_state:a.state;
 return (about==='post'?['UNKNOWN','SUBMITTED_UNVERIFIED']:about==='edit'?['EDIT_SUBMITTING','EDIT_SUBMITTED_UNVERIFIED']:['DELETE_PENDING']).includes(state);
}
export function settled(a,about){
 return confirmedAnswer(a,about) || (about==='post' && a.state==='SKIPPED_EXISTING' &&
   !a.not_saved && a.code!=='verified_not_saved' &&
   ['SUBMISSION_VERIFIED','EXISTING_VERIFIED','AGENT_REPORTED_VERIFIED'].includes(a.previous_state));
}
export function nothingSent(a,about){
 if(['NOT_STARTED','NEEDS_A_PERSON'].includes(a.state))return true;
 if(about==='edit')return ['EDIT_PREPARING','EDIT_PREPARED','EDIT_UNAVAILABLE','EDIT_MISMATCH'].includes(a.state);
 return about==='post' && STATES.includes(a.state) && !uncertainState(a,about) && a.press_flag===false;
}
export function possiblePress(a,about){
 return Boolean(a.state) && !settled(a,about) && !nothingSent(a,about);
}
export function confirmedAnswer(a,about){return a.state===confirmed[about]&&a.code!=='verified_not_saved'&&!a.not_saved;}

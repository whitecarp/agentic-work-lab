import path from 'node:path';
import {PLATFORMS, schema} from './schema.mjs';
import {publishStartedOn,readAnswers,codeWord,pressRoles,uncertainState,possiblePress,settled,nothingSent,confirmedAnswer} from './answers.mjs';
const actions=new Set(schema.oneOf.find(s=>s.properties.type.const==='STEP').properties.d.properties.action.enum);
// Plain verify can finish pending chapters/widgets, so it remains write-class.
const readActions=new Set(['status','doctor','receipts','stats','works','capabilities','report']);
const localActions=new Set(['plan','snapshot','help']);
export const roles=['publish','repeat_same','resume','verify','edit','delete_dry_run','delete','delete_verify','status','changed_plan','other'];
export function classify(command, flags) {
  if(!command.length) throw new Error('missing_command');
  const base=path.basename(command[0]).toLowerCase().replace(/\.(exe|cmd|bat)$/,'');
  const tool=['vpub','bili','node'].includes(base)?base:'other';
  if(tool!=='other' && flags.tool && flags.tool!==tool) throw new Error('tool_mismatch');
  const action=tool==='vpub' && actions.has(command[1])?command[1]:'other';
  let cls='write';
  if(tool==='vpub'){
    if(readActions.has(action)) cls='read';
    if(localActions.has(action)||command.includes('--help')||command.includes('--version')) cls='local';
    if(action==='audit'&&command[2]==='export')cls='local';
    if(action==='delete'&&command.includes('--dry-run')) cls='local';
    if(['delete','edit'].includes(action)&&command.includes('--verify-only')) cls='read';
  } else if(tool==='other' && flags.tool==='other' && ['read','local'].includes(flags.class)) cls=flags.class;
  if(flags.class && cls!==flags.class) throw new Error('class_mismatch');
  return {tool,action,class:cls};
}
export function guardRole(role, policy, command){
  if(!roles.includes(role)) throw new Error('invalid_role');
  if(policy.tool!=='vpub') return;
  const a=policy.action;
  const expected=a==='delete'?(command.includes('--dry-run')?['delete_dry_run']:command.includes('--verify-only')?['delete_verify']:['delete']):
    a==='edit'?['edit']:a==='resume'?['resume']:a==='verify'?['verify']:['publish','go'].includes(a)?['publish','repeat_same','changed_plan']:null;
  if(expected && !expected.includes(role)) throw new Error('role_mismatch');
}
export function grantScope(records,job,cls,reservations=[],role=null,platforms=[],runClass=null) {
  const grants=records.filter(r=>r.type==='GO_AHEAD'&&r.d.job===job&&r.d.by===r.site&&
    (r.d.scope!=='one_delete'||role===null||r.d.platform===platforms[0]));
  if(cls!=='write')return grants.length?'read_only':null;
  const purpose=role==='edit'?'edit':role==='resume'&&runClass==='real_release'?'resume':'publish';
  const kinds=['listed_test_posts','one_delete','publish','edit','resume'];
  const left=Object.fromEntries(kinds.map(k=>[k,0]));
  for(const r of grants){const k=r.d.scope==='one_write'?(r.d.purpose??'publish'):r.d.scope;if(k in left)left[k]+=r.d.count;}
  for(const r of reservations.filter(r=>r.job===job&&r.class==='write')){
    if(r.scope==='one_delete'&&role!==null&&r.platforms?.[0]!==platforms[0])continue;
    const k=r.scope==='one_write'?(r.purpose??'publish'):r.scope;
    if(k in left)left[k]--;else for(const key in left)left[key]--;
  }
  if(role==='delete')return left.one_delete>0?'one_delete':null;
  if(role===null&&left.one_delete>0)return 'one_delete';
  const listed=['publish','repeat_same','resume','verify','edit','changed_plan'];
  const separate=purpose==='resume'||role==='edit'&&grants.some(r=>r.site==='tester');
  if(!separate&&(role===null||listed.includes(role))&&left.listed_test_posts>0)return 'listed_test_posts';
  return (role===null?['publish','edit','resume'].some(k=>left[k]>0):left[purpose]>0)?'one_write':null;
}

export function grant(records,job,cls,reservations=[],role=null) {
  return grantScope(records,job,cls,reservations,role)!==null;
}
export function safetyStopOn(record, platform) {
 return record.type === 'NOTE' && record.d.platform === platform &&
  ['safety_stop','platform_warning','human_check'].includes(record.d.code);
}
export function validRestartGrant(record, jobId, threshold, start) {
 const at = Date.parse(record.at);
 return record.type === 'GO_AHEAD' && record.d.job === jobId && record.d.by === record.site &&
  ['listed_test_posts','one_write'].includes(record.d.scope) && record.d.count > 0 && at >= threshold && at <= start;
}
export function limits(records,job,role,platforms,now,reservations=[],jobs=[]) {
  if(platforms.some(p=>!PLATFORMS.includes(p))||new Set(platforms).size!==platforms.length) return 'invalid_platforms';
  if(role==='delete' && (platforms.length===0||platforms.includes('bilibili'))) return 'bilibili_delete_forbidden';
  if(job.run_class!=='test_post'||role!=='publish') return null;
  if(!platforms.length || platforms.some(p=>!['youtube','douyin'].includes(p))) return 'test_platform_forbidden';
  const when=Date.parse(now);
  const publishes=records.filter(r=>r.type==='STEP' && r.d.role==='publish');
  const starts=reservations.filter(r=>r.started!==false && r.role==='publish').map(r=>{
    const step=publishes.find(s=>s.run===r.run && s.seq===r.step_seq);
    if(!step)return r;
    const at=Number.isFinite(step.d.ms)?new Date(Date.parse(step.at)-step.d.ms).toISOString():r.at;
    return {...r,at,platforms:r.platforms.filter(p=>publishStartedOn(step,p))};
  });
  // Imported/completed STEP records use end time minus elapsed duration.
  const fallback=publishes.filter(r=>
    !reservations.some(x=>x.run===r.run&&x.step_seq===r.seq))
    .map(r=>({at:new Date(Date.parse(r.at)-r.d.ms).toISOString(),platforms:r.d.platforms.filter(p=>publishStartedOn(r,p)),job:r.d.job,
      run_class:jobs.find(j=>j.id===r.d.job)?.run_class}));
  for(const p of platforms){
    const recent=[...starts,...fallback].filter(s=>s.platforms.includes(p));
    const first=new Map();
    for(const s of recent.filter(s=>s.run_class==='test_post')) {
      const id=s.job??s; // Unlabelled legacy rows cannot prove a shared job.
      if(!first.has(id)||Date.parse(s.at)<Date.parse(first.get(id).at)) first.set(id,s);
    }
    if(!first.has(job.id)&&[...first.values()].filter(s=>when-Date.parse(s.at)<86400000).length>=4) return 'test_post_daily_cap';
    if(!first.has(job.id)&&recent.some(s=>s.job!==job.id&&when-Date.parse(s.at)<1200000)) return 'publish_spacing';
    if(!first.has(job.id)&&records.some(r=>r.type==='NOTE'&&r.d.platform===p&&r.d.code==='outside_upload'&&
        when-(Date.parse(r.at)-r.d.minutes_ago*60000)<1200000)) return 'outside_upload_spacing';
    if(!first.has(job.id)&&records.some(r=>safetyStopOn(r,p)&&
        when-Date.parse(r.at)<172800000)) return 'platform_warning_hold';
    const safety=records.findLast(r=>safetyStopOn(r,p));
    if(!first.has(job.id)&&safety&&!records.some(r=>validRestartGrant(r,job.id,Date.parse(safety.at)+172800000,when)))return 'safety_stop_new_go_ahead_required';
  }
  return null;
}
export function outcome(exit,stdout,{started=true,role,stepClass='write',platform,platforms=platform?[platform]:[]}={}){
  let value;
  const lines=stdout.trim().split(/\r?\n/);
  try{if(lines.length===1)value=JSON.parse(lines[0]);}catch{}
  const data=value?.data??value;
  const about=role==='edit'?'edit':['delete','delete_verify'].includes(role)?'delete':'post';
  const answers=readAnswers(value,platforms,about);
  const mayPress=started && stepClass==='write' && pressRoles.includes(role);
  const code=answers.length===1?answers[0].code:codeWord(value?.error?.code);
  let result=exit===0?'ok':'failed';
  if(exit===2) result='refused';
  if(answers.some(a=>uncertainState(a,about) || (mayPress && possiblePress(a,about))))result='in_doubt';
  else if(answers.length&&answers.every(a=>settled(a,about)))result='ok';
  else if(exit===3 && mayPress && answers.some(a=>!a.state))result='unknown';
  else if(answers.some(a=>settled(a,about)) && answers.some(a=>nothingSent(a,about)))result='partial';
  else if(answers.some(a=>nothingSent(a,about)))result=answers.some(a=>a.code==='stopped')?'stopped':answers.some(a=>['PREPARATION_INCOMPLETE','WAITING_FOR_USER','NEEDS_A_PERSON'].includes(a.state))?'needs_person':'refused';
  else if(exit===3&&pressRoles.includes(role))result='unknown';
  if(!Number.isInteger(exit)) result='unknown';
  const answered=!!value && typeof value==='object' && !Array.isArray(value) &&
    (typeof value.ok==='boolean'||typeof data?.state==='string'||typeof value.error?.code==='string'||!!data?.results);
  if(started&&!answered&&pressRoles.includes(role)) result='unknown';
  const confirmed_state=answers.length===1&&confirmedAnswer(answers[0],about)?answers[0].state:null;
  return {result,code,answered,confirmed_state,answers,rawRefs:[data?.key,...Object.values(data?.results??{}).map(x=>x?.key)].filter(x=>typeof x==='string'&&/^[a-f0-9]{64}$/.test(x))};
}

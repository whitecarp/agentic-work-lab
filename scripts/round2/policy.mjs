import path from 'node:path';
import {PLATFORMS, schema} from './schema.mjs';
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
export function grantScope(records,job,cls,reservations=[],role=null) {
  const grants=records.filter(r=>r.type==='GO_AHEAD'&&r.d.job===job);
  if(cls==='write') {
    const left={listed_test_posts:0,one_write:0};
    for(const r of grants) if(r.d.scope in left) left[r.d.scope]+=r.d.count;
    for(const r of reservations.filter(r=>r.job===job&&r.class==='write')) {
      // Legacy reservations have no provable scope. Debit both pools rather
      // than turn a previously spent unit into new delete authorization.
      if(r.scope in left) left[r.scope]--; else for(const s in left) left[s]--;
    }
    const listed=['publish','repeat_same','resume','verify','edit','changed_plan'];
    if((role===null||listed.includes(role))&&left.listed_test_posts>0) return 'listed_test_posts';
    return left.one_write>0?'one_write':null;
  }
  return grants.length?'read_only':null;
}
export function grant(records,job,cls,reservations=[],role=null) {
  return grantScope(records,job,cls,reservations,role)!==null;
}
export function limits(records,job,role,platforms,now,reservations=[],jobs=[]) {
  if(platforms.some(p=>!PLATFORMS.includes(p))||new Set(platforms).size!==platforms.length) return 'invalid_platforms';
  if(role==='delete' && (platforms.length===0||platforms.includes('bilibili'))) return 'bilibili_delete_forbidden';
  if(job.run_class!=='test_post'||role!=='publish') return null;
  if(!platforms.length || platforms.some(p=>!['youtube','douyin'].includes(p))) return 'test_platform_forbidden';
  const when=Date.parse(now);
  const starts=reservations.filter(r=>r.started!==false && r.role==='publish');
  // Imported/completed STEP records use end time minus elapsed duration.
  const fallback=records.filter(r=>r.type==='STEP'&&r.d.role==='publish'&&
    !reservations.some(x=>x.run===r.run&&x.step_seq===r.seq))
    .map(r=>({at:new Date(Date.parse(r.at)-r.d.ms).toISOString(),platforms:r.d.platforms,job:r.d.job,
      run_class:jobs.find(j=>j.id===r.d.job)?.run_class}));
  for(const p of platforms){
    const recent=[...starts,...fallback].filter(s=>s.platforms.includes(p));
    const first=new Map();
    for(const s of recent.filter(s=>s.run_class==='test_post')) {
      const id=s.job??s; // Unlabelled legacy rows cannot prove a shared job.
      if(!first.has(id)||Date.parse(s.at)<Date.parse(first.get(id).at)) first.set(id,s);
    }
    if([...first.values()].filter(s=>when-Date.parse(s.at)<86400000).length>=4) return 'test_post_daily_cap';
    if(recent.some(s=>when-Date.parse(s.at)<1200000)) return 'publish_spacing';
    if(records.some(r=>r.type==='NOTE'&&r.d.platform===p&&r.d.code==='outside_upload'&&
        when-(Date.parse(r.at)-r.d.minutes_ago*60000)<1200000)) return 'outside_upload_spacing';
    if(records.some(r=>r.type==='NOTE'&&r.d.platform===p&&['platform_warning','human_check','safety_stop'].includes(r.d.code)&&
        when-Date.parse(r.at)<172800000)) return 'platform_warning_hold';
  }
  return null;
}
export function outcome(exit,stdout,{started=true,role,platform}={}){
  let value;
  const lines=stdout.trim().split(/\r?\n/);
  try{if(lines.length===1)value=JSON.parse(lines[0]);}catch{}
  const supplied=value?.error?.code;
  // Only codes with known operational meanings enter a public STEP.
  const codes=new Set(['stopped','confirmation_required','deletion_already_attempted','already_submitted','inspect_existing_edit',
    'fault_injection_refused','injected_fault','backend_timeout','readback_failed','schedule_unverified','identity_mismatch','busy',
    'file_not_found','invalid_file','internal_error','interrupted','submission_unverified','preparation_incomplete',
    'lock_marker_unreadable','browser_busy']);
  const code=codes.has(supplied)?supplied:null;
  const data=value?.data??value;
  const states=[data?.state,...Object.values(data?.results??{}).map(x=>x?.state)];
  let result=exit===0?'ok':'failed';
  if(exit===2) result='refused';
  if(exit===4) result='in_doubt';
  if(['UNKNOWN','DELETE_PENDING','SUBMITTED_UNVERIFIED','EDIT_SUBMITTED_UNVERIFIED'].some(s=>states.includes(s))) result='in_doubt';
  if(states.includes('PREPARATION_INCOMPLETE')) result='needs_person';
  if(['stopped','interrupted'].includes(code)) result='stopped';
  if(!Number.isInteger(exit)) result='unknown';
  const answered=!!value && typeof value==='object' && !Array.isArray(value) &&
    (typeof value.ok==='boolean'||typeof data?.state==='string'||typeof value.error?.code==='string'||!!data?.results);
  if(started&&!answered&&['publish','edit','delete'].includes(role)) result='unknown';
  const state=data?.results?.[platform]?.state??data?.state;
  const confirmed_state=answered&&exit===0&&value.ok!==false&&
    ['SUBMISSION_VERIFIED','EDIT_VERIFIED','DELETED_VERIFIED'].includes(state)?state:null;
  return {result,code,answered,confirmed_state,rawRefs:[data?.key,...Object.values(data?.results??{}).map(x=>x?.key)].filter(x=>typeof x==='string'&&/^[a-f0-9]{64}$/.test(x))};
}

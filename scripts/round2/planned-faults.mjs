import fs from 'node:fs';
import path from 'node:path';
import {processHandles} from './process-handles.mjs';
import {put,readJournal} from './journal.mjs';
import {readJSON,atomic,sha} from './io.mjs';
import {assertFaultReady} from './fault-setup.mjs';

export const FAULTS={
 end_command_after_press:{mechanism:'wrapper_kill',role:'publish',trigger:'waiting_for_platform_confirmation',tree:false},
 end_tree_after_press:{mechanism:'wrapper_kill',role:'publish',trigger:'waiting_for_platform_confirmation',tree:true},
 readback_timeout:{mechanism:'tool_switch',role:'publish',trigger:'none',switch:'after_press'},
 between_save_and_press:{mechanism:'tool_switch',role:'publish',trigger:'none',switch:'before_press'},
 browser_lost_during_upload:{mechanism:'wrapper_kill',role:'publish',trigger:'uploading',tree:true},
 stop_during_upload:{mechanism:'stop_file',role:'publish',trigger:'uploading'},
 delete_readback_lost:{mechanism:'tool_switch',role:'delete',trigger:'none',switch:'delete_readback_lost'},
 end_browser_after_edit_save:{mechanism:'wrapper_kill',role:'edit',trigger:'reading_back',tree:true,afterPress:true},
};
const permitted=new Set(['youtube_private','youtube_unlisted','douyin_private']);
const supported=JSON.parse(fs.readFileSync(new URL('../../templates/round2/crosscast-fault-support.json',import.meta.url)));
export function verifyFaultSupport(executable,expected=supported){
 // Only the audited venv layout is supported. Read code, never sign-in data.
 const folder=path.resolve(path.dirname(executable),'..','Lib','site-packages','video_publisher');
 try{for(const [name,digest]of Object.entries(expected.files)){
  const file=path.join(folder,name);if(fs.lstatSync(file).isSymbolicLink()||sha(fs.readFileSync(file).toString('utf8').replaceAll('\r\n','\n'))!==digest)return false;
 }return Object.keys(expected.files).length>0;}catch{return false;}
}
export function selectFault(ctx,job,role,policy){
 if(policy.tool!=='vpub'||job.run_class!=='test_post')return null;
 const file=path.join(ctx.home,'planned-runs.json');
 if(!fs.existsSync(file))return null;
 if(fs.lstatSync(file).isSymbolicLink())throw new Error('fault_plan_invalid');
 const bytes=fs.readFileSync(file),plan=JSON.parse(bytes),records=readJournal(ctx.journal);
 const seal=records.findLast(r=>r.type==='PROTOCOL_SEAL')?.d;
 const sealed=seal?.plan_sha256;
 if(sealed&&sha(bytes)!==sealed)throw new Error('fault_plan_invalid');
 if(!Array.isArray(plan.runs))throw new Error('fault_plan_invalid');
 const matches=plan.runs.filter(r=>r.id===job.id);if(matches.length>1)throw new Error('fault_plan_invalid');
 const row=matches[0];if(!row)return null;
 const choices=(row.faults??[]).filter(f=>f.role===role);
 if(choices.length>1)throw new Error('fault_plan_invalid');
 const fault=choices[0];
 if(!fault||!permitted.has(fault.armed_on)||records.some(r=>r.type==='FAULT'&&r.d.job===job.id&&r.d.role===role))return null;
 if(seal?.phase==='2b'&&!sealed)throw new Error('fault_plan_invalid');
 const rule=FAULTS[fault.kind];
 if(!rule||rule.role!==role||rule.trigger!==fault.trigger||rule.mechanism!==fault.mechanism||
   ![0,500,2000].includes(fault.delay_ms)||row.run_class!=='test_post'||row.site!==ctx.site||
   row.platforms?.length!==1||job.platforms?.length!==1||row.platforms[0]!==job.platforms[0]||
   `${row.platforms[0]}_${row.visibility}`!==fault.armed_on||row.visibility!==job.visibility)throw new Error('fault_plan_invalid');
 if(rule.tree&&records.some(r=>r.type==='FAULT_PLATFORM_POLICY'&&r.d.platform===row.platforms[0]&&r.d.enabled===false)){
  if(!records.some(r=>r.type==='FAULT_SKIPPED'&&r.d.job===job.id&&r.d.role===role))put(ctx,'FAULT_SKIPPED',{job:job.id,role,kind:fault.kind,platform:row.platforms[0],reason:'browser_faults_disabled'},ctx.run);
  return null;
 }
 return {job:job.id,...fault};
}
export function armFault(ctx,fault,executable,supportCheck=verifyFaultSupport){
 if(!fault)return;
 if(!supportCheck(executable))throw new Error('fault_switch_unavailable');
 // Immediately before the durable arming record/spawn; re-read actual setup.
 assertFaultReady(ctx,fault.kind);
 put(ctx,'FAULT',fault,ctx.run);
}
export const faultVariable=f=>f?(FAULTS[f.kind].switch??'external'):null;
export function injectedReply(stdout,platform){
 const lines=stdout.trim().split(/\r?\n/);if(lines.length!==1)return false;
 try{const value=JSON.parse(lines[0]),data=value?.data??value;
  const part=data?.results?.[platform];
  return [value?.error?.code,data?.error?.code,data?.code,part?.error?.code,part?.code].includes('injected_fault');
 }catch{return false;}
}

// Birth-time checks exclude old orphans with a recycled parent number.
export function family(rows,root){
 const first=rows.find(r=>r.pid===root);if(!first)throw new Error('starter_missing');
 const selected=new Map([[root,first]]);let changed=true;
 while(changed){changed=false;for(const r of rows){const parent=selected.get(r.parent);
  if(!parent||selected.has(r.pid)||BigInt(r.born)<BigInt(parent.born))continue;
  selected.set(r.pid,r);changed=true;
 }}return [...selected.values()];
}
const pause=ms=>new Promise(r=>setTimeout(r,ms));
const category=r=>/^python(?:w)?(?:\.exe)?$/i.test(r.image)?'python':/^node(?:\.exe)?$/i.test(r.image)?'driver':/chrome|chromium/i.test(r.image)?'chromium':'other';
export function faultController(ctx,fault,{snapshot,kill}={}){
 if(!fault)return null;
 const rule=FAULTS[fault.kind],created=Date.now();
 let child,observer,rootReady=Promise.resolve(),rootIdentity,press=false,triggered=false,fired=false,failed=false,pending=Promise.resolve(),partial='',ended=[];
 let requestMs=null,goneMs=null,killExit=null,reason=null,childEnded=false;
 const running=()=>child&&!childEnded&&child.exitCode==null&&child.signalCode==null;
 const rowsOf=rows=>rows.map(r=>({pid:r.pid,parent:r.parent,root:r.pid===child.pid,...(r.born?{born:r.born}:{}),kind:r.pid===child.pid?'launcher':category(r),ended:r.ended??null}));
 const api={
  attach(c){
   child=c;c.once('exit',()=>{childEnded=true;});
   if(rule.tree){
    if(!snapshot){if(process.platform!=='win32')throw new Error('owned_handle_controller_unavailable');observer=processHandles();}
    c.once('spawn',()=>{rootReady=(snapshot?snapshot():observer.init(c.pid,created)).then(rows=>{rootIdentity=rows.find(r=>r.pid===c.pid);if(!rootIdentity)throw new Error('starter_missing');}).catch(e=>{reason=e.message;});});
   }
  },
  line(line){
   let packet;try{packet=JSON.parse(line);}catch{return;}
   if(packet?.event!=='browser_progress'||packet.platform!==fault.armed_on.split('_')[0])return;
   if(packet.stage==='pressing')press=true;
   if(triggered||rule.mechanism==='tool_switch'||packet.stage!==rule.trigger.replaceAll('_',' ')||(rule.afterPress&&!press))return;
   triggered=true;const start=performance.now();
   pending=(async()=>{
    await pause(fault.delay_ms);
    if(!running()){reason='command_finished_before_request';return;}
    if(rule.mechanism==='stop_file'){
     requestMs=Math.round(performance.now()-start);
     put(ctx,'STOP_SET',{by:'scheduler'},ctx.run);atomic(path.join(ctx.home,'STOP'),'planned fault\n');fired=true;return;
    }
    if(!rule.tree){
     // Node retains the exact child handle: no PID walk or snapshot delay.
     requestMs=Math.round(performance.now()-start);
     const sent=kill?await kill(child.pid,false,child):child.kill('SIGKILL');
     killExit=typeof sent==='number'?sent:sent?0:1;
     if(sent===false){reason=running()?'terminate_handle_failed':'command_finished_before_request';failed=running();fired=failed?null:false;return;}
     for(let i=0;i<100&&running();i++)await pause(10);
     fired=!running();failed=!fired;reason=failed?'starter_not_observed_gone':null;
     goneMs=fired?Math.round(performance.now()-start):null;
     ended=[{pid:child.pid,root:true,kind:'launcher',ended:fired}];return;
    }
    await rootReady;
    if(!running()){reason='command_finished_before_request';return;}
    if(!rootIdentity)throw new Error(reason??'starter_identity_missing');
    const before=snapshot?await snapshot():await observer.scan();
    const root=before.find(r=>r.pid===child.pid&&r.born===rootIdentity.born&&!r.ended);
    if(!root)throw new Error('starter_missing');
    const owned=family(before,child.pid);ended=rowsOf(owned);
    requestMs=Math.round(performance.now()-start);
    if(kill){killExit=await kill(child.pid,true,child,owned);const after=await snapshot();ended=ended.map(r=>({...r,ended:!after.some(a=>a.pid===r.pid&&a.born===r.born&&!a.zombie)}));}
    else{const after=await observer.end();killExit=0;ended=rowsOf(after);}
    fired=ended.length>0&&ended.every(r=>r.ended);failed=!fired;
    reason=failed?'tree_not_observed_gone':null;goneMs=fired?Math.round(performance.now()-start):null;
   })().catch(e=>{failed=true;fired=null;reason=['starter_missing','starter_identity_missing','process_identity_changed','process_snapshot_failed','terminate_handle_failed','owned_handle_controller_unavailable'].includes(e.message)?e.message:'process_controller_failed';});
  },
  receive(bytes){partial+=bytes.toString('utf8');let i;while((i=partial.indexOf('\n'))!==-1){api.line(partial.slice(0,i));partial=partial.slice(i+1);}if(partial.length>8192)partial='';},
  async finish(stdout){
   try{if(partial)api.line(partial);await rootReady;await pending;
    if(rule.mechanism==='tool_switch')fired=injectedReply(stdout,fault.armed_on.split('_')[0]);
    put(ctx,'FAULT_RESULT',{job:fault.job,kind:fault.kind,role:fault.role,triggered,fired,failed,reason,
     action_delay_ms:requestMs,gone_delay_ms:goneMs,kill_exit:killExit,processes:ended},ctx.run,30000);
    return {fired,failed,reason};
   }finally{observer?.close();}
  }
 };return api;
}

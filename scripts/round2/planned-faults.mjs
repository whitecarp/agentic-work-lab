import fs from 'node:fs';
import path from 'node:path';
import {execFile} from 'node:child_process';
import {promisify} from 'node:util';
import {put,readJournal} from './journal.mjs';
import {readJSON,atomic,sha} from './io.mjs';
import {assertFaultReady} from './fault-setup.mjs';

const exec=promisify(execFile);
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
 if(['2a','2b'].includes(seal?.phase)&&!sealed)throw new Error('fault_plan_invalid');
 const rule=FAULTS[fault.kind];
 if(!rule||rule.role!==role||rule.trigger!==fault.trigger||rule.mechanism!==fault.mechanism||
   ![0,500,2000].includes(fault.delay_ms)||row.run_class!=='test_post'||row.site!==ctx.site||
   row.platforms?.length!==1||job.platforms?.length!==1||row.platforms[0]!==job.platforms[0]||
   `${row.platforms[0]}_${row.visibility}`!==fault.armed_on||row.visibility!==job.visibility)throw new Error('fault_plan_invalid');
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

// Enumerate only identity/parent fields, never command lines or account files.
async function processes(){
 if(process.platform==='win32'){
  const {stdout}=await exec('powershell.exe',['-NoProfile','-NonInteractive','-Command',
   'Get-CimInstance Win32_Process | Select-Object Name,ProcessId,ParentProcessId,@{n="born";e={$_.CreationDate.ToUniversalTime().Ticks.ToString()}} | ConvertTo-Json -Compress'],
   {windowsHide:true,maxBuffer:4*1024*1024,timeout:15000});
  const rows=JSON.parse(stdout);return (Array.isArray(rows)?rows:[rows]).map(r=>({pid:r.ProcessId,parent:r.ParentProcessId,born:r.born,image:r.Name}));
 }
 if(process.platform==='linux')return fs.readdirSync('/proc').filter(n=>/^\d+$/.test(n)).flatMap(n=>{
  try{const s=fs.readFileSync(`/proc/${n}/stat`,'utf8').split(') ')[1].split(' ');return [{pid:+n,parent:+s[1],born:s[19],zombie:s[0]==='Z'}];}catch{return [];}
 });
 throw new Error('owned_tree_inspection_unavailable');
}
const pause=ms=>new Promise(r=>setTimeout(r,ms));
function family(rows,root){const selected=new Map([[root,rows.find(r=>r.pid===root)]]);let changed=true;while(changed){changed=false;for(const r of rows)if(selected.has(r.parent)&&!selected.has(r.pid)){
 // An orphan retaining a recycled parent PID is not owned by this invocation.
 if(BigInt(r.born)<BigInt(selected.get(r.parent).born))throw new Error('ambiguous_process_parent');
 selected.set(r.pid,r);changed=true;
 }}return [...selected.values()];}
async function terminate(pid,tree,child){
 if(process.platform==='win32'){
  try{await exec('taskkill.exe',['/PID',String(pid),...(tree?['/T']:[]),'/F'],{windowsHide:true,timeout:15000});return 0;}
  catch(e){return Number.isInteger(e.code)?e.code:null;}
 }
 if(tree)process.kill(-pid,'SIGKILL');else child.kill('SIGKILL');
 return 0;
}
export function faultController(ctx,fault,{snapshot=processes,kill=terminate}={}){
 if(!fault)return null;
 const rule=FAULTS[fault.kind];let child,rootIdentity,rootReady=Promise.resolve(),press=false,triggered=false,fired=false,failed=false,pending=Promise.resolve(),ended=[],partial='';
 let measured=null,killExit=null;
 const api={
  attach(c){child=c;if(rule.mechanism==='wrapper_kill')c.once('spawn',()=>{rootReady=snapshot().then(rows=>{rootIdentity=rows.find(r=>r.pid===c.pid);}).catch(()=>{failed=true;});});},
  line(line){
   // CrossCast progress.show emits JSON, not a bare word. Ignore unrelated
   // stderr, another platform and arbitrary strings containing the trigger.
   let packet;try{packet=JSON.parse(line);}catch{return;}
   if(packet?.event!=='browser_progress'||packet.platform!==fault.armed_on.split('_')[0])return;
   const stage=packet.stage;
   if(stage==='pressing')press=true;
   if(triggered||rule.mechanism==='tool_switch'||stage!==rule.trigger.replaceAll('_',' ')||(rule.afterPress&&!press))return;
   triggered=true;
   const start=performance.now();
   pending=(async()=>{
    if(rule.mechanism==='stop_file'){
     await pause(fault.delay_ms);
     put(ctx,'STOP_SET',{by:'scheduler'},ctx.run);
     measured=Math.round(performance.now()-start);
     atomic(path.join(ctx.home,'STOP'),'planned fault\n');fired=true;return;
    }
    const [before]=await Promise.all([snapshot(),rootReady,pause(fault.delay_ms)]);
    if(failed||!rootIdentity){failed=true;fired=null;return;}
    if(child.exitCode!==null||child.signalCode!==null)return;
    const root=before.find(r=>r.pid===child.pid&&r.born===rootIdentity.born);
    if(!root)return;
    const owned=family(before,child.pid);
    const category=r=>r.pid===root.pid?'launcher':/^python(?:w)?(?:\.exe)?$/i.test(r.image)?'python':
      /^node(?:\.exe)?$/i.test(r.image)?'driver':/chrome|chromium/i.test(r.image)?'chromium':'other';
    ended=owned.map(r=>({pid:r.pid,parent:r.parent,root:r.pid===root.pid,born:r.born,kind:category(r),ended:null}));
    measured=Math.round(performance.now()-start);
    killExit=await kill(root.pid,rule.tree,child);
    // Also observe descendants of the launcher for a command-only trial.
    // A nonzero taskkill code can coincide with an already-ended member.
    if(!rule.tree)await pause(2200);
    for(let n=0;n<4;n++){
     const after=await snapshot();
     ended=ended.map(r=>({...r,ended:!after.some(a=>a.pid===r.pid&&a.born===r.born&&!a.zombie)}));
     const targets=ended.filter(r=>rule.tree||r.root);
     if(targets.every(r=>r.ended)){fired=true;return;}
     if(n<3)await pause(200);
    }
    failed=true;
    fired=ended.filter(r=>rule.tree||r.root).some(r=>r.ended)?null:false;
   })().catch(()=>{failed=true;fired=null;});
  },
  receive(bytes){partial+=bytes.toString('utf8');let i;while((i=partial.indexOf('\n'))!==-1){api.line(partial.slice(0,i));partial=partial.slice(i+1);}if(partial.length>8192)partial='';},
  async finish(stdout){if(partial)api.line(partial);await rootReady;await pending;if(rule.mechanism==='tool_switch')fired=injectedReply(stdout,fault.armed_on.split('_')[0]);
   put(ctx,'FAULT_RESULT',{job:fault.job,kind:fault.kind,role:fault.role,triggered,fired:failed&&!triggered?null:fired,failed,action_delay_ms:measured,kill_exit:killExit,processes:ended},ctx.run);
   return {fired:failed&&!triggered?null:fired,failed};
  }
 };return api;
}

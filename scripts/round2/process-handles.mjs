import {spawn} from 'node:child_process';
import fs from 'node:fs';
// One helper owns verified process handles for one child invocation. No name
// targeting and no recursive PID-based termination is available here.
export function processHandles({readOnly=false}={}){
 const child=spawn('powershell.exe',['-NoProfile','-NonInteractive','-Command',fs.readFileSync(new URL('./process-handles.ps1',import.meta.url),'utf8')],{stdio:['pipe','pipe','pipe'],windowsHide:true});
 let serial=0,buffer='',closed=false,unopened=0,unopenedPresent=0;const pending=new Map();
 const fail=()=>{closed=true;for(const p of pending.values()){clearTimeout(p.timer);p.reject(new Error('process_helper_closed'));}pending.clear();};
 child.on('error',fail);child.on('exit',fail);child.stderr.resume();
 child.stdout.on('data',data=>{buffer+=data;let i;while((i=buffer.indexOf('\n'))>=0){const line=buffer.slice(0,i);buffer=buffer.slice(i+1);let r;try{r=JSON.parse(line);}catch{continue;}const p=pending.get(r.id);if(!p)continue;pending.delete(r.id);clearTimeout(p.timer);if(r.ok){unopened=r.unopened;unopenedPresent=r.unopened_present;}r.ok?p.resolve(r.rows):p.reject(new Error(r.reason));}});
 function ask(op,args={}){return new Promise((resolve,reject)=>{
  if(closed)return reject(new Error('process_helper_closed'));const id=++serial;
  const timer=readOnly?setTimeout(()=>{pending.delete(id);reject(new Error('process_observation_timeout'));},5000):undefined;
  pending.set(id,{resolve,reject,timer});child.stdin.write(JSON.stringify({id,op,...args})+'\n',e=>{if(e){pending.delete(id);clearTimeout(timer);reject(new Error('process_helper_closed'));}});
 });}
 return {get unopened(){return unopened;},get unopenedPresent(){return unopenedPresent;},init:(root,earliest)=>ask('init',{root,parent:process.pid,earliest:String(BigInt(Math.floor(earliest))*10000n+116444736000000000n),read_only:readOnly}),scan:()=>ask('scan'),end:()=>readOnly?Promise.reject(new Error('observation_only')):ask('end'),
  close(){if(!closed)child.stdin.end(JSON.stringify({id:++serial,op:'close'})+'\n');}};
}

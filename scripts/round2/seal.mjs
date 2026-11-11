import fs from 'node:fs';
import path from 'node:path';
import {pathToFileURL} from 'node:url';
import {readJournal,verifyJournal,canonical,ZERO,context} from './journal.mjs';
import {sha,atomic,withLock,parseFlags,allowed} from './io.mjs';

export const TYPES=['PROTOCOL_SEAL','GATE_DECISION','GATE_ERROR','GO_AHEAD','STEP','STEP_REFUSED','RUN_END','JOB_END','OBSERVATION','FAULT','FAULT_RESULT','FAULT_SETUP','REAL_RELEASE','STOP_SET','STOP_CLEARED','HANDOFF','SIGN_OFF','VIOLATION','NOTE'];
export function day(value){
 if(typeof value!=='string'||!/^\d{4}-\d{2}-\d{2}$/.test(value)||!Number.isFinite(Date.parse(value))||new Date(value).toISOString().slice(0,10)!==value)throw new Error('invalid_day');
 return value;
}
export function counts(records){const result={};for(const r of records)result[r.type]=(result[r.type]??0)+1;return result;}
const dir=ctx=>path.join(path.dirname(ctx.journal),'seals');
function prefixHead(records,n){return n?sha(canonical(records[n-1])):ZERO;}
function shape(s){
 if(!s||Object.keys(s).sort().join(',')!=='counts,day,head_sha256,previous_sha256,records,tools')throw new Error('invalid_seal');
 day(s.day);
 if(!Number.isSafeInteger(s.records)||s.records<0||![s.head_sha256,s.previous_sha256].every(x=>typeof x==='string'&&/^[a-f0-9]{64}$/.test(x)))throw new Error('invalid_seal');
 if(!s.tools||Object.keys(s.tools).sort().join(',')!=='journal,node'||s.tools.journal!=='1'||!/^\d+\.\d+\.\d+(?:-[0-9A-Za-z.-]+)?$/.test(s.tools.node))throw new Error('invalid_seal');
 if(!s.counts||Object.entries(s.counts).some(([k,v])=>!TYPES.includes(k)||!Number.isSafeInteger(v)||v<0))throw new Error('invalid_seal');
}
export function verifySeals(ctx,records=readJournal(ctx.journal)){
 const directory=dir(ctx),headFile=path.join(directory,'.head');
 if(!fs.existsSync(directory))return {seals:[],sealed_records:0,unsealed_records:records.length,head_sha256:ZERO};
 const names=fs.readdirSync(directory).filter(n=>n.endsWith('.json')).sort();
 if(names.some(n=>!/^\d{4}-\d{2}-\d{2}\.json$/.test(n)))throw new Error('invalid_seal_name');
 const anchor=fs.existsSync(headFile)?JSON.parse(fs.readFileSync(headFile)):null;
 if(names.length&&!anchor)throw new Error('seal_anchor_missing');
 let previous=ZERO,number=0;const seals=[],hashes={};
 for(const name of names){
   const file=path.join(directory,name);if(fs.lstatSync(file).isSymbolicLink())throw new Error('linked_seal');
   const bytes=fs.readFileSync(file),s=JSON.parse(bytes);shape(s);
   if(bytes.toString('utf8')!==canonical(s)+'\n'||s.day+'.json'!==name||s.previous_sha256!==previous||s.records<number||s.records>records.length||s.head_sha256!==prefixHead(records,s.records)||canonical(s.counts)!==canonical(counts(records.slice(0,s.records)))||records.slice(0,s.records).some(r=>r.at.slice(0,10)>s.day))throw new Error('seal_mismatch');
   previous=sha(bytes);hashes[name]=previous;number=s.records;seals.push(s);
 }
 if(anchor&&(Object.keys(anchor).sort().join(',')!=='files,head_sha256'||canonical(anchor.files)!==canonical(hashes)||anchor.head_sha256!==previous))throw new Error('seal_anchor_mismatch');
 return {seals,sealed_records:number,unsealed_records:records.length-number,head_sha256:previous};
}
export function seal(ctx,selectedDay=new Date().toISOString().slice(0,10)){
 day(selectedDay);
 return withLock(ctx.journal+'.lock',()=>{
  const records=readJournal(ctx.journal),existing=verifySeals(ctx,records);
  if(records.some(r=>r.at.slice(0,10)>selectedDay))throw new Error('seal_day_before_records');
  const found=existing.seals.find(s=>s.day===selectedDay);
  const previous=found?found.previous_sha256:existing.head_sha256;
  const value={day:selectedDay,records:records.length,head_sha256:verifyJournal(ctx.journal).head_sha256,counts:counts(records),previous_sha256:previous,tools:{journal:'1',node:process.versions.node}};
  if(found){if(canonical(found)!==canonical(value))throw new Error('seal_would_change');return found;}
  if(existing.seals.at(-1)?.day>=selectedDay)throw new Error('seal_out_of_order');
  const directory=dir(ctx);fs.mkdirSync(directory,{recursive:true});
  const file=path.join(directory,selectedDay+'.json'),bytes=canonical(value)+'\n';
  const fd=fs.openSync(file,'wx',0o600);try{fs.writeFileSync(fd,bytes);fs.fsyncSync(fd);}finally{fs.closeSync(fd);}
  const hashes=Object.fromEntries(fs.readdirSync(directory).filter(n=>/^\d{4}-\d{2}-\d{2}\.json$/.test(n)).sort().map(n=>[n,sha(fs.readFileSync(path.join(directory,n)))]));
  atomic(path.join(directory,'.head'),{files:hashes,head_sha256:sha(bytes)});
  return value;
 });
}
export function main(argv=process.argv.slice(2)){
 const {flags:f,command}=parseFlags(argv);allowed(f,['site','slot','journal','day']);if(command.length)throw new Error('invalid_arguments');
 console.log(JSON.stringify(seal(context(f,false),f.day)));return 0;
}
if(process.argv[1]&&import.meta.url===pathToFileURL(path.resolve(process.argv[1])).href){try{process.exitCode=main();}catch{console.error('seal_refused');process.exitCode=70;}}

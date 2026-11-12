import fs from 'node:fs';
import path from 'node:path';
import {pathToFileURL} from 'node:url';
import {context,operator,readJournal,put,keyedDigest,words} from './journal.mjs';
import {bindHome} from './site.mjs';
import {refusalWord,withLock,parseFlags,allowed,readJSON,atomic} from './io.mjs';

const normalized=p=>process.platform==='win32'?p.toLowerCase():p;
function inspect(ctx,records){
 const pinned=bindHome(ctx,records),file=path.join(pinned.home,'config.json');
 if(fs.lstatSync(file).isSymbolicLink())throw new Error('fault_setup_unconfirmed');
 const config=readJSON(file),expected=path.join(ctx.home,'STOP');
 const registered=typeof config.external_stop_file==='string'&&path.isAbsolute(config.external_stop_file)&&
   normalized(path.resolve(config.external_stop_file))===normalized(expected);
 return {enabled:config.fault_injection===true,registered,
   stop_digest:keyedDigest('round2-fault-stop-v1\0'+normalized(expected),ctx.home,{create:false})};
}
// C15 must call this immediately before arming; a record alone is insufficient.
export function assertFaultReady(ctx,kind){
 const records=readJournal(ctx.journal),last=records.filter(r=>r.type==='FAULT_SETUP').at(-1),actual=inspect(ctx,records);
 if(!['end_command_after_press','end_tree_after_press','readback_timeout','between_save_and_press',
   'browser_lost_during_upload','stop_during_upload','delete_readback_lost','end_browser_after_edit_save'].includes(kind)||
   !last||last.d.configured!==true||last.d.enabled!==true||!actual.enabled||last.d.stop_digest!==actual.stop_digest||
   !last.d.stop_registered||!actual.registered)throw new Error('fault_setup_unconfirmed');
 return actual;
}
export function recordSetup(ctx,{enabled,by},statement){
 throw new Error('configure_required');
}
// Operator-only setup/cleanup. Tests call this only with empty synthetic homes.
// Prior values stay private; only checked booleans and a keyed path digest travel.
export function configureSetup(ctx,{enabled,by},statement){
 if(by!==ctx.site||typeof enabled!=='boolean')throw new Error('fault_setup_unconfirmed');
 return withLock(path.join(ctx.home,'operation.guard'),()=>{
  const records=readJournal(ctx.journal);
  if(ctx.slot!==operator(ctx,records))throw new Error('not_operator');
  const handoff=records.findLast(r=>r.type==='HANDOFF');
  if(handoff?.d.job)throw new Error('temporary_operator_cannot_configure');
  const pinned=bindHome(ctx,records),file=path.join(pinned.home,'config.json');
  if(readJSON(path.join(ctx.home,'run.lock'),null))throw new Error('fault_setup_during_session');
  const before=inspect(ctx,records),config=readJSON(file),backup=path.join(ctx.home,'fault-config-original.json');
  if(enabled && config.external_stop_file!=null && config.external_stop_file!=='' && !before.registered)
    throw new Error('external_stop_already_registered');
  const digest=statement();
  if(enabled){
   if(fs.existsSync(backup)||before.enabled||before.registered)throw new Error('fault_setup_already_enabled');
   const prior={};for(const key of ['external_stop_file','fault_injection'])if(Object.hasOwn(config,key))prior[key]=config[key];
   const fd=fs.openSync(backup,'wx',0o600);try{fs.writeFileSync(fd,JSON.stringify(prior));fs.fsyncSync(fd);}finally{fs.closeSync(fd);}
   config.external_stop_file=path.join(ctx.home,'STOP');config.fault_injection=true;
  }else{
   if(fs.lstatSync(backup).isSymbolicLink()||!before.enabled||!before.registered)throw new Error('fault_setup_changed');
   const prior=readJSON(backup);for(const key of ['external_stop_file','fault_injection']){delete config[key];if(Object.hasOwn(prior,key))config[key]=prior[key];}
  }
  atomic(file,config);const actual=inspect(ctx,records);
  const r=put(ctx,'FAULT_SETUP',{enabled:actual.enabled,stop_registered:actual.registered,stop_digest:actual.stop_digest,by,words_sha256:digest,configured:true});
  // Preserve the original settings as evidence; a second setup requires review.
  return r;
 });
}
export function main(argv=process.argv.slice(2)){
 const configure=argv[0]==='configure';if(configure)argv.shift();
 const {flags:f,command}=parseFlags(argv);allowed(f,['site','slot','journal','enabled','by']);
 if(command.length||!['true','false'].includes(f.enabled))throw new Error('invalid_arguments');
 const ctx=context(f);
 console.log(JSON.stringify((configure?configureSetup:recordSetup)(ctx,{enabled:f.enabled==='true',by:f.by??ctx.site},()=>words(ctx.home))));return 0;
}
if(process.argv[1]&&import.meta.url===pathToFileURL(path.resolve(process.argv[1])).href){
 try{process.exitCode=main();}catch(e){console.error(refusalWord(e,'fault_setup_unconfirmed'));process.exitCode=70;}
}

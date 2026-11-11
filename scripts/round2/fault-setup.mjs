import fs from 'node:fs';
import path from 'node:path';
import {pathToFileURL} from 'node:url';
import {context,readJournal,put,keyedDigest,words} from './journal.mjs';
import {bindHome} from './site.mjs';
import {withLock,parseFlags,allowed,readJSON} from './io.mjs';

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
   !last||last.d.enabled!==true||!actual.enabled||last.d.stop_digest!==actual.stop_digest||
   !last.d.stop_registered||!actual.registered)throw new Error('fault_setup_unconfirmed');
 return actual;
}
export function recordSetup(ctx,{enabled,by},statement){
 if(by!==ctx.site||typeof enabled!=='boolean')throw new Error('fault_setup_unconfirmed');
 return withLock(path.join(ctx.home,'operation.guard'),()=>{
  const actual=inspect(ctx,readJournal(ctx.journal));
  if(actual.enabled!==enabled||actual.registered!==enabled)throw new Error('fault_setup_unconfirmed');
  return put(ctx,'FAULT_SETUP',{enabled,stop_registered:actual.registered,stop_digest:actual.stop_digest,by,
    words_sha256:statement()});
 });
}
export function main(argv=process.argv.slice(2)){
 const {flags:f,command}=parseFlags(argv);allowed(f,['site','slot','journal','enabled','by']);
 if(command.length||!['true','false'].includes(f.enabled))throw new Error('invalid_arguments');
 const ctx=context(f);
 console.log(JSON.stringify(recordSetup(ctx,{enabled:f.enabled==='true',by:f.by??ctx.site},()=>words(ctx.home))));return 0;
}
if(process.argv[1]&&import.meta.url===pathToFileURL(path.resolve(process.argv[1])).href){
 try{process.exitCode=main();}catch{console.error('fault_setup_unconfirmed');process.exitCode=70;}
}

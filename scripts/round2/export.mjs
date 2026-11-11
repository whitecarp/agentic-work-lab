import fs from 'node:fs';
import path from 'node:path';
import {pathToFileURL} from 'node:url';
import {readJournal,verifyJournal,canonical,context} from './journal.mjs';
import {verifySeals,counts} from './seal.mjs';
import {crosscast} from './crosscast.mjs';
import {atomic,parseFlags,allowed,withLock} from './io.mjs';

export function switches(argv,names){
 const set=new Set();const rest=argv.filter(a=>{if(!names.includes(a))return true;if(set.has(a))throw new Error('duplicate_flag');set.add(a);return false;});
 return {set,...parseFlags(rest)};
}
function strings(value){return typeof value==='string'?[value]:Array.isArray(value)?value.flatMap(strings):value&&typeof value==='object'?Object.values(value).flatMap(strings):[];}
export function exportData(ctx,{privacy=false,deny,crosscast:ccfile}={}){
 const records=readJournal(ctx.journal),head=verifyJournal(ctx.journal),seals=verifySeals(ctx,records),cc=crosscast(ccfile);
 const forbidden=deny?fs.readFileSync(deny,'utf8').split(/\r?\n/).filter(Boolean):[];
 const result={schema:'round2-export/1',privacy,source:{records:head.records,head_sha256:head.head_sha256,chain:'sha256_without_lf',derived_records:privacy},
  counts:counts(records),records:structuredClone(records),seals:seals.seals,unsealed_records:seals.unsealed_records,crosscast_present:cc.present,crosscast_chain_ok:cc.chain_ok,crosscast_command_losses:cc.losses};
 // Check the originals too: privacy coarsening must not hide a denied string.
 if(strings(result).some(s=>forbidden.some(word=>s.includes(word))))throw new Error('private_string_refused');
 if(privacy)for(const r of result.records){
  r.at=r.at.slice(0,16)+':00.000Z';
  if(r.type==='GATE_DECISION')r.d.now=r.d.now.slice(0,16)+':00.000Z';
  if(r.type==='OBSERVATION')delete r.d.minutes_after;
 }
 return result;
}
export function exportFile(ctx,out,options={}){
 const destination=path.resolve(out),publicRoot=path.dirname(path.resolve(ctx.journal));
 if(destination===publicRoot||destination.startsWith(publicRoot+path.sep)||ctx.home&&(destination===ctx.home||destination.startsWith(ctx.home+path.sep)))throw new Error('export_destination_is_evidence');
 return withLock(ctx.journal+'.lock',()=>{
  const result=exportData(ctx,options);atomic(destination,canonical(result)+'\n');return {records:result.records.length,counts:result.counts,unsealed_records:result.unsealed_records};
 });
}
export function main(argv=process.argv.slice(2)){
 const {flags:f,command,set}=switches(argv,['--privacy']);allowed(f,['site','journal','out','deny','crosscast']);if(command.length||!f.out)throw new Error('invalid_arguments');
 const ctx=context(f,false);if(process.env.ROUND2_HOME)ctx.home=fs.realpathSync(process.env.ROUND2_HOME);
 console.log(JSON.stringify(exportFile(ctx,f.out,{privacy:set.has('--privacy'),deny:f.deny,crosscast:f.crosscast})));return 0;
}
if(process.argv[1]&&import.meta.url===pathToFileURL(path.resolve(process.argv[1])).href){try{process.exitCode=main();}catch{console.error('export_refused');process.exitCode=70;}}

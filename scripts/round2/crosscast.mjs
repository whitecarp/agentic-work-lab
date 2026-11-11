import fs from 'node:fs';
const hex32=v=>typeof v==='string'&&v.length===32&&/^[a-f0-9]{32}$/.test(v);
const number=v=>Number.isSafeInteger(v)&&v>=0;
const bool=v=>typeof v==='boolean';
export function crosscast(file){
 if(!file)return {present:false,losses:[],integrity:null,runs:[],times:null,chain_ok:null};
 const parsed=JSON.parse(fs.readFileSync(file));
 const value=parsed.schema==='round2-export/1'&&parsed.privacy===true?parsed.crosscast:parsed;
 if(!value)throw new Error('invalid_crosscast_export');
 if(value.schema!=='crosscast-audit/1'||!Array.isArray(value.runs)||!['relative','exact','per_command_or_record'].includes(value.times)||!bool(value.integrity?.chain_ok))throw new Error('invalid_crosscast_export');
 const seen=new Set(),losses=[],integrity={};
 for(const field of ['dropped_events','unplaced_dropped_events','unreadable_loss_records']){
  if(value.integrity[field]!==undefined&&!number(value.integrity[field]))throw new Error('invalid_crosscast_loss');
  integrity[field]=value.integrity[field]??null;
 }
 for(const field of ['dropped_count_is_lower_bound','loss_records_complete','loss_folder_unreadable']){
  if(value.integrity[field]!==undefined&&!bool(value.integrity[field]))throw new Error('invalid_crosscast_loss');
  integrity[field]=value.integrity[field]??null;
 }
 for(const run of value.runs){
  if(!hex32(run.run)||seen.has(run.run))throw new Error('invalid_crosscast_command');seen.add(run.run);
  if(run.awl_run_id!=null&&!hex32(run.awl_run_id))throw new Error('invalid_crosscast_shared_id');
  // Older exports can be read, but unavailable loss evidence stays explicitly null.
  for(const field of ['dropped_events'])if(run[field]!==undefined&&!number(run[field]))throw new Error('invalid_crosscast_loss');
  for(const field of ['sequence_gap','loss_complete','chain_ok'])if(run[field]!==undefined&&!bool(run[field]))throw new Error('invalid_crosscast_loss');
  losses.push({run:run.run,dropped_events:run.dropped_events??null,sequence_gap:run.sequence_gap??null,chain_ok:run.chain_ok??value.integrity.chain_ok,loss_complete:run.loss_complete??false});
 }
 return {present:true,losses,integrity,runs:value.runs,events:Array.isArray(value.events)?value.events:[],times:value.times,chain_ok:value.integrity.chain_ok};
}
export function commandClass(run){
 const read=new Set(['status','doctor','receipts','stats','works','capabilities','report']);
 const local=new Set(['plan','snapshot','help']);
 if(read.has(run.command))return 'read';if(local.has(run.command))return 'local';
 if(run.command==='audit')return 'local';
 if(run.command==='delete'&&run.flags?.dry_run===true)return 'local';
 if(['delete','edit'].includes(run.command)&&run.flags?.verify_only===true)return 'read';
 return 'write'; // Plain verify and unknown commands remain conservative.
}

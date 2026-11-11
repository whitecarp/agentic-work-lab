import path from 'node:path';
import {pathToFileURL} from 'node:url';
import {readJournal,context} from './journal.mjs';
import {seal,verifySeals,day} from './seal.mjs';
import {reconcile} from './reconcile.mjs';
import {switches} from './export.mjs';
import {allowed} from './io.mjs';

export function daily(ctx,{selectedDay=new Date().toISOString().slice(0,10),apply=false,crosscast,now=Date.now()}={}){
 day(selectedDay);const records=readJournal(ctx.journal);verifySeals(ctx,records);
 const current=records.filter(r=>r.at.slice(0,10)===selectedDay),checked=reconcile(ctx,crosscast,{now});
 const gates={},results={};for(const r of current){const target=r.type==='GATE_DECISION'?gates:r.type==='STEP'?results:null;if(target){const key=r.type==='STEP'?r.d.result:r.d.decision;target[key]=(target[key]??0)+1;}}
 const lookCounts={on_time:0,late:0,early:0,pending:0,no_ground_truth:0};
 for(const look of checked.look_checks)lookCounts[look.status]++;
 const lines=[`records_today: ${current.length}`,`gate_decisions: ${JSON.stringify(gates)}`,`steps_by_result: ${JSON.stringify(results)}`,`open_in_doubt: ${checked.in_doubt_open}`,`violations: ${current.filter(r=>r.type==='VIOLATION').length}; looks_all_records: ${JSON.stringify(lookCounts)}`];
 if(apply)seal(ctx,selectedDay);
 lines.push(apply?'sealed; SIGN_OFF requires the owner/tester answer':'would seal; would prompt owner/tester for SIGN_OFF');
 lines.push(`sign_off_day: ${selectedDay}; no answer recorded`);
 return {lines,checked,applied:apply};
}
export function main(argv=process.argv.slice(2)){
 const {flags:f,command,set}=switches(argv,['--apply']);allowed(f,['site','journal','day','crosscast']);if(command.length)throw new Error('invalid_arguments');
 const result=daily(context(f),{selectedDay:f.day,apply:set.has('--apply'),crosscast:f.crosscast});console.log(result.lines.join('\n'));return 0;
}
if(process.argv[1]&&import.meta.url===pathToFileURL(path.resolve(process.argv[1])).href){try{process.exitCode=main();}catch{console.error('daily_refused');process.exitCode=70;}}

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
 const lines=[`records_today: ${current.length}`,`gate_decisions_today: ${JSON.stringify(gates)}`,`steps_by_result_today: ${JSON.stringify(results)}`,`open_in_doubt_all_records: ${checked.in_doubt_open}`,`violations_today: ${current.filter(r=>r.type==='VIOLATION').length}; looks_all_records: ${JSON.stringify(lookCounts)}`];
 lines.push(`open_in_doubt_strict_all_records: ${checked.in_doubt_open_strict}`);
 lines.push(`go_aheads_today: ${JSON.stringify(current.filter(r=>r.type==='GO_AHEAD').map(r=>({job:r.d.job,scope:r.d.scope,count:r.d.count})))}`);
 lines.push(`looks_today: ${JSON.stringify(current.filter(r=>r.type==='OBSERVATION').map(r=>({job:r.d.job,platform:r.d.platform??null,about:r.d.about,answer:r.d.seen,
  status:r.d.seen==='unclear'?'unclear':r.d.minutes_after<10?'early':r.d.minutes_after<=60?'on_time':r.d.minutes_after<=1440?'late':'outside_window'})))}`);
 if(apply)seal(ctx,selectedDay);
 lines.push(apply?'sealed; SIGN_OFF requires the owner/tester answer':'would seal; would prompt owner/tester for SIGN_OFF');
 lines.push(`sign_off_day: ${selectedDay}; no answer recorded`);
 return {lines,checked,applied:apply,day:selectedDay,as_of:checked.as_of,scopes:{gates:'selected_UTC_day',steps:'selected_UTC_day',violations:'selected_UTC_day',go_aheads:'selected_UTC_day',observations:'selected_UTC_day',in_doubt:'all_records',looks:'all_records'}};
}
export function main(argv=process.argv.slice(2)){
 const {flags:f,command,set}=switches(argv,['--apply']);allowed(f,['site','journal','day','crosscast','as-of']);if(command.length)throw new Error('invalid_arguments');
 const result=daily(context(f),{selectedDay:f.day,apply:set.has('--apply'),crosscast:f.crosscast,now:f['as-of']});console.log(result.lines.join('\n'));return 0;
}
if(process.argv[1]&&import.meta.url===pathToFileURL(path.resolve(process.argv[1])).href){try{process.exitCode=main();}catch{console.error('daily_refused');process.exitCode=70;}}

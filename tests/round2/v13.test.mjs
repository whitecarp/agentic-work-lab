import test from 'node:test';
import assert from 'node:assert/strict';
import fs from 'node:fs';
import path from 'node:path';
import {begin,step,closeJob} from '../../scripts/round2/gate-run.mjs';
import {put,readJournal,verifyJournal} from '../../scripts/round2/journal.mjs';
import {REFUSALS} from '../../scripts/round2/schema.mjs';
import {planRun} from '../../scripts/operations-gate.mjs';
import {reconcile} from '../../scripts/round2/reconcile.mjs';
import {daily} from '../../scripts/round2/daily.mjs';
import {fixture,setup,approve,cli,flags} from './helpers.mjs';
const write=(f,name,value)=>fs.writeFileSync(path.join(f.home,name),JSON.stringify(value));
function ready(t){const f=fixture(t);setup(f.ctx);approve(f.ctx);f.ctx.run=begin(f.ctx,{}).run;return f;}
function recorded(f,changes={}){return put(f.ctx,'STEP',{job:'job-1',class:'write',tool:'vpub',action:'delete',role:'delete',platforms:['youtube'],exit:4,ms:1,capture_sha256:'a'.repeat(64),result:'in_doubt',code:null,refs:[],fault_fired:null,...changes},f.ctx.run);}
function look(f,changes={}){return put(f.ctx,'OBSERVATION',{job:'job-1',platform:'youtube',ref:null,about:'delete',by:'owner',client:'phone_app',seen:'absent',fields_match:null,minutes_after:15,words_sha256:'b'.repeat(64),shot_sha256:null,...changes});}

for(const reason of REFUSALS)test(`wrapper refusal ${reason}: one durable record, no child`,async t=>{
 const f=ready(t),control=JSON.parse(fs.readFileSync(path.join(f.home,'run.lock'))),job=control.jobs[0];
 const marker=path.join(f.root,'CHILD'),args={job:'job-1',role:'other'};
 let command=[process.execPath,'-e',`require('fs').writeFileSync(${JSON.stringify(marker)},'started')`];
 const reservation={job:'job-1',class:'write',role:'other',scope:'one_write',at:new Date().toISOString(),platforms:['youtube'],started:true};
 switch(reason){
  case 'vpub_home_missing':fs.unlinkSync(path.join(f.home,'site.json'));break;
  case 'vpub_home_invalid':write(f,'site.json',{site:'owner',vpub_home:'relative'});break;
  case 'vpub_home_unsealed':{
    // A journal can predate the binding field; retain a valid hash chain.
    const rows=readJournal(f.journal).map(r=>({site:r.site,slot:r.slot,type:r.type,run:r.run,d:{...r.d}}));
    for(const row of rows)if(row.type==='PROTOCOL_SEAL')delete row.d.vpub_home_sha256;
    fs.unlinkSync(f.journal);fs.unlinkSync(f.journal+'.head');
    for(const row of rows)put({...f.ctx,slot:row.slot},row.type,row.d,row.run);
    break;
  }
  case 'vpub_home_mismatch':write(f,'site.json',{site:'owner',vpub_home:f.root});break;
  case 'run_not_admitted':f.ctx.run='f'.repeat(32);break;
  case 'step_already_running':control.child_pid=process.pid;break;
  case 'stop_flag':fs.writeFileSync(path.join(f.home,'STOP'),'stop');break;
  case 'job_not_due':args.job='unknown';break;
  case 'job_closed':put(f.ctx,'JOB_END',{job:'job-1',reason:'done',by:'operator',words_sha256:null});break;
  case 'platform_mismatch':args.platform=['douyin'];break;
  case 'job_class_mismatch':job.class='read';break;
  case 'bilibili_delete_forbidden':command=['bili','delete'];break;
  case 'test_platform_forbidden':job.platforms=['x'];args.role='publish';break;
  case 'test_post_daily_cap':args.role='publish';write(f,'reservations.json',Array.from({length:4},(_,i)=>({...reservation,job:'old-'+i,role:'publish',run_class:'test_post',at:new Date(Date.now()-3600000).toISOString()})));break;
  case 'publish_spacing':args.role='publish';write(f,'reservations.json',[{...reservation,role:'publish'}]);break;
  case 'outside_upload_spacing':args.role='publish';put(f.ctx,'NOTE',{code:'outside_upload',platform:'youtube',job:null,agent:'gemini',minutes_ago:0});break;
  case 'platform_warning_hold':args.role='publish';put(f.ctx,'NOTE',{code:'platform_warning',platform:'youtube',job:null,agent:null,minutes_ago:null});break;
  case 'go_ahead_exhausted':write(f,'reservations.json',[reservation]);break;
  case 'invalid_platforms':job.platforms=['PRIVATE_INVALID'];break;
  case 'missing_command':command=[];break;
  case 'tool_mismatch':command=['vpub','status'];args.tool='bili';break;
  case 'class_mismatch':args.class='read';break;
  case 'invalid_role':args.role='PRIVATE_INVALID';break;
  case 'role_mismatch':command=['vpub','delete'];args.role='publish';break;
  case 'operator_record_mismatch':write(f,'operator.json',{operator:'tester'});break;
 }
 write(f,'run.lock',control);
 assert.equal(await step(f.ctx,args,command),5);
 const rows=readJournal(f.journal).filter(r=>r.type==='STEP_REFUSED');
 assert.equal(rows.length,1);assert.equal(rows[0].d.reason,reason);assert.equal(rows[0].run,f.ctx.run);
 assert.equal(fs.existsSync(marker),false);assert.equal(readJournal(f.journal).filter(r=>r.type==='STEP').length,0);
 assert.ok(!fs.readFileSync(f.journal,'utf8').includes('PRIVATE_INVALID'));assert.equal(verifyJournal(f.journal).ok,true);
 assert.equal(reconcile(f.ctx).refusals_by_code[reason],1);
});
test('stop-set refusal run closes with its refusal evidence retained',async t=>{
 const f=ready(t);write(f,'jobs.json',[{id:'job-1',run_class:'refusal',class:'write',due_at:'2020-01-01',platforms:['youtube']}]);
 fs.writeFileSync(path.join(f.home,'STOP'),'stop');assert.equal(await step(f.ctx,{job:'job-1',role:'publish'},['vpub','publish','PRIVATE']),5);
 assert.equal(closeJob(f.ctx,{job:'job-1',reason:'done'}).exit,0);assert.equal(readJournal(f.journal).filter(r=>r.type==='STEP_REFUSED').length,1);
});
test('gate replay uses logged inputs, including per-child recheck',async t=>{
 const f=fixture(t);setup(f.ctx,[{id:'one',class:'write',due_at:'2020-01-01',platforms:['youtube']},{id:'future',class:'read',due_at:'2099-01-01'},{id:'denied',class:'write',due_at:'2020-01-01'},{id:'done',class:'read',due_at:'2020-01-01'}]);
 approve(f.ctx,'one');approve(f.ctx,'future');put(f.ctx,'JOB_END',{job:'done',reason:'done',by:'operator',words_sha256:null});
 f.ctx.run=begin(f.ctx,{}).run;
 assert.equal(await step(f.ctx,{job:'one',role:'other'},[process.execPath,'-e','']),0);
 for(const r of readJournal(f.journal).filter(r=>r.type==='GATE_DECISION')){
  const d=r.d;assert.deepEqual(planRun(d),{decision:d.decision,reason:d.reason,due:d.due});
  assert.ok(d.jobs.every(j=>typeof j.completed==='boolean'&&typeof j.authorized==='boolean'&&typeof j.due_at==='string'));
 }
});
test('every child disables shared browser without changing parent setting',async t=>{
 const f=ready(t),out=path.join(f.root,'env.json'),old=process.env.VPUB_BROWSER_IDLE_SECONDS;process.env.VPUB_BROWSER_IDLE_SECONDS='999';
 try{assert.equal(await step(f.ctx,{job:'job-1',role:'other'},[process.execPath,'-e',"require('fs').writeFileSync(process.argv[1],JSON.stringify(process.env.VPUB_BROWSER_IDLE_SECONDS))",out]),0);assert.equal(JSON.parse(fs.readFileSync(out)),'0');assert.equal(process.env.VPUB_BROWSER_IDLE_SECONDS,'999');}
 finally{if(old===undefined)delete process.env.VPUB_BROWSER_IDLE_SECONDS;else process.env.VPUB_BROWSER_IDLE_SECONDS=old;}
});
test('one look covers only one platform and only after the operation last started step',t=>{
 const f=ready(t);write(f,'jobs.json',[{id:'job-1',run_class:'test_post',platforms:['youtube','douyin']}]);
 recorded(f,{platforms:['youtube','douyin']});look(f);
 assert.deepEqual(reconcile(f.ctx).looks_owed.map(x=>x.platform),['douyin']);
 look(f,{platform:'douyin'});assert.equal(reconcile(f.ctx).looks_owed.length,0);
 recorded(f,{class:'read',role:'delete_verify',exit:0,result:'ok'});
 assert.deepEqual(reconcile(f.ctx).looks_owed.map(x=>x.platform),['youtube']);look(f);
 const r=reconcile(f.ctx);assert.equal(r.look_checks.length,2);assert.equal(r.looks_owed.length,0);
});
test('multi-platform observation CLI requires exactly one planned platform',t=>{
 const f=ready(t);write(f,'jobs.json',[{id:'job-1',run_class:'test_post',platforms:['youtube','douyin']}]);
 const argv=['observe',...flags(f),'--job','job-1','--about','post','--client','phone_app','--seen','present_once','--minutes-after','15'];
 assert.equal(cli(f,'journal',argv,'private words').status,70);
 assert.equal(cli(f,'journal',[...argv,'--platform','x'],'private words').status,70);
 assert.equal(cli(f,'journal',[...argv,'--platform','youtube'],'private words').status,0);
 assert.equal(readJournal(f.journal).at(-1).d.platform,'youtube');
});
test('negative sign-off needs private words; daily prints grants and looks before prompt',t=>{
 const f=ready(t);look(f,{minutes_after:90});const day=new Date().toISOString().slice(0,10),argv=['sign-off',...flags(f),'--day',day,'--by','owner','--answer','no'];
 assert.equal(cli(f,'journal',argv,'').status,70);assert.equal(cli(f,'journal',argv,'PRIVATE explanation').status,0);
 const sign=readJournal(f.journal).at(-1);assert.match(sign.d.words_sha256,/^[a-f0-9]{64}$/);
 assert.equal(fs.readFileSync(path.join(f.home,'captures',sign.d.words_sha256+'.words.txt'),'utf8'),'PRIVATE explanation');
 assert.ok(!fs.readFileSync(f.journal,'utf8').includes('PRIVATE'));
 const lines=daily(f.ctx).lines;assert.match(lines[6],/go_aheads_today:.*one_write.*1/);assert.match(lines[7],/looks_today:.*youtube.*delete.*absent.*late/);
 assert.match(lines[8],/would prompt/);
});
test('matched, complete no-operation refusal permits read closure while strict count stays visible',t=>{
 const f=ready(t),id='c'.repeat(32),rid='d'.repeat(32);recorded(f);
 recorded(f,{awl_run_id:id,exit:2,result:'refused',code:'deletion_already_attempted'});
 recorded(f,{class:'read',role:'delete_verify',exit:0,result:'ok'});
 const file=path.join(f.root,'cc.json'),snap={final_presses:1,delete_attempted:true};
 const cc={schema:'crosscast-audit/1',times:'exact',integrity:{chain_ok:true,unreadable_loss_records:0,loss_records_complete:true,loss_folder_unreadable:false},runs:[{run:rid,awl_run_id:id,command:'delete',flags:{},exit_code:2,ended:true,dropped_events:0,sequence_gap:false,chain_ok:true}],events:[{run:rid,type:'command_start',before:snap},{run:rid,type:'command_end',after:snap}]};
 fs.writeFileSync(file,JSON.stringify(cc));let r=reconcile(f.ctx,file);assert.equal(r.in_doubt_open,0);assert.equal(r.in_doubt_open_strict,1);assert.equal(r.in_doubt[0].by,'read');
 assert.equal(reconcile(f.ctx).in_doubt_open,1);
 cc.events.push({run:rid,type:'call'});fs.writeFileSync(file,JSON.stringify(cc));assert.equal(reconcile(f.ctx,file).in_doubt_open,1);
 cc.events.pop();cc.runs[0].dropped_events=1;fs.writeFileSync(file,JSON.stringify(cc));assert.equal(reconcile(f.ctx,file).in_doubt_open,1);
 cc.runs[0].dropped_events=0;cc.integrity.unreadable_loss_records=1;cc.integrity.loss_records_complete=false;
 fs.writeFileSync(file,JSON.stringify(cc));r=reconcile(f.ctx,file);
 assert.equal(r.crosscast_integrity.unreadable_loss_records,1);assert.equal(r.in_doubt_open,1);
 assert.deepEqual(r.in_doubt[0].ignored_refused_steps,[]);
 for(const integrity of [{chain_ok:true},{chain_ok:true,unreadable_loss_records:0},{chain_ok:true,loss_records_complete:true},
   {chain_ok:true,unreadable_loss_records:0,loss_records_complete:true},
   {chain_ok:true,unreadable_loss_records:0,loss_records_complete:false,loss_folder_unreadable:true}]){
  cc.integrity=integrity;fs.writeFileSync(file,JSON.stringify(cc));
  assert.equal(reconcile(f.ctx,file).in_doubt_open,1);
 }
});

import test from 'node:test';
import assert from 'node:assert/strict';
import fs from 'node:fs';
import path from 'node:path';
import {spawn} from 'node:child_process';
import {fixture,setup,approve,cli,flags,last} from './helpers.mjs';
import {begin,step} from '../../scripts/round2/gate-run.mjs';
import {put,readJournal} from '../../scripts/round2/journal.mjs';
import {configureSetup,recordSetup} from '../../scripts/round2/fault-setup.mjs';
import {selectFault,FAULTS,verifyFaultSupport,injectedReply} from '../../scripts/round2/planned-faults.mjs';
import {sha,alive} from '../../scripts/round2/io.mjs';

const job={id:'job-1',class:'write',run_class:'test_post',platforms:['youtube'],visibility:'private',due_at:'2020-01-01'};
function ready(t,kind='readback_timeout',script,delay=0){
 const f=fixture(t);setup(f.ctx,[job]);fs.writeFileSync(path.join(f.root,'vpub','config.json'),JSON.stringify({unrelated:17}));
 configureSetup(f.ctx,{enabled:true,by:'owner'},()=> 'a'.repeat(64));
 const rule=FAULTS[kind];f.fault={kind,mechanism:rule.mechanism,role:rule.role,trigger:rule.trigger,delay_ms:delay,armed_on:'youtube_private'};
 f.plan={runs:[{...job,site:'owner',faults:[f.fault]}]};f.save=()=>fs.writeFileSync(path.join(f.home,'planned-runs.json'),JSON.stringify(f.plan));f.save();
 approve(f.ctx,'job-1',20);f.ctx.run=begin(f.ctx,{}).run;
 const exe=path.join(f.root,process.platform==='win32'?'vpub.exe':'vpub');try{fs.linkSync(process.execPath,exe);}catch{fs.copyFileSync(process.execPath,exe);}
 const file=path.join(f.root,'standin.cjs');fs.writeFileSync(file,script??"console.log(JSON.stringify({error:{code:process.env.VPUB_FAULT==='after_press'?'injected_fault':'unexpected'}}));process.exitCode=4;");
 f.command=[exe,file];return f;
}
test('setup and cleanup restore only prior settings, preserve other edits, require owner',t=>{
 const f=fixture(t);setup(f.ctx);const file=path.join(f.root,'vpub','config.json');fs.writeFileSync(file,JSON.stringify({x:1}));
 assert.throws(()=>configureSetup(f.ctx,{enabled:true,by:'gemini'},()=> 'a'.repeat(64)));
 configureSetup(f.ctx,{enabled:true,by:'owner'},()=> 'a'.repeat(64));const c=JSON.parse(fs.readFileSync(file));c.x=2;fs.writeFileSync(file,JSON.stringify(c));
 configureSetup(f.ctx,{enabled:false,by:'owner'},()=> 'b'.repeat(64));assert.deepEqual(JSON.parse(fs.readFileSync(file)),{x:2});
 assert.equal(last(f.ctx,'FAULT_SETUP').d.enabled,false);assert.ok(fs.existsSync(path.join(f.home,'fault-config-original.json')));
});
test('fault selection skips absent/wrong role/other tool/public/real jobs and never repeats',t=>{
 const f=ready(t);assert.ok(selectFault(f.ctx,job,'publish',{tool:'vpub'}));
 assert.equal(selectFault(f.ctx,job,'edit',{tool:'vpub'}),null);assert.equal(selectFault(f.ctx,job,'publish',{tool:'other'}),null);
 assert.equal(selectFault(f.ctx,{...job,run_class:'real_slot'},'publish',{tool:'vpub'}),null);
 f.plan.runs[0].faults[0].armed_on='x_scheduled';f.save();assert.equal(selectFault(f.ctx,job,'publish',{tool:'vpub'}),null);
 f.plan.runs[0].faults=[];f.save();assert.equal(selectFault(f.ctx,job,'publish',{tool:'vpub'}),null);
});
test('in-tool fault passes only its explicit environment and is armed once',async t=>{
 const f=ready(t);assert.equal(await step(f.ctx,{job:'job-1',role:'publish'},f.command,{supportCheck:()=>true}),4);
 assert.equal(last(f.ctx,'STEP').d.fault_fired,true);assert.equal(last(f.ctx,'FAULT_RESULT').d.fired,true);
 assert.equal(selectFault(f.ctx,job,'publish',{tool:'vpub'}),null);
 assert.equal(readJournal(f.journal).filter(r=>r.type==='FAULT').length,1);
});
test('edited settings refuse immediately before arming, without starting stand-in',async t=>{
 const f=ready(t);fs.writeFileSync(path.join(f.root,'vpub','config.json'),'{}');
 assert.equal(await step(f.ctx,{job:'job-1',role:'publish'},f.command,{supportCheck:()=>true}),5);assert.equal(last(f.ctx,'STEP_REFUSED').d.reason,'fault_setup_unconfirmed');
 assert.equal(readJournal(f.journal).filter(r=>r.type==='FAULT'||r.type==='STEP').length,0);
 assert.equal(fs.existsSync(path.join(f.home,'captures')),false);
});
test('a damaged journal prevents an arming record and any child',async t=>{
 const f=ready(t);fs.appendFileSync(f.journal,'damage');await assert.rejects(step(f.ctx,{job:'job-1',role:'publish'},f.command,{supportCheck:()=>true}),/broken_journal/);
 assert.equal(fs.existsSync(path.join(f.home,'captures')),false);
});
test('fault never triggered remains false and is not eligible again',async t=>{
 const f=ready(t,'end_tree_after_press',"console.error('waiting for platform confirmation'); console.error(JSON.stringify({event:'browser_progress',platform:'douyin',stage:'waiting for platform confirmation'})); console.error(JSON.stringify({event:'other',platform:'youtube',stage:'waiting for platform confirmation'})); console.log('{}');");
 assert.equal(await step(f.ctx,{job:'job-1',role:'publish'},f.command,{supportCheck:()=>true}),0);assert.equal(last(f.ctx,'STEP').d.fault_fired,false);
 assert.equal(selectFault(f.ctx,job,'publish',{tool:'vpub'}),null);
});
test('stop during upload is observed at next check; manual removal does not authorize resume',async t=>{
 const source="const fs=require('fs'),path=require('path');console.error(JSON.stringify({event:'browser_progress',platform:'youtube',stage:'uploading'}));const t=setInterval(()=>{if(fs.existsSync(path.join(process.env.ROUND2_HOME,'STOP'))){clearInterval(t);console.log(JSON.stringify({error:{code:'stopped'}}));process.exitCode=2;}},40);setTimeout(()=>process.exit(99),10000).unref();";
 const f=ready(t,'stop_during_upload',source);const prior=process.env.ROUND2_HOME;process.env.ROUND2_HOME=f.home;t.after(()=>{if(prior===undefined)delete process.env.ROUND2_HOME;else process.env.ROUND2_HOME=prior;});
 assert.equal(await step(f.ctx,{job:'job-1',role:'publish'},f.command,{supportCheck:()=>true}),2);assert.equal(last(f.ctx,'STEP').d.fault_fired,true);
 assert.equal(last(f.ctx,'STOP_SET').d.by,'scheduler');fs.unlinkSync(path.join(f.home,'STOP'));
 assert.equal(await step(f.ctx,{job:'job-1',role:'resume'},[process.execPath,'-e','process.exit(0)']),5);
 assert.equal(cli(f,'journal',['clear-stop',...flags(f),'--by','owner'],'Account owner clears synthetic upload stop').status,0);
 assert.equal(await step(f.ctx,{job:'job-1',role:'resume'},[process.execPath,'-e','process.exit(0)']),0);
});
for(const [kind,tree]of [['end_command_after_press',false],['end_tree_after_press',true]])test(`${kind} ends only its owned targets and records observations`,{timeout:45000},async t=>{
 const source="const {spawn}=require('child_process'),fs=require('fs'),path=require('path');const c=spawn(process.execPath,['-e','setTimeout(()=>{},30000)'],{stdio:'ignore',windowsHide:true,detached:true});c.unref();fs.writeFileSync(path.join(process.env.ROUND2_HOME,'grandchild.pid'),String(c.pid));console.error(JSON.stringify({event:'browser_progress',platform:'youtube',stage:'waiting for platform confirmation'}));setTimeout(()=>{},30000);";
 const f=ready(t,kind,source,500),prior=process.env.ROUND2_HOME;process.env.ROUND2_HOME=f.home;
 t.after(()=>{if(prior===undefined)delete process.env.ROUND2_HOME;else process.env.ROUND2_HOME=prior;});
 const start=performance.now();await step(f.ctx,{job:'job-1',role:'publish'},f.command,{supportCheck:()=>true});
 const result=last(f.ctx,'FAULT_RESULT').d;assert.equal(result.fired,true);assert.equal(result.failed,false);assert.ok(performance.now()-start>=500);
 assert.ok(result.processes.filter(p=>p.root||tree).every(p=>p.ended));assert.equal(last(f.ctx,'STEP').d.fault_fired,true);
 const grand=Number(fs.readFileSync(path.join(f.home,'grandchild.pid')));
 // This is solely the PID created by the synthetic child above.
 if(!tree){assert.equal(alive(grand),true);process.kill(grand);for(let n=0;n<30&&alive(grand);n++)await new Promise(r=>setTimeout(r,50));assert.equal(alive(grand),false);await new Promise(r=>setTimeout(r,150));assert.ok(result.processes.some(p=>p.pid===grand&&p.ended===false));}
 else {assert.equal(alive(grand),false);assert.ok(result.processes.some(p=>p.pid===grand));}
});
test('edit fault ignores pre-save readback and fires only after pressing', {timeout:45000},async t=>{
 const source="console.error(JSON.stringify({event:'browser_progress',platform:'youtube',stage:'reading back'}));setTimeout(()=>{console.error(JSON.stringify({event:'browser_progress',platform:'youtube',stage:'pressing'}));console.error(JSON.stringify({event:'browser_progress',platform:'youtube',stage:'reading back'}))},700);setTimeout(()=>{},30000);";
 const f=ready(t,'end_browser_after_edit_save',source);const start=performance.now();await step(f.ctx,{job:'job-1',role:'edit'},f.command,{supportCheck:()=>true});
 assert.ok(performance.now()-start>=700);assert.equal(last(f.ctx,'FAULT_RESULT').d.fired,true);
});


test('unknown or changed installation cannot claim the audited fault switch',t=>{
 const f=fixture(t),exe=path.join(f.root,'Scripts','vpub.exe'),folder=path.join(f.root,'Lib','site-packages','video_publisher');
 fs.mkdirSync(folder,{recursive:true});fs.writeFileSync(path.join(folder,'faults.py'),'stand-in audited code\n');
 const expected={files:{'faults.py':sha('stand-in audited code\n')}};
 assert.equal(verifyFaultSupport(exe,expected),true);fs.appendFileSync(path.join(folder,'faults.py'),'changed');assert.equal(verifyFaultSupport(exe,expected),false);
 assert.equal(verifyFaultSupport(exe),false);
});
test('production step refuses unknown switch before FAULT and before spawn',async t=>{
 const f=ready(t);assert.equal(await step(f.ctx,{job:'job-1',role:'publish'},f.command),5);assert.equal(last(f.ctx,'STEP_REFUSED').d.reason,'fault_switch_unavailable');
 assert.equal(readJournal(f.journal).filter(r=>r.type==='FAULT'||r.type==='STEP').length,0);
 assert.equal(fs.existsSync(path.join(f.home,'captures')),false);
});


test('a contradictory private plan refuses without a reservation or child',async t=>{
 const f=ready(t);f.plan.runs[0].visibility='public';f.save();
 assert.equal(await step(f.ctx,{job:'job-1',role:'publish'},f.command,{supportCheck:()=>true}),5);
 assert.equal(last(f.ctx,'STEP_REFUSED').d.reason,'fault_plan_invalid');
 assert.equal(fs.existsSync(path.join(f.home,'captures')),false);
});

for(const [kind,point]of [['between_save_and_press','before_press'],['delete_readback_lost','delete_readback_lost']])test(`${kind} carries its matching in-tool point`,async t=>{
 const data=kind==='delete_readback_lost'?{state:'DELETE_PENDING',error:{code:'injected_fault'}}:{results:{youtube:{state:'UNKNOWN',error:{code:'injected_fault'}}}};
 const f=ready(t,kind,`if(process.env.VPUB_FAULT!==${JSON.stringify(point)})process.exit(99);console.log(${JSON.stringify(JSON.stringify({ok:false,data}))});process.exitCode=4;`);
 assert.equal(await step(f.ctx,{job:'job-1',role:f.fault.role},f.command,{supportCheck:()=>true}),4);
 assert.equal(last(f.ctx,'STEP').d.fault_fired,true);
});
test('fault response recognizes actual envelopes without borrowing another platform error',()=>{
 assert.equal(injectedReply(JSON.stringify({data:{results:{youtube:{state:'UNKNOWN',code:'injected_fault'}}}}),'youtube'),true);
 assert.equal(injectedReply(JSON.stringify({data:{results:{douyin:{error:{code:'injected_fault'}}}}}),'youtube'),false);
 assert.equal(injectedReply('{"error":{"code":"injected_fault"}}\n{}','youtube'),false);
 assert.equal(injectedReply('{"error":{"code":"readback_failed"}}','youtube'),false);
});
test('upload tree fault waits for its delay and never targets an unrelated process',{timeout:45000},async t=>{
 const sentinel=spawn(process.execPath,['-e','setTimeout(()=>{},30000)'],{stdio:'ignore',windowsHide:true});
 await new Promise((resolve,reject)=>{sentinel.once('spawn',resolve);sentinel.once('error',reject);});
 try{
  const f=ready(t,'browser_lost_during_upload',"if(process.env.VPUB_FAULT!=='external')process.exit(99);console.error(JSON.stringify({event:'browser_progress',platform:'youtube',stage:'uploading'}));setTimeout(()=>{},30000);",2000),start=performance.now();
  await step(f.ctx,{job:'job-1',role:'publish'},f.command,{supportCheck:()=>true});
  assert.ok(performance.now()-start>=2000);assert.equal(alive(sentinel.pid),true);
  const r=last(f.ctx,'FAULT_RESULT').d;assert.equal(r.fired,true);assert.ok(!r.processes.some(p=>p.pid===sentinel.pid));
 }finally{sentinel.kill();await new Promise(resolve=>sentinel.once('exit',resolve));}
});

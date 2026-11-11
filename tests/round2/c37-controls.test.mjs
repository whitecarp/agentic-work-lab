import test from 'node:test';import assert from 'node:assert/strict';import fs from 'node:fs';import path from 'node:path';import {spawnSync} from 'node:child_process';
import {fixture,setup,approve,cli,flags,last,ageJournal} from './helpers.mjs';
import {put,readJournal} from '../../scripts/round2/journal.mjs';import {begin,step,end,closeJob} from '../../scripts/round2/gate-run.mjs';
import {reconcile} from '../../scripts/round2/reconcile.mjs';import {generate,make,verifyPlan} from '../../scripts/round2/plan-runs.mjs';
import {faultController,family,selectFault} from '../../scripts/round2/planned-faults.mjs';import {configureSetup} from '../../scripts/round2/fault-setup.mjs';
const counts=JSON.parse(fs.readFileSync(new URL('./fixtures/planned-counts-v1.json',import.meta.url))),audit=JSON.parse(fs.readFileSync(new URL('./fixtures/c36-cli-repeat.json',import.meta.url)));
function ready(t){const f=fixture(t);setup(f.ctx);approve(f.ctx,'job-1',20);f.ctx.run=begin(f.ctx,{}).run;return f;}
const record=(f,d={})=>put(f.ctx,'STEP',{job:'job-1',class:'write',tool:'vpub',action:'publish',role:'publish',platforms:['youtube'],exit:4,ms:0,capture_sha256:'a'.repeat(64),result:'in_doubt',code:null,refs:[],fault_fired:null,started:true,...d},f.ctx.run);
const look=f=>put(f.ctx,'OBSERVATION',{job:'job-1',platform:'youtube',ref:null,about:'post',by:'owner',client:'phone_app',seen:'present_private',fields_match:null,minutes_after:15,words_sha256:'a'.repeat(64),shot_sha256:null});
test('C37 A3 premature claimed 15-minute look does not settle, elapsed one does',t=>{const f=ready(t);record(f);look(f);assert.equal(reconcile(f.ctx).in_doubt_open,1);ageJournal(f.ctx,15);look(f);assert.equal(reconcile(f.ctx).in_doubt_open,0);});
test('C37 A4 unchanged null deletion counts prove no press, null final count does not',t=>{
 const f=ready(t);record(f);const d=structuredClone(audit);record(f,{role:'repeat_same',result:'ok',exit:0,awl_run_id:d.runs[0].awl_run_id});record(f,{role:'verify',result:'ok',exit:0,confirmed_state:'SUBMISSION_VERIFIED'});
 const file=path.join(f.root,'audit.json'),before=d.events[0].before={state:'UNKNOWN',final_presses:1},after=d.events.at(-1).after={state:'UNKNOWN',final_presses:1};before.delete_attempted=after.delete_attempted=null;fs.writeFileSync(file,JSON.stringify(d));assert.equal(reconcile(f.ctx,file).in_doubt[0].ignored_no_press_steps.length,1);
 before.final_presses=after.final_presses=null;fs.writeFileSync(file,JSON.stringify(d));assert.equal(reconcile(f.ctx,file).in_doubt[0].ignored_no_press_steps.length,0);
});
for(const role of ['edit','delete'])test('C37 A5 '+role+' ignores an old upload press marker',t=>{
 const f=ready(t),d=structuredClone(audit);record(f,{action:role,role,exit:3,result:'unknown',answered:false,awl_run_id:d.runs[0].awl_run_id});
 d.runs[0].ended=false;d.runs[0].exit_code=null;d.events=[{...d.events[0],before:{final_submit_attempted:true,final_presses:1}},
 {run:d.runs[0].run,type:'transition',platform:'youtube',kind:role,final_submit_attempted:false,delete_attempted:role==='delete'?false:null}];
 const file=path.join(f.root,'audit.json');fs.writeFileSync(file,JSON.stringify(d));assert.equal(reconcile(f.ctx,file).in_doubt[0].cutoff_press_flag,false);
});
test('C37 A2 exit 2 with platform contact opens a case unless press-free',t=>{
 const f=ready(t),d=structuredClone(audit);record(f,{result:'refused',exit:2,awl_run_id:d.runs[0].awl_run_id});d.runs[0].exit_code=2;d.events.splice(1,0,{run:d.runs[0].run,type:'call',platform:'youtube',operation:'press',platform_writes:1});
 const file=path.join(f.root,'audit.json');fs.writeFileSync(file,JSON.stringify(d));assert.equal(reconcile(f.ctx,file).in_doubt_open,1);
});
test('C37 A6 a failed capture append still records the started STEP',async t=>{
 const f=ready(t),original=fs.writeSync;fs.writeSync=function(fd,data,...rest){if(typeof data==='string'&&data.includes('"stream":"stdout"'))throw new Error('synthetic full capture disk');return original.call(this,fd,data,...rest);};
 try{await step(f.ctx,{job:'job-1',role:'publish'},[process.execPath,'-e','console.log("{}")']);}finally{fs.writeSync=original;}
 assert.equal(last(f.ctx,'STEP').d.capture_failed,true);assert.equal(last(f.ctx,'STEP').d.started,true);
});
test('C37 A7 different post references cannot settle the earlier case',t=>{const f=ready(t);record(f,{refs:['a'.repeat(16)]});record(f,{refs:['b'.repeat(16)],role:'verify',result:'ok',exit:0,confirmed_state:'SUBMISSION_VERIFIED'});assert.equal(reconcile(f.ctx).in_doubt_open,1);});
test('C37 A6 controller attachment failure cannot relabel a spawned child as unstarted',async t=>{const f=ready(t);await step(f.ctx,{job:'job-1',role:'publish'},[process.execPath,'-e','process.exit(0)'],{controllerFactory:()=>({attach(){throw new Error('synthetic attach failure');},finish:async()=>({fired:null})})});assert.equal(last(f.ctx,'STEP').d.started,true);assert.equal(last(f.ctx,'STEP').d.capture_failed,true);});
test('C37 A9 standing job remains open',t=>{const f=fixture(t);setup(f.ctx,[{id:'standing',class:'read',run_class:'standing',platforms:[],due_at:'2020-01-01'}]);assert.throws(()=>closeJob(f.ctx,{job:'standing',reason:'done'}),/standing_job_never_closed/);});
test('C37 A10 independent platform closures permit one clean done',t=>{
 const f=fixture(t);setup(f.ctx,[{id:'job-1',class:'write',run_class:'test_post',platforms:['youtube','douyin'],due_at:'2020-01-01'}]);
 record(f,{role:'delete',action:'delete',platforms:['youtube']});record(f,{role:'delete_verify',action:'delete',class:'read',result:'ok',exit:0,confirmed_state:'DELETED_VERIFIED'});
 put(f.ctx,'NOTE',{code:'manual_step',job:'job-1',platform:'douyin',agent:null,minutes_ago:null});
 closeJob(f.ctx,{job:'job-1',reason:'done'});assert.deepEqual(last(f.ctx,'JOB_END').d.platform_settlements,[{platform:'youtube',how:'delete_confirmed'},{platform:'douyin',how:'removed_by_hand'}]);
});
test('C37 A12 every CLI needs a real typed slot and A13 rejects a stray journal',t=>{
 const f=fixture(t);setup(f.ctx);
 for(const [script,args]of [['journal',['stop']],['gate-run',['begin']],['seal',[]],['export',['--out',path.join(f.root,'copy')]],['daily',[]],['reconcile',[]],['plan-runs',['verify']]]){
  const r=spawnSync(process.execPath,[path.resolve('scripts/round2/'+script+'.mjs'),...args,'--site','owner','--journal',f.journal],{env:f.env,encoding:'utf8'});assert.notEqual(r.status,0,script);
 }
 const r=cli(f,'journal',['stop','--site','owner','--journal',path.join(f.root,'stray.jsonl')]);assert.equal(r.status,70);assert.match(r.stderr,/journal_place_mismatch/);assert.ok(!fs.existsSync(path.join(f.home,'STOP')));
 assert.equal(cli(f,'journal',['stop',...flags(f).filter((x,i,a)=>x!=='gemini'),'--slot','scheduler']).status,70);
});
test('C37 A14 two-phase seal forward only; A15 exact source regeneration catches edited list',t=>{
 const f=fixture(t);setup(f.ctx);fs.unlinkSync(path.join(f.home,'jobs.json'));make(f.home,counts,'owner',{dueFrom:'2020-01-01'});assert.equal(verifyPlan(f.home).matches,true);
 const protocol=path.join(f.root,'protocol.md');fs.writeFileSync(protocol,'synthetic protocol');const args=phase=>['protocol-seal',...flags(f),'--phase',phase,'--protocol',protocol,'--lab-commit','a'.repeat(40),'--crosscast-commit','b'.repeat(40),'--plan',path.join(f.home,'planned-runs.json')];
 assert.equal(cli(f,'journal',args('2b')).status,0);for(const phase of ['2b','2a','rehearsal'])assert.match(cli(f,'journal',args(phase)).stderr,/seal_phase_not_forward/);
 fs.appendFileSync(path.join(f.home,'jobs.json'),' ');assert.throws(()=>verifyPlan(f.home),/plan_reproduction_mismatch/);
});
test('C37 A16 CLI runner shell exit does not end an admitted session',t=>{const f=fixture(t);setup(f.ctx);approve(f.ctx);const a=cli(f,'gate-run',['begin',...flags(f)]),b=cli(f,'gate-run',['begin',...flags(f)]);assert.equal(JSON.parse(a.stdout).decision,'plan_only');assert.equal(JSON.parse(b.stdout).reason,'already_running');});
test('C37 B2 old orphan is skipped and missing starter fails',()=>{assert.deepEqual(family([{pid:1,parent:9,born:'20'},{pid:2,parent:1,born:'10'},{pid:3,parent:1,born:'21'}],1).map(x=>x.pid),[1,3]);assert.throws(()=>family([],1),/starter_missing/);});
test('C37 B2 a natural finish before requested delay is not fired',async t=>{const f=fixture(t);setup(f.ctx);const child={pid:1,exitCode:0,signalCode:null,once(){}};const c=faultController(f.ctx,{job:'job-1',kind:'end_command_after_press',role:'publish',delay_ms:0,armed_on:'youtube_private'});c.attach(child);c.line('{"event":"browser_progress","platform":"youtube","stage":"waiting for platform confirmation"}');assert.equal((await c.finish('')).fired,false);assert.equal(last(f.ctx,'FAULT_RESULT').d.reason,'command_finished_before_request');});
test('C37 B3 temporary operator cannot configure',t=>{const f=fixture(t);setup(f.ctx);put(f.ctx,'HANDOFF',{from:'gemini',to:'codex',job:'job-1',by:'owner',words_sha256:'a'.repeat(64)});fs.writeFileSync(path.join(f.home,'operator.json'),'{"operator":"codex"}');assert.throws(()=>configureSetup({...f.ctx,slot:'codex'},{enabled:true,by:'owner'},()=> 'a'.repeat(64)),/temporary_operator_cannot_configure/);});
test('C37 B4 owner-recorded platform switch skips tree fault with evidence',t=>{
 const f=fixture(t);setup(f.ctx);const job={id:'job-1',site:'owner',run_class:'test_post',platforms:['youtube'],visibility:'private'};
 fs.writeFileSync(path.join(f.home,'planned-runs.json'),JSON.stringify({runs:[{...job,faults:[{kind:'end_tree_after_press',role:'publish',mechanism:'wrapper_kill',trigger:'waiting_for_platform_confirmation',delay_ms:0,armed_on:'youtube_private'}]}]}));
 assert.equal(cli(f,'journal',['browser-faults',...flags(f),'--platform','youtube','--enabled','false','--by','owner'],'synthetic owner disable').status,0);
 assert.equal(selectFault(f.ctx,job,'publish',{tool:'vpub'}),null);assert.equal(last(f.ctx,'FAULT_SKIPPED').d.reason,'browser_faults_disabled');
});
test('C37 C3 privacy comparison uses date end and rounded whole minutes',t=>{const f=ready(t);record(f);ageJournal(f.ctx,15);look(f);const r=reconcile(f.ctx,undefined,{privacy:true});assert.match(r.as_of,/^\d{4}-\d\d-\d\d$/);assert.equal(r.in_doubt[0].minutes,16);assert.ok(!('seconds' in r.in_doubt[0]));});
test('C37 C5 audit session and purpose are not inherited',async t=>{const f=ready(t);const previous=[process.env.VPUB_AUDIT_SESSION,process.env.VPUB_AUDIT_PURPOSE];process.env.VPUB_AUDIT_SESSION='secret-session';process.env.VPUB_AUDIT_PURPOSE='secret-purpose';try{assert.equal(await step(f.ctx,{job:'job-1',role:'publish'},[process.execPath,'-e','process.exit(process.env.VPUB_AUDIT_SESSION||process.env.VPUB_AUDIT_PURPOSE?9:0)']),0);}finally{for(const [i,k]of ['VPUB_AUDIT_SESSION','VPUB_AUDIT_PURPOSE'].entries())if(previous[i]===undefined)delete process.env[k];else process.env[k]=previous[i];}});

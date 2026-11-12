import test from 'node:test';
import assert from 'node:assert/strict';
import fs from 'node:fs';
import path from 'node:path';
import {EventEmitter} from 'node:events';
import {generate,make,verifyPlan,assignTestTitles,assignC2Delays} from '../../scripts/round2/plan-runs.mjs';
import {wholePublishRefusal,publishStartedOn} from '../../scripts/round2/answers.mjs';
import {limits} from '../../scripts/round2/policy.mjs';
import {publishChecks} from '../../scripts/round2/analysis-checks.mjs';
import {FAULTS,selectFault,faultController} from '../../scripts/round2/planned-faults.mjs';
import {put} from '../../scripts/round2/journal.mjs';
import {fixture as local,setup,last} from './helpers.mjs';
import {fixture,at,bundle,gateHash,privateSite} from '../fixtures/round2-analysis.mjs';
import {analyze} from '../../paper/round2/metrics.mjs';
const counts=JSON.parse(fs.readFileSync(new URL('./fixtures/planned-counts-v1.json',import.meta.url)));
const options={dueFrom:'2026-11-19T00:00:00Z'};
const job={id:'job-1',site:'owner',run_class:'test_post',platforms:['youtube'],visibility:'private'};
const whole=z=>z.tables.find(t=>t.phase==='2b'&&t.platform===null).all_recorded;
const result=f=>analyze(bundle(f.finish()),{gateHash});

for(const site of ['owner','tester'])test('C47 counted C2 split and unique titles '+site,()=>{
 for(let i=0;i<12;i++){
  const {plan,jobs}=generate(counts,site,Buffer.alloc(32,i),options);
  const posts=plan.runs.filter(r=>r.run_class==='test_post');
  assert.equal(new Set(posts.map(r=>r.title)).size,posts.length);
  for(const r of posts){assert.equal(jobs.find(j=>j.id===r.id).title,r.title);assert.ok(r.title.includes(String(r.clip_no).padStart(3,'0')));}
  const late=posts.flatMap(r=>r.faults.filter(f=>f.delay_ms===10000).map(f=>({r,f})));
  assert.equal(late.length,site==='owner'?2:1);
  assert.ok(late.every(({r,f})=>r.platforms.join()==='youtube'&&f.kind==='end_tree_after_press'));
  assert.ok(posts.flatMap(r=>r.faults).every(f=>[0,500,2000,10000].includes(f.delay_ms)));
  assert.equal(plan.generator.version,2);
 }
});
test('C47 practice C2 has one early and one late; duplicate titles refuse',()=>{
 const runs=[1,2].map(n=>({...job,id:'practice-'+n,clip:'short',clip_no:n,
  faults:[{kind:'end_tree_after_press',role:'publish',delay_ms:500}]}));
 assignC2Delays(runs,'owner',()=>0,{practice:true});assignTestTitles(runs,'owner');
 assert.deepEqual(runs.map(r=>r.faults[0].delay_ms).sort((a,b)=>a-b),[500,10000]);
 assert.throws(()=>assignC2Delays(runs.slice(0,1),'owner',()=>0,{practice:true}),/invalid_c2_counts/);
 assert.throws(()=>assignTestTitles([runs[0],{...runs[1],clip_no:1}],'owner'),/duplicate_test_title/);
 const lengths=[{...runs[0],clip:'short'},{...runs[0],clip:'long'}];assignTestTitles(lengths,'owner');
 assert.notEqual(lengths[0].title,lengths[1].title);
});
test('C47 phase 2a generation keeps one early and one late YouTube C2',()=>{
 const {plan}=generate(counts,'tester',Buffer.alloc(32,4),{...options,phase:'2a'});
 const c2=plan.runs.filter(r=>r.platforms.join()==='youtube').flatMap(r=>r.faults.filter(f=>f.kind==='end_tree_after_press'));
 assert.equal(c2.length,2);assert.equal(c2.filter(f=>f.delay_ms===10000).length,1);
});
for(const version of [1,2])test('C47 reproducible lists preserve generator version '+version,t=>{
 const f=local(t);make(f.home,counts,'owner',{...options,version});
 assert.equal(verifyPlan(f.home).matches,true);
 const plan=JSON.parse(fs.readFileSync(path.join(f.home,'planned-runs.json')));
 assert.equal(plan.generator.version,version);
 assert.equal(plan.runs.some(r=>r.faults.some(f=>f.delay_ms===10000)),version===2);
});
function refused(code,exit=2){return {type:'STEP',seq:3,run:'run',at:at(1),d:{job:'old',role:'publish',platforms:['youtube'],ms:0,exit,
 answers:[{platform:'youtube',state:null,previous_state:null,press_flag:null,answered:true,code}]}};}
for(const code of ['stopped','lock_marker_unreadable','attempt_busy','confirmation_required'])test('C47 refusal excludes upload start '+code,()=>{
 const s=refused(code);assert.equal(wholePublishRefusal(s),true);assert.equal(publishStartedOn(s,'youtube'),false);
 const records=[s,{...refused(null,0),seq:4,at:at(8)}],jobs=[{...job,id:'old'}];
 for(const saved of [[],records.map(r=>({run:r.run,step_seq:r.seq,role:'publish',platforms:['youtube'],run_class:'test_post',job:'old',started:true,at:at(0)}))]){
  assert.equal(limits(records,job,'publish',['youtube'],at(25),saved,jobs),'publish_spacing');
  assert.equal(limits(records,job,'publish',['youtube'],at(28),saved,jobs),null);
 }
 assert.deepEqual(publishChecks(records,jobs).map(r=>r.step),[4]);
 const many=[0,1,2,3].map(i=>({...s,seq:i+1,at:at(i*25),d:{...s.d,job:'j'+i,exit:i?0:2}}));
 assert.equal(limits(many,job,'publish',['youtube'],at(200),[],many.map(r=>({...job,id:r.d.job}))),null);
});
test('C47 missing output, post-press state and incomplete multichannel refusal remain starts',()=>{
 const s=refused('attempt_busy');s.d.answers[0].press_flag=true;s.d.answers[0].state='SUBMITTED_UNVERIFIED';
 assert.equal(publishStartedOn(s,'youtube'),true);
 s.d.answers=[];assert.equal(publishStartedOn(s,'youtube'),true);
 s.d.answers=[{platform:'youtube',state:null,answered:false}];assert.equal(publishStartedOn(s,'youtube'),true);
 s.d.answers[0].answered=true;s.d.platforms.push('douyin');assert.equal(wholePublishRefusal(s),false);
});
function faultFile(f,kind='readback_timeout',platform='youtube',delay=0){
 const row={...job,platforms:[platform]},rule=FAULTS[kind];
 const fault={job:job.id,kind,mechanism:rule.mechanism,role:rule.role,trigger:rule.trigger,delay_ms:delay,armed_on:platform+'_private'};
 fs.writeFileSync(path.join(f.home,'planned-runs.json'),JSON.stringify({runs:[{...row,faults:[fault]}]}));
 return {row,fault};
}
const stepData=(code,extra={})=>({job:job.id,class:'write',tool:'vpub',action:'publish',role:'publish',platforms:['youtube'],exit:2,ms:1,
 capture_sha256:'1'.repeat(64),awl_run_id:'2'.repeat(32),result:'refused',code,refs:[],fault_fired:false,started:true,answered:true,
 answers:[{platform:'youtube',state:null,previous_state:null,code,press_flag:null,answered:true,not_saved:false,refs:[]}],...extra});
for(const code of ['busy','browser_busy','stopped','lock_marker_unreadable','attempt_busy','confirmation_required'])test('C47 rearm any whole refusal '+code,t=>{
 const f=local(t);setup(f.ctx);const {fault}=faultFile(f);
 put(f.ctx,'FAULT',fault);
 put(f.ctx,'FAULT_RESULT',{job:job.id,kind:fault.kind,role:'publish',triggered:false,fired:false,failed:false,reason:null,action_delay_ms:null,gone_delay_ms:null,kill_exit:null,processes:[]});
 put(f.ctx,'STEP',stepData(code));assert.ok(selectFault(f.ctx,job,'publish',{tool:'vpub'}));
});
for(const bad of ['exit','state','press','fired','triggered','failed'])test('C47 rearm refuses '+bad,t=>{
 const f=local(t);setup(f.ctx);const {fault}=faultFile(f);put(f.ctx,'FAULT',fault);
 put(f.ctx,'FAULT_RESULT',{job:job.id,kind:fault.kind,role:'publish',triggered:bad==='triggered',fired:bad==='fired',failed:bad==='failed',reason:null,action_delay_ms:null,gone_delay_ms:null,kill_exit:null,processes:[]});
 const d=stepData('attempt_busy');if(bad==='exit')d.exit=4;if(bad==='state')d.answers[0].state='IN_PROGRESS';if(bad==='press')d.answers[0].press_flag=true;
 put(f.ctx,'STEP',d);assert.equal(selectFault(f.ctx,job,'publish',{tool:'vpub'}),null);
});
test('C47 controller records non-busy whole refusal without firing',async t=>{
 const f=local(t);setup(f.ctx);const c=faultController(f.ctx,faultFile(f).fault);
 await c.finish(JSON.stringify({ok:false,error:{code:'confirmation_required'}}),2);
 assert.equal(last(f.ctx,'FAULT_RESULT').d.reason,'publish_refused_not_reached');
});
test('C47 ten-second delay accepts only YouTube C2',t=>{
 const f=local(t);setup(f.ctx);
 for(const [kind,p,allowed] of [['end_tree_after_press','youtube',true],['end_tree_after_press','douyin',false],['end_command_after_press','youtube',false]]){
  const {row}=faultFile(f,kind,p,10000);
  if(allowed)assert.equal(selectFault(f.ctx,row,'publish',{tool:'vpub'}).delay_ms,10000);
  else assert.throws(()=>selectFault(f.ctx,row,'publish',{tool:'vpub'}),/fault_plan_invalid/);
 }
});
test('C47 late C2 waits for its delay with injected process stand-ins only',async t=>{
 const f=local(t);setup(f.ctx);const {fault}=faultFile(f,'end_tree_after_press','youtube',10000);
 const child=new EventEmitter();Object.assign(child,{pid:777,exitCode:null,signalCode:null});let called=0;
 const rows=[{pid:777,parent:1,born:'10',image:'node.exe'}];
 const c=faultController(f.ctx,fault,{snapshot:async()=>called?[]:rows,kill:async()=>{called++;child.exitCode=0;return 0;}});
 c.attach(child);child.emit('spawn');
 c.line(JSON.stringify({event:'browser_progress',platform:'youtube',stage:'waiting for platform confirmation'}));
 assert.equal(called,0);await c.finish('',3);
 assert.equal(called,1);const r=last(f.ctx,'FAULT_RESULT').d;assert.equal(r.fired,true);assert.ok(r.action_delay_ms>=9900);
});
for(const code of ['busy','browser_busy','attempt_busy','lock_marker_unreadable'])test('C47 analysis skips lock-only tries '+code,()=>{
 const f=fixture();f.step({m:3,exit:2,state:null,press:null,contact:false,code});
 const real=f.step({m:7});f.step({m:9,role:'repeat_same',exit:2,state:null,press:null,contact:false,code});f.look({m:25,minutes_after:16});
 const z=result(f),w=whole(z);assert.equal(w.submission.repeats.length,0);
 assert.equal(f.finish().reconcile.publish_checks[0].step,real.seq);
 assert.equal(w.submission.attempts.length,1);
});
for(const lost of [false,true])test('C47 attempt_busy after press or missing record is not a try of nothing '+lost,()=>{
 const f=fixture();f.step({m:3});const s=f.step({m:6,role:'repeat_same',exit:2,state:'SUBMITTED_UNVERIFIED',code:'attempt_busy',press:1,contact:!lost});
 if(lost){const c=f.tool.runs.at(-1);c.ended=false;f.tool.events=f.tool.events.filter(e=>e.run!==c.run||e.type!=='command_end');}
 const x=f.finish(),z=analyze(bundle(x),{gateHash});
 assert.ok(x.reconcile.in_doubt.some(c=>c.steps.includes(s.seq)||c.step===s.seq));
 assert.equal(whole(z).submission.repeats.length,1);
});
test('C47 Douyin recorded owner removal counts look, has no delete try or gap',()=>{
 const f=fixture({platforms:['douyin']});f.step({m:3});f.look({m:18});
 const removal=f.put('NOTE',{job:'job-a',platform:'douyin',code:'manual_step',words_sha256:'a'.repeat(64)},20);
 f.look({about:'delete',seen:'absent',m:35});
 f.put('JOB_END',{job:'job-a',reason:'done',by:'owner',platform_settlements:[{platform:'douyin',how:'removed_by_hand'}]},36);
 const x=f.finish(),check=x.reconcile.look_checks.find(c=>c.about==='delete');
 assert.equal(check.step,removal.seq);assert.equal(check.status,'on_time');assert.deepEqual(x.reconcile.looks_owed,[]);
 for(const s of [x,privateSite(x)]){
  const z=analyze(bundle(s),{gateHash}),w=whole(z);
  assert.deepEqual(w.readback.deletes,[]);assert.deepEqual(z.verdict.gaps,[]);
  assert.equal(z.checklist.all_recorded.find(c=>c.cell===7&&c.platform==='douyin').status,'not_attempted');
 }
});
test('C47 state-free lock error without intact evidence stays a measured possible try',()=>{
 const f=fixture();f.step();const s=f.step({role:'repeat_same',exit:2,state:null,code:'lock_marker_unreadable',contact:false,press:null});
 f.tool.runs.at(-1).dropped_events=1;
 const x=f.finish(),z=analyze(bundle(x),{gateHash});
 assert.ok(x.reconcile.error_steps_unclassified.some(c=>c.step===s.seq));
 assert.equal(whole(z).submission.repeats.length,1);
 assert.ok(whole(z).in_doubt.possible_unclassified_errors.some(c=>c.step===s.seq));
});
test('C47 a manual step without the matching Douyin job supplies no removal evidence',()=>{
 const f=fixture({platforms:['douyin']});f.step();f.look();
 f.put('NOTE',{job:null,platform:'douyin',code:'manual_step',words_sha256:'a'.repeat(64)},20);
 f.look({about:'delete',seen:'absent',m:35});
 const x=f.finish();assert.ok(!x.reconcile.look_checks.some(c=>c.about==='delete'));
});

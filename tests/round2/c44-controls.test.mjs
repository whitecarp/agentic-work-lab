import test from 'node:test';
import assert from 'node:assert/strict';
import fs from 'node:fs';
import path from 'node:path';
import {fixture,setup,approve,cli,flags,last,ageJournal} from './helpers.mjs';
import {put,readJournal,verifyJournal} from '../../scripts/round2/journal.mjs';
import {begin,endFailedSession} from '../../scripts/round2/gate-run.mjs';
import {make} from '../../scripts/round2/plan-runs.mjs';
import {lookTiming} from '../../scripts/round2/looks.mjs';
import {limits} from '../../scripts/round2/policy.mjs';
import {selectFault,FAULTS,faultController} from '../../scripts/round2/planned-faults.mjs';
const counts=JSON.parse(fs.readFileSync(new URL('./fixtures/planned-counts-v1.json',import.meta.url)));
const step=f=>put(f.ctx,'STEP',{job:'job-1',class:'write',tool:'vpub',action:'publish',role:'publish',platforms:['youtube'],exit:4,ms:0,capture_sha256:'a'.repeat(64),result:'in_doubt',code:null,refs:[],fault_fired:null,started:true});
test('C44 re-freeze twice with unchanged plan and private owner words; forward only',t=>{
 const f=fixture(t);setup(f.ctx);fs.unlinkSync(path.join(f.home,'jobs.json'));make(f.home,counts,'owner',{dueFrom:'2020-01-01'});
 const file=path.join(f.root,'protocol');fs.writeFileSync(file,'synthetic');
 const args=['protocol-seal',...flags(f),'--phase','2b','--protocol',file,'--lab-commit','a'.repeat(40),'--crosscast-commit','b'.repeat(40),'--plan',path.join(f.home,'planned-runs.json')];
 assert.equal(cli(f,'journal',args).status,0);
 assert.match(cli(f,'journal',args).stderr,/refreeze_owner_words_required/);
 assert.match(cli(f,'journal',[...args,'--by','tester'],'words').stderr,/refreeze_owner_words_required/);
 assert.match(cli(f,'journal',[...args,'--by','owner'],'').stderr,/empty_words/);
 for(let i=0;i<2;i++)assert.equal(cli(f,'journal',[...args,'--by','owner'],'private owner recovery words').status,0);
 assert.match(cli(f,'journal',[...args,'--by','owner'],'words').stderr,/refreeze_limit/);
 assert.equal(readJournal(f.journal).filter(r=>r.type==='PROTOCOL_SEAL'&&r.d.phase==='2b').length,3);
 assert.equal(fs.readFileSync(f.journal,'utf8').includes('private owner recovery'),false);
 assert.equal(verifyJournal(f.journal).ok,true);
});
test('C44 checking agent ends failed session only on owner words; no other control changes',t=>{
 const f=fixture(t);setup(f.ctx);approve(f.ctx);const run=begin(f.ctx,{}).run;
 const c={...f.ctx,slot:'codex',run};
 assert.throws(()=>endFailedSession(c,'tester','words'),/checking_agent_required/);
 assert.throws(()=>endFailedSession({...c,slot:'gemini'},'owner','words'),/checking_agent_required/);
 assert.throws(()=>endFailedSession(c,'owner',''),/empty_words/);
 const before=fs.readFileSync(path.join(f.home,'operator.json'));fs.writeFileSync(path.join(f.home,'STOP'),'kept');
 const r=endFailedSession(c,'owner','owner asks to end failed service session').record;
 assert.equal(r.slot,'codex');assert.equal(r.d.by,'owner');assert.equal(r.d.operator,'gemini');assert.equal(r.d.state,'interrupted');
 assert.deepEqual(fs.readFileSync(path.join(f.home,'operator.json')),before);assert.ok(fs.existsSync(path.join(f.home,'STOP')));
 assert.ok(!fs.existsSync(path.join(f.home,'run.lock')));assert.throws(()=>endFailedSession(c,'owner','words'),/session_not_open/);
});
test('C44 recovery never ends an active child or edits any process',t=>{
 const f=fixture(t);setup(f.ctx);approve(f.ctx);const run=begin(f.ctx,{}).run;
 const file=path.join(f.home,'run.lock'),r=JSON.parse(fs.readFileSync(file));r.child_pid=process.pid;fs.writeFileSync(file,JSON.stringify(r));
 const bytes=fs.readFileSync(f.journal);
 assert.throws(()=>endFailedSession({...f.ctx,slot:'codex',run},'owner','words'),/step_already_running/);
 assert.deepEqual(fs.readFileSync(f.journal),bytes);
});
test('C44 observe refuses a rounded-up minute before recording it',t=>{
 const f=fixture(t);setup(f.ctx);step(f);const args=['observe',...flags(f),'--job','job-1','--about','post','--client','phone_app','--seen','absent','--minutes-after','10'];
 assert.match(cli(f,'journal',args,'look').stderr,/look_minute_not_reached/);
 assert.equal(readJournal(f.journal).filter(r=>r.type==='OBSERVATION').length,0);
 ageJournal(f.ctx,10);const r=cli(f,'journal',args,'look');assert.equal(r.status,0,r.stderr);assert.deepEqual(JSON.parse(r.stdout).look,{status:'on_time',counts:true,recorded_before_claimed_minute:false});
});
for(const [n,status,counts]of [[0,'early',false],[9,'early',false],[10,'on_time',true],[60,'on_time',true],[61,'late',true],[1440,'late',true],[1441,'outside_window',false]])test('C44 look timing '+n,()=>{
 const s={seq:1,at:'2026-11-12T00:00:00Z'},r={seq:2,at:new Date(Date.parse(s.at)+n*60000).toISOString(),d:{about:'post',seen:'absent',minutes_after:n}};
 assert.deepEqual(lookTiming(r,s),{status,counts,recorded_before_claimed_minute:false});
});
test('C44 second look at 36 hours counts only after absent in-doubt first look',()=>{
 const s={seq:1,type:'STEP',at:'2026-11-12T00:00:00Z',d:{job:'j',platforms:['youtube'],role:'publish',result:'in_doubt'}};
 const first={seq:2,type:'OBSERVATION',at:'2026-11-12T00:15:00Z',d:{job:'j',platform:'youtube',about:'post',seen:'absent',minutes_after:15}};
 const second={...first,seq:3,at:'2026-11-13T12:00:00Z',d:{...first.d,seen:'present_private',minutes_after:2160}};
 assert.equal(lookTiming(second,s,[s,first]).status,'second_look');assert.equal(lookTiming(second,s,[s,first]).counts,true);
 assert.equal(lookTiming(second,s,[s,{...first,d:{...first.d,seen:'present_private'}}]).counts,false);
});
test('C44 violation platform is optional and remains one closed vocabulary value',t=>{
 const f=fixture(t);setup(f.ctx);
 for(const extra of [[],['--platform','youtube']]){
  const r=cli(f,'journal',['violation',...flags(f),'--rule','record_edited','--found-by','self_report',...extra]);
  assert.equal(r.status,0,r.stderr);assert.equal(last(f.ctx,'VIOLATION').d.platform,extra.length?'youtube':undefined);
 }
 assert.equal(cli(f,'journal',['violation',...flags(f),'--rule','record_edited','--found-by','self_report','--platform','bad']).status,70);
});
test('C44 safety stop needs 48h and a later own grant; old credit cannot restart',()=>{
 const at='2026-11-12T00:00:00Z',job={id:'j',run_class:'test_post'},stop={type:'NOTE',at,d:{platform:'youtube',code:'safety_stop'}};
 const grant={type:'GO_AHEAD',site:'owner',at,d:{job:'j',scope:'listed_test_posts',count:5,by:'owner'}};
 assert.equal(limits([stop,grant],job,'publish',['youtube'],'2026-11-13T23:59:59Z'),'platform_warning_hold');
 assert.equal(limits([stop,grant],job,'publish',['youtube'],'2026-11-14T00:00:01Z'),'safety_stop_new_go_ahead_required');
 assert.equal(limits([stop,{...grant,at:'2026-11-14T00:00:00Z'}],job,'publish',['youtube'],'2026-11-14T00:00:01Z'),null);
 assert.equal(limits([stop],job,'verify',['youtube'],'2026-11-12T00:01:00Z'),null);
});
for(const code of ['busy','browser_busy'])test('C44 fault is eligible again only after explicit '+code,t=>{
 const f=fixture(t);setup(f.ctx);const job={id:'job-1',site:'owner',run_class:'test_post',platforms:['youtube'],visibility:'private'};
 const kind='readback_timeout',rule=FAULTS[kind],fault={job:job.id,kind,mechanism:rule.mechanism,role:rule.role,trigger:rule.trigger,delay_ms:0,armed_on:'youtube_private'};
 fs.writeFileSync(path.join(f.home,'planned-runs.json'),JSON.stringify({runs:[{...job,faults:[fault]}]}));
 put(f.ctx,'FAULT',fault);
 put(f.ctx,'FAULT_RESULT',{job:job.id,kind,role:'publish',triggered:false,fired:false,failed:false,reason:'busy_not_reached',action_delay_ms:null,gone_delay_ms:null,kill_exit:null,processes:[]});
 const base={...step(f).d,exit:2};put(f.ctx,'STEP',{...base,answers:[{platform:'youtube',state:'NOT_STARTED',previous_state:null,code,press_flag:false,not_saved:false,answered:true,refs:[]}]});
 // First matching STEP must be the actual busy answer, not an unrelated attempt.
 const rows=readJournal(f.journal);assert.equal(selectFault(f.ctx,job,'publish',{tool:'vpub'}),null);
 put(f.ctx,'FAULT',fault);put(f.ctx,'FAULT_RESULT',{job:job.id,kind,role:'publish',triggered:false,fired:false,failed:false,reason:'busy_not_reached',action_delay_ms:null,gone_delay_ms:null,kill_exit:null,processes:[]});
 put(f.ctx,'STEP',{...base,answers:[{platform:'youtube',state:'NOT_STARTED',previous_state:null,code,press_flag:false,not_saved:false,answered:true,refs:[]}]});
 assert.ok(selectFault(f.ctx,job,'publish',{tool:'vpub'}));
 put(f.ctx,'FAULT',fault);assert.equal(selectFault(f.ctx,job,'publish',{tool:'vpub'}),null);
});
test('C44 controller records a busy arm as not reached',async t=>{
 const f=fixture(t);setup(f.ctx);const c=faultController(f.ctx,{job:'job-1',kind:'readback_timeout',role:'publish',delay_ms:0,armed_on:'youtube_private'});
 await c.finish(JSON.stringify({ok:false,state:'NOT_STARTED',error:{code:'busy'}}),2);
 assert.equal(last(f.ctx,'FAULT_RESULT').d.reason,'busy_not_reached');assert.equal(last(f.ctx,'FAULT_RESULT').d.fired,false);
});

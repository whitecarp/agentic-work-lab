import test from 'node:test';
import assert from 'node:assert/strict';
import fs from 'node:fs';
import path from 'node:path';
import {fixture,setup,approve,cli,flags,last} from './helpers.mjs';
import {make} from '../../scripts/round2/plan-runs.mjs';
import {begin,step} from '../../scripts/round2/gate-run.mjs';
import {put,readJournal,commitWords} from '../../scripts/round2/journal.mjs';
import {grantScope,limits} from '../../scripts/round2/policy.mjs';
import {pressEvidence,priorPressCount} from '../../scripts/round2/press-evidence.mjs';
import {faultController} from '../../scripts/round2/planned-faults.mjs';
import {privateToolCopy} from '../../scripts/round2/private-tool-copy.mjs';
import {secondLookDue,lookTiming} from '../../scripts/round2/looks.mjs';
import {canonical} from '../../scripts/round2/journal.mjs';
import {sha} from '../../scripts/round2/io.mjs';
import {reconcile} from '../../scripts/round2/reconcile.mjs';
import {exportData} from '../../scripts/round2/export.mjs';
const ready=t=>{const f=fixture(t);setup(f.ctx);approve(f.ctx);f.ctx.run=begin(f.ctx,{}).run;return f;};
const record=f=>put(f.ctx,'STEP',{job:'job-1',class:'write',tool:'vpub',action:'publish',role:'publish',platforms:['youtube'],exit:4,ms:0,capture_sha256:'a'.repeat(64),result:'in_doubt',code:null,refs:[],fault_fired:null,started:true},f.ctx.run);

test('C52 B1 positive merged counter needs a same-ref earlier baseline',()=>{
 const ev=[{seq:3,type:'command_start'},{seq:4,type:'command_end',platforms:{youtube:{ref:'r',final_presses:1}}}];
 assert.equal(pressEvidence(ev,true,'youtube').noPress,false);
 assert.equal(pressEvidence(ev,true,'youtube','post',0).noPress,false);
 assert.equal(pressEvidence(ev,true,'youtube','post',2).noPress,false);
 const old={seq:1,type:'transition',platform:'youtube',ref:'r',final_presses:1};
 const prior=priorPressCount([old,...ev],ev,'youtube');assert.equal(prior,1);
 assert.equal(pressEvidence(ev,true,'youtube','post',prior).noPress,true);
 assert.equal(priorPressCount([{...old,ref:'other'},...ev],ev,'youtube'),null);
 assert.equal(priorPressCount([{...old,platform:'douyin'},...ev],ev,'youtube'),null);
 assert.equal(pressEvidence(ev,false,'youtube','post',prior).noPress,false);
 const changed=[{seq:3,type:'command_start',platforms:{youtube:{ref:'r'}}},{seq:4,type:'command_end',platforms:{youtube:{ref:'new',final_presses:1}}}];
 const changedPrior=priorPressCount([old,...changed],changed,'youtube');
 assert.equal(changedPrior,null);
 assert.equal(pressEvidence(changed,true,'youtube','post',changedPrior).noPress,false);
});

test('C52 G1 publish, edit and real-release resume approvals cannot substitute',t=>{
 const f=fixture(t);setup(f.ctx);approve(f.ctx,'job-1',5);
 let rows=readJournal(f.journal);
 assert.equal(grantScope(rows,'job-1','write',[],'edit',['youtube'],'test_post'),null);
 assert.equal(grantScope(rows,'job-1','write',[],'resume',['youtube'],'real_release'),null);
 approve(f.ctx,'job-1',1,'one_write','edit');rows=readJournal(f.journal);
 assert.equal(grantScope(rows,'job-1','write',[],'edit',['youtube'],'test_post'),'one_write');
 assert.equal(grantScope(rows,'job-1','write',[{job:'job-1',class:'write',scope:'one_write',purpose:'edit'}],'edit',['youtube'],'test_post'),null);
 assert.equal(grantScope(rows,'job-1','write',[],'resume',['youtube'],'real_release'),null);
});

test('C52 G1 wrong site cannot record person authorization',t=>{
 const f=fixture(t);setup(f.ctx);
 const args=['go-ahead',...flags(f),'--job','job-1','--scope','one_write','--count','1','--by','tester'];
 assert.match(cli(f,'journal',args,'synthetic words').stderr,/invalid_person/);
 assert.equal(readJournal(f.journal).filter(r=>r.type==='GO_AHEAD').length,0);
});

test('C52 G3 one message commitment is reusable across jobs, not replayable on a job',t=>{
 const f=fixture(t);setup(f.ctx);
 const base=['go-ahead',...flags(f),'--scope','one_write','--count','1','--by','owner','--message-id','day-1'];
 for(const job of ['job-1','job-2'])assert.equal(cli(f,'journal',[...base,'--job',job],'same synthetic message').status,0);
 const grants=readJournal(f.journal).filter(r=>r.type==='GO_AHEAD');assert.equal(grants[0].d.words_sha256,grants[1].d.words_sha256);
 assert.match(cli(f,'journal',[...base,'--job','job-1'],'same synthetic message').stderr,/approval_message_replayed/);
 assert.throws(()=>commitWords(f.home,Buffer.from('changed message'),'day-1'),/message_id_words_changed/);
});

test('C52 B5 lost reservations after approval refuse without spawning',async t=>{
 const f=ready(t);fs.unlinkSync(path.join(f.home,'reservations.json'));
 assert.equal(await step(f.ctx,{job:'job-1',role:'publish'},[process.execPath,'-e','process.exit(99)']),5);
 assert.equal(last(f.ctx,'STEP_REFUSED').d.reason,'reservations_missing');assert.equal(last(f.ctx,'STEP'),undefined);
});

test('C52 B5 recorded writes still consume a replaced empty reservation cache',async t=>{
 const f=ready(t);record(f);fs.writeFileSync(path.join(f.home,'reservations.json'),'[]');
 assert.equal(await step(f.ctx,{job:'job-1',role:'verify'},[process.execPath,'-e','process.exit(99)']),5);
 assert.equal(last(f.ctx,'STEP_REFUSED').d.reason,'go_ahead_exhausted');
});

test('C52 T1 a refused publish does not authorize a resume',async t=>{
 const f=ready(t);await step(f.ctx,{job:'job-1',role:'publish'},[process.execPath,'-e','console.log(JSON.stringify({error:{code:"browser_busy"}}));process.exitCode=4']);
 approve(f.ctx,'job-1',1);
 assert.equal(await step(f.ctx,{job:'job-1',role:'resume'},[process.execPath,'-e','process.exit(99)']),5);
 assert.equal(last(f.ctx,'STEP_REFUSED').d.reason,'repeat_without_publish');
});

test('C52 G2 spacing includes a real release but its start is not a test cap unit',()=>{
 const now=new Date().toISOString(),job={id:'new',run_class:'test_post'};
 const reservations=[{job:'release',role:'publish',run_class:'real_release',started:true,platforms:['youtube'],at:now}];
 assert.equal(limits([],job,'publish',['youtube'],now,reservations),'publish_spacing');
 reservations[0].at=new Date(Date.now()-21*60000).toISOString();assert.equal(limits([],job,'publish',['youtube'],now,reservations),null);
});

test('C52 G17 warning hold allows continuation of an already started test post',()=>{
 const now=new Date().toISOString(),job={id:'job',run_class:'test_post'};
 const rows=[{type:'NOTE',at:now,d:{code:'platform_warning',platform:'youtube'}}];
 const reservations=[{job:'job',role:'publish',run_class:'test_post',started:true,platforms:['youtube'],at:now}];
 assert.equal(limits(rows,job,'publish',['youtube'],now,reservations),null);
 assert.equal(limits(rows,job,'publish',['youtube'],now,[]),'platform_warning_hold');
});

for(const state of ['WAITING_FOR_USER','IN_PROGRESS','UNKNOWN'])test('C52 B2 an unresolved '+state+' survives a busy helper and owes its second look',()=>{
 const at=n=>new Date(Date.parse('2026-11-20T00:00:00Z')+n*60000).toISOString();
 const first={seq:1,type:'STEP',at:at(0),d:{job:'j',platforms:['youtube'],role:'publish',started:true,class:'write',result:'in_doubt',answers:[{platform:'youtube',state,press_flag:state==='WAITING_FOR_USER'?true:null}]}};
 const busy={seq:2,type:'STEP',at:at(1),d:{...first.d,role:'repeat_same',result:'refused',answers:[{platform:'youtube',state:'NOT_STARTED',code:'browser_busy',press_flag:false}]}};
 const look={seq:3,type:'OBSERVATION',at:at(16),d:{job:'j',platform:'youtube',about:'post',seen:'absent',minutes_after:15}};
 const second={seq:4,type:'OBSERVATION',at:at(1501),d:{...look.d,minutes_after:1500}};
 assert.equal(secondLookDue(second,busy,[first,busy,look]),true);assert.equal(lookTiming(second,busy,[first,busy,look]).status,'second_look');
});

test('C52 G4 a complete answer before the kill prevents fault firing and any kill request',async t=>{
 const f=ready(t);let kills=0;
 const c=faultController(f.ctx,{job:'job-1',kind:'end_command_after_press',role:'publish',delay_ms:0,armed_on:'youtube_private'},{kill:async()=>{kills++;return true;}});
 c.attach({pid:99999999,exitCode:null,signalCode:null,once(){}});
 c.answer('{"ok":true,"data":{"state":"SUBMISSION_VERIFIED"}}');
 c.line('{"event":"browser_progress","platform":"youtube","stage":"pressing"}');
 c.line('{"event":"browser_progress","platform":"youtube","stage":"waiting for platform confirmation"}');
 assert.equal((await c.finish('{"ok":true}',0)).reason,'completed_before_kill');assert.equal(kills,0);assert.equal(last(f.ctx,'FAULT_RESULT').d.fired,false);
});

test('C52 B3 linked boundary suppression removes run, stage, event and span offsets',()=>{
 const source={schema:'crosscast-audit/1',times:'exact',runs:[{run:'r',awl_run_id:'a',t_start:'2026-11-20T23:59:50Z',t_end:'2026-11-20T23:59:55Z',seconds:5,stages:[['p','read','p','2026-11-20T23:59:53Z']],spans:[['p','browser',2,3]]}],events:[{seq:1,run:'r',type:'command_start',at:'2026-11-20T23:59:50Z',day:'2026-11-20'}],publications:[],losses:[]};
 const r=privateToolCopy(source,x=>x,new Set(['a']));
 assert.equal(r.runs[0].seconds,null);assert.equal(r.runs[0].t_end,null);assert.equal(r.runs[0].stages[0][3],null);assert.deepEqual(r.runs[0].spans[0].slice(2),[null,null]);assert.equal(r.events[0].at,null);
 assert.equal(source.runs[0].seconds,5);
});

for(const phase of ['2a','2b'])test('C52 G16 '+phase+' seal requires rules card and frozen analysis inputs',t=>{
 const f=fixture(t);setup(f.ctx);const file=path.join(f.root,'synthetic');fs.writeFileSync(file,'{}');fs.unlinkSync(path.join(f.home,'jobs.json'));make(f.home,JSON.parse(fs.readFileSync(new URL('./fixtures/planned-counts-v1.json',import.meta.url))),'owner',{dueFrom:'2020-01-01'});
 const base=['protocol-seal',...flags(f),'--phase',phase,'--protocol',file,'--lab-commit','a'.repeat(40),'--crosscast-commit','b'.repeat(40),'--plan',path.join(f.home,'planned-runs.json')];
 assert.match(cli(f,'journal',base).stderr,/rules_card_required/);
 if(phase==='2b')assert.match(cli(f,'journal',[...base,'--rules-card',file]).stderr,/freeze_digests_required/);
 assert.equal(cli(f,'journal',[...base,'--rules-card',file,'--analysis',file,'--crosscast-digest','c'.repeat(64)]).status,0);
});

test('C52 T14 controller initialization error normalizes to a schema-safe reason',async t=>{
 const f=ready(t),events={};
 const c=faultController(f.ctx,{job:'job-1',kind:'end_tree_after_press',role:'publish',delay_ms:0,armed_on:'youtube_private'},
  {snapshot:async()=>{throw new Error('synthetic private details');},kill:async()=>{throw new Error('must not kill');}});
 c.attach({pid:99999999,exitCode:null,signalCode:null,once(name,cb){events[name]=cb;}});events.spawn();
 const result=await c.finish('',4);assert.equal(result.reason,'process_controller_failed');assert.notEqual(result.fired,true);
 assert.equal(fs.readFileSync(f.journal,'utf8').includes('synthetic private details'),false);
});

test('C52 B3 cross-date case and its linked STEP cannot reveal duration',t=>{
 const f=ready(t);record(f);
 put(f.ctx,'OBSERVATION',{job:'job-1',platform:'youtube',ref:null,about:'post',by:'owner',client:'phone_app',seen:'present_private',fields_match:null,minutes_after:15,words_sha256:'a'.repeat(64),shot_sha256:null});
 const rows=readJournal(f.journal);let previous='0'.repeat(64);
 rows.forEach((r,i)=>{r.at=r.type==='OBSERVATION'?'2026-11-21T00:05:00.000Z':'2026-11-20T23:50:00.000Z';if(r.type==='GATE_DECISION')r.d.now=r.at;r.prev=previous;previous=sha(canonical(r));});
 fs.writeFileSync(f.journal,rows.map(canonical).join('\n')+'\n');fs.writeFileSync(f.journal+'.head',JSON.stringify({records:rows.length,head_sha256:previous}));
 const local=reconcile(f.ctx,undefined,{privacy:true,now:Date.parse('2026-11-21T00:10:00Z')});
 assert.equal(local.in_doubt[0].duration_withheld,true);assert.equal(local.in_doubt[0].minutes,null);
 const copy=exportData(f.ctx,{privacy:true});assert.equal(copy.records.find(r=>r.type==='STEP').d.ms,undefined);
 assert.equal(readJournal(f.journal).find(r=>r.type==='STEP').d.ms,0);
});

for(const crossDate of [false,true])test('C52 B3 reconcile fault delays respect cross-date withholding '+crossDate,t=>{
 const f=ready(t),kind='end_command_after_press';
 put(f.ctx,'FAULT',{job:'job-1',kind,role:'publish',mechanism:'wrapper_kill',trigger:'pressing',delay_ms:500,armed_on:'youtube_private'},f.ctx.run);
 record(f);
 put(f.ctx,'FAULT_RESULT',{job:'job-1',kind,role:'publish',triggered:true,fired:true,failed:false,processes:[],action_delay_ms:523,gone_delay_ms:612},f.ctx.run);
 put(f.ctx,'OBSERVATION',{job:'job-1',platform:'youtube',ref:null,about:'post',by:'owner',client:'phone_app',seen:'present_private',fields_match:null,minutes_after:15,words_sha256:'a'.repeat(64),shot_sha256:null});
 const rows=readJournal(f.journal);let previous='0'.repeat(64);
 rows.forEach(r=>{r.at=r.type==='OBSERVATION'?(crossDate?'2026-11-21T00:05:00.000Z':'2026-11-20T23:55:00.000Z'):'2026-11-20T23:40:00.000Z';if(r.type==='GATE_DECISION')r.d.now=r.at;r.prev=previous;previous=sha(canonical(r));});
 fs.writeFileSync(f.journal,rows.map(canonical).join('\n')+'\n');fs.writeFileSync(f.journal+'.head',JSON.stringify({records:rows.length,head_sha256:previous}));
 const options={now:Date.parse('2026-11-21T00:10:00Z')},source=fs.readFileSync(f.journal,'utf8');
 const local=reconcile(f.ctx,undefined,options).fault_delays[0];
 const shared=reconcile(f.ctx,undefined,{...options,privacy:true}).fault_delays[0];
 assert.equal(local.measured_ms,523);assert.equal(local.gone_ms,612);
 assert.equal(shared.measured_ms,crossDate?null:523);assert.equal(shared.gone_ms,crossDate?null:612);
 assert.equal(shared.listed_ms,500);assert.equal(shared.fired,true);
 assert.equal(fs.readFileSync(f.journal,'utf8'),source);
});

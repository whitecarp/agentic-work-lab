import test from 'node:test';
import assert from 'node:assert/strict';
import {fixture, at, bundle, gateHash, privateSite} from '../fixtures/round2-analysis.mjs';
import {analyze} from '../../paper/round2/metrics.mjs';
import {hash} from '../../paper/round2/inputs.mjs';
import {limits} from '../../scripts/round2/policy.mjs';
import {canonical} from '../../scripts/round2/journal.mjs';
import {runChecks, publishChecks} from '../../scripts/round2/analysis-checks.mjs';

const job = (id,extra={}) => ({id,run_class:'test_post',platforms:['youtube'],group:'B',faults:[],...extra});
const analyzeSites = (...sites) => analyze(bundle(...sites),{gateHash});
const whole = result => result.tables.find(t=>t.phase==='2b'&&t.platform===null).all_recorded;

for (const seen of ['present_private','present_once','present_twice_or_more','absent','unclear']) {
 test('F9 delete confirmed by verification followed by '+seen, () => {
  const f=fixture();
  f.step({m:3}); f.look({m:18});
  f.put('GO_AHEAD',{job:'job-a',scope:'one_delete',count:2,by:'owner',platform:'youtube'},20);
  f.step({role:'delete',kind:'deletion',state:'DELETE_PENDING',m:21,exit:4});
  f.step({role:'delete_verify',kind:'deletion',state:'DELETED_VERIFIED',cls:'read',contact:false,m:23});
  f.look({about:'delete',seen,m:42,minutes_after:21});
  const z=analyzeSites(f.finish()),expected=seen.startsWith('present');
  assert.equal(whole(z).readback.deletes[0].tool_gone,true);
  assert.equal(whole(z).must_be_zero.contradicted_confirmations,Number(expected));
  const row=z.checklist.all_recorded.find(c=>c.cell===7&&c.platform==='youtube');
  assert.equal(row.tries[0].contradicted,expected);
 });
}

for (const code of ['safety_stop','human_check','platform_warning']) {
 test('F2 '+code+' requires a fresh grant in the wrapper and the analysis', () => {
  const j=job('one'), p='youtube';
  const stop={type:'NOTE',seq:1,at:at(0),d:{platform:p,code}};
  const grant=m=>({type:'GO_AHEAD',site:'owner',at:at(m),seq:2,d:{job:j.id,scope:'one_write',count:1,by:'owner'}});
  const start={type:'STEP',seq:3,at:at(2881),d:{job:j.id,ms:1000,role:'publish',platforms:[p]}};
  for(const [minute,valid] of [[10,false],[2880,true]]){
   const rows=[stop,grant(minute)];
   assert.equal(limits(rows,j,'publish',[p],at(2880)),valid?null:'safety_stop_new_go_ahead_required');
   assert.equal(runChecks([...rows,start],[j],[])[0].safety_restarts[0].valid,valid);
  }
  assert.equal(limits([stop,grant(1)],j,'publish',[p],at(2879)),'platform_warning_hold');
 });
}

test('F11 excluded fired fault stays in all-recorded site/platform bounds', () => {
 const fault={kind:'readback_timeout',role:'publish',mechanism:'tool_switch',delay_ms:0};
 const f=fixture({jobs:[job('job-a',{faults:[fault]})]});
 f.put('FAULT',{job:'job-a',...fault},3); f.put('FAULT_RESULT',{job:'job-a',...fault,fired:true,failed:false},3);
 f.step({m:4}); f.look({m:20,minutes_after:16});
 const x=f.finish();
 const row=analyze(bundle(x),{gateHash,lastDay:'2026-11-19'}).pooled_fault_bounds[0];
 for(const bound of [row,row.by_site[0],row.by_platform[0]]){
  assert.equal(bound.counted.n,0);assert.equal(bound.all_recorded.n,1);
 }
});

test('F12 a practice gap is reported separately with clean counted evidence on both sites', () => {
 const f=fixture(); const seal=f.rows[0];
 // An early practice session with a gate decision lacking replay evidence.
 f.rows.splice(0,0,{...seal,at:at(-3),d:{...seal.d,phase:'2a',plan_sha256:null}},
  {...f.rows.find(r=>r.type==='GATE_DECISION'),at:at(-2),d:{...f.rows.find(r=>r.type==='GATE_DECISION').d,jobs:undefined}});
 const practiceGate=f.rows[1];delete practiceGate.d.jobs;
 f.step({m:4});f.look({m:20,minutes_after:16});
 const t=fixture({site:'tester'});t.rows.find(r=>r.type==='GO_AHEAD').d.scope='one_write';t.step({m:4});t.look({m:20,minutes_after:16});
 const z=analyzeSites(f.finish(),privateSite(t.finish()));
 assert.ok(z.verdict.shakedown_gaps.some(g=>g.phase==='2a'&&g.code==='unmeasured_gate_replay_disagreements'));
 assert.deepEqual(z.verdict.gaps,[]);
 assert.equal(z.verdict.status,'no_observed_must_be_zero_failures');
});

test('F6 resume presses and a subsequent verify confirms the attempt', () => {
 const fault={kind:'browser_lost_during_upload',role:'publish',mechanism:'wrapper_kill',delay_ms:0};
 const f=fixture({jobs:[job('job-a',{group:'D',faults:[fault]})]});
 f.step({m:3,press:0,state:'IN_PROGRESS',exit:4});
 const later=f.step({m:6,role:'resume',state:'SUBMITTED_UNVERIFIED',exit:4,press:1});
 f.step({m:8,role:'verify',state:'SUBMISSION_VERIFIED',contact:false});f.look({m:24});
 const a=whole(analyzeSites(f.finish())).submission.faults[0];
 assert.equal(a.later_press_step,later.seq);assert.equal(a.outcome,'finished_by_later_press');
});

test('F10 pair gap uses actual sequence, last upload end and second start, inclusive', () => {
 for(const [minutes,expected] of [[19.99,false],[20,true],[60,true],[60.01,false]]){
  const f=fixture({jobs:[job('second',{pair:'pair1',pair_mode:'one_at_a_time'}),job('first',{pair:'pair1',pair_mode:'two_at_a_time'})]});
  f.step({job:'first',m:5});f.step({job:'first',m:12,role:'resume'});
  f.step({job:'first',m:15,role:'verify',contact:false});
  const end=(12+minutes)*60000+1000;
  f.step({job:'second',m:end/60000});
  for(const c of f.tool.runs)c.concurrency={wall:1,sum:1};
  const z=analyzeSites(f.finish()),row=z.timing_pairs[0];
  assert.ok(Math.abs(row.gap_minutes-minutes)<0.0001);
  assert.equal(row.complete,expected);
 }
});

test('F5 only the named run is void for either write code or a test-post violation', () => {
 for(const rule of ['write_without_gate','write_without_go_ahead','test_post_rule_broken','retry_around_tool']){
  const f=fixture({jobs:[job('job-a'),job('job-b')]});
  for(const [i,id] of ['job-a','job-b'].entries()){
   f.step({job:id,m:3+i*30,ref:hash(id).slice(0,16)});f.look({job:id,m:20+i*30,minutes_after:17});
  }
  f.put('VIOLATION',{job:'job-a',platform:'youtube',rule,found_by:'self_report'},59);
  const rules=whole(analyzeSites(f.finish())).protocol.runs.map(j=>j.rules['6_no_voiding_violation']);
  assert.deepEqual(rules,[false,rule!=='retry_around_tool']);
 }
});

test('F1 an initial busy answer affects neither spacing nor daily cap, including completed reservations', () => {
 const j=job('new'), pub=(id,m,code=null)=>({type:'STEP',seq:m,run:'session',at:at(m),d:{job:id,ms:0,role:'publish',platforms:['youtube'],answers:[{platform:'youtube',state:code?'NOT_STARTED':'UNKNOWN',code}]}});
 const records=[pub('first',1,'browser_busy'),pub('first',6)];
 for(const saved of [[], records.map(r=>({run:r.run,step_seq:r.seq,role:'publish',platforms:['youtube'],run_class:'test_post',job:r.d.job,started:true,at:at(0)}))]){
  assert.equal(limits(records,j,'publish',['youtube'],at(22),saved,[job('first')]),'publish_spacing');
  assert.equal(limits(records,j,'publish',['youtube'],at(26),saved,[job('first')]),null);
 }
 assert.deepEqual(publishChecks(records,[job('first')]).map(x=>x.step),[6]);
 const many=['a','b','c','d'].map((id,i)=>pub(id,1+i*25,i===0?'busy':null));
 assert.equal(limits(many,j,'publish',['youtube'],at(200),[],many.map(r=>job(r.d.job))),null);
});

test('F4 rehearsal seal accepts only the supplied practice digest or skips an absent practice list', () => {
 const f=fixture({phase:'rehearsal'}),practice={schema:'round2-planned-runs/1',site:'owner',runs:[]};
 f.rows[0].d.plan_sha256=hash(canonical(practice)+'\n');
 f.step();f.look();const x=f.finish();
 assert.doesNotThrow(()=>analyzeSites(x));
 x.practicePlan=practice;assert.doesNotThrow(()=>analyzeSites(x));
 x.practicePlan={...practice,runs:[job('unrelated')]};assert.throws(()=>analyzeSites(x),/sealed_plan_digest_mismatch/);
});

import test from 'node:test';
import assert from 'node:assert/strict';
import fs from 'node:fs';
import os from 'node:os';
import path from 'node:path';
import {fixture,privateSite,bundle,gateHash} from '../fixtures/round2-analysis.mjs';
import {analyze} from '../../paper/round2/metrics.mjs';
import * as checks from '../../scripts/round2/analysis-checks.mjs';
import {reconcile} from '../../scripts/round2/reconcile.mjs';
import {canonical,keyedDigest} from '../../scripts/round2/journal.mjs';
const run=x=>analyze(bundle(x),{gateHash});
const all=r=>r.tables.find(t=>t.platform===null).all_recorded;

for(const site of ['owner','tester'])for(const seen of ['absent','present_private','present_twice_or_more','unclear']){
 test(`C41 4.4 readback-closed ${site} case is classified by its counting look: ${seen}`,()=>{
  const f=fixture({site});f.step({state:'UNKNOWN',exit:4});
  f.step({role:'verify',contact:false});f.look({seen,minutes_after:16});
  const x=f.finish(),z=all(run(site==='tester'?privateSite(x):x));
  assert.equal(z.in_doubt.cases.length,1);assert.equal(z.in_doubt.cases[0].by,'read');
  assert.equal(z.in_doubt.observed_outcomes[seen],1);
  assert.deepEqual(z.in_doubt.false_alarm_share,{n:seen==='absent'?1:0,denominator:1,share:seen==='absent'?1:0});
  assert.equal(z.in_doubt.click_adjusted_false_alarm_share.n,seen==='absent'?1:0);
  assert.equal(z.readback.contradicted.length,['absent','present_twice_or_more'].includes(seen)?1:0);
 });
}
test('C41 4.4 a person-closed absent case remains a false alarm, a missing look does not',()=>{
 for(const observed of [true,false]){
  const f=fixture();f.step({state:'UNKNOWN',exit:4});if(observed)f.look({seen:'absent'});
  const z=all(run(f.finish()));assert.equal(z.in_doubt.false_alarm_share.n,observed?1:0);
  assert.equal(z.in_doubt.observed_outcomes[observed?'absent':'unclear'],1);
 }
});

const timingFixture=site=>{
 const f=fixture({site});f.step({m:3});
 for(const m of [0,9,10,60,61,1440,1441])f.look({m:3+m,minutes_after:m});
 f.look({seen:'unclear',m:1500,minutes_after:1497});
 return f.finish();
};
for(const site of ['owner','tester'])test(`C41 7.3 ${site} counts every look, with boundary, unclear and day separation`,()=>{
 const x=timingFixture(site),z=all(run(site==='tester'?privateSite(x):x));
 assert.deepEqual(z.timing.human_look_rows.map(r=>r.status),['early','early','on_time','on_time','late','late','outside_window','unclear']);
 assert.equal(z.timing.human_look_rows.at(-1).timing_status,'outside_window');
 const days=z.timing.human_by_day.filter(d=>d.looks_total);
 assert.deepEqual(days.map(d=>[d.day,d.looks_total,d.looks]),[
  ['2026-11-20',5,{early:2,late:1,on_time:2}],
  ['2026-11-21',3,{late:1,outside_window:1,unclear:1}]]);
 assert.equal(z.submission.attempts[0].look_status,'late');
 assert.equal(x.reconcile.look_checks[0].observation,x.journal.records.filter(r=>r.type==='OBSERVATION')[5].seq);
 assert.ok(z.timing.human_look_rows.every(r=>r.source===(site==='tester'?'site_reconcile_attested_dates_only':'computed_from_exact_export_and_compared')));
});
test('C41 7.3 repeated, early, premature and no-obligation looks all count as human work',()=>{
 const f=fixture();f.step({m:3});
 f.look({m:4,minutes_after:15}); // Cannot be ground truth, but was recorded as a look.
 f.look({m:18,minutes_after:15});f.look({m:19,minutes_after:16});
 f.look({about:'edit',m:20,minutes_after:17});
 const x=f.finish(),z=all(run(x));
 assert.equal(z.timing.human_by_day[0].looks.on_time,4);
 assert.equal(x.reconcile.look_checks.length,1);
 assert.equal(x.reconcile.look_checks[0].recorded_before_claimed_minute.length,1);
});
test('C41 7.3 human look rows stay in their platform',()=>{
 const f=fixture({platforms:['youtube','douyin']});f.step();
 f.look({platform:'youtube'});f.look({platform:'douyin',seen:'unclear'});
 const z=run(f.finish());
 for(const p of ['youtube','douyin']){
  const rows=z.tables.find(t=>t.platform===p).all_recorded.timing.human_look_rows;
  assert.equal(rows.length,1);assert.equal(rows[0].platform,p);
 }
});
for(const damage of ['missing','not_array','duplicate','identity','invalid','unclear_mismatch'])test(`C41 7.3 private ${damage} attestation is unknown`,()=>{
 const f=fixture({site:'tester'});f.step();f.look();const x=privateSite(f.finish());
 const row=x.reconcile.observation_checks?.[0];
 if(damage==='missing')delete x.reconcile.observation_checks;
 else if(damage==='not_array')x.reconcile.observation_checks={};
 else if(damage==='duplicate')x.reconcile.observation_checks.push({...row});
 else if(damage==='identity')row.job='wrong';
 else if(damage==='invalid')row.status='pending';
 else row.status='unclear';
 const r=run(x),z=all(r);
 assert.equal(z.timing.human_look_rows[0].status,'unknown');
 assert.equal(z.timing.human_by_day[0].looks.unknown,1);
 assert.ok(r.verdict.gaps.some(g=>g.code==='observation_timing_unavailable'));
});
test('C41 7.3 owner recomputes every observation and detects changed comparison',()=>{
 const f=fixture();f.step();f.look();const x=f.finish();
 x.reconcile.observation_checks[0].status='early';
 const r=run(x);assert.ok(r.inputs[0].journal.exceptions.some(e=>e.code==='reconcile_disagreement'&&e.field==='observation_checks'));
 assert.equal(all(r).timing.human_look_rows[0].status,'on_time');
});
test('C41 7.3 no minutes means unknown; an unclear look keeps its separate outcome',()=>{
 const records=[undefined,null,-1,NaN,Infinity,'15'].map((m,i)=>({seq:i+1,type:'OBSERVATION',d:{job:'j',platform:'youtube',about:'post',seen:'absent',minutes_after:m}}));
 assert.ok(checks.observationChecks(records).every(r=>r.status==='unknown'));
 records[0].d.seen='unclear';const row=checks.observationChecks(records)[0];
 assert.equal(row.status,'unclear');assert.equal(row.timing_status,'unknown');
});
test('C41 7.3 a legacy platform omission is inferred only for a single-platform job',()=>{
 for(const platforms of [['youtube'],['youtube','douyin']]){
  const f=fixture({platforms});f.step();delete f.look().d.platform;
  const x=f.finish(),r=x.reconcile.observation_checks[0];
  assert.equal(r.platform,platforms.length===1?'youtube':null);
  assert.equal(all(run(x)).timing.human_look_rows.length,1);
 }
});
test('C41 actual reconcile retains every observation classification in dates-only output',t=>{
 const root=fs.mkdtempSync(path.join(os.tmpdir(),'c41-reconcile-'));
 t.after(()=>{assert.ok(path.resolve(root).startsWith(path.resolve(os.tmpdir())+path.sep));fs.rmSync(root,{recursive:true,force:true});});
 const x=timingFixture('owner'),journal=path.join(root,'journal.jsonl'),tool=path.join(root,'tool.json');
 fs.writeFileSync(journal,x.journal.records.map(canonical).join('\n')+'\n');
 fs.writeFileSync(journal+'.head',JSON.stringify({records:x.journal.source.records,head_sha256:x.journal.source.head_sha256}));
 fs.writeFileSync(tool,JSON.stringify(x.tool));fs.writeFileSync(path.join(root,'jobs.json'),JSON.stringify(x.plan.runs));fs.writeFileSync(path.join(root,'planned-runs.json'),canonical(x.plan)+'\n');
 keyedDigest('synthetic-c41-key',root,{create:true});
 const ctx={home:root,journal,site:'owner'},exact=reconcile(ctx,tool,{now:Date.parse(x.asOf)}),copy=reconcile(ctx,tool,{now:Date.parse(x.asOf),privacy:true});
 assert.equal(exact.observation_checks.length,8);
 assert.deepEqual(exact.observation_checks,x.reconcile.observation_checks);
 assert.deepEqual(copy.observation_checks,exact.observation_checks);
 assert.ok(!/minutes_after|\d{4}-\d\d-\d\dT\d\d:\d\d/.test(JSON.stringify(copy.observation_checks)));
});

test('C41 8.10 tester without real releases prints not applicable and no 24h unknown',()=>{
 const f=fixture({site:'tester'});f.step();f.look();const r=run(privateSite(f.finish())),z=all(r);
 assert.deepEqual(z.protocol.real_release_24h,{status:'not_applicable',reason:'tester_no_real_releases',checks:[]});
 assert.ok(!r.verdict.gaps.some(g=>g.code==='real_release_24h_unavailable'));
});
for(const last of [1439,1440])test(`C41 8.10 owner 24h readback boundary ${last} minutes`,()=>{
 const f=fixture({jobs:[{id:'job-a',run_class:'real_slot',platforms:['youtube'],group:'A',faults:[]}]});
 f.step({m:3});f.step({role:'verify',contact:false,m:13});f.look({m:33,minutes_after:30});
 f.step({role:'verify',contact:false,m:63});f.step({role:'verify',contact:false,m:3+last});
 f.put('JOB_END',{job:'job-a',reason:'done',by:'owner'},4+last);
 const z=all(run(f.finish()));
 assert.equal(z.protocol.real_release_24h.checks[0].status,last===1440?'passed':'missing');
 assert.equal(z.protocol.runs[0].closed.problems.includes('real_release_24h:youtube'),last!==1440);
});
test('C41 8.10 an unexpected dates-only real release is not hidden by tester not-applicable',()=>{
 const f=fixture({site:'tester',jobs:[{id:'job-a',run_class:'real_slot',platforms:['youtube'],group:'A',faults:[]}]});
 f.step();f.look();const r=run(privateSite(f.finish())),z=all(r);
 assert.equal(z.protocol.real_release_24h.checks[0].status,'unknown');
 assert.notEqual(z.protocol.real_release_24h.status,'not_applicable');
 assert.ok(r.verdict.gaps.some(g=>g.code==='real_release_24h_unavailable'));
});

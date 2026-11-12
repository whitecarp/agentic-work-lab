import test from 'node:test';
import assert from 'node:assert/strict';
import fs from 'node:fs';
import os from 'node:os';
import path from 'node:path';
import {fixture,privateSite,bundle,gateHash,at,digest} from '../fixtures/round2-analysis.mjs';
import {analyze} from '../../paper/round2/metrics.mjs';
import {gateChecks,publishChecks} from '../../scripts/round2/analysis-checks.mjs';
import {protectDigests} from '../../scripts/round2/export.mjs';
import {reconcile} from '../../scripts/round2/reconcile.mjs';
import {canonical,keyedDigest} from '../../scripts/round2/journal.mjs';
const run=x=>analyze(bundle(x),{gateHash});
const all=r=>r.tables.find(t=>t.platform===null).all_recorded;
function drill(named=true){
 const f=fixture({jobs:[{id:'drill-done',run_class:'drill',kind:'completed',platforms:[],group:'I',faults:[]}]});
 const end=f.put('JOB_END',{job:'drill-done',reason:'done',by:'operator',words_sha256:null},3);end.run=null;
 const base=f.rows.find(r=>r.type==='GATE_DECISION');
 const start=f.put('GATE_DECISION',{...base.d,now:at(4),slot_id:named?'drill-done':null,
  decision:'no_work_due',reason:'checked',due:[],jobs:base.d.jobs.map(j=>({...j,completed:true,verdict:'completed'}))},4);
 start.run='2'.repeat(32);return {f,start};
}
test('C39 completed drill finds named start after a sessionless closing',()=>{
 const {f,start}=drill(),row=all(run(f.finish())).gate.drills[0];
 assert.equal(row.passed,true);assert.deepEqual(row.decisions,[start.seq]);assert.equal(row.run,start.run);
});
test('C39 no named start is unmeasured even if an unrelated decision lists the drill',()=>{
 const {f}=drill(false),z=all(run(f.finish()));
 assert.equal(z.gate.drills[0].passed,null);assert.equal(z.gate.drill_bound.n,0);
});
test('C39 every named drill start is judged, including a later failing admission',()=>{
 const {f,start}=drill();f.put('GATE_DECISION',{...start.d,now:at(5),decision:'plan_only',due:['drill-done']},5).run='3'.repeat(32);
 const row=all(run(f.finish())).gate.drills[0];assert.equal(row.passed,false);assert.equal(row.decisions.length,2);
});
test('C39 already-running checklist includes every named attempt, not only the refusal',()=>{
 const f=fixture({jobs:[{id:'busy',run_class:'drill',kind:'already_running',platforms:[],group:'I',faults:[]}]});
 const first=f.rows.find(r=>r.type==='GATE_DECISION');first.d.slot_id='busy';
 const second=f.put('GATE_DECISION',{...first.d,now:at(3),running:true,decision:'skip',reason:'already_running',due:[]},3);second.run='2'.repeat(32);
 f.put('JOB_END',{job:'busy',reason:'done',by:'operator',words_sha256:null},4).run=null;
 const r=run(f.finish()),cell=r.checklist.all_recorded.find(c=>c.cell===5&&c.platform==='gate');
 assert.equal(cell.n,2);assert.equal(cell.confirmed,1);assert.equal(all(r).gate.drills[0].passed,false);
});
test('C39 only the exact public build digest path is exempt from keying',()=>{
 const input={crosscast:{builds:[{source_digest:digest,nested:{source_digest:digest}}],source_digest:digest},
  source_digest:digest,records:[{d:{capture_sha256:digest,source_digest:digest}}]};
 const out=protectDigests(input,x=>'keyed:'+x);
 assert.equal(out.crosscast.builds[0].source_digest,digest);
 for(const v of [out.crosscast.builds[0].nested.source_digest,out.crosscast.source_digest,out.source_digest,
  out.records[0].d.capture_sha256,out.records[0].d.source_digest])assert.equal(v,'keyed:'+digest);
});
test('C39 tester stamp is checked by digest and a changed digest excludes the run',()=>{
 const f=fixture({site:'tester'});f.rows.find(r=>r.type==='GO_AHEAD').d.scope='one_write';f.step();f.look();
 const x=privateSite(f.finish());let z=run(x);
 assert.equal(z.tables.find(t=>t.platform==='youtube').counted.submission.attempts.length,1);
 x.journal.crosscast.builds[0].source_digest='7'.repeat(64);z=run(x);
 assert.equal(all(z).must_be_zero.code_stamp_mismatches,1);
 assert.equal(z.tables.find(t=>t.platform==='youtube').counted.submission.attempts.length,0);
});
test('C39 date-only replay, spacing and cap failures come from the on-site comparison',()=>{
 const f=fixture({site:'tester',jobs:['a','b','c','d','e'].map(v=>({id:'job-'+v,run_class:'test_post',platforms:['youtube'],group:'B',faults:[]}))});
 f.rows.find(r=>r.type==='GATE_DECISION').d.reason='wrong';
 for(const [i,v]of ['a','b','c','d','e'].entries())f.step({job:'job-'+v,m:10+i*10,ref:v.repeat(16)});
 const x=privateSite(f.finish()),z=all(run(x));
 assert.equal(z.must_be_zero.gate_replay_disagreements,1);
 assert.equal(z.must_be_zero.test_spacing,4);assert.equal(z.must_be_zero.test_daily_cap,1);
 assert.equal(z.gate.replay_rows[0].source,'site_reconcile_attested_dates_only');
 assert.ok(z.protocol.cap_observations.every(c=>c.source==='site_reconcile_attested_dates_only'));
 assert.ok(!/\d{4}-\d\d-\d\dT\d\d:\d\d/.test(JSON.stringify([x.reconcile.gate_checks,x.reconcile.publish_checks])));
});
test('C39 missing or duplicate attested replay rows are unknown rather than agreeing',()=>{
 const f=fixture({site:'tester'});f.step();f.look();const x=privateSite(f.finish());
 x.reconcile.gate_checks.decisions.push({...x.reconcile.gate_checks.decisions[0]});
 assert.equal(all(run(x)).must_be_zero.gate_replay_disagreements,null);
 delete x.reconcile.gate_checks;assert.equal(all(run(x)).gate.replay.not_replayable,1);
});
test('C39 duplicate publish attestations cannot supply a clean cap or spacing result',()=>{
 const f=fixture({site:'tester'});f.step();f.look();const x=privateSite(f.finish());
 x.reconcile.publish_checks.push({...x.reconcile.publish_checks[0]});
 const z=all(run(x));assert.equal(z.must_be_zero.test_spacing,null);assert.equal(z.must_be_zero.test_daily_cap,null);
});
test('C39 owner checks are recomputed and comparison disagreement is an integrity failure',()=>{
 for(const field of ['gate_checks','publish_checks']){
  const f=fixture();f.step();f.look();const x=f.finish();
  if(field==='gate_checks')x.reconcile[field].decisions[0].replay='disagree';else x.reconcile[field][0].spacing=true;
  const result=run(x);assert.ok(result.inputs[0].journal.exceptions.some(e=>e.code==='reconcile_disagreement'&&e.field===field));
  assert.equal(result.tables.find(t=>t.platform==='youtube').counted.submission.attempts.length,0);
 }
});
test('C39 first publishes have exact 20-minute and rolling 24-hour boundaries',()=>{
 const jobs=Array.from({length:6},(_,i)=>({id:'j'+i,run_class:'test_post'}));
 const records=[0,20,40,60,80,1440].map((m,i)=>({seq:i+1,type:'STEP',at:at(m),d:{job:'j'+i,role:'publish',platforms:['youtube'],ms:0,exit:0}}));
 let c=publishChecks(records,jobs);assert.ok(c.every(r=>r.spacing===false));assert.equal(c[4].daily,true);assert.equal(c[5].daily,true);
 // At exactly 24h the first record is excluded; only three other starts remain.
 c=publishChecks(records.filter(r=>r.seq!==5),jobs);assert.equal(c.at(-1).daily,false);
 records[1].at=at(19.999);assert.equal(publishChecks(records,jobs)[1].spacing,true);
 const repeat={...records[0],seq:20,at:at(1450)};
 assert.equal(publishChecks([...records,repeat],jobs).length,6);
});
test('C39 missing/reversed start times and redacted gate inputs stay unmeasured',()=>{
 const f=fixture();const r=f.step();delete r.d.ms;
 assert.equal(publishChecks(f.rows,f.plan.runs)[0].daily,null);
 const gate=f.rows.find(r=>r.type==='GATE_DECISION');gate.d.now=gate.d.now.slice(0,10);
 const c=gateChecks(f.rows);assert.deepEqual(c.unmeasured,[gate.seq]);assert.equal(c.agree.length,0);
});
test('C39 actual privacy reconcile carries the same checks and no exact clocks',t=>{
 const root=fs.mkdtempSync(path.join(os.tmpdir(),'c39-reconcile-'));
 t.after(()=>{assert.ok(path.resolve(root).startsWith(path.resolve(os.tmpdir())+path.sep));fs.rmSync(root,{recursive:true,force:true});});
 const f=fixture();f.step();f.look();const x=f.finish(),journal=path.join(root,'journal.jsonl'),tool=path.join(root,'tool.json');
 fs.writeFileSync(journal,x.journal.records.map(canonical).join('\n')+'\n');
 fs.writeFileSync(journal+'.head',JSON.stringify({records:x.journal.source.records,head_sha256:x.journal.source.head_sha256}));
 fs.writeFileSync(tool,JSON.stringify(x.tool));fs.writeFileSync(path.join(root,'jobs.json'),JSON.stringify(x.plan.runs));
 keyedDigest('create-test-key',root,{create:true});
 const ctx={home:root,journal,site:'owner'},exact=reconcile(ctx,tool,{now:Date.parse(x.asOf)}),copy=reconcile(ctx,tool,{now:Date.parse(x.asOf),privacy:true});
 assert.deepEqual(copy.gate_checks,exact.gate_checks);assert.deepEqual(copy.publish_checks,exact.publish_checks);
 assert.ok(!/\d{4}-\d\d-\d\dT\d\d:\d\d/.test(JSON.stringify(copy)));
});

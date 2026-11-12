import test from 'node:test';
import assert from 'node:assert/strict';
import fs from 'node:fs';
import {generate} from '../../scripts/round2/plan-runs.mjs';
import {analyze} from '../../paper/round2/metrics.mjs';
import {fixture,bundle,gateHash,privateSite} from '../fixtures/round2-analysis.mjs';

test('C51 planned Douyin delete faults stay unarmed after owner removal at both sites',()=>{
 const counts=JSON.parse(fs.readFileSync(new URL('./fixtures/planned-counts-v1.json',import.meta.url)));
 let planned=0;
 for(const site of ['owner','tester']){
  const {plan}=generate(counts,site,Buffer.alloc(32,7),{dueFrom:'2026-11-19T00:00:00Z'});
  const deletes=plan.runs.filter(r=>r.platforms.join()==='douyin').flatMap(r=>r.faults.filter(f=>f.role==='delete'));
  assert.equal(deletes.length,site==='owner'?3:1);planned+=deletes.length;
  const job={id:'job-a',run_class:'test_post',platforms:['douyin'],group:'B',visibility:'private',edit:false,faults:[deletes[0]]};
  const f=fixture({site,platforms:['douyin'],jobs:[job]});f.step({m:3});f.look({m:18});
  const removal=f.put('NOTE',{job:'job-a',platform:'douyin',code:'manual_step',words_sha256:'a'.repeat(64)},20);
  f.look({about:'delete',seen:'absent',m:35});
  f.put('JOB_END',{job:'job-a',reason:'done',by:site,platform_settlements:[{platform:'douyin',how:'removed_by_hand'}]},36);
  const x=f.finish(),look=x.reconcile.look_checks.find(c=>c.about==='delete');
  assert.equal(look.step,removal.seq);assert.equal(look.status,'on_time');
  assert.deepEqual(x.reconcile.looks_owed,[]);
  assert.ok(!x.journal.records.some(r=>r.type==='FAULT'||r.type==='FAULT_RESULT'||r.type==='STEP'&&r.d.role==='delete'));
  for(const sample of [x,privateSite(x)]){
   const z=analyze(bundle(sample),{gateHash}),w=z.tables.find(t=>t.phase==='2b'&&t.platform===null).all_recorded;
   const faults=w.submission.faults;assert.equal(faults.length,1);
   assert.equal(faults[0].kind,'delete_readback_lost');assert.equal(faults[0].planned,true);
   assert.equal(faults[0].armed,0);assert.equal(faults[0].state,'not_armed');
   assert.equal(faults[0].look,'absent');assert.equal(faults[0].outcome,'gone_unconfirmed');
   assert.deepEqual(w.readback.deletes,[]);assert.deepEqual(z.verdict.gaps,[]);
   assert.equal(z.checklist.all_recorded.find(c=>c.cell===7&&c.platform==='douyin').status,'not_attempted');
  }
 }
 assert.equal(planned,4);
});

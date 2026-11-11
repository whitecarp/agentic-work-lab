import test from 'node:test';
import assert from 'node:assert/strict';
import fs from 'node:fs';
import path from 'node:path';
import {generate,make,check,reveal} from '../../scripts/round2/plan-runs.mjs';
import {canonical,put,verifyJournal} from '../../scripts/round2/journal.mjs';
import {fixture,setup,cli} from './helpers.mjs';
const counts=JSON.parse(fs.readFileSync(new URL('./fixtures/planned-counts-v1.json',import.meta.url)));
const options={dueFrom:'2026-11-19T00:00:00Z'};
for(const site of ['owner','tester'])test(`planned ${site} totals, variants, paired adjacency and determinism`,()=>{
 const a=generate(counts,site,Buffer.alloc(32,7),options),b=generate(counts,site,Buffer.alloc(32,7),options);
 assert.equal(canonical(a),canonical(b));assert.notEqual(canonical(a),canonical(generate(counts,site,Buffer.alloc(32,8),options)));
 assert.equal(a.plan.runs.length,site==='owner'?129:19);assert.doesNotMatch(JSON.stringify(a.jobs),/fault/);
 for(const r of a.plan.runs){
  if(r.pair){const other=a.plan.runs.find(x=>x.pair===r.pair&&x.id!==r.id);assert.equal(Math.abs(other.order-r.order),1);assert.notEqual(r.pair_mode,other.pair_mode);}
  assert.equal(new Set(r.faults.map(f=>f.role)).size,r.faults.length);
  if(r.wrong_delete_code_first)assert.ok(!r.faults.some(f=>f.role==='delete'));
  for(const f of r.faults){assert.equal(r.run_class,'test_post');assert.match(f.armed_on,/^(youtube_(private|unlisted)|douyin_private)$/);assert.ok([0,500,2000].includes(f.delay_ms));}
 }
 const tests=a.plan.runs.filter(r=>r.run_class==='test_post');
 assert.equal(new Set(tests.map(r=>r.clip+':'+r.clip_no)).size,tests.length);
 for(const p of ['youtube','douyin']){
  assert.equal(tests.filter(r=>r.edit&&r.platforms.includes(p)).length,site==='owner'?5:2);
  assert.equal(tests.filter(r=>r.platforms.includes(p)&&r.faults.some(f=>f.role==='edit')).length,site==='owner'?2:0);
  assert.equal(tests.filter(r=>r.platforms.includes(p)&&r.faults.some(f=>f.role==='delete')).length,site==='owner'?3:1);
 }
 for(const job of a.jobs.filter(r=>r.run_class==='test_post')){
  assert.equal(job.listed_writes,2+3*job.platforms.length+(site==='owner'&&job.edit?job.platforms.length:0));
  assert.equal(job.delete_writes,(job.wrong_delete_code_first?3:2)*job.platforms.length);
 }
 if(site==='tester')assert.equal(new Set(a.plan.runs.map(r=>r.clip_no)).size,19);
 else{
  const slots=a.plan.runs.filter(r=>r.run_class==='real_slot');assert.equal(new Set(slots.map(r=>r.day)).size,3);
  const hands=a.plan.runs.filter(r=>r.kind==='handoff');assert.equal(new Set(hands.map(r=>r.day)).size,3);
  for(const h of hands){const next=a.plan.runs[h.order];assert.equal(next.id,h.uses);assert.equal(next.run_class,'refusal');assert.equal(next.day,h.day);}
 }
});
test('operator identifiers and cross-references describe only shuffled order',()=>{
 for(const site of ['owner','tester']){
  const {plan,jobs}=generate(counts,site,Buffer.alloc(32,7),options);
  for(const [i,job] of jobs.entries()){
   const id=`${site}-${String(i+1).padStart(3,'0')}`;
   assert.equal(job.id,id);assert.equal(plan.runs[i].id,id);
   assert.ok(!Object.hasOwn(job,'group'));assert.ok(!Object.hasOwn(job,'faults'));
   if(job.pair){const first=jobs.find(j=>j.pair===job.pair);assert.equal(job.pair,`pair-${first.id}`);}
   if(job.uses)assert.equal(job.uses,jobs[i+1].id);
  }
 }
});
test('make never overwrites, same seed produces identical bytes, check catches one byte',t=>{
 const a=fixture(t),b=fixture(t);for(const f of [a,b])fs.writeFileSync(path.join(f.home,'plan-seed'),Buffer.alloc(32,9));
 const result=make(a.home,counts,'owner',options);make(b.home,counts,'owner',options);
 assert.deepEqual(fs.readFileSync(path.join(a.home,'planned-runs.json')),fs.readFileSync(path.join(b.home,'planned-runs.json')));
 assert.equal(check(a.home,result.sha256).matches,true);assert.throws(()=>make(a.home,counts,'owner',options),/exists/);
 fs.appendFileSync(path.join(a.home,'planned-runs.json'),' ');assert.throws(()=>check(a.home,result.sha256),/digest/);
});
test('insufficient clips and inconsistent counts leave no generated lists',t=>{
 const f=fixture(t);assert.throws(()=>make(f.home,counts,'tester',{...options,firstShort:80}),/pool_exhausted/);
 assert.ok(!fs.existsSync(path.join(f.home,'planned-runs.json')));assert.ok(!fs.existsSync(path.join(f.home,'jobs.json')));
 const wrong=structuredClone(counts);wrong.totals_to_check.owner.test_posts++;assert.throws(()=>generate(wrong,'owner',Buffer.alloc(32),options),/total_mismatch/);
});
test('reveal requires explicit closing after last step and matching sealed bytes',t=>{
 const f=fixture(t),made=make(f.home,counts,'owner',options);setup(f.ctx);
 put(f.ctx,'PROTOCOL_SEAL',{protocol_sha256:'a'.repeat(64),lab_commit:'b'.repeat(40),crosscast_commit:'c'.repeat(40),phase:'2b',crosscast_digest:null,plan_sha256:made.sha256,analysis_sha256:null,rules_card_sha256:null});
 const sign=closing=>{const h=verifyJournal(f.journal);return put(f.ctx,'SIGN_OFF',{day:new Date().toISOString().slice(0,10),by:'owner',answer:'yes',words_sha256:null,head_sha256:h.head_sha256,records:h.records,closing});};
 sign(false);assert.throws(()=>reveal(f.ctx),/closing/);sign(true);assert.equal(reveal(f.ctx).sha256,made.sha256);assert.throws(()=>reveal(f.ctx));
});
test('make CLI uses only isolated private home',t=>{
 const f=fixture(t),r=cli(f,'plan-runs',['make','--counts',path.resolve('tests/round2/fixtures/planned-counts-v1.json'),'--site','tester','--due-from',options.dueFrom]);
 assert.equal(r.status,0,r.stderr);assert.equal(JSON.parse(r.stdout).runs,19);
});

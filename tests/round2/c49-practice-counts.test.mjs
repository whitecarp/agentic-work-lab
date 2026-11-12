import test from 'node:test';
import assert from 'node:assert/strict';
import fs from 'node:fs';
import path from 'node:path';
import {generate,make,verifyPlan} from '../../scripts/round2/plan-runs.mjs';
import {fixture,cli} from './helpers.mjs';
const file=new URL('../../templates/round2/practice-counts-phase2a-v1.json',import.meta.url);
const counts=JSON.parse(fs.readFileSync(file));
const core=JSON.parse(fs.readFileSync(new URL('./fixtures/planned-counts-v1.json',import.meta.url)));
const options={phase:'2a',dueFrom:'2026-11-14T15:00:00Z'};
const build=(c=counts,seed=0)=>generate(c,'owner',Buffer.alloc(32,seed),options);

test('C49/C51 practice is exactly eighteen private uploads, six refusals and seven drills',()=>{
 const {plan,jobs}=build();
 assert.equal(plan.runs.length,31);assert.equal(jobs.length,32);
 assert.deepEqual(plan.totals,{test_post_runs:18,test_posts:18,faults_around_the_press:9,faults_before_the_press:4,
  refusal_runs:6,drills:7,repeats_after_a_delete_press:2,bilibili_real_release_slots:0,of_which_handoff_drills:1,gate_drills_of_the_six_kinds:6});
 assert.ok(plan.runs.every(r=>r.pair===null&&r.pair_mode===null));
 const posts=plan.runs.filter(r=>r.run_class==='test_post');
 for(const p of ['youtube','douyin'])assert.equal(posts.filter(r=>r.platforms.join()===p).length,p==='youtube'?10:8);
 assert.ok(posts.every(r=>r.visibility==='private'&&r.clip==='short'));
 assert.deepEqual(posts.map(r=>r.clip_no).sort((a,b)=>a-b),Array.from({length:18},(_,i)=>i+1));
 assert.equal(new Set(posts.map(r=>r.title)).size,18);
 assert.equal(plan.runs.filter(r=>r.run_class==='real_slot').length,0,'no publication is invented for a Bilibili dry run');
 assert.equal(counts.additional_checks.bilibili_delete_dry_run.tries,1);
});
test('C49/C51 lists each kind once per platform except YouTube C2 twice, early and late',()=>{
 for(let seed=0;seed<12;seed++){
  const {plan}=build(counts,seed);
  for(const platform of ['youtube','douyin']){
   const faults=plan.runs.filter(r=>r.platforms.join()===platform).flatMap(r=>r.faults);
   const observed={};for(const f of faults)observed[f.kind]=(observed[f.kind]??0)+1;
   assert.deepEqual(observed,{...Object.fromEntries(Object.keys(observed).map(k=>[k,1])),end_tree_after_press:platform==='youtube'?2:1});
   assert.deepEqual(Object.keys(observed).sort(),['end_command_after_press','end_tree_after_press','readback_timeout','between_save_and_press',
    'browser_lost_during_upload','stop_during_upload','end_browser_after_edit_save','delete_readback_lost'].sort());
   const late=faults.filter(f=>f.delay_ms===10000);assert.equal(late.length,platform==='youtube'?1:0);
   assert.ok(late.every(f=>f.kind==='end_tree_after_press'));
  }
 }
});
test('C49/C51 clean variants survive shuffle; no extra plain Douyin deletion posts',()=>{
 for(let seed=0;seed<12;seed++){
  const {plan}=build(counts,seed);
  for(const platform of ['youtube','douyin']){
   const clean=plan.runs.filter(r=>r.run_class==='test_post'&&r.platforms.join()===platform&&!r.faults.some(f=>f.role==='publish'));
   assert.equal(clean.length,platform==='youtube'?3:2);
   assert.equal(clean.filter(r=>r.edit&&r.faults.length===0).length,1);
   assert.equal(clean.filter(r=>r.edit&&r.faults.length===2&&r.faults.some(f=>f.role==='edit')&&r.faults.some(f=>f.role==='delete')).length,1);
   const plain=clean.filter(r=>!r.edit&&!r.wrong_delete_code_first&&r.faults.length===0);
   assert.equal(plain.length,0);
   const wrong=clean.filter(r=>r.wrong_delete_code_first);
   assert.equal(wrong.length,platform==='youtube'?1:0);assert.ok(wrong.every(r=>r.faults.length===0));
  }
 }
});
test('C49 all refusal and gate kinds once; handoff attaches to a refusal within five practice days',()=>{
 const {plan}=build(),refusals=plan.runs.filter(r=>r.run_class==='refusal'),drills=plan.runs.filter(r=>r.run_class==='drill');
 assert.deepEqual(refusals.map(r=>r.kind).sort(),['changed_words','changed_video','changed_cover','changed_platform_list','changed_visibility','stop_before_write'].sort());
 assert.deepEqual(drills.map(r=>r.kind).sort(),['stop_flag','not_owner','already_running','not_authorized','completed','not_due','handoff'].sort());
 assert.ok(plan.runs.filter(r=>r.day!==undefined).every(r=>r.day>=1&&r.day<=5));
 const handoff=drills.find(r=>r.kind==='handoff'),next=plan.runs[handoff.order];
 assert.equal(next.id,handoff.uses);assert.equal(next.run_class,'refusal');assert.equal(next.day,handoff.day);
});
test('C49 practice IDs and clips do not collide with counted runs after practice retirement',()=>{
 const practice=build(),counted=generate(core,'owner',Buffer.alloc(32,3),{dueFrom:'2026-11-19',firstShort:19});
 const ids=new Set(counted.jobs.map(j=>j.id));assert.ok(practice.jobs.every(j=>j.id.startsWith('owner-practice-')&&!ids.has(j.id)));
 const clips=new Set(practice.plan.runs.filter(r=>r.run_class==='test_post').map(r=>r.clip+':'+r.clip_no));
 assert.ok(counted.plan.runs.filter(r=>r.run_class==='test_post').every(r=>!clips.has(r.clip+':'+r.clip_no)));
 assert.equal(Math.max(...counted.plan.runs.filter(r=>r.run_class==='test_post'&&r.clip==='short').map(r=>r.clip_no)),50);
 assert.equal(counts.caps.test_uploads_per_platform_per_24h,4);assert.equal(counts.caps.minutes_between_test_uploads_on_one_platform,20);
});
test('C49 make and verify retain raw provenance and never replace an existing practice list',t=>{
 const f=fixture(t),raw=fs.readFileSync(file),before=raw.toString();
 const result=make(f.home,counts,'owner',{...options,countsRaw:raw});
 assert.equal(result.runs,31);assert.equal(verifyPlan(f.home).sha256,result.sha256);
 assert.equal(fs.readFileSync(file,'utf8'),before);
 assert.throws(()=>make(f.home,counts,'owner',options),/plan_already_exists/);
});
test('C49 phase-2a CLI makes only an isolated synthetic list',t=>{
 const f=fixture(t),r=cli(f,'plan-runs',['make','--counts',path.resolve('templates/round2/practice-counts-phase2a-v1.json'),
  '--site','owner','--phase','2a','--due-from',options.dueFrom]);
 assert.equal(r.status,0,r.stderr);assert.equal(JSON.parse(r.stdout).runs,31);assert.equal(verifyPlan(f.home).matches,true);
});
for(const bad of ['counted_phase','tester','version1','day_count','unknown_variant','edit_fault_without_edit','delete_and_wrong_code','public_variant','faulted_publish_variant']){
 test('C49 refuses '+bad,()=>{
  const c=structuredClone(counts),o={...options};let site='owner';
  if(bad==='counted_phase')o.phase='2b';if(bad==='tester'){site='tester';c.sites.tester=c.sites.owner;}
  if(bad==='version1')o.version=1;if(bad==='day_count')c.practice_days=6;
  const r=c.sites.owner.test_posts[1];
  if(bad==='unknown_variant')r.practice.typo=true;
  if(bad==='edit_fault_without_edit')r.practice.edit=false;
  if(bad==='delete_and_wrong_code')r.practice.wrong_delete_code_first=true;
  if(bad==='public_variant')r.visibility='public';
  if(bad==='faulted_publish_variant')r.fault='readback_timeout';
  assert.throws(()=>generate(c,site,Buffer.alloc(32),o),/invalid_practice/);
 });
}

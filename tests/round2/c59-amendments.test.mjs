import test from 'node:test';
import assert from 'node:assert/strict';
import {fixture,bundle,gateHash,at,privateSite} from '../fixtures/round2-analysis.mjs';
import {analyze} from '../../paper/round2/metrics.mjs';

const result=(f,options={})=>analyze(bundle(f.finish()),{gateHash,...options});
const whole=z=>z.tables.find(t=>t.platform===null&&t.phase==='2b').all_recorded;
const cell=(z,id)=>z.checklist.all_recorded.find(c=>c.cell===id&&c.platform==='youtube');
const job=(id,extra={})=>({id,run_class:'test_post',platforms:['youtube'],group:'B',faults:[],...extra});

for(const [label,index,voided,seen,expected] of [
 ['missing middle',4,false,null,'confirmed'],['missing last',9,false,null,'partly_confirmed'],
 ['voided agreement',9,true,'present_private','partly_confirmed'],
 ['voided contradiction',4,true,'absent','unconfirmed_effect_happened'],
])test('A1 '+label,()=>{
 const jobs=Array.from({length:10},(_,i)=>job('post-'+i));
 const f=fixture({site:'tester',jobs});
 for(const [i,j] of jobs.entries()){
  f.step({job:j.id,m:3+i*30,ref:String(i).padStart(16,'0')});
  if(i===index&&voided)f.put('VIOLATION',{job:j.id,rule:'test_post_rule_broken',platform:'youtube'},4+i*30);
  if(i!==index||seen)f.look({job:j.id,m:18+i*30,seen:i===index?seen:'present_private'});
 }
 const c=cell(result(f),2);
 assert.equal(c.n,10);assert.equal(c.tool_confirmed,10);assert.equal(c.confirmed,9);
 assert.equal(c.status,expected);assert.equal(c.last_three_confirmed,index<7);
});

test('A2 real YouTube release finished from a kept draft stays in cell 1',()=>{
 const f=fixture({jobs:[job('job-a',{group:'C',run_class:'real_release'})]});
 f.step({state:'UNKNOWN',press:1,exit:4,m:3});
 const s=f.step({role:'resume',m:4,press:2,before:{final_presses:1,still_a_draft:true}});
 const i=f.tool.events.findIndex(e=>e.run===s.d.awl_run_id&&e.type==='stage');
 f.tool.events.splice(i,0,{type:'transition',run:s.d.awl_run_id,at:at(3.99),platform:'youtube',
  kind:'browser',ref:'f'.repeat(16),state:'UNKNOWN',final_presses:1,still_a_draft:true});
 f.look({seen:'present_once',m:20,minutes_after:16});
 const z=result(f);assert.equal(cell(z,1).n,1);assert.equal(cell(z,3).n,0);
});

for(const late of [false,true])test('A6 UTC cutoff, first publish '+(late?'late':'on time'),()=>{
 const f=fixture({jobs:[job('job-a',{edit:true})]});
 f.step({role:'plan',cls:'read',m:3,contact:false,press:null});
 const publish=f.step({m:late?970:950});
 f.step({role:'repeat_same',m:971,state:'SKIPPED_EXISTING',press:1});
 f.look({m:988,minutes_after:17});
 f.step({role:'edit',kind:'edit',state:'EDIT_VERIFIED',m:991});
 f.look({about:'edit',m:1006});
 const z=result(f,{lastDay:'2026-11-20'}),t=z.tables.find(t=>t.platform===null);
 const j=t.all_recorded.protocol.runs[0];
 assert.equal(j.dated_step,publish.seq);assert.equal(j.run_day,late?'2026-11-21':'2026-11-20');
 assert.equal(j.counted,!late,JSON.stringify(j.rules));
 assert.equal(t.all_recorded.readback.edits.length,1);assert.equal(t.counted.readback.edits.length,late?0:1);
});

test('A6 gate drill is dated by the first gate decision explicitly naming it',()=>{
 const id='job-a',f=fixture({jobs:[job(id,{run_class:'drill',kind:'already_running'})]});
 const gate=f.put('GATE_DECISION',{...f.rows.at(-1).d,slot_id:id},970);
 const j=whole(result(f,{lastDay:'2026-11-20'})).protocol.runs[0];
 assert.equal(j.dated_step,gate.seq);assert.equal(j.rules['1_counted_phase'],false);
});

test('A7 owner output is independent of tester input and withholds combined results',()=>{
 const o=fixture();o.step();o.look();const owner=o.finish();
 const t=fixture({site:'tester',jobs:[job('private-tester-canary')]});
 t.step({job:'private-tester-canary'});t.look({job:'private-tester-canary'});const tester=privateSite(t.finish());
 const analyzeOwner=(...sites)=>analyze(bundle(...sites),{gateHash,ownerOnly:true});
 const z=analyzeOwner(owner);
 for(const other of [tester,{site:'tester',get journal(){throw Error('tester read');}}])
  assert.deepEqual(analyzeOwner(other,owner),z);
 assert.ok(!JSON.stringify(z).includes('private-tester-canary'));
 for(const rows of Object.values(z.checklist))for(const c of rows){
  if([1,8].includes(c.cell))assert.ok('n' in c);
  else {assert.equal(c.status,'held_back');assert.ok(!('n' in c));}
 }
 assert.equal(z.pooled_fault_bounds.status,'held_back');
 assert.deepEqual(Object.values(z.pooled_secondary_bounds).map(b=>b.status),Array(3).fill('held_back'));
 assert.ok(z.tables.every(t=>t.site==='owner'));
 const both=analyze(bundle(owner,tester),{gateHash});assert.ok(both.all_sites_present);
 assert.ok(Array.isArray(both.pooled_fault_bounds));assert.ok(JSON.stringify(both).includes('private-tester-canary'));
});

import test from 'node:test';
import assert from 'node:assert/strict';
import fs from 'node:fs';
import path from 'node:path';
import {put} from '../../scripts/round2/journal.mjs';
import {reconcile} from '../../scripts/round2/reconcile.mjs';
import {daily} from '../../scripts/round2/daily.mjs';
import {fixture,setup} from './helpers.mjs';

function started(f,changes={}){
 return put(f.ctx,'STEP',{job:'job-1',class:'write',tool:'vpub',action:'publish',role:'publish',platforms:['youtube'],exit:0,ms:10,capture_sha256:'a'.repeat(64),result:'ok',code:null,refs:[],fault_fired:false,...changes});
}
function look(f,minutes,changes={}){
 return put(f.ctx,'OBSERVATION',{job:'job-1',ref:null,about:'post',by:'owner',client:'phone_app',seen:'present_private',fields_match:null,minutes_after:minutes,words_sha256:'b'.repeat(64),shot_sha256:null,...changes});
}
for(const [minutes,status,owed] of [[0,'early',1],[9,'early',1],[10,'on_time',0],[60,'on_time',0],[61,'late',0],[1440,'late',0],[1441,'no_ground_truth',1]]){
 test(`look at ${minutes} minutes is ${status} in reconcile and daily`,t=>{
  const f=fixture(t);setup(f.ctx);const s=started(f);look(f,minutes);
  const now=Date.parse(s.at)+minutes*60000,r=reconcile(f.ctx,undefined,{now});
  assert.equal(r.look_checks[0].status,status);assert.equal(r.looks_owed.length,owed);
  const d=daily(f.ctx,{now});assert.equal(d.lines.length,7);
  assert.match(d.lines[4],new RegExp('"'+status+'":1'));assert.deepEqual(d.checked.look_checks,r.look_checks);
 });
}
test('missing or only early/unclear/wrong-scope looks never become ground truth at 24 hours',t=>{
 const f=fixture(t);setup(f.ctx);const s=started(f),start=Date.parse(s.at);
 assert.equal(reconcile(f.ctx,undefined,{now:start+1000}).look_checks[0].status,'pending');
 look(f,2);look(f,15,{seen:'unclear'});look(f,15,{job:'other'});look(f,15,{about:'delete'});
 assert.equal(reconcile(f.ctx,undefined,{now:start+86399999}).look_checks[0].status,'early');
 const r=reconcile(f.ctx,undefined,{now:start+86400000});assert.equal(r.look_checks[0].status,'no_ground_truth');
 assert.equal(r.look_checks[0].observation,null);assert.equal(r.looks_owed.length,1);
 assert.match(daily(f.ctx,{now:start+86400000}).lines[4],/"no_ground_truth":1/);
});
test('early then late satisfies the look while preserving the early and late evidence',t=>{
 const f=fixture(t);setup(f.ctx);const s=started(f),early=look(f,3),late=look(f,90);
 look(f,1500);const r=reconcile(f.ctx,undefined,{now:Date.parse(s.at)+172800000});
 assert.equal(r.looks_owed.length,0);assert.equal(r.look_checks[0].status,'late');
 assert.equal(r.look_checks[0].observation,late.seq);assert.deepEqual(r.look_checks[0].early_observations,[early.seq]);
});
test('an on-time look remains on time after later observations',t=>{
 const f=fixture(t);setup(f.ctx);started(f);const first=look(f,60);look(f,61);
 const r=reconcile(f.ctx);assert.equal(r.look_checks[0].status,'on_time');assert.equal(r.look_checks[0].observation,first.seq);
});
test('read-only edit checks, drills, refusals and children never started owe no look',t=>{
 const f=fixture(t);setup(f.ctx,[{id:'job-1',run_class:'test_post'},{id:'drill',run_class:'drill'},{id:'refusal',run_class:'refusal'}]);
 started(f,{class:'read',role:'edit'});started(f,{job:'drill'});started(f,{job:'refusal'});started(f,{exit:127,result:'failed'});
 const s=started(f);fs.writeFileSync(path.join(f.home,'reservations.json'),JSON.stringify([{run:s.run,step_seq:s.seq,started:false}]));
 assert.equal(reconcile(f.ctx).look_checks.length,0);assert.equal(reconcile(f.ctx).looks_owed.length,0);
});
test('real-slot edits and deletes require their matching looks, including late ones',t=>{
 const f=fixture(t);setup(f.ctx,[{id:'job-1',run_class:'real_slot'}]);
 started(f,{role:'edit',action:'edit'});started(f,{role:'delete',action:'delete'});look(f,15);
 assert.equal(reconcile(f.ctx).looks_owed.length,2);look(f,70,{about:'edit'});look(f,1440,{about:'delete',seen:'absent'});
 const r=reconcile(f.ctx);assert.equal(r.looks_owed.length,0);assert.deepEqual(r.look_checks.map(x=>x.status),['late','late']);
});

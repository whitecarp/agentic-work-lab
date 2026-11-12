import test from 'node:test';
import assert from 'node:assert/strict';
import {outcome} from '../../scripts/round2/policy.mjs';
import {reconcile} from '../../scripts/round2/reconcile.mjs';
import {begin} from '../../scripts/round2/gate-run.mjs';
import {put} from '../../scripts/round2/journal.mjs';
import {fixture,setup,approve} from './helpers.mjs';
function record(f,results,exit=4,role='publish'){
 const platforms=Object.keys(results),o=outcome(exit,JSON.stringify({ok:exit===0,data:{results}}),{role,platforms});
 return put(f.ctx,'STEP',{job:'job-1',class:'write',tool:'vpub',action:'publish',role,platforms,exit,ms:1,capture_sha256:'a'.repeat(64),result:o.result,code:o.code,answered:o.answered,started:true,confirmed_state:o.confirmed_state,answers:o.answers,refs:[],fault_fired:null},f.ctx.run);
}
function ready(t){const f=fixture(t);setup(f.ctx);approve(f.ctx,'job-1',20);f.ctx.run=begin(f.ctx,{}).run;return f;}
for(const part of [{state:'NOT_STARTED',code:'browser_busy'},{state:'NOT_STARTED',code:'stopped'},{state:'PREPARATION_INCOMPLETE',final_submit_attempted:false},{state:'WAITING_FOR_USER',final_submit_attempted:false},{state:'NEEDS_A_PERSON'},{state:'SUBMISSION_VERIFIED',chapters_state:'PENDING'}]){
 test('C37 A1 no case for '+JSON.stringify(part),t=>{const f=ready(t);const r=record(f,{youtube:part});assert.equal(reconcile(f.ctx).in_doubt.length,0);assert.equal(r.d.answers[0].state,part.state);assert.equal(r.d.code,part.code??null);});
}
test('C37 A1 each platform opens from its own answer and keeps previous state',t=>{
 const f=ready(t);record(f,{youtube:{state:'SUBMISSION_VERIFIED'},douyin:{state:'SKIPPED_EXISTING',previous_state:'UNKNOWN',code:'injected_fault'}},4,'repeat_same');
 const r=reconcile(f.ctx);assert.equal(r.in_doubt.length,1);assert.equal(r.in_doubt[0].platform,'douyin');
});
test('C37 A1 unrecognized words and arbitrary messages are never recorded',()=>{
 const o=outcome(4,JSON.stringify({data:{results:{youtube:{state:'PRIVATE TITLE',code:'private-path',message:'private',previous_state:'secret'}}}}),{role:'publish',platforms:['youtube']});
 assert.equal(o.answers[0].state,'UNLISTED');assert.equal(o.answers[0].code,null);assert.equal(o.answers[0].previous_state,'UNLISTED');assert.ok(!JSON.stringify(o).includes('PRIVATE'));
});
for(const role of ['repeat_same','changed_plan','resume'])test('C37 A2 cutoff '+role+' opens a case',t=>{
 const f=ready(t),o=outcome(3,'',{role,platforms:['youtube']});
 put(f.ctx,'STEP',{job:'job-1',class:'write',tool:'vpub',action:'publish',role,platforms:['youtube'],exit:3,ms:1,capture_sha256:'a'.repeat(64),result:o.result,code:o.code,answered:o.answered,started:true,confirmed_state:null,answers:o.answers,refs:[],fault_fired:null},f.ctx.run);
 assert.equal(reconcile(f.ctx).in_doubt.length,1);
});
test('C37 A7 a readback not saved cannot settle',t=>{
 const f=ready(t);record(f,{youtube:{state:'UNKNOWN'}});record(f,{youtube:{state:'SUBMISSION_VERIFIED',not_saved:'private detail'}},0,'verify');
 assert.equal(reconcile(f.ctx).in_doubt_open,1);
});

for(const state of ['WAITING_FOR_USER','PREPARATION_INCOMPLETE','NEW_BACKEND_STATE']){
 for(const flag of [true,false,undefined])test('C38 Annex 4.1 '+state+' press='+flag,t=>{
  const f=ready(t),step=record(f,{youtube:{state,final_submit_attempted:flag}});
  const opens=flag!==false||state==='NEW_BACKEND_STATE';
  assert.equal(reconcile(f.ctx).in_doubt.length,Number(opens));
  if(opens)assert.equal(step.d.result,'in_doubt');
 });
}
test('verified prior receipt settles a skipped repeat',t=>{
 const f=ready(t);record(f,{youtube:{state:'SKIPPED_EXISTING',previous_state:'SUBMISSION_VERIFIED',final_submit_attempted:true}},4,'repeat_same');
 assert.equal(reconcile(f.ctx).in_doubt.length,0);
});

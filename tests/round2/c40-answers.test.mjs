import test from 'node:test';
import assert from 'node:assert/strict';
import fs from 'node:fs';
import {outcome} from '../../scripts/round2/policy.mjs';
import {reconcile} from '../../scripts/round2/reconcile.mjs';
import {begin} from '../../scripts/round2/gate-run.mjs';
import {put} from '../../scripts/round2/journal.mjs';
import {fixture,setup,approve} from './helpers.mjs';
const worker=JSON.parse(fs.readFileSync(new URL('./fixtures/c40-worker-answer.json',import.meta.url),'utf8'));
const read=(v,role='publish',platforms=['youtube'],exit=4)=>outcome(exit,JSON.stringify(v),{role,platforms});

test('C40 worker build stamp does not hide its own error word',()=>{
 const r=read(worker);
 assert.equal(r.code,'layout_changed');assert.equal(r.answers[0].code,'layout_changed');
 assert.equal(r.answers[0].press_flag,false);assert.equal(r.result,'needs_person');
 assert.ok(!JSON.stringify(r).includes('SYNTHETIC_PRIVATE_MESSAGE'));
 assert.ok(!JSON.stringify(r).includes('source_digest'));
});
for(const [label,part,envelope,expected] of [
 ['direct string wins',{code:'stopped',error:{code:'layout_changed'}},'browser_timeout','stopped'],
 ['numeric code falls back',{code:7,error:{code:'layout_changed'}},'browser_timeout','layout_changed'],
 ['own error wins',{code:{commit:'private'},error:{code:'layout_changed'}},'browser_timeout','layout_changed'],
 ['envelope fallback',{code:{commit:'private'}},'browser_timeout','browser_timeout'],
 ['non-string own error',{code:{},error:{code:{private:'value'}}},'browser_timeout','browser_timeout'],
 ['missing code',{code:{}},undefined,null],
 ['invalid string stays sanitized',{code:'PRIVATE PATH',error:{code:'layout_changed'}},'browser_timeout',null],
])test('C40 code precedence: '+label,()=>{
 const r=read({error:{code:envelope},data:{state:'NOT_STARTED',...part}});
 assert.equal(r.code,expected);
});
for(const flag of [false,true,undefined])test('C40 pending delete uses its own flag '+flag,t=>{
 const f=fixture(t);setup(f.ctx);approve(f.ctx,'job-1',2,'one_delete');f.ctx.run=begin(f.ctx,{}).run;
 const r=read({data:{state:'DELETE_PENDING',delete_attempted:flag,final_submit_attempted:flag!==true}},'delete');
 assert.equal(r.answers[0].press_flag,flag??null);
 assert.equal(r.result,flag===false?'refused':'in_doubt');
 put(f.ctx,'STEP',{job:'job-1',class:'write',tool:'vpub',action:'delete',role:'delete',platforms:['youtube'],
  exit:4,ms:1,capture_sha256:'a'.repeat(64),result:r.result,code:r.code,answered:r.answered,
  started:true,confirmed_state:r.confirmed_state,answers:r.answers,refs:[],fault_fired:null},f.ctx.run);
 assert.equal(reconcile(f.ctx).in_doubt.length,Number(flag!==false));
});
test('C40 verified already-gone delete keeps false and stays confirmed',()=>{
 const r=read({data:{state:'DELETED_VERIFIED',delete_attempted:false,final_submit_attempted:true}},'delete',['youtube'],0);
 assert.equal(r.result,'ok');assert.equal(r.confirmed_state,'DELETED_VERIFIED');assert.equal(r.answers[0].press_flag,false);
});
test('C40 delete verify uses deletion flag and post keeps its own nested flag',()=>{
 const r=read({data:{state:'DELETED_VERIFIED',delete_attempted:false}},'delete_verify',['youtube'],0);
 assert.equal(r.answers[0].press_flag,false);
 const p=read({data:{state:'SKIPPED_EXISTING',previous_state:'PREPARATION_INCOMPLETE',receipt:{final_submit_attempted:false,delete_attempted:true}}},'repeat_same');
 assert.equal(p.answers[0].press_flag,false);assert.notEqual(p.result,'in_doubt');
});
test('C40 mixed platforms keep separate codes and deletion flags',()=>{
 const r=read({error:{code:'browser_timeout'},data:{results:{
  youtube:{state:'DELETE_PENDING',delete_attempted:false,code:{},error:{code:'page_not_ready'}},
  douyin:{state:'DELETE_PENDING',delete_attempted:true,code:{},error:{code:'layout_changed'}}
 }}},'delete',['youtube','douyin']);
 assert.deepEqual(r.answers.map(a=>[a.platform,a.code,a.press_flag]),[['youtube','page_not_ready',false],['douyin','layout_changed',true]]);
 assert.equal(r.result,'in_doubt');
});

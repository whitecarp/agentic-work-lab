import test from 'node:test';
import assert from 'node:assert/strict';
import fs from 'node:fs';
import {outcome} from '../scripts/round2/policy.mjs';
import {analyze} from '../paper/round2/metrics.mjs';
import {fixture,privateSite,bundle,gateHash} from './fixtures/round2-analysis.mjs';
const all=x=>analyze(bundle(x),{gateHash}).tables.find(t=>t.platform===null).all_recorded;
const worker=JSON.parse(fs.readFileSync(new URL('./round2/fixtures/c40-worker-answer.json',import.meta.url),'utf8'));
test('C40 summary and outcome table both declare current Annex A 1.13',()=>{
 const f=fixture();f.step();f.look();const r=analyze(bundle(f.finish()),{gateHash});
 assert.equal(r.annex,'1.13');assert.equal(r.outcome_table.version,'Annex A '+r.annex);
});
test('C40 Annex 4.5 preserves the worker error through exported analysis',()=>{
 const f=fixture(),o=outcome(4,JSON.stringify(worker),{role:'publish',platforms:['youtube']});
 const s=f.step({state:'PREPARATION_INCOMPLETE',press:0,exit:4});for(const k of ['result','code','answers','confirmed_state','answered'])s.d[k]=o[k];
 const r=all(f.finish());assert.equal(r.in_doubt.cases.length,0);assert.equal(r.in_doubt.stops_before_press[0].code,'layout_changed');
});
for(const site of ['owner','tester'])for(const state of ['DELETE_PENDING','DELETED_VERIFIED'])for(const flag of [false,true,undefined]){
 test(`C40 Annex 5.4 ${site} ${state} delete flag ${flag}`,()=>{
  const f=fixture({site});f.step();f.look();
  const exit=state==='DELETED_VERIFIED'?0:4;
  const o=outcome(exit,JSON.stringify({data:{state,code:{commit:'a'.repeat(40)},error:{code:'layout_changed'},delete_attempted:flag,final_submit_attempted:true}}),{role:'delete',platforms:['youtube']});
  const s=f.step({role:'delete',kind:'deletion',state,press:0,exit,m:20});for(const k of ['result','code','answers','confirmed_state','answered'])s.d[k]=o[k];
  // Incomplete audit evidence cannot be mistaken for an observed zero press count.
  f.tool.runs.at(-1).ended=false;
  f.look({about:'delete',seen:state==='DELETED_VERIFIED'?'absent':'present_private',m:35});
  const x=f.finish(),r=all(site==='tester'?privateSite(x):x),d=r.readback.deletes[0];
  assert.equal(d.pressed,flag??null);assert.equal(d.press_count,null);
  assert.equal(d.found_already_gone,state==='DELETED_VERIFIED'&&flag===false);
  assert.equal(d.failed_before_press,state==='DELETE_PENDING'&&flag===false);
  assert.equal(d.pending,state==='DELETE_PENDING'&&flag!==false);
  assert.equal(d.code,'layout_changed');
  assert.equal(r.in_doubt.cases.length,Number(state==='DELETE_PENDING'&&flag!==false));
 });
}

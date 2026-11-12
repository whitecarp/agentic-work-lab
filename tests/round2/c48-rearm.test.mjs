import test from 'node:test';
import assert from 'node:assert/strict';
import fs from 'node:fs';
import path from 'node:path';
import {fixture,setup,last} from './helpers.mjs';
import {put,readJournal} from '../../scripts/round2/journal.mjs';
import {FAULTS,selectFault,faultController} from '../../scripts/round2/planned-faults.mjs';
import {readAnswers} from '../../scripts/round2/answers.mjs';
import {assignC2Delays,generate,make,verifyPlan} from '../../scripts/round2/plan-runs.mjs';

const counts=JSON.parse(fs.readFileSync(new URL('./fixtures/planned-counts-v1.json',import.meta.url)));
const options={dueFrom:'2026-11-19T00:00:00Z'};
const kinds={publish:'readback_timeout',edit:'end_browser_after_edit_save',delete:'delete_readback_lost'};
function ready(t,role,platform='youtube'){
 const f=fixture(t),job={id:'job-1',site:'owner',run_class:'test_post',platforms:[platform],visibility:'private'};
 setup(f.ctx,[job]);
 const kind=kinds[role],rule=FAULTS[kind];
 const fault={job:job.id,kind,role,mechanism:rule.mechanism,trigger:rule.trigger,delay_ms:0,armed_on:platform+'_private'};
 fs.writeFileSync(path.join(f.home,'planned-runs.json'),JSON.stringify({runs:[{...job,faults:[fault]}]}));
 put(f.ctx,'FAULT',fault);
 return {...f,job,fault};
}
const reply=(code='browser_busy',platform='youtube')=>({ok:false,data:{results:{[platform]:{state:'NOT_STARTED',code}}}});
function record(f,value,exit=4,result={}){
 const {fault,job}=f,role=fault.role,about=role==='publish'?'post':role;
 put(f.ctx,'FAULT_RESULT',{job:job.id,kind:fault.kind,role,triggered:false,fired:false,failed:false,reason:null,
  action_delay_ms:null,gone_delay_ms:null,kill_exit:null,processes:[],...result});
 put(f.ctx,'STEP',{job:job.id,class:'write',tool:'vpub',action:role,role,platforms:job.platforms,exit,ms:1,
  capture_sha256:'1'.repeat(64),result:'refused',code:null,refs:[],fault_fired:false,started:true,
  answers:readAnswers(value,job.platforms,about)});
}
for(const [exit,code,state] of [[4,'browser_busy','NOT_STARTED'],[2,'busy','NOT_STARTED'],[0,'stopped','NOT_STARTED'],
 [3,'preflight_error','NOT_STARTED'],[2,'confirmation_required',null]]){
 test(`C48 publish rearms ${exit}/${code}/${state}`,async t=>{
  const f=ready(t,'publish'),value=state?reply(code):{ok:false,error:{code}};
  record(f,value,exit);
  const before=readJournal(f.journal);
  assert.deepEqual(selectFault(f.ctx,f.job,'publish',{tool:'vpub'}),f.fault);
  assert.deepEqual(readJournal(f.journal),before,'selecting the fault grants no step or approval');
  const c=faultController(f.ctx,f.fault);await c.finish(JSON.stringify(value),exit);
  const r=last(f.ctx,'FAULT_RESULT').d;
  assert.equal(r.reason,['busy','browser_busy'].includes(code)?'busy_not_reached':'publish_refused_not_reached');
  assert.deepEqual([r.triggered,r.fired,r.failed],[false,false,false]);
 });
}
for(const platform of ['youtube','douyin'])for(const role of ['edit','delete'])for(const code of ['busy','browser_busy']){
 test(`C48 ${platform} ${role} rearms ${code} whole refusal`,async t=>{
  const f=ready(t,role,platform),value={ok:false,error:{code}};
  record(f,value,2);assert.deepEqual(selectFault(f.ctx,f.job,role,{tool:'vpub'}),f.fault);
  const c=faultController(f.ctx,f.fault);await c.finish(JSON.stringify(value),2);
  assert.equal(last(f.ctx,'FAULT_RESULT').d.reason,'busy_not_reached');
 });
}
for(const role of ['edit','delete'])test(`C48 ${role} keeps exit-4 busy answer with explicit no-press receipt`,async t=>{
 const f=ready(t,role),flag=role==='delete'?'delete_attempted':'final_submit_attempted';
 const value={ok:false,data:{state:role==='delete'?'DELETE_PENDING':'EDIT_PREPARED',error:{code:'browser_busy'},[flag]:false}};
 record(f,value,4);assert.ok(selectFault(f.ctx,f.job,role,{tool:'vpub'}));
 const c=faultController(f.ctx,f.fault);await c.finish(JSON.stringify(value),4);
 assert.equal(last(f.ctx,'FAULT_RESULT').d.reason,'busy_not_reached');
});
for(const role of ['publish','edit','delete'])for(const bad of ['press','fired','triggered','failed','missing_answer']){
 test(`C48 ${role} never rearms ${bad}`,async t=>{
  const f=ready(t,role),value=reply(),part=value.data.results.youtube;
  if(bad==='press')part[role==='delete'?'delete_attempted':'final_submit_attempted']=true;
  if(bad==='missing_answer')delete value.data.results.youtube;
  record(f,value,4,['fired','triggered','failed'].includes(bad)?{[bad]:true}:{});
  assert.equal(selectFault(f.ctx,f.job,role,{tool:'vpub'}),null);
  if(['press','missing_answer'].includes(bad)){
   const c=faultController(f.ctx,f.fault);await c.finish(JSON.stringify(value),4);
   assert.equal(last(f.ctx,'FAULT_RESULT').d.reason,null);
  }
 });
}
for(const role of ['edit','delete'])test(`C48 ${role} does not broaden non-busy refusal rearming`,async t=>{
 const f=ready(t,role),value={ok:false,error:{code:'confirmation_required'}};
 record(f,value,2);assert.equal(selectFault(f.ctx,f.job,role,{tool:'vpub'}),null);
 const c=faultController(f.ctx,f.fault);await c.finish(JSON.stringify(value),2);
 assert.equal(last(f.ctx,'FAULT_RESULT').d.reason,null);
});
test('C48 publish needs its own NOT_STARTED answer, not another platform or an in-flight state',t=>{
 for(const value of [reply('browser_busy','douyin'),{ok:false,error:{code:'browser_busy'}},
  {ok:false,data:{results:{youtube:{state:'SUBMITTED_UNVERIFIED',code:'browser_busy',final_submit_attempted:true}}}}]){
  const f=ready(t,'publish');record(f,value,4);
  assert.equal(selectFault(f.ctx,f.job,'publish',{tool:'vpub'}),null);
 }
});
function amended(site,n){
 const c=structuredClone(counts),rows=c.sites[site].test_posts;
 const c2=rows.find(r=>r.platforms.join()==='youtube'&&r.fault==='end_tree_after_press');
 const c1=rows.find(r=>r.platforms.join()==='youtube'&&r.fault==='end_command_after_press');
 c1.runs+=c2.runs-n;c2.runs=n;return c;
}
for(const site of ['owner','tester'])for(const n of (site==='owner'?[0,1,2,3]:[0,1,2])){
 test(`C48 amended ${site} list with ${n} YouTube C2 faults`,t=>{
  const c=amended(site,n),before=JSON.stringify(c);
  const generated=generate(c,site,Buffer.alloc(32,3),options);
  const faults=generated.plan.runs.flatMap(r=>r.faults);
  const yt=faults.filter(f=>f.armed_on.startsWith('youtube_')&&f.kind==='end_tree_after_press');
  assert.equal(yt.length,n);assert.equal(yt.filter(f=>f.delay_ms===10000).length,n===0?0:n===3?2:1);
  assert.ok(faults.filter(f=>!yt.includes(f)).every(f=>[0,500,2000].includes(f.delay_ms)));
  assert.equal(JSON.stringify(c),before,'the approved counts are not rewritten');
  assert.deepEqual(generated,generate(c,site,Buffer.alloc(32,3),options));
  const f=fixture(t);make(f.home,c,site,options);assert.equal(verifyPlan(f.home).matches,true);
 });
}
for(const n of [0,1,2])test(`C48 amended practice helper with ${n} C2 faults`,()=>{
 const runs=Array.from({length:n},()=>({run_class:'test_post',platforms:['youtube'],
  faults:[{kind:'end_tree_after_press',role:'publish',delay_ms:500}]}));
 assignC2Delays(runs,'owner',()=>0,{practice:true});
 assert.equal(runs.filter(r=>r.faults[0].delay_ms===10000).length,n?1:0);
});
test('C48 excess counts and unsupported initial delays still refuse',()=>{
 const run=delay=>({run_class:'test_post',platforms:['youtube'],faults:[{kind:'end_tree_after_press',role:'publish',delay_ms:delay}]});
 assert.throws(()=>assignC2Delays([run(0),run(0),run(0)],'tester',()=>0),/invalid_c2_counts/);
 assert.throws(()=>assignC2Delays([run(10000)],'owner',()=>0),/invalid_c2_counts/);
});

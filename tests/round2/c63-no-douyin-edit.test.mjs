import test from 'node:test';
import assert from 'node:assert/strict';
import fs from 'node:fs';
import path from 'node:path';
import {begin,step,closeJob} from '../../scripts/round2/gate-run.mjs';
import {selectFault} from '../../scripts/round2/planned-faults.mjs';
import {put,readJournal} from '../../scripts/round2/journal.mjs';
import {fixture as offline,setup,approve,last} from './helpers.mjs';
import {fixture,bundle,privateSite,gateHash} from '../fixtures/round2-analysis.mjs';
import {completeRound} from '../fixtures/round2-complete.mjs';
import {analyze} from '../../paper/round2/metrics.mjs';

const whole=z=>z.tables.find(t=>t.platform===null).all_recorded;
const job={id:'job-1',class:'write',run_class:'test_post',platforms:['douyin'],edit:true,due_at:'2020-01-01'};

for(const readback of [false,true])test('A9 Douyin test edit refuses before child, reservation and fault'+(readback?' including read-back':''),async t=>{
 const f=offline(t);setup(f.ctx,[job]);approve(f.ctx,job.id,6,'listed_test_posts');
 f.ctx.run=begin(f.ctx,{}).run;
 const before=fs.readFileSync(path.join(f.home,'reservations.json'));
 // An intentionally absent executable in our temporary tree cannot touch any account.
 const command=[path.join(f.root,'vpub.exe'),'edit','a'.repeat(64),...(readback?['--verify-only']:[])];
 assert.equal(await step(f.ctx,{job:job.id,role:'edit'},command),5);
 assert.equal(last(f.ctx,'STEP_REFUSED').d.reason,'douyin_test_edit_not_attempted');
 assert.deepEqual(fs.readFileSync(path.join(f.home,'reservations.json')),before);
 assert.ok(!readJournal(f.journal).some(r=>['STEP','FAULT','FAULT_RESULT'].includes(r.type)));
 assert.equal(fs.existsSync(path.join(f.home,'captures')),false);
 // It cannot even read/arm a stale planned edit fault on this platform.
 assert.equal(selectFault({home:path.join(f.root,'absent')},job,'edit',{tool:'vpub'}),null);
});

for(const [platform,run_class] of [['youtube','test_post'],['douyin','real_release'],['douyin','real_slot']])
test('A9 preserves admitted '+platform+' '+run_class+' edit',async t=>{
 const f=offline(t),j={...job,platforms:[platform],run_class};setup(f.ctx,[j]);
 approve(f.ctx,j.id,1,'one_write','edit');f.ctx.run=begin(f.ctx,{}).run;
 assert.equal(await step(f.ctx,{job:j.id,role:'edit'},[process.execPath,'-e',
  'console.log(JSON.stringify({ok:true,state:"EDIT_VERIFIED"}))']),0);
 assert.equal(last(f.ctx,'STEP').d.role,'edit');
});

test('A9 listed edit is not required to close a manually removed Douyin test',t=>{
 const f=offline(t);setup(f.ctx,[job]);
 put(f.ctx,'NOTE',{job:job.id,platform:'douyin',code:'manual_step',agent:null,minutes_ago:null});
 const r=closeJob(f.ctx,{job:job.id,reason:'removed_by_hand',by:'owner'});
 assert.deepEqual(r.record.d.platform_settlements,[{platform:'douyin',how:'removed_by_hand'}]);
 assert.ok(!readJournal(f.journal).some(r=>r.type==='STEP'&&r.d.role==='edit'));
});

test('A9 whole round keeps counts, lists seven unrun Douyin edits and runs seven YouTube edits',()=>{
 const input=completeRound();
 input.checklist=JSON.parse(fs.readFileSync(new URL('../../paper/round2/checklist-v1.json',import.meta.url)));
 const z=analyze(input,{gateHash}),all=z.tables.filter(t=>t.platform===null&&t.phase==='2b').map(t=>t.all_recorded);
 assert.deepEqual(z.verdict.violations,[]);assert.deepEqual(z.verdict.gaps,[]);
 assert.equal(z.pooled_fault_bounds[0].counted.n,34);
 assert.equal(z.pooled_secondary_bounds[0].repeat.counted.n,100);
 assert.equal(z.pooled_secondary_bounds[0].changed_plan.counted.n,24);
 assert.equal(all.reduce((n,t)=>n+t.gate.drill_bound.n,0),60);
 assert.deepEqual(all.map(t=>t.readback.listed_edits_not_run.length),[5,2]);
 assert.equal(all.flatMap(t=>t.readback.edits).length,7);
 assert.ok(all.flatMap(t=>t.readback.edits).every(e=>e.platform==='youtube'));
 for(const site of input.sites){
  assert.ok(!site.journal.records.some(r=>['STEP','FAULT','FAULT_RESULT'].includes(r.type)&&r.d.role==='edit'&&(r.d.platforms?.includes('douyin')||r.d.armed_on==='douyin_private')));
 }
 const faults=all.flatMap(t=>t.submission.faults).filter(f=>f.role==='edit'&&f.platforms.includes('douyin'));
 assert.equal(faults.length,2);assert.ok(faults.every(f=>f.state==='not_armed'&&f.outcome==='listed_not_run'&&f.reason.includes('A9')));
 for(const view of ['counted','all_recorded']){
  const cell=z.checklist[view].find(c=>c.cell===6&&c.platform==='douyin');
  assert.equal(cell.status,'not_attempted');assert.equal(cell.n,0);assert.match(cell.reason,/A9/);
 }
 assert.ok(all.every(t=>t.protocol.runs.every(r=>r.closed?.problems.length===0)));
});

test('A9 historical review refusals keep the owed look and print stopped before typing',()=>{
 const f=fixture({platforms:['douyin'],jobs:[{id:'job-a',run_class:'test_post',platforms:['douyin'],group:'B',edit:true,faults:[]}]});
 f.step({m:3});f.look({m:18});
 for(const m of [20,40]){
  // The actual refusal has no answer state; its command snapshots retain the
  // earlier upload's positive marker, while its edit transition is pre-typing.
  const snap={ref:'f'.repeat(16),platform:'douyin',state:'SUBMISSION_VERIFIED',final_presses:1,final_submit_attempted:true};
  const s=f.step({role:'edit',kind:'edit',state:null,code:'edit_held_by_review',press:1,contact:false,exit:2,m,before:snap,after:snap});
  s.d.answers[0].press_flag=null;
  const events=f.tool.events.filter(e=>e.run===s.d.awl_run_id),end=events.at(-1);
  end.platforms.douyin={...snap};
  f.tool.events.splice(f.tool.events.indexOf(end),0,
   {type:'transition',run:s.d.awl_run_id,at:end.at,platform:'douyin',kind:'edit',state:'EDIT_PREPARING',platform_writes:null},
   {type:'call',run:s.d.awl_run_id,at:end.at,platform:'douyin',action:'edit',operation:null,ok:false});
  f.tool.events.forEach((e,i)=>{e.seq=i+1;});
 }
 const before=f.finish({cutoff:41,closing:false});
 assert.equal(before.reconcile.looks_owed.filter(x=>x.about==='edit').length,1);
 f.look({about:'edit',m:55,fields_match:false});
 f.put('NOTE',{job:'job-a',platform:'douyin',code:'manual_step',words_sha256:'a'.repeat(64)},56);
 f.look({about:'delete',seen:'absent',m:71});
 f.put('JOB_END',{job:'job-a',reason:'done',by:'owner',platform_settlements:[{platform:'douyin',how:'removed_by_hand'}]},72);
 const x=f.finish();
 assert.deepEqual(x.reconcile.looks_owed,[]);
 for(const s of [x,privateSite(x)]){
  const w=whole(analyze(bundle(s),{gateHash}));
  assert.equal(w.readback.edits.length,2);
  assert.ok(w.readback.edits.every(e=>e.status==='stopped_before_typing'&&e.code==='edit_held_by_review'&&!e.confirmed_by));
  assert.deepEqual(w.readback.listed_edits_not_run,[]);
  assert.deepEqual(w.readback.contradicted,[]);
  assert.equal(w.must_be_zero.edit_post_invariant_broken,0);
  assert.deepEqual(w.protocol.runs[0].closed.problems,[]);
 }
});

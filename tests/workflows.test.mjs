import test from 'node:test';
import assert from 'node:assert/strict';
import fs from 'node:fs';
import {prioritize} from '../scripts/prioritize-merchants.mjs';
import {planRun,verifyScheduledRun} from '../scripts/operations-gate.mjs';
const now='2026-10-28T12:00:00+08:00';
const base={executor:'new',owner:'new',now,jobs:[{id:'research',due_at:now,authorized:true}]};
test('old executor cannot duplicate work',()=>assert.equal(planRun({...base,executor:'old'}).reason,'not_owner'));
test('stop flag blocks planning',()=>assert.equal(planRun({...base,stop:true}).reason,'stop_flag'));
test('running task is not retriggered',()=>assert.equal(planRun({...base,running:true}).reason,'already_running'));
test('only due, authorized, unfinished work is selected',()=>assert.deepEqual(planRun({...base,jobs:[...base.jobs,{id:'future',due_at:'2026-10-29T12:00:00+08:00',authorized:true},{id:'done',due_at:now,authorized:true,completed:true},{id:'unapproved',due_at:now}]}).due,['research']));
test('invalid time is not treated as immediately due',()=>assert.throws(()=>planRun({...base,jobs:[{id:'bad',authorized:true,due_at:'unknown'}]})));
test('duplicate job IDs are rejected',()=>assert.throws(()=>planRun({...base,jobs:[...base.jobs,...base.jobs]})));
test('actual recorded scheduled run passes',()=>assert.equal(verifyScheduledRun(JSON.parse(fs.readFileSync(new URL('../data/scheduled-run-evidence.json',import.meta.url),'utf8'))),true));
test('manual or incomplete runs do not count as scheduled success',()=>{
 const r=JSON.parse(fs.readFileSync(new URL('../data/scheduled-run-evidence.json',import.meta.url),'utf8'));
 assert.equal(verifyScheduledRun({...r,manual_test_triggered:true}),false);
 assert.equal(verifyScheduledRun({...r,completed_at:null}),false);
 assert.equal(verifyScheduledRun({...r,task_errors:['failed']}),false);
});
test('missing merchant information requires research',()=>{
 const input=JSON.parse(fs.readFileSync(new URL('../examples/merchants-demo.json',import.meta.url),'utf8'));
 const r=prioritize(input);assert.equal(r.merchants[0].id,'DEMO-001');
 assert.equal(r.merchants.find(x=>x.id==='DEMO-002').next_step,'research_required');
 assert.ok(r.merchants.every(x=>x.outreach_sent===false));
});
test('malformed merchant scores cannot inflate ranking',()=>assert.throws(()=>prioritize({merchants:[{id:'bad',category_fit:10}]})));

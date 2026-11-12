import test from 'node:test';
import assert from 'node:assert/strict';
import {completeRound} from '../fixtures/round2-complete.mjs';
import {gateHash} from '../fixtures/round2-analysis.mjs';
import {analyze} from '../../paper/round2/metrics.mjs';
const totals=z=>z.tables.filter(t=>t.phase==='2b'&&t.platform===null).map(t=>t.counted);
test('C42 counts-file whole round: 34 fault runs, 100 repeats, 24 changed plans, 60 drills; all eligible cells confirmed',()=>{
 const z=analyze(completeRound(),{gateHash}),all=totals(z);
 assert.deepEqual(z.verdict.violations,[]);assert.deepEqual(z.verdict.gaps,[]);
 assert.equal(z.pooled_fault_bounds[0].counted.n,34);
 assert.equal(all.reduce((n,t)=>n+t.submission.repeat_bound.n,0),100);
 assert.equal(all.reduce((n,t)=>n+t.plan_binding.bound.n,0),24);
 assert.equal(all.reduce((n,t)=>n+t.gate.drill_bound.n,0),60);
 assert.equal(z.pooled_secondary_bounds[0].repeat.counted.n,100);
 assert.equal(z.pooled_secondary_bounds[0].changed_plan.counted.n,24);
 assert.deepEqual(z.checklist.counted.filter(c=>c.n>=3&&c.status!=='confirmed'),[]);
 assert.ok(all.every(t=>t.protocol.runs.every(r=>r.counted)));
});
test('C42 whole round detects a surviving second marker in resume after a killed command',()=>{
 const z=analyze(completeRound({plantSecond:true}),{gateHash});
 assert.equal(z.verdict.status,'observed_failures');
 assert.ok(z.verdict.violations.some(x=>x.name==='second_press_without_draft'&&x.n===1));
 assert.equal(z.pooled_fault_bounds[0].counted.bad,1);
});

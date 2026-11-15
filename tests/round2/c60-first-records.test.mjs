import test from 'node:test';
import assert from 'node:assert/strict';
import {fixture,at,bundle,privateSite,gateHash} from '../fixtures/round2-analysis.mjs';
import {analyze} from '../../paper/round2/metrics.mjs';

for (const [label, minute, helper] of [['before',2.9,false],['at',3,false],['after',3.1,true]]) {
 test('C1 worker saves confirmation '+label+' the recorded cutoff',()=>{
  const f=fixture();
  const fault={job:'job-a',role:'publish',kind:'end_command_after_press'};
  f.put('FAULT',{...fault,mechanism:'wrapper_kill',delay_ms:500},2);
  f.put('FAULT_RESULT',{...fault,fired:true,failed:false,processes:[{root:true,ended:true}]},3);
  const s=f.step({state:'UNKNOWN',press:1,exit:4,m:2.7});s.at=at(3.2);
  s.d.answered=false;s.d.result='unknown';s.d.answers=[];
  const c=f.tool.runs.at(-1);c.ended=false;c.exit_code=null;
  f.tool.events=f.tool.events.filter(e=>e.type!=='command_end');
  for(const e of [{type:'stage',stage:'reading back',at:at(2.8)},
                  {type:'transition',state:'SUBMISSION_VERIFIED',kind:'browser',ref:'f'.repeat(16),at:at(minute)}])
   f.tool.events.push({...e,seq:f.tool.events.length+100,run:c.run,platform:'youtube'});
  f.step({role:'resume',m:5});f.look({m:20});
  f.tool.events.forEach((event,i)=>{event.seq=i+101;});
  const x=f.finish();
  for (const site of [x,privateSite(x)]) {
   const table=analyze(bundle(site),{gateHash}).tables.find(t=>t.platform===null).all_recorded;
   assert.equal(table.submission.faults[0].helper_confirmed,helper);
   assert.equal(table.submission.helper_confirmed_C1,Number(helper));
  }
 });
}

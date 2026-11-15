import test from 'node:test';
import assert from 'node:assert/strict';
import {fixture,at,bundle,privateSite,gateHash} from '../fixtures/round2-analysis.mjs';
import {reconcileData} from '../../paper/round2/reconcile-export.mjs';
import {analyze} from '../../paper/round2/metrics.mjs';

for (const [label,minute,kind,helper] of [
 ['before',2.9,'browser',false],['at',3,'browser',false],['after',3.1,'browser',true],['ledger save after',3.1,'ledger',false],
]) {
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
                  {type:'transition',state:'SUBMISSION_VERIFIED',kind,ref:'f'.repeat(16),at:at(minute)}])
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

function liveCase(f) {
 const records=f.rows;
 const cc={...f.tool,present:true,chain_ok:true,losses:[]};
 const result=reconcileData(records,cc,{jobs:f.plan.runs,now:Date.parse(records.at(-1).at)});
 const check=result.look_checks.find(r=>r.about==='post');
 return {owed:check.second_look_owed,closed:result.in_doubt[0].settled_at_seq};
}

function releaseWithAbsentLook() {
 const f=fixture({jobs:[{id:'job-a',platforms:['youtube'],run_class:'real_release',faults:[]}]});
 f.step({state:'SUBMITTED_UNVERIFIED',press:1,m:3,exit:4});
 f.look({seen:'absent',m:18,minutes_after:15});
 return f;
}

test('4.3 a kept draft settles at the read after its pressing resume',()=>{
 const f=releaseWithAbsentLook();
 assert.deepEqual(liveCase(f),{owed:true,closed:null});
 f.step({role:'resume',press:2,m:20});
 assert.deepEqual(liveCase(f),{owed:true,closed:null});
 f.step({role:'verify',m:25,answers:{youtube:{refs:['d'.repeat(16)]}}});
 f.step({role:'verify',m:26,answers:{youtube:{not_saved:true}}});
 assert.deepEqual(liveCase(f),{owed:true,closed:null});
 const verify=f.step({role:'verify',m:30});
 assert.deepEqual(liveCase(f),{owed:false,closed:verify.seq});
 f.look({seen:'present_once',m:36,minutes_after:16});
 assert.deepEqual(liveCase(f),{owed:false,closed:verify.seq});
 f.step({role:'resume',press:3,m:40});
 assert.deepEqual(liveCase(f),{owed:true,closed:null});
 f.look({seen:'present_once',m:55,minutes_after:15});
 assert.deepEqual(liveCase(f),{owed:true,closed:null});
 const second=f.look({seen:'present_once',m:1303,minutes_after:1300});
 assert.equal(liveCase(f).closed,second.seq);
});

for(const result of ['refused','busy'])test('An absent release still owes a second look after a '+result+' resume',()=>{
 const f=releaseWithAbsentLook();
 const s=f.step({role:'resume',m:20,state:'NOT_STARTED',press:null,contact:false,exit:2});
 s.d.result=result;
 assert.deepEqual(liveCase(f),{owed:true,closed:null});
});

test('First absent test-post look after an unresolved resume starts its second-look window',()=>{
 const f=fixture();
 f.step({state:'SUBMITTED_UNVERIFIED',press:1,m:3,exit:4});
 f.step({role:'resume',state:'SUBMITTED_UNVERIFIED',press:1,m:12,exit:4});
 f.look({seen:'absent',m:27,minutes_after:15});
 assert.deepEqual(liveCase(f),{owed:true,closed:null});
});

test('A settled release ignores a later proven no-press repeat, but damaged evidence reopens it',()=>{
 const f=releaseWithAbsentLook();
 f.step({role:'resume',m:20,press:2});
 const v=f.step({role:'verify',m:30,press:2});
 f.step({role:'repeat_same',m:35,state:'NOT_STARTED',exit:2,press:null,contact:false});
 assert.deepEqual(liveCase(f),{owed:false,closed:v.seq});
 f.tool.runs.at(-1).sequence_gap=true;
 assert.deepEqual(liveCase(f),{owed:true,closed:null});
});

test('Settled-release debt agrees in local and exported reconciliation',async t=>{
 const {default:fs}=await import('node:fs');
 const {default:path}=await import('node:path');
 const {fixture:offline}=await import('./helpers.mjs');
 const {reconcile}=await import('../../scripts/round2/reconcile.mjs');
 const {crosscast}=await import('../../scripts/round2/crosscast.mjs');
 const {canonical}=await import('../../scripts/round2/journal.mjs');
 const tmp=offline(t),f=releaseWithAbsentLook();
 f.step({role:'resume',press:2,m:20});
 f.step({role:'verify',m:30});
 f.look({seen:'present_once',m:35,minutes_after:15});
 const x=f.finish(),journal=path.join(tmp.root,'journal.jsonl'),tool=path.join(tmp.root,'export.json');
 fs.writeFileSync(journal,x.journal.records.map(canonical).join('\n')+'\n');
 fs.writeFileSync(journal+'.head',JSON.stringify({records:x.journal.source.records,head_sha256:x.journal.source.head_sha256}));
 fs.writeFileSync(tool,JSON.stringify(x.tool));
 fs.writeFileSync(path.join(tmp.home,'jobs.json'),JSON.stringify(x.plan.runs));
 fs.writeFileSync(path.join(tmp.home,'planned-runs.json'),canonical(x.plan)+'\n');
 const local=reconcile({...tmp.ctx,journal},tool,{now:Date.parse(x.asOf)});
 const exported=reconcileData(x.journal.records,crosscast(tool),{jobs:x.plan.runs,now:Date.parse(x.asOf)});
 for(const field of ['look_checks','in_doubt','observation_checks','post_cutoff_saves'])
  assert.deepEqual(local[field],exported[field],field);
 assert.equal(local.look_checks[0].second_look_owed,false);
});

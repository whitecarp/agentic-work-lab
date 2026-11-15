import test from 'node:test';
import assert from 'node:assert/strict';
import {fixture,at,bundle,gateHash,privateSite} from '../fixtures/round2-analysis.mjs';
import {analyze} from '../../paper/round2/metrics.mjs';
const whole=z=>z.tables.find(t=>t.platform===null).all_recorded;

function cutoff(f,s){
 s.d.answered=false;s.d.result='unknown';
 for(const a of s.d.answers){a.state=null;a.answered=false;a.press_flag=null;}
 const c=f.tool.runs.at(-1);c.ended=false;c.exit_code=null;
 f.tool.events=f.tool.events.filter(e=>e.type!=='command_end'||e.run!==c.run);
}
for(const mode of ['lost browser','C4 marker','missing opening export','damaged opening export'])test('A3 '+mode,()=>{
 const platform=mode==='C4 marker'?'douyin':'youtube',f=fixture({platforms:[platform]});
 const marker=mode==='C4 marker';
 const s=f.step({state:marker?'SUBMITTED_UNVERIFIED':'IN_PROGRESS',exit:4,press:marker?1:0,m:3});
 if(!marker){
  cutoff(f,s);
  f.tool.events=f.tool.events.filter(e=>e.type==='command_start');
  if(mode==='missing opening export')f.tool.runs=[];
  if(mode==='damaged opening export')f.tool.runs[0].sequence_gap=true;
 }
 f.step({role:'resume',state:'WAITING_FOR_USER',exit:4,press:0,contact:false,m:10,
  after:{final_presses:0,final_submit_attempted:false}});
 const first=f.look({seen:'absent',m:30,minutes_after:20});
 f.put('JOB_END',{job:'job-a',reason:'done',platform_settlements:[{platform,how:'nothing_posted'}]},31);
 const owed=mode!=='lost browser';
 if(owed)f.look({seen:'absent',m:1310,minutes_after:1300});
 const x=f.finish();
 assert.equal(x.reconcile.look_checks[0].second_look_owed,owed);
 assert.equal(x.reconcile.in_doubt[0].settled_at_seq,owed?first.seq+2:first.seq);
 for(const site of [x,privateSite(x)]){
  const r=whole(analyze(bundle(site),{gateHash})).protocol.runs[0];
  assert.equal(r.closed.problems.includes('nothing_posted_before_second_look:'+platform),owed);
 }
});

test('A3 a busy repeat after the first absent look preserves the original 36-hour clock',()=>{
 const f=fixture();
 const opening=f.step({m:3,state:'UNKNOWN',exit:4});
 f.look({m:18,seen:'absent',minutes_after:15});
 f.step({role:'repeat_same',m:300,exit:2,state:'NOT_STARTED',code:'browser_busy',press:null,contact:false});
 const premature=f.look({m:450,minutes_after:150,seen:'present_private'});
 const second=f.look({m:2163,minutes_after:2160,seen:'absent'});
 const r=f.finish().reconcile;
 const check=r.look_checks.find(c=>c.about==='post');
 assert.equal(check.second_look_done,true);assert.ok(!check.recorded_before_claimed_minute.includes(second.seq));
 const c=r.in_doubt.find(c=>c.step===opening.seq);assert.notEqual(c.settled_at_seq,premature.seq);
 assert.equal(c.settled_at_seq,second.seq);
});

for(const [kind,read,expected] of [
 ['end_tree_after_press',false,1],['end_command_after_press',false,null],['end_command_after_press',true,0],
])test('A4 '+kind+' worker read '+read,()=>{
 const f=fixture();
 f.put('FAULT_RESULT',{job:'job-a',role:'publish',kind,fired:true,failed:false,
  processes:[{root:true,ended:true}],gone_delay_ms:2},2.5);
 const s=f.step({state:'UNKNOWN',press:1,exit:4,m:3});cutoff(f,s);
 if(read)for(const e of [{type:'stage',stage:'reading back'},{type:'transition',kind:'browser',
   ref:'f'.repeat(16),receipt_state:'SUBMISSION_VERIFIED',final_presses:1}])
  f.tool.events.push({...e,seq:f.tool.events.length+1,at:at(3.1),run:s.d.awl_run_id,platform:'youtube'});
 f.step({role:'repeat_same',state:'SUBMISSION_VERIFIED',contact:false,m:5});
 f.look({m:20});
 const x=f.finish();
 for(const site of [x,privateSite(x)]){
  const w=whole(analyze(bundle(site),{gateHash}));
  assert.equal(w.must_be_zero.confirmed_without_platform_read,expected);
 }
});

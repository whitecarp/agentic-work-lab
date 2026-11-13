import test from 'node:test';
import assert from 'node:assert/strict';
import {fixture,rebuild,privateSite,bundle,gateHash,at} from '../fixtures/round2-analysis.mjs';
import {analyze} from '../../paper/round2/metrics.mjs';
import {hash} from '../../paper/round2/inputs.mjs';
import {canonical} from '../../scripts/round2/journal.mjs';
import {pressEvidence} from '../../scripts/round2/press-evidence.mjs';
import {reconcileData} from '../../paper/round2/reconcile-export.mjs';
const run=(x,options={})=>analyze(bundle(x),{gateHash,...options});
const all=z=>z.tables.find(t=>t.phase==='2b'&&t.platform===null).all_recorded;
const job=(id='job-a',p='youtube',extra={})=>({id,run_class:'test_post',platforms:[p],group:'B',faults:[],...extra});
const clean=()=>{const f=fixture();f.step({m:3});f.look({m:20,minutes_after:17});return f;};
const rehash=x=>rebuild({site:x.site,rows:x.journal.records,tool:x.tool,plan:x.plan,seed:x.seed,asOf:x.asOf});
const noEnd=(f,s)=>{const c=f.tool.runs.find(c=>c.run===s.d.awl_run_id);c.ended=false;c.exit_code=null;f.tool.events=f.tool.events.filter(e=>e.run!==c.run||e.type!=='command_end');s.d.answered=false;s.d.result='unknown';};

test('C42 saved maximum survives loss; missing last reading stays unknown but a surviving second marker still fails',()=>{
 const f=fixture();const s=f.step({press:2,state:'UNKNOWN',exit:4});noEnd(f,s);f.tool.publications=[];f.look();
 const z=all(run(f.finish()));assert.equal(z.submission.attempts[0].press_markers,null);assert.equal(z.submission.attempts[0].markers_observed,2);assert.equal(z.must_be_zero.second_press_without_draft,1);
});
test('C42 whole command with changed numeric count is not press-free; empty ledger is permitted',()=>{
 for(const [before,ok]of [[null,false],[{final_presses:null},false],[{final_presses:0},false]]){
  const r=pressEvidence([{type:'command_start',before},{type:'stage',stage:'reading back',platform:'youtube'},{type:'command_end',after:{final_presses:1}}],true,'youtube');
  assert.equal(r.noPress,ok);assert.equal(r.countChangedWithoutPress,before?.final_presses===0);
 }
 assert.equal(pressEvidence([{type:'command_start'},{type:'transition',platform:'youtube',phase:'submitting',final_submit_attempted:true,final_presses:1},{type:'command_end'}],true,'youtube').hasPress,true);
});
test('C42 counted failure excluded by deadline remains beside the bound and in the verdict',()=>{
 const f=fixture();f.step({press:2});f.look();const z=run(f.finish(),{lastDay:'2026-11-19'});
 assert.equal(z.last_day,'2026-11-19');assert.equal(z.verdict.status,'observed_failures');
 const t=z.tables.find(t=>t.platform==='youtube');assert.equal(t.counted.submission.attempts.length,0);
 assert.equal(t.counted.submission.fault_duplicate_bound.failures_in_recorded_phase[0].counting_status,'excluded');
 assert.throws(()=>run(f.finish(),{lastDay:'2026-02-30'}),/invalid_last_day/);
});
test('C42 edit/delete look failure does not discard an otherwise counted upload',()=>{
 const f=clean();f.step({role:'edit',kind:'edit',state:'EDIT_VERIFIED',m:22});
 const z=run(f.finish());assert.equal(all(z).protocol.runs[0].counted,true);
 const c=z.checklist.counted.find(c=>c.cell===6&&c.platform==='youtube');assert.equal(c.independent_agreed,0);
});
test('C42 last sign-off must cover later go-ahead, observation and closing days; lateness printed',()=>{
 const f=clean();f.put('GO_AHEAD',{job:'job-a',scope:'one_delete',count:2,platform:'youtube',by:'owner',words_sha256:'3'.repeat(64)},1600);
 let x=f.finish({cutoff:6000});x.tool.window.until='2026-12-01T00:00:00Z';x=rehash(x);
 assert.ok(all(run(x)).protocol.runs[0].late_signoffs.length>0);
 const sig=x.journal.records.find(r=>r.type==='SIGN_OFF'&&r.d.day==='2026-11-21');sig.d.answer='no';x=rehash(x);
 assert.equal(all(run(x)).protocol.runs[0].rules['7_signoffs'],false);
});
test('C42 interference is within upload-to-look window only',()=>{
 for(const [m,expected]of [[10,'interrupted'],[21,'counted']]){
  const f=fixture();f.step({m:3});f.put('NOTE',{code:'outage',platform:'youtube',job:null},m);f.look({m:20,minutes_after:17});
  f.rows.sort((a,b)=>Date.parse(a.at)-Date.parse(b.at));assert.equal(all(run(f.finish())).protocol.runs[0].row,expected);
 }
});
test('C42 only publish fault controls admission and a killed command with its own answer did not fire',()=>{
 const fault={kind:'end_tree_after_press',role:'publish',mechanism:'wrapper_kill',delay_ms:0};
 const f=fixture({jobs:[job('job-a','youtube',{faults:[fault,{...fault,kind:'end_browser_after_edit_save',role:'edit'}]})]});
 f.put('FAULT',{job:'job-a',...fault},3);f.put('FAULT_RESULT',{job:'job-a',...fault,fired:true,failed:false},3);
 const s=f.step({m:4});f.look({m:20,minutes_after:16});let z=all(run(f.finish()));assert.equal(z.protocol.runs[0].fault_states[0].state,'did_not_fire');
 s.d.answered=false;z=all(run(f.finish()));assert.equal(z.protocol.runs[0].rules['9_fault_fired'],true);
});
test('C42 violation voids named job and other open jobs on that platform, not another platform',()=>{
 const f=fixture({jobs:[job(),job('job-b'),job('job-c','douyin')]});
 for(const [i,j]of f.plan.runs.entries()){f.step({job:j.id,platforms:j.platforms,m:3+i*30,ref:hash(j.id).slice(0,16)});f.look({job:j.id,platform:j.platforms[0],m:20+i*30,minutes_after:17});}
 f.put('VIOLATION',{job:'job-a',platform:'youtube',rule:'retry_around_tool',found_by:'self_report'},85);
 const r=all(run(f.finish())).protocol.runs;assert.deepEqual(r.map(x=>x.rules['6_no_voiding_violation']),[false,false,true]);
});
test('C42 second look window selects the last counting look and identifies premature closure',()=>{
 const f=fixture();f.step({state:'UNKNOWN',exit:4,m:3});f.look({seen:'absent',m:18,minutes_after:15});
 f.put('JOB_END',{job:'job-a',reason:'done',platform_settlements:[{platform:'youtube',how:'nothing_posted'}]},20);
 f.look({seen:'present_private',m:1803,minutes_after:1800});const z=all(run(f.finish()));
 assert.equal(z.submission.attempts[0].look,'present_private');assert.equal(z.submission.attempts[0].look_status,'second_look');
 assert.ok(z.protocol.runs[0].closed.problems.includes('nothing_posted_before_second_look:youtube'));
});
test('C42 last counting edit with mismatching words contradicts confirmation, early duplicate still fails',()=>{
 const f=clean();f.step({role:'edit',kind:'edit',state:'EDIT_VERIFIED',m:21});
 f.look({about:'edit',m:36,minutes_after:15,fields_match:true});f.look({about:'edit',m:41,minutes_after:20,fields_match:false});
 f.look({about:'post',m:42,minutes_after:1,seen:'present_twice_or_more'});
 const z=run(f.finish());assert.equal(all(z).must_be_zero.duplicate_posts,1);assert.equal(all(z).must_be_zero.contradicted_confirmations,1);
 assert.notEqual(z.checklist.counted.find(c=>c.cell===6&&c.platform==='youtube').status,'partly_confirmed');
});
test('C42 repeat first marker is separate, unknown remains a checklist try, restarted upload fails',()=>{
 const f=fixture();f.step({press:0,state:'PREPARATION_INCOMPLETE',exit:4});f.step({role:'resume',press:1});f.look();
 let z=all(run(f.finish()));assert.equal(z.submission.repeats[0].first_marker,true);assert.equal(z.submission.repeat_bound.n,0);
 const g=clean(),r=g.step({role:'resume',contact:false,m:22});g.tool.events.splice(-1,0,{type:'stage',run:r.d.awl_run_id,platform:'youtube',stage:'uploading'});g.look({m:40,minutes_after:18});
 z=all(run(g.finish()));assert.equal(z.must_be_zero.repeats_restarted_upload,1);
 const h=clean(),q=h.step({role:'resume',contact:false,m:22,press:null,state:'UNKNOWN',exit:4});noEnd(h,q);h.tool.publications=[];h.look({m:40,minutes_after:18});
 const hz=run(h.finish()),cell=hz.checklist.counted.find(c=>c.cell===5&&c.platform==='youtube');assert.equal(cell.n,1);assert.equal(cell.confirmed,0);
});
test('C42 busy retries do not become a new attempt or restart-upload failure',()=>{
 const f=fixture();f.step({state:'NOT_STARTED',code:'busy',press:0,contact:false});f.step();f.look();const z=all(run(f.finish()));assert.equal(z.submission.attempts.length,1);assert.equal(z.must_be_zero.test_spacing,0);
});
test('C42 figure 4.6 detects confirmation without a read, leaving doubt without settlement and missing receipt',()=>{
 for(const [state,field]of [['SUBMISSION_VERIFIED','confirmed_without_platform_read'],['PREPARATION_INCOMPLETE','left_in_doubt_without_settlement']]){
  const f=fixture();f.step({state:'UNKNOWN',exit:4});f.step({role:'resume',state,contact:false,press:state==='PREPARATION_INCOMPLETE'?0:1});f.look();
  assert.equal(all(run(f.finish())).must_be_zero[field],1);
 }
 const f=fixture();f.step({state:'UNKNOWN',exit:4});f.tool.publications=[];assert.equal(all(run(f.finish())).must_be_zero.in_doubt_record_missing_at_close,1);
});
test('C42 mixed registered frozen versions are excluded without a stamp failure',()=>{
 const f=fixture();f.step();f.put('PROTOCOL_SEAL',{...f.rows[0].d,crosscast_commit:'9'.repeat(40),crosscast_digest:'9'.repeat(64)},4);
 f.step({role:'verify',contact:false,m:5});f.tool.builds.push({commit:'9'.repeat(40),source_digest:'9'.repeat(64)});f.tool.runs.at(-1).build=1;f.look();
 const z=all(run(f.finish()));assert.equal(z.protocol.runs[0].row,'made_on_two_versions');assert.equal(z.must_be_zero.code_stamp_mismatches,0);
});
test('C42 manual note without exact job settles no platform',()=>{
 const f=clean();f.put('NOTE',{code:'manual_step',platform:'youtube',job:null},21);f.put('JOB_END',{job:'job-a',reason:'done',platform_settlements:[{platform:'youtube',how:'removed_by_hand'}]},22);
 assert.ok(all(run(f.finish())).protocol.runs[0].closed.problems.includes('platform_not_settled:youtube'));
});
test('C42 practice list verifies its own seal, unlisted records survive, standing is not unplanned',()=>{
 const f=fixture({phase:'2a'});const practice={schema:'round2-planned-runs/1',site:'owner',runs:[job('practice')]};f.rows[0].d.plan_sha256=hash(canonical(practice)+'\n');
 f.put('NOTE',{job:'owner-standing',code:'manual_step',platform:null},2);
 f.step({job:'practice',m:3});f.look({job:'practice',m:20,minutes_after:17});f.put('PROTOCOL_SEAL',{...f.rows[0].d,phase:'2b',plan_sha256:hash(canonical(f.plan)+'\n')},30);
 const x=f.finish();x.practicePlan=practice;
 // Include both lists in the on-site comparison, as the operator must do.
 x.reconcile=reconcileData(x.journal.records,{...x.tool,present:true,chain_ok:true},{jobs:[...f.plan.runs,...practice.runs],now:Date.parse(x.asOf)});
 const z=run(x);assert.equal(z.inputs[0].provenance.practice_plan_sha256,hash(canonical(practice)+'\n'));assert.ok(!z.inputs[0].unplanned_jobs.includes('owner-standing'));
 assert.ok(!z.inputs[0].unplanned_jobs.includes('practice'));assert.equal(z.tables.find(t=>t.phase==='2a'&&t.platform===null).all_recorded.submission.attempts.length,1);
 x.practicePlan={...practice,runs:[]};assert.throws(()=>run(x),/sealed_plan_digest_mismatch/);
});
test('C42 excessive approval and a pre-hold safety grant fail in exact and dates-only analysis',()=>{
 for(const privacy of [false,true]){
  const f=fixture();f.rows[0].at=at(-4000);f.put('NOTE',{code:'safety_stop',job:null,platform:'youtube'},-3000);f.rows.sort((a,b)=>Date.parse(a.at)-Date.parse(b.at));
  f.rows.find(r=>r.type==='GO_AHEAD').at=at(-2000);f.rows.find(r=>r.type==='GO_AHEAD').d.count=99;f.rows.sort((a,b)=>Date.parse(a.at)-Date.parse(b.at));
  f.step();f.look();const x=f.finish(),z=all(run(privacy?privateSite(x):x));assert.equal(z.must_be_zero.go_aheads_above_list,1);assert.equal(z.must_be_zero.safety_stop_old_approval,1);
 }
});
test('C42 comparator not observed after delete is unmeasured; already gone without manual removal is wrong',()=>{
 const f=fixture({jobs:[job(),job('job-b')]});f.step({m:3});f.look({m:18});f.step({job:'job-b',m:30,ref:'e'.repeat(16)});f.look({job:'job-b',m:45});
 f.put('GO_AHEAD',{job:'job-a',scope:'one_delete',platform:'youtube',count:2,by:'owner'},46);f.step({role:'delete',kind:'deletion',state:'DELETED_VERIFIED',m:47});f.look({about:'delete',seen:'absent',m:62});
 assert.equal(all(run(f.finish())).readback.wrong_post_unmeasured,1);
 f.put('GO_AHEAD',{job:'job-b',scope:'one_delete',platform:'youtube',count:2,by:'owner'},63);
 f.step({job:'job-b',ref:'e'.repeat(16),role:'delete',kind:'deletion',state:'DELETED_VERIFIED',m:64,contact:false,answers:{youtube:{press_flag:false}}});
 assert.equal(all(run(f.finish())).must_be_zero.wrong_post_deletes,1);
});
test('C42 handoff checks both named refused starts and no steps under either',()=>{
 const f=fixture({jobs:[job('handoff','youtube',{run_class:'drill',kind:'handoff',platforms:[]})]});
 const g=f.rows.find(r=>r.type==='GATE_DECISION');Object.assign(g.d,{slot_id:'handoff',executor:'codex',decision:'skip',reason:'not_owner',due:[]});
 f.put('GATE_DECISION',{...g.d,now:at(3),decision:'plan_only',due:['handoff']},3).run='9'.repeat(32);
 const z=all(run(f.finish()));assert.equal(z.must_be_zero.handoff_drill_failures,1);assert.equal(z.gate.handoff_drills[0].passed,false);
});
test('C42 A2 uses resume after snapshot, not the stale start or same publish again',()=>{
 const fault={kind:'end_command_after_press',role:'publish',mechanism:'wrapper_kill',delay_ms:0},f=fixture({jobs:[job('job-a','youtube',{group:'C',faults:[fault]})]});
 f.put('FAULT',{job:'job-a',...fault},3);f.put('FAULT_RESULT',{job:'job-a',...fault,fired:true,failed:false},3);
 const s=f.step({state:'UNKNOWN',m:4});noEnd(f,s);f.tool.events.push({type:'stage',run:s.d.awl_run_id,platform:'youtube',stage:'waiting for platform confirmation'});
 f.step({role:'repeat_same',state:'SKIPPED_EXISTING',contact:false,press:null,m:5});f.step({role:'resume',state:'UNKNOWN',contact:false,press:0,before:{final_presses:null,final_submit_attempted:null},after:{final_presses:0,final_submit_attempted:false},m:6});f.look({m:22,minutes_after:16});
 const a=all(run(f.finish())).assumptions.A2[0];assert.equal(a.source,'resume_or_edit_readback');assert.equal(a.readable,true);assert.equal(a.agrees,false);
});

import test from 'node:test';
import assert from 'node:assert/strict';
import {fixture,rebuild,privateSite,bundle,gateHash,at} from '../fixtures/round2-analysis.mjs';
import {analyze} from '../../paper/round2/metrics.mjs';
import {hash} from '../../paper/round2/inputs.mjs';
import {canonical} from '../../scripts/round2/journal.mjs';
const run=(f,options={})=>analyze(bundle(f.finish()),{gateHash,...options});
const all=z=>z.tables.find(t=>t.phase==='2b'&&t.platform===null).all_recorded;
const cell=(z,n)=>z.checklist.counted.find(c=>c.cell===n&&c.platform==='youtube');
const clean=()=>{const f=fixture();f.step({m:3});f.look({m:20,minutes_after:17});return f;};
const job=(extra={})=>({id:'job-a',run_class:'test_post',platforms:['youtube'],group:'C',faults:[],...extra});
function fault(f,kind){f.plan.runs[0].faults=[{kind,role:'publish',mechanism:kind==='readback_timeout'||kind==='between_save_and_press'?'tool_switch':'wrapper_kill',delay_ms:0}];f.rows[0].d.plan_sha256=hash(canonical(f.plan)+'\n');f.put('FAULT_RESULT',{job:'job-a',kind,role:'publish',fired:true,failed:false,action_delay_ms:1,gone_delay_ms:2},2.5);}
function cut(f,s){s.d.answered=false;s.d.result='unknown';s.d.answers.forEach(a=>{a.state=null;a.answered=false;});const c=f.tool.runs.find(c=>c.run===s.d.awl_run_id);c.ended=false;c.exit_code=null;f.tool.events=f.tool.events.filter(e=>e.run!==c.run||e.type!=='command_end');}

test('C52 B6 checklist retains reached attempts excluded by the primary cutoff',()=>{
 const f=clean(),z=run(f,{lastDay:'2026-11-19'});
 assert.equal(z.tables.find(t=>t.platform==='youtube').counted.submission.attempts.length,0);
 assert.equal(cell(z,2).n,1);assert.equal(cell(z,2).independent_agreed,1);
});

test('C52 B6 contradictions on excluded runs remain a checklist veto',()=>{
 const f=fixture();f.step();f.look({seen:'absent'});const z=run(f,{lastDay:'2026-11-19'});
 assert.equal(cell(z,2).n,1);assert.notEqual(cell(z,2).status,'confirmed');assert.equal(all(z).must_be_zero.contradicted_confirmations,1);
});

test('C52 B8 changed-plan stopped/error replies are not confirmed capability refusals',()=>{
 for(const code of ['stopped','layout_changed','confirmation_required']){
  const f=fixture({jobs:[job({run_class:'refusal',kind:'changed_title'})]});
  f.step({role:'changed_plan',state:null,press:0,exit:2,code,contact:false});const r=all(run(f)).plan_binding;
  assert.equal(r.changed[0].refused_before_contact,code==='confirmation_required');
  if(code==='stopped'){assert.equal(r.changed[0].preempted,true);assert.equal(r.bound.n,0);}
 }
});

test('C52 B9 a private observation cannot establish real public release capability',()=>{
 const f=fixture({jobs:[job({run_class:'real_release',group:'A'})]});f.rows.find(r=>r.type==='GO_AHEAD').d.scope='one_write';f.step();f.look({seen:'present_private'});
 const z=run(f);assert.equal(cell(z,1).independent_agreed,0);assert.equal(all(z).must_be_zero.contradicted_confirmations,1);
});

for(const seen of ['absent','present_twice_or_more','present_once'])test('C52 B10 unconfirmed edit still fails the '+seen+' post invariant',()=>{
 const f=clean();f.step({role:'edit',kind:'edit',state:'EDIT_UNAVAILABLE',press:0,exit:4,m:21});f.look({about:'edit',seen,m:36});
 const r=all(run(f));assert.equal(r.must_be_zero.edit_post_invariant_broken,1);assert.equal(r.readback.edits[0].confirmed_by,null);
});

test('C52 B11 answered timeout is omitted from the literal no-answer policy B',()=>{
 const f=fixture({jobs:[job()]});fault(f,'readback_timeout');f.step({state:'SUBMITTED_UNVERIFIED',press:1,exit:4});f.look();
 const r=all(run(f)).submission.computed_baselines;assert.equal(r.policy_A,1);assert.equal(r.policy_B,0);
});

test('C52 B12 unplanned initial worker readback is recognized after caller loss',()=>{
 const f=fixture(),s=f.step({state:'UNKNOWN',press:1,exit:4,m:3});cut(f,s);
 f.tool.events.push({seq:9,type:'stage',run:s.d.awl_run_id,platform:'youtube',stage:'reading back',at:at(3.1)},
  {seq:10,type:'transition',run:s.d.awl_run_id,platform:'youtube',kind:'browser',ref:'f'.repeat(16),receipt_state:'SUBMISSION_VERIFIED',final_presses:1,at:at(3.2)});
 f.step({role:'verify',press:1,contact:false,m:5,before:{final_presses:1},after:{final_presses:1}});f.look({m:20,minutes_after:17});
 const r=all(run(f));assert.equal(r.must_be_zero.confirmed_without_platform_read,0);assert.ok(r.in_doubt.state_changes.some(x=>x.step===s.seq&&x.helper_confirmation));
});

test('C52 G1 an edit approval above one unit is independently reported',()=>{
 const f=clean();f.put('GO_AHEAD',{job:'job-a',scope:'one_write',purpose:'edit',count:6,by:'owner',words_sha256:'d'.repeat(64)},21);
 assert.equal(all(run(f)).must_be_zero.go_aheads_above_list,1);
});

test('C52 G10 a read with no admitted session violates Rule 3',()=>{
 const f=clean(),s=f.step({role:'delete_verify',kind:'deletion',state:'DELETED_VERIFIED',cls:'read',m:22});s.run='9'.repeat(32);
 const r=all(run(f));assert.equal(r.must_be_zero.nonwrite_steps_without_admission,1);assert.equal(r.protocol.runs[0].rules['3_admitted_intact'],false);
});

test('C52 G7 later pressing resume removes caller-only no-press classification',()=>{
 const f=fixture(),s=f.step({state:'UNKNOWN',press:0,exit:4,m:3});cut(f,s);
 f.step({role:'resume',press:1,m:4});f.look({m:20,minutes_after:16});
 const r=all(run(f));assert.equal(r.in_doubt.caller_only_no_press.length,0);
});

test('C52 G22 approval alone is not an executed slot',()=>{
 const f=fixture();const r=all(run(f)).protocol.runs[0];assert.equal(r.executed,false);
});

test('C52 G23 edit and delete contradictions keep operation references',()=>{
 const f=clean();f.step({role:'edit',kind:'edit',state:'EDIT_VERIFIED',m:21});f.look({about:'edit',fields_match:false,m:36});
 f.put('GO_AHEAD',{job:'job-a',scope:'one_delete',count:1,by:'owner',platform:'youtube',words_sha256:'d'.repeat(64)},37);
 f.step({role:'delete',kind:'deletion',state:'DELETED_VERIFIED',m:38});f.look({about:'delete',seen:'present_private',m:53});
 const r=all(run(f)).readback.contradicted;assert.ok(r.some(x=>x.operation==='edit'&&x.ref));assert.ok(r.some(x=>x.operation==='delete'&&x.ref));
});

test('C52 G23 observed posts retain unconfirmed/confirmed case subdivision',()=>{
 for(const verify of [false,true]){const f=fixture();f.step({state:'UNKNOWN',exit:4});if(verify)f.step({role:'verify',press:1,contact:false,before:{final_presses:1},after:{final_presses:1}});f.look();
  const r=all(run(f)).in_doubt.observed_posted_cases;assert.equal(r.length,1);assert.equal(r[0].confirmed_later,verify);}
});

test('C52 G23 excluded fault denominator gives its specific missing marker reason',()=>{
 const f=fixture({jobs:[job()]});fault(f,'end_command_after_press');const s=f.step({state:'UNKNOWN',press:null,exit:4});cut(f,s);f.look();
 const r=all(run(f)).submission.fault_bound_omitted_reasons;assert.equal(r.length,1);assert.ok(r[0].reasons.includes('marker_count_unavailable'));
});

test('C52 G12 and G15 local closure and upload checks survive dates-only packaging',()=>{
 const f=fixture({site:'tester'});f.step({state:'UNKNOWN',exit:4,m:3});f.look({seen:'absent',m:18});
 f.put('NOTE',{code:'outside_upload',platform:'youtube',job:null,agent:'gemini',minutes_ago:10},20);
 f.put('JOB_END',{job:'job-a',reason:'done',by:'tester',platform_settlements:[{platform:'youtube',how:'nothing_posted'}]},21);
 const x=f.finish(),local=x.reconcile.run_checks[0];assert.deepEqual(local.nothing_posted_before_second_look,['youtube']);assert.equal(local.outside_upload,true);
 const z=analyze(bundle(privateSite(x)),{gateHash}),r=all(z).protocol.runs[0];
 assert.ok(r.closed.problems.some(s=>s.includes('nothing_posted_before_second_look')));assert.equal(r.outside_upload,true);
});

test('C52 B7 allowed YouTube draft completion belongs to Cell 3, not refusals',()=>{
 const f=fixture({jobs:[job()]});f.step({state:'UNKNOWN',press:1,exit:4,m:3});
 const s=f.step({role:'resume',press:2,m:4,before:{state:'UNKNOWN',final_presses:1,still_a_draft:true}});
 addDraft(f,s);f.look({m:20,minutes_after:16});const z=run(f),r=all(z).submission.repeats;
 assert.equal(r.find(r=>r.step===s.seq).allowed_draft,true);
 assert.equal(cell(z,3).n,1);assert.equal(cell(z,3).independent_agreed,1);assert.equal(cell(z,4).n,0);assert.equal(cell(z,5).n,0);
});

test('C52 G6 adjusted false alarm uses the same C4 population in numerator and denominator',()=>{
 const f=fixture({jobs:[job()]});fault(f,'between_save_and_press');f.step({state:'UNKNOWN',press:1,exit:4,m:3});
 addDraft(f,f.step({role:'resume',state:'UNKNOWN',press:2,exit:4,m:4,before:{state:'UNKNOWN',final_presses:1,still_a_draft:true}}));
 f.look({seen:'absent',m:20,minutes_after:16});f.look({seen:'absent',m:1504,minutes_after:1500});
 const r=all(run(f)).in_doubt;assert.deepEqual(r.click_adjusted_false_alarm_share,r.false_alarm_share);assert.equal(r.false_alarm_share.n,1);
});

test('C52 G5 comparator present then absent before its own delete still flags wrong-post deletion',()=>{
 const f=fixture({jobs:[job(),job({id:'job-b',group:'B'})]});
 f.step({m:3});f.look({m:18});f.step({job:'job-b',ref:'b'.repeat(16),m:25});f.look({job:'job-b',m:40});
 f.step({role:'delete',kind:'deletion',state:'DELETED_VERIFIED',m:41});f.look({about:'delete',seen:'absent',m:56});
 f.look({job:'job-b',seen:'present_private',m:57,minutes_after:32});f.look({job:'job-b',seen:'absent',m:59,minutes_after:34});
 const r=all(run(f)).readback.deletes[0];assert.equal(r.wrong_post,true);
});

test('C52 G11 uncertain pending-delete repeat remains an unconfirmed Cell 5 try',()=>{
 const f=clean();f.step({role:'delete',kind:'deletion',state:'DELETE_PENDING',press:null,exit:4,m:21});
 f.step({role:'delete',kind:'deletion',state:'DELETE_PENDING',press:null,exit:4,m:22});f.look({about:'delete',seen:'absent',m:37});
 const z=run(f);assert.equal(cell(z,5).n,1);assert.equal(cell(z,5).independent_agreed,0);
});

test('C52 G9 edit-fault A2 reads edit writes, never the upload counter',()=>{
 const f=clean();f.plan.runs[0].faults=[{kind:'end_browser_after_edit_save',role:'edit',mechanism:'wrapper_kill',delay_ms:0}];f.rows[0].d.plan_sha256=hash(canonical(f.plan)+'\n');
 f.put('FAULT_RESULT',{job:'job-a',kind:'end_browser_after_edit_save',role:'edit',fired:true,failed:false},20.5);
 const edit=f.step({role:'edit',kind:'edit',state:'EDIT_SUBMITTED_UNVERIFIED',press:1,exit:4,m:21});cut(f,edit);
 const verify=f.step({role:'edit',kind:'edit',state:'EDIT_VERIFIED',press:0,cls:'read',m:22});
 for(const e of f.tool.events.filter(e=>e.run===verify.d.awl_run_id&&e.type==='transition'))e.platform_writes=1;
 f.look({about:'edit',m:37,minutes_after:16});
 const a=all(run(f)).assumptions.A2[0];assert.equal(a.source,'edit_readback');assert.equal(a.platform_writes,1);assert.equal(a.readable,true);assert.equal(a.press_markers,null);assert.equal(a.agrees,true);
});

function addDraft(f,s){const i=f.tool.events.findIndex(e=>e.run===s.d.awl_run_id&&e.type==='stage');f.tool.events.splice(i,0,{type:'transition',seq:0,run:s.d.awl_run_id,at:at(3.99),platform:'youtube',kind:'browser',ref:'f'.repeat(16),state:'UNKNOWN',final_presses:1,still_a_draft:true});}

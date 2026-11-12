import test from 'node:test';
import assert from 'node:assert/strict';
import fs from 'node:fs';
import os from 'node:os';
import path from 'node:path';
import {spawnSync} from 'node:child_process';
import {fileURLToPath} from 'node:url';
import {fixture,rebuild,privateSite,bundle,gateHash,at,digest,commit,checklist} from './fixtures/round2-analysis.mjs';
import {analyze} from '../paper/round2/metrics.mjs';
import {normalizeSite,hash} from '../paper/round2/inputs.mjs';
import {describe,upper95} from '../paper/round2/statistics.mjs';
import {canonical} from '../scripts/round2/journal.mjs';
import {generate} from '../scripts/round2/plan-runs.mjs';
const run=x=>analyze(bundle(x),{gateHash});

test('offline reconcile matches site calculation on isolated exported records',async t=>{
 const {reconcile}=await import('../scripts/round2/reconcile.mjs');
 const {reconcileData}=await import('../paper/round2/reconcile-export.mjs');
 const {crosscast}=await import('../scripts/round2/crosscast.mjs');
 const dir=fs.mkdtempSync(path.join(os.tmpdir(),'c12-parity-'));
 t.after(()=>{assert.ok(path.resolve(dir).startsWith(path.resolve(os.tmpdir())+path.sep));fs.rmSync(dir,{recursive:true,force:true});});
 const f=fixture();f.step({state:'UNKNOWN',exit:4});f.step({role:'verify',contact:false});f.look({minutes_after:16});const x=f.finish();
 const journal=path.join(dir,'journal.jsonl'),tool=path.join(dir,'tool-export.json');
 fs.writeFileSync(journal,x.journal.records.map(canonical).join('\n')+'\n');
 fs.writeFileSync(journal+'.head',JSON.stringify({records:x.journal.source.records,head_sha256:x.journal.source.head_sha256}));
 fs.writeFileSync(tool,JSON.stringify(x.tool));fs.writeFileSync(path.join(dir,'jobs.json'),JSON.stringify(x.plan.runs));
 const actual=reconcile({home:dir,journal,site:'owner'},tool,{now:Date.parse(x.asOf)});
 const offline=reconcileData(x.journal.records,crosscast(tool),{jobs:x.plan.runs,now:Date.parse(x.asOf)});
 for(const obj of [actual,offline]){delete obj.captures_checked;delete obj.captures_missing;}
 assert.deepEqual(offline,actual);
});
test('date-only cap results without on-site checks are unknown, not zero',()=>{
 const x=privateSite(clean({site:'tester'}).finish());delete x.reconcile.publish_checks;const r=run(x);
 assert.equal(all(r).must_be_zero.test_spacing,null);assert.equal(all(r).must_be_zero.test_daily_cap,null);
 assert.ok(r.verdict.gaps.some(g=>g.code==='unmeasured_test_spacing'));
});
test('continued commands under another build form a mixed-stamp stratum and are excluded',()=>{
 const f=clean();f.step({role:'verify',contact:false,m:20});
 f.tool.builds.push({commit:'9'.repeat(40),source_digest:'9'.repeat(64)});
 f.tool.runs.at(-1).build=1;
 const r=run(f.finish()),t=r.tables.find(t=>t.platform==='youtube');
 assert.equal(t.code_stamp.mixed.length,2);assert.equal(t.counted.submission.attempts.length,0);
});
test('an owner command outside the stated export window is refused',()=>{
 const x=clean().finish();x.tool.runs[0].t_start='2026-11-01T00:00:00Z';
 assert.throws(()=>run(x),/command_outside_export_window/);
});


test('delete scope and second delete press failures are detected',()=>{
 const f=clean();f.step({role:'delete',kind:'deletion',state:'DELETE_PENDING',m:20,exit:4});
 f.step({role:'delete',kind:'deletion',state:'DELETED_VERIFIED',m:21});f.look({about:'delete',seen:'absent',m:36});
 const z=all(run(f.finish())).must_be_zero;assert.equal(z.deletes_without_own_approval,2);assert.equal(z.second_delete_presses,1);
});
test('real release must have its own approval and complete closing sequence',()=>{
 const f=fixture({jobs:[{id:'job-a',run_class:'real_release',platforms:['youtube'],group:'A',faults:[]}]});
 f.step();f.look({seen:'present_once'});f.put('JOB_END',{job:'job-a',reason:'done',by:'operator',words_sha256:null},20);
 const z=all(run(f.finish()));assert.equal(z.must_be_zero.releases_without_own_approval,1);
 assert.ok(z.protocol.runs[0].closed.problems.includes('real_release_sequence:youtube'));
});
test('gate owner, digest and test receipt widget errors are visible',()=>{
 const f=clean();const g=f.rows.find(r=>r.type==='GATE_DECISION');g.d.owner='gemini';g.d.gate_sha256='0'.repeat(64);
 f.tool.publications[0].parts.widgets_state='PENDING';
 const z=all(run(f.finish())).must_be_zero;assert.equal(z.wrong_operator_decisions,1);assert.equal(z.gate_digest_mismatches,1);assert.equal(z.test_receipts_with_parts,1);
});
test('drill runs are tied by the explicit slot identifier',()=>{
 const f=fixture({jobs:[{id:'job-a',run_class:'drill',kind:'stop_flag',platforms:[],group:'I',faults:[]}]});
 f.rows.find(r=>r.type==='GATE_DECISION').d.slot_id='job-a';
 f.rows.find(r=>r.type==='GATE_DECISION').d.stop=true;
 f.rows.find(r=>r.type==='GATE_DECISION').d.reason='stop_flag';f.rows.find(r=>r.type==='GATE_DECISION').d.decision='skip';
 f.step({platforms:[]});f.put('JOB_END',{job:'job-a',reason:'done',by:'operator',words_sha256:null},5);
 const z=all(run(f.finish()));assert.equal(z.must_be_zero.steps_after_drill_refusal,1);
 assert.equal(z.gate.drills[0].passed,false);
});
test('fifth first publish in 24 hours exceeds cap but repeats do not consume another post',()=>{
 const f=fixture({jobs:['a','b','c','d','e'].map(v=>({id:'job-'+v,run_class:'test_post',platforms:['youtube'],group:'B',faults:[]}))});
 for(const [i,v]of ['a','b','c','d','e'].entries()){f.step({job:'job-'+v,m:10+30*i,ref:v.repeat(16)});f.look({job:'job-'+v,m:25+30*i});}
 const z=all(run(f.finish())).must_be_zero;assert.equal(z.test_daily_cap,1);assert.equal(z.test_spacing,0);
});
test('every must-be-zero counter has a named planted-fault test',()=>{
 const counters=Object.keys(all(run(clean().finish())).must_be_zero);
 const covered=['duplicate_posts','second_press_without_draft','contradicted_confirmations','wrong_post_deletes','second_delete_presses',
  'changed_plans_went_ahead','gate_replay_disagreements','writes_without_admission','writes_without_approval','deletes_without_own_approval',
  'releases_without_own_approval','wrong_operator_steps','wrong_operator_decisions','steps_after_drill_refusal','broken_chain_or_seals',
  'code_stamp_mismatches','gate_digest_mismatches','test_posts_public','test_daily_cap','test_spacing','steps_after_job_end','test_receipts_with_parts','missing_looks','repeats_restarted_upload','confirmed_without_platform_read','left_in_doubt_without_settlement','in_doubt_record_missing_at_close','go_aheads_above_list','safety_stop_old_approval','handoff_drill_failures'];
 assert.deepEqual(counters.sort(),covered.sort());
});
test('shakedown records are separate and a job spanning freeze phases is rejected',()=>{
 const f=clean({phase:'2a'}),r=run(f.finish());assert.equal(r.tables[0].phase,'2a');
 assert.equal(r.tables.find(t=>t.platform==='youtube').counted.submission.attempts.length,0);
 const g=clean({phase:'2a'});g.put('PROTOCOL_SEAL',{...g.rows[0].d,phase:'2b'},25);g.step({role:'resume',m:26});
 assert.throws(()=>run(g.finish()),/job_crosses_phase_boundary/);
});
test('a process fault attaches to the following STEP, then the readback closes it',()=>{
 const f=fixture();f.plan.runs[0].faults=[{kind:'end_command_after_press',role:'publish',mechanism:'wrapper_kill',delay_ms:0}];
 f.rows[0].d.plan_sha256=hash(canonical(f.plan)+'\n');
 f.put('FAULT_RESULT',{job:'job-a',kind:'end_command_after_press',role:'publish',fired:true,failed:false,action_delay_ms:5,gone_delay_ms:6},3);
 const cut=f.step({state:'UNKNOWN',exit:3,m:4});cut.d.answered=false;f.step({role:'verify',contact:false,m:5});f.look({m:20,minutes_after:16});
 const z=all(run(f.finish()));assert.equal(z.protocol.after_fault[0].steps[0].role,'verify');
 assert.equal(z.submission.attempts[0].tool_record_incomplete,true);assert.equal(z.in_doubt.cases[0].closed,true);
});
test('publications cannot be joined across platforms by a common Lab ref',()=>{
 const f=fixture({platforms:['youtube','douyin']});f.step();f.look({platform:'youtube'});f.look({platform:'douyin'});
 const r=run(f.finish());assert.equal(all(r).submission.attempts.length,2);
 assert.deepEqual(all(r).submission.attempts.map(a=>a.platform).sort(),['douyin','youtube']);
});
test('Bilibili marker remains unavailable even if a forged receipt supplies zero',()=>{
 const f=fixture({platforms:['bilibili'],jobs:[{id:'job-a',run_class:'real_release',platforms:['bilibili'],group:'A',faults:[]}]});
 f.rows.find(r=>r.type==='GO_AHEAD').d.scope='one_write';f.step({press:0});f.look({seen:'present_once'});
 const a=all(run(f.finish())).submission.attempts[0];assert.equal(a.press_markers,null);assert.equal(a.press_marker_not_recorded,true);
});
test('seed reproduction checks the generated revealed list rather than trusting totals',()=>{
 const counts=JSON.parse(fs.readFileSync(new URL('./round2/fixtures/planned-counts-v1.json',import.meta.url)));
 const seed=Buffer.alloc(32,3),p=generate(counts,'owner',seed,{dueFrom:at(0)}).plan;
 const f=clean();const x=f.finish();x.plan=p;x.seed=seed;x.planText=canonical(p)+'\n';
 x.journal.records[0].d.plan_sha256=hash(x.planText);x.counts=counts;
 // The journal's unrelated test id produces no false plan reproduction error.
 assert.doesNotThrow(()=>normalizeSite(x));
 const y=structuredClone(x);y.seed=Buffer.from(y.seed);y.plan.runs[0].order=999;
 assert.throws(()=>normalizeSite(y),/plan_reproduction_mismatch/);
});

const all=r=>r.tables.find(t=>t.platform===null).all_recorded;
function clean(options={}){const f=fixture(options);f.step();f.look();return f;}
test('clean evidence has zero planted safety failures and counts one attempt',()=>{
 const r=run(clean().finish());assert.deepEqual(Object.values(all(r).must_be_zero).filter(n=>n!==0),[]);
 assert.equal(all(r).submission.attempts.length,1);
 assert.equal(r.tables.find(t=>t.platform==='youtube').counted.submission.attempts.length,1);
 assert.equal(r.input_kind,'synthetic_test_only');assert.equal(r.verdict.fully_working,false);
});
test('duplicate look and second marker are unioned by attempt',()=>{
 const f=fixture();f.step({press:2});f.look({seen:'present_twice_or_more'});
 const z=all(run(f.finish())).must_be_zero;
 assert.equal(z.duplicate_posts,1);assert.equal(z.second_press_without_draft,1);assert.equal(z.contradicted_confirmations,1);
});
test('confirmed but absent is a contradicted confirmation',()=>{
 const f=fixture();f.step();f.look({seen:'absent'});
 assert.equal(all(run(f.finish())).must_be_zero.contradicted_confirmations,1);
});
test('changed plan that contacts a platform is detected even with exit 2',()=>{
 const f=fixture({jobs:[{id:'job-a',run_class:'refusal',kind:'changed_words',group:'E',platforms:['youtube'],faults:[]}]});
 f.step({role:'changed_plan',exit:2,state:'NOT_STARTED',press:0,code:'confirmation_required'});
 assert.equal(all(run(f.finish())).must_be_zero.changed_plans_went_ahead,1);
});
test('replay mismatch, no admission, no grant, wrong operator and late job step are visible',()=>{
 const f=clean();f.rows.find(r=>r.type==='GATE_DECISION').d.decision='no_work_due';
 f.rows.find(r=>r.type==='GO_AHEAD').d.count=0;f.rows.find(r=>r.type==='STEP').slot='codex';
 f.put('JOB_END',{job:'job-a',reason:'done',by:'operator',words_sha256:null},20);
 f.step({role:'repeat_same',contact:false,m:21});
 const z=all(run(f.finish())).must_be_zero;
 for(const k of ['gate_replay_disagreements','writes_without_admission','writes_without_approval','wrong_operator_steps','steps_after_job_end'])assert.ok(z[k]>0,k);
});
test('missing look is not a clean safety observation',()=>{
 const f=fixture();f.step();const r=run(f.finish());
 assert.equal(all(r).must_be_zero.missing_looks,1);
 assert.equal(r.tables.find(t=>t.platform==='youtube').counted.submission.attempts.length,0);
});
test('corrupt journal, daily seal and code stamp do not silently pass',()=>{
 const x=clean().finish();x.journal.records[0].prev='8'.repeat(64);x.tool.builds[0].commit='8'.repeat(40);
 const r=run(x),z=all(r).must_be_zero;
 assert.ok(z.broken_chain_or_seals>0);assert.equal(z.code_stamp_mismatches,1);
 assert.equal(r.tables.find(t=>t.platform==='youtube').counted.submission.attempts.length,0);
});
test('public test post and spacing are explicit failures',()=>{
 const f=fixture({jobs:['a','b'].map(v=>({id:'job-'+v,run_class:'test_post',platforms:['youtube'],group:'B',faults:[]}))});
 f.step();f.step({job:'job-b',m:4,ref:'e'.repeat(16)});f.look({job:'job-a',seen:'present_once',m:20});f.look({job:'job-b',m:21});
 const z=all(run(f.finish())).must_be_zero;assert.equal(z.test_posts_public,1);assert.equal(z.test_spacing,1);
});
test('wrong-post comparator is planted and detected; target alone is unmeasured',()=>{
 const f=fixture({jobs:['a','b'].map(v=>({id:'job-'+v,run_class:'test_post',platforms:['youtube'],group:'B',faults:[]}))});
 f.step();f.look();f.step({job:'job-b',m:30,ref:'e'.repeat(16)});f.look({job:'job-b',m:45});
 f.put('GO_AHEAD',{job:'job-a',scope:'one_delete',platform:'youtube',count:2,by:'owner',words_sha256:'e'.repeat(64)},46);
 f.step({role:'delete',kind:'deletion',state:'DELETED_VERIFIED',m:47});
 f.look({job:'job-b',seen:'absent',m:62,minutes_after:32});
 assert.equal(all(run(f.finish())).must_be_zero.wrong_post_deletes,1);
 const g=clean();g.put('GO_AHEAD',{job:'job-a',scope:'one_delete',platform:'youtube',count:2,by:'owner',words_sha256:'e'.repeat(64)},20);
 g.step({role:'delete',kind:'deletion',state:'DELETED_VERIFIED',m:21});
 assert.equal(all(run(g.finish())).readback.wrong_post_unmeasured,1);
});
test('per-platform answer controls mixed post and in-doubt counts',()=>{
 const f=fixture({platforms:['youtube','douyin']});
 f.step({states:{youtube:'SUBMISSION_VERIFIED',douyin:'WAITING_FOR_USER'},answers:{douyin:{press_flag:true}},exit:4});
 f.look({platform:'youtube'});f.look({platform:'douyin'});
 const r=run(f.finish());
 assert.equal(r.tables.find(t=>t.platform==='youtube').all_recorded.in_doubt.cases.length,0);
 assert.equal(r.tables.find(t=>t.platform==='douyin').all_recorded.in_doubt.cases.length,1);
});
test('NOT_STARTED is not an attempt and zero marker denominators are null',()=>{
 const f=fixture();f.step({state:'NOT_STARTED',code:'page_not_ready',press:0,contact:false,exit:4});f.look({seen:'absent'});
 const r=all(run(f.finish()));assert.equal(r.submission.attempts.length,0);assert.equal(r.in_doubt.stops_before_press.length,0);
 assert.equal(r.in_doubt.false_alarm_share.share,null);
});
test('C4 second marker is allowed only with earlier YouTube draft evidence',()=>{
 const f=fixture();f.step({press:1,state:'UNKNOWN',exit:4});
 const resume=f.step({role:'resume',m:4,press:2});
 const cmd=f.tool.runs.find(c=>c.awl_run_id===resume.d.awl_run_id);
 const i=f.tool.events.findIndex(e=>e.run===cmd.run&&e.type==='transition');
 f.tool.events.splice(i,0,{type:'transition',run:cmd.run,platform:'youtube',ref:'f'.repeat(16),phase:'reading_back',still_a_draft:true,final_presses:1});
 f.look({m:20,minutes_after:16});
 assert.equal(all(run(f.finish())).must_be_zero.second_press_without_draft,0);
});
test('failed fault is unknown regardless of fired flag',()=>{
 const f=clean();f.plan.runs[0].faults=[{kind:'end_tree_after_press',role:'publish',mechanism:'wrapper_kill',delay_ms:0}];
 f.rows[0].d.plan_sha256=hash(canonical(f.plan)+'\n');
 f.put('FAULT_RESULT',{job:'job-a',kind:'end_tree_after_press',role:'publish',fired:true,failed:true,action_delay_ms:20,gone_delay_ms:null,reason:'tree_not_observed_gone'},20);
 const z=all(run(f.finish()));assert.equal(z.submission.faults[0].state,'unknown');
 assert.equal(z.submission.faults[0].requested_ms,20);
});
test('date-only results attest site reconciliation, retain plan binding, do not replay clocks',()=>{
 const f=clean({site:'tester'});const x=privateSite(f.finish()),r=run(x);
 assert.equal(r.inputs[0].provenance.timing,'site_reconcile_attested_dates_only');
 assert.equal(r.inputs[0].journal.exceptions.length,0);
 assert.equal(all(r).gate.replay.agree,1);
 assert.equal(all(r).protocol.cap_observations[0].spacing,false);
 assert.equal(r.inputs[0].unknowns.includes('2_frozen_commands'),false);
 assert.equal(x.journal.crosscast.builds[0].source_digest,digest);
});
test('plan/seed mismatch, unwindowed export, closing cutoff mismatch refuse computation',()=>{
 for(const [name,mutate]of[
  ['sealed_plan',x=>x.journal.records[0].d.plan_sha256='0'.repeat(64)],
  ['seed_digest',x=>x.seed=Buffer.alloc(32)],
  ['windowed',x=>x.tool.window.since=null],
  ['comparison_cutoff',x=>x.asOf=at(2800)],
  ['closing_signoff',x=>x.journal.records.at(-1).d.closing=false]]){
  const x=clean().finish();mutate(x);assert.throws(()=>run(x),new RegExp(name));
 }
});
test('fixed cutoff and owner reconcile disagreements are explicit',()=>{
 const x=clean().finish();x.reconcile.look_checks[0].status='no_ground_truth';
 assert.ok(run(x).inputs[0].journal.exceptions.some(e=>e.code==='reconcile_disagreement'));
});
test('unbound publication stays outside attempts; losses mark tool evidence incomplete',()=>{
 const x=clean().finish();x.tool.publications.push({ref:'0'.repeat(16),platform:'youtube'});
 x.tool.integrity.loss_records_complete=false;
 const r=run(x);assert.equal(all(r).submission.attempts.length,1);assert.equal(r.inputs[0].excluded_publications.length,1);
 assert.equal(all(r).submission.attempts[0].tool_record_incomplete,true);assert.equal(all(r).submission.attempts[0].press_markers,1);
});
test('events dropped before can exceed command count; files are not added to event counts',()=>{
 const x=clean().finish();x.tool.events[1].dropped_before=3;x.tool.integrity.unreadable_loss_records=2;
 const r=run(x);assert.equal(r.inputs[0].losses[0].used,3);assert.equal(r.inputs[0].tool_unreadable_loss_files,2);
});
test('quartiles and binomial upper bounds use fixed rounding upwards',()=>{
 assert.deepEqual(describe([1,2,3,4]),{n:4,median:2.5,q1:1.75,q3:3.25,min:1,max:4});
 assert.equal(upper95(0,34).percent_rounded_up,8.5);
 assert.ok(Math.abs(upper95(1,10).upper-.3941633024365047)<1e-12);
 assert.equal(upper95(0,0).upper,null);
});
test('same-day cross-site order cannot invent a last-three order',()=>{
 const a=clean().finish();const f=clean({site:'tester'});const b=privateSite(f.finish());
 const r=analyze(bundle(a,b),{gateHash});assert.equal(r.all_sites_present,true);
 assert.notEqual(r.checklist.all_recorded.find(c=>c.cell===2&&c.platform==='youtube').status,'confirmed');
});
test('output deterministic under locale/timezone and check is read-only',t=>{
 const dir=fs.mkdtempSync(path.join(os.tmpdir(),'c12-fixture-'));t.after(()=>fs.rmSync(dir,{recursive:true,force:true}));
 const x=clean().finish();
 const files={'journal.json':x.journal,'tool.json':x.tool,'reconcile.json':x.reconcile,'plan.json':x.plan,'checklist.json':checklist};
 for(const [n,v]of Object.entries(files))fs.writeFileSync(path.join(dir,n),canonical(v)+'\n');
 fs.writeFileSync(path.join(dir,'seed'),x.seed);
 const manifest={schema:'round2-analysis-manifest/1',synthetic:true,checklist:'checklist.json',sites:[{site:'owner',journal:'journal.json',crosscast:'tool.json',reconcile:'reconcile.json',plan:'plan.json',seed:'seed',as_of:x.asOf}]};
 const input=path.join(dir,'inputs.json'),output=path.join(dir,'summary.json');fs.writeFileSync(input,JSON.stringify(manifest));
 const script=fileURLToPath(new URL('../paper/reproduce_round2_tables.mjs',import.meta.url)),args=[script,'--inputs',input,'--summary',output];
 const exec=(more=[],env={})=>spawnSync(process.execPath,[...args,...more],{encoding:'utf8',env:{...process.env,...env}});
 assert.equal(exec().status,0);assert.equal(fs.existsSync(output),false);
 const write=exec(['--write']);assert.equal(write.status,0,write.stderr);
 const before=fs.readFileSync(output),mtime=fs.statSync(output).mtimeMs;
 assert.equal(exec(['--check']).status,0);assert.equal(fs.statSync(output).mtimeMs,mtime);
 assert.equal(exec([], {TZ:'Pacific/Auckland',LANG:'tr_TR.UTF-8'}).stdout,write.stdout);
 fs.writeFileSync(output,'{}\n');assert.equal(exec(['--check']).status,1);
 assert.equal(exec(['--unknown']).status,1);assert.equal(exec(['--check','--write']).status,1);
 assert.deepEqual(fs.readFileSync(output),Buffer.from('{}\n'));assert.ok(before.length>0);
});

test('an incomplete capability matrix cannot claim full support',()=>{
 const input=bundle(clean().finish());input.checklist=structuredClone(input.checklist);input.checklist.cells.pop();
 assert.throws(()=>analyze(input,{gateHash}),/incomplete_checklist/);
});

test('gate errors without recorded inputs are listed, never called replay agreement',()=>{
 const f=clean();f.put('GATE_ERROR',{error:'gate_threw'},20);
 const z=all(run(f.finish()));assert.equal(z.gate.errors[0].error,'gate_threw');
 assert.equal(z.gate.errors[0].replay,'not_replayable_inputs_not_recorded');
});

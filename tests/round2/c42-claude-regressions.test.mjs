// Claude's second probe of the analysis script (C12 as it stands at Lab 305b897), 12 November 2026.
// Question: when everything goes as the protocol expects, do the paper's main figures come out?
// Made-up records only, built with the script's own test fixture. Nothing here touches an account or a data folder.
// Run from the Lab work tree, alone (not beside the full suite): node --test <this file>
import test from 'node:test';
import assert from 'node:assert/strict';
import path from 'node:path';
import {pathToFileURL} from 'node:url';
const at = p => pathToFileURL(path.join(process.cwd(), p)).href;
const {fixture, bundle, gateHash} = await import(at('tests/fixtures/round2-analysis.mjs'));
const {analyze} = await import(at('paper/round2/metrics.mjs'));
const {hash} = await import(at('paper/round2/inputs.mjs'));
const {canonical} = await import(at('scripts/round2/journal.mjs'));

// Claude's thirteen diagnostic cases now fail on a regression rather than just printing.
const say = o => {
 const g=o.got,c=o.case;
 if(c.startsWith('C1 run, everything')) {assert.equal(g.fault_runs_in_the_bound,1);assert.equal(g.attempt.press_markers,1);assert.equal(g.computed.policy_A,1);assert.equal(g.computed.policy_B,1);assert.equal(g.cell_5.confirmed,2);assert.equal(g.cell_4.confirmed,1);}
 else if(c.startsWith('C1 run in which')) {assert.equal(g.second_press_without_draft,1);assert.equal(g.verdict,'observed_failures');}
 else if(c.startsWith('C1 run, the tool')) assert.equal(g.fault_rows[0].outcome,'held_in_doubt');
 else if(c.startsWith('group D')) {assert.equal(g.cell_3.confirmed,1);assert.equal(g.attempt.press_markers,1);assert.equal(g.fault_rows[0].outcome,'finished_by_later_press');}
 else if(c.startsWith('delete, pending')) {assert.equal(g.cell_5.confirmed,1);assert.equal(g.cell_7.confirmed,1);assert.equal(g.second_delete_presses,0);}
 else if(c.startsWith('edit ended')) {assert.equal(g.cell_6.confirmed,1);assert.equal(g.edits[0].confirmed_by,'readback');assert.equal(g.attempt.press_markers,1);}
 else if(c.startsWith('the look after')) {assert.equal(g.test_posts_public,1);assert.equal(g.verdict,'observed_failures');}
 else if(c.startsWith('the only look')) {assert.equal(g.test_posts_public,1);assert.equal(g.missing_looks,1);}
 else if(c.startsWith('FAULT record')) assert.equal(g.fault_rows[0].state,'unknown');
 else if(c.startsWith('C1 run with the snapshots')) {assert.equal(g.repeat_bound.n,2);assert.ok(g.repeat_rows.every(r=>r.no_press&&r.delta===0));assert.ok(g.case.steps_shown_press_free.includes(g.resume_step));}
 else if(c.startsWith('the seal of')) assert.equal(g,'the script ran');
 else if(c.startsWith('in the shakedown')) assert.equal(g.the_word_present_twice_or_more_appears,true);
 else if(c.startsWith('the owner switched')) {assert.equal(g.fault_rows[0].state,'switched_off');assert.equal(g.fault_rows[0].reason,'browser_faults_disabled');}
 else assert.fail('unasserted diagnostic: '+c);
};
const REF = 'f'.repeat(16);
const snap = (presses, state, more = {}) => ({platform: 'youtube', ref: REF, state, final_presses: presses,
  final_submit_attempted: presses > 0, delete_attempted: null, ...more});
const job = (group, faults = [], more = {}) => ({id: 'job-a', run_class: 'test_post', platforms: ['youtube'], group, faults, visibility: 'private', edit: false, ...more});
const planned = (kind, role = 'publish') => ({kind, role, mechanism: ['readback_timeout', 'between_save_and_press', 'delete_readback_lost'].includes(kind) ? 'tool_switch' : 'wrapper_kill', delay_ms: 0});
function start(jobRow) {
  const f = fixture({jobs: [jobRow]});
  f.rows.find(r=>r.type==='GO_AHEAD').d.count=jobRow.edit?6:5;
  f.rows[0].d.plan_sha256 = hash(canonical(f.plan) + '\n');
  return f;
}
function arm(f, kind, m, {role = 'publish', result = {fired: true, failed: false, action_delay_ms: 5, gone_delay_ms: 6}} = {}) {
  f.put('FAULT', {job: 'job-a', ...planned(kind, role)}, m);
  if (result) f.put('FAULT_RESULT', {job: 'job-a', kind, role, ...result}, m);
}
// A command that was ended from outside: it gave no answer and wrote no end event.
function endedFromOutside(f, step) {
  Object.assign(step.d, {result: 'unknown', answered: false, exit: 1, code: null, fault_fired: true,
    answers: step.d.platforms.map(platform => ({platform, state: null, previous_state: null, code: null, press_flag: null, answered: false, refs: [], not_saved: false}))});
  const run = f.tool.runs.find(r => r.awl_run_id === step.d.awl_run_id);
  Object.assign(run, {ended: false, exit_code: null, t_end: null});
  const i = f.tool.events.findIndex(e => e.run === run.run && e.type === 'command_end');
  f.tool.events.splice(i, 1);
}
// A command that contacted the platform to read, and pressed nothing.
function readsOnly(f, step) {
  const id = step.d.awl_run_id, i = f.tool.events.findIndex(e => e.run === id && e.type === 'command_end');
  f.tool.events.splice(i, 0, {type: 'stage', run: id, at: step.at, platform: 'youtube', stage: 'reading back', seq: 0, dropped_before: 0});
}
const run = f => analyze(bundle(f.finish()), {gateHash});
const whole = z => z.tables.find(t => t.platform === null).all_recorded;
const cell = (z, id, platform = 'youtube') => { const c = z.checklist.all_recorded.find(c => c.cell === id && c.platform === platform);
  return c && {status: c.status, tries: c.n, confirmed: c.confirmed}; };
const attempt = z => { const a = whole(z).submission.attempts[0];
  return a && {press_markers: a.press_markers, tool_record_incomplete: a.tool_record_incomplete, duplicate: a.duplicate, look: a.look}; };
const bound = z => { const s = whole(z).submission; return {fault_runs_in_the_bound: s.fault_duplicate_bound.n, left_out_as_unmeasured: s.fault_bound_unmeasured, percent: s.fault_duplicate_bound.percent_rounded_up}; };

// ---- A fault run of kind C1 (the command is ended after the press), then the fixed sequence.
function c1({resumePressesAgain = false, readBackConfirms = true, kind = 'end_command_after_press'} = {}) {
  const f = start(job('C', [planned(kind)]));
  arm(f, kind, 3);
  endedFromOutside(f, f.step({state: 'UNKNOWN', press: 1, m: 4}));
  f.step({role: 'repeat_same', state: 'SKIPPED_EXISTING', exit: 4, press: 1, contact: false, m: 10, answers: {youtube: {previous_state: 'UNKNOWN'}}});
  const one = snap(1, 'UNKNOWN'), n = resumePressesAgain ? 2 : 1;
  if (resumePressesAgain) f.step({role: 'resume', state: 'UNKNOWN', exit: 4, press: 2, contact: true, m: 16, before: one, after: snap(2, 'UNKNOWN')});
  else readsOnly(f, f.step({role: 'resume', state: 'UNKNOWN', exit: 4, press: 1, contact: false, m: 16, before: one, after: one}));
  const end = readBackConfirms ? 'SUBMISSION_VERIFIED' : 'UNKNOWN';
  readsOnly(f, f.step({role: 'verify', state: end, exit: readBackConfirms ? 0 : 4, press: n, contact: false, m: 22, before: snap(n, 'UNKNOWN'), after: snap(n, end)}));
  f.look({seen: 'present_private', m: 40, minutes_after: 20});
  return run(f);
}
test('1. a clean C1 fault run: is it in the bound, in the computed policies and in cell 5?', () => {
  const z = c1(), s = whole(z).submission;
  say({case: 'C1 run, everything as the protocol expects (one post, read-back confirms, look finds one)',
    expect: 'counted in the bound (1 run); markers known as 1; Policy A and B each would have made 1 duplicate; 2 tries of cell 5',
    got: {...bound(z), attempt: attempt(z), computed: s.computed_baselines, repeats: s.repeats.map(r => ({step: r.step, delta: r.delta, unknown: r.unknown})),
      cell_5: cell(z, 5), cell_4: cell(z, 4), must_be_zero_above_zero: Object.entries(whole(z).must_be_zero).filter(([, v]) => v).map(([k, v]) => k + '=' + v)}});
});
test('2. the same C1 run, but resume presses a second time and the platform still shows one post', () => {
  const z = c1({resumePressesAgain: true}), s = whole(z).submission, m = whole(z).must_be_zero;
  say({case: 'C1 run in which resume wrote a second press marker (the failure the study looks for); the look still finds one post',
    expect: 'second_press_without_draft = 1, and the verdict says observed failures',
    got: {second_press_without_draft: m.second_press_without_draft, duplicate_posts: m.duplicate_posts, attempt: attempt(z),
      repeats: s.repeats.map(r => ({step: r.step, delta: r.delta, forbidden_repeat: r.forbidden_repeat})),
      repeat_bound: {bad: s.repeat_bound.bad, n: s.repeat_bound.n, percent: s.repeat_bound.percent_rounded_up},
      assumption_A4_rows: whole(z).assumptions.A4.length, verdict: z.verdict.status, violations: z.verdict.violations}});
});
test('3. a C1 run whose read-back cannot confirm, and the look finds the post', () => {
  const z = c1({readBackConfirms: false});
  say({case: 'C1 run, the tool never confirms, the person finds exactly one post',
    expect: 'the fault row says the run was held in doubt (the post exists)',
    got: {fault_rows: whole(z).submission.faults.map(f => ({kind: f.kind, state: f.state, outcome: f.outcome})),
      cases: whole(z).in_doubt.cases.map(c => ({closed: c.closed, by: c.by}))}});
});
test('4. a group D run: the upload is cut off, resume finishes it with one press', () => {
  const f = start(job('D', [planned('browser_lost_during_upload')]));
  arm(f, 'browser_lost_during_upload', 3);
  endedFromOutside(f, f.step({state: 'IN_PROGRESS', press: 0, m: 4}));
  f.step({role: 'repeat_same', state: 'SKIPPED_EXISTING', exit: 4, press: 0, contact: false, m: 10, answers: {youtube: {previous_state: 'IN_PROGRESS'}}});
  f.step({role: 'resume', state: 'SUBMISSION_VERIFIED', exit: 0, press: 1, contact: true, m: 16, before: snap(0, 'IN_PROGRESS'), after: snap(1, 'SUBMISSION_VERIFIED')});
  readsOnly(f, f.step({role: 'verify', state: 'SUBMISSION_VERIFIED', exit: 0, press: 1, contact: false, m: 22, before: snap(1, 'SUBMISSION_VERIFIED'), after: snap(1, 'SUBMISSION_VERIFIED')}));
  f.look({seen: 'present_private', m: 40, minutes_after: 20});
  const z = run(f);
  say({case: 'group D run, as the protocol expects: one press, made by resume; confirmed; the look finds one post',
    expect: 'cell 3 has 1 try and it is confirmed; markers known as 1; the fault row says how the run ended',
    got: {cell_3: cell(z, 3), attempt: attempt(z), fault_rows: whole(z).submission.faults.map(f => ({kind: f.kind, state: f.state, outcome: f.outcome}))}});
});
test('5. a delete left pending, the same delete once more (refused by the tool), then the read-back', () => {
  const f = start(job('B'));
  f.step({m: 4}); f.look({m: 20, minutes_after: 15});
  f.put('GO_AHEAD', {job: 'job-a', scope: 'one_delete', count: 2, platform: 'youtube', by: 'owner', words_sha256: '3'.repeat(64)}, 21);
  const pending = snap(1, 'DELETE_PENDING', {delete_attempted: true});
  f.step({role: 'delete', kind: 'deletion', state: 'DELETE_PENDING', exit: 4, press: 1, contact: true, m: 22, before: snap(1, 'SUBMISSION_VERIFIED'), after: pending});
  // What the tool prints for the repeat is an error with the word deletion_already_attempted and no record: no state, no delete flag.
  f.step({role: 'delete', kind: 'deletion', exit: 2, press: 1, contact: false, code: 'deletion_already_attempted', m: 28, before: pending, after: pending,
    answers: {youtube: {state: null, press_flag: null}}});
  f.step({role: 'delete_verify', kind: 'deletion', state: 'DELETED_VERIFIED', exit: 0, press: 1, contact: false, m: 34, before: pending, after: snap(1, 'DELETED_VERIFIED', {delete_attempted: true})});
  f.look({about: 'delete', seen: 'absent', m: 60, minutes_after: 20});
  const z = run(f), d = whole(z).readback.deletes;
  say({case: 'delete, pending; repeat refused with the tool\'s own word; read-back says gone; the look finds nothing',
    expect: 'the repeat is a confirmed try of cell 5 (refused, no second press); no second delete press',
    got: {deletes: d.map(x => ({step: x.step, repeat: x.repeat, refused: x.refused, pressed: x.pressed, second_press: x.second_press, code: x.code})),
      cell_5: cell(z, 5), cell_7: cell(z, 7), second_delete_presses: whole(z).must_be_zero.second_delete_presses}});
});
test('6. an edit whose browser is ended after the save (fault 8), then the edit\'s read-back confirms', () => {
  const f = start(job('B', [planned('end_browser_after_edit_save', 'edit')], {edit: true}));
  f.step({m: 4}); f.look({m: 20, minutes_after: 15});
  arm(f, 'end_browser_after_edit_save', 21, {role: 'edit'});
  const same = snap(1, 'SUBMISSION_VERIFIED');
  endedFromOutside(f, f.step({role: 'edit', kind: 'edit', state: 'EDIT_SUBMITTED_UNVERIFIED', press: 1, m: 22, before: same, after: same}));
  readsOnly(f, f.step({role: 'edit', cls: 'read', kind: 'edit', state: 'EDIT_VERIFIED', exit: 0, press: 1, contact: false, m: 28, before: same, after: same}));
  f.look({about: 'edit', seen: 'present_private', m: 50, minutes_after: 20, fields_match: true});
  const z = run(f);
  say({case: 'edit ended from outside after its save; the read-back confirms it on the same post; the look agrees',
    expect: 'the edit is a confirmed try of cell 6; the earlier, undisturbed publish keeps its marker count of 1',
    got: {edits: whole(z).readback.edits, cell_6: cell(z, 6), attempt: attempt(z)}});
});
test('7. a test post that a look finds public after its edit', () => {
  const f = start(job('B', [], {edit: true})), same = snap(1, 'SUBMISSION_VERIFIED');
  f.step({m: 4}); f.look({m: 20, minutes_after: 15});
  f.step({role: 'edit', kind: 'edit', state: 'EDIT_VERIFIED', press: 1, m: 22, before: same, after: same});
  f.look({about: 'edit', seen: 'present_once', m: 50, minutes_after: 20});
  const z = run(f);
  say({case: 'the look after the publish says private; the look after the edit says public (present_once)',
    expect: 'test_posts_public = 1',
    got: {test_posts_public: whole(z).must_be_zero.test_posts_public, edits: whole(z).readback.edits.map(e => ({stayed_private: e.stayed_private, look: e.look})), verdict: z.verdict.status}});
});
test('8. a test post that an early look (5 minutes) finds public, with no later look', () => {
  const f = start(job('B'));
  f.step({m: 4}); f.look({seen: 'present_once', m: 9, minutes_after: 5});
  const z = run(f);
  say({case: 'the only look was made too early to count, and it says public',
    expect: 'test_posts_public = 1 (a public test post is a fact whenever it is seen), and the look is still missing as ground truth',
    got: {test_posts_public: whole(z).must_be_zero.test_posts_public, missing_looks: whole(z).must_be_zero.missing_looks}});
});
test('9. a fault that was armed and has no result record', () => {
  const f = start(job('C', [planned('end_tree_after_press')]));
  arm(f, 'end_tree_after_press', 3, {result: null});
  f.step({state: 'UNKNOWN', exit: 3, press: 1, m: 4});
  readsOnly(f, f.step({role: 'verify', state: 'SUBMISSION_VERIFIED', exit: 0, press: 1, contact: false, m: 10, before: snap(1, 'UNKNOWN'), after: snap(1, 'SUBMISSION_VERIFIED')}));
  f.look({m: 30, minutes_after: 20});
  const z = run(f);
  say({case: 'FAULT record written, the wrapper died before it wrote FAULT_RESULT',
    expect: 'state unknown (third row)',
    got: {fault_rows: whole(z).submission.faults.map(f => ({kind: f.kind, armed: f.armed, state: f.state})), run_row: whole(z).protocol.runs[0].row}});
});
// ---- The same C1 run, written the way the real tool records it (read from its code at c2739d7):
// the command that was ended never copied the worker's record into the ledger, so the next commands' snapshots of the
// ledger are empty until `resume` has merged the worker's own copy; a press is saved with phase "submitting" and no operation word.
test('11. a clean C1 run as the real tool records it: the ledger copy is empty when resume starts', () => {
  const f = start(job('C', [planned('end_command_after_press')]));
  arm(f, 'end_command_after_press', 3);
  const pub = f.step({state: 'UNKNOWN', press: 1, m: 4});
  for (const e of f.tool.events.filter(e => e.run === pub.d.awl_run_id && e.type === 'transition')) Object.assign(e, {operation: null, phase: 'submitting', platform_writes: null});
  endedFromOutside(f, pub);
  f.tool.publications[0].final_presses = 1; f.tool.publications[0].final_submit_attempted = true; // the export merges the worker's own copy
  const empty = {platform: 'youtube', ref: REF, state: 'IN_PROGRESS', final_presses: null, final_submit_attempted: null, delete_attempted: null};
  const again = f.step({role: 'repeat_same', state: 'SKIPPED_EXISTING', exit: 4, press: null, contact: false, m: 10, answers: {youtube: {previous_state: 'IN_PROGRESS'}}});
  const resume = f.step({role: 'resume', state: 'UNKNOWN', exit: 4, press: 1, contact: false, m: 16, before: empty, after: snap(1, 'UNKNOWN')});
  { const id = resume.d.awl_run_id, i = f.tool.events.findIndex(e => e.run === id && e.type === 'command_end');
    f.tool.events.splice(i, 0,
      {type: 'stage', run: id, at: resume.at, platform: 'youtube', stage: 'reading back', seq: 0, dropped_before: 0},
      {type: 'transition', run: id, at: resume.at, platform: 'youtube', kind: 'browser', ref: REF, state: 'UNKNOWN', phase: 'reading_back',
        final_presses: 1, final_submit_attempted: true, operation: null, platform_writes: null, seq: 0, dropped_before: 0}); }
  readsOnly(f, f.step({role: 'verify', state: 'SUBMISSION_VERIFIED', exit: 0, press: 1, contact: false, m: 22, before: snap(1, 'UNKNOWN'), after: snap(1, 'SUBMISSION_VERIFIED')}));
  f.look({seen: 'present_private', m: 40, minutes_after: 20});
  const z = run(f), s = whole(z).submission, c = whole(z).in_doubt.cases[0];
  assert.equal(whole(z).must_be_zero.left_in_doubt_without_settlement, 0);
  say({case: 'C1 run with the snapshots as the real tool writes them; resume prints no pressing stage and saves no start of a submission',
    expect: 'resume is shown to have pressed nothing (its own record is whole and holds no press)',
    got: {repeat_rows: s.repeats.map(r => ({step: r.step, role: r.step === again.seq ? 'the same publish again' : 'resume', delta: r.delta, unknown: r.unknown, no_press: r.no_press})),
      repeat_bound: {bad: s.repeat_bound.bad, n: s.repeat_bound.n}, case: c && {closed: c.closed, by: c.by, steps_shown_press_free: c.ignored_no_press_steps, last_step_that_may_have_pressed: c.last_may_press},
      resume_step: resume.seq, attempt: attempt(z)}});
});
test('12. the shakedown seal names another list than the counted phase', () => {
  const f = fixture({phase: '2a'});
  f.rows[0].d.plan_sha256 = '9'.repeat(64);
  f.put('PROTOCOL_SEAL', {...f.rows[0].d, phase: '2b', plan_sha256: hash(canonical(f.plan) + '\n')}, 3);
  f.step({m: 4}); f.look({m: 20, minutes_after: 15});
  let got; try { run(f); got = 'the script ran'; } catch (e) { got = 'the script stopped: ' + e.message; }
  say({case: 'the seal of 14 November carries the digest of a practice list; the freeze seal carries the digest of the real list',
    expect: 'the script runs (each seal is checked against the list of its own phase)', got});
});
test('13. a shakedown run on a job that is not in the sealed list, seen twice by the look', () => {
  const f = fixture({phase: '2a'});
  f.rows[0].d.plan_sha256 = null;
  f.put('GO_AHEAD', {job: 'practice-1', scope: 'listed_test_posts', count: 20, by: 'owner', words_sha256: 'e'.repeat(64)}, 2);
  const g = f.rows.find(r => r.type === 'GATE_DECISION');
  g.d.due.push('practice-1'); g.d.jobs.push({id: 'practice-1', verdict: 'due', due_at: g.d.jobs[0].due_at, completed: false, authorized: true});
  f.step({job: 'practice-1', m: 4}); f.look({job: 'practice-1', seen: 'present_twice_or_more', m: 20, minutes_after: 15});
  f.put('PROTOCOL_SEAL', {...f.rows[0].d, phase: '2b', plan_sha256: hash(canonical(f.plan) + '\n')}, 25);
  f.step({m: 30}); f.look({m: 50, minutes_after: 15});
  let z; try { z = run(f); } catch (e) { say({case: 'shakedown duplicate on an unlisted job', got: 'the script stopped: ' + e.message}); return; }
  const text = JSON.stringify(z), paths = [];
  (function walk(v, p) { if (typeof v === 'string') { if (v === 'practice-1' || v === 'present_twice_or_more') paths.push(p + '=' + v); }
    else if (v && typeof v === 'object') for (const [k, x] of Object.entries(v)) walk(x, p + '/' + k); })(z, '');
  say({case: 'in the shakedown a practice post is seen twice (a duplicate); the job is not in the list the freeze seals',
    expect: 'the summary shows the duplicate look somewhere',
    got: {the_word_present_twice_or_more_appears: text.includes('present_twice_or_more'), where_the_job_or_the_look_appears: paths.slice(0, 12),
      tables_by_phase: z.tables.map(t => `${t.phase}/${t.platform}`), verdict: z.verdict.status}});
});
test('10. a fault switched off by the site (FAULT_SKIPPED)', () => {
  const f = start(job('C', [planned('end_tree_after_press')]));
  f.put('FAULT_SKIPPED', {job: 'job-a', role: 'publish', kind: 'end_tree_after_press', platform: 'youtube', reason: 'browser_faults_disabled'}, 3);
  f.step({m: 4}); f.look({m: 20, minutes_after: 15});
  const z = run(f);
  say({case: 'the owner switched browser-ending faults off for the platform; the run went ahead without its fault',
    expect: 'the fault row says it was switched off, with the reason',
    got: {fault_rows: whole(z).submission.faults.map(f => ({kind: f.kind, armed: f.armed, state: f.state, reason: f.reason})), run_row: whole(z).protocol.runs[0].row}});
});

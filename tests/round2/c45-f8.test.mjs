// Claude's probe for finding F8 of the reading at Lab 2667f10 (12 November 2026, about 4 AM CT).
// Question: which later answers does figure 4.6's second count ("left in doubt without settlement") take as a failure?
// Made-up records only, built with the script's own test fixture. Nothing here touches an account or a data folder.
// Run from the Lab work tree, alone: node --test <this file>
import test from 'node:test';
import assert from 'node:assert/strict';
import path from 'node:path';
import {pathToFileURL} from 'node:url';
const at = p => pathToFileURL(path.join(process.cwd(), p)).href;
const {fixture, bundle, gateHash} = await import(at('tests/fixtures/round2-analysis.mjs'));
const {analyze} = await import(at('paper/round2/metrics.mjs'));
const {hash} = await import(at('paper/round2/inputs.mjs'));
const {canonical} = await import(at('scripts/round2/journal.mjs'));

// Keep Claude's six inputs unchanged and assert the reported figure, including refusals.
function say({case: label, got}) {
  const expected = label.startsWith('after a press was recorded') ? 1 : 0;
  assert.equal(got.rows_of_4_6.filter(row => row.counted_as_failure).length, expected);
  assert.equal(got.must_be_zero_above_zero.includes('left_in_doubt_without_settlement=1'), expected === 1);
  if (label === 'another command holds the browser')
    assert.ok(got.rows_of_4_6.some(row => row.step === 'the same publish again'));
}
const REF = 'f'.repeat(16);
const snap = (presses, state, more = {}) => ({platform: 'youtube', ref: REF, state, final_presses: presses,
  final_submit_attempted: presses > 0, delete_attempted: null, ...more});
const KIND = 'end_command_after_press';
function endedFromOutside(f, step) {
  Object.assign(step.d, {result: 'unknown', answered: false, exit: 1, code: null, fault_fired: true,
    answers: step.d.platforms.map(platform => ({platform, state: null, previous_state: null, code: null, press_flag: null, answered: false, refs: [], not_saved: false}))});
  const run = f.tool.runs.find(r => r.awl_run_id === step.d.awl_run_id);
  Object.assign(run, {ended: false, exit_code: null, t_end: null});
  const i = f.tool.events.findIndex(e => e.run === run.run && e.type === 'command_end');
  f.tool.events.splice(i, 1);
}
function readsOnly(f, step) {
  const id = step.d.awl_run_id, i = f.tool.events.findIndex(e => e.run === id && e.type === 'command_end');
  f.tool.events.splice(i, 0, {type: 'stage', run: id, at: step.at, platform: 'youtube', stage: 'reading back', seq: 0, dropped_before: 0});
  return step;
}
// A fault run in which the press was made and the command was ended; the record is in doubt with one press.
// `again` writes the same publish again, `resume` writes the resume; the read-back then confirms and the look finds one post.
function runWith({again, resume}) {
  const f = fixture({jobs: [{id: 'job-a', run_class: 'test_post', platforms: ['youtube'], group: 'C', visibility: 'private', edit: false,
    faults: [{kind: KIND, role: 'publish', mechanism: 'wrapper_kill', delay_ms: 0}]}]});
  f.rows[0].d.plan_sha256 = hash(canonical(f.plan) + '\n');
  f.put('FAULT', {job: 'job-a', kind: KIND, role: 'publish', mechanism: 'wrapper_kill', delay_ms: 0}, 3);
  f.put('FAULT_RESULT', {job: 'job-a', kind: KIND, role: 'publish', fired: true, failed: false, action_delay_ms: 5, gone_delay_ms: 6}, 3);
  endedFromOutside(f, f.step({state: 'UNKNOWN', press: 1, m: 4}));
  const one = snap(1, 'UNKNOWN');
  const a = again(f, one), r = resume(f, one);
  readsOnly(f, f.step({role: 'verify', state: 'SUBMISSION_VERIFIED', exit: 0, press: 1, contact: false, m: 22, before: snap(1, r.after ?? 'UNKNOWN'), after: snap(1, 'SUBMISSION_VERIFIED')}));
  f.look({seen: 'present_private', m: 40, minutes_after: 20});
  const z = analyze(bundle(f.finish()), {gateHash}), w = z.tables.find(t => t.platform === null).all_recorded;
  const name = seq => seq === a.step.seq ? 'the same publish again' : seq === r.step.seq ? 'resume' : 'another step';
  return {rows_of_4_6: w.in_doubt.state_changes.map(x => ({step: name(x.step), outcome: x.outcome, counted_as_failure: x.left_unsettled})),
    must_be_zero_above_zero: Object.entries(w.must_be_zero).filter(([, v]) => v).map(([k, v]) => k + '=' + v), verdict: z.verdict.status};
}
// The plain steps of a clean run: the tool answers the repeat for the attempt in doubt, and resume reads the page and presses nothing.
const plainAgain = (f, one) => ({step: f.step({role: 'repeat_same', state: 'SKIPPED_EXISTING', exit: 4, press: 1, contact: false, m: 10, before: one, after: one, answers: {youtube: {previous_state: 'UNKNOWN'}}})});
const plainResume = (f, one) => ({step: readsOnly(f, f.step({role: 'resume', state: 'UNKNOWN', exit: 4, press: 1, contact: false, m: 16, before: one, after: one}))});

test('F8-0. control: the repeat answers for an attempt in doubt, resume reads and presses nothing', () => {
  say({case: 'control', expect: 'no failure', got: runWith({again: plainAgain, resume: plainResume})});
});
test('F8-1. a stop is in force: the same publish again answers NOT_STARTED with the code stopped', () => {
  // cli.py gives this answer before it has looked at its ledger; the record itself is untouched.
  say({case: 'the tool refuses to start because of a stop; the record still says in doubt, one press',
    expect: 'no failure by claim 3b: the answer is about the command, not about the record',
    got: runWith({resume: plainResume,
      again: (f, one) => ({step: f.step({role: 'repeat_same', state: 'NOT_STARTED', code: 'stopped', exit: 4, press: 1, contact: false, m: 10, before: one, after: one, answers: {youtube: {press_flag: null}}})})})});
});
test('F8-2. resume meets a check for a person: WAITING_FOR_USER with the press flag still true', () => {
  say({case: 'resume read the page, pressed nothing, and says a person is needed; the flag stays true',
    expect: 'no failure by claim 3b: the case stays open (Annex A 4.1 d)',
    got: runWith({again: plainAgain,
      resume: (f, one) => ({after: 'WAITING_FOR_USER', step: readsOnly(f, f.step({role: 'resume', state: 'WAITING_FOR_USER', code: 'human_verification_required', exit: 4, press: 1, contact: false, m: 16, before: one, after: snap(1, 'WAITING_FOR_USER')}))})})});
});
test('F8-3. the record goes back: the same publish again answers for an attempt with the press flag false', () => {
  say({case: 'after a press was recorded, the tool answers that the attempt stopped before its press (flag false)',
    expect: 'a failure: the record turned back by itself',
    got: runWith({resume: plainResume,
      again: (f, one) => ({step: f.step({role: 'repeat_same', state: 'SKIPPED_EXISTING', exit: 4, press: 1, contact: false, m: 10, before: one, after: one, answers: {youtube: {previous_state: 'PREPARATION_INCOMPLETE', press_flag: false}}})})})});
});
test('F8-4. the browser is busy: the same publish again answers NOT_STARTED with the code browser_busy', () => {
  say({case: 'another command holds the browser', expect: 'no failure (a try of nothing)',
    got: runWith({resume: plainResume,
      again: (f, one) => ({step: f.step({role: 'repeat_same', state: 'NOT_STARTED', code: 'browser_busy', exit: 4, press: 1, contact: false, m: 10, before: one, after: one, answers: {youtube: {press_flag: null}}})})})});
});
test('F8-5. the ledger copy is stale: the same publish again answers for an attempt still IN_PROGRESS, with no flag', () => {
  say({case: 'as the real tool answers after a command that was ended from outside (my case 11)', expect: 'no failure: the stale copy is not a record that changed',
    got: runWith({resume: plainResume,
      again: (f, one) => ({step: f.step({role: 'repeat_same', state: 'SKIPPED_EXISTING', exit: 4, press: null, contact: false, m: 10, answers: {youtube: {previous_state: 'IN_PROGRESS'}}})})})});
});

test('4.6 reads saved failures and rollbacks from each platform in order', () => {
  for (const [source, expected] of [
    [{state:'FAILED'},1], [{state:'SKIPPED_EXISTING',previous_state:'forgotten'},1],
    [{state:'PREPARING',final_submit_attempted:false},1],
    [{state:'PREPARING',final_submit_attempted:true},0],
    [{state:'FAILED',platform:'douyin'},0],
    [{state:'FAILED',kind:'edit'},0],
  ]) {
    const f=fixture();
    f.step({state:'UNKNOWN',m:3,exit:4});
    const step=f.step({role:'resume',state:'WAITING_FOR_USER',contact:false,m:5,exit:4});
    f.tool.events.splice(-1,0,{type:'transition',run:step.d.awl_run_id,platform:'youtube',ref:REF,...source});
    f.look({m:20});
    const z=analyze(bundle(f.finish()),{gateHash});
    assert.equal(z.tables.find(t=>t.platform===null).all_recorded.must_be_zero.left_in_doubt_without_settlement,expected,JSON.stringify(source));
  }
});

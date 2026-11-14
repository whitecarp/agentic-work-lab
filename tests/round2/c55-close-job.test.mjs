import assert from 'node:assert/strict';
import fs from 'node:fs';
import path from 'node:path';
import test from 'node:test';
import {begin, closeJob, step} from '../../scripts/round2/gate-run.mjs';
import {put} from '../../scripts/round2/journal.mjs';
import {sha} from '../../scripts/round2/io.mjs';
import {outcome} from '../../scripts/round2/policy.mjs';
import {ageJournal, approve, cli, fixture, flags, last, setup} from './helpers.mjs';

const job = 'job-1';
const platform = 'youtube';
const pending = {ok: false, data: {state: 'DELETE_PENDING', delete_attempted: true}};
const refused = {ok: false, error: {code: 'deletion_already_attempted'}};

function ready(t) {
  const f = fixture(t);
  setup(f.ctx, [{id: job, class: 'write', run_class: 'test_post', due_at: '2020-01-01',
    platforms: [platform], wrong_delete_code_first: true}]);
  approve(f.ctx, job, 3, 'one_delete');
  approve(f.ctx, job, 1, 'listed_test_posts');
  f.ctx.run = begin(f.ctx, {}).run;
  return f;
}

async function answer(f, role, exit, reply) {
  const child = [process.execPath, '-e',
    'process.stdout.write(process.argv[1]); process.exitCode = Number(process.argv[2]);',
    reply === null ? '' : JSON.stringify(reply), String(exit)];
  assert.equal(await step(f.ctx, {job, role, platform: [platform]}, child), exit);
  return last(f.ctx, 'STEP').d;
}

function observeAbsent(f, about) {
  return cli(f, 'journal', ['observe', ...flags(f), '--job', job, '--platform', platform,
    '--about', about, '--client', 'phone_app', '--seen', 'absent', '--minutes-after', '10',
    '--by', 'owner'], 'synthetic absent look');
}

function close(f, reason = 'done') {
  return closeJob(f.ctx, {job, reason}).record.d.platform_settlements;
}

test('C55 pending YouTube delete survives its refused repeat and uses the repeat for look timing', async t => {
  const f = ready(t);
  const first = await answer(f, 'delete', 4, pending);
  assert.equal(first.result, 'in_doubt');
  assert.equal(first.answers[0].press_flag, true);
  ageJournal(f.ctx, 11);
  const repeat = await answer(f, 'delete', 2, refused);
  assert.equal(repeat.result, 'refused');
  assert.equal(repeat.code, 'deletion_already_attempted');
  ageJournal(f.ctx, 6);
  const read = outcome(4, JSON.stringify(pending), {role: 'delete_verify', stepClass: 'read', platform});
  put(f.ctx, 'STEP', {job, platforms: [platform], role: 'delete_verify',
    class: 'read', tool: 'vpub', action: 'delete', started: true, exit: 4, ms: 1,
    answers: read.answers, result: read.result, confirmed_state: read.confirmed_state,
    code: read.code, refs: [], capture_sha256: 'c'.repeat(64), fault_fired: null}, f.ctx.run);

  const early = observeAbsent(f, 'delete');
  assert.equal(early.status, 70);
  assert.equal(early.stderr.trim(), 'look_minute_not_reached');
  // A previously recorded look with a premature timestamp cannot close the job either.
  put(f.ctx, 'OBSERVATION', {job, platform, about: 'delete', ref: null, client: 'phone_app',
    by: 'owner', seen: 'absent', minutes_after: 10, fields_match: null,
    words_sha256: 'a'.repeat(64), shot_sha256: null});
  assert.throws(() => close(f), /job_close_evidence_missing/);
  ageJournal(f.ctx, 4); // Ten minutes after the repeat, only four after the read-back.
  const onTime = observeAbsent(f, 'delete');
  assert.equal(onTime.status, 0, onTime.stderr);
  assert.deepEqual(close(f), [{platform, how: 'gone_by_person'}]);
});

test('C55 unknown delete survives a busy repeat', async t => {
  const f = ready(t);
  assert.equal((await answer(f, 'delete', 3, null)).result, 'unknown');
  await answer(f, 'delete', 4, {ok: false, data: {state: 'NOT_STARTED', code: 'browser_busy'}});
  ageJournal(f.ctx, 10);
  assert.equal(observeAbsent(f, 'delete').status, 0);
  assert.deepEqual(close(f), [{platform, how: 'gone_by_person'}]);
});

test('C55 a confirmed delete supersedes older uncertainty before a refused repeat', async t => {
  const f = ready(t);
  await answer(f, 'delete', 4, pending);
  await answer(f, 'delete', 0, {ok: true, data: {state: 'DELETED_VERIFIED', delete_attempted: true}});
  await answer(f, 'delete', 2, refused);
  ageJournal(f.ctx, 10);
  assert.equal(observeAbsent(f, 'delete').status, 0);
  assert.throws(() => close(f), /job_close_evidence_missing/);
});

test('C55 planned wrong-code delete never reached allows nothing_posted', async t => {
  const f = ready(t);
  await answer(f, 'publish', 4, {ok: false, data: {state: 'NOT_STARTED', final_submit_attempted: false}});
  ageJournal(f.ctx, 10);
  assert.equal(observeAbsent(f, 'post').status, 0);
  assert.deepEqual(close(f, 'nothing_posted'), [{platform, how: 'nothing_posted'}]);
});

test('C55 wrong-code refusal alone establishes neither an unresolved delete nor nothing_posted', async t => {
  const f = ready(t);
  await answer(f, 'publish', 4, {ok: false, data: {state: 'NOT_STARTED', final_submit_attempted: false}});
  await answer(f, 'delete', 2, {ok: false, error: {code: 'confirmation_required'}});
  ageJournal(f.ctx, 10);
  assert.equal(observeAbsent(f, 'post').status, 0);
  assert.equal(observeAbsent(f, 'delete').status, 0);
  assert.throws(() => close(f), /job_close_evidence_missing/);
});

for (const issue of ['reservations_missing', 'reservations_invalid', 'sealed_plan_missing',
  'sealed_plan_digest_mismatch', 'working_list_mismatch', 'operator_record_mismatch']) {
  test('C55 begin records a gate error and admits no run for ' + issue, t => {
    const f = fixture(t);
    setup(f.ctx);
    approve(f.ctx);
    const file = name => path.join(f.home, name);
    switch (issue) {
      case 'reservations_missing': fs.unlinkSync(file('reservations.json')); break;
      case 'reservations_invalid': fs.writeFileSync(file('reservations.json'), '{}'); break;
      case 'sealed_plan_missing':
      case 'sealed_plan_digest_mismatch':
        put(f.ctx, 'PROTOCOL_SEAL', {...last(f.ctx, 'PROTOCOL_SEAL').d, plan_sha256: sha('{}')});
        if (issue === 'sealed_plan_digest_mismatch') fs.writeFileSync(file('planned-runs.json'), '[]');
        break;
      case 'working_list_mismatch':
        fs.writeFileSync(file('planned-runs.json'), JSON.stringify({jobs_sha256: sha('[]')}));
        break;
      case 'operator_record_mismatch': fs.writeFileSync(file('operator.json'), '{"operator":"claude"}'); break;
    }
    const result = cli(f, 'gate-run', ['begin', ...flags(f)]);
    assert.equal(result.status, 4);
    assert.deepEqual(JSON.parse(result.stdout), {exit: 4, error: 'gate_threw'});
    assert.equal(result.stderr, '');
    assert.equal(last(f.ctx, 'GATE_ERROR').d.error, 'gate_threw');
    assert.equal(last(f.ctx, 'GATE_DECISION'), undefined);
    assert.equal(fs.existsSync(file('run.lock')), false);
  });
}

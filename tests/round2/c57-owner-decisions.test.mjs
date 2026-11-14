import test from 'node:test';
import assert from 'node:assert/strict';
import fs from 'node:fs';
import path from 'node:path';
import {spawnSync} from 'node:child_process';
import {fixture, setup, approve, flags, cli, last} from './helpers.mjs';
import {begin} from '../../scripts/round2/gate-run.mjs';
import {operation, pressRoles} from '../../scripts/round2/answers.mjs';
import {readJournal} from '../../scripts/round2/journal.mjs';
import {limits} from '../../scripts/round2/policy.mjs';
import {reconcile} from '../../scripts/round2/reconcile.mjs';

function stopFixture(t) {
  const f = fixture(t);
  setup(f.ctx);
  const file = path.join(f.env.VPUB_HOME, 'config.json');
  const earlier = path.join(f.root, 'synthetic-prior-stop');
  const original = {external_stop_file: earlier, fault_injection: false, other: 1};
  fs.writeFileSync(file, JSON.stringify(original));
  const configure = (enabled, extra, input) => cli(f, 'fault-setup',
    ['configure', ...flags(f), '--by', 'owner', '--enabled', String(enabled), ...extra], input);
  return {f, file, earlier, original, configure};
}
const displace = ['--displace-stop', 'true'];
const grant = 'Fixture owner authorizes displacement of the previous registration.';

test('C57 displacement needs explicit opt-in and owner words before any config or journal change', t => {
  const {f, file, configure} = stopFixture(t);
  const config = fs.readFileSync(file), journal = fs.readFileSync(f.journal);
  for (const [extra, input, reason] of [
    [[], '', 'external_stop_already_registered'],
    [[], 'Ordinary setup words', 'external_stop_already_registered'],
    [displace, '  \n', 'empty_words'],
  ]) {
    const result = configure(true, extra, input);
    assert.equal(result.status, 70);
    assert.equal(result.stderr.trim(), reason);
    assert.deepEqual(fs.readFileSync(file), config);
    assert.deepEqual(fs.readFileSync(f.journal), journal);
    assert.equal(fs.existsSync(path.join(f.home, 'fault-config-original.json')), false);
  }
});

test('C57 journals consent to displacement and restores the exact prior value at close', t => {
  const {f, file, earlier, original, configure} = stopFixture(t);
  fs.writeFileSync(earlier, 'fixture stop contents');
  const enabled = configure(true, displace, grant);
  assert.equal(enabled.status, 0, enabled.stderr);
  const record = last(f.ctx, 'FAULT_SETUP');
  assert.equal(record.d.displaced_external_stop, true);
  assert.equal(record.d.stop_registered, true);
  assert.match(record.d.words_sha256, /^[a-f0-9]{64}$/);
  assert.equal(fs.readFileSync(path.join(f.home, 'captures', record.d.words_sha256 + '.words.txt'), 'utf8'), grant);
  assert.deepEqual(JSON.parse(fs.readFileSync(path.join(f.home, 'fault-config-original.json'))),
    {external_stop_file: earlier, fault_injection: false});
  const live = JSON.parse(fs.readFileSync(file));
  assert.deepEqual(live, {...original, external_stop_file: path.join(f.home, 'STOP'), fault_injection: true});
  fs.writeFileSync(file, JSON.stringify({...live, other: 2}));
  const closed = configure(false, [], 'Restore the earlier fixture registration.');
  assert.equal(closed.status, 0, closed.stderr);
  assert.deepEqual(JSON.parse(fs.readFileSync(file)), {...original, other: 2});
  assert.deepEqual(readJournal(f.journal).filter(r => r.type === 'FAULT_SETUP').map(r =>
    [r.d.enabled, r.d.stop_registered, r.d.displaced_external_stop]), [[true, true, true], [false, false, undefined]]);
  assert.equal(fs.readFileSync(earlier, 'utf8'), 'fixture stop contents');
  for (const output of [fs.readFileSync(f.journal, 'utf8'), enabled.stdout, closed.stdout]) {
    assert.equal(output.includes(path.basename(earlier)), false);
    assert.equal(output.includes(grant), false);
  }
});

// Optional sibling checkout for the tool-side contract. Use only the Python
// stop predicate on the fixture home. No CLI, browser, account or installed tool.
test('C57 the CrossCast stop check follows the displaced and restored registration',
  {skip: !process.env.ROUND2_CROSSCAST_SRC && 'supply the CrossCast src directory for the offline check'}, t => {
    const {f, earlier, configure} = stopFixture(t);
    const check = () => {
      const script = [
        'import sys', 'sys.path.insert(0, sys.argv[1])',
        'from video_publisher.core import stop_check, VpubError',
        'try: stop_check("youtube")', 'except VpubError as e: print(e.code)', 'else: print("clear")',
      ].join('\n');
      const result = spawnSync('python', ['-I', '-B', '-c', script, process.env.ROUND2_CROSSCAST_SRC],
        {cwd: f.root, env: f.env, encoding: 'utf8', windowsHide: true});
      assert.equal(result.status, 0, result.stderr);
      return result.stdout.trim();
    };
    fs.writeFileSync(earlier, '{}');
    assert.equal(check(), 'stopped');
    assert.equal(configure(true, displace, grant).status, 0);
    assert.equal(check(), 'clear');
    fs.writeFileSync(path.join(f.home, 'STOP'), '{}');
    assert.equal(check(), 'stopped');
    fs.unlinkSync(path.join(f.home, 'STOP'));
    assert.equal(check(), 'clear');
    assert.equal(configure(false, [], 'Restore the earlier fixture registration.').status, 0);
    assert.equal(check(), 'stopped');
});

test('C57 a wrapped browser login needs one write and consumes no publish slot or planned fault', t => {
  const f = fixture(t);
  const [job] = setup(f.ctx);
  fs.writeFileSync(path.join(f.home, 'planned-runs.json'), JSON.stringify({runs: [{...job, site: 'owner',
    faults: [{kind: 'readback_timeout', mechanism: 'tool_switch', role: 'publish', trigger: 'none',
      delay_ms: 0, armed_on: 'youtube_private'}]}]}));
  approve(f.ctx, job.id, 2, 'listed_test_posts');
  const session = begin(f.ctx, {});
  assert.equal(session.exit, 0);
  const exe = path.join(f.root, process.platform === 'win32' ? 'vpub.exe' : 'vpub');
  try { fs.linkSync(process.execPath, exe); } catch { fs.copyFileSync(process.execPath, exe); }
  // Node impersonates the executable. "auth" is a local script that only records its arguments.
  fs.writeFileSync(path.join(f.root, 'auth'), [
    "if (process.env.VPUB_FAULT) process.exit(97);",
    "require('node:fs').appendFileSync('calls.jsonl', JSON.stringify(process.argv.slice(2)) + '\\n');",
    "console.log(JSON.stringify({ok:true}));",
  ].join('\n'));
  const calls = path.join(f.root, 'calls.jsonl');
  const args = [path.resolve('scripts/round2/gate-run.mjs'), 'step', ...flags(f), '--run', session.run,
    '--job', job.id, '--role', 'other', '--', exe, 'auth', 'login', 'youtube', '--method', 'browser'];
  const run = () => spawnSync(process.execPath, args,
    {cwd: f.root, env: f.env, encoding: 'utf8', windowsHide: true});
  const refused = () => {
    const result = run();
    assert.equal(result.status, 5, result.stderr);
    assert.equal(result.stderr.trim(), 'go_ahead_exhausted');
    assert.equal(last(f.ctx, 'STEP_REFUSED').d.reason, 'go_ahead_exhausted');
  };
  refused();
  assert.equal(fs.existsSync(calls), false);
  approve(f.ctx, job.id, 1, 'one_write');
  const allowed = run();
  assert.equal(allowed.status, 0, allowed.stderr);
  refused();
  assert.deepEqual(JSON.parse(fs.readFileSync(calls, 'utf8')), ['login', 'youtube', '--method', 'browser']);
  const records = readJournal(f.journal), steps = records.filter(r => r.type === 'STEP');
  assert.equal(steps.length, 1);
  const [login] = steps;
  assert.deepEqual([login.d.tool, login.d.action, login.d.role, login.d.class, login.d.started, login.d.result],
    ['vpub', 'auth', 'other', 'write', true, 'ok']);
  assert.equal(operation(login), null);
  assert.equal(pressRoles.includes(login.d.role), false);
  assert.deepEqual(reconcile(f.ctx).look_checks, []);
  assert.equal(records.some(r => ['FAULT','FAULT_RESULT'].includes(r.type)), false);
  const reservations = JSON.parse(fs.readFileSync(path.join(f.home, 'reservations.json')));
  assert.equal(reservations.length, 1);
  assert.equal(reservations[0].scope, 'one_write');
  // Three distinct earlier uploads leave one slot. Counting the recent login
  // as an upload would now trip either the daily cap or the spacing rule.
  const now = Date.parse(login.at);
  const earlier = [1,2,3].map(n => ({job: 'prior-' + n, role: 'publish', run_class: 'test_post',
    platforms: ['youtube'], started: true, at: new Date(now - n*3600000).toISOString()}));
  assert.equal(limits(records, {...job, id: 'next-post'}, 'publish', ['youtube'], login.at,
    [...earlier, ...reservations], [job]), null);
});

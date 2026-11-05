// Recomputes every number the manuscript reports from the committed data files.
// Run from anywhere:  node paper/reproduce_paper_tables.mjs
// Reads:  this repository's data/, media/, results/ and tests/ folders, and a sibling
//         checkout of publishops-crosscast (read-only; files are read with `git show`).
// Writes: paper/paper_metrics_summary.json next to this script. Nothing else is changed.
import fs from 'node:fs';
import path from 'node:path';
import { execFileSync } from 'node:child_process';
import { fileURLToPath } from 'node:url';

const here = path.dirname(fileURLToPath(import.meta.url));
const lab = path.resolve(here, '..');
const cc = process.env.CROSSCAST_REPO || path.resolve(lab, '..', 'publishops-crosscast');
const ccCommit = process.env.CROSSCAST_COMMIT || '75b7abc';
const ccReleaseCommit = 'a46bd19';
const readJson = rel => JSON.parse(fs.readFileSync(path.join(lab, rel), 'utf8'));

// ---------- helpers ----------
const sum = a => a.reduce((s, x) => s + x, 0);
const mean = a => sum(a) / a.length;
const sd = a => { const m = mean(a); return Math.sqrt(sum(a.map(x => (x - m) ** 2)) / (a.length - 1)); }; // sample SD
const quantile = (a, p) => { const s = [...a].sort((x, y) => x - y); const h = (s.length - 1) * p; const lo = Math.floor(h); return s[lo] + (h - lo) * ((s[lo + 1] ?? s[lo]) - s[lo]); }; // linear (R type 7)
const r2 = x => Math.round(x * 100) / 100;
const pct = (a, b) => r2(a / b * 100);
const has = (o, k) => Object.prototype.hasOwnProperty.call(o, k);
const git = (...args) => execFileSync('git', ['-C', cc, ...args], { encoding: 'utf8', maxBuffer: 1 << 28 });
const physicalLines = t => (t.length ? t.split('\n').length - (t.endsWith('\n') ? 1 : 0) : 0); // as `wc -l` counts them

// ---------- Table 3: operational journal, tabulated by the evidence each record carries ----------
const opsFile = readJson('data/operations-events.json');
const ops = opsFile.records;
const classNames = ['explicit_zero_write', 'zero_platform_calls', 'platform_call_without_write_field', 'platform_write', 'no_call_or_write_field'];
const classes = Object.fromEntries(classNames.map(k => [k, { ids: [], reads: 0, writes: 0 }]));
ops.forEach((r, i) => {
  const writes = [r.platform_writes, r.write_calls, r.platform_write_calls].filter(v => typeof v === 'number');
  let c;
  if (writes.some(v => v > 0)) c = 'platform_write';
  else if (writes.length) c = 'explicit_zero_write';
  else if (r.platform_calls === 0) c = 'zero_platform_calls';
  else if (has(r, 'platform_calls')) c = 'platform_call_without_write_field';
  else c = 'no_call_or_write_field';
  classes[c].ids.push(i + 1);
  classes[c].reads += (r.platform_reads || 0) + (r.read_calls || 0);
  classes[c].writes += sum(writes);
});
const stamps = ops.map(r => r.at || r.checked_at).filter(Boolean).sort((a, b) => Date.parse(a) - Date.parse(b));
const writeNo = classes.platform_write.ids[0];
const table3 = {
  source: 'data/operations-events.json',
  exported_at: opsFile.exported_at,
  records: ops.length,
  classes: Object.fromEntries(classNames.map(k => [k, { records: classes[k].ids.length, share_pct: pct(classes[k].ids.length, ops.length), platform_reads: classes[k].reads, platform_writes: classes[k].writes, record_numbers: classes[k].ids }])),
  platform_reads_total: sum(classNames.map(k => classes[k].reads)),
  platform_writes_total: sum(classNames.map(k => classes[k].writes)),
  write_record: { number: writeNo, ...ops[writeNo - 1] },
  cli_calls_total: sum(ops.map(r => r.cli_calls || 0)),
  cli_call_records: ops.filter(r => has(r, 'cli_calls')).length,
  named_event_records: ops.filter(r => has(r, 'event')).length,
  state_only_records: ops.filter(r => !has(r, 'event') && has(r, 'state')).length,
  unnamed_records: ops.filter(r => !has(r, 'event') && !has(r, 'state')).length,
  automation_trigger_records: ops.filter(r => has(r, 'trigger')).length,
  new_risk_present: ops.filter(r => has(r, 'new_risk')).length,
  new_risk_true: ops.filter(r => r.new_risk === true).length,
  upload_attempts_replayed_present: ops.filter(r => has(r, 'upload_attempts_replayed')).length,
  upload_attempts_replayed_sum: sum(ops.map(r => r.upload_attempts_replayed || 0)),
  timestamped_records: stamps.length,
  first_timestamp: stamps[0],
  last_timestamp: stamps.at(-1),
  span_hours: r2((Date.parse(stamps.at(-1)) - Date.parse(stamps[0])) / 3.6e6)
};

// ---------- heartbeat ----------
const run = readJson('data/scheduled-run-evidence.json');
const heartbeat = { trigger: run.trigger, status: run.status, planned_at: run.planned_at, trigger_delay_ms: run.trigger_delay_ms, duration_ms: run.duration_ms, task_errors: run.task_errors.length, manual_test_triggered: run.manual_test_triggered, duplicate_run_started: run.duplicate_run_started, work_items: run.work_completed.length };

// ---------- Tables 6 and 7: the 3 November release record ----------
const upd = readJson('data/production-update-2026-11-03.json');
const ig = upd.instagram_timing_seconds;
const pre = +(ig.source_plan_checks_outside_publish + ig.rehearsal_outside_publish + ig.auth_preflight_outside_publish).toFixed(3);
const e2e = +(pre + ig.publish_total).toFixed(3);
const table7 = {
  ...ig, pre_publish_subtotal: pre, end_to_end: e2e,
  residual_derived: +(ig.publish_total - ig.browser_start_to_file_entry - ig.after_submit_wait - ig.independent_readback).toFixed(3),
  wait_pct_of_command: pct(ig.after_submit_wait, ig.publish_total), wait_pct_of_e2e: pct(ig.after_submit_wait, e2e),
  timing_limits: upd.timing_limits
};
const film = upd.new_media[0];
const table6 = { file: film.file, bytes: film.bytes, sha256: film.sha256, duration_seconds: film.duration_seconds, platforms: upd.film_platforms, bilibili_state_at_snapshot: film.bilibili_state_at_snapshot, state_verified_at: film.state_verified_at, local_read_discrepancy_resolved: film.validation.historical_local_read_discrepancy_resolved };

// ---------- Table 9: media manifest ----------
const man = readJson('media/manifest.json');
const sums = new Map(fs.readFileSync(path.join(lab, 'media/SHA256SUMS.txt'), 'utf8').trim().split(/\r?\n/).map(l => { const [h, f] = l.trim().split(/\s+\*?/); return [f.replace(/^\*/, ''), h]; }));
const bytes = sum(man.items.map(i => i.bytes));
const table9 = {
  items: man.items.length, total_bytes: bytes, total_gb_decimal: r2(bytes / 1e9), total_mib: r2(bytes / 2 ** 20),
  release_tags: man.release_urls.length,
  github_asset_status_published_verified: man.items.filter(i => i.github_asset_status === 'published_verified').length,
  manifest_vs_sha256sums_mismatches: man.items.filter(i => sums.get(i.file) !== i.sha256).length,
  full_decode_zero_errors_recorded: man.items.filter(i => i.validation && (i.validation.full_decode_errors === 0 || i.validation.production_full_decode_errors === 0)).length,
  smallest_bytes: Math.min(...man.items.map(i => i.bytes)), largest_bytes: Math.max(...man.items.map(i => i.bytes)),
  github_verified_at: man.github_verified_at
};

// ---------- Table 10: channel snapshot (descriptive only) ----------
const snap = readJson('data/creator-video-snapshot.json');
const metrics = ['views', 'likes', 'comments', 'favorites', 'coins', 'shares', 'danmaku'];
const describe = rows => Object.fromEntries(metrics.map(m => { const a = rows.map(r => r[m]); return [m, { mean: r2(mean(a)), sd: r2(sd(a)), median: quantile(a, 0.5), q1: quantile(a, 0.25), q3: quantile(a, 0.75), min: Math.min(...a), max: Math.max(...a), sum: sum(a) }]; }));
const human = snap.items.filter(x => x.cohort === 'human_led_history');
const aiAll = snap.items.filter(x => x.cohort === 'current_ai_series');
const aiPub = aiAll.filter(x => x.published);
const sampledAt = Date.parse(snap.items[0].sampled_at);
const agesH = aiPub.map(x => (sampledAt - Date.parse(x.publish_time_raw)) / 3.6e6);
const weekly = readJson('data/channel-weekly.json');
const table10 = {
  sampled_at: snap.items[0].sampled_at, records: snap.items.length,
  cohorts: { human_led_history: human.length, current_ai_series: aiAll.length, current_ai_series_published: aiPub.length },
  human_publication_range: [human.map(x => x.publish_time_raw).sort()[0], human.map(x => x.publish_time_raw).sort().at(-1)],
  ai_published_age_hours: { min: r2(Math.min(...agesH)), max: r2(Math.max(...agesH)) },
  complete_separation_of_views: Math.min(...human.map(x => x.views)) > Math.max(...aiPub.map(x => x.views)),
  human_min_views: Math.min(...human.map(x => x.views)), ai_max_views: Math.max(...aiPub.map(x => x.views)),
  human: describe(human), ai_published: describe(aiPub),
  weekly_plays: weekly.plays, weekly_data_date: weekly.data_date, ott_share_pct: weekly.play_platforms_percent.ott,
  top_videos_yesterday: weekly.top_videos_yesterday.map(v => v.plays),
  earlier_snapshot_records: readJson('data/snapshots/2026-10-28-afternoon/creator-video-snapshot.json').items.length,
  day_one: { gpt_ep03_24h: readJson('data/episode03-24h-2026-10-28.json').metrics, whale_ep02_24h: readJson('data/whale-log02-24h-2026-10-29.json').metrics }
};

// ---------- Table 4: code and test volume of publishops-crosscast at a pinned commit ----------
// Test groups are assigned by filename, first matching rule wins; a file may exercise several concerns.
const groupRules = [
  ['deletion_and_absence_readback', /^tests\/test_(work_delete|deletion)/],
  ['edits_and_metadata_readback', /edit/],
  ['collections_and_playlists', /collection|playlist/],
  ['publish_workflow_receipts_locks_recovery', /publish|two_commands|test_workflow|rehearsal|less_waiting|progress|browser_backend|browser_broker|runtime_cleanup|safety_processes|hidden_desktop_lifecycle|auth_jobs_lifecycle|file_selection_guards|closeout|stage1_stops|stage1_repeats/],
  ['platform_pages_and_adapters', /youtube|test_x_|douyin|instagram|bili/],
  ['messages_comments_engagement_safety', /message|comment|engage|likes|repl|hostile|safety|platform_warnings|stage1_limits|stage1_reading|stage1_reporting|stage2_|write_answers/],
  ['other_cli_docs_install', /./]
];
function codeVolume(rev) {
  const files = git('ls-tree', '-r', '--name-only', rev).split('\n').filter(Boolean);
  const src = files.filter(f => /^src\/.*\.py$/.test(f));
  const tests = files.filter(f => /^tests\/.*\.py$/.test(f));
  const out = { commit: git('rev-parse', rev).trim(), src_files: src.length, src_lines: 0, own_package_files: 0, own_package_lines: 0, bundled_bili_cli_files: 0, bundled_bili_cli_lines: 0, test_files: tests.length, test_modules: tests.filter(f => /^tests\/test_[^/]*\.py$/.test(f)).length, test_lines: 0, test_functions: 0, groups: {} };
  for (const f of src) {
    const n = physicalLines(git('show', `${rev}:${f}`));
    out.src_lines += n;
    if (f.startsWith('src/bili_cli/')) { out.bundled_bili_cli_files++; out.bundled_bili_cli_lines += n; }
    else if (f.startsWith('src/video_publisher/')) { out.own_package_files++; out.own_package_lines += n; }
  }
  for (const f of tests) {
    const t = git('show', `${rev}:${f}`);
    const n = physicalLines(t);
    const fn = (t.match(/^\s*(async\s+)?def test_/gm) || []).length;
    out.test_lines += n; out.test_functions += fn;
    if (!/^tests\/test_/.test(f)) continue; // shared helpers count in the totals only
    const g = groupRules.find(([, re]) => re.test(f))[0];
    out.groups[g] ??= { files: 0, lines: 0, test_functions: 0 };
    out.groups[g].files++; out.groups[g].lines += n; out.groups[g].test_functions += fn;
  }
  out.test_to_source_line_ratio = r2(out.test_lines / out.src_lines);
  out.test_to_own_package_line_ratio = r2(out.test_lines / out.own_package_lines);
  return out;
}
let crosscast;
try {
  crosscast = { repository: cc, pinned: codeVolume(ccCommit), release_v0_16_0: codeVolume(ccReleaseCommit), group_rules: groupRules.map(([g, re]) => [g, String(re)]) };
} catch (e) {
  crosscast = { error: `Could not read ${cc} at ${ccCommit}: ${e.message.split('\n')[0]}` };
}

// ---------- lab workflow tests ----------
const validation = readJson('results/validation.json');
const labTests = { file: 'tests/workflows.test.mjs', lines: physicalLines(fs.readFileSync(path.join(lab, 'tests/workflows.test.mjs'), 'utf8').replace(/\r\n/g, '\n')), recorded: validation.workflow_tests, recorded_on: validation.checked_date, node_version: validation.node_version };

const summary = {
  generated_at: new Date().toISOString(),
  table3_operational_journal: table3, heartbeat, table6_release: table6, table7_instagram_timing: table7,
  table9_manifest: table9, table10_channel_snapshot: table10, table4_crosscast: crosscast, lab_workflow_tests: labTests,
  note: 'Descriptive statistics only. No significance test is reported: lifetime views separate completely between the cohorts, so a rank test would restate the exposure-age confound rather than test a production effect.'
};
fs.writeFileSync(path.join(here, 'paper_metrics_summary.json'), JSON.stringify(summary, null, 2) + '\n');
console.log(JSON.stringify(summary, null, 2));

import fs from 'node:fs';
import path from 'node:path';

const lab = 'C:/Users/ROG/.gemini/antigravity/scratch/agentic-sci-paper/agentic-work-lab';
const cc = 'C:/Users/ROG/.gemini/antigravity/scratch/agentic-sci-paper/publishops-crosscast';
const outDir = 'C:/Users/ROG/.gemini/antigravity/scratch/agentic-sci-paper/paper';
fs.mkdirSync(outDir, { recursive: true });

// 1. Load datasets
const ops = JSON.parse(fs.readFileSync(path.join(lab, 'data/operations-events.json'), 'utf8'));
const snap = JSON.parse(fs.readFileSync(path.join(lab, 'data/creator-video-snapshot.json'), 'utf8'));
const manifest = JSON.parse(fs.readFileSync(path.join(lab, 'media/manifest.json'), 'utf8'));
const schedRun = JSON.parse(fs.readFileSync(path.join(lab, 'data/scheduled-run-evidence.json'), 'utf8'));
const ep03_24h = JSON.parse(fs.readFileSync(path.join(lab, 'data/episode03-24h-2026-10-28.json'), 'utf8'));
const whale02_24h = JSON.parse(fs.readFileSync(path.join(lab, 'data/whale-log02-24h-2026-10-29.json'), 'utf8'));
const upd1103 = JSON.parse(fs.readFileSync(path.join(lab, 'data/production-update-2026-11-03.json'), 'utf8'));

// 2. Summarize Instagram latency breakdown (Table 4 in paper)
const ig = upd1103.instagram_timing_seconds;
const prePublishOutside = +(ig.source_plan_checks_outside_publish + ig.rehearsal_outside_publish + ig.auth_preflight_outside_publish).toFixed(3);
const endToEndObserved = +(prePublishOutside + ig.publish_total).toFixed(3);
const igBreakdown = {
  source_plan_checks_s: ig.source_plan_checks_outside_publish,
  rehearsal_s: ig.rehearsal_outside_publish,
  auth_preflight_s: ig.auth_preflight_outside_publish,
  pre_publish_subtotal_s: prePublishOutside,
  browser_start_to_file_entry_s: ig.browser_start_to_file_entry,
  after_submit_wait_s: ig.after_submit_wait,
  independent_readback_s: ig.independent_readback,
  other_in_command_overhead_s: +(ig.publish_total - (ig.browser_start_to_file_entry + ig.after_submit_wait + ig.independent_readback)).toFixed(3),
  publish_command_total_s: ig.publish_total,
  end_to_end_total_s: endToEndObserved,
  after_submit_wait_pct_of_publish: +((ig.after_submit_wait / ig.publish_total) * 100).toFixed(2),
  after_submit_wait_pct_of_e2e: +((ig.after_submit_wait / endToEndObserved) * 100).toFixed(2)
};

// 3. Summarize 30 archived media artifacts by resolution and duration
let durationSum = 0;
let durationCount = 0;
let landscapeCount = 0;
let portraitCount = 0;
let fullDecodeCheckedCount = 0;
for (const item of manifest.items) {
  if (typeof item.duration_seconds === 'number') {
    durationSum += item.duration_seconds;
    durationCount++;
  }
  if (item.width && item.height) {
    if (item.width > item.height) landscapeCount++;
    else if (item.height > item.width) portraitCount++;
  }
  if (item.validation && item.validation.full_decode_errors === 0 || item.validation && item.validation.production_full_decode_errors === 0) {
    fullDecodeCheckedCount++;
  }
}

const summary = {
  generated_at: new Date().toISOString(),
  repositories: {
    agentic_work_lab_videos: manifest.items.length,
    agentic_work_lab_total_bytes: manifest.items.reduce((s, x) => s + (x.bytes || 0), 0),
    agentic_work_lab_operations_events: ops.records.length,
    agentic_work_lab_channel_records: snap.items.length,
    crosscast_version: upd1103.crosscast.released_version,
    crosscast_release_commit: upd1103.crosscast.release_commit,
    crosscast_main_commit: upd1103.crosscast.main_at_snapshot
  },
  scheduled_run_verification: {
    trigger_delay_ms: schedRun.trigger_delay_ms,
    duration_ms: schedRun.duration_ms,
    task_errors: schedRun.task_errors.length,
    duplicate_run_started: schedRun.duplicate_run_started,
    manual_test_triggered: schedRun.manual_test_triggered
  },
  instagram_latency_analysis: igBreakdown,
  day_one_checkpoints: {
    gpt_ep03_24h: ep03_24h.metrics,
    whale_ep02_24h: whale02_24h.metrics
  }
};

fs.writeFileSync(path.join(outDir, 'paper_metrics_summary.json'), JSON.stringify(summary, null, 2));
console.log(JSON.stringify(summary, null, 2));

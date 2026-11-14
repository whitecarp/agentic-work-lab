# What the Code Does

[繁體中文](code-map.md) · **English**

The `tools/` directory in this repository contains the full source code and automated test suites for three core engineering toolkits:
1. **[`tools/ai-memory-keeper/`](../tools/ai-memory-keeper/README_EN.md)** (standalone repository [`whitecarp/ai-memory-keeper`](https://github.com/whitecarp/ai-memory-keeper)): cross-agent context-compaction memory guardian, model-tier verifier, and Gemini constellation web console.
2. **[`tools/i-must-use-claude/`](../tools/i-must-use-claude/README_EN.md)** (standalone repository [`whitecarp/I-Must-Use-Claude`](https://github.com/whitecarp/I-Must-Use-Claude)): four-in-one native Windows desktop utility suite for everyday Claude workflows (reversible environment/DNS diagnostics, real-time bilingual companion translator, dual-timezone quota clock, and ≤29 MB context packager).
3. **[`tools/publishops-crosscast/`](../tools/publishops-crosscast/README_EN.md)** (standalone repository [`whitecarp/publishops-crosscast`](https://github.com/whitecarp/publishops-crosscast)): multi-platform video publishing console `vpub` (synced to **v0.16.0 alpha** with bundled `bili-cli` 1.2.1; English, Traditional Chinese, and Simplified Chinese docs included).

This project combines human-defined requirements, Golden Triangle multi-agent implementation (Antigravity + Claude + Codex (powered by Google Gemini/Vertex AI, Anthropic Claude, and OpenAI GPT/Codex)), tool-based verification, and human review. Reproducibility and failure handling matter more than listing languages or line counts.

---

## 1. AI Memory Keeper: Cross-Agent Memory & Model Verification (`tools/ai-memory-keeper/`)

- [cli.mjs](../tools/ai-memory-keeper/src/cli.mjs) & [hooks.mjs](../tools/ai-memory-keeper/src/hooks.mjs): `amk` CLI entry point; non-destructively installs and removes ownership-marked compaction hooks for Claude Code (`.claude/settings.local.json`), Codex (`.codex/hooks.json`), Antigravity (`.agents/rules/`), and WorkBuddy.
- [memory.mjs](../tools/ai-memory-keeper/src/memory.mjs), [store.mjs](../tools/ai-memory-keeper/src/store.mjs) & [engine.mjs](../tools/ai-memory-keeper/src/engine.mjs): three-tier local memory storage (immutable transcript history, active compacted context, and pinned high-priority rules) plus Gemini-backed extraction of preferences, decisions, and constraints.
- [key.mjs](../tools/ai-memory-keeper/src/key.mjs) & [omni.mjs](../tools/ai-memory-keeper/src/omni.mjs): Windows DPAPI local credential encryption, automatic fallback routing between AI Studio free tier and Google Cloud Service Account JSON tokens (locally signed RS256 JWT exchanged for OAuth2 Bearer tokens), and `gemini-omni-1.2-flash` video generation calls.
- [audit.mjs](../tools/ai-memory-keeper/src/audit.mjs) & [safety.mjs](../tools/ai-memory-keeper/src/safety.mjs): pre-flight secret scrubbing (API keys, passwords, tokens) and model-tier verification comparing requested vs. replying models in Claude Code.
- [overlay-native.cs](../tools/ai-memory-keeper/src/overlay-native.cs) & [overlay.mjs](../tools/ai-memory-keeper/src/overlay.mjs): borderless native Windows guardian pop-up reporting restored memories during compaction.
- [web/public/core.js](../tools/ai-memory-keeper/web/public/core.js) & [web/test/core.test.mjs](../tools/ai-memory-keeper/web/test/core.test.mjs): static web console chunking, multi-round merging, Twins constellation rendering, and 33 zero-dependency unit tests.

---

## 2. I Must Use Claude: 4-in-1 Native Windows Toolbox (`tools/i-must-use-claude/`)

- **Whale Environment Helper ([`apps/whale-claude-helper/`](../tools/i-must-use-claude/apps/whale-claude-helper/README.md))**:
  - [RepairEngine.cs](../tools/i-must-use-claude/apps/whale-claude-helper/src/RepairEngine.cs) & [OptimizerService.cs](../tools/i-must-use-claude/apps/whale-claude-helper/src/OptimizerService.cs): measures browser environment baselines and creates private local backups before applying matched adjustments, with conflict-aware one-click undo.
  - [NetworkDnsService.cs](../tools/i-must-use-claude/apps/whale-claude-helper/src/NetworkDnsService.cs) & [CodexProxySettings.cs](../tools/i-must-use-claude/apps/whale-claude-helper/src/CodexProxySettings.cs): local DNS resolver path measurement, CuteCloud DNS preview/backup/apply/undo, and Codex proxy setup.
- **Whale Translator ([`apps/claude-live-translator/`](../tools/i-must-use-claude/apps/claude-live-translator/README.md))**:
  - [ClaudeReader.cs](../tools/i-must-use-claude/apps/claude-live-translator/src/ClaudeReader.cs), [CompanionForm.cs](../tools/i-must-use-claude/apps/claude-live-translator/src/CompanionForm.cs) & [Glossary.cs](../tools/i-must-use-claude/apps/claude-live-translator/src/Glossary.cs): live companion translation of Claude replies, Chinese-to-English prompt drafting, custom glossary locking, and offline Argos language-pack management.
- **Two-Zone Clock ([`apps/two-zone-clock/`](../tools/i-must-use-claude/apps/two-zone-clock/README.md))**:
  - [WorldClock.cs](../tools/i-must-use-claude/apps/two-zone-clock/WorldClock.cs) & [ResetCountdown.cs](../tools/i-must-use-claude/apps/two-zone-clock/ResetCountdown.cs): always-visible dual-timezone clock with countdowns to Claude's 5-hour session and weekly quota resets.
- **Whale Pack Helper ([`apps/whale-pack-helper/`](../tools/i-must-use-claude/apps/whale-pack-helper/README.md))**:
  - [Scanner.cs](../tools/i-must-use-claude/apps/whale-pack-helper/src/Scanner.cs) & [Packaging.cs](../tools/i-must-use-claude/apps/whale-pack-helper/src/Packaging.cs): scans and filters local workspaces, splitting source code, docs, and media into upload-ready bundles (strictly ≤ 29 MB per archive) with an automatic handoff manifest.
- **Shared Update Verifier ([shared/UpdateChecker.cs](../tools/i-must-use-claude/shared/UpdateChecker.cs))**: manual GitHub Release update checker that enforces SHA-256 digest verification and never overwrites a running binary automatically.

---

## 3. PublishOps CrossCast: Publishing, Rehearsal, and Safety Rails (`tools/publishops-crosscast/` · v0.16.0 alpha)

- [cli.py](../tools/publishops-crosscast/src/video_publisher/cli.py), [quick.py](../tools/publishops-crosscast/src/video_publisher/quick.py), and [rehearsal.py](../tools/publishops-crosscast/src/video_publisher/rehearsal.py): `vpub` CLI entry point, one-request quick flow (`prepare`/`go`), and offline `--dry-run` rehearsal checks.
- [safety.py](../tools/publishops-crosscast/src/video_publisher/safety.py), [language_policy.py](../tools/publishops-crosscast/src/video_publisher/language_policy.py), and [publication_review.py](../tools/publishops-crosscast/src/video_publisher/publication_review.py): daily rate limits, repeat guards, warning stops, paired confirmation (`--confirm` + `--person-approved`), per-platform copy/AI-disclosure checks, and pre-publish plan review.
- [process_lock.py](../tools/publishops-crosscast/src/video_publisher/process_lock.py), [browser_broker.py](../tools/publishops-crosscast/src/video_publisher/browser_broker.py), [browser_runtime.py](../tools/publishops-crosscast/src/video_publisher/browser_runtime.py), and [hidden_desktop.py](../tools/publishops-crosscast/src/video_publisher/hidden_desktop.py): cross-process exclusion with verified dead-owner cleanup, reusable dedicated browser broker/runtime isolation, and Windows hidden-desktop execution.
- [youtube_studio.py](../tools/publishops-crosscast/src/video_publisher/youtube_studio.py), [youtube_playlists.py](../tools/publishops-crosscast/src/video_publisher/youtube_playlists.py), and [x_pages.py](../tools/publishops-crosscast/src/video_publisher/x_pages.py): YouTube Studio free-route browser uploads and playlist CRUD, plus dual-variant X / Media Studio routing.
- [bili_cli/main.py](../tools/publishops-crosscast/src/bili_cli/main.py), [drafts_bilibili.py](../tools/publishops-crosscast/src/video_publisher/drafts_bilibili.py), [edits_bilibili.py](../tools/publishops-crosscast/src/video_publisher/edits_bilibili.py), and [widgets_bilibili.py](../tools/publishops-crosscast/src/video_publisher/widgets_bilibili.py): bundled `bili-cli` 1.2.1 alongside Bilibili draft uploads, post-publish metadata edits, and interactive video widgets.
- [collections.py](../tools/publishops-crosscast/src/video_publisher/collections.py), [collections_browser.py](../tools/publishops-crosscast/src/video_publisher/collections_browser.py), and [read_scheduler.py](../tools/publishops-crosscast/src/video_publisher/read_scheduler.py): cross-platform collection/folder management, bounded concurrency, and resumable read checkpoints.
- [work_catalog.py](../tools/publishops-crosscast/src/video_publisher/work_catalog.py), [overview.py](../tools/publishops-crosscast/src/video_publisher/overview.py), [progress.py](../tools/publishops-crosscast/src/video_publisher/progress.py), [operations_runs.py](../tools/publishops-crosscast/src/video_publisher/operations_runs.py), and [work_delete.py](../tools/publishops-crosscast/src/video_publisher/work_delete.py): work adoption, overview/status reporting, live progress events, interruption recovery, and per-post explicit deletion (YouTube/Douyin/Instagram/X browser routes; Bilibili unsupported).

---

## 4. Runnable Workflow Scripts at Repository Root

| Code | Problem Solved |
| --- | --- |
| [prioritize-merchants.mjs](../scripts/prioritize-merchants.mjs) | Standardise lead fields, explain priorities, and flag missing information. |
| [summarize-channel.mjs](../scripts/summarize-channel.mjs) | Recompute descriptive cohort statistics from the actual dataset. |
| [operations-gate.mjs](../scripts/operations-gate.mjs) | Demonstrate sole-operator, stop, due-time, and duplicate checks offline. |
| [check-delivery.mjs](../scripts/check-delivery.mjs) | Stream-check sizes and SHA-256 to identify wrong or incomplete downloads. |
| [workflows.test.mjs](../tests/workflows.test.mjs) | Test duplicate work, invalid times, unauthorised jobs, and missing data. |

```sh
npm run demo
npm run report
npm test
node scripts/check-delivery.mjs /path/to/downloaded/videos
```

No `npm install` is required; the public examples use Node.js standard libraries.

[Back to the lab](../README_EN.md)

# What the Code Does

[繁體中文](code-map.md) · **English**

The `tools/` directory in this repository contains the full source code and automated test suites for three core engineering toolkits:
1. **[`tools/ai-memory-keeper/`](../tools/ai-memory-keeper/README_EN.md)** (standalone repository [`whitecarp/ai-memory-keeper`](https://github.com/whitecarp/ai-memory-keeper)): cross-agent context-compaction memory guardian, model-tier verifier, and Gemini constellation web console.
2. **[`tools/i-must-use-claude/`](../tools/i-must-use-claude/README_EN.md)** (standalone repository [`whitecarp/I-Must-Use-Claude`](https://github.com/whitecarp/I-Must-Use-Claude)): four-in-one native Windows desktop utility suite for everyday Claude workflows (reversible environment/DNS diagnostics, real-time bilingual companion translator, dual-timezone quota clock, and ≤29 MB context packager).
3. **[`tools/publishops-crosscast/`](../tools/publishops-crosscast/README_EN.md)** (standalone repository [`whitecarp/publishops-crosscast`](https://github.com/whitecarp/publishops-crosscast)): multi-platform video publishing console `vpub` (0.15.1 historical snapshot preserved in-repo; latest releases in the standalone repository).

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

## 3. PublishOps CrossCast: Upload & Read Recovery (`tools/publishops-crosscast/`)

- [process_lock.py](../tools/publishops-crosscast/src/video_publisher/process_lock.py): cross-process exclusion and marker recovery only after confirmed process exit.
- [browser_session.py](../tools/publishops-crosscast/src/video_publisher/browser_session.py): private pipe-based browser context reuse.
- [upload_resume.py](../tools/publishops-crosscast/src/video_publisher/upload_resume.py): persisted upload sessions and server-offset recovery.
- [full_read.py](../tools/publishops-crosscast/src/video_publisher/full_read.py) & [full_read_browser.py](../tools/publishops-crosscast/src/video_publisher/full_read_browser.py): atomic API page checkpoints, full reply-tree reads, and official end-of-page verification.
- [collections.py](../tools/publishops-crosscast/src/video_publisher/collections.py) & [collections_browser.py](../tools/publishops-crosscast/src/video_publisher/collections_browser.py): account-bound IDs, durable request intents, no resubmission, and read-back verification.
- [read_scheduler.py](../tools/publishops-crosscast/src/video_publisher/read_scheduler.py) & [playlists.py](../tools/publishops-crosscast/src/video_publisher/playlists.py): bounded platform concurrency, independent checkpoints, and YouTube playlist CRUD.
- [work_catalog.py](../tools/publishops-crosscast/src/video_publisher/work_catalog.py) & [operations_runs.py](../tools/publishops-crosscast/src/video_publisher/operations_runs.py): catalogues, fresh ownership checks, account-scoped caches, and interruption recovery.

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

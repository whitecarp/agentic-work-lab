# What the code does

[繁體中文](code-map.md) · **English**

The project uses human-defined requirements, AI-assisted implementation, tool-based verification, and human review. Reproducibility and failure handling matter more than listing languages or line counts.

## CrossCast work adoption and collection

- [work_catalog.py](../tools/publishops-crosscast/src/video_publisher/work_catalog.py): catalogues, fresh ownership checks and adoption records.
- [work_discovery_browser.py](../tools/publishops-crosscast/src/video_publisher/work_discovery_browser.py): bounded official-page scrolling/pagination and management checks.
- [operations_runs.py](../tools/publishops-crosscast/src/video_publisher/operations_runs.py): account-scoped freshness caches, change lists, API page checkpoints and interruption recovery.
- [test_operations_011.py](../tools/publishops-crosscast/tests/test_operations_011.py): expiry, account switching, receipt conflicts, checkpoints, continuations and platform failure isolation.

## Runnable in this repository

| Code | Problem solved |
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

No npm install is required; the public examples use Node.js standard libraries.

## Code in the original production workspace

- **PowerShell collection wrapper `run-read.ps1`:** Standardised JSON capture, provenance logs, call intervals, and stop conditions.
- **Python one-shot publisher `publish-once.py`:** Locked media and metadata hashes, time-bounded preflight, exclusive attempt claims, and durable success receipts before post-processing.
- **Python video checks:** Format, decode, stereo, and transcription checks without equating ASR with human listening.
- **HTML/JavaScript scenes and timeline:** Timed scenes, captions, transitions, and baked audio to actual speech duration.

These items are supported by inspected source code or delivery records. The original project includes account-bound configuration and substantial media, so a complete source release is not claimed here. Public scripts are reproducible examples prepared for this portfolio and are distinguished from the original production code.

[Back to the lab](../README_EN.md)

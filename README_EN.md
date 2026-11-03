# Agentic Work Lab

[繁體中文](README.md) · **English**

### From everyday tasks to reusable workflows

Explore how AI supports research, content production and operations, then turn practical lessons into repeatable, verifiable workflows that others can use.

🎬 [Bilibili channel · UID 10706217](https://space.bilibili.com/10706217) · 📦 [Video downloads](https://github.com/whitecarp/agentic-work-lab/releases) · [Video catalogue](media/README_EN.md) · [Operations data](data/README_EN.md)

**2026-11-03 update:** *I Watched It For You* is archived, bringing the catalogue to **30 video versions**. New notes cover publication evidence across five platforms, one Instagram upload timing and CrossCast software progress. [Read the update](docs/updates/2026-11-03_EN.md).

**2026-11-02 update:** Episode 07 and three portrait cuts are archived, **29 video versions** in total. New notes cover layout adaptation and commit-scoped CrossCast checks. [Read the update](docs/updates/2026-11-02_EN.md).

**2026-11-01 update:** Four complete study exports uploaded and hash-verified, **25 video versions** in total; development/independent-validation methods and unresolved checks documented. [Read the update](docs/updates/2026-11-01_EN.md).

## PublishOps CrossCast

**Multi-platform video release operations console**, available through the `vpub` CLI. All five platforms have real publication records.

Bring video, platform-specific copy, production covers and receipts into one workflow. Local sessions are reused after initial sign-in, with user assistance when a platform requests verification. Uncertain submissions retain their original records so the work can be checked before deciding what happens next.

[Standalone repository and installation](https://github.com/whitecarp/publishops-crosscast/blob/main/README_EN.md) · [v0.16.0 release](https://github.com/whitecarp/publishops-crosscast/releases/tag/v0.16.0) · [Current work and limits](docs/updates/2026-11-03_EN.md)

v0.16.0 is public. Subsequent main-branch work shortens the capability list and improves first-run guidance and publication checks. At this snapshot, the two-command flow and concurrent publication still await delivery; local tests are not claimed as measured live speedups. The update distinguishes release assets from main.

The [0.15.1 code and guide](tools/publishops-crosscast/README_EN.md) inside this lab preserve a historical implementation and its validation. Use the standalone repository for a new installation.

**2026-10-30 update (uploads completed 1 November):** Transformation-study v3/v4 technical checks and the dated GPT04 publication record. [Research record](docs/updates/2026-10-30-evening_EN.md).

**2026-10-29 update:** two episode-six films archived, **21 downloadable versions** in total; Whale Girl 03 public status and Whale Girl 02 day-one metrics recorded. [What changed](docs/updates/2026-10-29-evening_EN.md).

## How AI supports everyday work

Start with concrete tasks: document where AI can help, where human judgment is needed and what the work produces. Merchant-development examples are application demos; video production and channel operations include records from actual use.

| Use in practice | Methods and outputs |
|---|---|
| Organise merchant research, prioritise leads and prepare outreach | [Merchant-development demo](docs/business-development_EN.md) |
| Choose AI tools and plugins for the task | [Tool selection and usage](docs/toolchain_EN.md) |
| Coordinate research, production and review | [GPT/DeepSeek workflow evidence](docs/model-workflow_EN.md) |
| Turn repeated steps into reusable code | [Code map](docs/code-map_EN.md) · [Runnable examples](scripts) |
| Compare operations data and check conclusions | [Data comparison and limits](docs/data-comparison_EN.md) |
| Preserve context for team handoffs | [Merchant brief](templates/merchant-brief_EN.md) · [Operations checklist](templates/operations-checklist_EN.md) |

**2026-10-28 snapshot:** 55 upload records, 69 de-identified operations events, 19 publicly downloadable video versions, four workflow scripts/modules and ten workflow tests. These counts exclude subsequent CrossCast code and tests. The merchant example uses fictional data; simulated results are not presented as real partnerships or measured team-efficiency gains.

I drove requirements, editorial direction, feedback and review, with AI assisting research, implementation and production. This lab records how tools fit into working processes, where they fail and how delivery improves. Code and documentation are explicitly AI-assisted.

### Three reproducible entry points

```sh
npm run demo     # Synthetic merchant prioritisation
npm run report   # Real-data summary
npm test         # Workflow checks
```

Use Node.js 20+. No external npm packages are required. See [validation.json](results/validation.json) for the existing workflow-test and local-media verification results.

## Research and production records

This is the public research and production archive behind the **AI Girls at Work** series. It covers character-driven shorts, speech-to-picture timing, editable video delivery and Bilibili channel operations. A human directs and reviews the work; AI assists with research, production and execution. Results include their evidence and limitations.

| Topic | Delivered | Read |
|---|---|---|
| Animation timing | Distinguish output FPS, exposure and spacing; define contact-action checks. | [Study](docs/animation-timing_EN.md) |
| Editable video | Align scenes, captions and audio to a speech clock; document editable delivery. | [Workflow](docs/editable-video-workflow_EN.md) |
| Automation verification | Verify an actual hourly scheduled run and completion with one active operator. | [Verification](docs/automation-verification_EN.md) |
| Channel research | Document historical human-led uploads and the current AI series with sampling definitions. | [Data notes](data/README_EN.md) |

These are production experiments and engineering findings, not controlled evidence of guaranteed channel growth. Different topics, video ages and traffic sources prevent a direct “human versus AI” verdict from lifetime views.

## Published work

Titles below are English summaries; original Bilibili titles remain in the datasets for verification.

| Date (UTC+8) | Work |
|---|---|
| 2026-10-29 | [Whale Girl Shift Log 03: Watching Big Fat Fish on Bilibili](https://www.bilibili.com/video/BV1Wcx76dErs) |
| 2026-10-28 | [Whale Girl Duty Log 02: Three tasks on the first working day](https://www.bilibili.com/video/BV1Wfx56JEih) |
| 2026-10-28 | [Whale Girl Duty Log 01: Covering the shift when ChatGPT runs out of tokens](https://www.bilibili.com/video/BV1usxV69EUQ) |
| 2026-10-27 | [GPT Duty Log 03: Choosing dinner turns into a form](https://www.bilibili.com/video/BV1UXxN6aECf) |
| 2026-10-26 | [GPT Duty Log 02: A song-title question receives another question](https://www.bilibili.com/video/BV1gxnU6GEuz) |
| 2026-10-25 | [GPT Duty Log 01: A reply to a comment from 2022](https://www.bilibili.com/video/BV1qjHV6sEuD) |

Videos not yet public on Bilibili and earlier experimental cuts are listed in the [catalogue](media/README_EN.md). A public GitHub download does not mean a Bilibili upload has been published; this repository does not change platform schedules.

[2026-10-28 evening update: three video versions, day-one analytics and frame-level repair](docs/updates/2026-10-28-evening_EN.md)

## Using the material

- [Research method](docs/research-method_EN.md): source → evidence → test → limits.
- [Research-note template](templates/research-note_EN.md): separate observations from inferences.
- [Delivery manifest](templates/delivery-manifest.json): record formats, hashes, review and publication status.
- [Operations checklist](templates/operations-checklist_EN.md): act on due work and verify before recording success.

Data is provided as CSV/JSON, with downloadable video files and SHA-256 checksums. Missing values remain empty rather than becoming zero. Sampling times and sources accompany each dataset.

## Scope

The archive contains research notes, videos, aggregate and per-video channel data and de-identified operations records. Credentials, private messages, viewer personal data and unrelated private documents are excluded. Third-party material appearing in videos remains with its respective rights holders; public access does not grant reuse rights to that material.

First edition: 2026-10-28. Homepage research updated: 2026-11-03; the historical CrossCast snapshot is preserved.

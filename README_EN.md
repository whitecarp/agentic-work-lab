# Agentic Work Lab

[繁體中文](README.md) · **English**

### From everyday tasks to reusable workflows

Explore how AI supports research, content production and operations, then turn practical lessons into repeatable, verifiable workflows that others can use.

🎬 [Bilibili channel · UID 10706217](https://space.bilibili.com/10706217) · 📦 [Video downloads](https://github.com/whitecarp/agentic-work-lab/releases) · [Video catalogue](media/README_EN.md) · [Operations data](data/README_EN.md)

## PublishOps CrossCast

**Multi-platform video release operations console**, available through the `vpub` CLI.

Prepare platform-specific descriptions, covers and publication receipts for one original video. Choose platforms, sign in once and let the CLI reuse its own local sessions. The conversation model prepares copy; the CLI checks assets and accounts, uploads, applies covers and records results. YouTube uses its official API; Douyin, Instagram and X use a dedicated background browser.

- Source-aware copy: Chinese content becomes Simplified Chinese on Bilibili/Douyin; other languages retain the original plus Chinese. Non-English content retains the original plus English on YouTube/Instagram/X.
- Douyin/Instagram share dedicated portrait artwork; the user confirmed readable text in both v3 phone previews. Other layouts still need checking.
- X publishing supports subscribed users whose accounts can actually access Media Studio. Non-subscribers have a 140-second, 512 MB upload limit and lack the Studio media-management and custom-thumbnail access required by this backend. [Upload limits](https://help.x.com/en/using-x/x-videos) · [Studio access](https://help.x.com/en/using-x/media-studio).
- Instagram retains the original video and aspect ratio with a separate upload cover. Its mobile editor can use the same portrait artwork as Douyin.
- Added cross-platform comment reads, single-language replies matching commenters, work metrics and Chinese/English operations reports; tested and pending scopes are documented.
- Added five-platform work listing, watchlists and read-only collection: comment candidates, comparable metric changes and per-run reports; paged Bilibili/YouTube thread replies.
- Production-cover fitting, YouTube playlist settings and Gmail-only email notifications, off until explicitly enabled.
- Added five-platform likes/unlikes and favorites, bookmarks or selected-playlist saves/removals; check state, submit once and verify independently, with repeated identical commands becoming no-ops.
- Added read-only browser batches, persistent comment history and handling marks; live-tested Bilibili/Instagram/X comment likes/unlikes and YouTube own-comment editing/deletion.
- Added Douyin comment like/unlike/own-comment deletion and single-recipient messaging for Douyin, Instagram and X, with durable receipts, fresh-session read-back and no pending-message resend.
- Added unified messages with Douyin @ notifications, Instagram tags/mentions, X mentions and three-platform conversation previews. YouTube viewer hold/approve and X hide/unhide each passed one live round trip; nonempty Instagram mentions still need a live fixture.
- Added existing-work adoption, freshness-based read reuse and interruption recovery; ownership evidence and progress are stored without reuploading.
- Added parallel platform reads, incremental progress, message resumption and YouTube playlist management; 10 reads across four platforms took 27.4 seconds versus 55.4 in one development comparison.
- Added Instagram collection rename and member changes, Douyin work-collection management and the PublishOps CrossCast Codex skill, with independently verified receipts. X named-folder membership remains pending.
- CrossCast now reuses collection browser sessions, recovers new YouTube uploads after interruption, and reads supported paginated lists with durable progress; [scope and live results](tools/publishops-crosscast/docs/VALIDATION_EN.md).
- **0.15.1 alpha, with 417 passing offline tests.** This release freezes feature scope and repairs interruption recovery, partial-read retention and Instagram editor timing. Existing validation and platform limits are documented separately.

[Install and use](tools/publishops-crosscast/README_EN.md) · [Feature scope](tools/publishops-crosscast/docs/FEATURES_EN.md) · [Validation](tools/publishops-crosscast/docs/VALIDATION_EN.md)

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

First edition: 2026-10-28. Homepage and CrossCast documentation updated: 2026-10-29.

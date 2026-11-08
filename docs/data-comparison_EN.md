# Data Comparison, Empirical Bihuo Studies, and Antigravity Operations Interpretation

[繁體中文](data-comparison.md) · **English**

Baseline source: the creator-centre snapshot collected on the evening of 2026-10-28. See the [CSV](../data/creator-video-snapshot.csv) and [JSON](../data/creator-video-snapshot.json). Recompute with `npm run report`; output is in [channel-summary.json](../results/channel-summary.json).

| Metric | Human-led history (2019–2026-10) | AI series baseline snapshot (2026-10-28) |
| --- | ---: | ---: |
| Upload records | 45 | 10 |
| Public | 45 | 5 |
| Scheduled | 0 | 5 |
| Sum of lifetime views | 1,839,794 | 666 |
| Median lifetime views | 15,407 | 183 |
| Publication range | 2019-10-08 – 2026-10-08 | 2026-10-25 – 2026-10-28 |

This baseline table describes the 2026-10-28 coverage snapshot, **not a model-performance leaderboard**. Historical uploads have years of exposure and different topics; at that baseline, the new series had only five public works.

---

## 1. 2026-11-08 Empirical Bihuo Audience-Pool Studies & 24/7 Antigravity Operations

While keeping the 2026-10-28 historical cohort baseline separate, [`production-update-2026-11-08.json`](../data/production-update-2026-11-08.json) and the [8 November update report](updates/2026-11-08_EN.md) document controlled single-video audience-pool experiments and 24/7 automated channel operations under our **`Antigravity + Claude + Codex` Golden Triangle Workflow**:

### 1. Controlled 4-Way Bihuo Audience-Pool A/B/C/D Test on a Single Video
Rather than comparing videos of different ages or topics, we ran a parallel `4 × ¥100` (`24h` · `VIEW`) audience-pool test on the same dress-up short [`BV1Eaxt6LEhb`](https://www.bilibili.com/video/BV1Eaxt6LEhb/) (which reached **`96,658` views, `14,131` likes, and `4,706` favorites** by `2026-11-08 00:21` BJT, with a `19.95%` 3-second bounce rate vs. the `28.56%` peer median):
- **Curated creator-audience pools (`h50` / `fresh25`) achieved `5.30%–5.68%` like+favorite (L+S) rates**, more than **3.5× higher** than system smart recommendation (`1.42%`) or broad animation interest tags (`1.76%`).
- Scaling the winning `h50` pool over a **`72h` window** (Order `497711549966336`) reduced CPM from `¥18.28 / 1k` down to **`¥12.74 / 1k`**, lifted the Bihuo-attributed L+S rate to **`8.03%`**, and lowered cost-per-follow to **`¥13.59`**.
- On the 161-second anime PV [`BV1sQ4E6eEhK`](https://www.bilibili.com/video/BV1sQ4E6eEhK/) (*AiGO!!!!! · Lost Compute*, `12,265` views on Day 1 with `293` favorites exceeding `245` likes), a `4 × ¥250` four-pool horse race confirmed that vertical AI character audiences (`Pool A`, **`5.58%`** L+S) and AI MV visual audiences (`Pool D`, **`5.10%`** L+S) outperformed broad anime-game audiences (`Pool B`, `3.24%`).

### 2. How Google (Antigravity / Gemini / Vertex AI) Supports Daily Channel Operations & Data
- **2-Hour Read-Only Telemetry Collection**: Antigravity periodically samples Bilibili Creator Center and Bihuo ad console metrics into `bihuo-readings.jsonl`, giving every ad-spend decision a continuous time-series baseline.
- **Hourly Comment-Section Engagement Patrols**: Automatically detects new viewer comments across published works and drafts context-aware, in-character replies to sustain community engagement.
- **Daily Multi-Part Serial Scheduling & Front-Loaded Interactive Overlays**: For *WhaleGirl Shift Simulator* ([`BV1YL4M6EEek`](https://www.bilibili.com/video/BV1YL4M6EEek/)), Antigravity appends one new ending part daily at `12:00` BJT, updates the description progress table, and automatically mounts four front-loaded interactive components (`0.5s` follow card, `6.0s` poll, `13.5s` rating, and `19.0s` jump link) at `12:30` BJT once moderation clears.
- **Cross-Agent Memory Synchronization**: Uses [`AI Memory Keeper`](../tools/ai-memory-keeper/README_EN.md) to keep operational metrics, definitions, and red lines synchronized across `Antigravity + Claude + Codex`.

---

## 2. How to Measure Practical AI Efficiency

Define the task and acceptance criteria before comparing completion time, rework, and usefulness on equivalent tasks. Record sample size, tool versions, and human intervention. Distinguish drafts, completed tasks, and usable outputs. More generated text does not establish time savings.

- **2026-10-28 afternoon snapshot**: 52 records preserved in the [snapshot archive](../data/snapshots/2026-10-28-afternoon/creator-video-snapshot.json).
- **2026-10-29 day-one observation**: [Whale Girl 02](../data/whale-log02-24h-2026-10-29.json) recorded 308 views, 24 likes, 5 favorites, 51 replies, and 36.63% completion.
- **2026-10-30 through 11-08 delivery records**: preserved separately in [`production-update-2026-10-30-evening.json`](../data/production-update-2026-10-30-evening.json), [`production-update-2026-11-01.json`](../data/production-update-2026-11-01.json), [`production-update-2026-11-02.json`](../data/production-update-2026-11-02.json), [`production-update-2026-11-03.json`](../data/production-update-2026-11-03.json), and [`production-update-2026-11-08.json`](../data/production-update-2026-11-08.json).

[Back to the lab](../README_EN.md)

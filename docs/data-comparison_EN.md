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

## 1. 2026-11-08 Empirical Bihuo Audience-Pool Studies, Lookalike Reverse-Engineering & 24/7 Antigravity Operations

While keeping the 2026-10-28 historical cohort baseline separate, [`production-update-2026-11-08.json`](../data/production-update-2026-11-08.json) and the [8 November update report](updates/2026-11-08_EN.md) document controlled single-video audience-pool experiments, lookalike audience reverse-engineering, and 24/7 automated channel operations (with channel followers crossing **`37,005`** as of `2026-11-08 21:06` BJT) under our **`Antigravity + Claude + Codex` Golden Triangle Workflow**:

### 1. Controlled 4-Way Bihuo Audience-Pool Test & Lookalike Reverse-Engineering
Rather than comparing videos of different ages or topics, we ran a parallel `4 × ¥100` (`24h` · `VIEW`) audience-pool test on the same dress-up short [`BV1Eaxt6LEhb`](https://www.bilibili.com/video/BV1Eaxt6LEhb/) (which crossed **100,000 views** to reach **`106,127` views, `15,509` likes, and `5,088` favorites** by `2026-11-08 21:06` BJT, with a `19.95%` 3-second bounce rate vs. the `28.56%` peer median):
- **Curated creator-audience pools (`h50` / `fresh25`) achieved `5.30%–5.68%` like+favorite (L+S) rates**, more than **3.5× higher** than system smart recommendation (`1.42%`) or broad animation interest tags (`1.76%`).
- Scaling the winning `h50` pool over a **`72h` window** (Order `497711549966336`) and settling early upon passing the 100k contest threshold (`¥226.78` spent, `¥273.22` refunded) delivered `16,174` Bihuo views at **`¥14.02 / 1k views`**, `1,119` likes + `165` favorites (**`7.94%`** L+S rate), and `14` follows (**`¥16.20 / fan`**), while the companion `¥100` interaction order (`497711604487680`) generated `2,673` likes + `664` favorites (`96.03%` L+S per play).
- On the 161-second anime PV [`BV1sQ4E6eEhK`](https://www.bilibili.com/video/BV1sQ4E6eEhK/) (*AiGO!!!!! · Lost Compute*, **`22,989` views, `649` likes (crossing 500 likes!), and `716` favorites** as of `2026-11-08 21:06` BJT), the completed `4 × ¥250` 24h four-pool horse race (`18,492` Bihuo plays at `¥54.08 / 1k`) confirmed that vertical AI character audiences (`Pool A`, **`5.96%`** L+S) and AI MV visual audiences (`Pool D`, **`5.55%`** L+S) outperformed broad anime-game audiences (`Pool B`, `3.39%`).
- **Stage-2 Evening Tests & Lookalike Reverse-Engineering**: In the evening `3 × ¥50` (`6h`) tests (`+3h` reading at `20:00` BJT), system smart recommendation (`P1b`, `VIEW · AUTO`) compressed CPM down to **`¥35.37 / 1k`** (`28%` cheaper than custom `ad50` `P1a` at `¥49.16 / 1k`), while custom `ad50` interaction order `I1` delivered **`88` likes + `92` favorites on `182` plays (`98.90%` L+S per play at `¥0.11 / interact`)**. Reverse-engineering `89` public follow lists among pre-`17:31` organic/view-order likers revealed that **45 of the Top 50 co-followed creators (`90%`) were missing from the hand-picked `ad50` pool**, establishing our dual-track formula: use `VIEW · AUTO` (`¥35.37 / 1k`) for low-cost incremental reach and `INTERACT` on the reverse-engineered 50-creator vertical pool (`98.9%` L+S) for social proof.

### 2. Google Cloud Official Traffic Support Study: Gemini Saver (`AI Memory Keeper`) 3-Video Matrix (`¥0` Self-Funded Bihuo)
Backed by **Google Cloud ("Google Global Innovation Track") official traffic support** with `¥0` self-funded Bihuo spend, our 3-video launch matrix for [`AI Memory Keeper`](../tools/ai-memory-keeper/README_EN.md) reached **`35,690` total views, `2,104` likes, `2,203` favorites (`4,307` total likes+favorites, favorites exceeding likes overall), `92` coins, `337` comments, `165` shares, and `129` danmaku** as of `2026-11-08 21:06` BJT:
- **Flagship Tool Demo [`BV1WQ4t6xEdT`](https://www.bilibili.com/video/BV1WQ4t6xEdT)**: **`24,943` views, `1,898` likes, `1,806` favorites (`3,704` L+S, `14.85%` L+S rate), `273` comments, `127` shares**;
- **Google Cloud Credits & Free API Key Tutorial [`BV1nw4h64Ebf`](https://www.bilibili.com/video/BV1nw4h64Ebf)**: **`5,954` views, `144` likes, `370` favorites (`2.57×` favorite-to-like ratio), `42` comments, `36` shares**;
- **Model-Verification Skit [`BV1BM4h6pEY7`](https://www.bilibili.com/video/BV1BM4h6pEY7)**: **`4,793` views, `62` likes, `27` favorites, `22` comments, `50` danmaku**.

### 3. How Google (Antigravity / Gemini / Vertex AI / Google Cloud) Supports Daily Channel Operations & Data
- **2-Hour Read-Only Telemetry Collection**: Antigravity periodically samples Bilibili Creator Center and Bihuo ad console metrics into `bihuo-readings.jsonl`, giving every ad-spend decision a continuous time-series baseline.
- **Hourly Comment-Section Engagement Patrols**: Automatically detects new viewer comments across published works and drafts context-aware, in-character replies to sustain community engagement.
- **Daily Multi-Part Serial Scheduling & Front-Loaded Interactive Overlays**: For *WhaleGirl Shift Simulator* ([`BV1YL4M6EEek`](https://www.bilibili.com/video/BV1YL4M6EEek/), now `P1–P7` live with `15,810` views and `753` likes), Antigravity appends one new ending part daily at `12:00` BJT, updates the description progress table, and automatically mounts four front-loaded interactive components (`0.5s` follow card, `6.0s` poll, `13.5s` rating, and `19.0s` jump link) at `12:30` BJT once moderation clears.
- **Single-Master (`16:9`) Adaptation & Cross-Agent Memory Synchronization**: Claude delivers a single `16:9` master per project while Antigravity adapts `9:16` vertical and overseas cuts and uses [`AI Memory Keeper`](../tools/ai-memory-keeper/README_EN.md) to keep operational metrics, definitions, and red lines synchronized across `Antigravity + Claude + Codex`.

---

## 2. How to Measure Practical AI Efficiency

Define the task and acceptance criteria before comparing completion time, rework, and usefulness on equivalent tasks. Record sample size, tool versions, and human intervention. Distinguish drafts, completed tasks, and usable outputs. More generated text does not establish time savings.

- **2026-10-28 afternoon snapshot**: 52 records preserved in the [snapshot archive](../data/snapshots/2026-10-28-afternoon/creator-video-snapshot.json).
- **2026-10-29 day-one observation**: [Whale Girl 02](../data/whale-log02-24h-2026-10-29.json) recorded 308 views, 24 likes, 5 favorites, 51 replies, and 36.63% completion.
- **2026-10-30 through 11-08 delivery records**: preserved separately in [`production-update-2026-10-30-evening.json`](../data/production-update-2026-10-30-evening.json), [`production-update-2026-11-01.json`](../data/production-update-2026-11-01.json), [`production-update-2026-11-02.json`](../data/production-update-2026-11-02.json), [`production-update-2026-11-03.json`](../data/production-update-2026-11-03.json), and [`production-update-2026-11-08.json`](../data/production-update-2026-11-08.json).

[Back to the lab](../README_EN.md)

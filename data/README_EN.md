# Channel and operations data

[繁體中文](README.md) · **English**

**2026-11-17:** Added [2026-11-17 evening incremental structured record](production-update-2026-11-17.json) and refreshed [public live metrics snapshot](metrics/current-metrics.json) (see [2026-11-17 update report](../docs/updates/2026-11-17_EN.md)). Earlier incremental records remain preserved at [production-update-2026-11-16.json](production-update-2026-11-16.json), [production-update-2026-11-15.json](production-update-2026-11-15.json), [production-update-2026-11-14.json](production-update-2026-11-14.json), [production-update-2026-11-09.json](production-update-2026-11-09.json), [production-update-2026-11-08.json](production-update-2026-11-08.json), and [production-update-2026-11-03.json](production-update-2026-11-03.json). The 2026-10-28 historical snapshots below retain their original scope and dates.

Account: [Bilibili channel · UID 10706217](https://space.bilibili.com/10706217)

## Coverage

The snapshot contains **46** public-list entries and **55** creator-centre entries across two pages: **45** historical human-led works and **10** current AI-series works. These endpoints expose different sets, so their records remain separate.

| Dataset | Contents |
| --- | --- |
| [Public-list CSV](public-video-snapshot.csv) / [JSON](public-video-snapshot.json) | Titles, times, views, comments, danmaku |
| [Creator-centre CSV](creator-video-snapshot.csv) / [JSON](creator-video-snapshot.json) | All retrieved upload states, schedules, views, and interactions |
| [Human-led history](human-led-history.csv) | Per-video historical data |
| [Current AI series](current-ai-series.csv) | Includes public and scheduled uploads |
| [Weekly analytics](channel-weekly.json) | Views, visitors, fan changes, devices, and yesterday's top videos |
| [Earlier AI snapshots](historical-ai-analytics.json) | Available completion, average watch, and interaction metrics |
| [Operations events](operations-events.json) | 69 de-identified event records |
| [Scheduled-run evidence](scheduled-run-evidence.json) | Actual automatic trigger and completion times |

New data: [episode 03 day-one observation](episode03-24h-2026-10-28.json) and [evening production/publication summary](production-update-2026-10-28-evening.json). The afternoon data remains in the [snapshot archive](snapshots/2026-10-28-afternoon/creator-video-snapshot.json).

## Interpretation

- Metrics are API snapshots, not independently audited traffic.
- sampled_at is UTC; platform times retain their offsets.
- For scheduled uploads, publish_time_raw must not be mistaken for an actual public-release time.
- Human-led is a cohort label, not a claim that no AI tools were used.
- Historical file_modified_at is provenance, not a guaranteed exact observation time.
- Weekly channel analytics include old and new uploads and cannot all be attributed to the AI series.
- Original titles and platform state text are preserved verbatim; documentation is Traditional Chinese and English.
- Missing values remain empty or null; platform-reported zero views on unpublished uploads remain zero.

The dataset excludes viewer lists, comment text, private messages, credentials, and raw client logs.

## 2026-10-29 incremental observations

[Whale Girl 02 day-one metrics](whale-log02-24h-2026-10-29.json) and [two episode-six delivery and schedule records](production-update-2026-10-29-evening.json) are added separately. The full-channel snapshot above remains dated 2026-10-28; this is not a new census of the channel.

## 2026-10-30 state update

[This record](production-update-2026-10-30-evening.json) adds two local experimental deliveries and GPT04 public state, without refreshing full-channel traffic or day-one metrics. Existing totals and human-led/AI-series populations retain their original sample date; partial records from another date are not merged into them.

[Back to the lab](../README_EN.md)


## 2026-11-01 delivery and validation update

The [new record](production-update-2026-11-01.json) covers four video attachments and commit-scoped engineering checks. No traffic sample was added; historical human-led/AI comparisons retain their original scope.

## 2026-11-02 production evidence

The [new record](production-update-2026-11-02.json) adds episode 07 and its portrait cuts, historical scheduling receipts and scoped CrossCast results. It adds no traffic sample and does not change the earlier human-led/AI-series population or sampling date.

## 2026-11-08 catch-up archive and Bihuo empirical study

The [new record](production-update-2026-11-08.json) documents 17 newly archived published video versions from 2026-11-04 to 2026-11-07 (AI Memory Keeper v0.3.0 bilingual series, *Aha Drama Tune*, *Four AI Sisters Fighting for the Mic*, *AiGO!!!!! · Lost Compute* long-form anime PV, and Shift Simulator anthology `BV1YL4M6EEek` `P1–P6`), checksums for completed cuts pending scheduled or daily release (`aigo-epilogue-20261108` and `P7–P19`), and empirical Bilibili Bihuo promotion A/B/C/D audience-pool tests (`h50` scaling down to `¥12.74 / 1k views`) as of `2026-11-08 00:21` BJT.

## 2026-11-09 public archive and audience-cohort A/B records

The [2026-11-09 record](production-update-2026-11-09.json) archives 3 newly published masters (`BV145Ws6VEg2`, `BV1YL4M6EEek?p=8`, `BV121W864EVe`), pre-registered SHA-256 digests for Episodes 3–5, and sanitized equal-weight 6h audience-cohort A/B experiment metrics on `BV1sQ4E6eEhK` (where the 21-UID `L2` liker-lookalike cohort led engagement density in both rounds; zero spend amounts are disclosed).

## 2026-11-14 catch-up & evening archive and live channel metrics update

The [new structured record](production-update-2026-11-14.json) and [live metrics snapshot](metrics/current-metrics.json) cover **20 newly archived published video versions** verified live (`state == 0`) through `2026-11-14 21:06` BJT (`19` catch-up cuts plus tonight's `20:02:08` BJT [`BV1mWs56JEFd`](https://www.bilibili.com/video/BV1mWs56JEFd) `news-20261114-gemini-4-hydrogen.mp4`, bringing the repository to **75 video versions · 5.26 GiB** across **11 primary Releases**), refreshed stats for **30 tracked standalone BVs** (**37,475** channel followers, **3,268,151** cumulative channel views, **222,048** cumulative likes; Google Cloud 3-video matrix at **39,468** views and `4,592` likes+favorites), expanded `BV1YL4M6EEek` coverage (`P1–P12` live and archived; future daily parts `P13–P19` and held series masters `EP07–EP13B` recorded as metadata only without uploading files), `30fps` vocal alignment logs across `EP06SP`, `EP08`, `EP11`, `EP12SP`, `EP13A`, and `EP13B`, the `tools/publishops-crosscast v0.16.0 alpha` sync, and the first `8` recorded items of the zero-paid-boost `12:00` vs `20:00` BJT *[AI News Flash]* slot observation (`1` excluded delayed baseline `BV1ToWL6XEvo` plus `7` on-time samples). No credentials, private messages, viewer lists, wallet figures, or order IDs are included.

## 2026-11-15 evening incremental archive and live channel metrics update

The [new structured record](production-update-2026-11-15.json) and refreshed [live metrics snapshot](metrics/current-metrics.json) document **5 newly archived live cuts** verified public (`state == 0`) through `2026-11-15 21:11` BJT ([`BV12YsE6fEcU`](https://www.bilibili.com/video/BV12YsE6fEcU), [`BV1YL4M6EEek/?p=13`](https://www.bilibili.com/video/BV1YL4M6EEek/?p=13), [`BV1NZsJ6aEgb`](https://www.bilibili.com/video/BV1NZsJ6aEgb) *Ep.7 Masked Assembly*, [`BV1FVsp6FENs`](https://www.bilibili.com/video/BV1FVsp6FENs), and tonight's `21:00:00` BJT pure code-drawn film [`BV1HPsn6tEyN`](https://www.bilibili.com/video/BV1HPsn6tEyN) *A Lifetime in 28,946 Squares*, bringing the archive to **80 video versions · 5.64 GiB** across **12 primary public Releases**), refreshed stats for **34 tracked standalone BVs** (**37,654** channel followers, **3,273,974** cumulative views, **222,620** cumulative likes; Google Cloud 3-video matrix at **39,825** views and `4,617` likes+favorites), updated `BV1YL4M6EEek` coverage (`P1–P13` live and archived; future `P14–P19`, held series masters `EP08–EP13B` incl. `EP12SP`, review-paused candidate master, and unscheduled news drafts kept metadata-only/unreleased with zero binary uploads), Pass-3 zero-hold word-locked sync across rebuilt `EP13A v2` / `EP13B v3`, the 14-step Claude API + Gemini HyperFrames `0.9.23` workflow behind `BV1HPsn6tEyN`, and `10` recorded *[AI News Flash]* slot observations (`1` excluded delayed baseline plus `9` on-time samples). No credentials, private messages, viewer lists, wallet figures, or order IDs are included.

## 2026-11-16 evening incremental archive and live channel metrics update

The [new structured record](production-update-2026-11-16.json) and refreshed [live metrics snapshot](metrics/current-metrics.json) document **5 newly archived live cuts** verified public (`code == 0`, `state == 0`) via read-only Bilibili APIs through `2026-11-16 21:11` BJT ([`BV1kQsw6kEx4`](https://www.bilibili.com/video/BV1kQsw6kEx4), [`BV1YL4M6EEek/?p=14`](https://www.bilibili.com/video/BV1YL4M6EEek/?p=14), [`BV1GUsc6dE9A`](https://www.bilibili.com/video/BV1GUsc6dE9A) *Ep.8 Compute Marionette*, [`BV1g5sA6oESL`](https://www.bilibili.com/video/BV1g5sA6oESL), and tonight's `21:00:00` BJT pure code-drawn film [`BV1Eqsc6PE92`](https://www.bilibili.com/video/BV1Eqsc6PE92) *Voyager 1 Approaches One Light-Day*, bringing the archive to **85 video versions · 6.22 GiB** across **13 primary public Releases**), refreshed stats across **38 tracked BVs** (`24` publicly visible standalone snapshots: **37,815** channel followers, Google Cloud 3-video matrix crossing **40,822** views and `4,695` likes+favorites; `14` owner-only entries retain prior snapshots and cumulative account-wide `upstat` totals remain `null`), updated `BV1YL4M6EEek` coverage (`P1–P14` live and archived; future `P15–P19`, held series masters `EP09–EP13B` incl. `EP12SP`, unreleased code-film candidates, and unscheduled news drafts kept strictly excluded), and `12` recorded *[AI News Flash]* slot observations (`1` excluded delayed baseline plus `11` on-time samples). No credentials, private messages, viewer lists, wallet figures, or order IDs are included.

## 2026-11-17 evening incremental archive and live channel metrics update

The [new structured record](production-update-2026-11-17.json) and refreshed [live metrics snapshot](metrics/current-metrics.json) document **3 newly archived live cuts** verified public (`code == 0`, `state == 0`) via `bili.exe` and public Bilibili APIs through `2026-11-17 21:11` BJT ([`BV1YL4M6EEek/?p=15`](https://www.bilibili.com/video/BV1YL4M6EEek/?p=15), [`BV1aJsz6FEBv`](https://www.bilibili.com/video/BV1aJsz6FEBv) *Ep.9 Unmasked*, and tonight's `21:00:00` BJT pure code-drawn film [`BV1gHsz6ZE1c`](https://www.bilibili.com/video/BV1gHsz6ZE1c) *Calligraphy Painting*, bringing the archive to **88 video versions · 6.76 GiB** across **14 primary public Releases**), refreshed stats across **40 tracked BVs** (`26` publicly visible standalone snapshots: **37,935** channel followers, **3,293,232** cumulative channel views, **224,068** cumulative likes, Google Cloud 3-video matrix reaching **41,866** views and `4,802` likes+favorites; `14` owner-only entries retain prior snapshots), updated `BV1YL4M6EEek` coverage (`P1–P15` live and archived; future `P16–P19`, held series masters `EP10–EP13B` incl. `EP12SP` with `EP13B v3r2` updated digest, unreleased code-film candidates, and unscheduled news drafts kept strictly excluded), and the *[AI News Flash]* shift away from fixed twice-daily quotas to quality-gated selection (`0` new news flashes today, with `BV1FVsp6FENs`, `EP07`, and `EP08` submitted to the free AI community collection form). No credentials, private messages, viewer lists, wallet figures, or order IDs are included.

[Back to the lab](../README_EN.md)

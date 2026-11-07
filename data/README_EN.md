# Channel and operations data

[繁體中文](README.md) · **English**

**2026-11-08:** Added [catch-up archive (2026-11-04 to 11-08), six-platform receipts, and empirical Bihuo audience-pool data](production-update-2026-11-08.json) (see [full update report](../docs/updates/2026-11-08_EN.md)). Earlier 2026-11-03 records remain at [production-update-2026-11-03.json](production-update-2026-11-03.json). The 2026-10-28 historical snapshots below retain their original scope and dates.

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

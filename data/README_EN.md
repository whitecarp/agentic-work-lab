# Channel and operations data

[繁體中文](README.md) · **English**

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

[Back to the lab](../README_EN.md)

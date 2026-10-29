# 2026-10-29: Episode-six films, day-one data and delivery improvements

[繁體中文](2026-10-29-evening.md) · **English**

This update adds two complete video versions, bringing the public catalogue to 21 versions. The already published CrossCast 0.3.0 code and its 86-test record remain unchanged. This run updates the media archive, data and production notes.

## Complete exports and schedules

| Work | Version and duration | Bilibili schedule (UTC+8) |
|---|---|---|
| GPT Duty Log 06: Step by step, getting DeepSeek to help with my work | v2, 591.47 seconds | 2026-10-31 12:00 |
| Whale Girl Shift Log 06: AI does the automation; I pretend to be human | v2, 184.20 seconds | 2026-11-02 12:00 |

Both operations receipts record platform review approval with publication pending. Earlier production receipts retain their awaiting-user-review status as history; platform approval does not establish full user acceptance. Publishing the archive on GitHub does not change the Bilibili schedules. [Downloads and hashes](../../media/README_EN.md) · [This release](https://github.com/whitecarp/agentic-work-lab/releases/tag/update-2026-10-29-evening)

Whale Girl Shift Log 03 became public on Bilibili at 19:30 on 29 October, with operations readback at 19:30:36 confirming public access. Its existing download file is unchanged and was not uploaded again. [State data](../../data/production-update-2026-10-29-evening.json)

## Reusable practices from revision work

- **Keep tutorial dialogue complete.** GPT06 v2 places 18 tutorial messages on a continuous chat page. Older messages scroll before new ones arrive, preserving the current bubble and reducing split terms and isolated trailing characters.
- **Attach callouts to stable footage.** Boxes and arrows for input fields, API settings and instance controls were repositioned; fixed callouts are removed during short moving transitions. Footage comes from sanitized source media; raw recordings are excluded from public delivery.
- **Preserve approved voice and wording.** Only three GPT06 narration clips were regenerated in this revision; 46 source WAVs were retained. Whale Girl 06 v2 reused existing voices, adjusted relative role levels and three caption timings, and round-tripped all 44 lines with punctuation intact.
- **State the verification boundary.** GPT06 contains 17,744 encoded frames and Whale Girl 06 contains 5,526. Delivery records include full decoding, audio comparisons and sampled-frame checks, without claiming continuous human viewing or listening. For the latter, explicit user acceptance is recorded only for a 47-second preview.

These methods can be reused in other tutorials and team handoffs. No controlled experiment quantifies time saved. [Delivery workflow](../editable-video-workflow_EN.md)

## Whale Girl 02 day-one observation

Sampled at 19:33 on 2026-10-29, 24 hours and three minutes after publication: 308 views, 24 likes, five favourites, 51 replies, 36.63% completion and 42 seconds of average watch time on a 117-second film.

Replies are not unique viewers. Different duration, topics and publication times mean a direct comparison with GPT03's day-one metrics cannot establish differences in model ability or operating efficiency. [Recorded metrics](../../data/whale-log02-24h-2026-10-29.json) · [Comparison and limits](../data-comparison_EN.md)

## Current CrossCast evidence

The 0.3.0 record published earlier today includes episode-two submission and readback on YouTube, Instagram and X. Douyin was submitted and a review card observed, but login expired after restart and final readback remains pending; this run did not reupload it. The 86 passing offline tests belong to that code update and were not rerun for this documentation change. [Full validation record](../../tools/publishops-crosscast/docs/VALIDATION_EN.md)

This run did not retrieve a new full-channel dataset. The 55-record snapshot and human-led/AI-series comparison retain their 28 October sampling date; partial new receipts are not merged into the old totals.

[Back to the lab](../../README_EN.md)

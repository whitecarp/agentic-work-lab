# Frame-level repair and verification

[繁體中文](temporal-quality.md) · **English**

Case: GPT Duty Log 05 · v3-antiflash · 2026-10-28

## Problem and fix

A full-opacity frame appearing before a fade, white flashes during panel changes, and a preceding shot leaking into an entry made the tutorial harder to follow. The revision sets explicit hidden initial states, aligns animation boundaries to frames, replaces the right-hand panel as a whole, and uses a four-frame clean-entry hold at one cut. The v2 audio master is retained so visual repairs do not change the selected voice track.

| Check | Recorded result |
| --- | --- |
| Delivery | 434.47 s · 1920 × 1080 · 30 fps · H.264/AAC |
| Scan coverage | 13,034 / 13,034 frames; analysis at 320 × 180 |
| Known defects | 11 before/after sequences reviewed and reported fixed |
| Targeted pulse candidates | 0 isolated ABA; 0 premature contrast peaks; 0 black pulses; 0 right-panel white pulses |
| Video entries | 12 entries reviewed; 72 entry frames compared with sources |
| Audio | Unchanged v2 master; 0 ms alignment offset; −15.97 LUFS, −2.21 dBTP |

One short excursion remained and matched a normal update in the source page. Seventeen intentional gaps during left-panel changes were retained. Classifying such events avoids treating all visual changes as defects. Technical checks and targeted review passed; this is not a claim of continuous human viewing/listening or a photosensitivity safety certification.

## A reusable repair method

Translate vague dissatisfaction into specific defects, locations, and acceptance criteria. Repair the affected part, preserve the rest, and compare with the same measures. The same discipline applies to research, documents and content production: retain sources, identify incorrect fields, verify corrections, and make unresolved claims visible to teammates.

Figures come from local delivery and temporal-QA receipts. See the public [production summary](../data/production-update-2026-10-28-evening.json) and [video manifest](../media/manifest.json).

[Back to the lab](../README_EN.md)

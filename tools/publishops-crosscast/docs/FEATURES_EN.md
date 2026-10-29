# Feature scope

[繁體中文](FEATURES.md) · **English**

**X publishing supports subscribed users whose accounts can actually access Media Studio.**

This requirement exists because non-subscribers are limited to **140-second, 512 MB** video uploads and cannot use the Media Studio media-management and custom-thumbnail workflow on which this tool depends. CrossCast uploads videos and sets production thumbnails through Media Studio, so its X backend currently supports subscription accounts with that access. Login or payment alone does not establish access; the CLI checks the library entry and managed account before selecting media. [Official upload limits](https://help.x.com/en/using-x/x-videos) · [Media Studio access](https://help.x.com/en/using-x/media-studio).

**0.6.0 alpha.** New manifests use CLI execution after first login; `method: native` is an explicit host-browser fallback. Bilibili continues to call [bili-cli](https://github.com/kukemc/bili-cli).

| Capability | Bilibili | YouTube | Douyin | X | Instagram |
|---|---|---|---|---|---|
| Login | Upstream CLI | Desktop OAuth | Dedicated official Chrome | Dedicated official Chrome | Dedicated official Chrome |
| Default publish | bili-cli | Official API | Background browser | Background Media Studio | Background browser |
| Cover | Upstream | Thumbnail API | Landscape + portrait | Custom Studio thumbnail | Separate cover; Original crop |
| Target metadata language | Simplified Chinese | English | Simplified Chinese | English | English |
| Work read-back | Upstream | API | Creator cards/detail IDs | Own profile/status | Own grid/Reel |
| Comments/replies | Upstream CLI | Official API | Public reads/Creator Center top-level replies | Visible conversation/Reply | Visible comments/Reply |
| Work metrics | Creator data | Basic statistics | Creator Center | Post metrics | Visible values; not full Insights |
| Operations report | Local receipts | Local receipts | Local receipts | Local receipts | Local receipts |
| Like / unlike | Upstream CLI | Rating API | Video controls | Post controls | Post toolbar |
| Save / remove | Selected favorite folder | Selected ordinary playlist | Default saved items | Private bookmarks | Default Saved |

Shared: exact-source plans, per-platform account checks, immutable upload snapshots, local receipts and no blind retry after final submission. Browser `works` is limited to the visible first page (up to ten Douyin cards). Review cards without detail IDs remain pending.

Real episode-two uploads and their development limitations are recorded in [validation](VALIDATION_EN.md). Background browser automation is not an official direct API or a speed guarantee. Platform changes, expired sessions and challenges can require intervention. Metadata writing/translation uses the host conversation model through request/apply; it does not translate the video itself.

When the source language differs, retain source plus translation; Chinese scripts become Simplified Chinese only on Bilibili/Douyin. Douyin custom chapters are integrated into CLI publication/verification. `vpub edit` handles YouTube metadata, Instagram captions, an available X Edit action and Douyin covers/chapters; native handoffs remain. The user confirmed the shared v3 portrait and visible footer in both supplied phone views; this is not all-device acceptance. Email supports Gmail only, remains off by default and offers plugin setup assistance only after an explicit enable request. Outlook, QQ, NetEase and SMTP are unsupported. Single-language replies and user-language reports are described in the [README](../README_EN.md#comments-work-metrics-and-operations-reports). TikTok, private messages/automatic likes, full-history exports and YouTube cards/clickable end screens are not implemented.

## 0.6.0 — Explicit likes and saves

All five platforms use `engage status/like/save/verify`; `--remove` reverses the selected action. Bilibili targets a favorite folder, YouTube an ordinary owned playlist, Douyin and Instagram their default saved items, and X private bookmarks. `engage collections` lists supported collections; creating collections, named bookmark folders and YouTube Watch Later are not implemented.

Read current state, submit once and independently verify. Repeated identical commands cause no new write. Account changes, unknown states and ambiguous controls stop execution; pending attempts are not resent. Chinese/English reports count verified operations, reversals and unresolved attempts from receipts, excluding tests. Installation and collection do not enable automatic engagement. [Commands and limits](../README_EN.md#likes-saves-and-reversals).

## 0.5.0 — Bounded operations collection

Five-platform work listing; Bilibili/YouTube pagination and reply-page reads; account-bound watchlists; `ops sync` with inbox candidates, comparable metric changes and per-run reports. Reads only, no scheduler or automatic engagement. [Commands and limits](../README_EN.md#multi-platform-collection-and-metric-changes).

## Historical changes

## 0.2.0 — Cross-platform content settings

| Capability | This release |
| --- | --- |
| Copywriting | Host-model request/apply, source-bound validation; no independent model API |
| Language | Bilibili/Douyin → Simplified Chinese; YouTube/Instagram/X → English; bilingual opt-in |
| Covers | Production artwork → platform canvases; no cropping; immutable snapshots and read-back per variant |
| Existing-work edits | Metadata/cover changes retain the original work ID; privacy reduction preserved after actual read-back |
| Playlists | YouTube native fields and optional API membership operations; Bilibili collections remain upstream |
| Email | Off by default; host Gmail/Outlook send-tool handoff, claims and durable results; no SMS |
| Chapters | Native handoff; Douyin chapters submitted and mobile visibility user-confirmed |

See README and current validation for the latest acceptance status; these supersede historical live-upload entries above.

## 0.2.2 — Drafts and explicit publication

Draft-only mode distinguishes preview from saved drafts. Explicit operator authorization can enable submission of the same draft without reuploading. X draft and status were tested; Instagram succeeded after reauthentication and a rejection/absence-checked retry; profile-cover cropping remains unresolved. See [validation](VALIDATION_EN.md).

## 0.2.3 — Deletion receipts and cover limits

Instagram now has explicit deletion handoff, acknowledgement/absence evidence and history-preserving same-video cover replacement. Deletion was exercised; replacement has not been shared. X custom thumbnails use subscription-gated Media Studio; this session hit library initialization failure, so thumbnail changes remain unverified. [Guide](DELETION_AND_COVERS_EN.md).

## 0.2.4 — Historical experiment: lead-in withdrawn

The former preparation command added a one-second production-cover lead to landscape, passed portrait/square through, and retained original ratio, inputs, hashes and rerun reuse. This experiment was withdrawn in 0.2.5 after mobile testing. X's landscape cover was saved/published through Studio UI, not a new standalone API. Earlier 0.2.3 entries are historical; consult current validation.

## 0.2.5 — Original video and shared portrait cover

Mobile testing supersedes the one-second lead-in experiment; that command is removed. Preparation preserves original video bytes, ratio and duration, with a separate web cover and shared Douyin portrait for the phone camera-roll picker. The handoff returns the identical image path/hash; the user saves and verifies mobile changes.

Related links: [README](../README_EN.md)

[Back to the lab](../../../README_EN.md)

# Feature scope

[繁體中文](FEATURES.md) · **English**

**0.3.0 alpha.** New manifests use CLI execution after first login; `method: native` is an explicit host-browser fallback. Bilibili continues to call [bili-cli](https://github.com/kukemc/bili-cli).

| Capability | Bilibili | YouTube | Douyin | X | Instagram |
|---|---|---|---|---|---|
| Login | Upstream CLI | Desktop OAuth | Dedicated official Chrome | Dedicated official Chrome | Dedicated official Chrome |
| Default publish | bili-cli | Official API | Background browser | Background Media Studio | Background browser |
| Cover | Upstream | Thumbnail API | Landscape + portrait | Custom Studio thumbnail | Separate cover; Original crop |
| Metadata | Simplified Chinese | English | Simplified Chinese | English | English |
| Work read-back | Upstream | API | Creator cards/detail IDs | Own profile/status | Own grid/Reel |
| Full archive/analytics/comments | Upstream features | Not unified | Not unified | Not unified | Not unified |

Shared: exact-source plans, per-platform account checks, immutable upload snapshots, local receipts and no blind retry after final submission. Browser `works` is limited to the visible first page (up to ten Douyin cards). Review cards without detail IDs remain pending.

Real episode-two uploads and their development limitations are recorded in [validation](VALIDATION_EN.md). Background browser automation is not an official direct API or a speed guarantee. Platform changes, expired sessions and challenges can require intervention. Metadata writing/translation uses the host conversation model through request/apply; it does not translate the video itself.

Chapters and advanced existing-work edits retain explicit native handoffs. Optional host Gmail/Outlook email is off by default. TikTok, unified interaction commands and YouTube cards/clickable end screens are not implemented.

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

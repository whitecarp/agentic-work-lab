# Feature mapping and implementation status

[繁體中文](FEATURES.md) · **English**

The reference is [bili-cli](https://github.com/kukemc/bili-cli): content understanding, work management, operational messages and authorized interaction. Plans below are not completed features.

| Capability | Bilibili | YouTube | Douyin | X (Twitter) | Instagram |
| --- | --- | --- | --- | --- | --- |
| Official-page login | Native handoff | Native handoff | Native handoff | Native handoff | Native handoff |
| Plan and dedup | Implemented | Implemented | Implemented | Implemented | Implemented |
| Native upload handoff | Implemented | Implemented | Implemented | Implemented | Implemented |
| Standalone uploader | bili-cli | Optional API | Experimental browser | Not implemented | Not implemented |
| Live upload acceptance | Existing work retained | One private native upload verified | One private work; chapter visibility user-confirmed | One status read back; full playback unverified | One reel read back; cover-grid crop unresolved |
| Cards and clickable end screens | Not applicable | Not implemented | Not applicable | Not applicable | Not applicable |
| Discovery | Existing bili | Planned | Planned | Planned | Planned |
| Content understanding | Existing bili | Planned, platform permitting | Planned, platform permitting | Planned, platform permitting | Planned, platform permitting |
| Comments and inbox | Existing bili | Platform-specific adaptation planned | Planned | Planned | Planned |
| Interaction and editing | Existing bili | Native metadata/cover edits | Native edits; review pending | Planned | Planned |
| Creator analytics | Existing bili | Planned | Planned | Planned | Planned |

**Native handoff requires agent-driven UI work; it is not a claim that CLI retrieval or upload has occurred.** Existing Bilibili capabilities use installed `bili`; this project does not claim to reimplement or validate every upstream command.

Adapt platform concepts rather than copying Bilibili coins/triples/danmaku everywhere. Instagram uses captions and account audience; YouTube has visibility, kids and synthetic-content controls; X uses a single post body, account audience and status ID; TikTok is suspended. Analytics must retain platform, sampling time and metric definitions rather than treating differently defined views as equivalent.

Next: accept original-video publication on signed-in accounts, integrate works/analytics reads, then add explicitly authorized comments/replies. Each needs real read-back, error handling, deduplication and platform-specific tests.

[X guide](X_GUIDE_EN.md) · [TikTok suspension and volunteer testing](TIKTOK_STATUS_EN.md)

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

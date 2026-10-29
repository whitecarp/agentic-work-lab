# Validation status

[繁體中文](VALIDATION.md) · **English**

Date: **2026-10-29**, PublishOps CrossCast **0.3.0 alpha**.

## Episode 02: independent backends

The operator authorized this exact test asset: **GPT Duty Log 02 | Asked for a Song, I Asked a Question First**. Source: 84.4 seconds, 1920×1080, SHA-256 `decb95dbb5ebaac47aa41a9d578c55e17e027d565b2610652ca435277dd70991`. All uploads used the same original bytes. Bilibili was not reuploaded and existing schedules were retained.

| Platform | Observed result |
|---|---|
| YouTube | [API-uploaded episode](https://www.youtube.com/watch?v=zuLz9rEBcj0). Exact English title, public visibility, category 1 (Film & Animation) and processing `succeeded` read back. Production-thumbnail API write succeeded. |
| X | [Studio post](https://x.com/White_Carp2000/status/2115749753178992679). Dedicated session, one media upload, saved production thumbnail and exact English post text read back. Resumed the same Studio asset after selector fixes; no duplicate post. Text AI disclosure is present; native AI badge was not observed. |
| Instagram | [Reel](https://www.instagram.com/reel/DfExzPeiC_i/). Original crop and unchanged video, separate production cover, full English caption and native AI Content label read back. Grid/mobile cropping is not guaranteed. |
| Douyin | One episode-two work submitted with the exact Simplified Chinese title/caption, native AI declaration, public setting and both cover variants. Creator Center displayed the matching 1:24 work under platform review. This is submission evidence, not anonymous playback acceptance. |

X and Instagram passed a fresh CLI read-back after closing their test browsers. Douyin initially reused the dedicated session, but the post-upload restart returned to the official login page; stable session reuse remains an open acceptance item. Its submitted work was retained, and no duplicate was uploaded. YouTube used the public CLI API route. Browser backend development runs included selector repairs while preserving the existing upload; Instagram and Douyin were exercised through a persistent local development harness. These are real publications, but **not a clean-machine, uninterrupted end-to-end acceptance run**. No full continuous playback or cross-OS installation acceptance was performed.

The production cover is the correct episode-two artwork. Douyin accepted the paired contain-and-pad images, but flagged the portrait's large border area as a quality issue. Dedicated portrait composition is preferable; automatic fitting does not guarantee a good thumbnail. Source hashes and UI acceptance do not prove pixel-identical remote assets.

**86 offline tests pass.** New coverage includes single-submit persistence on timeout, profile locks, wrong-account rejection, changed assets, recovery without reupload, Instagram's account-prefixed work links, portrait-only artwork handling and verified-receipt persistence. Tests do not post to platforms or send email.

Unfinished scope: direct X/Instagram official APIs, unified analytics/comments/inbox, YouTube cards/clickable end screens, live API playlist writes and fully unattended multi-platform acceptance. Advanced chapters and edits remain explicit native handoffs. TikTok stays suspended. Private profiles, tokens, raw page captures and receipts are excluded from GitHub.

## 0.2.5 — Historical status

**2026-10-29 live YouTube API connection:** Desktop OAuth completed and `vpub auth status youtube --method api` returned the intended channel. An API read confirmed the existing test video's public visibility and processed upload status. Missing PySocks caused `httplib2` to ignore the configured proxy and time out; installing `PySocks==1.7.1` restored connectivity. It is now an installation dependency and a `doctor` check. This session made read-only calls, with no new upload or platform edit. Live API upload, thumbnail writes and playlist writes remain unverified.

The operator supplied two phone recordings and reported that the one-second lead did not solve the need, while camera-roll portrait covers worked. Sampled frames show cover editing and return to the work; this is not agent-operated phone control or full-playback acceptance. Lead-in functionality is removed. Original-video/shared-artwork handoff is implemented; this rule update did not republish the test work.

72 tests pass. Four current Instagram tests check unchanged video SHA-256, exact Douyin portrait reuse, scoped/idempotent manifests, overwrite guards and truthful missing-artwork state. The 0.2.4 FFmpeg test is historical, not an active publishing feature.

## Earlier live-test records

- **72 offline tests passed:** source/plan integrity, media snapshots, duplicate suppression, durable IDs, native-account checks, language routing, immutable privacy, optional email with single claim and uncertain-send suppression, cover fitting with no cropping, per-platform cover evidence, and playlist membership/deduplication. Added draft receipts, no-publication guards, explicit same-draft authorization, scoped CLI initialization and rejection-only recovery with fresh absence evidence. Tests use synthetic media and mocked services, including original-video/shared-cover handoff tests; they do not create live posts or mail.
- **YouTube:** One original native private upload was verified. The latest Simplified Chinese/English description without external links was saved and read back. The dedicated cover and private GPT Duty Log playlist were saved and read back in Studio. Category changed from People & Blogs to Film & Animation; the saved selection and disabled Save button were observed.
- **Category fix:** Explicit plans, host selection, in-place revision and receipt matching are offline-tested; API assignability and mismatch handling use mocks.
- **Douyin:** The same work stays private. Management now shows the revised Simplified Chinese caption without URLs and the dedicated portrait cover. Landscape cover was not freshly rechecked in this round. Four chapters were submitted. Initial read-back reported review; the user subsequently confirmed mobile chapter visibility.
- **Bilibili:** Existing upstream CLI/work retained; no reupload or operations takeover. Historical stop-file state is not presented as current.
- **Instagram initial publication (historical):** Original-ratio video, production cover, English caption and AI label were published and read back. Initial Share failed; after user reauthentication and a fresh same-account absence check, one retry succeeded, retaining failure/recovery history. The AI Content badge, exact caption and video scene were visible. The selected landscape cover is present, but profile-grid cropping cuts some text; grid fitting remains unresolved.
- **X:** Real upload reached Ready and survived draft save/reopen. After explicit authorization it was submitted once; the original status showed the matching author, exact English text, Made with AI and a 1:19 embedded video. Full playback and anonymous access were not tested. No deletion; custom thumbnails remain unimplemented. [Procedure](DRAFT_TESTING_EN.md).
- **Email:** Handoff, deduplication and result classification have offline tests; no test email was sent and delivery is unverified. A send tool must actually be available at execution.
- **API playlists:** Scope checks, list-before-insert and uncertain-write suppression have mock tests, not live API acceptance.
- **Chapters:** The hover menu entry was exercised. Four starts were checked before submission; the user confirmed mobile visibility; full post-review list inspection by the agent remains pending. [Procedure](CATEGORIES_AND_CHAPTERS_EN.md).
- **Not completed:** YouTube cards/clickable end screens; X/Instagram APIs; unified analytics/comments; clean-machine/cross-OS installation acceptance. TikTok remains suspended.

Private receipts, sign-in data, raw pages and test-account details stay local and are excluded from this code update.

## 0.2.3 — Historical status

- **Instagram deletion:** The operator explicitly authorized deletion of that test work. Confirmation returned to the profile; refresh showed 4 rather than 5 posts and no original work link. Both receipts are retained. Recovery and permanent purge were not tested.
- **Instagram replacement:** The original video was uploaded to an unpublished preview. A dedicated Douyin portrait cover was still cropped in the landscape preview. The operator is inspecting Instagram; replacement publication is incomplete.
- **X Media Studio:** After subscription Studio opened, but its library reported a setup failure after onboarding dismissal and reload. No custom thumbnail was saved; the existing X post was neither deleted nor reposted.
- **Added tests:** Tests reject wrong identities/IDs, missing deletion or absence evidence, premature/duplicate reposts and changed video/settings; the allowed path retains history and cover snapshots.

## 0.2.4 — Historical experiment

- **Instagram original-ratio replacement:** The original-ratio replacement was shared, then deleted by the operator; a refreshed profile confirmed absence. The new 1920×1080 cover-lead version is about 79.5 seconds; sampled frames show cover at 0.5 s and original footage at 1.2 s. Original crop, English caption and AI disclosure were selected before sharing; success and a new work link were observed. The exact 1327-character caption, native AI badge and cover thumbnail were read back; the profile grid still crops the landscape artwork. Mobile cover selection remains operator acceptance.
- **X Studio:** Direct library upload and persistent original 16:9 thumbnail succeeded. Newly authorized publication was read back with matching author, text, 1:19 video and cover; the earlier post remains. Text disclosure is present but the native AI badge was not observed. Full playback remains unverified.
- **New tests:** Tests cover portrait/square passthrough, Instagram-only output, missing artwork, overwrite/duration guards, changed covers, duplicate lead prevention, and real FFmpeg duration/audio/frame-colour/reuse checks.

[Back to the lab](../../../README_EN.md)

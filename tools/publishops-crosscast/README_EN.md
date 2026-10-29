# PublishOps CrossCast

[繁體中文](README.md) · **English**

**X publishing supports subscribed users whose accounts can actually access Media Studio.**

This requirement exists because non-subscribers are limited to **140-second, 512 MB** video uploads and cannot use the Media Studio media-management and custom-thumbnail workflow on which this tool depends. CrossCast uploads videos and sets production thumbnails through Media Studio, so its X backend currently supports subscription accounts with that access. Login or payment alone does not establish access; the CLI checks the library entry and managed account before selecting media. [Official upload limits](https://help.x.com/en/using-x/x-videos) · [Media Studio access](https://help.x.com/en/using-x/media-studio).

### Multi-platform video release operations console

**v0.6.0 alpha · CLI `vpub` · AI-assisted development**

Prepare platform-specific metadata, covers and publication receipts for one original video. Choose any subset of Bilibili, Douyin, YouTube, Instagram and X. Sign in once; the CLI reuses its own local sessions for later uploads.

Default routes: Bilibili uses bili-cli, YouTube uses its official API, and Douyin/Instagram/X use a dedicated persistent Chrome profile controlled by the CLI in the background. First login and official verification require the user. Explicit `method: native` retains host-browser handoff. This remains alpha; live development tests are documented separately from unattended acceptance. Comments, work metrics and reports now share CLI commands, with platform-specific validation below. TikTok is suspended; YouTube cards/clickable end screens and standalone X/Instagram APIs remain unimplemented.

[Features](docs/FEATURES_EN.md) · [Validation](docs/VALIDATION_EN.md) · [Agent handoff](DEEPSEEK_HANDOFF_EN.md) · [Draft testing](docs/DRAFT_TESTING_EN.md)

## Likes, saves and reversals

After first login, use the same CLI commands for an exact work. Bilibili reuses bili-cli and YouTube uses its official API. Douyin, Instagram and X reuse the CLI's dedicated background Chrome session without step-by-step host-browser operation.

| Platform | Like / unlike | What save / remove actually means |
|---|---|---|
| Bilibili | bili-cli | Selected favorite folder; a unique default is used if omitted. Removal preserves membership in other folders. |
| YouTube | Official video rating API | Selected ordinary playlist owned by the account; no Watch Later, Likes-list or legacy Favorites management. |
| Douyin | Exact video's Like control | Default favorite/save for that video. |
| Instagram | Post toolbar Like | Default Saved; no named collections or comment likes. |
| X | Exact post's Like | Private Bookmarks; not Lists or bookmark-folder management. |

```sh
vpub engage status instagram WORK_ID
vpub engage like instagram WORK_ID
vpub engage like instagram WORK_ID --remove
vpub engage save x STATUS_ID
vpub engage save x STATUS_ID --remove
vpub engage collections bilibili
vpub engage save bilibili BV_ID --collection FOLDER_ID
vpub engage collections youtube
vpub engage save youtube VIDEO_ID --collection PLAYLIST_ID
vpub engage save youtube VIDEO_ID --collection PLAYLIST_ID --remove
vpub engage verify RECEIPT_KEY
```

`status`, `collections` and `verify` are read-only. Choose folder/playlist IDs from `collections`; YouTube supports continuation through `--cursor`. Commands support `--out result.json`. `like` and `save` execute directly with optional `--dry-run`, without another per-action approval step.

Exact Bilibili folder membership uses a read-only helper in pinned `bili-agent==0.7.0`; writes still use its CLI. That internal interface needs revalidation after upstream upgrades. Missing membership information never triggers guessing or removal from other folders.

The CLI checks the account and current state first; an already matching state returns `NO_CHANGE`. It writes a durable receipt before one submission and confirms the result through an independent read. Only matching read-back produces `ACTION_VERIFIED`. For `ACTION_UNVERIFIED`, use read-only `verify`, retain the receipt and do not repeat or reverse the pending attempt. Login challenges and ambiguous controls stop the command. Account-state evidence is not organic growth; the [YouTube rating API](https://developers.google.com/youtube/v3/docs/videos/rate) also distinguishes account ratings from official like counts.

Reports distinguish likes, unlikes, saves, removals and pending attempts, deduplicated by receipt. Explicitly authorized tests can use `--test-id LABEL` to exclude traffic from operations results; the label does not automatically undo an action. Login, installation and `ops sync` do not automatically engage. Bulk interaction, coins, triple actions, reposts and creating collections are outside this release.

## Comments, work metrics and operations reports

Read a selected work's comments, then let the host conversation model interpret the context and compose a reply. **Reply only in the comment author's language, using one language.** Publication metadata's bilingual policy and the report/account language do not apply. The host identifies languages such as English or French and supplies `--language`; the CLI's script hint is not a complete language detector and does not guess unknown cases.

```sh
vpub comments list youtube VIDEO_ID --limit 20 --out comments.json
vpub comments reply youtube VIDEO_ID COMMENT_ID --file reply.txt --language en
vpub comments verify-reply REPLY_KEY
vpub stats youtube VIDEO_ID --out stats.json
vpub preferences --language en
vpub report --language auto --out report.json
```

Replace `youtube` with `bilibili`, `douyin`, `instagram` or `x` for the same command interface. Browser operations currently require an existing CrossCast work receipt to verify the account; ownership is never guessed. `comments reply` sends directly without per-comment approval; `--dry-run` is optional. The host model supplies the copy; the CLI does not chat, translate or append a signature itself.

Before sending, the CLI checks the current comment and account, persists its intent and submits once. An uncertain result uses read-only `verify-reply`, never a blind resend. Ordinary replies reject own comments and threads known to contain an owner reply. Reads cover only the returned page or visible list; `complete: false` is not a full-history claim. YouTube and Bilibili return `next_cursor`; browser adapters do not invent pagination tokens.

| Platform | Comments and replies | Work metrics |
|---|---|---|
| Bilibili | Upstream bili-cli; thread and reply IDs retained | Views, likes, comments, favorites and other supplied fields; creator completion/watch time when available |
| YouTube | Official Data API; work, thread, author, text and reply ID checks | Views, likes and comments; completion/watch duration do not yet use Analytics |
| Douyin | Public work reads; Creator Center replies to top-level comments; fresh reply and parent read back | Creator Center's displayed views, likes, comments, shares, favorites and completion metrics |
| Instagram | Visible comments and Reply; fresh comment ID, author, text and action receipts | Values actually displayed on the work; full Insights is not integrated |
| X | Visible conversation and Reply; fresh status ID, author, text and action receipts | Post views, replies, reposts and likes; post views are not video plays |

Douyin replies use native Creator Center controls and observe requests made by that page to bind the comment ID; this is not a standalone official API. A missing target, ambiguous identical name/text, or nested-reply target stops before sending and retains the receipt. Submission acknowledgement is separate from fresh visibility.

Metrics retain source, observation time and units. Unavailable values stay `null`, not zero. Browser verification records the targeted Reply action and visible result; it does not claim an official API parent relationship. See [validation boundaries](docs/VALIDATION_EN.md).

Reports follow the **user's** language, defaulting to Chinese or English. The host sets `preferences --language zh|en` from the conversation, or uses `report --language zh|en` for one run; an unset preference falls back to the system locale. The output is a natural paragraph based on local execution receipts, counting only read-back replies as completed. Own comments and self-tests are excluded from audience interactions. `--since` accepts a timezone-aware start, and `--notes` accepts sourced findings to follow up. Likes, mentions, private messages and research outcomes are not invented. Reports do not send email automatically.

Only when the user explicitly requests a live test:

```sh
vpub comments self-test youtube VIDEO_ID --test-id hello-test-01
```

This posts one “你好” comment and one “你好” reply on your own work. Reusing the test ID retains the attempt; an uncertain result is not resent. It never runs during login/setup and never deletes comments automatically.

## Multi-platform collection and metric changes

Choose works to watch, then use one command to collect comments, work metrics and a report for that run. This is an on-demand, read-only collection: it creates no schedule and sends no replies, likes or email.

```sh
vpub works youtube --limit 20
vpub works bilibili --limit 20
vpub works instagram --limit 10
vpub ops watch add youtube VIDEO_ID
vpub ops watch add douyin WORK_ID
vpub ops watch list
vpub ops sync --pages 2 --limit 20 --language en --out round.json
vpub ops sync --platforms youtube,douyin --out selected-round.json
vpub ops watch remove youtube VIDEO_ID
```

`works` supports all five platforms. YouTube reads the authorized channel uploads playlist; Bilibili lists published submissions and leaves scheduled/reviewing work alone. These two return a `--cursor` for pagination. Douyin, Instagram and X still expose only the visible first page, not a full account archive. Browser-platform watch targets require an existing CrossCast publication receipt; API platforms check work ownership directly.

`ops sync` processes only the watchlist, up to `--pages` per work and `--limit` top-level comments per page; API reply previews can add entries. The first read establishes a baseline. Later reads deduplicate by comment ID and exclude own comments from audience interactions. `inbox` contains observed candidates, not private messages or a full account inbox. `needs_thread_check` means reply status is uncertain and context must be read. Candidates never trigger automatic sending.

Metrics retain historical events and the latest snapshot. `changes.delta` requires exact numbers from the same account, work, source, label and unit. Missing, rounded or changed-source values keep a null delta; percentage changes use percentage points and negative changes are retained. View/play definitions are not added across platforms. A partial failure preserves successful work and returns `PARTIAL` with exit code 4. Challenges or account mismatches are not retried through another route. Each run saves a local receipt and reports only its own events.

### Read the next page of a comment thread

```sh
vpub comments replies youtube VIDEO_ID ROOT_COMMENT_ID --limit 20 --out replies.json
vpub comments replies youtube VIDEO_ID ROOT_COMMENT_ID --cursor NEXT_CURSOR
vpub comments replies bilibili BV_ID ROOT_COMMENT_ID --limit 20
```

These two platforms support paged replies to a top-level comment with work and parent checks. `next_cursor` indicates another page; `complete` describes only the requested scope. This does not turn Instagram, X or Douyin visible comments into a complete historical export or bypass reply deduplication.

## Install and start

Requires Python 3.10–3.12 and Git for Git-URL installation. The [Windows installer](install.ps1) finds compatible Python, creates an isolated environment and installs dependencies; missing Python must be prepared by the agent. The background browser routes require Google Chrome. Run `vpub setup --install-browser` if it is missing. YouTube API mode requires a Desktop OAuth client JSON; the other three routes do not need platform developer keys. A standalone model API key is not required.

```sh
pip install "git+https://github.com/whitecarp/agentic-work-lab.git#subdirectory=tools/publishops-crosscast"
vpub doctor
vpub onboard --platforms bilibili,douyin,youtube
vpub init --video final.mp4 --language zh-Hans --out publish.json --platforms bilibili,douyin,youtube
```

Choose platforms and automatic publication versus editorial review once. Gmail-only email stays off unless explicitly requested. Reuse authorization without repeated editorial approvals. `--confirm` is a plan-integrity hash. The CLI persists email settings; the host remembers editorial preferences.

For upgrades, uninstall the old `whitecarp-video-publisher` package first. The `vpub` command, `video_publisher` Python module and runtime/sign-in/job storage are retained; do not delete user data.

### YouTube API sign-in

Enable YouTube Data API v3 in Google Cloud, create a Desktop app OAuth client and download its JSON. An API key alone cannot authorize uploads. If the project is in Testing mode, add your sign-in account under Google Auth Platform → Audience → Test users to avoid `403 access_denied`.

```sh
vpub auth login youtube --method api --client-json /path/to/client.json
vpub auth status youtube --method api
```

Open the printed official Google authorization link on the same computer running the CLI, choose your channel and grant access. The callback waits for five minutes. Keep client JSON and tokens local; never commit them to GitHub. Set the YouTube manifest's `method` to `api` to use this backend; signing in does not change existing manifests. First connection requests upload, read and video/playlist management permissions together for the user to approve in Google. Old upload/read-only tokens need one reauthorization; `--manage-playlists` remains a compatibility flag. Permission does not authorize arbitrary deletion.

If authorization completes but API reads time out, run `vpub doctor`. Dependencies include [PySocks](https://pypi.org/project/PySocks/): without it, the Google client's `httplib2` transport can ignore a configured proxy. In an older environment, run `python -m pip install PySocks==1.7.1` with the same Python environment and retry the status query; do not reupload the video.

## Login once, publish through the CLI

```sh
vpub auth login x
vpub auth login instagram
vpub auth login douyin
vpub auth status all
```

Each first login opens CrossCast's own official Chrome window. Choose QR, password or another option offered by the site and complete any SMS/identity check yourself. Later commands run headlessly with the same local profile. This profile is separate from Codex's browser; no host cookies are exported. Sessions may expire or require verification again.

`onboard` can start only the selected platforms. Existing manifests are not migrated automatically: set `method: browser` for Douyin/Instagram/X and `method: api` for YouTube; `init` now supplies those defaults. `method: native` is the explicit host-agent fallback.

X/Instagram passed a fresh CLI read-back after restart. After reauthentication, several independent Douyin commands reused the session to read and edit episode two; long-term session stability remains unverified. See [validation](docs/VALIDATION_EN.md).

## Copywriting and language

Routing follows the video's original content language, independently of account locale or GitHub documentation:

| Platform | Content language | Description |
|---|---|---|
| Bilibili, Douyin | Chinese, either script | Simplified Chinese only |
| Bilibili, Douyin | Any other language | Original language + Simplified Chinese |
| YouTube, Instagram, X | English | English only |
| YouTube, Instagram, X | Any other language | Original language + English |

Keep the source-language section first, then the target-language section. Matching-language content is not duplicated. Preserve voice, episode identity and events; shorten both sections together to fit limits. This does not translate audio, subtitles or text baked into artwork.

```sh
vpub metadata request publish.json --source script.txt --out request.json
# The host conversation model checks the script and writes response.json.
vpub metadata apply publish.json --request request.json --response response.json --out localized.json
```

Bilingual responses include two `description_parts`; `description` must join them with a blank line. The CLI validates order, mode and length. Legacy `--bilingual` is a compatibility assertion and is no longer needed. If the language is unknown, the host inspects the script/subtitles and sets `content_language` first; documentation language is not evidence.

`bili-cli` submits supplied descriptions; the host conversation model writes them. CrossCast adds structured requests and validation without reading hidden history or embedding a translation model. External links are off by default; irrelevant BV references are omitted while context and plain-text third-party credits/licenses remain. Schema checks do not prove translation quality.

## Production covers and original video

```sh
vpub covers prepare localized.json --source production/cover.jpg --portrait production/portrait.jpg --out-dir covers-v1 --out ready.json
```

Landscape roles prefer platform-specific artwork, then the production cover, using contain fitting. Douyin and Instagram portrait roles require one dedicated composition: explicit `--portrait` first, otherwise the manifest's Douyin/Instagram portrait. Both outputs preserve identical image bytes and SHA-256. Landscape padding is no longer a portrait fallback. Missing artwork prompts preparation rather than replacing originals.

| Platform | Working canvas and application |
|---|---|
| Bilibili | 1600×1000; upstream CLI or native browser |
| YouTube | 1920×1080; API or Studio, possibly requiring official verification |
| Douyin | 1200×900 landscape + shared original portrait; CLI uploads both, and the portrait editor crops to 3:4 |
| Instagram | 1920×1080 web artwork + the same shared original portrait for the phone picker; original video ratio |
| X | Landscape artwork uploaded by the CLI through Media Studio; Studio access required |

These are working presets, not permanent platform specifications. Complete local artwork does not prevent platform cropping: inspect platform previews and profile thumbnails. Source SHA-256 is not a remote-byte hash of the platform's compressed file.

### Instagram publishing and covers

**Instagram retains original footage and Original crop, with a separate upload cover.** On a phone, Edit cover → Add from camera roll can select the same dedicated portrait used for Douyin. The 0.2.4 one-second lead-in experiment did not solve this requirement and was removed in 0.2.5. Preparation no longer modifies/transcodes video or requires FFmpeg.

```sh
vpub instagram prepare ready.json --out instagram-ready.json
# Add --mobile-cover production/portrait.jpg if the manifest lacks one.
```

On the phone, open Edit cover, choose Add from camera roll and select the shared portrait. Inspect both the cover and profile-grid previews before saving; different surfaces may crop different regions.

The handoff prefers an explicit `--mobile-cover`, then `covers.douyin.portrait`, then `covers.instagram.portrait`. It records the identical image path and hash; the user performs the mobile step. For future shared artwork, generally consider a 9:16 composition with titles, faces and essential text in a central safe area that also survives 3:4 crops. Avoid edges and UI overlays and inspect both platforms. This is a design guideline, not a guarantee of identical cropping.

**Episode-two shared cover accepted.** On 2026-10-30, the user applied v3 on Douyin and Instagram and confirmed both phone profile-grid screenshots. The footer now sits inside the visible area. Acceptance covers those two supplied views, not every device or display surface. Future artwork should retain shared safe areas and check text size, contrast, composition and playback-count overlays.

Use the production original in new plans, not retained 0.2.4 lead-in experiments. A CLI update does not replace, delete or repost existing publications.

[X Media Studio workflow](docs/X_GUIDE_EN.md) · [Deletion and cover replacement](docs/DELETION_AND_COVERS_EN.md)

For supported platforms, use the following handoff to edit an existing work, checking its original account, work ID and saved result. Instagram mobile covers and X Studio use the dedicated workflows above.

```sh
vpub browser cover RECEIPT_KEY --file landscape.jpg --portrait portrait.jpg --identity OBSERVED_ACCOUNT
```

## Playlists, categories and chapters

YouTube accepts native `playlist_titles` or `playlist_ids`. The API checks membership before inserting and never blindly retries uncertain writes. The required management permission is included in first-time OAuth. Studio creation of a private list and selection for the existing work were verified; the API route remains mock-tested. Bilibili collections remain upstream functionality rather than a unified collection-management command.

New YouTube plans require an explicit `category_id`, selected from content and official choices rather than defaulting to People & Blogs. The host model can supply it via `metadata request/apply`; explicit selections are preserved.

Douyin `method: browser` accepts measured `duration_seconds` and `chapters` in the publication plan. Once a durable published work ID is available, the CLI applies custom chapters, saves and reads each pair back; platform-generated suggestions are not treated as completion. Review delays leave a pending step, completed by later `vpub verify`/`resume` without reuploading. Native Bilibili/Douyin/YouTube handoffs remain available. Starts must come from the same final video, begin at zero and increase within its duration. Translate titles without altering timing.

```json
{
  "duration_seconds": 78.506667,
  "chapters": [
    {"start_seconds": 0, "title": "A late reply"},
    {"start_seconds": 19, "title": "Memories in old comments"},
    {"start_seconds": 46, "title": "Speaking in my own role"},
    {"start_seconds": 57, "title": "Read before replying"}
  ]
}
```

These four timestamps are a historical episode-one example with user-confirmed mobile visibility at the time. The six custom episode-two chapters have now also been saved and fully read back through the CLI. See the [guide](docs/CATEGORIES_AND_CHAPTERS_EN.md) for the chapter entry, 12-character title limit and review receipts. Description timestamps do not prove native chapters are active; missing access or review stays pending.

## Edit an existing publication

```sh
vpub edit RECEIPT_KEY correction.json --dry-run
vpub edit RECEIPT_KEY correction.json
# Read back an uncertain save without saving again:
vpub edit RECEIPT_KEY correction.json --verify-only
```

Instagram caption edits read the original saved text from the existing work’s editor. Page translations, comments and old success receipts are not current-caption evidence. Matching text causes no save; a mismatch is corrected on the same work, then the editor is reopened for read-back. `vpub edit KEY PATCH --verify-only` checks without saving; `EDIT_MISMATCH` means the actual text differs and permits an explicit correction after inspection.

JSON requires `expected_identity` and exactly one group: full `title`/`description` with language fields, a Douyin `portrait` file, or `chapters`/`duration_seconds`. YouTube uses its API; Instagram/X/Douyin use the CLI-owned session. Video bytes and visibility are not changed. Receipts retain the work ID and block automatic retries after uncertain saves. Cover read-back provides a local screenshot for visual inspection; source hashes are not remote pixel comparisons.

X editing is attempted only when the original post exposes Edit. Missing access never triggers deletion, reposting or replies. The operator chose to keep episode two unchanged; the next post will follow the new language policy.

## Publication and receipts

```sh
vpub plan ready.json
vpub publish ready.json --dry-run
vpub publish ready.json --confirm CURRENT_PLAN_HASH
vpub verify RECEIPT_KEY
vpub works instagram
# X only: resume the existing Studio asset before final submission.
vpub resume RECEIPT_KEY
vpub receipts
```

Browser receipts persist the account, exact text, source hashes and final-submit flag. An uncertain submit is never automatically clicked again; `verify` can find a unique new matching work. After submission, `resume` verifies the original work and can complete its planned, unattempted Douyin chapter step; before submission it can continue the saved X Studio asset. Instagram/Douyin interrupted uploads require inspection of the existing draft. `works` lists the visible first page (up to ten Douyin cards), not a complete archive. Advanced native edits use `browser request/begin/record`.

Templates default to public Bilibili/Douyin, private YouTube and account audience for Instagram/X; the host must apply user intent. Signing in never adds publication targets. Preparation, verification waits, platform review and public success are distinct; UI evidence and API verification have separate states.

## Optional email notifications: Gmail only

**Off by default; enable only when explicitly requested by the user.** Ordinary installation/login does not ask for an address or turn on email. This feature supports the host agent's Gmail plugin only: no Outlook, QQ, NetEase, SMTP or SMS.

Only after an enable request does the host ask for the user's own Gmail address. It checks the address and plugin, helps install a missing plugin or authorize an unconnected one, and allows cancellation with email left off. Before sending, the connected account must exactly match the chosen address. **Send from that account to itself only.** External service sign-in aliases are not treated as Gmail mailboxes.

```sh
# Run only when email is wanted; ordinary installation does not configure it
vpub notify configure --to you@gmail.com
# Disable at any time
vpub notify configure --provider off
```

The following is performed by the host agent; ordinary users do not edit JSON:

```sh
vpub notify inspect --email you@gmail.com --inventory host-mail-inventory.json
# A one-off test does not enable ongoing alerts; the same test-id never resends
vpub notify test --email you@gmail.com --test-id setup-test-001 --inventory host-mail-inventory.json
vpub notify claim RETURNED_ALERT_ID --available-tool mcp__codex_apps__gmail_send_email --inventory host-mail-inventory.json
# The host calls the returned real send tool, saves its response, then records it
vpub notify record RETURNED_ALERT_ID --claim-token RETURNED_TOKEN --response actual-send-response.json
```

Test messages are labelled TEST, with no fabricated sign-in incident. When official verification interrupts publishing, preserve the existing work and notify inline. Email queues only if enabled. The host must have a usable Gmail plugin at execution time; the CLI cannot send through it when the host is not running. Queued, sent and inbox-delivered are separate states. Timeouts or uncertain results never cause automatic retries. See the [handoff guide](DEEPSEEK_HANDOFF_EN.md) and [live validation](docs/VALIDATION_EN.md).

## Backends and references

Bilibili reuses [kukemc/bili-cli](https://github.com/kukemc/bili-cli). YouTube uses its official API. Douyin/X/Instagram use CLI-owned background browsers, with host-browser handoff as an explicit fallback. See [validation](docs/VALIDATION_EN.md) for actual publication evidence and limits.

[NOTICE](NOTICE_EN.md) · [Research](docs/RESEARCH_EN.md) · [TikTok status](docs/TIKTOK_STATUS_EN.md) · [Bilibili contact](https://space.bilibili.com/10706217) · [Lab home](../../README_EN.md)

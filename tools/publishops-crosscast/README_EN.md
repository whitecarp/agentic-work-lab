# PublishOps CrossCast

[繁體中文](README.md) · **English**

### Multi-platform video release operations console

**v0.3.1 alpha · CLI `vpub` · AI-assisted development**

Prepare platform-specific metadata, covers and publication receipts for one original video. Choose any subset of Bilibili, Douyin, YouTube, Instagram and X. Sign in once; the CLI reuses its own local sessions for later uploads.

Default routes: Bilibili uses bili-cli, YouTube uses its official API, and Douyin/Instagram/X use a dedicated persistent Chrome profile controlled by the CLI in the background. First login and official verification require the user. Explicit `method: native` retains host-browser handoff. This remains alpha; live development tests are documented separately from unattended acceptance. TikTok is suspended. YouTube cards/clickable end screens, standalone X/Instagram APIs and unified comments/analytics remain unimplemented.

[Features](docs/FEATURES_EN.md) · [Validation](docs/VALIDATION_EN.md) · [Agent handoff](DEEPSEEK_HANDOFF_EN.md) · [Draft testing](docs/DRAFT_TESTING_EN.md)

## Install and start

Requires Python 3.10–3.12 and Git for Git-URL installation. The [Windows installer](install.ps1) finds compatible Python, creates an isolated environment and installs dependencies; missing Python must be prepared by the agent. The background browser routes require Google Chrome. Run `vpub setup --install-browser` if it is missing. YouTube API mode requires a Desktop OAuth client JSON; the other three routes do not need platform developer keys. A standalone model API key is not required.

```sh
pip install "git+https://github.com/whitecarp/agentic-work-lab.git#subdirectory=tools/publishops-crosscast"
vpub doctor
vpub onboard --platforms bilibili,douyin,youtube
vpub init --video final.mp4 --language zh-Hans --out publish.json --platforms bilibili,douyin,youtube
```

Choose platforms, automatic publication versus editorial review, and optional email once. Reuse authorization without repeated editorial approvals. `--confirm` is a plan-integrity hash. The CLI persists email settings; the host remembers editorial preferences.

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

**Next-video priority: cover adaptation and visual quality.** Both phone profile grids cropped the footer text in this test. Application succeeded; layout acceptance did not. Next, inspect real phone-size thumbnails for headline, subtitle, footer and face visibility, including crop and playback-count overlays. Move essential information into the common safe area, remove ineffective small text and compare size, contrast and composition. Retain the current cover for this round.

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

## Optional email notifications

Choose off/Gmail/Outlook once. **Default is off; no SMS.** If enabled, select a recipient and use an actually available connected send tool. An installed plugin alone does not prove send capability.

```sh
vpub notify status
vpub notify configure --provider off
# Opt in: vpub notify configure --provider gmail --to you@example.com
vpub notify pending
```

On official verification, notify inline and preserve the work. Email queues only if enabled. The host uses `notify claim`, calls Gmail/Outlook and records the actual result with `notify record`. Queued is not sent; accepted is not delivered. Report missing tools or uncertainty without automatic resend. See the [handoff guide](DEEPSEEK_HANDOFF_EN.md).

## Backends and references

Bilibili reuses [kukemc/bili-cli](https://github.com/kukemc/bili-cli). YouTube uses its official API. Douyin/X/Instagram use CLI-owned background browsers, with host-browser handoff as an explicit fallback. See [validation](docs/VALIDATION_EN.md) for actual publication evidence and limits.

[NOTICE](NOTICE_EN.md) · [Research](docs/RESEARCH_EN.md) · [TikTok status](docs/TIKTOK_STATUS_EN.md) · [Bilibili contact](https://space.bilibili.com/10706217) · [Lab home](../../README_EN.md)

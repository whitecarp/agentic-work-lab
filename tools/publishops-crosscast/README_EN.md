# PublishOps CrossCast

[繁體中文](README.md) · **English**

### Multi-platform video release operations console

**v0.2.5 alpha · CLI `vpub` · AI-assisted development**

Prepare platform-specific metadata, covers and publication receipts for one original video. Choose any subset of Bilibili, Douyin, YouTube, Instagram and X, then select your sign-in method on official sites inside the host's browser.

This alpha is not a fully validated five-platform publisher. Browser execution needs a host agent; a login URL or queue does not prove sign-in or publication. TikTok is suspended. YouTube cards/clickable end screens, standalone X/Instagram APIs and unified comments/analytics remain unimplemented.

[Features](docs/FEATURES_EN.md) · [Validation](docs/VALIDATION_EN.md) · [Agent handoff](DEEPSEEK_HANDOFF_EN.md) · [Draft testing](docs/DRAFT_TESTING_EN.md)

## Install and start

Requires Python 3.10–3.12 and Git for Git-URL installation. The [Windows installer](install.ps1) finds compatible Python, creates an isolated environment and installs dependencies; missing Python must be prepared by the agent. Default sign-in needs no user OAuth setup or model API key.

```sh
pip install "git+https://github.com/whitecarp/agentic-work-lab.git#subdirectory=tools/publishops-crosscast"
vpub doctor
vpub onboard --platforms bilibili,douyin,youtube
vpub init --video final.mp4 --language zh-Hans --out publish.json --platforms bilibili,douyin,youtube
```

Choose platforms, automatic publication versus editorial review, and optional email once. Reuse authorization without repeated editorial approvals. `--confirm` is a plan-integrity hash. The CLI persists email settings; the host remembers editorial preferences.

For upgrades, uninstall the old `whitecarp-video-publisher` package first. The `vpub` command, `video_publisher` Python module and runtime/sign-in/job storage are retained; do not delete user data.

### Optional YouTube API sign-in

Enable YouTube Data API v3 in Google Cloud, create a Desktop app OAuth client and download its JSON. An API key alone cannot authorize uploads. If the project is in Testing mode, add your sign-in account under Google Auth Platform → Audience → Test users to avoid `403 access_denied`.

```sh
vpub auth login youtube --method api --client-json /path/to/client.json
vpub auth status youtube --method api
```

Open the printed official Google authorization link on the same computer running the CLI, choose your channel and grant access. The callback waits for five minutes. Keep client JSON and tokens local; never commit them to GitHub. Set the YouTube manifest's `method` to `api` to use this backend; signing in does not change existing manifests. Playlist writes require explicit additional authorization with `--manage-playlists`.

If authorization completes but API reads time out, run `vpub doctor`. Dependencies include [PySocks](https://pypi.org/project/PySocks/): without it, the Google client's `httplib2` transport can ignore a configured proxy. In an older environment, run `python -m pip install PySocks==1.7.1` with the same Python environment and retry the status query; do not reupload the video.

## Copywriting and language

| Platform | Default metadata language |
|---|---|
| Bilibili, Douyin | Simplified Chinese; translate other languages |
| YouTube, Instagram, X | English; translate non-English metadata |

Preserve wording and voice when source and target match, adapting necessary length and format. Bilingual output is an explicit exception. **GitHub documentation language does not determine video metadata language.** Translating metadata does not alter video audio or subtitles.

```sh
vpub metadata request publish.json --source script.txt --out request.json
# The host conversation model reads request.json and writes response.json.
# For an explicit bilingual exception, add --bilingual youtube to request.
vpub metadata apply publish.json --request request.json --response response.json --out localized.json
```

`bili-cli` submits supplied descriptions; the host conversation model writes them. CrossCast adds structured requests, language routing and validation, without reading hidden history or embedding a standalone translation model. External links are off by default; irrelevant BV references are omitted while context and plain-text third-party credits/licenses remain. Schema checks do not prove translation quality.

## Production covers and original video

```sh
vpub covers prepare localized.json --source production/cover.jpg --out-dir covers-v1 --out ready.json
# Add --portrait production/portrait.jpg for dedicated portrait artwork.
```

Prefer platform-specific `covers`, then a dedicated portrait, then shared production artwork. Contain-and-pad fitting preserves the whole image without cropping, stretching or replacing originals; matching reruns reuse hash-checked output. Translate baked-in image text in the source composition separately.

| Platform | Working canvas and application |
|---|---|
| Bilibili | 1600×1000; upstream CLI or native browser |
| YouTube | 1920×1080; API or Studio, possibly requiring official verification |
| Douyin | 1200×900 landscape + 1080×1440 portrait; native browser |
| Instagram | Local 1080×1920 variants; separate web cover, shared Douyin portrait on mobile |
| X | Original landscape artwork through Premium/Premium+ Media Studio; no standalone CLI cover command |

These are working presets, not permanent platform specifications. Complete local artwork does not prevent platform cropping: inspect platform previews and profile thumbnails. Source SHA-256 is not a remote-byte hash of the platform's compressed file.

### Instagram publishing and covers

**Instagram retains original footage and Original crop, with a separate upload cover.** On a phone, Edit cover → Add from camera roll can select the same dedicated portrait used for Douyin. The 0.2.4 one-second lead-in experiment did not solve this requirement and was removed in 0.2.5. Preparation no longer modifies/transcodes video or requires FFmpeg.

```sh
vpub instagram prepare ready.json --out instagram-ready.json
# Add --mobile-cover production/portrait.jpg if the manifest lacks one.
```

On the phone, open Edit cover, choose Add from camera roll and select the shared portrait. Inspect both the cover and profile-grid previews before saving; different surfaces may crop different regions.

The handoff prefers an explicit `--mobile-cover`, then `covers.douyin.portrait`, then `covers.instagram.portrait`. It records the identical image path and hash; the user performs the mobile step. For future shared artwork, generally consider a 9:16 composition with titles, faces and essential text in a central safe area that also survives 3:4 crops. Avoid edges and UI overlays and inspect both platforms. This is a design guideline, not a guarantee of identical cropping.

Use the production original in new plans, not retained 0.2.4 lead-in experiments. A CLI update does not replace, delete or repost existing publications.

[X Media Studio workflow](docs/X_GUIDE_EN.md) · [Deletion and cover replacement](docs/DELETION_AND_COVERS_EN.md)

For supported platforms, use the following handoff to edit an existing work, checking its original account, work ID and saved result. Instagram mobile covers and X Studio use the dedicated workflows above.

```sh
vpub browser cover RECEIPT_KEY --file landscape.jpg --portrait portrait.jpg --identity OBSERVED_ACCOUNT
```

## Playlists, categories and chapters

YouTube accepts native `playlist_titles` or `playlist_ids`. The API checks membership before inserting and never blindly retries uncertain writes. Extra API permission requires explicit `auth login youtube --method api --manage-playlists`. Studio creation of a private list and selection for the existing work were verified; the API route remains mock-tested. Bilibili collections remain upstream functionality rather than a unified collection-management command.

New YouTube plans require an explicit `category_id`, selected from content and official choices rather than defaulting to People & Blogs. The host model can supply it via `metadata request/apply`; explicit selections are preserved.

Native Bilibili/Douyin/YouTube handoffs accept measured `duration_seconds` and `chapters`. Starts must come from the same final video, begin at zero and increase within its duration. Translate titles using platform metadata language without altering timing. Other publication routes reject this field.

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

The same four timestamps with Simplified Chinese titles were checked and submitted on Douyin. The user later confirmed mobile visibility; the work remains private. See the [guide](docs/CATEGORIES_AND_CHAPTERS_EN.md) for the chapter entry, 12-character title limit and review receipts. Description timestamps do not prove native chapters are active; missing access or review stays pending.

## Publication and receipts

```sh
vpub plan ready.json
vpub publish ready.json --dry-run
vpub publish ready.json --confirm CURRENT_PLAN_HASH
vpub browser request RECEIPT_KEY
vpub browser begin RECEIPT_KEY --identity OBSERVED_ACCOUNT
# The host performs authorized actions and reads back the saved result.
vpub browser record RECEIPT_KEY --evidence local-evidence.json
vpub receipts
```

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

Bilibili reuses [kukemc/bili-cli](https://github.com/kukemc/bili-cli). YouTube offers an optional official API; standalone Douyin remains experimental. X/Instagram use host-browser handoff. See [validation](docs/VALIDATION_EN.md) for actual publication evidence and limits.

[NOTICE](NOTICE_EN.md) · [Research](docs/RESEARCH_EN.md) · [TikTok status](docs/TIKTOK_STATUS_EN.md) · [Bilibili contact](https://space.bilibili.com/10706217) · [Lab home](../../README_EN.md)

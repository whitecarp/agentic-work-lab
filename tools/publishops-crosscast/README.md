# PublishOps CrossCast

### 跨平台影片發布營運台 / Multi-platform video release operations console

**v0.2.0 alpha · CLI `vpub` · AI 協助開發 / AI-assisted development**

讓同一支原創影片擁有各平台適用的文案、封面與發布回執。選擇 Bilibili、抖音、YouTube、Instagram、X 的任意組合，在宿主內置瀏覽器開官方網站，登入方式由使用者選。

Prepare platform-specific metadata, covers and publication receipts for one original video. Choose any subset of Bilibili, Douyin, YouTube, Instagram and X, then sign in on official sites inside the host's browser.

**這是 alpha 整合工具，五站完整實發尚未驗收。** CLI 的登入網址或佇列不是成功證據；內置瀏覽器需要宿主 Agent 實際操作。X／Instagram API、通用跨平台評論與數據命令、YouTube 卡片／可點擊片尾尚未實作。TikTok 已暫停。

**This alpha is not a fully validated five-platform publisher.** A URL or queue is not success evidence; native-browser execution needs a host agent. X/Instagram APIs, unified comments/analytics, YouTube cards and clickable end screens remain unimplemented. TikTok is suspended.

[功能範圍 / Features](docs/FEATURES.md) · [驗證記錄 / Validation](docs/VALIDATION.md) · [Agent 交接 / Handoff](DEEPSEEK_HANDOFF.md)

## 安裝與開始 / Install and start

需要 Python 3.10–3.12，Git URL 安裝另需 Git。[Windows 安裝腳本](install.ps1) 尋找相容 Python、建立獨立環境並安裝依賴；缺少 Python 時提示 Agent 準備。預設登入不需要觀眾申請 OAuth 或模型 API 金鑰。

Requires Python 3.10–3.12 and Git for Git-URL installation. The Windows installer finds compatible Python, creates an isolated environment and installs dependencies; missing Python must be prepared by the agent. Default sign-in needs no viewer OAuth setup or model API key.

```sh
pip install "git+https://github.com/whitecarp/ai-video-lab.git#subdirectory=tools/publishops-crosscast"
vpub doctor
vpub onboard --platforms bilibili,douyin,youtube
vpub init --video final.mp4 --language zh-Hans --out publish.json --platforms bilibili,douyin,youtube
```

首次讓使用者選平台、自動發布或先看文案，以及是否開啟郵件。沿用已取得的授權，不逐片強迫文案審核。`--confirm` 是方案完整性雜湊，不是另一次人工審核。CLI 保存郵件設定；文案審核偏好由宿主保存。

Ask once about platforms, automatic publication versus editorial review, and optional email. Reuse authorization without repeated editorial approvals. `--confirm` is a plan-integrity hash, not another human review. CLI persists email settings; the host remembers editorial preferences.

舊版升級先卸載套件 `whitecarp-video-publisher`，再安裝本版本。保留 `vpub`、Python 模組 `video_publisher` 與原有 runtime／登入／工作紀錄，不刪除使用者資料。

For upgrades, uninstall the old `whitecarp-video-publisher` package first. The command, Python module and runtime/sign-in/job storage are retained; do not delete user data.

## 自動簡介與語言 / Copywriting and language

| 平台 / Platform | 預設目標 / Default target |
|---|---|
| Bilibili、抖音 / Douyin | 簡體中文，其他語言翻譯為簡體 / Simplified Chinese; translate other languages |
| YouTube、Instagram、X | 英文，非英文翻譯為英文 / English; translate non-English metadata |

已符合目標語言時保留原文與角色語氣，僅適配長度及格式。雙語是明確選擇的例外。GitHub 用繁體中文＋英文，**不決定影片文案語言**；翻譯文案不改影片音軌或字幕。

Preserve wording and voice when source and target match, adapting necessary length/format. Bilingual output is an explicit exception. Traditional Chinese/English GitHub docs **do not determine video metadata language**. Metadata translation does not alter video audio or subtitles.

```sh
vpub metadata request publish.json --source script.txt --out request.json
# Optional bilingual exception: --bilingual youtube
# Host conversation model reads request.json and writes response.json.
vpub metadata apply publish.json --request request.json --response response.json --out localized.json
```

`bili-cli` 接收已寫好的簡介，寫作由宿主聊天模型完成。CrossCast 將此方式擴充成結構化請求、語言規則與校驗，不讀隱藏對話，也不內建獨立翻譯模型。預設不放外鏈，移除跨平台無用的 BV 參照，保留情節及第三方作者／授權的純文字資訊。結構校驗不等於翻譯品質驗證。

`bili-cli` submits supplied descriptions; the host conversation model writes them. CrossCast adds structured requests, language routing and validation, without reading hidden history or embedding a standalone translation model. External links are off by default; irrelevant BV references are omitted while context and plain-text third-party credits/licenses remain. Schema checks do not prove translation quality.

## 自動封面適配 / Automatic cover fitting

```sh
vpub covers prepare localized.json --source production/cover.jpg --out-dir covers-v1 --out ready.json
# Optional dedicated portrait composition: --portrait production/portrait.jpg
```

優先使用方案 `covers` 裡的平台專用圖，再用直式專用圖，最後共用製作封面。自動縮放補邊保留完整畫面，不盲目裁切、不拉伸、不覆蓋原圖；重跑核對雜湊後復用。圖片內嵌文字不會自動翻譯，需要時由 Agent 編輯原製作工程再輸出。

Prefer platform-specific `covers`, then a dedicated portrait, then the shared production artwork. Contain-and-pad fitting keeps the whole image without cropping, stretching or replacing originals; matching reruns reuse hash-checked output. Baked-in text is not automatically translated; the host edits the source composition when necessary.

| 工作畫布 / Working canvas | 尺寸 / Size | 套用 / Application |
|---|---|---|
| Bilibili | 1600×1000 | CLI / native |
| YouTube | 1920×1080 | API / Studio; verification may be required |
| 抖音 / Douyin | 1200×900 + 1080×1440 | Native landscape + portrait |
| Instagram | 1080×1920 | File generation; live application unverified |
| X | No output | Custom thumbnail unsupported and reported |

這些是工作預設，不是永久平台規格；以當前官方介面為準。保存後逐版讀回，來源 SHA-256 不冒充平台壓縮檔的遠端雜湊。修改現有作品使用下列命令，不重傳影片：

These are working presets, not permanent platform specifications. Follow the current UI and inspect every saved variant. Source SHA-256 is not a remote-byte hash. Edit an existing work without reuploading video:

```sh
vpub browser cover RECEIPT_KEY --file landscape.jpg --portrait portrait.jpg --identity OBSERVED_ACCOUNT
# Host applies the covers, saves, then records actual visual evidence.
```

## 播放列表與發布 / Playlists and publication

YouTube 可設定 `playlist_titles`（內置瀏覽器）或 `playlist_ids`。讀回需包含選定清單；API 先查成員避免重複加入，不盲目重試結果不明的寫入。額外 API 權限需明確執行 `auth login youtube --method api --manage-playlists`。已驗證 Studio 新建私密清單並選入現有影片；API 路徑仍為離線測試。B站合集沿用上游能力，尚無統一跨平台合集管理命令。

YouTube accepts native `playlist_titles` or `playlist_ids`. Verify selected lists on read-back; API checks membership before inserting and never blindly retries uncertain writes. Extra API permission requires explicit `auth login youtube --method api --manage-playlists`. Studio creation of a private list and selection for the existing work were verified; the API route remains mock-tested. Bilibili collections remain upstream functionality, not a unified collection-management command.

```sh
vpub plan ready.json
vpub publish ready.json --dry-run
vpub publish ready.json --confirm CURRENT_PLAN_HASH
vpub browser request RECEIPT_KEY
vpub browser begin RECEIPT_KEY --identity OBSERVED_ACCOUNT
# Host performs authorized UI actions on the original work.
vpub browser record RECEIPT_KEY --evidence local-evidence.json
vpub receipts
```

新範本預設 B站／抖音公開、YouTube 私密、Instagram／X 依帳號受眾，宿主須按使用者意圖設定。登入不會自動增加發布目標。準備、待驗證、審核中與公開成功分開；宿主畫面證據及 API 查詢使用不同狀態。

Templates default to public Bilibili/Douyin, private YouTube and account audience for Instagram/X; the host must apply user intent. Signing in never adds targets. Preparation, verification waits, platform review and public success are distinct; UI evidence and API verification have separate states.

## 可選郵件 / Optional email

首次選「不啟用／Gmail／Outlook」，**預設關閉，不需要簡訊**。啟用才設定收件地址，宿主使用當時實際可用的已連接寄信工具；已安裝插件不等於具備寄信權限。

Choose off/Gmail/Outlook once. **Default is off; no SMS.** If enabled, select a recipient and use an actually available connected send tool. An installed plugin alone does not prove send capability.

```sh
vpub notify status
vpub notify configure --provider off
# Optional: vpub notify configure --provider gmail --to you@example.com
vpub notify pending
```

官方驗證出現時立即在對話提醒，保留原作品。已啟用郵件才入列；宿主 `notify claim` 後調用 Gmail／Outlook，再 `notify record` 記真實結果。入列不等於寄出、服務接受不等於送達；缺少工具或結果不明就回報，不自動重寄。詳見 [交接文件](DEEPSEEK_HANDOFF.md)。

On official verification, notify inline and preserve the work. Email queues only if enabled. The host claims once, calls Gmail/Outlook and records the actual result. Queued is not sent; accepted is not delivered. Report missing tools/uncertainty without automatic resend. See the handoff guide.

## 後端與參考 / Backends and references

B站重用 [kukemc/bili-cli](https://github.com/kukemc/bili-cli)，YouTube 可選官方 API，抖音獨立後端仍屬實驗；封面及私密修改使用內置瀏覽器。X／Instagram 為宿主瀏覽器交接，完整實發待驗收。

Bilibili reuses upstream bili-cli; YouTube offers an optional official API. Standalone Douyin remains experimental; covers and private edits use native mode. X/Instagram use host-browser handoff, with complete live publication still unverified.

[NOTICE](NOTICE.md) · [研究 / Research](docs/RESEARCH.md) · [X guide](docs/X_GUIDE.md) · [TikTok 狀態 / Status](docs/TIKTOK_STATUS.md) · [B站聯絡 / Contact](https://space.bilibili.com/10706217)

## 章節 / Chapters

平台選項可設定 `duration_seconds` 與 `chapters`。時間點必須來自同一支成片，自零開始、遞增且小於片長；章節標題跟隨平台文案語言。宿主可翻譯標題，但不能改時間。

Platform options accept measured `duration_seconds` and `chapters`. Starts must come from the same final video, begin at zero and increase within its duration. Translate titles using platform metadata language without altering timing.

```json
{
  "duration_seconds": 79,
  "chapters": [
    {"start_seconds": 0, "title": "迟到的回复"},
    {"start_seconds": 19, "title": "旧留言里的想念"},
    {"start_seconds": 46, "title": "用自己的身份回应"},
    {"start_seconds": 57, "title": "先读，再开口"}
  ]
}
```

本例是同一集現有章節的時間規劃，不是抖音已添加的證明。目前只實作 B站／抖音／YouTube 的內置瀏覽器交接與回執檢查，其他路徑明確拒絕。平台無入口、未開通或審核中時保留待辦，不能只在簡介寫時間碼就宣稱平台章節已生效。

This example is a timing plan from existing chapters for the same episode, not proof of Douyin application. Native Bilibili/Douyin/YouTube handoff and evidence checks are implemented; other routes reject the field. Missing access/UI or review stays pending; description timestamps alone do not prove native chapters are active.

# 五平台影片工作流 / Five-platform Video Workflow

**v0.1.1 alpha · 帳號與發布驗收中 / Account and publication acceptance in progress**

Bilibili、抖音、YouTube、Instagram、X（Twitter）：選擇平台，在官方網站自行登入，由 AI Agent 接續操作。繁體中文＋English；AI 協助開發。

Choose Bilibili, Douyin, YouTube, Instagram, X (Twitter), or any subset. Sign in yourself on the official websites, then let your AI agent continue. AI-assisted development.

**這不是五站全功能、自動實發均已驗證的產品。** 已實作統一方案、檔案快照、SHA-256 去重、持久回執、內置瀏覽器交接及可選後端。五站實發尚未驗收；X／Instagram API 與跨平台查詢／互動命令尚未實作。

**This is not a fully verified five-platform autopublisher.** Implemented: a common plan, media snapshots, SHA-256 deduplication, durable receipts, in-app-browser handoff and optional backends. Live publication remains unverified. X/Instagram API clients and unified cross-platform query/interaction commands are not implemented.

**TikTok 已暫停 / TikTok suspended:** 本次香港（+852）手機登入未成功，已移除 TikTok 選項；歡迎其他地區可登入的帳號持有人透過 [B站](https://space.bilibili.com/10706217) 協助測試。[限制與招募說明 / Limitation and testing invitation](docs/TIKTOK_STATUS.md)。 / Our Hong Kong phone-number login test was unsuccessful. TikTok is disabled; volunteers with working accounts in other regions can contact us on Bilibili.

**X（Twitter）新增 / X added:** 官方頁登入交接、帖文文案與 AI 聲明、帳號及 status ID 回執校驗已實作；真實 X 發片尚未驗收。[操作說明 / Guide](docs/X_GUIDE.md)。 / Sign-in handoff, post text/disclosure and author/status-ID receipt checks are implemented; live X publication is not yet verified.

## 交給 Agent / Give this to your agent

```text
請安裝 https://github.com/whitecarp/ai-video-lab/tree/main/tools/video-publisher
先閱讀 README 和 DEEPSEEK_HANDOFF.md，讓我選擇平台。
直接在內置瀏覽器打開官方網頁，登入方式由我自己選。
不要使用畫面轉接頁，不要替我輸入密碼或驗證碼。
確認影片、帳號、文字與可見範圍後，再進行發布測試。
```

```text
Install https://github.com/whitecarp/ai-video-lab/tree/main/tools/video-publisher.
Read README and DEEPSEEK_HANDOFF.md; let me choose the platforms.
Open official websites directly in your built-in browser. I choose how to sign in.
Do not use a screen relay or enter my passwords or verification codes.
Test publication after the video, accounts, text and visibility are approved.
```

觀眾自行選擇網站提供的掃碼、電話、郵箱或第三方登入。已有登入則沿用，之後繼續使用同一瀏覽器。不是每個平台都有掃碼；不自動搬移 Cookie 至另一個程式。

Viewers choose among the site's available QR, phone, email or identity-provider methods. Reuse existing sign-in and continue in that browser. QR availability varies; cookies are not transferred to another program.

## 安裝 / Install

需要 Python 3.10–3.12；從 Git URL 安裝另需 Git。可選抖音獨立後端另需 Chrome。Agent 可準備依賴；預設流程不要求觀眾申請 API 金鑰或 OAuth 設定。

Requires Python 3.10–3.12 and Git for installation from a Git URL; the optional standalone Douyin backend also needs Chrome. The agent can prepare dependencies. Default mode needs no viewer API keys or OAuth setup.

```sh
pip install "git+https://github.com/whitecarp/ai-video-lab.git#subdirectory=tools/video-publisher"
vpub doctor
vpub capabilities
vpub onboard --platforms youtube,instagram,x
```

Windows 下載本倉庫後可執行 [install.ps1](install.ps1)。腳本尋找相容 Python、建立獨立環境並安裝本目錄；不登入或投稿，缺少 Python 時明確報錯。

On Windows, after downloading this repository, run [install.ps1](install.ps1). It finds compatible Python, creates an isolated environment and installs this directory. It never logs in or posts, and reports missing Python.

`onboard`／`auth login` 預設回傳 `NATIVE_BROWSER_REQUIRED` 與官方網址。**需要宿主 Agent 打開網址及讀取頁面；CLI 單獨執行不會操作 Codex，也不會宣稱已登入。**

Default `onboard` / `auth login` returns `NATIVE_BROWSER_REQUIRED` and official URLs. **The host agent must open and inspect the page. The standalone CLI cannot control Codex or claim completed sign-in.**

## 後端 / Backends

| 平台 / Platform | 新範本預設 / New-template default | 可選後端 / Optional backend |
|---|---|---|
| Bilibili | 內置瀏覽器 / Native | `method: cli`，bili-agent 0.7.0 |
| 抖音 / Douyin | 內置瀏覽器 / Native | `method: browser`，實驗性 Chrome / Experimental Chrome |
| YouTube | 內置瀏覽器 / Native | `method: api`，自備 Desktop OAuth / Own Desktop OAuth |
| X（Twitter） | 內置瀏覽器 / Native | API 尚未實作 / API not implemented |
| Instagram | 內置瀏覽器 / Native | API 尚未實作 / API not implemented |

X 與 Instagram 的 `visibility: account` 依帳號及當前受眾設定，不是假設有每條影片的公開／私密開關。以實際頁面選項為準；遇到地區、帳號或驗證限制時回報，不變更帳號所在地或繞過限制。

X and Instagram `visibility: account` follows account/current audience settings, not an invented per-video privacy switch. Use actual UI options; report regional, account or verification restrictions without changing regions or bypassing controls.

## 發布 / Publication

```sh
vpub init --video final.mp4 --out publish.json --platforms bilibili,youtube,douyin,instagram,x
# 審閱並編輯 / Review and edit metadata, disclosure and visibility
vpub plan publish.json
vpub publish publish.json --dry-run
vpub publish publish.json --confirm CURRENT_PLAN_HASH
vpub receipts
```

登入不會自動新增發布平台。可以 `--platforms youtube,x` 限定本次方案。範本的 B站／抖音為公開，YouTube 為私密，Instagram／X 依帳號受眾，發布前按意圖修改並確認。`plan`／dry-run 不會投稿。

Login never adds publication targets. Restrict a plan with `--platforms youtube,x`. Templates use public Bilibili/Douyin and private YouTube, and account audience for Instagram/X; review against your intent. Planning/dry-run never uploads.

預設 `publish` 建立 `WAITING_FOR_BROWSER` 工作及快照，**不代表影片已上傳**。Agent 按 [交接流程](DEEPSEEK_HANDOFF.md) 在同一瀏覽器執行：

Default `publish` creates `WAITING_FOR_BROWSER` jobs and snapshots, **not uploaded videos**. The host follows the [handoff](DEEPSEEK_HANDOFF.md):

```sh
vpub browser request RECEIPT_KEY
vpub browser begin RECEIPT_KEY --identity OBSERVED_ACCOUNT_ID
# Agent 在官方 UI 上傳、讀回 / Agent uploads and reads back through official UI
vpub browser record RECEIPT_KEY --evidence local-evidence.json
vpub verify RECEIPT_KEY
```

`begin` 必須在選擇影片前執行，且只能一次。`AGENT_REPORTED_VERIFIED` 是 Agent 提供的畫面證據，與 API 回查的 `SUBMISSION_VERIFIED` 分開；等待、草稿、模糊結果都不是公開成功。原生模式 `verify` 要求重新讀取原作品，不假裝 CLI 已讀取瀏覽器。

Call single-use `begin` before selecting the file. `AGENT_REPORTED_VERIFIED` means host-provided UI evidence, separate from API `SUBMISSION_VERIFIED`. Pending jobs, drafts and ambiguous outcomes are not public success. Native `verify` requests a new inspection of the original work.

## 可選獨立登入 / Optional standalone login

```sh
vpub auth login bilibili --method cli
vpub auth status bilibili --method cli --wait 120
vpub setup
vpub auth login douyin --method browser
vpub auth status douyin --method browser --wait 30
vpub auth login youtube --method api --client-json /private/path/client.json
vpub auth status youtube --method api
```

這些後端的會話與內置瀏覽器分開。抖音 QR 後端遇到安全驗證就停下。選配 YouTube API 提供官方 OAuth 網址供使用者操作，不自動開外部窗口。預設不需要觀眾設定 OAuth。

These backends have separate sessions. The Douyin QR backend stops for verification. Optional YouTube API mode emits an official OAuth URL without automatically opening an external window. Default native mode needs no OAuth configuration.

## 恢復與資料 / Recovery and data

同平台／帳號／影片 SHA-256 去重，修改標題不能繞過。部分結果分開保留；不確定時不自動重發。`vpub stop` 暫停發布；`setup --external-stop-file PATH` 可接現有停止檔。中斷後檢查原草稿及回執，不盲目刪資料庫或鎖。每平台只支援一個 `default` 帳號。

Deduplication uses platform/account/media SHA-256; title changes do not bypass it. Preserve partial outcomes and never automatically retry uncertain submissions. Use `vpub stop` or connect an existing stop file. Inspect original drafts/receipts after interruptions instead of blindly clearing locks or the ledger. One default account per platform.

外部停止檔若明確為 `STOP_ALL_BILIBILI_READ_WRITE`，只阻止 B站；本工具自身 STOP 與未知／損壞停止檔仍阻止全部平台。 / An explicit external `STOP_ALL_BILIBILI_READ_WRITE` signal blocks Bilibili only; local STOP and unknown/malformed signals remain global.

執行資料在 `VPUB_HOME` 或 Windows `%LOCALAPPDATA%/WhiteCarpVideoPublisher`，不在倉庫。憑證檔依 OS 使用者權限保護，本工具不提供加密保險箱；不要分享該目錄。原生登入資料由宿主瀏覽器管理。

Runtime files live in `VPUB_HOME` or Windows `%LOCALAPPDATA%/WhiteCarpVideoPublisher`, outside the repository. Files rely on OS user permissions, not an encrypted vault; do not share the directory. Native login data is managed by the host browser.

## bili-cli 與測試 / bili-cli and tests

復用 [bili-cli](https://github.com/kukemc/bili-cli)，不是其官方多平台版本。B站搜尋、影片理解、收件箱、評論及創作數據沿用安裝後的 `bili` 命令；不移除原有限流、審計與權限規則。跨平台對應見 [功能矩陣](docs/FEATURES.md)。

This reuses [bili-cli](https://github.com/kukemc/bili-cli), not an official multi-platform edition. Its installed `bili` command retains Bilibili discovery, video understanding, inbox, comments and analytics with its existing controls. See the [feature matrix](docs/FEATURES.md).

```sh
python -m unittest discover -s tests -v
```

離線測試不投稿，也不代表真實網站實發已驗證。 / Offline tests never post and do not establish live-publication compatibility.

[驗證狀態 / Validation](docs/VALIDATION.md) · [研究 / Research](docs/RESEARCH.md) · [教學提綱 / Tutorial](docs/TUTORIAL_BRIEF.md) · [MIT](LICENSE) · [第三方說明 / Notices](NOTICE.md)

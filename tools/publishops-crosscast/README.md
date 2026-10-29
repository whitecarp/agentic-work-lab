# PublishOps CrossCast

**繁體中文** · [English](README_EN.md)

### 跨平台影片發布營運台

**v0.3.0 alpha · CLI `vpub` · AI 協助開發**

讓同一支原創影片擁有各平台適用的文案、封面與發布回執。選擇 Bilibili、抖音、YouTube、Instagram、X 的任意組合，首次登入後，CLI 復用本機專用會話完成後續上傳。

預設路徑：B站沿用 bili-cli，YouTube 使用官方 API，抖音／Instagram／X 由 CLI 在背景控制專用 Chrome。首次登入與官方驗證需要使用者協助；明確設定 `method: native` 仍可使用宿主瀏覽器交接。這是 alpha 版本，開發期實測與無人值守驗收分開記錄。TikTok 已暫停，YouTube 卡片／可點擊片尾、X／Instagram 獨立 API、通用評論與數據命令尚未實作。

[功能範圍](docs/FEATURES.md) · [驗證記錄](docs/VALIDATION.md) · [Agent 交接](DEEPSEEK_HANDOFF.md) · [草稿測試](docs/DRAFT_TESTING.md)

## 安裝與開始

需要 Python 3.10–3.12，Git URL 安裝另需 Git。[Windows 安裝腳本](install.ps1) 尋找相容 Python、建立獨立環境並安裝依賴；缺少 Python 時提示 Agent 準備。背景瀏覽器需要 Google Chrome，缺少時可用 `vpub setup --install-browser` 安裝。YouTube API 需要桌面 OAuth 用戶端 JSON，另外三站無需申請平台開發者金鑰；不需要獨立模型 API 金鑰。

```sh
pip install "git+https://github.com/whitecarp/agentic-work-lab.git#subdirectory=tools/publishops-crosscast"
vpub doctor
vpub onboard --platforms bilibili,douyin,youtube
vpub init --video final.mp4 --language zh-Hans --out publish.json --platforms bilibili,douyin,youtube
```

首次選擇平台、自動發布或先看文案，以及是否開啟郵件。沿用已取得的授權，不逐片強迫文案審核。`--confirm` 是方案完整性雜湊；CLI 保存郵件設定，宿主保存文案審核偏好。

舊版升級先卸載套件 `whitecarp-video-publisher`，再安裝本版本。保留 `vpub`、Python 模組 `video_publisher` 與原有執行、登入及工作紀錄，不刪除使用者資料。

### YouTube 官方 API 登入

啟用 Google Cloud 的 YouTube Data API v3，建立「桌面應用程式」OAuth 用戶端並下載 JSON；API Key 本身不能授權上傳。若專案仍在測試模式，先於 Google Auth Platform 的「目標對象／受眾 → 測試使用者」加入登入帳號，避免 `403 access_denied`。

```sh
vpub auth login youtube --method api --client-json /path/to/client.json
vpub auth status youtube --method api
```

在執行 CLI 的同一台電腦開啟輸出的 Google 官方授權連結，選擇頻道並完成授權；回呼等待時間為五分鐘。JSON 與登入權杖留在本機，請勿提交 GitHub。方案的 YouTube `method` 設為 `api` 才會使用 API；登入不會自動改變既有方案。播放列表寫入需另加 `--manage-playlists` 授權。

若授權已完成卻在 API 查詢逾時，先執行 `vpub doctor`。安裝依賴包含 [PySocks](https://pypi.org/project/PySocks/)；Google API 客戶端的 `httplib2` 缺少它時可能忽略已設定的代理。舊環境可在同一個 Python 環境執行 `python -m pip install PySocks==1.7.1`，再執行狀態查詢，毋須重傳影片。

## 登入一次，後續用 CLI 發布

```sh
vpub auth login x
vpub auth login instagram
vpub auth login douyin
vpub auth status all
```

首次登入開啟 CrossCast 專用的官方 Chrome 視窗，使用者自行選擇網站提供的掃碼、密碼等方式並完成簡訊或身份驗證。後續命令在背景復用同一本機會話。這與 Codex 內置瀏覽器分開，不搬移宿主 Cookie；登入可能過期或再次要求驗證。

`onboard` 只啟動選中的平台。既有方案不會自動遷移：抖音／Instagram／X 設為 `method: browser`，YouTube 設為 `method: api`；新版 `init` 已使用這些預設。`method: native` 是明確選用的宿主 Agent 備援。

本次 X／Instagram 已通過重啟後 CLI 回讀；抖音上傳後重啟再次要求登入，會話穩定性仍待驗收。詳見[驗證記錄](docs/VALIDATION.md)。

## 自動簡介與語言

| 平台 | 預設文案語言 |
|---|---|
| Bilibili、抖音 | 簡體中文，其他語言翻譯為簡體中文 |
| YouTube、Instagram、X | 英文，非英文翻譯為英文 |

已符合目標語言時保留原文與角色語氣，僅適配長度及格式。雙語是明確選擇的例外。**GitHub 文件的語言不決定影片文案語言**，翻譯文案也不改影片音軌或字幕。

```sh
vpub metadata request publish.json --source script.txt --out request.json
# 宿主聊天模型讀取 request.json，撰寫 response.json。
# 需要雙語例外時：在 request 加上 --bilingual youtube。
vpub metadata apply publish.json --request request.json --response response.json --out localized.json
```

`bili-cli` 接收已寫好的簡介，寫作由宿主聊天模型完成。CrossCast 將此方式擴充成結構化請求、語言規則與校驗，不讀隱藏對話，也不內建獨立翻譯模型。預設不放外鏈，移除跨平台無用的 BV 參照，保留情節及第三方作者／授權的純文字資訊。結構校驗不等於翻譯品質驗證。

## 專用封面與原影片

```sh
vpub covers prepare localized.json --source production/cover.jpg --out-dir covers-v1 --out ready.json
# 可加 --portrait production/portrait.jpg 指定專用直式構圖。
```

優先使用方案 `covers` 裡的平台專用圖，再用直式專用圖，最後共用製作封面。自動縮放補邊保留完整畫面，不盲目裁切、不拉伸、不覆蓋原圖；重跑核對雜湊後復用。圖片內嵌文字需要在原製作工程中另行翻譯。

| 平台 | 工作畫布與套用方式 |
|---|---|
| Bilibili | 1600×1000；上游 CLI 或內置瀏覽器 |
| YouTube | 1920×1080；API 或 Studio，可能要求官方驗證 |
| 抖音 | 1200×900 橫式＋1080×1440 直式；CLI 在背景分別上傳 |
| Instagram | 1920×1080 網頁圖及 1080×1920 手機圖；另傳封面，影片保留原比例 |
| X | CLI 經 Media Studio 上傳橫式封面；帳號需有 Studio 權限 |

這些是工作預設，不是永久平台規格。本地保留全圖不代表平台不裁切，必須檢查平台預覽與主頁縮圖。來源 SHA-256 也不是平台壓縮檔的遠端雜湊。

### Instagram 發布與封面

**Instagram 保留原片及「原版」比例，上傳時另外設定封面。** 手機「編輯封面 → 從相機膠卷中添加」可選與抖音共用的專用直式圖。0.2.4 的一秒片頭實驗未解決需求，0.2.5 已移除；目前準備命令不改影片、不轉碼，也不需要 FFmpeg。

```sh
vpub instagram prepare ready.json --out instagram-ready.json
# 若方案未指定共用直式圖，可加 --mobile-cover production/portrait.jpg。
```

手機操作：開啟作品的「編輯封面」，選「從相機膠卷中添加」及共用直式圖；檢查封面與「主頁網格」預覽，再保存。不同畫面可能裁切不同區域。

交接優先取明確的 `--mobile-cover`，其次是 `covers.douyin.portrait`，再取 `covers.instagram.portrait`。交接檔保存同一張圖片的路徑與雜湊，手機操作由使用者完成。未來共用封面優先考慮 9:16 構圖，把標題、人物臉部與必要文字放在兼顧 3:4 裁切的中央安全區，避開邊緣與介面遮擋，並檢查兩站實際預覽。這是設計原則，不保證所有畫面裁切一致。

新方案明確選原製作成片，不要選 0.2.4 已插入片頭的實驗版本。更新 CLI 不會自動替換、刪除或重發既有作品。

[X Media Studio 流程](docs/X_GUIDE.md) · [刪除與封面重發](docs/DELETION_AND_COVERS.md)

修改支援平台的現有作品可用下列交接命令；需核對原帳號、作品及保存結果。Instagram 手機換圖與 X Studio 使用上面的專用流程。

```sh
vpub browser cover RECEIPT_KEY --file landscape.jpg --portrait portrait.jpg --identity OBSERVED_ACCOUNT
```

## 播放列表、類別與章節

YouTube 可設定 `playlist_titles`（內置瀏覽器）或 `playlist_ids`。API 先查成員避免重複加入，不盲目重試結果不明的寫入。額外權限需明確執行 `auth login youtube --method api --manage-playlists`。Studio 新建私密清單並選入既有影片已驗證，API 路徑仍為模擬測試；B站合集沿用上游能力，尚無統一合集管理命令。

新 YouTube 方案必須明確填寫 `category_id`，由宿主依影片內容與官方選項選擇，不再固定為「人物和博客」。`metadata request/apply` 可提供類別，已指定值會保留。

B站／抖音／YouTube 的內置瀏覽器交接支援 `duration_seconds` 與 `chapters`。時間點來自同一支成片，自零開始、遞增且小於片長；標題跟隨平台文案語言。其他發布路徑明確拒絕此欄位。

```json
{
  "duration_seconds": 78.506667,
  "chapters": [
    {"start_seconds": 0, "title": "迟到的回复"},
    {"start_seconds": 19, "title": "旧留言里的想念"},
    {"start_seconds": 46, "title": "用自己的身份回应"},
    {"start_seconds": 57, "title": "先读，再开口"}
  ]
}
```

以上四章已在抖音核對後提交，使用者後續確認手機可見，作品保持私密。章節入口、12 字元限制與審核回執見[操作指南](docs/CATEGORIES_AND_CHAPTERS.md)。在簡介寫時間碼不代表平台章節已生效；入口未開通或仍審核中時保留待辦。

## 發布與回執

```sh
vpub plan ready.json
vpub publish ready.json --dry-run
vpub publish ready.json --confirm CURRENT_PLAN_HASH
vpub verify RECEIPT_KEY
vpub works instagram
# X：最終提交前，接續既有 Studio 素材。
vpub resume RECEIPT_KEY
vpub receipts
```

新回執保存帳號、完整文案、素材雜湊與最終提交標記。不確定的提交不會自動再按一次；`verify` 可尋找唯一吻合的新作品。`resume` 在提交後只做驗證；提交前可接續 X 已存的 Studio 素材。Instagram／抖音中斷上傳需檢查既有草稿。`works` 只讀可見首頁（抖音最多十張卡片），不是完整歷史匯出。進階 native 修改仍用 `browser request/begin/record`。

範本預設 B站／抖音公開、YouTube 私密、Instagram／X 依帳號受眾，宿主須按使用者意圖設定。登入不會自動增加發布目標。準備、待驗證、審核中與公開成功分開記錄；畫面證據及 API 查詢使用不同狀態。

## 可選郵件提醒

首次選「不啟用／Gmail／Outlook」，**預設關閉，不需要簡訊**。啟用後設定收件地址，宿主使用當時實際可用的已連接寄信工具；已安裝插件不等於具備寄信權限。

```sh
vpub notify status
vpub notify configure --provider off
# 選擇啟用：vpub notify configure --provider gmail --to you@example.com
vpub notify pending
```

官方驗證出現時立即在對話提醒並保留原作品。已啟用郵件才入列；宿主 `notify claim` 後呼叫 Gmail／Outlook，再 `notify record` 記真實結果。入列不等於寄出，服務接受不等於送達；缺少工具或結果不明就回報，不自動重寄。詳見[交接文件](DEEPSEEK_HANDOFF.md)。

## 後端與參考

B站重用 [kukemc/bili-cli](https://github.com/kukemc/bili-cli)，YouTube 可選官方 API，抖音獨立後端仍屬實驗。X／Instagram 使用宿主瀏覽器交接，完整實發及限制見[驗證記錄](docs/VALIDATION.md)。

[NOTICE](NOTICE.md) · [研究](docs/RESEARCH.md) · [TikTok 暫停說明](docs/TIKTOK_STATUS.md) · [B站聯絡](https://space.bilibili.com/10706217) · [實驗室首頁](../../README.md)

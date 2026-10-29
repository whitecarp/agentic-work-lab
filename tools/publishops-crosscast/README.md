# PublishOps CrossCast

**繁體中文** · [English](README_EN.md)

**X 發布僅面向已訂閱、且帳號實際能進入 Media Studio 的使用者。** 目前不提供免費帳號發布路徑；登入或付款本身不代表已具備發布權限，上傳前仍會核對媒體庫入口與所選帳號。

### 跨平台影片發布營運台

**v0.3.1 alpha · CLI `vpub` · AI 協助開發**

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

在執行 CLI 的同一台電腦開啟輸出的 Google 官方授權連結，選擇頻道並完成授權；回呼等待時間為五分鐘。JSON 與登入權杖留在本機，請勿提交 GitHub。方案的 YouTube `method` 設為 `api` 才會使用 API；登入不會自動改變既有方案。首次連接一次請求上傳、讀取及影片／播放列表管理權限，由使用者在 Google 頁面確認。舊版僅有上傳／讀取的權杖需重新授權一次；`--manage-playlists` 保留為相容旗標。授權不等於允許任意刪片。

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

X／Instagram 已通過重啟後 CLI 回讀；抖音重新登入後，多次獨立命令已可回讀和修改第二期，長期會話穩定性仍待驗收。詳見[驗證記錄](docs/VALIDATION.md)。

## 自動簡介與語言

規則依影片內容的原語言自動選擇，不跟隨帳號地區或 GitHub 文件語言：

| 平台 | 內容語言 | 簡介 |
|---|---|---|
| Bilibili、抖音 | 中文，含簡體與繁體 | 僅簡體中文 |
| Bilibili、抖音 | 其他語言 | 原語言＋簡體中文 |
| YouTube、Instagram、X | 英文 | 僅英文 |
| YouTube、Instagram、X | 其他語言 | 原語言＋英文 |

先保留原語言段落，再放目標語言段落；符合平台語言時不重複。保留角色語氣、集數與情節，按字數限制共同精簡兩段。影片音軌、字幕和封面內的文字不會因此自動翻譯。

```sh
vpub metadata request publish.json --source script.txt --out request.json
# 宿主聊天模型讀取請求、核對腳本，撰寫 response.json。
vpub metadata apply publish.json --request request.json --response response.json --out localized.json
```

雙語回應須包含兩段 `description_parts`，`description` 等於兩段以空行連接；CLI 校驗順序、模式與字數。舊 `--bilingual` 只作相容性檢查，已不必額外指定。原語言不明時先由宿主核對腳本／字幕並設定 `content_language`；不以文件語言猜測。

`bili-cli` 接收已寫好的簡介，寫作由宿主聊天模型完成。CrossCast 將此方式擴充成結構化請求、語言規則與校驗，不讀隱藏對話，也不內建獨立翻譯模型。預設不放外鏈，移除跨平台無用的 BV 參照，保留情節及第三方作者／授權的純文字資訊。結構校驗不等於翻譯品質驗證。

## 專用封面與原影片

```sh
vpub covers prepare localized.json --source production/cover.jpg --portrait production/portrait.jpg --out-dir covers-v1 --out ready.json
```

橫式角色優先使用方案的專用圖，再用製作封面，縮放補邊保留全圖。抖音與 Instagram 的直式角色必須使用同一張專用構圖：`--portrait` 優先，其次取方案的抖音／Instagram 直圖。兩站輸出保留完全相同的圖片位元組與 SHA-256，不再把橫圖補邊冒充直式封面。缺少直圖時提示準備素材；不覆蓋原圖。

| 平台 | 工作畫布與套用方式 |
|---|---|
| Bilibili | 1600×1000；上游 CLI 或內置瀏覽器 |
| YouTube | 1920×1080；API 或 Studio，可能要求官方驗證 |
| 抖音 | 1200×900 橫式＋共用直式原圖；CLI 分別上傳，平台直式編輯器會裁為 3:4 |
| Instagram | 1920×1080 網頁圖＋同一張共用直式原圖供手機選取；影片保留原比例 |
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

**下一條影片的重點：封面適配與美觀。** 本次兩站手機主頁都裁掉了底部小字；封面已套用，不代表版式驗收通過。下一輪先以真實手機縮略圖檢查主標、副標、底部小字及人物臉部，預留裁切與播放數等介面遮擋空間；必要資訊移入共同安全區，減少無效小字，再比較字級、對比和構圖。這次保留現有封面，不再反覆換圖。

新方案明確選原製作成片，不要選 0.2.4 已插入片頭的實驗版本。更新 CLI 不會自動替換、刪除或重發既有作品。

[X Media Studio 流程](docs/X_GUIDE.md) · [刪除與封面重發](docs/DELETION_AND_COVERS.md)

修改支援平台的現有作品可用下列交接命令；需核對原帳號、作品及保存結果。Instagram 手機換圖與 X Studio 使用上面的專用流程。

```sh
vpub browser cover RECEIPT_KEY --file landscape.jpg --portrait portrait.jpg --identity OBSERVED_ACCOUNT
```

## 播放列表、類別與章節

YouTube 可設定 `playlist_titles`（內置瀏覽器）或 `playlist_ids`。API 先查成員避免重複加入，不盲目重試結果不明的寫入。所需管理權限已納入首次 OAuth 授權。Studio 新建私密清單並選入既有影片已驗證，API 路徑仍為模擬測試；B站合集沿用上游能力，尚無統一合集管理命令。

新 YouTube 方案必須明確填寫 `category_id`，由宿主依影片內容與官方選項選擇，不再固定為「人物和博客」。`metadata request/apply` 可提供類別，已指定值會保留。

抖音 `method: browser` 可在發布流程中設定 `duration_seconds` 與 `chapters`：作品取得持久 ID 後，CLI 填入自訂章節、保存並逐項讀回；不把平台自動建議當成已完成。若作品仍審核中，回執記錄待辦，後續 `vpub verify`／`resume` 完成這個章節步驟，不重傳影片。B站／抖音／YouTube 的 native 交接仍保留。時間點來自同一支成片，自零開始、遞增且小於片長；標題跟隨平台文案語言。

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

以上為第一期的歷史範例，當時使用者確認手機可見；目前第二期的六個自訂章節也已由 CLI 保存並完整讀回。章節入口、12 字元限制與審核回執見[操作指南](docs/CATEGORIES_AND_CHAPTERS.md)。在簡介寫時間碼不代表平台章節已生效；入口未開通或仍審核中時保留待辦。

## 修改已發布作品

```sh
vpub edit RECEIPT_KEY correction.json --dry-run
vpub edit RECEIPT_KEY correction.json
# 保存結果不明時只讀回，不再按保存：
vpub edit RECEIPT_KEY correction.json --verify-only
```

JSON 必須有 `expected_identity`，每次只改一組：完整 `title`／`description` 與語言欄位，或抖音 `portrait` 檔案，或 `chapters`／`duration_seconds`。YouTube 經 API、Instagram／X／抖音經專用 CLI 會話；不更換影片或修改可見性。回執保留原作品 ID，結果不明的保存禁止自動重試。封面回讀提供本機截圖，仍須視覺核對，不以檔案雜湊冒充遠端像素比對。

X 只有原帖出現 Edit 時才可嘗試修改；不可編輯時不自動刪除、重發或回覆。本次第二期依使用者選擇保留原帖，下一條才採新語言規則。

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

新回執保存帳號、完整文案、素材雜湊與最終提交標記。不確定的提交不會自動再按一次；`verify` 可尋找唯一吻合的新作品。`resume` 在提交後驗證原作品，並可完成原方案尚未執行的抖音章節；提交前可接續 X 已存的 Studio 素材。Instagram／抖音中斷上傳需檢查既有草稿。`works` 只讀可見首頁（抖音最多十張卡片），不是完整歷史匯出。進階 native 修改仍用 `browser request/begin/record`。

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

B站重用 [kukemc/bili-cli](https://github.com/kukemc/bili-cli)，YouTube 使用官方 API；抖音／X／Instagram 使用 CLI 專用背景瀏覽器，宿主瀏覽器僅為明確選用的備援。完整實發及限制見[驗證記錄](docs/VALIDATION.md)。

[NOTICE](NOTICE.md) · [研究](docs/RESEARCH.md) · [TikTok 暫停說明](docs/TIKTOK_STATUS.md) · [B站聯絡](https://space.bilibili.com/10706217) · [實驗室首頁](../../README.md)

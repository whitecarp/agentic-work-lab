# PublishOps CrossCast

**繁體中文** · [English](README_EN.md)

**X 發布僅面向已訂閱、且帳號實際能進入 Media Studio 的使用者。**

原因是未訂閱帳號的影片上傳上限為 **140 秒、512 MB**，且無法使用本工具所依賴的 Media Studio 媒體管理與自訂封面流程。CrossCast 的 X 後端透過 Media Studio 上傳影片並設定製作封面，因此目前僅支援具備此權限的訂閱帳號。登入或付款本身不代表權限已開通，上傳前仍會核對媒體庫入口與所選帳號。[官方上傳限制](https://help.x.com/en/using-x/x-videos) · [Media Studio 權限](https://help.x.com/en/using-x/media-studio)。

### 跨平台影片發布營運台

**v0.5.0 alpha · CLI `vpub` · AI 協助開發**

讓同一支原創影片擁有各平台適用的文案、封面與發布回執。選擇 Bilibili、抖音、YouTube、Instagram、X 的任意組合，首次登入後，CLI 復用本機專用會話完成後續上傳。

預設路徑：B站沿用 bili-cli，YouTube 使用官方 API，抖音／Instagram／X 由 CLI 在背景控制專用 Chrome。首次登入與官方驗證需要使用者協助；明確設定 `method: native` 仍可使用宿主瀏覽器交接。這是 alpha 版本，開發期實測與無人值守驗收分開記錄。已新增評論、作品數據及匯報命令；各站實測範圍見下方。TikTok、YouTube 卡片／可點擊片尾與 X／Instagram 獨立 API 尚未實作。

[功能範圍](docs/FEATURES.md) · [驗證記錄](docs/VALIDATION.md) · [Agent 交接](DEEPSEEK_HANDOFF.md) · [草稿測試](docs/DRAFT_TESTING.md)

## 評論、作品數據與營運匯報

先讀取指定作品的評論，再由宿主聊天模型理解上下文並撰寫回覆。**回覆只使用評論者的語言，單語發送**；不套用影片簡介的雙語規則，也不跟隨帳號或匯報語言。宿主識別英語、法語等語言後傳入 `--language`；CLI 的字元提示不是完整語言識別器，不確定時不猜。

```sh
vpub comments list youtube VIDEO_ID --limit 20 --out comments.json
vpub comments reply youtube VIDEO_ID COMMENT_ID --file reply.txt --language zh
vpub comments verify-reply REPLY_KEY
vpub stats youtube VIDEO_ID --out stats.json
vpub preferences --language zh
vpub report --language auto --out report.json
```

把 `youtube` 換成 `bilibili`、`douyin`、`instagram` 或 `x` 即可使用相同入口。瀏覽器平台目前要求作品已有 CrossCast 回執，以核對登入帳號；不猜測其他人的作品歸屬。`comments reply` 直接發送，沒有逐條審批；`--dry-run` 僅為可選預覽。文案由宿主模型提供，CLI 不自行聊天、翻譯或追加署名。

發送前重新核對評論與帳號，先寫持久狀態，再提交一次；結果不明時只用 `verify-reply` 讀回，不盲目重送。一般回覆拒絕自己的評論及已知已有作者回覆的串。讀取只涵蓋回傳的頁面／可見列表；`complete: false` 不能當作已讀完所有評論。YouTube 與 B站可用 `next_cursor` 繼續；瀏覽器不捏造分頁游標。

| 平台 | 評論與回覆 | 作品數據 |
|---|---|---|
| B站 | 上游 bili-cli；保留串與回覆 ID | 播放、讚、評論、收藏等；創作者完播／觀看時長依上游可用欄位 |
| YouTube | 官方 Data API；驗證作品、串、作者、正文及回覆 ID | 播放、讚、評論；完播率／觀看時長尚未接入 Analytics |
| 抖音 | 作品頁讀取；創作者中心回覆頂層留言；新回覆及父留言已讀回確認 | 創作者後台顯示的播放、讚、評論、分享、收藏、完播率等 |
| Instagram | 可見評論及 Reply；以新評論 ID、作者、正文與操作記錄讀回 | 僅作品頁實際顯示的數值，未接入完整 Insights |
| X | 可見對話及 Reply；以新帖 ID、作者、正文與操作記錄讀回 | 帖文瀏覽、回覆、轉發、讚；帖文瀏覽不等於影片播放 |

抖音回覆使用創作者中心原生控制項，觀察該頁自己發出的請求以核對留言 ID；不是獨立官方 API。若後台尚未載入目標、存在同名同文的歧義或目標是子回覆，就停止並保留原記錄。發送回執與刷新後可見性分開判定。

各項數據保留來源、採樣時間和單位；缺少的欄位是 `null`，不寫成零。瀏覽器驗證記錄的是指定 Reply 動作及後續可見結果，不冒充官方 API 的父評論關係。詳見[實測邊界](docs/VALIDATION.md)。

匯報跟隨**使用者**語言，預設中文或英文。宿主根據對話設定 `preferences --language zh|en`，也可單次 `report --language zh|en`；未設定時由系統語系決定。它把本機執行回執整理成一段話，只把讀回的回覆算作完成；自己的評論和自測不算真實觀眾互動。`--since` 可指定帶時區的起點，`--notes` 可附有來源 URL 的待跟進發現；不編造點讚、@消息、私信或研究成果。匯報不會自動寄信。

僅在使用者明確要求實發測試時執行：

```sh
vpub comments self-test youtube VIDEO_ID --test-id hello-test-01
```

這會在自己的作品下發送一次「你好」並回覆「你好」；同一 test-id 重跑復用記錄，未確認結果不重送。它不會在登入或安裝時執行，也不會自動刪除留言。

## 多平台巡檢與數據變化

先選擇要追蹤的作品，之後一個指令即可收集評論、作品數據與本輪匯報。這是手動執行的唯讀巡檢，不會建立排程、發送回覆、按讚或寄信。

```sh
vpub works youtube --limit 20
vpub works bilibili --limit 20
vpub works instagram --limit 10
vpub ops watch add youtube VIDEO_ID
vpub ops watch add douyin WORK_ID
vpub ops watch list
vpub ops sync --pages 2 --limit 20 --language zh --out round.json
vpub ops sync --platforms youtube,douyin --out selected-round.json
vpub ops watch remove youtube VIDEO_ID
```

`works` 支援五站：YouTube 從已授權頻道的上傳列表讀取，B站只列已發布稿件，保留定時與審核安排。這兩站用 `--cursor` 翻頁。抖音、Instagram、X 仍只列目前可見的首頁，不把它當成完整作品庫。瀏覽器平台的追蹤作品需要既有 CrossCast 發布回執；API 平台直接核對作品擁有者。

`ops sync` 僅處理追蹤清單，每部作品最多讀取 `--pages` 頁，每頁最多 `--limit` 條頂層評論；API 回覆預覽可能另外增加項目數。首次讀取建立基線，後續按評論 ID 去重，自己的留言不列入觀眾互動。`inbox` 是已觀察評論的待處理候選清單，不是私訊或全帳號收件箱；`needs_thread_check` 表示尚不能確定是否已回覆，必須讀取上下文。CLI 不會因候選清單而自行發送。

作品數據保留歷次事件與最新快照；只有同帳號、同作品、同來源、同標籤及單位的精確數值才計算 `changes.delta`。缺失、約數、換來源都保留 `null`，百分比差值標為百分點，負數變化照實保留。不同平台的播放或瀏覽定義不相加。部分失敗會保留成功項目並回傳 `PARTIAL`、退出碼 4；遇到驗證或帳號錯誤，不會換路徑重試。每輪保存本機回執，匯報只涵蓋該輪事件。

### 讀取評論樓的下一頁

```sh
vpub comments replies youtube VIDEO_ID ROOT_COMMENT_ID --limit 20 --out replies.json
vpub comments replies youtube VIDEO_ID ROOT_COMMENT_ID --cursor NEXT_CURSOR
vpub comments replies bilibili BV_ID ROOT_COMMENT_ID --limit 20
```

這兩站可分頁讀取指定頂層留言下的回覆，核對作品與父留言。`next_cursor` 表示仍有下一頁；`complete` 僅描述目前請求範圍。這不會把 Instagram、X、抖音的可見留言讀取升級為完整歷史匯出，也不會繞過原有發送去重。

## 安裝與開始

需要 Python 3.10–3.12，Git URL 安裝另需 Git。[Windows 安裝腳本](install.ps1) 尋找相容 Python、建立獨立環境並安裝依賴；缺少 Python 時提示 Agent 準備。背景瀏覽器需要 Google Chrome，缺少時可用 `vpub setup --install-browser` 安裝。YouTube API 需要桌面 OAuth 用戶端 JSON，另外三站無需申請平台開發者金鑰；不需要獨立模型 API 金鑰。

```sh
pip install "git+https://github.com/whitecarp/agentic-work-lab.git#subdirectory=tools/publishops-crosscast"
vpub doctor
vpub onboard --platforms bilibili,douyin,youtube
vpub init --video final.mp4 --language zh-Hans --out publish.json --platforms bilibili,douyin,youtube
```

首次選擇平台及自動發布或先看文案；郵件僅支援 Gmail，預設關閉，使用者主動要求時才開啟。沿用已取得的授權，不逐片強迫文案審核。`--confirm` 是方案完整性雜湊；CLI 保存郵件設定，宿主保存文案審核偏好。

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

**第二期共用封面已確認可用。** 2026-10-30，使用者在抖音與 Instagram 手機主頁套用 v3 後提供截圖並確認通過；底部小字已移入可見區域。這次驗收僅涵蓋提供的兩個手機主頁畫面，不能保證所有裝置與展示位置的裁切一致。後續封面沿用共同安全區，繼續檢查字級、對比、構圖及播放數等介面遮擋。

新方案明確選原製作成片，不要選 0.2.4 已插入片頭的實驗版本。更新 CLI 不會自動替換、刪除或重發既有作品。

[X Media Studio 流程](docs/X_GUIDE.md) · [刪除與封面重發](docs/DELETION_AND_COVERS.md)

修改支援平台的現有作品可用下列交接命令；需核對原帳號、作品及保存結果。Instagram 手機換圖與 X Studio 使用上面的專用流程。

Instagram 文案修正會打開原作品的編輯欄，完整比較已保存的原始文字；不把頁面翻譯、留言或舊成功回執當作目前簡介。相同文字不再保存；不一致時修正同一作品，再重新開啟編輯欄讀回。`vpub edit KEY PATCH --verify-only` 只核驗、不保存；`EDIT_MISMATCH` 代表實際文字不同，可檢查後明確重新執行修改。

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

## 可選郵件提醒：僅支援 Gmail

**預設關閉，只有使用者主動要求時才開啟。** 一般安裝／登入不詢問郵箱，也不會自動啟用。此功能僅支援宿主 Agent 的 Gmail 插件；不支援 Outlook、QQ、網易、SMTP 或簡訊。

使用者要求開啟後，才請其填寫自己的 Gmail 地址。宿主識別地址並檢查插件：缺少插件時協助安裝；已安裝但未連接時引導授權；隨時可放棄並維持關閉。寄出前核對當前連接帳號與所填地址完全一致，**只從自己發給自己**，不把其他服務的登入別名當作 Gmail。

```sh
# 需要時才執行；一般安裝不執行 configure
vpub notify configure --to you@gmail.com
# 隨時關閉
vpub notify configure --provider off
```

以下由宿主 Agent 操作；一般使用者不用編輯 JSON：

```sh
vpub notify inspect --email you@gmail.com --inventory host-mail-inventory.json
# 一次性測試不會開啟長期提醒，同一 test-id 不重寄
vpub notify test --email you@gmail.com --test-id setup-test-001 --inventory host-mail-inventory.json
vpub notify claim RETURNED_ALERT_ID --available-tool mcp__codex_apps__gmail_send_email --inventory host-mail-inventory.json
# 宿主呼叫返回的真實寄信工具，再保存回傳並記錄
vpub notify record RETURNED_ALERT_ID --claim-token RETURNED_TOKEN --response actual-send-response.json
```

測試信明確標記 TEST，不偽造登入故障。官方驗證出現時保留原作品並立即在對話提示；只有已啟用郵件才入列。宿主必須當下能呼叫 Gmail 插件，CLI 不會在宿主未運行時獨自寄信。入列、已寄出與收件箱送達分開記錄；逾時或結果不明不自動重寄。詳見[交接文件](DEEPSEEK_HANDOFF.md)與[實測狀態](docs/VALIDATION.md)。

## 後端與參考

B站重用 [kukemc/bili-cli](https://github.com/kukemc/bili-cli)，YouTube 使用官方 API；抖音／X／Instagram 使用 CLI 專用背景瀏覽器，宿主瀏覽器僅為明確選用的備援。完整實發及限制見[驗證記錄](docs/VALIDATION.md)。

[NOTICE](NOTICE.md) · [研究](docs/RESEARCH.md) · [TikTok 暫停說明](docs/TIKTOK_STATUS.md) · [B站聯絡](https://space.bilibili.com/10706217) · [實驗室首頁](../../README.md)

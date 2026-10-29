# Agent 交接

**繁體中文** · [English](DEEPSEEK_HANDOFF_EN.md)

## 0.3.0 預設 CLI 流程

1. 讀 `vpub capabilities`，保留使用者選擇的平台與既有發布授權，不干擾其他營運排程。
2. 首次 `auth login`，之後 `auth status`；CLI 用專用會話核對實際帳號，不匯入宿主 Cookie。YouTube 需要桌面 OAuth JSON，X 需要 Media Studio 權限。
3. 綁定正確集數、原片 SHA-256 及製作封面。透過 `metadata request/apply` 準備 B站／抖音簡體中文、YouTube／Instagram／X 英文；逐項核對標題、文案長度、類別、受眾與 AI 聲明，不跟隨 GitHub 文件語言。
4. 先 `plan` 再 `publish --confirm HASH`；雜湊是內容一致性檢查，不是額外文案審批。CLI 自行上傳並提交一次，保留回執。
5. 提交不確定時用 `verify KEY`。`resume KEY` 在提交前只接續 X 已存的 Studio 素材，提交後只驗證。不得清空回執或資料庫強制重傳；Instagram／抖音中斷上傳需檢查既有草稿。
6. 遇到 `WAITING_FOR_USER` 立即告知，保留進度並讓使用者完成官方登入驗證。郵件需事先選用且宿主真的有寄信工具；佇列不是寄送成功。

開發測試程式的實測不等於無人值守驗收，見[驗證記錄](docs/VALIDATION.md)。安裝本身不授權新增自動化或平台互動。

## 明確選用的 native 備援

以下保留流程僅適用於 `method: native` 方案，使用宿主支援的內置瀏覽器工具；不取代使用者要求或宿主權限。

**草稿模式優先** 若 request/begin 的 `final_submission_allowed` 為 false，禁止最終 Share／Post／排期；只到預覽或保存草稿。此限制優先於下方一般發布步驟。用 `outcome: draft` 分別記錄 PREVIEW_READY／DRAFT_SAVED，不能當成實發成功。先讀 [草稿測試指南](docs/DRAFT_TESTING.md)。

1. 讀 `vpub capabilities`，讓使用者選平台。打開 `auth login` 提供的官方網址，讓使用者自行選擇登入方式；已有登入直接沿用，不搬 Cookie。
2. 在真正創作者頁確認自己帳號。YouTube 核對 Studio 頻道 ID 與創作介面；其他平台核對個人／創作者頁。標籤打開、本地檔案存在或 CLI 回傳網址都不是登入證據。
3. 展示確切影片、平台、帳號、文字、AI 聲明與可見範圍。沿用已取得的明確授權；沒有指定測試片時不要任選檔案投稿。
4. 用當前 plan hash 執行 `publish`。讀 `browser request KEY`，確認帳號，再 `browser begin KEY --identity OBSERVED_ID`。**之後才選擇回傳的快照影片**。B站 UID、YouTube channel ID、其他平台實際帳號名稱均由頁面取得。
5. 按實際 UI 上傳，每步重新讀頁面。檢查受眾、AI、商業內容、音樂等欄位；不能按方案設定就停止，保留草稿。不替使用者接受新條款、解驗證或改地區。最終提交最多一次。
6. 立刻記錄作品 ID，再開同一作品讀回。處理、審核與公開可播放分開記錄。保存短小的本地證據並執行 `browser record KEY --evidence FILE`；不確定用 `uncertain`，不重傳。

```json
{
  "outcome": "verified",
  "id": "PLATFORM_VIDEO_ID",
  "page_url": "OFFICIAL_WORK_URL",
  "identity": "OBSERVED_ACCOUNT_ID",
  "title": "EXACT_APPROVED_TITLE",
  "description": "EXACT_APPROVED_DESCRIPTION",
  "tags": ["EXACT_APPROVED_TAG"],
  "visibility": "private",
  "ai_generated": true,
  "made_for_kids": false,
  "observation": "Describe actual fields and status read from the original work."
}
```

這是格式示例，不是觀察證據。`made_for_kids` 僅 YouTube；Instagram 用作品 shortcode 及 `visibility: account`；X 用數字 status ID 和 `https://x.com/HANDLE/status/ID`；另須讀回完整 `post_text`，包括 AI 聲明。TikTok 已暫停，不得登入或發布。使用 caption 的平台，要核對組合後的標題、說明、標籤；不能省略欄位卻聲稱完整。

`AGENT_REPORTED_VERIFIED` 是宿主 UI 證據，不是 CLI 獨立查詢。`verify` 不重發。宿主缺少瀏覽器工具時，明確報告缺少能力。

安裝與測試不建立排程、不恢復舊營運、不自動留言／私訊／按讚。維持原單一營運歸屬。不要將 Cookie、私訊、驗證碼、原始瀏覽器頁面或私人回執上傳 GitHub。

X 操作與欄位見 [X_GUIDE](docs/X_GUIDE.md)；取消 TikTok 的原因及招募見 [TIKTOK_STATUS](docs/TIKTOK_STATUS.md)。

## 0.2.0 必須沿用的設定

- 首次詢問平台、是否自動發布與可選郵件；已有授權不逐片再問。郵件預設關閉，保存 off/Gmail/Outlook 選擇。
- 用 `metadata request/apply` 請宿主模型寫簡介。B站／抖音簡體，YouTube／Instagram／X 英文；雙語僅在指定平台啟用。GitHub 文件繁體不影響影片文案。
- 預設簡介不放連結；保留角色聲音、內容與第三方作者／授權資訊，移除無用的跨平台 BV 參照。
- 專用封面優先。`covers prepare` 自動縮放補邊；原工程有橫豎重排版時優先使用。不得只找到檔案便宣稱已更新。`cover_evidence` 每個 role 需 source_sha256 與實際畫面 observation。
- YouTube 清單按方案選擇，已有成員不重複加入；同名清單有歧義時確認對象。章節使用同片真實時間點；平台未提供入口或正在審核就明確記待辦，不改成文字時間碼後宣稱已適配。
- 遇驗證立即在對話告知使用者，保存 `outcome: action_required`；恢復原作品，不再次 begin 或重傳。

郵件宿主流程

```sh
vpub notify pending
vpub notify claim ALERT_ID --available-tool EXACT_AVAILABLE_SEND_TOOL
# Call the returned tool_name with its arguments through the connected host tool.
# Save {"tool_name":"ACTUAL_TOOL_NAME","response": ACTUAL_TOOL_RESULT} locally.
vpub notify record ALERT_ID --claim-token RETURNED_TOKEN --response response.json
```

只在使用者啟用並選定收件地址後寄信。工具不存在就回報，不能把 claim 當成已寄出。Gmail 的 message ID 與 Outlook 的服務接受分開記；未知結果不自動重寄。不要附登入網址、驗證碼或原始頁面。

## 0.2.1 類別與章節修復

YouTube 每片補上明確 `category_id`，按內容及當前官方選項選擇，不複製上一片類別。改現有作品用 `browser revise`，保存後回執必須匹配。抖音懸停「編輯作品」找到「添加章節／編輯章節」；12 字元內，核對列表整秒，審核中不重交。

## 草稿改為發布

使用者明確要求發布後，先確認同一帳號，再用 `browser authorize-publish KEY --identity OBSERVED_ACCOUNT`。該命令只更新本地授權，保留原草稿證據；不重新 begin／選檔，之後在原頁提交一次並記錄 ID。沒有這項授權就維持草稿。刪除需要針對具體作品的明確授權，不能從發布授權推斷。

刪除與重發使用 [專用交接](docs/DELETION_AND_COVERS.md)。只驗證刪除不代表重發成功；舊作品 ID 必須保留於歷史。不得對 X、YouTube、抖音或 B站套用 Instagram 刪除命令。

## Instagram 預設處理

每個 Instagram 任務保留原製作成片，在內置瀏覽器選「原版」上傳並另選封面。`vpub instagram prepare MANIFEST --out instagram-ready.json` 只建立方案與封面交接，不修改影片。手機竪版圖優先重用抖音專用封面的同一檔案；可用 `--mobile-cover PATH` 指定，或由方案的抖音／Instagram portrait 路徑取得。提醒使用者在手機「編輯封面」→「從相機膠卷中添加」選共用圖，檢查網格後保存。

0.2.4 的一秒封面插入實驗已撤回。不要再執行此處理，也不要選到留存的實驗衍生片。手機設定是使用者輔助操作，未取得回報前不可聲稱 CLI 已設定完成。[指南](README.md#instagram-發布與封面)。

X 封面使用 Media Studio 原比例圖片；Instagram 也不再插入片頭。Studio 自訂封面與發帖本輪已實測；CLI `revise-cover` 尚未支援此表面，不能繞過其錯誤而偽造既有帖回執。

相關連結：[完整指南](docs/CATEGORIES_AND_CHAPTERS.md)

[返回實驗室首頁](../../README.md)

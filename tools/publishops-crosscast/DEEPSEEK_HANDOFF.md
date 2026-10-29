# Agent 交接 / Agent handoff

使用宿主支援的內置瀏覽器工具。此流程不取代使用者要求或宿主權限。

Use the host's supported in-app browser. This workflow does not override user instructions or host permissions.

**草稿模式優先 / Draft-mode override:** 若 request/begin 的 `final_submission_allowed` 為 false，禁止最終 Share／Post／排期；只到預覽或保存草稿。此限制優先於下方一般發布步驟。用 `outcome: draft` 分別記錄 PREVIEW_READY／DRAFT_SAVED，不能當成實發成功。先讀 [草稿測試指南 / Draft testing](docs/DRAFT_TESTING.md)。 / A false flag overrides the general publication steps below: stop at preview/save draft, never submit or schedule. Record the distinct unpublished states with draft evidence.

1. 讀 `vpub capabilities`，讓使用者選平台。打開 `auth login` 提供的官方網址，讓使用者自行選擇登入方式；已有登入直接沿用，不搬 Cookie。
   Read capabilities and selected platforms. Open the official URL and let the user choose a login method; reuse sign-in without exporting cookies.
2. 在真正創作者頁確認自己帳號。YouTube 核對 Studio 頻道 ID 與創作介面；其他平台核對個人／創作者頁。標籤打開、本地檔案存在或 CLI 回傳網址都不是登入證據。
   Verify the account in the actual creator UI. For YouTube inspect the Studio channel ID. A tab, local file or returned URL alone is not login evidence.
3. 展示確切影片、平台、帳號、文字、AI 聲明與可見範圍。沿用已取得的明確授權；沒有指定測試片時不要任選檔案投稿。
   Review exact media, targets, accounts, text, AI disclosure and visibility. Reuse existing explicit authorization; never choose an arbitrary test upload.
4. 用當前 plan hash 執行 `publish`。讀 `browser request KEY`，確認帳號，再 `browser begin KEY --identity OBSERVED_ID`。**之後才選擇回傳的快照影片**。B站 UID、YouTube channel ID、其他平台實際帳號名稱均由頁面取得。
   Publish with the current hash, read the browser request, verify identity, then begin. **Only then select the returned snapshot file**. Obtain identity from the actual page.
5. 按實際 UI 上傳，每步重新讀頁面。檢查受眾、AI、商業內容、音樂等欄位；不能按方案設定就停止，保留草稿。不替使用者接受新條款、解驗證或改地區。最終提交最多一次。
   Use actual UI, refreshing observations after actions. Check audience, AI, commercial-content and music controls. Stop and preserve the draft if approved settings cannot be applied. Do not accept new terms, solve challenges or change region for the user. Submit at most once.
6. 立刻記錄作品 ID，再開同一作品讀回。處理、審核與公開可播放分開記錄。保存短小的本地證據並執行 `browser record KEY --evidence FILE`；不確定用 `uncertain`，不重傳。
   Preserve the ID immediately and reopen that work. Distinguish processing, review and playback. Record concise local evidence; use `uncertain` rather than reuploading when unclear.

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

This is a format example, not evidence. `made_for_kids` is YouTube-only; Instagram uses a shortcode and `visibility: account`; X uses a numeric status ID and `https://x.com/HANDLE/status/ID`, and additionally requires the exact `post_text` read-back including AI disclosure. TikTok is suspended; do not log in or publish. For captions, verify the combined title, description and tags instead of claiming omitted fields were applied.

`AGENT_REPORTED_VERIFIED` 是宿主 UI 證據，不是 CLI 獨立查詢。`verify` 不重發。宿主缺少瀏覽器工具時，明確報告缺少能力。

`AGENT_REPORTED_VERIFIED` means host UI evidence, not an independent CLI query. Verification never reuploads. Report missing host browser capability explicitly.

安裝與測試不建立排程、不恢復舊營運、不自動留言／私訊／按讚。維持原單一營運歸屬。不要將 Cookie、私訊、驗證碼、原始瀏覽器頁面或私人回執上傳 GitHub。

Installation/testing creates no schedules, restarts no old operators and automatically sends no comments/messages/likes. Preserve the single existing operator. Do not publish cookies, private messages, verification codes, raw browser pages or private receipts to GitHub.

X 操作與欄位見 [X_GUIDE](docs/X_GUIDE.md)；取消 TikTok 的原因及招募見 [TIKTOK_STATUS](docs/TIKTOK_STATUS.md)。 / See the linked X guide and TikTok status before proceeding.

## 0.2.0 必須沿用的設定 / Settings for this version

- 首次詢問平台、是否自動發布與可選郵件；已有授權不逐片再問。郵件預設關閉，保存 off/Gmail/Outlook 選擇。 / Ask once about platforms, automatic publication and optional email; reuse authorization. Email defaults off; persist the chosen option.
- 用 `metadata request/apply` 請宿主模型寫簡介。B站／抖音簡體，YouTube／Instagram／X 英文；雙語僅在指定平台啟用。GitHub 文件繁體不影響影片文案。 / Use host-model copywriting; route Bilibili/Douyin to Simplified Chinese and YouTube/Instagram/X to English. Bilingual is explicit; GitHub language is unrelated.
- 預設簡介不放連結；保留角色聲音、內容與第三方作者／授權資訊，移除無用的跨平台 BV 參照。 / No external links by default; preserve voice, context and third-party credits/licenses; omit irrelevant BV references.
- 專用封面優先。`covers prepare` 自動縮放補邊；原工程有橫豎重排版時優先使用。不得只找到檔案便宣稱已更新。`cover_evidence` 每個 role 需 source_sha256 與實際畫面 observation。 / Prefer dedicated artwork and re-laid-out variants. Fit without cropping and verify every saved role, recording its source hash and visual observation.
- YouTube 清單按方案選擇，已有成員不重複加入；同名清單有歧義時確認對象。章節使用同片真實時間點；平台未提供入口或正在審核就明確記待辦，不改成文字時間碼後宣稱已適配。 / Select intended playlists, avoid duplicate membership, resolve ambiguous names. Chapters must use actual same-video timings; unavailable UI or review means pending, not fake success from text timestamps.
- 遇驗證立即在對話告知使用者，保存 `outcome: action_required`；恢復原作品，不再次 begin 或重傳。 / Notify inline immediately on verification, record action_required and resume the same work without another begin/upload.

郵件宿主流程 / Host email flow:

```sh
vpub notify pending
vpub notify claim ALERT_ID --available-tool EXACT_AVAILABLE_SEND_TOOL
# Call the returned tool_name with its arguments through the connected host tool.
# Save {"tool_name":"ACTUAL_TOOL_NAME","response": ACTUAL_TOOL_RESULT} locally.
vpub notify record ALERT_ID --claim-token RETURNED_TOKEN --response response.json
```

只在使用者啟用並選定收件地址後寄信。工具不存在就回報，不能把 claim 當成已寄出。Gmail 的 message ID 與 Outlook 的服務接受分開記；未知結果不自動重寄。不要附登入網址、驗證碼或原始頁面。 / Send only after opt-in and recipient selection. Missing tools are reported. Claims are not sends; Gmail message IDs and Outlook acceptance are distinct, and unknown results are not retried. Never include authentication URLs, codes or raw pages.

## 0.2.1 類別與章節修復 / Category and chapter fixes

YouTube 每片補上明確 `category_id`，按內容及當前官方選項選擇，不複製上一片類別。改現有作品用 `browser revise`，保存後回執必須匹配。抖音懸停「編輯作品」找到「添加章節／編輯章節」；12 字元內，核對列表整秒，審核中不重交。

Set YouTube category per video from content/current official choices. Revise existing work in place and verify the saved category. For Douyin, hover Edit work for Add/Edit chapters, keep titles within 12 UTF-16 units, check whole-second starts and do not resubmit during review. [完整指南 / Full guide](docs/CATEGORIES_AND_CHAPTERS.md).

## 草稿改為發布 / Authorizing an existing draft

使用者明確要求發布後，先確認同一帳號，再用 `browser authorize-publish KEY --identity OBSERVED_ACCOUNT`。該命令只更新本地授權，保留原草稿證據；不重新 begin／選檔，之後在原頁提交一次並記錄 ID。沒有這項授權就維持草稿。刪除另行處理；本次使用者明確要求暫不刪除。 / Only explicit user authorization permits this command. It preserves draft evidence and updates local authorization without uploading. Submit the existing draft once and record its ID. Without authorization, keep draft mode. Deletion is separate; the current user requested no deletion.

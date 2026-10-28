# Agent 交接 / Agent handoff

使用宿主支援的內置瀏覽器工具。此流程不取代使用者要求或宿主權限。

Use the host's supported in-app browser. This workflow does not override user instructions or host permissions.

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

這是格式示例，不是觀察證據。`made_for_kids` 僅 YouTube；Instagram 用作品 shortcode 及 `visibility: account`；TikTok 用數字 ID 和 `/@name/video/ID` 官方網址。使用 caption 的平台，要核對組合後的標題、說明、標籤；不能省略欄位卻聲稱完整。

This is a format example, not evidence. `made_for_kids` is YouTube-only; Instagram uses a shortcode and `visibility: account`; TikTok uses a numeric ID and official `/@name/video/ID` URL. For captions, verify the combined title, description and tags instead of claiming omitted fields were applied.

`AGENT_REPORTED_VERIFIED` 是宿主 UI 證據，不是 CLI 獨立查詢。`verify` 不重發。宿主缺少瀏覽器工具時，明確報告缺少能力。

`AGENT_REPORTED_VERIFIED` means host UI evidence, not an independent CLI query. Verification never reuploads. Report missing host browser capability explicitly.

安裝與測試不建立排程、不恢復舊營運、不自動留言／私訊／按讚。維持原單一營運歸屬。不要將 Cookie、私訊、驗證碼、原始瀏覽器頁面或私人回執上傳 GitHub。

Installation/testing creates no schedules, restarts no old operators and automatically sends no comments/messages/likes. Preserve the single existing operator. Do not publish cookies, private messages, verification codes, raw browser pages or private receipts to GitHub.

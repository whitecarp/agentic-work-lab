# X（Twitter）適配 / X (Twitter) adapter

本版提供 **內置瀏覽器交接**：登入網址、單一帖文文案、帳號核對、檔案快照、單次提交與回執校驗。Agent 仍須實際操作官方網站；沒有獨立 X API 發布器，也尚未完成 X 真實發片驗收。

This release provides **in-app-browser handoff**: sign-in URL, one post body, account checks, media snapshots, single submission and receipt validation. A host agent must operate the official site. A standalone X API publisher and live X publication acceptance are not complete.

```sh
vpub onboard --platforms x
vpub init --video final.mp4 --out x-publish.json --platforms x
# 修改帳號與文案 / Review identity and text
vpub plan x-publish.json
vpub publish x-publish.json --confirm CURRENT_PLAN_HASH
```

在平台選項加入 `expected_identity`，值為登入後個人檔案可見的 handle（不含 `@`）。同一瀏覽器確認帳號後才執行 `browser begin`；登入方式由使用者自行選擇。

Set `expected_identity` to the handle visible in the signed-in profile, without `@`. Confirm the account in the same browser before `browser begin`; let the user choose the sign-in method.

- **文案 / Text:** `plan.platforms.x.post_text` 依序組合標題、說明、hashtag；`ai_generated: true` 會附「AI 生成內容 / AI-generated」。上傳時使用完整 `post_text`，不要重組或截斷。 / Use the exact generated `post_text`, including its AI disclosure; do not silently rewrite or truncate it.
- **受眾 / Audience:** `visibility: account` 遵循帳號和頁面當前受眾。不是每條影片的「僅自己可見」，不自動修改帳號公開／保護設定。 / Account/current UI audience applies; this is not an individual private-video switch and does not change protected-account settings.
- **規格 / Limits:** 以網站的文案計數器、影片檢查和帳號權限為準；中文、emoji、網址不能用 Python `len()` 當成 X 的正式計數。Agent 先核對影片時長、大小與格式，有不符即停止，不自動裁片或假設有付費權限。 / Check the website's weighted character counter, media validation and account limits; do not treat Python string length as X's counter. Inspect duration/size/format first, stop on mismatch, and do not silently trim or assume paid privileges.
- **提交 / Submit:** 等待影片處理完成、核對全文與受眾，再點擊一次發布；不確定結果只檢查原帖。 / Wait for processing, check exact text/audience, submit once; inspect the original post after an uncertain result.
- **回執 / Receipt:** 讀回 `https://x.com/HANDLE/status/ID`，記錄數字 ID、原帳號、全文 `post_text` 與各來源欄位。校驗器拒絕另一帳號的網址、不同 ID 或遺漏 AI 說明的全文。 / Read back the status URL, numeric ID, original handle, exact `post_text` and source fields. Validation rejects a different author, mismatched ID or altered disclosure/text.

X 官方網頁上傳說明目前列出：未訂閱帳號最多 140 秒、512 MB；付費帳號有不同額度。這是查閱日期的參考，實際帳號介面仍須驗證。 / X's web-upload guide currently lists 140 seconds and 512 MB for non-subscribers, with different paid limits; validate the actual account UI as well. [官方來源 / Official source](https://help.x.com/en/using-x/x-videos)（查閱 / checked 2026-10-29）。

搜尋、作品清單、數據分析、評論與收件箱尚未接成統一 X 命令；不要將此交接流程宣傳為完整 X CLI。

Discovery, work listings, analytics, comments and inbox are not unified X commands yet. Do not describe this handoff as a full X CLI.

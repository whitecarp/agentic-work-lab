# 上傳方式研究 / Upload-route research

核對日期 / Checked: **2026-10-29**. 下列為來源與設計判斷，不是實發成功證明。 / Sources and design decisions below do not prove live upload success.

## 內置瀏覽器 / Native browser

實測 YouTube 在 Codex 內置瀏覽器沿用現有登入；Instagram 也進入登入後首頁，能打開照片／影片選擇入口。這只證明本次帳號與介面，不能外推所有地區、帳號或 AI 客戶端。畫面轉接的 Google 登入原型已從發行程式移除。

YouTube reused an existing in-app session. Instagram reached an authenticated home page and a photo/video picker. This proves only the tested account/UI state, not all regions, accounts or agents. The Google screen-relay login prototype was removed from the distributed code.

## YouTube

官方 API 使用 OAuth 與 resumable upload，需要開發者端設定；本工具保留為選配，不把設定交給預設流程的觀眾。預設用宿主的官方 Studio 網頁。 / The optional API uses OAuth and resumable upload with developer setup; default viewers use official Studio through the host browser.

Sources: [Desktop OAuth](https://developers.google.com/youtube/data/guides/auth/installed-apps), [resumable upload](https://developers.google.com/youtube/data/guides/using_resumable_upload_protocol), [videos.insert](https://developers.google.com/youtube/data/reference/rest/v3/videos/insert).

## TikTok

Direct Post 需要應用與使用者授權，未通過審查的 API client 有私密可見限制；使用者要能預覽、選擇當前允許的受眾並同意上傳。因此，不能把「個人有帳號」說成「公共 API 發布已可用」。本版不實作 TikTok API，先驗證官方網頁流程。

Direct Post needs app/user authorization; unaudited API clients face private-viewing restrictions. Its UX requires preview, allowed audience choices and consent. Having an account does not establish public API publishing access. This version validates the official web workflow first and does not implement a TikTok API client.

Sources: [Direct Post prerequisites](https://developers.tiktok.com/docs/en/content-posting-api-get-started), [content sharing guidelines](https://developers.tiktok.com/docs/en/content-sharing-guidelines).

## Instagram

Meta 官方 API 文件描述 Professional 帳號的內容發布，Facebook Login 路線另有 Page 關聯及權限要求。不能把普通網站登入當作 API 已授權。本次先用官方網頁，API 整合另列待辦。

Meta's API documentation describes content publishing for professional accounts; its Facebook Login route adds Page linkage and permissions. A website login is not API authorization. This iteration uses official web UI, with API integration tracked separately.

Source: [Meta's Instagram API collection](https://www.postman.com/meta/instagram/documentation/6yqw8pt/instagram-api?entity=request-23987686-1ff01566-3509-48bd-a0f4-8571a91ccfdf). Meta 開發者部分頁面本次回傳 429／無法讀取，未將第三方教學當成官方現況。 / Some Meta developer pages returned 429/unavailable; third-party tutorials were not treated as authoritative current behavior.

## Bilibili／抖音 / Bilibili and Douyin

B站復用 bili-cli；抖音官方開放平台有應用能力和授權要求，另保留個人帳號網頁路線。可選 SAU 輔助固定版本，只用表單填寫，發布由本工具的單次提交與回執邏輯管理。

Bilibili reuses bili-cli. Douyin's open platform has app-capability/authorization requirements, while its official website offers an account workflow. Optional pinned SAU helpers fill forms; this tool controls single submission and receipts.

Sources: [bili-cli](https://github.com/kukemc/bili-cli), [Douyin video-create documentation](https://open.douyin.com/platform/resource/docs/openapi/video-management/douyin/create/create-video), [social-auto-upload pinned source](https://github.com/dreammis/social-auto-upload/tree/3847fff01f5434a8a32829833163cd6723cabc59).

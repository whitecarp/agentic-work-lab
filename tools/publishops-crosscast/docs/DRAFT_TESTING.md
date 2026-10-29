# 草稿測試與隱私範圍 / Draft tests and audience boundaries

版本 / Version: **0.2.2 alpha**, 2026-10-29.

Instagram 私密帳號及 X 保護帳號仍可能讓已獲准的追蹤者看到內容，不能當作「僅自己可見」。首次設定時區分「自動發布」和「僅預覽／草稿測試」；沿用已明確選定的範圍，不把登入當成發布授權，也不為測試改整個帳號的隱私設定。

Instagram private accounts and X protected accounts can still expose posts to approved followers; they are not self-only publication. Distinguish automatic publication from preview/draft testing during onboarding, preserve that choice, and do not change account-wide privacy just to test. Signing in is not publication authorization. [X 官方受眾說明 / Official X audience guidance](https://help.x.com/en/safety-and-security/public-and-protected-posts) · [Meta 私密帳號說明 / Meta private-account guidance](https://about.fb.com/news/2021/07/instagram-safe-and-private-for-young-people/).

## CLI

```sh
vpub init --video final.mp4 --language zh-Hans --platforms instagram,x --draft-only --out drafts.json
# Host prepares English metadata, observes account identities, and sets expected_identity.
vpub plan drafts.json
vpub publish drafts.json --dry-run
vpub publish drafts.json --confirm CURRENT_PLAN_HASH
vpub browser request RECEIPT_KEY
vpub browser begin RECEIPT_KEY --identity OBSERVED_ACCOUNT
```

`--draft-only` 在所選 Instagram／X 選項保存 `submission_mode: draft_only`；其他平台會拒絕這個旗標，不悄悄降級成公開發片。Instagram 專用封面請使用獨立方案或平台專用 `covers`；X 不能接受共用自訂封面。

The flag stores `submission_mode: draft_only` for selected Instagram/X platforms. Other platforms reject it instead of silently publishing. Use a separate Instagram plan or platform-specific `covers` for custom artwork; X rejects a shared custom thumbnail.

`publish` 只建立持久交接，`begin` 在選檔前記錄本次嘗試。`final_submission_allowed: false` 要求宿主停在預覽或保存草稿，**不得點 Share／Post 或排期**。CLI 不控制瀏覽器按鈕，必須由執行 Agent 遵守。帳號受眾欄位仍是 `visibility: account`，不偽造平台不存在的 private 選項。

The commands create a durable handoff and record the attempt before file selection. A false final-submission flag requires the host to stop at preview or saved draft: **never Share/Post or schedule**. The CLI cannot disable browser controls; the executing agent must honor the flag. Visibility remains account-based rather than inventing a per-post private option.

## 本次實測 / Live observations

| 平台 / Platform | 驗證內容 / Observed | 結果與限制 / Result and limit |
|---|---|---|
| Instagram | 79 秒橫式原片、製作專用封面、1327/2200 英文配文、AI 標籤；Facebook 同步與定時關閉 / 79-second landscape source, production cover, English caption, AI label; Facebook sharing and scheduling off | 最終預覽可見。退出僅有放棄／取消，提示不保存，已取消退出。未點分享，未確認伺服器完整上傳或持久草稿 / Final preview visible; exit offered Discard/Cancel and warned edits would be lost. Cancel retained preview. No Share, server-upload completion or persistent draft verified |
| X | 160,441,478-byte 原片上傳至 Ready；英文文案及 Made with AI / Source uploaded to Ready with English text and Made with AI | Close → Save；Unsent posts 列表顯示影片 1:18。重新打開後 Ready、全文及 AI 標記仍在。未點 Post / Saved to Unsent posts; 1:18 video displayed. Reopened with Ready media, exact text and AI label retained. No Post |

上表是首次草稿階段。其後使用者明確改為發布：X 從同一草稿提交一次，原帖作者、完整英文文案、Made with AI 與 1:19 嵌入影片已讀回；完整播放與未登入存取未驗證。Instagram 點分享後顯示「無法分享帖子」，未盲目重試、沒有成功作品 ID。使用者要求先處理 X，且暫不刪除。兩站 API 與跨裝置草稿同步未驗收。影片語音與內嵌字仍為原語言，英文適配指配文。

The table records the initial draft stage. The user then explicitly authorized publication: X was submitted once from that draft, and the original status was read back with matching author, exact English text, Made with AI and a 1:19 embedded video. Full playback and anonymous access remain unverified. Instagram Share returned an error; no blind retry or successful work ID was recorded. The user prioritized X and requested no deletion. APIs and cross-device draft synchronization remain unverified. English adaptation refers to metadata, not translated audio or baked-in text.

## 使用者改為發布 / Explicit change to publication

只有使用者明確改變要求後才執行： / Run only after explicit operator authorization:

```sh
vpub browser authorize-publish RECEIPT_KEY --identity OBSERVED_ACCOUNT
# Resume the same preview/draft. Never begin or select the video again.
# Submit once, capture its work ID, reopen it and record publication evidence.
```

命令保留原草稿回執和授權時間，更新方案雜湊及 final_submission_allowed；不呼叫平台、不自動發布、不改帳號受眾。拒絕錯誤帳號、尚未核驗的草稿及重複授權。作品發出後，草稿階段的 published:false 只是歷史紀錄；以最新回執為準。刪除是另外一項操作，本輪未執行。

This preserves the draft receipt and authorization timestamp, updates the plan hash and submission flag, and performs no platform action or audience change. Wrong accounts, unverified drafts and repeated authorization are rejected. After publication, the earlier unpublished receipt is historical; consult the current receipt. Deletion is a separate action and was not performed.

## 宿主操作 / Host procedure

- Instagram：Create → 選擇影片 → 按實際內容選原比例 → Edit 的封面檔案選擇器 → Next → 配文／AI 標籤。不要將 Reels 提示或「分享」當成保存草稿。 / Select the original ratio, load production artwork in Edit, then fill caption and AI label. Share is not Save draft.
- X：Add photos or video 可能先開選單，再選 Upload 才出現檔案選擇器。等待 Ready，設定 Content disclosure → Made with AI，Close → Save，再由 Drafts → Unsent posts 開回同一份草稿。 / The media button may open a menu first; choose Upload. Wait for Ready, set AI disclosure, save on close and reopen from Unsent posts.
- X 的本次 Edit media 只觀察到字幕及內容警告，未看到自訂縮圖入口；仍標記不支援，不宣稱專用封面已套用。 / The tested X editor offered captions and content warnings, with no custom-thumbnail control observed. Custom thumbnails remain unsupported.
- 文案核驗需保留段落；X 的可見 Draft.js 段落讀取可避免 `innerText` 額外空行造成誤判。不讀取隱藏帳號狀態或 Cookie。 / Verify visible editor paragraphs: X innerText can insert extra blank lines. Read rendered text blocks, never hidden account state or cookies.
- 上傳逾時先檢查同一頁，不重新 begin 或重傳。未確認保存就記預覽，不把「按過保存」當成成功證據。 / Inspect the same page after timeouts; do not begin/upload again. Only claim a saved draft after read-back.

## 回執 / Receipts

```json
{
  "outcome": "draft",
  "page_url": "https://x.com/compose/post",
  "identity": "OBSERVED_HANDLE",
  "post_text": "EXACT_APPROVED_AND_OBSERVED_TEXT",
  "media_preview": true,
  "draft_saved": true,
  "observation": "Describe the actual upload status, saved-draft list and reopened media. Never use this example as evidence."
}
```

`draft_saved: false` → `PREVIEW_READY`；`true` → `DRAFT_SAVED`。兩者都帶 `published: false`，不填作品 ID。驗證要求原帳號、官方頁、精確配文及明確保存狀態；draft-only 不能記成 verified publication。CLI 記錄的是宿主提供的 UI 證據，不是獨立平台查詢。相同影片重跑會去重，不因「尚未發布」而重傳。

False means PREVIEW_READY; true means DRAFT_SAVED. Both are unpublished and have no published work ID. Validation checks account, official URL, exact caption and explicit save status; draft-only cannot become a verified publication receipt. Evidence is supplied by the host, not independently queried by the CLI. Repeated runs deduplicate the same media even when unpublished.

## 登入恢復後的實發 / Publication after reauthentication

Instagram 初次失敗後，使用者重新登入。原帳號的 4 條舊作品中沒有測試片；以新查驗及原錯誤作為證據，記錄 rejected 並執行一次恢復。重試成功，帖子數增為 5，原作品英文全文、AI 標記和影片畫面已讀回。專用橫式封面確已套用，但主頁直式網格裁切文字；完整封面版面驗收仍未通過。X 和 Instagram 作品均保留，未測刪除。

After reauthentication, the original account still had only four old posts and no test video. The fresh check plus the explicit error permitted one recorded recovery. Retry succeeded, the count became five, and the original reel's exact English caption, AI label and video scene were read back. Production artwork is applied, but the portrait profile grid crops text; full cover-layout acceptance remains incomplete. Both works remain published; deletion was not tested.

```sh
# Only after explicit rejection and a fresh same-account absence check:
vpub browser retry-rejected RECEIPT_KEY --identity OBSERVED_ACCOUNT --evidence retry-evidence.json
vpub browser begin RECEIPT_KEY --identity OBSERVED_ACCOUNT
```

retry evidence 僅含 identity、官方個人頁 page_url、work_absent:true、短 observation。未知提交或已知作品 ID 一律拒絕重試；保存原拒絕回執，重新 begin 後才選回傳快照。這不是自動重試迴圈。 / Retry evidence contains only identity, official profile page_url, work_absent:true and a short observation. Unknown submissions or known work IDs cannot retry. Prior rejection is retained; begin before selecting the returned snapshot. This is not an automatic retry loop.

X 片頭加入封面作為縮圖候選目前只屬待測方案，尚未實作，也不保證平台取第一幀。 / Inserting artwork at the start as an X thumbnail candidate remains a proposed test, not an implemented feature or a guarantee that X selects frame zero.



## 後續狀態 / Subsequent status — 0.2.3

使用者後續明確授權刪除 Instagram 測試作品，已確認刷新後不存在。替換上傳停在發布前封面檢查，尚無新作品 ID。X 原帖仍保留；使用者訂閱後改用 Media Studio 自訂封面方向，取消片頭封面方案，但媒體庫初始化尚未成功。此段取代以上歷史狀態中的下一步計畫。

The operator subsequently authorized deleting the Instagram test work; refreshed absence was verified. Replacement remains at an unpublished cover preview with no new work ID. The X status is retained. Following subscription, Media Studio replaces the cancelled intro-cover approach, but library setup has not succeeded. This supersedes next-step plans in the historical entries above.

# 功能對照與實作狀態 / Feature mapping and implementation status

目標參考 [bili-cli](https://github.com/kukemc/bili-cli)：讓 Agent 能理解內容、管理作品、讀取營運訊息並在授權內互動。以下區分現在可用與待實作，不能將計畫列成完成。

The reference is [bili-cli](https://github.com/kukemc/bili-cli): content understanding, work management, operational messages and authorized interaction. Plans below are not completed features.

| 能力 / Capability | Bilibili | YouTube | Douyin | X (Twitter) | Instagram |
|---|---|---|---|---|---|
| 官方頁登入 / Official-page login | Native handoff | Native handoff | Native handoff | Native handoff | Native handoff |
| 跨平台方案與去重 / Plan and dedup | Implemented | Implemented | Implemented | Implemented | Implemented |
| 內置瀏覽器發布交接 / Native upload handoff | Implemented | Implemented | Implemented | Implemented | Implemented |
| 獨立發布後端 / Standalone uploader | bili-cli | Optional API | Experimental browser | Not implemented | Not implemented |
| 真實發片驗收 / Live upload acceptance | Existing work retained | One private native upload verified | One private work; chapter visibility user-confirmed | One status read back; full playback unverified | One reel read back; cover-grid crop unresolved |
| YouTube 卡片／可點擊片尾 / Cards and clickable end screens | Not applicable | Not implemented | Not applicable | Not applicable | Not applicable |
| 搜尋／推薦 / Discovery | Existing bili | Planned | Planned | Planned | Planned |
| 作品／字幕／內容理解 / Content understanding | Existing bili | Planned, platform permitting | Planned, platform permitting | Planned, platform permitting | Planned, platform permitting |
| 評論／收件箱／訊息 / Comments and inbox | Existing bili | Platform-specific adaptation planned | Planned | Planned | Planned |
| 互動與發布後修改 / Interaction and editing | Existing bili | Native metadata/cover edits | Native edits; review pending | Planned | Planned |
| 創作者數據 / Creator analytics | Existing bili | Planned | Planned | Planned | Planned |

**Native handoff 是需要 Agent 操作網頁的流程，不是 CLI 已取得資料或已上傳的宣告。** B站現有能力使用安裝後的 `bili`；本工具不宣稱重新實作或驗收了它所有命令。

**Native handoff requires agent-driven UI work; it is not a claim that CLI retrieval or upload has occurred.** Existing Bilibili capabilities use installed `bili`; this project does not claim to reimplement or validate every upstream command.

平台適配原則：B站投幣／三連／彈幕不硬套其他平台；Instagram 使用 caption 與帳號受眾；YouTube 使用影片可見範圍、兒童與合成內容聲明；X 使用單一帖文、帳號受眾與 status ID；TikTok 已暫停。跨平台數據保留平台、採樣時間與各自定義，不把不同播放口徑當成同一指標。

Adapt platform concepts rather than copying Bilibili coins/triples/danmaku everywhere. Instagram uses captions and account audience; YouTube has visibility, kids and synthetic-content controls; X uses a single post body, account audience and status ID; TikTok is suspended. Analytics must retain platform, sampling time and metric definitions rather than treating differently defined views as equivalent.

下一步依序：完成已登入帳號的原創影片驗收、整合作品與數據讀取、再增加經使用者授權的評論／回覆。每項都需真實回讀、錯誤碼、去重及平台對應測試。

Next: accept original-video publication on signed-in accounts, integrate works/analytics reads, then add explicitly authorized comments/replies. Each needs real read-back, error handling, deduplication and platform-specific tests.

[X 操作說明 / X guide](X_GUIDE.md) · [TikTok 暫停及協助測試 / TikTok suspension and volunteer testing](TIKTOK_STATUS.md)

## 0.2.0：跨平台內容設定 / Cross-platform content settings

| 能力 / Capability | 本版範圍 / This release |
|---|---|
| 文案生成 / Copywriting | Host-model request/apply, source-bound validation; no independent model API |
| 語言 / Language | Bilibili/Douyin → Simplified Chinese; YouTube/Instagram/X → English; bilingual opt-in |
| 封面 / Covers | Production artwork → platform canvases; no cropping; immutable snapshots and read-back per variant |
| 現有作品修改 / Existing-work edits | Metadata/cover changes retain the original work ID; privacy reduction preserved after actual read-back |
| 播放列表 / Playlists | YouTube native fields and optional API membership operations; Bilibili collections remain upstream |
| 郵件 / Email | Off by default; host Gmail/Outlook send-tool handoff, claims and durable results; no SMS |
| 章節 / Chapters | Native handoff; Douyin chapters submitted and mobile visibility user-confirmed |

詳見 [README](../README.md) 與當前 [驗證記錄](VALIDATION.md)，上方早期能力表中的發布驗收以此記錄為準。

See README and current validation for the latest acceptance status; these supersede historical live-upload entries above.

## 0.2.2：草稿與明確發布 / Drafts and explicit publication

Instagram／X 可用 `init --draft-only`，區分預覽與保存草稿；使用者明確改為發布後才用 `browser authorize-publish` 繼續同一份內容，不重傳。X 的草稿及原帖已實測，Instagram 首次提交失敗，重新登入及核對無重複後重試成功；封面網格裁切仍待適配。 / Draft-only mode distinguishes preview from saved drafts. Explicit operator authorization can enable submission of the same draft without reuploading. X draft and status were tested; Instagram succeeded after reauthentication and a rejection/absence-checked retry; profile-cover cropping remains unresolved. See [validation](VALIDATION.md).


## 0.2.3：刪除回執與封面限制 / Deletion receipts and cover limits

Instagram 新增明確刪除交接、確認結果與刷新後缺席證據、保留歷史的同片封面重發流程。已實測刪除；替換影片尚未分享。X 自訂封面改走需要訂閱權限的 Media Studio；本次媒體庫初始化失敗，封面修改未驗收。 / Instagram now has explicit deletion handoff, acknowledgement/absence evidence and history-preserving same-video cover replacement. Deletion was exercised; replacement has not been shared. X custom thumbnails use subscription-gated Media Studio; this session hit library initialization failure, so thumbnail changes remain unverified. [操作指南 / Guide](DELETION_AND_COVERS.md).


## 0.2.4：Instagram 自動片頭與 X Studio 實測 / Automatic Instagram lead-in and X Studio validation

`instagram prepare` 自動判斷：橫式加入 1 秒原封面，直式／方形原片通過；維持原比例、原檔、可追溯雜湊及重跑復用。X 原橫式封面已透過 Studio 保存及發布；這是宿主網頁操作，不是新建了獨立 API。前面的 0.2.3 段落是歷史測試狀態，以最新 [驗證記錄](VALIDATION.md) 為準。

Instagram preparation automatically adds a one-second production-cover lead to landscape, passes portrait/square through, and retains original ratio, inputs, hashes and rerun reuse. X's landscape cover was saved/published through Studio UI, not a new standalone API. Earlier 0.2.3 entries are historical; consult current validation.

# 功能對照與實作狀態 / Feature mapping and implementation status

目標參考 [bili-cli](https://github.com/kukemc/bili-cli)：讓 Agent 能理解內容、管理作品、讀取營運訊息並在授權內互動。以下區分現在可用與待實作，不能將計畫列成完成。

The reference is [bili-cli](https://github.com/kukemc/bili-cli): content understanding, work management, operational messages and authorized interaction. Plans below are not completed features.

| 能力 / Capability | Bilibili | YouTube | Douyin | TikTok | Instagram |
|---|---|---|---|---|---|
| 官方頁登入 / Official-page login | Native handoff | Native handoff | Native handoff | Native handoff | Native handoff |
| 跨平台方案與去重 / Plan and dedup | Implemented | Implemented | Implemented | Implemented | Implemented |
| 內置瀏覽器發布交接 / Native upload handoff | Implemented | Implemented | Implemented | Implemented | Implemented |
| 獨立發布後端 / Standalone uploader | bili-cli | Optional API | Experimental browser | Not implemented | Not implemented |
| 真實發片驗收 / Live upload acceptance | Pending | Pending | Pending | Pending | Pending |
| 搜尋／推薦 / Discovery | Existing bili | Planned | Planned | Planned | Planned |
| 作品／字幕／內容理解 / Content understanding | Existing bili | Planned, platform permitting | Planned, platform permitting | Planned, platform permitting | Planned, platform permitting |
| 評論／收件箱／訊息 / Comments and inbox | Existing bili | Platform-specific adaptation planned | Planned | Planned | Planned |
| 互動與發布後修改 / Interaction and editing | Existing bili | Planned | Planned | Planned | Planned |
| 創作者數據 / Creator analytics | Existing bili | Planned | Planned | Planned | Planned |

**Native handoff 是需要 Agent 操作網頁的流程，不是 CLI 已取得資料或已上傳的宣告。** B站現有能力使用安裝後的 `bili`；本工具不宣稱重新實作或驗收了它所有命令。

**Native handoff requires agent-driven UI work; it is not a claim that CLI retrieval or upload has occurred.** Existing Bilibili capabilities use installed `bili`; this project does not claim to reimplement or validate every upstream command.

平台適配原則：B站投幣／三連／彈幕不硬套其他平台；Instagram 使用 caption 與帳號受眾；YouTube 使用影片可見範圍、兒童與合成內容聲明；TikTok 與抖音分開處理帳號、地區能力與創作者介面。跨平台數據保留平台、採樣時間與各自定義，不把不同播放口徑當成同一指標。

Adapt platform concepts rather than copying Bilibili coins/triples/danmaku everywhere. Instagram uses captions and account audience; YouTube has visibility, kids and synthetic-content controls; TikTok and Douyin have separate account/regional/creator capabilities. Analytics must retain platform, sampling time and metric definitions rather than treating differently defined views as equivalent.

下一步依序：完成已登入帳號的原創影片驗收、整合作品與數據讀取、再增加經使用者授權的評論／回覆。每項都需真實回讀、錯誤碼、去重及平台對應測試。

Next: accept original-video publication on signed-in accounts, integrate works/analytics reads, then add explicitly authorized comments/replies. Each needs real read-back, error handling, deduplication and platform-specific tests.

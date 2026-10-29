# 驗證狀態 / Validation status

日期 / Date: **2026-10-29**, PublishOps CrossCast 0.2.0 alpha.

- **55 項離線測試通過 / 55 offline tests passed:** source/plan integrity, media snapshots, duplicate suppression, durable IDs, native-account checks, language routing, immutable privacy, optional email with single claim and uncertain-send suppression, cover fitting with no cropping, per-platform cover evidence, and playlist membership/deduplication. Tests use synthetic media and mocked services, not live posts or mail.
- **YouTube:** 一條原創影片的內置瀏覽器私密上傳已驗證。最新簡介改為簡體中文＋英文、無外鏈，已保存並讀回。專用封面及私密的 GPT Duty Log 播放列表已保存，畫面讀回一致。 / One original native private upload was verified. The latest Simplified Chinese/English description without external links was saved and read back. The dedicated cover and private GPT Duty Log playlist were saved and read back in Studio.
- **Douyin:** 只有一條測試作品，使用者改為私密，本輪再次確認。已提交製作工程重排的 4:3／3:4 封面及簡體無外鏈簡介，管理頁顯示「修改審核中」，仍展示舊文案；不能宣稱新封面／文案已通過。 / One test work remains private. Dedicated 4:3/3:4 covers and Simplified Chinese metadata without links were submitted; management shows modification under review and previous metadata, so the new content is not yet accepted evidence.
- **Bilibili:** 沿用既有上游 CLI 與原作品，本輪不重傳、不接管營運。舊停止狀態不再視為當前狀態。 / Existing upstream CLI/work retained; no reupload or operations takeover. Historical stop-file state is not presented as current.
- **Instagram／X:** 先前確認登入及部分入口；本輪無影片發布。Instagram 封面只驗證本地生成，X 自訂縮圖未實作。 / Sign-in and some entry points were previously observed; no publication this round. Instagram cover generation is local-only validation; X custom thumbnails are unimplemented.
- **郵件 / Email:** Gmail／Outlook 交接、去重、結果判定經離線測試；沒有寄送測試郵件，未驗證送達。寄信工具必須當下可用。 / Handoff, deduplication and result classification have offline tests; no test email was sent and delivery is unverified. A send tool must actually be available at execution.
- **YouTube API 播放列表 / API playlists:** 權限檢查、查詢後加入及不確定寫入不重試經模擬測試；尚無真實 API 驗收。 / Scope checks, list-before-insert and uncertain-write suppression have mock tests, not live API acceptance.
- **章節 / Chapters:** 本集 B站既有章節可作為同片時間點來源；抖音章節尚未套用。不能把簡介時間碼當作平台章節已保存。 / Existing Bilibili chapters provide timing for this same episode; Douyin chapters are not applied. Description timestamps alone do not prove native chapters were saved.
- **未完成 / Not completed:** YouTube cards/clickable end screens; X/Instagram APIs; unified analytics/comments; clean-machine/cross-OS installation acceptance. TikTok remains suspended.

所有私密回執、登入資料、原始頁面和測試帳號資訊留在本機，未包含於本次公開程式更新。 / Private receipts, sign-in data, raw pages and test-account details stay local and are excluded from this code update.

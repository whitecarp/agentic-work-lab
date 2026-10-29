# 驗證狀態 / Validation status

日期 / Date: **2026-10-29**, PublishOps CrossCast 0.2.5 alpha.

## 0.2.5 最新狀態 / Latest status

使用者提供兩段手機錄屏並回報：一秒片頭沒有解決需求，手機可從相機膠卷選直式封面。已抽查錄屏中的編輯封面及返回作品畫面；這不是 Agent 控制手機或全程播放驗收。撤回片頭功能，原影片發布與抖音共用圖片交接已實作；本輪沒有為更新規則重新發布測試片。

The operator supplied two phone recordings and reported that the one-second lead did not solve the need, while camera-roll portrait covers worked. Sampled frames show cover editing and return to the work; this is not agent-operated phone control or full-playback acceptance. Lead-in functionality is removed. Original-video/shared-artwork handoff is implemented; this rule update did not republish the test work.

72 項測試通過；目前新增的四項測試核對原影片 SHA-256 不變、精確重用抖音直式圖、方案隔離／去重、覆蓋防護及無圖片時不偽造手機成功。0.2.4 的 FFmpeg 實驗測試屬歷史記錄，不代表該功能仍在使用。

72 tests pass. Four current Instagram tests check unchanged video SHA-256, exact Douyin portrait reuse, scoped/idempotent manifests, overwrite guards and truthful missing-artwork state. The 0.2.4 FFmpeg test is historical, not an active publishing feature.

## 先前實測記錄 / Earlier live-test records

- **72 項離線測試通過 / 72 offline tests passed:** source/plan integrity, media snapshots, duplicate suppression, durable IDs, native-account checks, language routing, immutable privacy, optional email with single claim and uncertain-send suppression, cover fitting with no cropping, per-platform cover evidence, and playlist membership/deduplication. Added draft receipts, no-publication guards, explicit same-draft authorization, scoped CLI initialization and rejection-only recovery with fresh absence evidence. Tests use synthetic media and mocked services, including original-video/shared-cover handoff tests; they do not create live posts or mail.
- **YouTube:** 一條原創影片的內置瀏覽器私密上傳已驗證。最新簡介改為簡體中文＋英文、無外鏈，已保存並讀回。專用封面及私密的 GPT Duty Log 播放列表已保存，畫面讀回一致。 / One original native private upload was verified. The latest Simplified Chinese/English description without external links was saved and read back. The dedicated cover and private GPT Duty Log playlist were saved and read back in Studio. 類別已由「人物和博客」改為「電影和動畫」，保存後選項一致，保存按鈕停用。 / Category changed from People & Blogs to Film & Animation; the saved selection and disabled Save button were observed.
- **類別修復 / Category fix:** 新方案要求明確類別、宿主按內容填寫、原地修改與回執比對均有離線測試。API 類別可用性及不一致回執為模擬測試。 / Explicit plans, host selection, in-place revision and receipt matching are offline-tested; API assignability and mismatch handling use mocks.
- **Douyin:** 同一條作品維持私密。管理頁已回顯新簡體無外鏈文案及專用直式封面；本輪未重新開啟橫式封面核驗。四個章節已提交；首次回讀為審核中，之後使用者確認手機版可見章節。 / The same work stays private. Management now shows the revised Simplified Chinese caption without URLs and the dedicated portrait cover. Landscape cover was not freshly rechecked in this round. Four chapters were submitted. Initial read-back reported review; the user subsequently confirmed mobile chapter visibility.
- **Bilibili:** 沿用既有上游 CLI 與原作品，本輪不重傳、不接管營運。舊停止狀態不再視為當前狀態。 / Existing upstream CLI/work retained; no reupload or operations takeover. Historical stop-file state is not presented as current.
- **Instagram 初次發布（歷史） / Initial publication (historical):** 原比例影片、專用封面、英文配文及 AI 標籤已發布並讀回。首次 Share 明確失敗，使用者重新登入；核對原帳號僅有舊作後重試一次成功，保留了失敗與恢復記錄。官方 AI 內容標記、全文及影片畫面可見。專用橫式封面已套用，但首頁直式網格裁掉部分文字，版面適配待修。 / Original-ratio video, production cover, English caption and AI label were published and read back. Initial Share failed; after user reauthentication and a fresh same-account absence check, one retry succeeded, retaining failure/recovery history. The AI Content badge, exact caption and video scene were visible. The selected landscape cover is present, but profile-grid cropping cuts some text; grid fitting remains unresolved.
- **X:** 真實影片上傳達 Ready；草稿保存並重新開啟成功。其後依明確授權發布一次，原帖作者、完整英文文案、Made with AI 及 1:19 嵌入影片已讀回。未完整播放、未測未登入存取，未刪除。自訂縮圖仍未實作。 / Real upload reached Ready and survived draft save/reopen. After explicit authorization it was submitted once; the original status showed the matching author, exact English text, Made with AI and a 1:19 embedded video. Full playback and anonymous access were not tested. No deletion; custom thumbnails remain unimplemented. [流程 / Procedure](DRAFT_TESTING.md).
- **郵件 / Email:** Gmail／Outlook 交接、去重、結果判定經離線測試；沒有寄送測試郵件，未驗證送達。寄信工具必須當下可用。 / Handoff, deduplication and result classification have offline tests; no test email was sent and delivery is unverified. A send tool must actually be available at execution.
- **YouTube API 播放列表 / API playlists:** 權限檢查、查詢後加入及不確定寫入不重試經模擬測試；尚無真實 API 驗收。 / Scope checks, list-before-insert and uncertain-write suppression have mock tests, not live API acceptance.
- **章節 / Chapters:** 已實測「編輯作品」懸停選單內的「添加章節／編輯章節」。00:00、00:19、00:46、00:57 的列表已核對後提交；使用者已確認手機版可見章節，Agent 尚未完成審核後完整列表讀回。 / The hover menu entry was exercised. Four starts were checked before submission; the user confirmed mobile visibility; full post-review list inspection by the agent remains pending. [操作說明 / Procedure](CATEGORIES_AND_CHAPTERS.md).
- **未完成 / Not completed:** YouTube cards/clickable end screens; X/Instagram APIs; unified analytics/comments; clean-machine/cross-OS installation acceptance. TikTok remains suspended.

所有私密回執、登入資料、原始頁面和測試帳號資訊留在本機，未包含於本次公開程式更新。 / Private receipts, sign-in data, raw pages and test-account details stay local and are excluded from this code update.


## 0.2.3 歷史狀態 / Historical status

- **Instagram 刪除 / Deletion:** 使用者明確授權刪除上述測試作品。確認對話框後回到個人主頁；刷新後作品數由 5 變 4，原作品連結不存在。刪除回執及原發布回執保留。未測試最近刪除的還原或永久清除。 / The operator explicitly authorized deletion of that test work. Confirmation returned to the profile; refresh showed 4 rather than 5 posts and no original work link. Both receipts are retained. Recovery and permanent purge were not tested.
- **Instagram 重發 / Replacement:** 原影片重新上傳到發布前預覽，未分享。直接換成抖音直式封面仍在橫式預覽截字；使用者先自行查看 Instagram，重發驗收未完成。 / The original video was uploaded to an unpublished preview. A dedicated Douyin portrait cover was still cropped in the landscape preview. The operator is inspecting Instagram; replacement publication is incomplete.
- **X Media Studio:** 使用者訂閱後可以進入，但首次提示關閉及刷新後媒體庫仍報初始化失敗。未保存自訂封面、未刪除或重發 X 帖子。 / After subscription Studio opened, but its library reported a setup failure after onboarding dismissal and reload. No custom thumbnail was saved; the existing X post was neither deleted nor reposted.
- **新增測試 / Added tests:** 錯帳號／作品、缺少刪除確認或刷新後缺席證據、重發過早、變更影片／設定、重複重發均會拒絕；有效重發保留歷史與封面快照。 / Tests reject wrong identities/IDs, missing deletion or absence evidence, premature/duplicate reposts and changed video/settings; the allowed path retains history and cover snapshots.


## 0.2.4 歷史實驗 / Historical experiment

- **Instagram 原比例重發 / Original-ratio replacement:** 原橫式版成功分享並讀到新作品連結；使用者隨後在手機刪除，刷新主頁確認缺席。再依需求建立 1 秒原封面＋完整正片的版本，1920×1080、約 79.5 秒；抽查 0.5 秒與 1.2 秒分別為封面與正片。已選「原版」、英文配文和 AI 標記後提交，新版本顯示分享成功並讀到新作品連結；已讀回完整 1327 字元配文、原生 AI 標記及使用原封面的主頁縮圖（仍有網格裁切）。手機選封面仍待使用者驗收。 / The original-ratio replacement was shared, then deleted by the operator; a refreshed profile confirmed absence. The new 1920×1080 cover-lead version is about 79.5 seconds; sampled frames show cover at 0.5 s and original footage at 1.2 s. Original crop, English caption and AI disclosure were selected before sharing; success and a new work link were observed. The exact 1327-character caption, native AI badge and cover thumbnail were read back; the profile grid still crops the landscape artwork. Mobile cover selection remains operator acceptance.
- **X Studio:** 直接上傳入庫成功，原 16:9 封面保存並在重開詳情後保留。新增明確授權後發布新帖，作者、英文文案、1:19 影片與橫式封面讀回；舊帖保留。Studio 新帖未見原生 Made with AI 標記，文案內 AI-generated 已保留；完整播放未驗收。 / Direct library upload and persistent original 16:9 thumbnail succeeded. Newly authorized publication was read back with matching author, text, 1:19 video and cover; the earlier post remains. Text disclosure is present but the native AI badge was not observed. Full playback remains unverified.
- **新增測試 / New tests:** 直式／方形不修改、僅輸出 Instagram 方案、橫式缺封面拒絕、原檔不覆蓋、無效時長、封面變更與重複片頭防護，以及實際 FFmpeg 成片時長／音軌／影格色彩與重跑校驗。 / Tests cover portrait/square passthrough, Instagram-only output, missing artwork, overwrite/duration guards, changed covers, duplicate lead prevention, and real FFmpeg duration/audio/frame-colour/reuse checks.

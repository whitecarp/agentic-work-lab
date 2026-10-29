# 類別與章節 / Categories and chapters

## YouTube 類別 / YouTube category

每支影片依內容選類別，不能因為使用 AI 就一律選科技，也不能默認「人物和博客」。本次 GPT 角色動畫敘事短片已由「人物和博客」改為「電影和動畫」，Studio 已保存；這只是本片的選擇，不是所有影片的預設。

Choose a category from each video's content. AI use alone does not make a video a technology demonstration. The tested GPT character narrative was changed from People & Blogs to Film & Animation and saved in Studio. This is a choice for that video, not a universal default.

從 0.2.1 起，新 YouTube 方案必須有字串 `category_id`。`vpub init` 刻意不填預設；宿主透過 `metadata request/apply`，結合腳本及當前 Studio／API 類別選項填寫。使用者已指定的類別會保留，文案生成不能擅改。這是機器校驗，不是額外強制人工審核。

Since 0.2.1, new YouTube plans require a string `category_id`. `vpub init` leaves it unset. The host supplies it through `metadata request/apply`, using the script and current Studio/API choices. Copywriting preserves an explicit selection. This validation does not add mandatory editorial approval.

- 內置瀏覽器：詳細資訊 → 展開／顯示高級設定 → 類別 → 保存；核對保存狀態及選項。現有影片用 `browser revise` 原地修改，回執必須匹配 `category_id`。 / Native: Details → Show more → Category → Save, then inspect the saved choice. Use `browser revise` for an existing work; its receipt must match `category_id`.
- API：先查所選類別是否可指定，才上傳；讀回類別不符時不能標為驗證成功。此 API 路徑目前為模擬測試。 / API: check assignability before upload and reject mismatched read-back as unverified. This path is mock-tested.
- 舊版回執可繼續讀取；重新產生或修改方案時補上明確類別，不清空歷史、不重傳影片。 / Existing receipts remain readable; supply an explicit category when rebuilding or revising a plan, without clearing history or reuploading.

官方參考 / Official references: [videoCategories.list](https://developers.google.com/youtube/v3/docs/videoCategories/list), [videos.insert](https://developers.google.com/youtube/v3/docs/videos/insert).

## 抖音章節 / Douyin chapters

實測入口（2026-10-29）：內容管理 → 對應作品 → 懸停「編輯作品」→「添加章節」。已有章節時變成「編輯章節」。直接點主文字可能只進入作品資料編輯頁。

Observed on 2026-10-29: Content management → the intended work → hover Edit work → Add chapters. Existing chapters use Edit chapters. Clicking the main label can open ordinary metadata editing instead.

1. 使用同一支成片及字幕的真實時間點；不要從別的版本照搬。 / Use timings from the same final cut and transcript.
2. 點時間軸、按「手動添加」，填標題後按「確定」。每章完成後，表單會關閉，下一章需要重新打開。 / Seek, choose Manual add, enter the title and confirm. Reopen the form for each chapter.
3. 標題最多 12 個 UTF-16 單位；目前 CLI 不自動截字。時間軸顯示與章節列表可能因小數秒取整相差一秒，必須核對列表中的最終整秒。 / Titles allow at most 12 UTF-16 units; CLI does not truncate them. Fractional seeking can round differently in the chapter list; verify its final whole-second starts.
4. 核對全部章節，再按整體「保存」。若顯示「章節要點正在審核中」，記為已提交待審核，保留作品 ID，不重新提交、不重新上傳。 / Check the full list, then Save. A chapter-review notice means submitted pending review; keep the work ID and do not resubmit or reupload.
5. 審核後重新打開「編輯章節」，讀回全部時間、標題及外顯進度條設定，才能記錄已保存驗證。 / After review, reopen Edit chapters and read every start/title and the visible progress-bar setting before recording verified evidence.

本次四章（00:00／00:19／00:46／00:57）已提交，首次讀回顯示平台審核中；之後使用者已確認抖音手機版可見章節；桌面端完整讀回尚未補驗。本次作品維持私密。影片文案與章節為簡體中文；本文件使用繁體中文＋英文。

Four chapters at 00:00 / 00:19 / 00:46 / 00:57 were submitted. The first read-back showed platform review, then the user confirmed that chapters are visible in the Douyin mobile app. A full desktop read-back has not yet been repeated. The work stays private. Video copy and chapter titles use Simplified Chinese; this documentation uses Traditional Chinese and English.

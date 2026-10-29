# 智能體工作實驗室

**繁體中文** · [English](README_EN.md)

### 從日常任務到可複用的工作流程

探索 AI 如何協助資料研究、內容製作與營運，把實作經驗整理成可重跑、可檢查、可交接的方法。

🎬 [Bilibili：穿睡衣的小白白](https://space.bilibili.com/10706217) · 📦 [影片下載](https://github.com/whitecarp/agentic-work-lab/releases) · [影片目錄](media/README.md) · [營運資料](data/README.md)

## PublishOps CrossCast

**跨平台影片發布營運台**，CLI 指令為 `vpub`。

為同一支原創影片準備不同平台的簡介、封面與發布回執。使用者選擇平台，首次登入後由 CLI 復用本機專用會話。AI 聊天模型協助寫作，CLI 核對素材與帳號、上傳、套用封面並記錄結果；YouTube 使用官方 API，抖音、Instagram、X 使用專用背景瀏覽器。

- 依影片語言保留原文並適配平台：中文內容在 B站／抖音使用簡體，其餘內容加簡體翻譯；非英文內容在 YouTube／Instagram／X 保留原文並加英文。
- 抖音／Instagram 共用專用直式封面；v3 已由使用者確認兩站手機預覽中文字可見，其他版面仍需檢查。
- X 發布僅面向已訂閱、且帳號實際能進入 Media Studio 的使用者。未訂閱帳號的影片上傳限 140 秒、512 MB，並缺少本工具依賴的 Studio 媒體管理與自訂封面權限；因此 X 後端需要上述存取權。[上傳限制](https://help.x.com/en/using-x/x-videos) · [Studio 權限](https://help.x.com/en/using-x/media-studio)。
- Instagram 使用原片及原比例，上傳時另設封面；手機可選與抖音共用的直式圖。
- 新增跨平台評論讀取、按評論者語言單語回覆、作品數據與中／英文營運匯報；各站實測及待驗項目見驗證記錄。
- 新增五站作品列表、追蹤清單與唯讀巡檢：彙整評論候選、同口徑數據變化及本輪匯報；B站／YouTube 可分頁讀取評論樓。
- 提供專用封面適配、YouTube 播放列表設定與預設關閉、主動開啟的 Gmail 郵件提醒。
- **0.5.0 alpha，164 項離線測試通過。** 完整五平台發布仍有待驗收項目；已測範圍與限制均有記錄。

[安裝與使用](tools/publishops-crosscast/README.md) · [功能範圍](tools/publishops-crosscast/docs/FEATURES.md) · [驗證記錄](tools/publishops-crosscast/docs/VALIDATION.md)

**2026-10-29 更新：** 新增兩部第六期成片，累計 **21 個可下載版本**；補充鯨魚娘03 公開狀態與鯨魚娘02 首日數據。[查看更新](docs/updates/2026-10-29-evening.md)。

## AI 如何協助實際工作

從具體任務出發，記錄 AI 能協助的步驟、人類需要判斷的地方，以及實際留下的成果。商戶拓展示例是延伸應用演示；影片製作與帳號營運則保留真實實作紀錄。

| 工作中的用途 | 方法與成果 |
|---|---|
| 整理商戶資料、排列線索與準備溝通 | [商戶拓展應用示例](docs/business-development.md) |
| 選擇合適的 AI 工具與插件 | [工具選型與使用方式](docs/toolchain.md) |
| 分配研究、製作與檢查工作 | [GPT／DeepSeek 協作紀錄](docs/model-workflow.md) |
| 將重複步驟整理成程式 | [程式導覽](docs/code-map.md) · [可執行示例](scripts) |
| 比較營運數據並檢查結論 | [數據對比與限制](docs/data-comparison.md) |
| 保存工作脈絡，方便團隊接手 | [商戶簡報範本](templates/merchant-brief.md) · [營運檢查表](templates/operations-checklist.md) |

**2026-10-28 資料快照：** 55 筆稿件資料、69 筆去識別營運事件、19 個已公開影片版本、4 個流程示例腳本／模組，以及 10 項流程測試。這組統計不包含後續 CrossCast 程式與測試。商戶示例使用虛構資料，沒有把模擬結果寫成真實簽約或團隊提效業績。

我以需求提出、選題方向、回饋與審片推進專案，AI 協助研究、編寫程式及製作。這個實驗室記錄工具如何進入實際流程、遇到哪些錯誤，以及如何改善交付；程式與文件也明確標示 AI 協助。

### 三個可重跑的入口

```sh
npm run demo     # 虛構商戶排序
npm run report   # 真實資料摘要
npm test         # 流程檢查
```

使用 Node.js 20+，不需額外 npm 套件。原有流程測試與本地影片校驗結果見 [validation.json](results/validation.json)。

## 研究與製作紀錄

這是「Ai娘在工作」系列的公開研究與製作紀錄。內容涵蓋 AI 角色短劇、語音與畫面的時間同步、可編輯影片工程，以及 B站帳號的日常營運。人類負責方向與審片，AI 協助研究、製作和執行；每一項結果都標示證據與限制。

| 主題 | 已完成內容 | 閱讀 |
|---|---|---|
| 動畫節奏 | 區分輸出幀率、換姿勢頻率和運動間距，建立接觸動作檢查方法。 | [研究筆記](docs/animation-timing.md) |
| 可編輯影片 | 以語音時間軸組織場景、字幕及混音，形成可重開的交付流程。 | [工作流程](docs/editable-video-workflow.md) |
| 自動營運驗證 | 核實一次真正的每小時排程觸發與完成，保留單一執行者。 | [驗證紀錄](docs/automation-verification.md) |
| 帳號研究 | 整理歷史人工營運時期和目前 AI 系列的資料，保留採樣口徑。 | [資料說明](data/README.md) |

這些是製作實驗與工程經驗，並非經過對照實驗驗證的成長保證。舊影片與新影片的題材、片齡和流量來源不同，累積播放量不能直接判定「人類或 AI 誰比較會營運」。

## 已公開作品

下列標題顯示為繁體中文，B站原始標題保存在資料檔中，以便核對。

| 發布日期（UTC+8） | 作品 |
|---|---|
| 2026-10-29 | [鯨魚娘代班日誌03｜重生之我在B站看大肥魚](https://www.bilibili.com/video/BV1Wcx76dErs) |
| 2026-10-28 | [鯨魚娘代班日誌02｜上班第一天，我被人類派了三個活](https://www.bilibili.com/video/BV1Wfx56JEih) |
| 2026-10-28 | [鯨魚娘代班日誌01｜當ChatGPT沒有token了](https://www.bilibili.com/video/BV1usxV69EUQ) |
| 2026-10-27 | [GPT值班日記03｜問晚飯吃什麼，還得先填表？](https://www.bilibili.com/video/BV1UXxN6aECf) |
| 2026-10-26 | [GPT值班日記02｜有人問歌名，我先回了一個問題](https://www.bilibili.com/video/BV1gxnU6GEuz) |
| 2026-10-25 | [GPT值班日記01｜我回了2022年的評論，粉絲說我有點遲鈍](https://www.bilibili.com/video/BV1qjHV6sEuD) |

未在 B站公開的成片及舊版實驗片，另見[影片目錄](media/README.md)。GitHub 公開與 B站發布是兩個不同狀態，本倉庫不會更改 B站排期。

[2026-10-28 晚間更新：三個影片版本、首日數據與逐幀修復](docs/updates/2026-10-28-evening.md)

## 使用資料與方法

- [研究與選題方法](docs/research-method.md)：來源 → 證據 → 實測 → 限制。
- [研究記錄範本](templates/research-note.md)：把觀察和推論分開。
- [交付清單範本](templates/delivery-manifest.json)：記錄格式、雜湊、審片與發布狀態。
- [營運檢查表](templates/operations-checklist.md)：只處理到期事項，核驗結果後記錄。

資料提供 CSV／JSON，影片提供可下載檔案與 SHA-256。缺失值保留為空，不當成零；採樣時間與資料來源見各資料檔。

## 內容範圍

本倉庫公開研究說明、影片、帳號整體及單片數據、去識別的營運紀錄。登入憑證、私人訊息、觀眾個人資料及不相關的私人文件不屬於公開資料。作品中引用的第三方素材仍屬各自權利人；公開展示不代表授予第三方素材的再利用授權。

首次整理：2026-10-28。首頁與 CrossCast 文件更新：2026-10-29。

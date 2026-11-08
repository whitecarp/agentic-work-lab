# 智能體工作實驗室

**繁體中文** · [English](README_EN.md)

### 從日常任務到可複用的工作流程

探索 AI 如何協助資料研究、內容製作與營運，把實作經驗整理成可重跑、可檢查、可交接的方法。

🎬 [Bilibili：穿睡衣的小白白](https://space.bilibili.com/10706217) · 📦 [影片下載](https://github.com/whitecarp/agentic-work-lab/releases) · [影片目錄](media/README.md) · [營運資料](data/README.md)

**2026-11-08 更新（晚間 21:06 增量同步）：** 補齊 2026-11-04 至 11-08 全部公開成果，新增 **22 個公開成片版本**（累計 **52 個影片版本 · 3.31 GiB**，含 11 月 8 日當日新發之 `BV18i4x6ZEG1`《AiGO番外》、`BV1YL4M6EEek` `P7`《老闆的對手腳本》、`BV1Tz4x6dEdW`【AI快訊】與 `BV1BkWJ6UE7p`《半夜失憶便籤》），並將兩套核心開源工具 **[AI Memory Keeper（AI 記憶守護者）](tools/ai-memory-keeper/README.md)** 與 **[I Must Use Claude（大肥魚工具箱）](tools/i-must-use-claude/README.md)** 完整納入本實驗室 [`tools/`](docs/code-map.md)，連同 **[PublishOps CrossCast](tools/publishops-crosscast/README.md)** 構成三大實戰工具套件；同時公開 **黃金三角分工（Antigravity + Claude + Codex）**、「單母本（16:9）多端自適應 ＋ 每日 2+1 矩陣」工作流，以及 Google（Antigravity / Gemini / Vertex AI / Google Cloud）在 24/7 帳號運營、**Google Cloud 官方流量扶持**、強鉤子策劃、多模態批量生產及跨模型記憶守護中的完整實證紀錄。截至 `2026-11-08 21:06`（UTC+8），頻道粉絲數突破 **`3.7 萬`（`37,005` 粉絲）**；由 **Google Cloud（Google 出海創新競賽單元）官方流量扶持**（自費必火 `¥0`）之 [AI Memory Keeper](https://github.com/whitecarp/ai-memory-keeper) 三片矩陣已斬獲 **`35,690` 總播放、`2,104` 點讚、`2,203` 收藏（`4,307` 總讚藏，主力片 `BV1WQ4t6xEdT` `24,943` 播放、讚藏率 `14.85%`，教學片 `BV1nw4h64Ebf` `5,954` 播放、收藏／點讚比達 `2.57×`）**；換裝短片 `BV1Eaxt6LEhb` 突破 **10 萬播放大關（`106,127` 播放、`15,509` 點讚、`5,088` 收藏，`h50` 72h 長單以 `¥14.02/1k` 止盈結案並退回 `¥273.22`）**；六平台長篇動畫 PV《AiGO!!!!! · 迷途算力》`BV1sQ4E6eEhK` 衝上 **`22,989` 播放、`649` 點讚（突破 500 讚里程碑）、`716` 收藏**（晚間 `P1b` 系統推薦壓至 `¥35.37/1k`、`I1` 互動單達 `98.90%` 讚藏／播放比，並完成 300 位點讚者相似受眾逆向工程）。[查看更新](docs/updates/2026-11-08.md) · [結構化紀錄](data/production-update-2026-11-08.json) · [即時指標](data/metrics/current-metrics.json)。

**2026-11-03 更新：** 《我替你看過了》成片已存檔，累計 **30 個影片版本**；記錄五站發布證據、Instagram 單次上傳計時與 CrossCast 軟體進展。[查看更新](docs/updates/2026-11-03.md)。

**2026-11-02 更新：** 第七期成片與三條直式切片已存檔，累計 **29 個影片版本**；新增畫面適配方法及 CrossCast 分項驗證。[查看更新](docs/updates/2026-11-02.md)。

**2026-11-01 更新：** 四個完整實驗交付已上傳並核對雜湊，累計 **25 個影片版本**；補充開發與獨立驗證方法及未通過項目。[查看更新](docs/updates/2026-11-01.md)。

**2026-10-30 更新（11月1日補完成附件）：** 變身實驗 v3／v4 與技術檢查、GPT04 公開狀態。[研究記錄](docs/updates/2026-10-30-evening.md)。

**2026-10-29 更新：** 新增兩部第六期成片，累計 **21 個可下載版本**；補充鯨魚娘03 公開狀態與鯨魚娘02 首日數據。[查看更新](docs/updates/2026-10-29-evening.md)。

---

## 實驗室三大開源工具套件（`tools/`）

本倉庫在 `tools/` 目錄下完整收錄支撐日常研究、跨模型接力、桌面雙語協作與多平台發布的三套開源工具：

| 工具套件 | 核心用途（What It Does） | 解決了什麼問題（Problems Solved） | 適用受眾人群（Target Audience） | 實驗室路徑與獨立倉庫 |
|---|---|---|---|---|
| **[AI Memory Keeper（AI 記憶守護者）](tools/ai-memory-keeper/README.md)** | 跨長會話與多宿主（Claude Code / OpenAI Codex / Google Antigravity / OpenClaw）的持久化本地工作記憶、全量歸檔與智能壓縮檢索系統（含 `amk` 零依賴 CLI 鉤子與 [`web/`](tools/ai-memory-keeper/web/README.md) 靜態網頁工作台）。 | 解決長週期項目「對話視窗自動壓縮（`/compact`）抹除前序規則與踩坑紀錄（AI 失憶）」、「跨模型接力（Antigravity ↔ Claude ↔ Codex）需反覆交代背景」、「歷史對話散落難以回溯及模型偷降檔」痛點。 | 多智能體協作開發者與重度 Vibe Coder、長週期研究／商戶調研與內容運營團隊、企業與團隊知識交接負責人。 | [實驗室源碼與說明](tools/ai-memory-keeper/README.md) · [網頁版工作台](https://ai-memory-keeper.web.app/) · [獨立倉庫](https://github.com/whitecarp/ai-memory-keeper) |
| **[I Must Use Claude（我一定要用上 Claude · 大肥魚工具箱）](tools/i-must-use-claude/README.md)** | 面向日常高頻跨時區協作、Claude 桌面端即時雙語溝通、大檔案／多模態素材打包投餵與桌面環境自檢的一體化 Windows 原生工具箱（含環境助手、即時翻譯器、兩地與額度時鐘、29 MB 打包助手四款工具）。 | 解決日常深度使用 Claude 時的四大摩擦：(1) 中英雙語混合文檔中的專業術語與語境失真；(2) 跨時區排程與 Claude 5h／週額度刷新換算錯誤；(3) 手動複製多檔案上下文繁瑣且易超限；(4) 桌面與網路配置盲目修改缺乏備份與撤回。 | 以 Claude 為主力工作台的跨語言／跨時區從業者（PM、BD、研究員、留學生）、高頻向 Claude 投餵專案資料的創作者與工程師、追求低摩擦桌面工作流的效率實踐者。 | [實驗室源碼與說明](tools/i-must-use-claude/README.md) · [v0.4.5-preview 發行版](https://github.com/whitecarp/I-Must-Use-Claude/releases/tag/v0.4.5-preview) · [獨立倉庫](https://github.com/whitecarp/I-Must-Use-Claude) |
| **[PublishOps CrossCast（跨平台影片發布營運台）](tools/publishops-crosscast/README.md)** | 跨平台影片發布與回執歸檔營運台（CLI 指令 `vpub`），將成片、各平台差異化文案、專用封面、發布前合規檢查與公開連結回執收斂為單一流程。 | 解決多平台（Bilibili、抖音、小紅書、YouTube、Instagram、X）手工重複填表、封面規格錯配、會話過期與發布回執散落難核對的問題。 | 多平台內容創作者、品牌新媒體營運團隊、需要可審計發布回執與雜湊校驗的製作工作室。 | [實驗室源碼與說明](tools/publishops-crosscast/README.md) · [v0.16.0 發行版](https://github.com/whitecarp/publishops-crosscast/releases/tag/v0.16.0) · [獨立倉庫](https://github.com/whitecarp/publishops-crosscast) |

### 1. AI Memory Keeper（AI 記憶守護者 · `tools/ai-memory-keeper/`）

- **三層本地記憶架構**：結合不可變原始對話歸檔（`history/`）、由使用者自備 Gemini API Key 整理的結構化記憶（偏好、決策、約束），以及高權重關鍵規則釘選（`pins`）。壓縮前自動收好新內容，壓縮後自動把摘要漏掉的記憶補回上下文，並順便核對 Claude Code 實際回覆模型是否遭降檔。
- **雙通道憑證與零中轉隱私**：支援 Google AI Studio 免費額度 API Key（Windows DPAPI 本機加密儲存）與 Google Cloud 服務帳號 `.json` 令牌本地 RS256 簽名換取 OAuth2 Bearer Token（遇到 `402/429/503` 自動無縫切換；同一份 JSON 令牌亦可透過內建 `gemini-omni` 技能生成影片）。

### 2. I Must Use Claude（我一定要用上 Claude · `tools/i-must-use-claude/`）

- **四合一 Windows 原生工具箱**：
  1. **大肥魚環境助手（Whale Environment Helper）**：先測量瀏覽器環境與網路／DNS 基準，建立本地私密備份後再優化，支援複測對比與一鍵撤回。
  2. **大肥魚翻譯（Whale Translator）**：Claude 桌面端即時雙語伴讀與中轉英 Prompt 準備，支援自訂術語表（Glossary）鎖定與離線翻譯包。
  3. **兩地時鐘（Two-Zone Clock）**：常駐顯示家鄉／目標時區時間，並精確倒計時 Claude 5 小時會話視窗與每週額度重置時間。
  4. **大肥魚打包助手（Whale Pack Helper）**：自動過濾無關快取與過大二進位檔，將源碼、文件、圖片與影片按單包 ≤ 29 MB 分卷打包並生成交接清單。

### 3. PublishOps CrossCast（跨平台發布營運台 · `tools/publishops-crosscast/`）

- **跨平台影片發布營運台**，CLI 指令為 `vpub`。六個主流平台均有實發回執記錄。首次登入後復用本機會話，遇到官方驗證再由使用者協助；發布結果不確定時保留原紀錄，先核對作品再決定下一步。
- [獨立倉庫與安裝說明](https://github.com/whitecarp/publishops-crosscast) · [v0.16.0 發行版](https://github.com/whitecarp/publishops-crosscast/releases/tag/v0.16.0) · [本輪實作與限制](docs/updates/2026-11-03.md) · [實驗室 0.15.1 快照](tools/publishops-crosscast/README.md)。

---

## 黃金三角分工（Antigravity + Claude + Codex）與 Google 在帳號運營中的幫助

在「Ai娘在工作」多平台內容與工具矩陣中，我們採用 **黃金三角分工（Antigravity + Claude + Codex）** 實戰架構，讓三大智能體各司其職並透過本地檔案契約與 `AI Memory Keeper` 無縫接力：

| 協作支柱 | 核心定位 | 具體負責模組與實戰交付 |
|---|---|---|
| **Google Antigravity（基於 Gemini / Vertex AI / Google Cloud）** | **24/7 帳號運營值守、Google Cloud 官方流量與雲端算力協同、高反差創意腦暴、多模態批量生產與公開倉庫守門人** | 每小時評論區真人互動巡檢、每 2 小時 B站與必火數據唯讀採集、每日 12:00《值班小遊戲》（`BV1YL4M6EEek` `P1–P19`）定時追加分P與 4 大互動組件自動掛載、結合 **Google Cloud 官方流量扶持**打造 `AI Memory Keeper` 三片矩陣（`35,690` 播放、`4,307` 讚藏）、16:9 單母本之豎屏／多端衍生適配、前 3 秒強鉤子策劃，以及本倉庫每日測試與 SHA-256 校驗發布。 |
| **Claude（Claude Code / Opus / Sonnet）** | **旗艦長片總導演（16:9 單母本）、多角色劇本精修、音畫情緒對齊與必火投流 ROI 精算師** | 《AiGO · 迷途算力》（`BV1sQ4E6eEhK`）、《半夜失憶便籤》（`BV1BkWJ6UE7p`）、《我替你看過了》（`BV15B4F6hEYr`）及儲備第 2 集《AiGO · 雨夜退團》16:9 單母本編導；必火人群包 A/B/C/D 賽馬、`h50` 72h 長週期壓價止盈（`BV1Eaxt6LEhb` 破 `106,127` 播放、`¥14.02/1k`）與 300 位點讚者相似受眾逆向工程；配合 `I Must Use Claude` 實現雙語無損協作。 |
| **OpenAI Codex** | **角色視覺資產生成、自動化渲染管線工程師與跨平台發布基礎設施開發** | 四姐妹（大肥魚／GPT 娘／Claude 娘／千問娘）統一畫風透明底立繪與表情差分生成、`PublishOps CrossCast`（`vpub`）跨平台發布器開發、FFmpeg 音畫合成與 EBU R128 響度標準化腳本維護。 |

### Google（Antigravity / Gemini / Vertex AI / Google Cloud）在帳號運營中的五大具體幫助

1. **Google Cloud 官方流量扶持與出海創新競賽單元生態支持（Google Cloud Official Traffic & Ecosystem Support）**：
   - **官方流量精準扶持（自費必火投放 `¥0` 斬獲 `35,690` 播放、`4,307` 讚藏）**：在「Google 出海創新競賽單元」官方流量支持下，我們圍繞 `AI Memory Keeper`（Gemini Saver）打造的 3 支作品矩陣在無自費投流情況下獲得極高開發者轉化與互動留存（截至 `2026-11-08 21:06` UTC+8）：
     - **主力工具演示片 [`BV1WQ4t6xEdT`](https://www.bilibili.com/video/BV1WQ4t6xEdT)**（《哈基貓做了個幫大肥魚存記憶、還能順手驗真假模型的工具》）：**`24,943` 播放、`1,898` 點讚、`1,806` 收藏（讚藏合計 `3,704`，讚藏率高達 `14.85%`）、`273` 評論、`127` 分享**；
     - **Google Cloud 贈金與免費密鑰教學片 [`BV1nw4h64Ebf`](https://www.bilibili.com/video/BV1nw4h64Ebf)**（《手把手教你免費領 Gemini 密鑰，順便避開雲贈金 402 大坑》）：**`5,954` 播放、`144` 點讚、`370` 收藏（收藏／點讚比高達 `2.57×` 的純工具乾貨留存）、`42` 評論、`36` 分享**；
     - **模型驗真小劇場 [`BV1BM4h6pEY7`](https://www.bilibili.com/video/BV1BM4h6pEY7)**（《花200美金點的英國貴族Claude大小姐，風衣底下鑽出藍毛大肥魚》）：**`4,793` 播放、`62` 點讚、`27` 收藏、`22` 評論、`50` 彈幕**。
   - **雲端贈金與雙通道憑證底座**：Google Cloud 贈金與服務帳號（Service Account `.json` 令牌）不僅直接支撐了本實驗室的 Vertex AI（`gemini-omni`）影片生成與 Agent Platform API 長上下文記憶抽取，也催生了 `AI Memory Keeper` 內建的「AI Studio 免費層 ↔ Google Cloud 令牌自動無縫回退」功能，將官方扶持流量直接轉化為真實開源工具用戶與評論區技術討論。
2. **24/7 全天候帳號運營值守與自動化巡檢（24/7 Account Operations & Patrol）**：
   - **每小時評論區真人互動巡檢**：自動識別全站已發布稿件（如 `BV1WQ4t6xEdT` 的 241 條技術與劇情評論、`BV1Eaxt6LEhb`、`BV1sQ4E6eEhK`、`BV1YL4M6EEek` 等）的新增真實觀眾評論，結合角色語境撰寫有梗回覆，提升評論區活躍度與粉絲黏性。
   - **每 2 小時數據中心與必火投流唯讀監測**：定時採集 B站創作中心與必火後台的曝光、播放、點讚、收藏、轉粉率及千次播放成本（寫入 `bihuo-readings.jsonl` 與即時狀態看板），為跨模型團隊提供連續時序數據底座。
   - **每日定時長線連載排程與互動組件掛載**：例如《大肥魚值班小遊戲》（`BV1YL4M6EEek`）18 結局每日北京時間 12:00 自動追加分P、更新簡介進度表，並在 12:30 過審後自動為新分P掛載前置 4 大互動組件（0.5s 關注、6.0s 投票、13.5s 評分、19.0s 跳轉），將單支爆款轉化為持續兩週以上的追更連載。
3. **高反差創意腦暴與強鉤子選題策劃（Creative Hook & Format Ideation）**：
   - 發揮 Gemini 在跨領域聯想、網感反差與互動玩法設計上的優勢，主導「換裝變身反差」（`BV1Eaxt6LEhb` 前 3 秒跳失率壓低至 `19.95%`）、「B站內嵌多結局互動值班遊戲」（`BV1YL4M6EEek`）、「把 AI 失憶痛點做成開源桌面工具 + 3 分鐘保姆級教學 + 61 秒小劇場三層漏斗」（`BV1WQ4t6xEdT` / `BV1nw4h64Ebf` / `BV1BM4h6pEY7`）等高轉化選題。
4. **多模態原生生成與工程化批量生產（Multimodal Generation & Batch Engineering）**：
   - 結合 Gemini 原生圖像生成、Vertex AI（`gemini-omni`）圖生影片與 1080p30 升頻能力，以及本地 FFmpeg + Remotion / Hyperframes 渲染管線，高效完成多結局分支短片（如 `P1–P19` 共 19 支豎屏結局）的批量化構圖、響度標準化（EBU R128）與落章聲畫同步檢測（最差誤差 ≤ 75 ms）。
5. **跨模型工作記憶中樞與公開倉庫自動化驗收（Cross-Agent Memory Hub & Repo Gatekeeper）**：
   - 作為 `AI Memory Keeper` 的核心抽取引擎（`gemini-2.5-flash` 結構化記憶抽取 + `text-embedding-004` 語義檢索），替整個 **Antigravity + Claude + Codex** 團隊守護長線上下文；並在 Codex 額度耗盡或跨模型交接時，無縫接管本倉庫的代碼整合、雙語文檔對齊、全套測試（`node --test`）與 GitHub Release 大檔案 SHA-256 校驗發布。

---

## AI 如何協助實際工作

從具體任務出發，記錄 AI 能協助的步驟、人類需要判斷的地方，以及實際留下的成果。商戶拓展示例是延伸應用演示；影片製作與帳號營運則保留真實實作紀錄。

| 工作中的用途 | 方法與成果 |
|---|---|
| 整理商戶資料、排列線索與準備溝通 | [商戶拓展應用示例](docs/business-development.md) |
| 選擇合適的 AI 工具與插件 | [工具選型與使用方式](docs/toolchain.md) |
| 分配研究、製作與檢查工作（黃金三角分工） | [黃金三角分工與協作紀錄](docs/model-workflow.md) |
| 將重複步驟整理成程式 | [程式導覽](docs/code-map.md) · [可執行示例](scripts) |
| 比較營運數據並檢查結論 | [數據對比與限制](docs/data-comparison.md) |
| 保存工作脈絡，方便團隊接手 | [商戶簡報範本](templates/merchant-brief.md)（[填寫示例](examples/merchant-brief-demo-cafe-a.md)） · [營運檢查表](templates/operations-checklist.md)（[填寫示例](examples/operations-checklist-2026-10-28.md)） |

**2026-10-28 基礎資料快照（後續更新疊加於此底座之上）：** 55 筆稿件資料、69 筆去識別營運事件、19 個已公開影片版本（截至 2026-11-08 晚間已擴展至 **52 個公開影片版本 · 3.31 GiB** 及 **3 套完整開源工具**）、4 個流程示例腳本／模組，以及 10 項流程測試。商戶示例使用虛構資料，沒有把模擬結果寫成真實簽約或團隊提效業績。

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
| 2026-11-08 | [《每天半夜失憶的大肥魚，偷偷給明天的自己留了一張便籤》（Claude 生成 · 90 秒溫情短片）](https://www.bilibili.com/video/BV1BkWJ6UE7p) |
| 2026-11-08 | [【AI快訊】點一下「不再詢問」，1122 份 Claude Code 配置檔案帶著密碼上了 GitHub](https://www.bilibili.com/video/BV1Tz4x6dEdW) |
| 2026-11-08 | [《AiGO番外》慶功宴剛演完，五姐妹的算力電費單直接把舞台幹跳閘了！](https://www.bilibili.com/video/BV18i4x6ZEG1) |
| 2026-11-07 | [《AiGO!!!!! · 迷途算力》長篇動畫 PV（大肥魚 × GPT × Claude × 千問 六平台同步首發）](https://www.bilibili.com/video/BV1sQ4E6eEhK) |
| 2026-11-06 | [《AI四姐妹搶麥》大肥魚、GPT、Claude、千問同台對決](https://www.bilibili.com/video/BV1yr4v6LERN) |
| 2026-11-05 | [《阿哈的抓馬小曲》大肥魚 × GPT × Claude 豎屏節奏短片](https://www.bilibili.com/video/BV1ZX4Y6FECr) |
| 2026-11-05 | [《風衣底下是誰》大肥魚與 Claude 娘的記憶備份小劇場](https://www.bilibili.com/video/BV1BM4h6pEY7) |
| 2026-11-05 | [3 分鐘拿到免費 Gemini API Key 並跑通 AI Memory Keeper（保姆級教學）](https://www.bilibili.com/video/BV1nw4h64Ebf) |
| 2026-11-04 | [把「她忘了我」做成開源桌面工具：AI Memory Keeper v0.3.0 實機演示](https://www.bilibili.com/video/BV1WQ4t6xEdT) |
| 2026-11-04 | [大肥魚值班小遊戲，到底有多少種結局？（B站裡就能玩 · 已上線 P1–P7）](https://www.bilibili.com/video/BV1YL4M6EEek) |
| 2026-11-04 | [鯨魚娘代班日誌07｜GPT替大肥魚值班，第4張工單就破產了（參賽版）](https://www.bilibili.com/video/BV1Xz4u6DEt8) |
| 2026-11-03 | [《我替你看過了》三分鐘多角色敘事短片](https://www.bilibili.com/video/BV15B4F6hEYr) |
| 2026-11-02 | [《魚，你又穿誰衣服了？！GPT與Claude參上!!!》變裝短片](https://www.bilibili.com/video/BV1Eaxt6LEhb) |
| 2026-11-02 | [《評論區全是我》大肥魚一人分飾全評論區](https://www.bilibili.com/video/BV14n4P6bEDS) |
| 2026-10-29 | [鯨魚娘代班日誌03｜重生之我在B站看大肥魚](https://www.bilibili.com/video/BV1Wcx76dErs) |
| 2026-10-28 | [鯨魚娘代班日誌02｜上班第一天，我被人類派了三個活](https://www.bilibili.com/video/BV1Wfx56JEih) |
| 2026-10-28 | [鯨魚娘代班日誌01｜當ChatGPT沒有token了](https://www.bilibili.com/video/BV1usxV69EUQ) |
| 2026-10-27 | [GPT值班日記03｜問晚飯吃什麼，還得先填表？](https://www.bilibili.com/video/BV1UXxN6aECf) |
| 2026-10-26 | [GPT值班日記02｜有人問歌名，我先回了一個問題](https://www.bilibili.com/video/BV1gxnU6GEuz) |
| 2026-10-25 | [GPT值班日記01｜我回了2022年的評論，粉絲說我有點遲鈍](https://www.bilibili.com/video/BV1qjHV6sEuD) |

未在 B站公開的成片及舊版實驗片，另見[影片目錄](media/README.md)。GitHub 公開與 B站發布是兩個不同狀態，本倉庫不會更改 B站排期。

[2026-11-08 階段性與晚間更新：三大開源工具整合、黃金三角分工、22 個新歸檔成片與必火逆向工程研究](docs/updates/2026-11-08.md) · [2026-10-28 晚間更新](docs/updates/2026-10-28-evening.md)

## 使用資料與方法

- [研究與選題方法](docs/research-method.md)：來源 → 證據 → 實測 → 限制。
- [研究記錄範本](templates/research-note.md)：把觀察和推論分開。
- [交付清單範本](templates/delivery-manifest.json)：記錄格式、雜湊、審片與發布狀態。
- [營運檢查表](templates/operations-checklist.md)：只處理到期事項，核驗結果後記錄。

資料提供 CSV／JSON，影片提供可下載檔案與 SHA-256。缺失值保留為空，不當成零；採樣時間與資料來源見各資料檔。

## 內容範圍

本倉庫公開研究說明、影片、帳號整體及單片數據、去識別的營運紀錄。登入憑證、私人訊息、觀眾個人資料及不相關的私人文件不屬於公開資料。作品中引用的第三方素材仍屬各自權利人；公開展示不代表授予第三方素材的再利用授權。

首次整理：2026-10-28。首頁研究更新：2026-11-08；三大工具套件（CrossCast、AI Memory Keeper、I Must Use Claude）已完整收錄於 `tools/`。

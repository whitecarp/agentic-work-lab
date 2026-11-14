# 工具、三大自研套件與多智能體選型

**繁體中文** · [English](toolchain_EN.md)

此表記錄本實驗室實際開發、開源與日常使用的工具及驗證證據，不把「已安裝」寫成「已精通」。

---

## 一、本實驗室開源的三大核心工具套件（`tools/`）

我們將日常跨模型協作、跨時區使用 Claude 與多平台影片營運中反覆遇到的摩擦，封裝為三套開源工具並完整收錄於本倉庫 `tools/` 目錄：

| 工具套件 | 核心用途（What It Does） | 解決了什麼問題（Problems Solved） | 適用受眾人群（Target Audience） | 本倉庫路徑與獨立倉庫 |
| --- | --- | --- | --- | --- |
| **AI Memory Keeper（AI 記憶守護者）** | 跨長會話與多宿主智能體（Claude Code、OpenAI Codex、Google Antigravity、WorkBuddy／OpenClaw）的**持久化本地工作記憶、全量歸檔與智能壓縮檢索系統**（含三層記憶架構 `history/`、活躍壓縮上下文、關鍵規則釘選 `pins`，零依賴 `amk` CLI 守護鉤子、模型降檔核驗與靜態網頁端）。 | 1. **解決對話視窗爆滿自動壓縮（`/compact`）導致的 AI 失憶**：壓縮前備份原話並抽取偏好／決策／約束，壓縮後自動補回摘要遺漏的脈絡；<br>2. **消除跨模型接力（Claude ↔ Codex ↔ Gemini）重複交代背景與禁區的成本**；<br>3. **解決歷史對話散落不同視窗、難以精確回溯與審計移交的問題**。 | 1. **多智能體協作開發者與重度 Vibe Coder**；<br>2. **長週期研究、商戶調研與內容運營團隊**（需 AI 持續記住品牌規範、數據口徑與客戶偏好）；<br>3. **企業／團隊知識交接與復盤負責人**。 | [本倉庫 `tools/ai-memory-keeper/`](../tools/ai-memory-keeper/README.md) · [網頁端 `web/`](../tools/ai-memory-keeper/web/README.md) · [獨立倉庫](https://github.com/whitecarp/ai-memory-keeper) |
| **I Must Use Claude（我一定要用上 Claude · 大肥魚工具箱）** | 面向高頻跨時區協作、Claude 桌面端即時雙語溝通、大檔案／多模態打包投餵與桌面環境自檢的 **Windows 四合一原生工具箱**（含 `Whale Environment Helper` 環境與 DNS 可逆自檢、`Whale Translator` 雙語即時伴讀與術語鎖定、`Two-Zone Clock` 雙時區與額度重置時鐘、`Whale Pack Helper` ≤29 MB 上下文智能分卷打包器）。 | 1. **解決中英混合會議／文件中的專業術語失真與複製貼上疲勞**；<br>2. **解決跨時區協作與 Claude 5 小時／每週額度刷新換算錯誤**；<br>3. **解決向 Claude 投餵複雜程式碼庫或多檔案專案時手動篩選繁瑣、遺漏或超限報錯**；<br>4. **解決桌面環境與網路配置異常時盲目修改、缺少基準測量與一鍵撤回的問題**。 | 1. **以 Claude 為主力工作台的跨語言／跨時區從業者**（PM、商務拓展 BD、研究員、留學生）；<br>2. **日常高頻向 Claude 投餵專案資料的創作者與工程師**；<br>3. **追求低摩擦 Windows 桌面工作流的個人效率實踐者**。 | [本倉庫 `tools/i-must-use-claude/`](../tools/i-must-use-claude/README.md) · [獨立倉庫與 EXE 下載](https://github.com/whitecarp/I-Must-Use-Claude) |
| **PublishOps CrossCast（跨平台發布營運台 · v0.16.0 alpha）** | 以 `vpub` CLI 統一管理 Bilibili、YouTube、抖音、Instagram、X 五大平台的影片上傳（TikTok 暫停）、各站專屬文案、16:10／16:9／3:4 專用封面、內建 `bili-cli` 1.2.1 與發布回執獨立讀回核驗。 | 1. **解決多平台重複手動上傳、封面裁切跑位與發布狀態不明**；<br>2. **解決上傳中斷需重頭再來與跨平台評論／數據分頁採集易漏頁的問題**。 | 1. **多平台創作者與品牌內容營運團隊**；<br>2. **需要可審計發布回執與跨平台巡檢的自動化營運工程師**。 | [本倉庫 `tools/publishops-crosscast/`](../tools/publishops-crosscast/README_ZH_TW.md) · [獨立倉庫](https://github.com/whitecarp/publishops-crosscast) |

---

## 二、黃金三角分工（Antigravity + Claude + Codex）與 Google / Google Cloud 在帳號運營中的核心角色

在本實驗室的真實生產與「Ai娘在工作」帳號矩陣運營中，我們採用 **黃金三角分工（Antigravity + Claude + Codex）**，其中 **Google（Gemini / Google Antigravity / Vertex AI / Google Cloud）** 的具體貢獻如下：

| 協作面向 | Google（Gemini / Antigravity / Vertex AI / Google Cloud）的具體幫助與實證紀錄 |
| --- | --- |
| **0. Google Cloud 官方流量扶持與出海創新競賽單元支持** | • **官方流量扶持（自費必火 `¥0` 斬獲 `35,690` 播放、`4,307` 讚藏）**：在 Google Cloud「Google 出海創新競賽單元」官方流量支持下，圍繞 `AI Memory Keeper`（Gemini Saver）發布的三支 B站作品（截至 `2026-11-08 21:06` UTC+8）合計獲得 **`35,690` 播放、`2,104` 點讚、`2,203` 收藏（整體收藏反超點讚）、`337` 評論、`165` 分享**——主力演示片 [`BV1WQ4t6xEdT`](https://www.bilibili.com/video/BV1WQ4t6xEdT) 達 **`24,943` 播放、`1,898` 點讚、`1,806` 收藏（讚藏率 `14.85%`）、`273` 評論**；Google Cloud 贈金與免費密鑰教學片 [`BV1nw4h64Ebf`](https://www.bilibili.com/video/BV1nw4h64Ebf) 達 **`5,954` 播放、`144` 點讚、`370` 收藏（收藏／點讚比達 `2.57×`）**；模型驗真小劇場 [`BV1BM4h6pEY7`](https://www.bilibili.com/video/BV1BM4h6pEY7) 達 **`4,793` 播放**。<br>• **雲端贈金與雙通道憑證支持**：Google Cloud 贈金與服務帳號 `.json` 令牌直接支撐了 Vertex AI（`gemini-omni`）影片生成與 Agent Platform API 長上下文記憶抽取。 |
| **1. 24/7 全天候帳號運營值守與自動化巡檢** | • **每小時評論區真人互動巡檢**：自動掃描已公開稿件（如 `BV1WQ4t6xEdT`、`BV1Eaxt6LEhb`、`BV1sQ4E6eEhK`、`BV1YL4M6EEek` 等）新增真實觀眾留言，結合影片語境撰寫有梗回覆，提升評論區活躍度與粉絲黏性。<br>• **每 2 小時數據中心與必火投流唯讀監測**：定時採集 B站創作中心與必火後台的曝光、播放、點讚、收藏、轉粉率及千次播放成本（寫入 `bihuo-readings.jsonl`），為團隊提供連續時序數據底座。<br>• **每日定時長線連載排程與互動組件掛載**：執行《大肥魚值班小遊戲》（`BV1YL4M6EEek`）18 結局每日 12:00（UTC+8）自動追加分P、更新簡介進度表，並在 12:30 過審後自動為新分P掛載前置 4 大互動組件（`0.5s` 關注、`6.0s` 投票、`13.5s` 評分、`19.0s` 跳轉），將單支作品轉化為持續兩週以上的追更連載。 |
| **2. 高反差創意腦暴與強鉤子選題策劃** | 發揮 Gemini 在跨領域聯想、網感反差與互動玩法設計上的優勢，負責前置「前 3 秒視覺／認知鉤子」與 ≤60 秒強互動短片選題策劃（如多模型同台搶麥、職場代班翻車反轉、評論區點菜驅動的下期實驗）。 |
| **3. 多模態視覺生成與長分支批量生產** | 調用 Google 原生圖像生成與 Vertex AI（`gemini-omni-1.2-flash`，封裝於 `tools/ai-memory-keeper/skills/gemini-omni/`）能力，配合 FFmpeg 渲染管線，快速批量生產多結局豎屏分支短片（如 `P4–P19` 不同結局實機演示）、Q 版角色表情差分與桌面守護者動畫幀。 |
| **4. 跨模型記憶底座與公開實驗室守門人** | 驅動 [`AI Memory Keeper`](../tools/ai-memory-keeper/README.md)，讓 Google Antigravity、Claude 與 Codex 共享同一套本地持久化記憶與運營狀態；同時承擔本公開倉庫 `Agentic Work Lab` 的每日自動化驗收、雙語文件對齊、Release 媒體 SHA-256 校驗與合規守門職責。 |

---

## 三、完整工具鏈與插件選型一覽

| 工具 | 實際用途 | 證據與版本 |
| --- | --- | --- |
| **Google Antigravity／Gemini 2.5 Flash／Vertex AI** | 24/7 評論巡檢與必火數據採集、每日分P連載排程與組件掛載、前 3 秒創意鉤子策劃、`AI Memory Keeper` 結構化記憶抽取、`gemini-omni` 影片生成與 GitHub 公開倉庫守門驗收。 | [`tools/ai-memory-keeper/`](../tools/ai-memory-keeper/README.md)、[11 月 8 日更新報告](updates/2026-11-08.md)。 |
| **Anthropic Claude／Claude Code** | 必火投流精算與人群包 A/B/C/D 賽馬策略大腦、主線精品成片總導演（HyperFrames／Remotion 動效與節奏精修）、封面與前端視覺打磨。 | [`tools/i-must-use-claude/`](../tools/i-must-use-claude/README.md)、[多模型分工紀錄](model-workflow.md)。 |
| **OpenAI ChatGPT／Codex** | 核心角色立繪與關鍵幀原畫生成、`vpub`（`PublishOps CrossCast` v0.16.0 alpha）跨平台發布後端與基礎設施開發、桌面工具 C#/.NET 工程測試。 | [`tools/publishops-crosscast/`](../tools/publishops-crosscast/README_ZH_TW.md)、本倉庫提交與製作紀錄。 |
| **DeepSeek 獨立營運實例** | 早期接手週期性營運檢查表與共享進度驗證。 | [實際排程紀錄](automation-verification.md)；不是同題模型能力跑分。 |
| **Bili CLI（`bili-cli`）** | 取得稿件清單、創作中心數據、分P追加、互動組件掛載、草稿與發布回讀。 | 已整併 **1.2.1** 於 `PublishOps CrossCast 0.16.0`；[資料集](../data/README.md)。 |
| **Breeze CLI／Audio Studio + ACE-Step 1.5 + DiffSinger** | 角色語音合成、選用聲段、原創搖滾編曲（《迷途算力》）與 EBU R128 混音工作流。 | 可編輯交付案例與 [11 月 8 日更新報告](updates/2026-11-08.md)。 |
| **HyperFrames + GSAP** | 以 HTML 場景與語音時間軸組合 16:9 橫版與 9:16 豎版影片。 | 固定 **HyperFrames 0.8.208**；本地 GSAP 依賴。 |
| **Seedance 2.0** | 角色連續動作影片生成與 31 鏡頭長篇動畫 PV（《AiGO!!!!! · 迷途算力》）。 | GPT 第三至五期教學與《迷途算力》成片。 |
| **FFmpeg／ffprobe** | 媒體格式、解碼、4K 超採樣抗二壓重編碼、立體聲與落章同步檢查。 | 製作交付中的技術檢查紀錄。 |
| **Node.js／JavaScript** | 資料整理、`AI Memory Keeper` 零依賴 CLI 與網頁端、示例流程、雜湊核對、測試。 | 環境 **Node.js 26.11.1**；公開腳本只用標準函式庫。 |
| **C#／.NET Framework 4.8 + PowerShell + Python** | `I Must Use Claude` 四款 Windows 原生桌面程式、發布控制、資料採集與驗證腳本。 | [程式導覽](code-map.md)。 |

Computer Use 用於原生接口未涵蓋的網頁操作；先使用連接器或 CLI，再以瀏覽器補足。插件和 skill 是工作流入口，並不等於自行訓練了模型。目前沒有 GitHub Copilot 或 Microsoft Copilot 的實作證據，因此不將它們列為已完成案例。

[返回實驗室首頁](../README.md)

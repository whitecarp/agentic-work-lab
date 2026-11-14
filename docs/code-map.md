# 寫了哪些程式

**繁體中文** · [English](code-map_EN.md)

本倉庫 `tools/` 目錄完整收錄三套核心工程套件的原始碼與自動化測試：
1. **[`tools/ai-memory-keeper/`](../tools/ai-memory-keeper/README.md)**（獨立倉庫 [`whitecarp/ai-memory-keeper`](https://github.com/whitecarp/ai-memory-keeper)）：跨智能體上下文壓縮記憶守護、模型降檔核驗與雙子座星圖網頁工作台。
2. **[`tools/i-must-use-claude/`](../tools/i-must-use-claude/README.md)**（獨立倉庫 [`whitecarp/I-Must-Use-Claude`](https://github.com/whitecarp/I-Must-Use-Claude)）：面向 Claude 日常協作的四合一 Windows 原生工具箱（環境與 DNS 可逆自檢、即時雙語伴讀翻譯、兩地額度時鐘、≤29 MB 上下文打包器）。
3. **[`tools/publishops-crosscast/`](../tools/publishops-crosscast/README_ZH_TW.md)**（獨立倉庫 [`whitecarp/publishops-crosscast`](https://github.com/whitecarp/publishops-crosscast)）：跨平台影片發布營運台 `vpub`（已同步 **v0.16.0 alpha** 與內建 `bili-cli` 1.2.1；含繁體中文／簡體中文／英文文件）。

本專案採用人類提出需求、Antigravity + Claude + Codex 黃金三角協作編寫、工具驗證與人類審閱的方式。程式能否重跑、失敗時怎麼處理，比單純列出語言或程式碼行數更重要。

---

## 一、AI Memory Keeper：跨智能體記憶守護與模型核驗（`tools/ai-memory-keeper/`）

- [cli.mjs](../tools/ai-memory-keeper/src/cli.mjs) 與 [hooks.mjs](../tools/ai-memory-keeper/src/hooks.mjs)：`amk` 命令入口，為 Claude Code（`.claude/settings.local.json`）、Codex（`.codex/hooks.json`）、Antigravity（`.agents/rules/`）與 WorkBuddy 按所有權標記非破壞性掛載／卸載壓縮前後鉤子。
- [memory.mjs](../tools/ai-memory-keeper/src/memory.mjs)、[store.mjs](../tools/ai-memory-keeper/src/store.mjs) 與 [engine.mjs](../tools/ai-memory-keeper/src/engine.mjs)：三層本地記憶存儲（不可變歷史歸檔、活躍壓縮上下文、關鍵規則釘選）與基於 Google Gemini API 的偏好／決策／約束抽取。
- [key.mjs](../tools/ai-memory-keeper/src/key.mjs) 與 [omni.mjs](../tools/ai-memory-keeper/src/omni.mjs)：Windows DPAPI 本機加密憑證存儲、AI Studio 免費層與 Google Cloud 服務帳號 JSON 令牌（本地 RS256 簽名換取 OAuth2 Bearer Token）自動回退路由，以及 `gemini-omni-1.2-flash` 影片生成呼叫。
- [audit.mjs](../tools/ai-memory-keeper/src/audit.mjs) 與 [safety.mjs](../tools/ai-memory-keeper/src/safety.mjs)：敏感資訊（API Key／密碼／Token）外發前清洗，以及 Claude Code 請求模型與實際回覆模型檔位一致性核對。
- [overlay-native.cs](../tools/ai-memory-keeper/src/overlay-native.cs) 與 [overlay.mjs](../tools/ai-memory-keeper/src/overlay.mjs)：Windows 原生無邊框守護者浮窗，在壓縮完成時提示補回了哪些遺漏記憶。
- [web/public/core.js](../tools/ai-memory-keeper/web/public/core.js) 與 [web/test/core.test.mjs](../tools/ai-memory-keeper/web/test/core.test.mjs)：靜態網頁端分塊讀取、多輪合併、雙子座星圖渲染與 33 項零依賴單元測試。

---

## 二、I Must Use Claude：四合一 Windows 原生工具箱（`tools/i-must-use-claude/`）

- **大肥魚環境助手（[`apps/whale-claude-helper/`](../tools/i-must-use-claude/apps/whale-claude-helper/README.md)）**：
  - [RepairEngine.cs](../tools/i-must-use-claude/apps/whale-claude-helper/src/RepairEngine.cs) 與 [OptimizerService.cs](../tools/i-must-use-claude/apps/whale-claude-helper/src/OptimizerService.cs)：先測量瀏覽器環境基準、建立本地私密備份後再處理命中項，支援衝突感知的一鍵撤回。
  - [NetworkDnsService.cs](../tools/i-must-use-claude/apps/whale-claude-helper/src/NetworkDnsService.cs) 與 [CodexProxySettings.cs](../tools/i-must-use-claude/apps/whale-claude-helper/src/CodexProxySettings.cs)：本機 DNS 解析器路徑實測、CuteCloud DNS 預覽／備份／套用／撤回，以及 Codex 代理配置。
- **大肥魚翻譯（[`apps/claude-live-translator/`](../tools/i-must-use-claude/apps/claude-live-translator/README.md)）**：
  - [ClaudeReader.cs](../tools/i-must-use-claude/apps/claude-live-translator/src/ClaudeReader.cs)、[CompanionForm.cs](../tools/i-must-use-claude/apps/claude-live-translator/src/CompanionForm.cs) 與 [Glossary.cs](../tools/i-must-use-claude/apps/claude-live-translator/src/Glossary.cs)：即時讀取與伴讀翻譯 Claude 回覆、中文草稿轉英文 Prompt、固定術語表鎖定與離線 Argos 語言包管理。
- **兩地時鐘（[`apps/two-zone-clock/`](../tools/i-must-use-claude/apps/two-zone-clock/README.md)）**：
  - [WorldClock.cs](../tools/i-must-use-claude/apps/two-zone-clock/WorldClock.cs) 與 [ResetCountdown.cs](../tools/i-must-use-claude/apps/two-zone-clock/ResetCountdown.cs)：常駐顯示雙時區時間與 Claude 5 小時會話／每週額度重置倒計時。
- **大肥魚打包助手（[`apps/whale-pack-helper/`](../tools/i-must-use-claude/apps/whale-pack-helper/README.md)）**：
  - [Scanner.cs](../tools/i-must-use-claude/apps/whale-pack-helper/src/Scanner.cs) 與 [Packaging.cs](../tools/i-must-use-claude/apps/whale-pack-helper/src/Packaging.cs)：本機掃描並過濾無關快取，將程式碼、文件與多媒體拆分為單包嚴格 ≤ 29 MB 的交接壓縮包與清單。
- **共用更新校驗（[shared/UpdateChecker.cs](../tools/i-must-use-claude/shared/UpdateChecker.cs)）**：手動檢查 GitHub Release 並強制核對遠端 SHA-256 摘要，絕不自動覆蓋執行中程式。

---

## 三、PublishOps CrossCast：跨平台發布、預演與安全護欄（`tools/publishops-crosscast/` · v0.16.0 alpha）

- [cli.py](../tools/publishops-crosscast/src/video_publisher/cli.py)、[quick.py](../tools/publishops-crosscast/src/video_publisher/quick.py) 與 [rehearsal.py](../tools/publishops-crosscast/src/video_publisher/rehearsal.py)：`vpub` 命令列入口、一句話快速流程（`prepare`/`go`）與不觸網的 `--dry-run` 預演檢查。
- [safety.py](../tools/publishops-crosscast/src/video_publisher/safety.py)、[language_policy.py](../tools/publishops-crosscast/src/video_publisher/language_policy.py) 與 [publication_review.py](../tools/publishops-crosscast/src/video_publisher/publication_review.py)：安全護欄（日上限、防連發、預警停機、首次雙因子顯式批准 `--confirm` + `--person-approved`）、各平台繁簡英語言與 AI 宣告檢核，以及發布前計劃審閱。
- [process_lock.py](../tools/publishops-crosscast/src/video_publisher/process_lock.py)、[browser_broker.py](../tools/publishops-crosscast/src/video_publisher/browser_broker.py)、[browser_runtime.py](../tools/publishops-crosscast/src/video_publisher/browser_runtime.py) 與 [hidden_desktop.py](../tools/publishops-crosscast/src/video_publisher/hidden_desktop.py)：跨程序互斥與鎖回收、專用瀏覽器會話背景復用／隔離執行期，以及 Windows 隱藏桌面執行隔離。
- [youtube_studio.py](../tools/publishops-crosscast/src/video_publisher/youtube_studio.py)、[youtube_playlists.py](../tools/publishops-crosscast/src/video_publisher/youtube_playlists.py) 與 [x_pages.py](../tools/publishops-crosscast/src/video_publisher/x_pages.py)：YouTube Studio 免費瀏覽器發布與播放清單 CRUD，以及 X（含新版頁面／Media Studio）雙路由適配。
- [bili_cli/main.py](../tools/publishops-crosscast/src/bili_cli/main.py)、[drafts_bilibili.py](../tools/publishops-crosscast/src/video_publisher/drafts_bilibili.py)、[edits_bilibili.py](../tools/publishops-crosscast/src/video_publisher/edits_bilibili.py) 與 [widgets_bilibili.py](../tools/publishops-crosscast/src/video_publisher/widgets_bilibili.py)：隨附的 `bili-cli` 1.2.1 及 B站草稿、已發布稿件編修與互動組件管理。
- [collections.py](../tools/publishops-crosscast/src/video_publisher/collections.py)、[collections_browser.py](../tools/publishops-crosscast/src/video_publisher/collections_browser.py) 與 [read_scheduler.py](../tools/publishops-crosscast/src/video_publisher/read_scheduler.py)：跨平台合集／收藏夾管理、有界並行與獨立分頁檢查點。
- [work_catalog.py](../tools/publishops-crosscast/src/video_publisher/work_catalog.py)、[overview.py](../tools/publishops-crosscast/src/video_publisher/overview.py)、[progress.py](../tools/publishops-crosscast/src/video_publisher/progress.py)、[operations_runs.py](../tools/publishops-crosscast/src/video_publisher/operations_runs.py) 與 [work_delete.py](../tools/publishops-crosscast/src/video_publisher/work_delete.py)：作品認領與數據總覽、進度回報、中斷續傳與單條顯式授權刪稿（YouTube／抖音／Instagram／X 瀏覽器路線；B站不支援）。

---

## 四、本倉庫根目錄可直接執行的工作流腳本

| 程式 | 解決的問題 |
| --- | --- |
| [prioritize-merchants.mjs](../scripts/prioritize-merchants.mjs) | 統一商戶欄位、產生可解釋優先級、提示資料不足。 |
| [summarize-channel.mjs](../scripts/summarize-channel.mjs) | 由實際資料重算人類主導歷史與 AI 系列的描述性統計。 |
| [operations-gate.mjs](../scripts/operations-gate.mjs) | 離線示範唯一執行者、停止條件、到期和去重判斷。 |
| [check-delivery.mjs](../scripts/check-delivery.mjs) | 以串流核對影片大小與 SHA-256，避免下載錯版。 |
| [workflows.test.mjs](../tests/workflows.test.mjs) | 驗證重複執行、錯誤時間、未授權工作、缺失資料等情境。 |

```sh
npm run demo
npm run report
npm test
node scripts/check-delivery.mjs /path/to/downloaded/videos
```

不需 `npm install`；公開示例使用 Node.js 標準函式庫。

[返回實驗室首頁](../README.md)

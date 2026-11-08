# 寫了哪些程式

**繁體中文** · [English](code-map_EN.md)

本倉庫 `tools/` 目錄完整收錄三套核心工程套件的原始碼與自動化測試：
1. **[`tools/ai-memory-keeper/`](../tools/ai-memory-keeper/README.md)**（獨立倉庫 [`whitecarp/ai-memory-keeper`](https://github.com/whitecarp/ai-memory-keeper)）：跨智能體上下文壓縮記憶守護、模型降檔核驗與雙子座星圖網頁工作台。
2. **[`tools/i-must-use-claude/`](../tools/i-must-use-claude/README.md)**（獨立倉庫 [`whitecarp/I-Must-Use-Claude`](https://github.com/whitecarp/I-Must-Use-Claude)）：面向 Claude 日常協作的四合一 Windows 原生工具箱（環境與 DNS 可逆自檢、即時雙語伴讀翻譯、兩地額度時鐘、≤29 MB 上下文打包器）。
3. **[`tools/publishops-crosscast/`](../tools/publishops-crosscast/README.md)**（獨立倉庫 [`whitecarp/publishops-crosscast`](https://github.com/whitecarp/publishops-crosscast)）：跨平台影片發布營運台 `vpub`（保留 0.15.1 歷史快照，最新發行版見獨立倉庫）。

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

## 三、PublishOps CrossCast：跨平台發布與讀取恢復（`tools/publishops-crosscast/`）

- [process_lock.py](../tools/publishops-crosscast/src/video_publisher/process_lock.py)：跨程序互斥與確認程序已退出後的鎖標記回收。
- [browser_session.py](../tools/publishops-crosscast/src/video_publisher/browser_session.py)：透過私有管道復用瀏覽器環境。
- [upload_resume.py](../tools/publishops-crosscast/src/video_publisher/upload_resume.py)：保存上傳會話並以伺服器進度續傳。
- [full_read.py](../tools/publishops-crosscast/src/video_publisher/full_read.py) 與 [full_read_browser.py](../tools/publishops-crosscast/src/video_publisher/full_read_browser.py)：API 分頁原子檢查點、完整樓中樓讀取與官方分頁結束標記核驗。
- [collections.py](../tools/publishops-crosscast/src/video_publisher/collections.py) 與 [collections_browser.py](../tools/publishops-crosscast/src/video_publisher/collections_browser.py)：帳號與 ID 綁定、持久請求、禁止重送及獨立讀回。
- [read_scheduler.py](../tools/publishops-crosscast/src/video_publisher/read_scheduler.py) 與 [playlists.py](../tools/publishops-crosscast/src/video_publisher/playlists.py)：有界平台並行、獨立檢查點與 YouTube 播放列表 CRUD。
- [work_catalog.py](../tools/publishops-crosscast/src/video_publisher/work_catalog.py) 與 [operations_runs.py](../tools/publishops-crosscast/src/video_publisher/operations_runs.py)：作品目錄、即時歸屬核驗、帳號隔離快取與中斷恢復。

---

## 四、本倉庫根目录可直接執行的工作流腳本

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

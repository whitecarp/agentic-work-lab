# 寫了哪些程式

**繁體中文** · [English](code-map_EN.md)

本專案採用人類提出需求、AI 協助編寫、工具驗證與人類審閱的方式。程式能否重跑、失敗時怎麼處理，比單純列出語言或程式碼行數更重要。

## CrossCast 私人收藏集

- `collections_instagram.py`：官方頁面用戶端的持久更名與成員變更。
- `collections_douyin.py`：創作者合集、封面上傳與精確作品增刪。
- `test_collection_members.py`：完整基線、其他成員保留、去重與回應檢查。
- `skills/publishops-crosscast/`：具名 Codex 技能入口。


- [collections.py](../tools/publishops-crosscast/src/video_publisher/collections.py)：帳號與 ID 綁定、持久請求、禁止重送及獨立讀回。
- [collections_browser.py](../tools/publishops-crosscast/src/video_publisher/collections_browser.py)：官方頁面集合控制項、有界觀察及未支援操作的明確回報。
- [test_collections.py](../tools/publishops-crosscast/tests/test_collections.py)：帳號不符、部分清單、回應遺失與重複請求保護。

## CrossCast 的速度與播放列表

- [read_scheduler.py](../tools/publishops-crosscast/src/video_publisher/read_scheduler.py)：有界平台並行、獨立檢查點及逐步進度。
- [playlists.py](../tools/publishops-crosscast/src/video_publisher/playlists.py)：YouTube 列表 CRUD、成員排序、請求去重及讀回。
- [test_operations_012.py](../tools/publishops-crosscast/tests/test_operations_012.py)：並行隔離、訊息恢復、連線復用、列表歸屬及不確定寫入測試。

## CrossCast 的作品接管與巡檢

- [work_catalog.py](../tools/publishops-crosscast/src/video_publisher/work_catalog.py)：作品目錄、即時歸屬核驗與接管記錄。
- [work_discovery_browser.py](../tools/publishops-crosscast/src/video_publisher/work_discovery_browser.py)：官方頁面的有界翻頁／捲動與管理權限核驗。
- [operations_runs.py](../tools/publishops-crosscast/src/video_publisher/operations_runs.py)：帳號隔離快取、變更清單、API 分頁檢查點及中斷恢復。
- [test_operations_011.py](../tools/publishops-crosscast/tests/test_operations_011.py)：快取過期、帳號切換、回執衝突、斷點、續頁與跨平台故障隔離測試。

## 本倉庫可直接執行

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

不需 npm install；公開示例使用 Node.js 標準函式庫。

## 原製作工程中的程式

- **PowerShell 資料採集包裝器 `run-read.ps1`**：統一 JSON 回傳、來源日誌、呼叫間隔和停止條件。
- **Python 單次發布器 `publish-once.py`**：檔案與中繼資料雜湊鎖定、限時預檢、排他建立執行紀錄、先落盤成功回執再做後處理。
- **Python 影片檢查工具**：媒體格式、解碼、立體聲及機器轉寫檢查；沒有把 ASR 當成人耳驗收。
- **HTML／JavaScript 場景與時間軸**：以語音實際時長安排場景、字幕、過場和已混音音訊。

上述為已檢視原始碼或交付紀錄的實際工程內容；原專案包含帳號綁定設定與大量素材，未在此聲稱已完整公開。公開腳本是為本次作品集整理的可重跑示例，與原生產程式分開標示。

[返回實驗室首頁](../README.md)

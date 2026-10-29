# 寫了哪些程式

**繁體中文** · [English](code-map_EN.md)

本專案採用人類提出需求、AI 協助編寫、工具驗證與人類審閱的方式。程式能否重跑、失敗時怎麼處理，比單純列出語言或程式碼行數更重要。

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

# 帳號與營運資料

**繁體中文** · [English](README_EN.md)

**2026-11-14：** 新增 [2026-11-10 至 11-14 補檔暨晚間增量結構化紀錄](production-update-2026-11-14.json) 與更新後的 [公開即時指標快照](metrics/current-metrics.json)（詳見 [2026-11-14 增量更新報告](../docs/updates/2026-11-14.md)）。先前 2026-11-09 紀錄保留於 [production-update-2026-11-09.json](production-update-2026-11-09.json)，2026-11-08 與 2026-11-03 紀錄分別保留於 [production-update-2026-11-08.json](production-update-2026-11-08.json) 與 [production-update-2026-11-03.json](production-update-2026-11-03.json)。下方 2026-10-28 歷史快照保持原口徑與日期。

帳號：[穿睡衣的小白白 · UID 10706217](https://space.bilibili.com/10706217)

## 覆蓋範圍

本次取得公開投稿列表 **46** 筆、創作中心稿件 **55** 筆（兩頁），其中歷史人類主導作品 **45** 筆、目前 AI 系列 **10** 筆。兩個列表的可見範圍不同，分別保存，不為湊齊數字而合併或刪除。

| 資料 | 內容 |
| --- | --- |
| [公開列表 CSV](public-video-snapshot.csv) / [JSON](public-video-snapshot.json) | 標題、時間、播放、評論、彈幕 |
| [創作中心 CSV](creator-video-snapshot.csv) / [JSON](creator-video-snapshot.json) | 所有取得的稿件狀態、排期、播放與互動 |
| [歷史人類營運](human-led-history.csv) | 歷史作品逐片數據 |
| [目前 AI 系列](current-ai-series.csv) | 包含已公開與排期待發布 |
| [每週帳號數據](channel-weekly.json) | 播放、訪客、粉絲變動、播放端與昨日前列影片 |
| [過往 AI 數據快照](historical-ai-analytics.json) | 可取得的完播率、平均觀看與互動 |
| [營運事件](operations-events.json) | 69 筆去識別事件欄位 |
| [排程驗證](scheduled-run-evidence.json) | 真正自動觸發與完成的時間 |

新增資料：[第三期首日觀察](episode03-24h-2026-10-28.json)、[晚間製作與發布狀態](production-update-2026-10-28-evening.json)。下午快照保存在 [snapshot archive](snapshots/2026-10-28-afternoon/creator-video-snapshot.json)。

## 解讀方式

- 指標是接口回傳的快照，並非獨立稽核的流量。
- sampled_at 使用 UTC；平台發布與排期時間保留時區。
- publish_time_raw 保留原始欄位，待發布稿件不能把此欄誤認為已公開時間。
- 歷史稿件的「人類主導」是研究分組，不表示完全未使用 AI。
- 過往快照的 file_modified_at 是檔案來源線索，不保證等於精確採樣時刻。
- 每週帳號數據包含舊作與新作，不能全部歸功於 AI 系列。
- 原始標題與平台狀態文字保留原文；其餘說明為繁體中文與英文。
- 缺失值為空或 null，不改成零；未公開影片的零播放保留平台原值。

本資料包不包含觀眾名單、評論正文、私信、登入資訊或原始客戶端日誌。

## 2026-10-29 增量觀察

另行加入[鯨魚娘02 首日數據](whale-log02-24h-2026-10-29.json)及[兩部第六期交付與排期記錄](production-update-2026-10-29-evening.json)。上方全量帳號快照仍保留 2026-10-28 的採樣日期，本次不是全帳號重新盤點。

## 2026-10-30 狀態更新

[本輪記錄](production-update-2026-10-30-evening.json)只新增兩份本機實驗交付與 GPT 第四期公開狀態，未更新全帳號流量或首日指標。上方統計及歷史人類／AI 系列口徑仍為原採樣快照，不併入不同日期的局部紀錄。

[返回實驗室首頁](../README.md)


## 2026-11-01 交付與驗證更新

[新增記錄](production-update-2026-11-01.json)包含四個影片附件及版本限定的工程驗證；未新增流量樣本，歷史人工／AI 比較維持原口徑。

## 2026-11-02 製作證據

[新增記錄](production-update-2026-11-02.json)包含第七期與直式切片、歷史排期回執及 CrossCast 分項驗證；未新增流量樣本，不改變先前人類主導／AI系列的統計範圍與採樣日期。

## 2026-11-08 階段性成果與必火實證數據

[新增記錄](production-update-2026-11-08.json)收錄 2026-11-04 至 11-07 歸檔之 17 個公開成片版本（AI Memory Keeper v0.3.0 雙語系列、《阿哈的抓馬小曲》、《AI四姐妹搶麥》、《AiGO!!!!! · 迷途算力》長篇動畫 PV、值班小遊戲結局合集 `BV1YL4M6EEek` `P1–P6`）、已完成待發布／按日排期之原片雜湊（`aigo-epilogue-20261108` 及 `P7–P19`），以及截至 `2026-11-08 00:21` 之 B站必火推廣人群包 A/B/C/D 賽馬與 `h50` 放量（`¥12.74/1k`）實測數據。

## 2026-11-09 公開歸檔與受眾分群對照

[新增記錄](production-update-2026-11-09.json)補齊 `2026-11-09` 上線之 3 個公開母帶（`BV145Ws6VEg2`、`BV1YL4M6EEek?p=8`、`BV121W864EVe`）、第 3–5 話預登記校驗碼，以及 `BV1sQ4E6eEhK` 兩輪等權重 6 小時受眾分群（Audience Cohort）A/B 實驗去敏結果（`L2` 21 位點讚相似受眾包兩輪互動密度皆為全場最優，未披露任何投放金額）。

## 2026-11-14 增補歸檔暨晚間增量與公開指標更新

[本輪結構化紀錄](production-update-2026-11-14.json)與[即時指標快照](metrics/current-metrics.json)收錄截至 `2026-11-14 21:06`（UTC+8）經 B站線上逐條核驗公開（`state == 0`）的 **20 個新歸檔成片版本**（含 `19:20` BJT 補檔 `19` 支與今晚 `20:02:08` BJT 準點公開之 [`BV1mWs56JEFd`](https://www.bilibili.com/video/BV1mWs56JEFd) `news-20261114-gemini-4-hydrogen.mp4`，累計 **75 個影片版本 · 5.26 GiB**，分布於 **11 個主要公開 Release**）、**30 支獨立公開 BV 稿件**的刷新數據（頻道累計 **37,475** 粉絲、**3,268,151** 累計播放、**222,048** 累計點讚；Google Cloud 扶持三片矩陣合計 **39,468** 播放、`4,592` 點讚＋收藏）、《大肥魚值班小遊戲》（`BV1YL4M6EEek`）進度更新（`P1–P12` 已公開並完成歸檔，未到期分P `P13–P19` 與儲備正片 `EP07–EP13B` 僅保留元數據，不提前上傳附件）、`EP06SP`／`EP08`／`EP11`／`EP12SP`／`EP13A`／`EP13B` 之 `30fps` 歌聲對齊紀錄、`tools/publishops-crosscast v0.16.0 alpha` 工具升級，以及零投流「12:00 vs 20:00【AI快訊】上線時段觀察」目前已採集之 `8` 條樣本（含排除遲發基準對比的 `BV1ToWL6XEvo` 及 `7` 條準點樣本）。不含任何登入憑證、私訊、觀眾名單或內部錢包／訂單細節。

[返回實驗室首頁](../README.md)

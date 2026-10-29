# 帳號與營運資料

**繁體中文** · [English](README_EN.md)

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

[返回實驗室首頁](../README.md)
